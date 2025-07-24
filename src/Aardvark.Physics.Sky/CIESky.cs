/*
    Aardvark Platform
    Copyright (C) 2006-2025  Aardvark Platform Team
    https://aardvark.graphics
    
    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at
    
        http://www.apache.org/licenses/LICENSE-2.0
    
    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
*/
using Aardvark.Base;
using System.ComponentModel;

namespace Aardvark.Physics.Sky
{
    public enum CIESkyType
    {
        [Description("Standard Overcast Sky")]
        OvercastSky1 = 0,
        [Description("Overcast (steep gradation)")]
        OvercastSky2 = 1,
        [Description("Overcast (moderate gradation)")]
        OvercastSky3 = 2,
        [Description("Overcast (moderate gradation - brighter to sun)")]
        OvercastSky4 = 3,
        [Description("Uniform luminance")]
        UniformSky = 4,
        [Description("Partly Cloudy (brighter to sun)")]
        PartlyCloudedSky1 = 5,
        [Description("Partly Cloudy (bright circumsolar)")]
        PartlyCloudedSky2 = 6,
        [Description("Partly Cloudy (distinct solar corona)")]
        PartlyCloudedSky3 = 7,
        [Description("Partly Cloudy (obscured sun)")]
        PartlyCloudedSky4 = 8,
        [Description("Partly Cloudy (brighter circumsolar)")]
        PartlyCloudedSky5 = 9,
        [Description("White-Blue Sky (distinct solar corona)")]
        ClearSky1 = 10,
        [Description("Standard Clear Sky")]
        ClearSky2 = 11,
        [Description("Standard Clear Sky (polluted atmosphere")]
        ClearSky3 = 12,
        [Description("Cloudless turbid sky")]
        ClearSky4 = 13,
        [Description("White-blue turbid sky")]
        ClearSky5 = 14,
    }

    /// <summary>
    /// CIESky is the implementation of a physically based sky illumination
    /// according the paper CIE GENERAL SKY STANDARD DEFINING LUMINANCE DISTRIBUTIONS .
    /// http://mathinfo.univ-reims.fr/IMG/pdf/other2.pdf.
    /// </summary>
    public class CIESky : Sky, IPhysicalSky
    {
        // CIE Sky model type
        readonly int type;

        // Global Illuminance
        readonly double Gv;

        // Diffuse Illuminance
        readonly double Dv;
        
        // Angular distance between sun and zenith [radians]
        double Zs;
        
        // Solar meridian starting from North (0) over East (PI/2) to South (PI) [radians]
        //double alphaS;
        
        // Elevation angle of sun above the horizon [radians]
        double gammaS;
        
        // Gradiation and Indicatrix Function for zenith (used as a scaling factor)
        double gradationIndicatrixZ;
        
        // Zenith Luminance [kcd/m²]
        double Lz;

        // Turbidity
        double Tv;

        #region Constructor

        /// <summary>
        /// Constructs a CIESky from a sunPosition in spherical coordinates and skyType (0 to 14)
        /// </summary>
        /// <param name="sunPhi">Sun azimuth angle in radians [0..2 PI] 0=2 PI=south; PI/2=west, PI=north, 3 PI/2=east</param>
        /// <param name="sunTheta">Sun zenith angle in radians [0..PI] 0=zenith</param>
        /// <param name="type">CIE sky type</param>
        /// <param name="diffIllu">Optionally specifies the measured diffuse/sky horizontal illuminance (Dv)</param>
        /// <param name="globIllu">Optionally specifies the measured global illuminance (Gv)</param>
        public CIESky(double sunPhi, double sunTheta, CIESkyType type, double diffIllu = -1, double globIllu = -1)
            : this(sunPhi, sunTheta, (int)type, diffIllu, globIllu)
        {
        }

