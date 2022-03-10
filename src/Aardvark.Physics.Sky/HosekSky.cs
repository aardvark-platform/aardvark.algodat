using System;
using Aardvark.Base;

namespace Aardvark.Physics.Sky
{
    /// <summary>
    /// Wraps arhosekskymodelstate_alienworld_alloc_init
    /// NOTE: there are still some errors : 
    ///       1. direct and indirect intensity have to high differences
    ///       2. conversion from spectrum to XYZ not verified
    /// </summary>
    public class AlienWorld : Sky, IPhysicalSky
    {
        readonly ArHosekSkyModelState model_state;

        public AlienWorld(
            double solarPhi,
            double solarTheta,
            double solar_intensity,
            double solar_surface_temperature_kelvin,
            double atmospheric_turbidity,
            double ground_albedo
            )
            : base(solarPhi, solarTheta)
        {
            model_state = new ArHosekSkyModelState(Constant.PiHalf - solarTheta,
                                                   solar_intensity,
                                                   solar_surface_temperature_kelvin,
                                                   atmospheric_turbidity,
                                                   ground_albedo);
        }

        public C3f GetColor(V3d viewVec)
        {
            return HosekSky.XYZTosRGBScaledToFit(GetRadiance(viewVec));
        }

        public C3f GetRadiance(V3d viewVec)
        {
            var theta = Fun.Acos(viewVec.Z);
            var gamma = Fun.Acos(Fun.Clamp(viewVec.Dot(SunVec), -1, 1));

            var spec = new double[41];
            for (int wl = 0; wl < 41; wl++)
            {
                var owl = 380 + wl * 10;
                spec[wl] = model_state.arhosekskymodel_solar_radiance(theta, gamma, owl);
            }

            var specVec = new Vector<double>(spec);
            var cieXVec = new Vector<double>(SpectralData.Ciexyz31X_380_780_10nm);
            var cieYVec = new Vector<double>(SpectralData.Ciexyz31Y_380_780_10nm);
            var cieZVec = new Vector<double>(SpectralData.Ciexyz31Z_380_780_10nm);

            var color = new C3f(cieXVec.DotProduct(specVec),
                           cieYVec.DotProduct(specVec),
                           cieZVec.DotProduct(specVec));

            return color * 0.01f;
        }
    }

    public class HosekSky : Sky, IPhysicalSky
    {
        readonly ArHosekSkyModelState[]  model_states; // using 3 states because ground albedo is RGB
        //C3f m_groundAlbedo;

        public HosekSky(
            double solarPhi,
            double solarTheta,
            double atmospheric_turbidity,
            C3f ground_albedo,
            Col.Format color_format
            )
            : base(solarPhi, solarTheta)
        {
            //m_groundAlbedo = ground_albedo;
            
            model_states = new ArHosekSkyModelState[3].SetByIndex(
                i => new ArHosekSkyModelState(Constant.PiHalf - solarTheta,
                                              atmospheric_turbidity,
                                              ground_albedo[i], color_format));
        }

        public C3f GetColor(V3d viewVec)
        {
            return XYZTosRGBScaledToFit(GetRadiance(viewVec) / 1000 * 0.01f);
        }

        /// <summary>
        /// returns the sky luminance in cd/m² as XYZ-color in a V3d. 
        /// </summary>
        public C3f GetRadiance(V3d viewVec)
        {
            var theta = Fun.Acos(viewVec.Z);
            var gamma = Fun.Acos(Fun.Clamp(viewVec.Dot(SunVec), -1, 1));

            double X = model_states[0].arhosek_tristim_skymodel_radiance(theta, gamma, 0);
            double Y = model_states[1].arhosek_tristim_skymodel_radiance(theta, gamma, 1);
            double Z = model_states[2].arhosek_tristim_skymodel_radiance(theta, gamma, 2);
            return new C3f(X, Y, Z) * 1000; // conversion from kcd/m² to cd/m²
        }

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

