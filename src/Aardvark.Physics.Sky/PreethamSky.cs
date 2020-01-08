using Aardvark.Base;

namespace Aardvark.Physics.Sky
{
    /// <summary>
    /// PreethamSky is the implementation of a physically based sky illumination
    /// according the paper p91-preetham.pdf.
    /// </summary>
    public class PreethamSky : Sky, IPhysicalSky
    {
        /// <summary>
        /// Precalculated Yxy luminance values of zenith (computed in PreethamInit())
        /// </summary>
        private double m_Yz;

        /// <summary>
        /// Precalculated Yxy luminance values of zenith (computed in PreethamInit())
        /// </summary>
        private double m_xz;

        /// <summary>
        /// Precalculated Yxy luminance values of zenith (computed in PreethamInit())
        /// </summary>
        private double m_yz;

        /// <summary>
        /// The turbidity value for the PreethamSky. Valid values are positive 
        /// doubles, although too high values can cause distortions.
        /// Recommended values are between 1.9 and 10.0.
        /// </summary>
        private double m_turbidity;

        #region Constructor

        /// <summary>
        /// Constructs a PrethamSky from a sunPosition in spherical coordinates and turbidity.
        /// </summary>
        public PreethamSky(double sunPhi, double sunTheta, double turbidity = 1.9)
            : base(sunPhi, sunTheta)
        {
            m_turbidity = turbidity;

            PreethamInit();
        }

        #endregion    

        #region Static Members

        private static double[] s_ABCDE_Y = { -1.4630, 0.4275, 5.3251, -2.5771, 0.3703 };
        private static double[] s_ABCDET_Y = { 0.1787, -0.3554, -0.0227, 0.1206, -0.0670 };
        private static double[] s_ABCDE_x = { -0.2592, 0.0008, 0.2125, -0.8989, 0.0452 };
        private static double[] s_ABCDET_x = { -0.0193, -0.0665, -0.0004, -0.0641, -0.0033 };
        private static double[] s_ABCDE_y = { -0.2608, 0.0092, 0.2102, -1.6537, 0.0529 };
        private static double[] s_ABCDET_y = { -0.0167, -0.0950, -0.0079, -0.0441, -0.0109 };

        #endregion

        #region Methods

        /// <summary>
        /// Function f as described in the paper.
        /// Paper: p91-preetham.pdf
        /// <param name="A"/>
        /// <param name="B"/>
        /// <param name="C"/>
        /// <param name="D"/>
        /// <param name="E"/>
        /// <param name="theta">dot(view,sun)</param>
        /// <param name="gamma">sun elevation</param>
        /// </summary>
        private double f(
            double A, double B, double C, double D,
            double E, double theta, double gamma)
        {
            double cosG = gamma.Cos();
            double cosT = Fun.Max(theta.Cos(), 0);

            return (1.0 + A * (B / (cosT + 0.001)).Exp()) * 
                   (1.0 + C * (D * gamma).Exp() + E * cosG * cosG);
        }

        /// <summary>
        /// Function F as described in the paper.
        /// Paper: p91-preetham.pdf
        /// </summary>
        private double F
            (double[] ABCDE, double[] ABCDET, double T,
            double theta, double sunTheta, double gamma)
        {
            double A = ABCDE[0] + ABCDET[0] * T;
            double B = ABCDE[1] + ABCDET[1] * T;
            double C = ABCDE[2] + ABCDET[2] * T;
            double D = ABCDE[3] + ABCDET[3] * T;
            double E = ABCDE[4] + ABCDET[4] * T;

            return f(A, B, C, D, E, theta, gamma) / f(A, B, C, D, E, 0, sunTheta);
        }

        /// <summary>
        /// Inits the values of the PreethamSky class by computing all necessary
        /// internal values (Precalculated Yxy color values of zenith). Whenever 
        /// a PreethamSky is constructed the PreethamInit method is called. 
        /// </summary>
        private void PreethamInit()
        {
            var T = m_turbidity;
            var Os = SunTheta;

            var chi = (4.0 / 9.0 - T / 120.0) * (Constant.Pi - 2 * Os);

            // zenith luminance in K cd/m²
            m_Yz = (4.0453 * T - 4.9710) * chi.Tan() - 0.2155 * T + 2.4192;

            //AZT: is necessary for larger Os dependent on T
            if (m_Yz < 0)
                m_Yz = 0;

            var T2 = T * T;
            var Os2 = Os * Os;
            var Os3 = Os2 * Os;

            m_xz = T2 * (Os3 * 0.0017 + Os2 * -0.0037 + Os * 0.0021 + 0.0000) +
                   T * (Os3 * -0.0290 + Os2 * 0.0638 + Os * -0.0320 + 0.0039) +
                        (Os3 * 0.1169 + Os2 * -0.2120 + Os * 0.0605 + 0.2589);

            m_yz = T2 * (Os3 * 0.0028 + Os2 * -0.0061 + Os * 0.0032 + 0.0000) +
                   T * (Os3 * -0.0421 + Os2 * 0.0897 + Os * -0.0415 + 0.0052) +
                        (Os3 * 0.1535 + Os2 * -0.2676 + Os * 0.0667 + 0.2669);

        }

        /// <summary>
        /// phi [0,pi] 0=zenith, theta [0,2pi] 0=south, pi/2 = east
        /// returns the sky luminance in cd/m² as XYZ-color in a V3d. 
        /// </summary>
        public C3d Preetham(V3d viewVec)
        {
            var theta = Fun.Acos(viewVec.Z);
            var gamma = Fun.Acos(Fun.Clamp(V3d.Dot(viewVec, SunVec), -1, 1)); // clamp range: even dot-product of normalized vectors can be >1 caused by numerical inaccuracies -> Acos not defined for >1

            // Yz: zenith luminance in kcd/m²
            var Y = m_Yz * F(s_ABCDE_Y, s_ABCDET_Y, m_turbidity, theta, SunTheta, gamma);
            var x = m_xz * F(s_ABCDE_x, s_ABCDET_x, m_turbidity, theta, SunTheta, gamma);
            var y = m_yz * F(s_ABCDE_y, s_ABCDET_y, m_turbidity, theta, SunTheta, gamma);

            var XYZ = new C3f(Y, x, y).FromYxyToXYZ();

            return XYZ.ToC3d() * 1000; // conversion from kcd/m² to cd/m²
        }

        #endregion

        #region IPhysicalSky Members

        /// <summary>
        /// Returns the sky color in linear sRGB fitted to [0, 1]
        /// </summary>
        public C3f GetColor(V3d viewVec)
        {
            return XYZTosRGBScaledToFit((Preetham(viewVec) * 0.0006 / 318.0).ToC3f());
        }

        /// <summary>
        /// Calculates the radiance for a given direction.
        /// returns the sky luminance in cd/m² as XYZ-color in a V3d. 
        /// </summary>
        public C3f GetRadiance(V3d viewVec)
        {
            return Preetham(viewVec).ToC3f();
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
}