        /// <summary>
        /// Constructs a CIESky from a sunPosition in spherical coordinates and skyType (0 to 14)
        /// </summary>
        /// <param name="sunPhi">Sun azimuth angle in radians [0..2 PI] 0=2 PI=south; PI/2=west, PI=north, 3 PI/2=east</param>
        /// <param name="sunTheta">Sun zenith angle in radians [0..PI] 0=zenith</param>
        /// <param name="type">CIE sky type (0 to 14)</param>
        /// <param name="diffIllu">Optionally specifies the measured diffuse/sky horizontal illuminance (Dv)</param>
        /// <param name="globIllu">Optionally specifies the measured global illuminance (Gv)</param>
        public CIESky(double sunPhi, double sunTheta, int type, double diffIllu = -1, double globIllu = -1)
            : base(sunPhi, sunTheta)
        {
            this.type = type;
            this.Dv = diffIllu;
            this.Gv = globIllu;

            CIESkyInit();
        }
        
        public int Type
        {
            get { return type; }
        }

        #endregion    

        #region Static Members

        private static readonly double[] s_a = [      4.0,    4.0,    1.1,    1.1,    0.0,    0.0,    0.0,    0.0,   -1.0,   -1.0,   -1.0,   -1.0,   -1.0,   -1.0,   -1.0    ];
        private static readonly double[] s_b = [     -0.7,   -0.7,   -0.8,   -0.8,   -1.0,   -1.0,   -1.0,   -1.0,   -0.55,  -0.55,  -0.55,  -0.32,  -0.32,  -0.15,  -0.15   ];
        private static readonly double[] s_c = [      0.0,    2.0,    0.0,    2.0,    0.0,    2.0,    5.0,   10.0,    2.0,    5.0,   10.0,   10.0,   16.0,   16.0,   24.0    ];
        private static readonly double[] s_d = [     -1.0,   -1.5,   -1.0,   -1.5,   -1.0,   -1.5,   -2.5,   -3.0,   -1.5,   -2.5,   -3.0,   -3.0,   -3.0,   -3.0,   -2.8    ];
        private static readonly double[] s_e = [      0.0,    0.15,   0.0,    0.15,   0.0,    0.15,   0.3,    0.45,   0.15,   0.3,    0.45,   0.45,   0.3,    0.3,    0.15   ];


        private static readonly double[] s_Tv   = [     45.0,   20.0,   45.0,   20.0,   45.0,  20.0,   12.0,   10.0,   12.0,   10.0,    4.0,    2.5,    4.5,   5.0,    4.0    ];
        private static readonly double[] s_A    = [      0.0,    0.0,    0.0,    0.0,    0.0,   0.0,   13.27,  10.33,   8.7,    8.28,   5.01,   3.3,    4.76,  4.86,   3.62   ];
        private static readonly double[] s_A1   = [      0.0,    0.0,    0.0,    0.0,    0.0,   0.0,   0.957,   0.83,   0.6,    0.567,  1.44,   1.036,  1.244, 0.881,  0.418  ];
        private static readonly double[] s_A2   = [      0.0,    0.0,    0.0,    0.0,    0.0,   0.0,   1.790,   2.03,   1.5,    2.610, -0.75,   0.710, -0.84,  0.453,  1.95   ];
        private static readonly double[] s_B    = [    54.63,  12.35,   48.3,   12.23,  42.59, 11.84, 21.72,   29.35,  10.34,  18.41,  24.41,  23.0,   27.45, 25.54,  28.08   ];
        private static readonly double[] s_C    = [      1.0,   3.68,   1.0,    3.57,   1.0,   3.53,  4.52,    4.94,   3.45,   4.27,   4.6,    4.43,   4.61,  4.4,    4.13    ];
        private static readonly double[] s_D    = [      0.0,   0.59,   0.0,    0.57,   0.0,   0.55,  0.63,    0.7,     0.5,   0.63,   0.72,   0.74,   0.76,  0.79,   0.79    ];
        private static readonly double[] s_E    = [      0.0,  50.47,   0.0,   44.27,   0.0,  38.78, 34.56,   30.41,   27.47, 24.04,  20.76,  18.52,  16.59, 14.56,  13.0     ];
        private static readonly double[] s_DvEv = [      0.1,   0.18,   0.15,   0.22,   0.20,  0.38,  0.42,    0.41,    0.40,  0.36,   0.23,   0.1,    0.28,  0.28,   0.3     ];