    public partial class ArHosekSkyModelState
    {
        //   'blackbody_scaling_factor'
        //
        //   Fudge factor, computed in Mathematica, to scale the results of the
        //   following function to match the solar radiance spectrum used in the
        //   original simulation. The scaling is done so their integrals over the
        //   range from 380.0 to 720.0 nanometers match for a blackbody temperature
        //   of 5800 K.
        //   Which leaves the original spectrum being less bright overall than the 5.8k
        //   blackbody radiation curve if the ultra-violet part of the spectrum is
        //   also considered. But the visible brightness should be very similar.

        const double blackbody_scaling_factor = 3.19992 * 10E-11;

        //   'art_blackbody_dd_value()' function
        //
        //   Blackbody radiance, Planck's formula

        double art_blackbody_dd_value(
                double  temperature,
                double  lambda
                )
        {
            double  c1 = 3.74177 * 10E-17;
            double  c2 = 0.0143878;
            double  value;
    
            value =   ( c1 / ( Fun.Pow( lambda, 5.0 ) ) )
                    * ( 1.0 / ( Fun.Exp( c2 / ( lambda * temperature ) ) - 1.0 ) );

            return value;
        }

        //   'originalSolarRadianceTable[]'
        //
        //   The solar spectrum incident at the top of the atmosphere, as it was used 
        //   in the brute force path tracer that generated the reference results the 
        //   model was fitted to. We need this as the yardstick to compare any altered 
        //   Blackbody emission spectra for alien world stars to.

        //   This is just the data from the Preetham paper, extended into the UV range.

        readonly double[] originalSolarRadianceTable =
        {
             7500.0,
            12500.0,
            21127.5,
            26760.5,
            30663.7,
            27825.0,
            25503.8,
            25134.2,
            23212.1,
            21526.7,
            19870.8
        };
       

        internal double[][]  configs;
        internal double[] radiances;
        internal double turbidity;
        internal double solar_radius;
        internal double[] emission_correction_factor_sky;
        internal double[] emission_correction_factor_sun;
        internal double albedo;
        internal double elevation;

        public const double earth_solar_radius = ( 0.51 * Constant.RadiansPerDegree ) / 2.0;

        // constructor for alien world
        public ArHosekSkyModelState(double solar_elevation,
                                    double solar_intensity,
                                    double solar_surface_temperature_kelvin,
                                    double atmospheric_turbidity,
                                    double ground_albedo)
        {
            turbidity = atmospheric_turbidity;
            albedo = ground_albedo;
            elevation = solar_elevation;
                        
            configs = new double[11][].SetByIndex(i => new double[9]);
            radiances = new double[11];
            emission_correction_factor_sky = new double[11];
            emission_correction_factor_sun = new double[11];

            for (int wl = 0; wl < 11; wl++)
            {
                ArHosekSkyModel_CookConfiguration(
                    datasets[wl],
                    configs[wl],
                    turbidity,
                    albedo,
                    elevation);

                radiances[wl] =
                    ArHosekSkyModel_CookRadianceConfiguration(
                        datasetsRad[wl],
                        turbidity,
                        albedo,
                        elevation
                        );
                //   The wavelength of this band in nanometers

                double owl = (320.0 + 40.0 * wl) * 10E-10;

                //   The original intensity we just computed

                double osr = originalSolarRadianceTable[wl];

                //   The intensity of a blackbody with the desired temperature
                //   The fudge factor described above is used to make sure the BB
                //   function matches the used radiance data reasonably well
                //   in magnitude.

                double nsr =
                      art_blackbody_dd_value(solar_surface_temperature_kelvin, owl)
                    * blackbody_scaling_factor;

                //   Correction factor for this waveband is simply the ratio of
                //   the two.

                emission_correction_factor_sun[wl] = nsr / osr;
            }

            //   We then compute the average correction factor of all wavebands.

            //   Theoretically, some weighting to favour wavelengths human vision is
            //   more sensitive to could be introduced here - think V(lambda). But 
            //   given that the whole effort is not *that* accurate to begin with (we
            //   are talking about the appearance of alien worlds, after all), simple
            //   averaging over the visible wavelenghts (! - this is why we start at
            //   WL #2, and only use 2-11) seems like a sane first approximation.
    
            double  correctionFactor = 0.0;
    
            for (int i = 2; i < 11; i++ )
            {
                correctionFactor += emission_correction_factor_sun[i];
            }
    
            //   This is the average ratio in emitted energy between our sun, and an 
            //   equally large sun with the blackbody spectrum we requested.
    
            //   Division by 9 because we only used 9 of the 11 wavelengths for this
            //   (see above).
    
            double  ratio = correctionFactor / 9.0;

            //   This ratio is then used to determine the radius of the alien sun
            //   on the sky dome. The additional factor 'solar_intensity' can be used
            //   to make the alien sun brighter or dimmer compared to our sun.

            solar_radius = Fun.Sqrt(solar_intensity) * earth_solar_radius 
                        / Fun.Sqrt(ratio);

            //   Finally, we have to reduce the scaling factor of the sky by the
            //   ratio used to scale the solar disc size. The rationale behind this is 
            //   that the scaling factors apply to the new blackbody spectrum, which 
            //   can be more or less bright than the one our sun emits. However, we 
            //   just scaled the size of the alien solar disc so it is roughly as 
            //   bright (in terms of energy emitted) as the terrestrial sun. So the sky 
            //   dome has to be reduced in brightness appropriately - but not in an 
            //   uniform fashion across wavebands. If we did that, the sky colour would
            //   be wrong.
    
            for (int i = 0; i < 11; i++ )
            {
                emission_correction_factor_sky[i] =
                      solar_intensity
                    * emission_correction_factor_sun[i] / ratio;
            }
        }

