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

namespace Aardvark.Physics.Sky
{
    /// <summary>
    /// An athmospheric scattering model for sun light simulation.
    /// It is baed on references found in the Preetham paper.
    /// </summary>
    public class SunLightScattering(double sunPhi, double sunTheta, double turbidity = 1.9) : Sky(sunPhi, sunTheta)
    {
        // spectral quantities taken from preetham paper Table 2
        // radiance in W / cm^2 / μm / sr
        static readonly double[] s_sunSpectralRadiance_380_to_780_10nm = [1655.9, 1623.37, 2112.75, 2588.82, 2582.91, 2423.23, 2676.05, 2965.83, 3054.54, 3005.75, 3066.37, 2883.04, 2871.21, 2782.5, 2710.06, 2723.36, 2636.13, 2550.38, 2506.02, 2531.16, 2535.59, 2513.42, 2463.15, 2417.32, 2368.53, 2321.21, 2282.77, 2233.98, 2197.02, 2152.67, 2109.79, 2072.83, 2024.04, 1987.08, 1942.72, 1907.24, 1862.89, 1825.92, 0.0, 0.0, 0.0];
        static readonly double[] s_scatterK = [0.650393, 0.653435, 0.656387, 0.657828, 0.660644, 0.662016, 0.663365, 0.665996, 0.667276, 0.668532, 0.669765, 0.670974, 0.67216, 0.673323, 0.674462, 0.675578, 0.67667, 0.677739, 0.678784, 0.678781, 0.679802, 0.6808, 0.681775, 0.681771, 0.682722, 0.683649, 0.683646, 0.68455, 0.684546, 0.685426, 0.686282, 0.686279, 0.687112, 0.687108, 0.687917, 0.687913, 0.688699, 0.688695, 0.688691, 0.689453, 0.689449];
        static readonly double[] s_sunSpectral_k_o = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.003, 0.006, 0.009, 0.014, 0.021, 0.03, 0.04, 0.048, 0.063, 0.075, 0.085, 0.103, 0.12, 0.12, 0.115, 0.125, 0.12, 0.105, 0.09, 0.079, 0.067, 0.057, 0.048, 0.036, 0.028, 0.023, 0.018, 0.014, 0.011, 0.01, 0.009, 0.007, 0.004, 0.0];
        static readonly double[] s_sunSpectral_k_wa = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.016, 0.024, 0.0125, 1, 0.87, 0.061, 0.001, 1e-05, 1e-05, 0.0006];
        static readonly double[] s_sunSpectral_k_g = [0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 3.0, 0.21, 0.0];

        /// <summary>
        /// The turbidity value for the PreethamSky. Valid values are positive 
        /// doubles, alltough too high values can cause disortions.
        /// Recommended values are between 1.9 and 10.0.
        /// </summary>
        readonly double m_turbidity = turbidity;

        /// <summary>
        /// Simulates the sun color radiance in cd/m^2 in XYZ color space
        /// </summary>
        public C3d GetRadiance()
        {
            // preetham A.1, approximation of optical mass of earth atmosphere for given sun theta
            var m = 1.0 / (SunTheta.Cos() + 0.15 * Fun.Pow(Fun.Max(0, 93.885 - SunTheta.DegreesFromRadians()), -1.253));

            var beta = 0.04608 * m_turbidity - 0.04586;
            var l = 0.35f;
            //var alpha = 1.3f;
            var w = 2.0f;

            var n = 1.0003;         // approximative refraction index of air for visible light

            //var Na = 6.023e23; // Avogadro constant (per mol)
            //var M = 28.96 / 1000; // kg/mol (gramm per mole)
            //var p = 1.225; // kg/m^-3 (gramm per cubic meter) density of air
            var N = 2.545e25;       //var N = Na * p / M; // number of air molecules per unit volume (m^3)

            var pn = 0.035;  // depolarization factor

            var c = (0.6544 * m_turbidity - 0.651) * 1e-16; // dust concentration factor

            var H_0air = 8000;
            var H_0dust = 2000;

            var stepCount = 500;
            var stepDelta = 200; // 500 x 200m

            var sunSkyRadius = (0.54 * Constant.RadiansPerDegree) / 2.0;
            var solidAngle = Constant.PiTimesTwo * (1 - Fun.Cos(sunSkyRadius));

            // approximate absorption of ozone layer, gazes and vapor with optical density of atmosphere
            // rayleight and mie scattering for air and dust is performed more accurately by numerical intergration along the view ray

            var count = s_sunSpectralRadiance_380_to_780_10nm.Length;
            var spectralRadiance = new Vector<double>(count).SetByIndex(i =>
            {
                var lambda = (380 + 10 * i) * 1e-9; // wavelength in meters

                var totalTransmission = 1.0;

                // Preetham A.1

                // rayleigh and mie scattering approximation to inaccurate for sunrise/set

                //Rayleigh scattering
                //var rayleigh = -0.008735 * Fun.Pow(lambda, -4.08 * m); // lambda in micro meters

                //Angstrom
                //var angstrom = -beta * Fun.Pow(lambda, -alpha * m);    // lambda in micro meters

                //ozone (not scattered)
                var ozone = -s_sunSpectral_k_o[i] * l * m;

                //mixed gazes absorption (not scattered)
                var k_g = s_sunSpectral_k_g[i];
                var mixedGazes = (-1.41 * k_g * m) / Fun.Pow(1 + 118.93 * k_g * m, 0.45);

                //water vapor absorption (not scattered)
                var k_wa = s_sunSpectral_k_wa[i];
                var waterVapor = (-0.2385 * k_wa * w * m) / Fun.Pow(1 + 20.07 * k_wa * w * m, 0.45);

                var exponent = ozone + mixedGazes + waterVapor; // + rayleigh + angstrom;
                totalTransmission *= Fun.Exp(exponent);

                // forward scattering probability
                //var rayleighFw = Constant.PiSquared * (n.Square() - 1).Square() / (2 * N * (lambda * 1e-6).Pow(4)) * (6 + 3 * pn) / (6 - 7 * pn) * 2; // 2 == (1 + cos^2 theta)
                //var mieFw = 0.434 * c * (Constant.PiTimesTwo / (lambda * 1e-6)).Square() * 4 / 2;

                var transmissionR = 1.0;
                var transmissionM = 1.0;

                //  Preetham A.3
                var rayleighK = 8 * Constant.Pi.Pow(3) * (n.Square() - 1).Square() / (3 * N * lambda.Pow(4)) * (6 + 3 * pn) / (6 - 7 * pn); // preetham
                var mieK = 0.434 * c * (Constant.PiTimesTwo / lambda).Square() * s_scatterK[i];// *4 / 2;

                for (int j = 0; j < stepCount; j++) // 100km length
                {
                    var h = SunTheta.Cos() * (stepCount - j - 1) * stepDelta; // starting outside

                    var p_air = Fun.Exp(-h / H_0air); // density of air for given height

                    //var rayleighIn = Constant.PiSquared * (n.Square() - 1).Square() / (2 * N_h * lambda.Pow(4)) * (6 + 3 * pn) / (6 - 7 * pn) * 2; // 2 == (1 + cos^2 theta)

                    // Rayleight scattering suitable for clear air (no absoption, just scattring with same wavelength)
                    //var rayleighPhaseFw = 3.0 / 2.0 * 1 / (2 + pn) * ((1 + pn) + (1 - pn) * 1); // forward scattering amount
                    //var rayleighK = 8 * Constant.Pi.Pow(3) / (3 * lambda.Pow(4)) * (n.Square() - 1).Square() / N_h * 3 * (2 + pn) / (6 - 7 * pn);

                    var transmissionAir = Fun.Exp(-rayleighK * p_air * stepDelta); // probability of transmission without scattering

                    transmissionR *= transmissionAir;

                    var p_dust = Fun.Exp(-h / H_0dust); // density of dust for given height

                    var transmissionDust = Fun.Exp(-mieK * p_dust * stepDelta / 100); // conversion to cm? 100 scale the mie scattering quite well, maybe there is some missing unit conversion

                    transmissionM *= transmissionDust;
                }

                totalTransmission *= transmissionR * transmissionM;

                var sunIn = s_sunSpectralRadiance_380_to_780_10nm[i];
                return sunIn * totalTransmission;
            });
            
            var x = spectralRadiance.DotProduct(new Vector<double>(SpectralData.Ciexyz31X_380_780_10nm));
            var y = spectralRadiance.DotProduct(new Vector<double>(SpectralData.Ciexyz31Y_380_780_10nm));
            var z = spectralRadiance.DotProduct(new Vector<double>(SpectralData.Ciexyz31Z_380_780_10nm));

            var spectralColor = new C3d(x, y, z);

            // sunSpectralRadiance: W / cm^2 / μm / sr
            // output: cd / m^2
            // cd = lm * sr

            // 1. convert 1/cm² to 1/m²
            spectralColor *= 10000;

            // 2. 1/μm to 1/10nm spectrum
            spectralColor /= 100;

            // 3. watt to lumen
            spectralColor *= 683; // L = 683 lm / W * Int(spec)

            // the solar radiance is known to be 1.88e9 cd/m^2 or 128000 lx for the average solid angle (in space)
            // the solar luminance at noon 1.6e9 cd/m^2

            // NOTE: watt does not tell anything as spectrum is cut off
            // the solar power is also known to be 1362 W/m^2 (includes all radiation, also non-visible light) on earth 1050 W/m^2
            
            return spectralColor;
        }
    }
}