        #endregion

        #region Methods

        #pragma warning disable IDE1006 // Naming Styles

        // luminance gradation function
        // 0 <= Z <=p pi/2 // zenith to horizon
        // phi(pi/2) = 1
        // Zenith angle (Z) [radians]
        private double phi(double A, double B, double Z)
        {
            return 1 + A * Fun.Exp(B / (Z.Cos().Abs() + 0.000001));
        }

        // Standard indicatrices
        // Scattering angle (chi) [radians]
        private double f(double C, double D, double E, double chi) 
        {
            return 1 + C * (Fun.Exp(D * chi.DegreesFromRadians()) - Fun.Exp(D * (Constant.Pi / 2))) + E * chi.Cos() * chi.Cos();
        }

        // Gradation and Indicatrix Function
        // used as a scale factor
        private double gradationIndicatrix(double chi, double Z) 
        {
            return phi(s_a[type], s_b[type], Z) * f(s_c[type], s_d[type], s_e[type], chi);
        }

        #pragma warning restore IDE1006 // Naming Styles

        private void CIESkyInit() 
        {
            // CIE-Formula is not valid for Zs = 0!
            // To avoid critical angle-function values
            var clampAngle = Constant.RadiansPerDegree * 10;

            Zs = Fun.Clamp(SunTheta, clampAngle, Constant.Pi - clampAngle);
            gammaS = Constant.PiHalf - Zs; // < 0 if below horizont [+Pi/2, -Pi/2] zenith -> horizont -> bottom
            //alphaS = (SunPhi+Constant.Pi)%Constant.PiTimesTwo;
            gradationIndicatrixZ = gradationIndicatrix(Zs, 0.0);

            var sinGammaS = Fun.Sin(gammaS);
            var cosGammaS = Fun.Cos(gammaS);

            // direct illuminance
            var Ev = Fun.Max(0.00001, 133.8 * sinGammaS * 1000);   // in lx

            double A;
            double DvEv;
            // in case of global and diffuse illuminance are specified explicitly
            if (Gv >= 0 && Dv >= 0)
            {
                var m = 1 / (sinGammaS + 0.50572 * Fun.Pow(gammaS.DegreesFromRadians() + 6.07995, -1.6364));
                var av = 1 / (9.9 + 0.043 * m);
                // measured Gv and Dv
                var PvEv = Fun.Max(1e-12, (Gv / Ev) - (Dv / Ev));
                Tv = -Fun.Log(PvEv) / (av * m);
                A = s_A1[type] * Tv + s_A2[type];
                DvEv = Dv / Ev; //Lz / (((s_B[type] * Fun.Pow(Fun.Sin(gammaS), s_C[type])) / (Fun.Pow(Fun.Cos(gammaS), s_D[type]))) + s_E[type] * Fun.Sin(gammaS));
            }
            else
            {
                DvEv = s_DvEv[type];
                Tv = s_Tv[type];
                A = s_A[type];
            }

            // Zenith Luminance in [kcd/m²]
            // model 0-5 A is not defined! (overcast sky)

            if (type <= 5 || Tv > 12) 
            {
                // In case of weather data (DvEv) calculated
                // overcast model
                // Division by offset included 1e-6
                Lz = DvEv * (((s_B[type] * Fun.Pow(sinGammaS.Abs(), s_C[type])) / (Fun.Pow(cosGammaS, s_D[type])+ 1e-6)) + s_E[type] * sinGammaS);
            }
            else 
            {
                // In case of weather data (Tv, A) calculated 
                // sunny model (Tv <= 12)
                Lz = A * sinGammaS + 0.7 * (Tv + 1.0) * ((Fun.Pow(sinGammaS.Abs(), s_C[type])) / (Fun.Pow(cosGammaS, s_D[type]) + 1e-6)) + 0.04 * Tv;
            }

            // clamp to 0
            if (Lz < 0) Lz = 0;
        }