        public ArHosekSkyModelState(
                double solar_elevation,
                double atmospheric_turbidity,
                double ground_albedo,
                Col.Format color_format)
        {
            solar_radius = earth_solar_radius;
            turbidity    = atmospheric_turbidity;
            albedo       = ground_albedo;
            elevation    = solar_elevation;

            int channel_count = 0;
            double[][] dset = null;
            double[][] dsetrad = null;

            switch (color_format)
            {
                case Col.Format.None: channel_count = 11; dset = datasets; dsetrad = datasetsRad; break;
                case Col.Format.CieXYZ: channel_count = 3; dset = datasetsXYZ; dsetrad = datasetsXYZRad; break;
                case Col.Format.RGB: channel_count = 3; dset = datasetsRGB; dsetrad = datasetsRGBRad; break;
                default: throw new ArgumentException("color format cannot be handled");
            }

            configs = new double[channel_count][].SetByIndex(i => new double[9]);
            radiances = new double[channel_count];
            emission_correction_factor_sky = new double[channel_count];
            emission_correction_factor_sun = new double[channel_count];

            for (int channel = 0; channel < channel_count; channel++)
            {
                ArHosekSkyModel_CookConfiguration(
                    dset[channel],
                    configs[channel],
                    turbidity,
                    albedo,
                    elevation);
                radiances[channel] = 
                    ArHosekSkyModel_CookRadianceConfiguration(
                        dsetrad[channel],
                        turbidity, 
                        albedo,
                        elevation
                        );
                emission_correction_factor_sun[channel] = 1.0;
                emission_correction_factor_sky[channel] = 1.0;
            }
        }