        #endregion

        #region IPhysicalSky Members

        /// <summary>
        /// Returns the sky color in linear sRGB fitted to [0, 1]
        /// </summary>
        public C3f GetColor(V3d viewVec)
        {
            return XYZTosRGBScaledToFit(GetRadiance(viewVec) * 0.0006f / 318.0f);
        }

        /// <summary>
        /// returns the sky luminance in cd/m² as XYZ-color in a V3d. 
        /// </summary>
        public C3f GetRadiance(V3d viewVec)
        {
            // Below horizont black... can be CHANGED in future versions
            // if(viewVec.Z < 0) return C3f.Black; -> use wrapping chi function

            // Element paramters
            var phiTheta = PhiThetaFromV3d(viewVec);
            
            // Angular distance between element and zenith [radians]
            var Z = phiTheta.Y;

            // Elevation angle of element above the horizont [radians]
            //var gamma = Constant.PiHalf - Z;
            
            // Element meridian starting from North (0) over East (PI/2) to South (PI) [radians]
            //var alpha = (phiTheta.X + Constant.Pi) % Constant.PiTimesTwo;
            
            // Spherical angle between sun and point [radians]
            var chi = Fun.Acos(Fun.Clamp(SunVec.Dot(viewVec), -1, 1));

            // luminance scale factor (relative luminance in the view direction normalized by the zenith luminance)
            var luminanceScale = gradationIndicatrix(chi, Z) / gradationIndicatrixZ;

            // Luminance [kcd/m²]
            var luminance = Lz * luminanceScale;

            // convert to [cd/m²]
            luminance *= 1000;

            // define color temperature, from blue to white with increasing turbidity
            var colorTemp = Fun.Max(9000.0, 25000.0 - Tv * 360);

            var Yuv = colorTemp.TemperatureToYuvInC3f();
            var Yxy = Yuv.FromYuvToYxy(); // Yxy white point for sRGB [1.0000, 0.3127, 0.3290]
            //Yxy = value.ColTemperatureToYxyInC3f();
            var XYZ = Yxy.FromYxyToXYZ();
            //var temp = XYZ.XYZinC3fToCIERGB(); // http://www.brucelindbloom.com/index.html?Eqn_RGB_XYZ_Matrix.html
            var blue = XYZ * 0.01f;//
            //var testvalue = blue.XYZinC3fToSRGB() * 180;
            
            // add color (random blue)
            //var blue = new C3f(160/255.0, 200/255.0, 250/255.0).SRGBToXYZinC3f();

            return ((float) luminance * blue * (1.0f / blue.G));
        }

        #endregion

        /// <summary>
        /// Computes the sRGB from a XYZ.
        /// </summary>
        public static C3f XYZTosRGBScaledToFit(C3f XYZ)
        {
            C3f rv = XYZ.XYZinC3fToSRGB();
            if (rv.AnyGreater(1.0f))
            {
                float scale = 0.5f, delta = 0.25f;

                for (int i = 0; i < 20; i++)
                {
                    rv = (XYZ * scale).XYZinC3fToSRGB();
                    if (rv.AnyGreater(1.0f))
                        scale -= delta;
                    else
                        scale += delta;
                    delta /= 2;
                }
            }
            return rv.Clamped(0.0f, 1.0f);
        }
    }

    public static class CIESkyExt
    {
        public static bool IsSunVisible(this CIESkyType cieType)
        {
            return (int)cieType >= 6;
        }
    }
}