        void ArHosekSkyModel_CookConfiguration(
                double[]    dataset, 
                double[]    config, 
                double      turbidity, 
                double      albedo, 
                double      solar_elevation
                )
        {
            int     elev_matrix; // elev_matrix
        
            int     int_turbidity = (int)turbidity;
            double  turbidity_rem = turbidity - (double)int_turbidity;
        
            solar_elevation = solar_elevation <= 0 ? 0 : Fun.Pow(solar_elevation / Constant.PiHalf, (1.0 / 3.0));
        
            // alb 0 low turb
        
            elev_matrix = ( 9 * 6 * (int_turbidity-1) );
                        
            for (int i = 0; i < 9; ++i)
            {
                config[i] = 
                (1.0-albedo) * (1.0 - turbidity_rem) 
                * ( Fun.Pow(1.0-solar_elevation, 5.0) * dataset[elev_matrix+i]  + 
                   5.0  * Fun.Pow(1.0-solar_elevation, 4.0) * solar_elevation * dataset[elev_matrix+i+9] +
                   10.0*Fun.Pow(1.0-solar_elevation, 3.0)*Fun.Pow(solar_elevation, 2.0) * dataset[elev_matrix+i+18] +
                   10.0*Fun.Pow(1.0-solar_elevation, 2.0)*Fun.Pow(solar_elevation, 3.0) * dataset[elev_matrix+i+27] +
                   5.0*(1.0-solar_elevation)*Fun.Pow(solar_elevation, 4.0) * dataset[elev_matrix+i+36] +
                   Fun.Pow(solar_elevation, 5.0)  * dataset[elev_matrix+i+45]);
            }
        
            // alb 1 low turb
            elev_matrix = (9*6*10 + 9*6*(int_turbidity-1));
            for (int i = 0; i < 9; ++i)
            {
                config[i] += 
                (albedo) * (1.0 - turbidity_rem)
                * ( Fun.Pow(1.0-solar_elevation, 5.0) * dataset[elev_matrix+i]  + 
                   5.0  * Fun.Pow(1.0-solar_elevation, 4.0) * solar_elevation * dataset[elev_matrix+i+9] +
                   10.0*Fun.Pow(1.0-solar_elevation, 3.0)*Fun.Pow(solar_elevation, 2.0) * dataset[elev_matrix+i+18] +
                   10.0*Fun.Pow(1.0-solar_elevation, 2.0)*Fun.Pow(solar_elevation, 3.0) * dataset[elev_matrix+i+27] +
                   5.0*(1.0-solar_elevation)*Fun.Pow(solar_elevation, 4.0) * dataset[elev_matrix+i+36] +
                   Fun.Pow(solar_elevation, 5.0)  * dataset[elev_matrix+i+45]);
            }
        
            if(int_turbidity == 10)
                return;
        
            // alb 0 high turb
            elev_matrix = (9*6*(int_turbidity));
            for (int i = 0; i < 9; ++i)
            {
                config[i] += 
                (1.0-albedo) * (turbidity_rem)
                * ( Fun.Pow(1.0-solar_elevation, 5.0) * dataset[elev_matrix+i]  + 
                   5.0  * Fun.Pow(1.0-solar_elevation, 4.0) * solar_elevation * dataset[elev_matrix+i+9] +
                   10.0*Fun.Pow(1.0-solar_elevation, 3.0)*Fun.Pow(solar_elevation, 2.0) * dataset[elev_matrix+i+18] +
                   10.0*Fun.Pow(1.0-solar_elevation, 2.0)*Fun.Pow(solar_elevation, 3.0) * dataset[elev_matrix+i+27] +
                   5.0*(1.0-solar_elevation)*Fun.Pow(solar_elevation, 4.0) * dataset[elev_matrix+i+36] +
                   Fun.Pow(solar_elevation, 5.0)  * dataset[elev_matrix+i+45]);
            }
        
            // alb 1 high turb
            elev_matrix = (9*6*10 + 9*6*(int_turbidity));
            for (int i = 0; i < 9; ++i)
            {
                config[i] += 
                (albedo) * (turbidity_rem)
                * ( Fun.Pow(1.0-solar_elevation, 5.0) * dataset[elev_matrix+i]  + 
                   5.0  * Fun.Pow(1.0-solar_elevation, 4.0) * solar_elevation * dataset[elev_matrix+i+9] +
                   10.0*Fun.Pow(1.0-solar_elevation, 3.0)*Fun.Pow(solar_elevation, 2.0) * dataset[elev_matrix+i+18] +
                   10.0*Fun.Pow(1.0-solar_elevation, 2.0)*Fun.Pow(solar_elevation, 3.0) * dataset[elev_matrix+i+27] +
                   5.0*(1.0-solar_elevation)*Fun.Pow(solar_elevation, 4.0) * dataset[elev_matrix+i+36] +
                   Fun.Pow(solar_elevation, 5.0)  * dataset[elev_matrix+i+45]);
            }
        }

        double ArHosekSkyModel_CookRadianceConfiguration(
                double[]    dataset, 
                double      turbidity, 
                double      albedo, 
                double      solar_elevation
                )
        {
            int elev_matrix;
                
            int int_turbidity = (int)turbidity;
            double turbidity_rem = turbidity - (double)int_turbidity;
            double res;
            solar_elevation = solar_elevation <= 0 ? 0 : Fun.Pow(solar_elevation / Constant.PiHalf, (1.0 / 3.0));

            // alb 0 low turb
            elev_matrix = (6*(int_turbidity-1));
            res = (1.0-albedo) * (1.0 - turbidity_rem) *
                ( Fun.Pow(1.0-solar_elevation, 5.0) * dataset[elev_matrix+0] +
                 5.0*Fun.Pow(1.0-solar_elevation, 4.0)*solar_elevation * dataset[elev_matrix+1] +
                 10.0*Fun.Pow(1.0-solar_elevation, 3.0)*Fun.Pow(solar_elevation, 2.0) * dataset[elev_matrix+2] +
                 10.0*Fun.Pow(1.0-solar_elevation, 2.0)*Fun.Pow(solar_elevation, 3.0) * dataset[elev_matrix+3] +
                 5.0*(1.0-solar_elevation)*Fun.Pow(solar_elevation, 4.0) * dataset[elev_matrix+4] +
                 Fun.Pow(solar_elevation, 5.0) * dataset[elev_matrix+5]);

            // alb 1 low turb
            elev_matrix = (6*10 + 6*(int_turbidity-1));
            res += (albedo) * (1.0 - turbidity_rem) *
                ( Fun.Pow(1.0-solar_elevation, 5.0) * dataset[elev_matrix+0] +
                 5.0*Fun.Pow(1.0-solar_elevation, 4.0)*solar_elevation * dataset[elev_matrix+1] +
                 10.0*Fun.Pow(1.0-solar_elevation, 3.0)*Fun.Pow(solar_elevation, 2.0) * dataset[elev_matrix+2] +
                 10.0*Fun.Pow(1.0-solar_elevation, 2.0)*Fun.Pow(solar_elevation, 3.0) * dataset[elev_matrix+3] +
                 5.0*(1.0-solar_elevation)*Fun.Pow(solar_elevation, 4.0) * dataset[elev_matrix+4] +
                 Fun.Pow(solar_elevation, 5.0) * dataset[elev_matrix+5]);
            if(int_turbidity == 10)
                return res;

            // alb 0 high turb
            elev_matrix = (6*(int_turbidity));
            res += (1.0-albedo) * (turbidity_rem) *
                ( Fun.Pow(1.0-solar_elevation, 5.0) * dataset[elev_matrix+0] +
                 5.0*Fun.Pow(1.0-solar_elevation, 4.0)*solar_elevation * dataset[elev_matrix+1] +
                 10.0*Fun.Pow(1.0-solar_elevation, 3.0)*Fun.Pow(solar_elevation, 2.0) * dataset[elev_matrix+2] +
                 10.0*Fun.Pow(1.0-solar_elevation, 2.0)*Fun.Pow(solar_elevation, 3.0) * dataset[elev_matrix+3] +
                 5.0*(1.0-solar_elevation)*Fun.Pow(solar_elevation, 4.0) * dataset[elev_matrix+4] +
                 Fun.Pow(solar_elevation, 5.0) * dataset[elev_matrix+5]);

            // alb 1 high turb
            elev_matrix = (6*10 + 6*(int_turbidity));
            res += (albedo) * (turbidity_rem) *
                ( Fun.Pow(1.0-solar_elevation, 5.0) * dataset[elev_matrix+0] +
                 5.0*Fun.Pow(1.0-solar_elevation, 4.0)*solar_elevation * dataset[elev_matrix+1] +
                 10.0*Fun.Pow(1.0-solar_elevation, 3.0)*Fun.Pow(solar_elevation, 2.0) * dataset[elev_matrix+2] +
                 10.0*Fun.Pow(1.0-solar_elevation, 2.0)*Fun.Pow(solar_elevation, 3.0) * dataset[elev_matrix+3] +
                 5.0*(1.0-solar_elevation)*Fun.Pow(solar_elevation, 4.0) * dataset[elev_matrix+4] +
                 Fun.Pow(solar_elevation, 5.0) * dataset[elev_matrix+5]);
            return res;
        }

        public double arhosek_tristim_skymodel_radiance(
                double                  theta, 
                double                  gamma, 
                int                     channel
                )
        {
            return
                ArHosekSkyModel_GetRadianceInternal(
                    configs[channel], 
                    theta, 
                    gamma 
                    ) 
                    * radiances[channel];
        }


        double ArHosekSkyModel_GetRadianceInternal(
            double[]  configuration, 
            double    theta, 
            double    gamma
            )
        {
            double cos_gamma = Fun.Cos(gamma);
            double cos_theta = Fun.Max(Fun.Cos(theta), 0);

            double expM = Fun.Exp(configuration[4] * Fun.Abs(gamma));
            double rayM = cos_gamma*cos_gamma;
            double mieM = (1.0 + rayM) / Fun.Pow((1.0 + configuration[8] * configuration[8] - 2.0 * configuration[8] * cos_gamma), 1.5);
            double zenith = Fun.Sqrt(cos_theta);

            return (1.0 + configuration[0] * Fun.Exp(configuration[1] / (cos_theta + 0.01))) *
                (configuration[2] + configuration[3] * expM + configuration[5] * rayM + configuration[6] * mieM + configuration[7] * zenith);
        }

        public double arhosekskymodel_radiance(
            double                  theta, 
            double                  gamma, 
            double                  wavelength
            )
        {
            int low_wl = (int)((wavelength - 320.0 ) / 40.0);

            if ( low_wl < 0 || low_wl >= 11 )
                return 0.0f;

            double interp = Fun.Frac((wavelength - 320.0 ) / 40.0);

            double val_low = 
                ArHosekSkyModel_GetRadianceInternal(
                    configs[low_wl],
                    theta,
                    gamma
                    )
                    * radiances[low_wl]
                    * emission_correction_factor_sky[low_wl];

            if ( interp < 1e-6 )
                return val_low;

            double result = ( 1.0 - interp ) * val_low;

            if ( low_wl+1 < 11 )
            {
                result +=
                    interp
                        * ArHosekSkyModel_GetRadianceInternal(
                            configs[low_wl+1],
                            theta,
                            gamma
                            )
                        * radiances[low_wl+1]
                        * emission_correction_factor_sky[low_wl+1];
            }

            return result;
        }
        const int pieces = 45;
        const int order = 4;

        double arhosekskymodel_sr_internal(
            int                     turbidity,
            int                     wl,
            double                  elevation
            )
        {
            int pos =
                (int) (Fun.Pow(2.0*elevation / Constant.Pi, 1.0/3.0) * pieces); // floor

            if ( pos > 44 ) pos = 44;
            if (pos < 0) return 0;

            double break_x =
                Fun.Pow(((double) pos / (double) pieces), 3.0) * (Constant.Pi * 0.5);

            double[] solarDatasetsWl = solarDatasets[wl];
            int coefs = (order * pieces * turbidity + order * (pos+1) - 1);

            double res = 0.0;
            double x = elevation - break_x;
            double x_exp = 1.0;

            for (int i = 0; i < order; ++i)
            {
                res += x_exp * solarDatasetsWl[coefs--];
                x_exp *= x;
            }

            return res * emission_correction_factor_sun[wl];
        }

        double arhosekskymodel_solar_radiance_internal2(
            double                  wavelength,
            double                  elevation,
            double                  gamma
            )
        {
            if (wavelength < 320.0 || wavelength > 720.0)
                return 0;

            //assert(
            //    wavelength >= 320.0
            //    && wavelength <= 720.0
            //    && state->turbidity >= 1.0
            //    && state->turbidity <= 10.0
            //    );


            int     turb_low  = (int) turbidity - 1;
            double  turb_frac = turbidity - (double) (turb_low + 1);

            if (turb_low < 0)
            {
                turb_low = 0;
                turb_frac = 0.0;
            }
            else if ( turb_low >= 9 )
            {
                turb_low  = 8;
                turb_frac = 1.0;
            }

            int    wl_low  = (int) ((wavelength - 320.0) / 40.0);
            double wl_frac = Fun.Frac(wavelength/40.0);

            if ( wl_low == 10 )
            {
                wl_low = 9;
                wl_frac = 1.0;
            }

            double direct_radiance =
                ( 1.0 - turb_frac )
                    * (    (1.0 - wl_frac)
                       * arhosekskymodel_sr_internal(
                        turb_low,
                        wl_low,
                        elevation
                        )
                       +   wl_frac
                       * arhosekskymodel_sr_internal(
                        turb_low,
                        wl_low+1,
                        elevation
                        )
                       )
                    +   turb_frac
                    * (    ( 1.0 - wl_frac )
                       * arhosekskymodel_sr_internal(
                        turb_low+1,
                        wl_low,
                        elevation
                        )
                       +   wl_frac
                       * arhosekskymodel_sr_internal(
                        turb_low+1,
                        wl_low+1,
                        elevation
                        )
                       );

            double[] ldCoefficient = new double[6];

            for ( int i = 0; i < 6; i++ )
                ldCoefficient[i] =
                    (1.0 - wl_frac) * limbDarkeningDatasets[wl_low  ][i]
                    +        wl_frac  * limbDarkeningDatasets[wl_low+1][i];

            // sun distance to diameter ratio, squared

            double sol_rad_sin = Fun.Sin(solar_radius);
            double ar2 = 1 / ( sol_rad_sin * sol_rad_sin );
            double singamma = Fun.Sin(gamma);
            double sc2 = 1.0 - ar2 * singamma * singamma;
            if (sc2 < 0.0 ) sc2 = 0.0;
            double sampleCosine = Fun.Sqrt (sc2);

            //   The following will be improved in future versions of the model:
            //   here, we directly use fitted 5th order polynomials provided by the
            //   astronomical community for the limb darkening effect. Astronomers need
            //   such accurate fittings for their predictions. However, this sort of
            //   accuracy is not really needed for CG purposes, so an approximated
            //   dataset based on quadratic polynomials will be provided in a future
            //   release.

            double  darkeningFactor =
                ldCoefficient[0]
                + ldCoefficient[1] * sampleCosine
                    + ldCoefficient[2] * Fun.Pow( sampleCosine, 2.0 )
                    + ldCoefficient[3] * Fun.Pow( sampleCosine, 3.0 )
                    + ldCoefficient[4] * Fun.Pow( sampleCosine, 4.0 )
                    + ldCoefficient[5] * Fun.Pow( sampleCosine, 5.0 );

            direct_radiance *= darkeningFactor;

            return direct_radiance;
        }

        public double arhosekskymodel_solar_radiance(
            double                  theta, 
            double                  gamma, 
            double                  wavelength
            )
        {
            double direct_radiance =
                arhosekskymodel_solar_radiance_internal2(
                    wavelength,
                    ((Constant.Pi/2.0)-theta),
                    gamma
                    );

            double inscattered_radiance =
                arhosekskymodel_radiance(
                    theta,
                    gamma,
                    wavelength
                    );

            return direct_radiance + inscattered_radiance;
        }


    }

}