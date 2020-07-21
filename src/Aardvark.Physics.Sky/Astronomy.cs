using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aardvark.Physics.Sky
{
    public enum Planet
    {
        Mercury = 0,
        Venus,
        Earth,
        Mars,
        Jupiter,
        Saturn,
        Uranus,
        Neptune,
        Pluto
    }

    public static class Astronomy
    {
        /// <summary>
        /// AU = 149,597,870,700m
        /// </summary>
        public const double AU = 149597870700;

        /// <summary>
        /// speed of light = 299,792,458 m/s
        /// </summary>
        public const double c = 299792458;

        /// <summary>
        /// Julian day of 1st January 2000 12:00 (J2000)
        /// </summary>
        public const double J2000 = 2451545;

        /// <summary>
        /// Julian day per Julian century (average days per year = 365.25)
        /// </summary>
        public const double JulianDaysPerCentury = 36525.0;

        /// <summary>
        /// Conversion factor from Julian days to Julian centuries (1.0 / JulianDaysPerCentury).
        /// </summary>
        public const double JulianCenturiesPerDay = 1.0 / 36525.0;

        /// <summary>
        /// Orbital parameters of all planets in ecliptic coordinate system of the Earth
        /// a: aphelion / size of orbit measured in AU / maximum distance to sun
        /// e: eccentricity (shape of orbit)
        /// i: inclination angle in degrees
        /// ω: omega of perihelion in degrees
        /// Ω: ecliptic longitude in degrees
        /// M0: mean anomaly in degree on 1 January 2000 / orbital position
        /// </summary>
        public static (double, double, double, double, double, double)[] OrbitParameters = new[]
        {
            // a | e | i | ω | Ω | M0
            ( 0.38710, 0.20563,  7.005,  29.125,  48.331, 174.7948), // Mercury 
            ( 0.72333, 0.00677,  3.395,  54.884,  76.680,  50.4161), // Venus   
            ( 1.00000, 0.01671,  0.000, 288.064, 174.873, 357.5291), // Earth   
            ( 1.52368, 0.09340,  1.850, 286.502,  49.558,  19.3730), // Mars    
            ( 5.20260, 0.04849,  1.303, 273.867, 100.464,  20.0202), // Jupiter 
            ( 9.55491, 0.05551,  2.489, 339.391, 113.666, 317.0207), // Saturn  
            (19.21845, 0.04630,  0.773,  98.999,  74.006, 141.0498), // Uranus  
            (30.11039, 0.00899,  1.770, 276.340, 131.784, 256.2250), // Neptune 
            (39.543,   0.2490,  17.140, 113.768, 110.307,  14.8820)  // Pluto
        };

        /// <summary>
        /// n: angle the planet traverses per day in degree
        /// a(1 - e²)
        /// Π: Ω + ω (degrees)
        /// </summary>
        public static (double, double, double)[] DerivedOrbitParameters = new []
        {
            // n | a(1 - e²) | Π
            (4.092317,  0.37073,  77.456 ), // Mercury
            (1.602136,  0.72330, 131.564 ), // Venus   
            (0.985608,  0.99972, 102.937 ), // Earth   
            (0.524039,  1.51039, 336.060 ), // Mars    
            (0.083056,  5.19037,  14.331 ), // Jupiter 
            (0.033371,  9.52547,  93.057 ), // Saturn  
            (0.011698, 19.17725, 173.005 ), // Uranus  
            (0.005965, 30.10796,  48.124 ), // Neptune 
            (0.003964, 37.09129, 224.075 )  // Pluto   
        };

        /// Diameters in meter
        public static double[] PlanetDiameters = new []
        {
              4878000.0, // Mercury
             12104000.0, // Venus   
             12756000.0, // Earth   
              6794000.0, // Mars    
            142984000.0, // Jupiter 
            120536000.0, // Saturn  
             51118000.0, // Uranus  
             49532000.0, // Neptune 
              2370000.0  // Pluto   
        };

        /// <summary>
        /// Gets the diameter of a planet
        /// </summary>
        /// <returns>diameter in meter</returns>
        public static double GetPlanetDiameter(Planet planet)
        {
            return PlanetDiameters[(int)planet];
        }

        /// <summary>
        /// Diameter of the sun in meter = 1,391,400,000
        /// </summary>
        public const double SunDiameter = 1391400000;

        /// <summary>
        /// Diameter of the moon in meter = 3,474,200
        /// </summary>
        public const double MoonDiameter = 3474200;

        /// <summary>
        /// Calculate planet direction (phi, theta) and distance (AU) as seen
        /// from a point on Earth at a given time base on:
        /// https://aa.quae.nl/en/reken/hemelpositie.html#1_12
        /// 
        /// Accuracy:
        /// 
        /// Name | α (degrees) | δ (degrees) | Δ (AU)
        /// Sun	    0.03	    0.01	      0.0000
        /// Mercury	0.09	    0.04	      0.0013
        /// Venus	0.17	    0.05	      0.0008
        /// Mars	0.26	    0.07	      0.0018
        /// Jupiter	0.32	    0.12	      0.0093
        /// Saturn	1.08	    0.43	      0.049
        /// Uranus	1.00	    0.35	      0.047
        /// Neptune	0.68	    0.2	          0.072
        /// 
        /// <param name="planet">planet</param>
        /// <param name="time">date and time</param>
        /// <param name="timeZone">time zone</param>
        /// <param name="longitudeInDegrees">GPS longitude coordinate (east)</param>
        /// <param name="latitudeInDegrees">GPS latitude coordinate</param>
        /// </summary>
        static public (double, double, double) PlanetDirectionAndDistance(Planet planet, DateTime time, int timeZone, double longitudeInDegrees, double latitudeInDegrees)
        {
            var jd = time.ComputeJulianDay() - (timeZone / 24.0);
            return PlanetDirectionAndDistance(planet, jd, longitudeInDegrees, latitudeInDegrees);
        }

        /// <summary>
        /// Calculate planet direction (phi, theta) and distance (AU) as seen
        /// from a point on Earth at a given time base on:
        /// https://aa.quae.nl/en/reken/hemelpositie.html#1_12
        /// 
        /// Accuracy:
        /// 
        /// Name | α (degrees) | δ (degrees) | Δ (AU)
        /// Sun	    0.03	    0.01	      0.0000
        /// Mercury	0.09	    0.04	      0.0013
        /// Venus	0.17	    0.05	      0.0008
        /// Mars	0.26	    0.07	      0.0018
        /// Jupiter	0.32	    0.12	      0.0093
        /// Saturn	1.08	    0.43	      0.049
        /// Uranus	1.00	    0.35	      0.047
        /// Neptune	0.68	    0.2	          0.072
        /// 
        /// <param name="planet">planet</param>
        /// <param name="jd">UTC time in Julian days</param>
        /// <param name="longitudeInDegrees">GPS longitude coordinate (east)</param>
        /// <param name="latitudeInDegrees">GPS latitude coordinate</param>
        /// </summary>
        static public (double, double, double) PlanetDirectionAndDistance(Planet planet, double jd, double longitudeInDegrees, double latitudeInDegrees)
        {
            // 1.-4. rectangular heliocentric ecliptic coordinates of planet
            var coordPlanet = RectangularHeliocentricEclipticCoordinates(planet, jd);

            // 5. rectangular heliocentric ecliptic coordinates of earth
            var coordEarth = RectangularHeliocentricEclipticCoordinates(Planet.Earth, jd);

            // 6. geocentric ecliptic longitude and latitude
            var coord = coordPlanet - coordEarth;
            var d = coord.Length; // distance planet-earth

            var lambda = Fun.Atan2(coord.Y, coord.X);
            var beta = Fun.Asin(coord.Z / d);

            // 7. - 10.
            var (height, azimuth) = GeocentricToObserver(lambda, beta, jd, longitudeInDegrees, latitudeInDegrees);
                        
            var skyTheta = 90.0 * Constant.RadiansPerDegree - height;
            var skyPhi = azimuth;
            
            return (skyPhi, skyTheta, d);
        }

        /// <summary>
        /// Transforms geocentric ecliptic coordinates to equatorial coordinates.
        /// The earths angle of obliquity is approximated as constant.
        /// </summary>
        /// <param name="lambda">longitude in radians</param>
        /// <param name="beta">latitude in radians</param>
        /// <returns>right ascension and declination in radians</returns>
        public static (double, double) GeocentricEclipticToEquatorialCoodinates(double lambda, double beta)
        {
            // earths angle of the obliquity of the ecliptic at the beginning of 2000
            var epsilon = 23.4397 * Constant.RadiansPerDegree;
            return GeocentricEclipticToEquatorialCoodinates(lambda, beta, epsilon);
        }

        /// <summary>
        /// Transforms geocentric ecliptic coordinates to equatorial coordinates using the specified earths angle of obliquity.
        /// </summary>
        /// <param name="lambda">longitude in radians</param>
        /// <param name="beta">latitude in radians</param>
        /// <param name="epsilon">earths angle of obliquity in radians</param>
        /// <returns>right ascension and declination in radians</returns>
        public static (double, double) GeocentricEclipticToEquatorialCoodinates(double lambda, double beta, double epsilon)
        {
            // declination
            var delta = Fun.Asin(beta.Sin() * epsilon.Cos() + beta.Cos() * epsilon.Sin() * lambda.Sin());

            // right ascension
            var alpha = Fun.Atan2(lambda.Sin() * epsilon.Cos() - beta.Tan() * epsilon.Sin(), lambda.Cos());
            
            return (delta, alpha);
        }

        /// <summary>
        /// Transforms geocentric ecliptic coordinates (lambda, beta) to (height, azimuth) as seen by an observer.
        /// 
        /// The transformation is implemented according to:
        /// https://aa.quae.nl/en/reken/hemelpositie.html#1_12
        /// </summary>
        /// <param name="lambda">geocentric longitude angle in radians</param>
        /// <param name="beta">geocentric latitude angle in radians</param>
        /// <param name="jd">UTC time in Julian days</param>
        /// <param name="longitude">Observer GPS longitude coordinates (east)</param>
        /// <param name="latitude">Observer GPS latitude coordinates</param>
        /// <returns>height and azimuth in radians</returns>
        public static (double, double) GeocentricToObserver(double lambda, double beta, double jd, double longitude, double latitude)
        {
            // earths angle of obliquity
            var epsilon = GetEarthMeanObliquityAA2010(jd);

            var (delta, alpha) = GeocentricEclipticToEquatorialCoodinates(lambda, beta, epsilon);

            // 8. sidearl time
            var phi = latitude * Constant.RadiansPerDegree;
            var lw = -longitude * Constant.RadiansPerDegree; // convert East to West

            var theta =
                280.1470 * Constant.RadiansPerDegree +
                360.9856235 * Constant.RadiansPerDegree *
                (jd - J2000) - lw;

            // 9. hour angle: how far (hours) the object has passed beyond the celestial meridian
            var H = theta - alpha;

            // 10. height and azimuth
            double sinPhi = phi.Sin();
            double cosPhi = phi.Cos();
            double sinDelta = delta.Sin();
            double cosDelta = delta.Cos();
            double sinH = H.Sin();
            double cosH = H.Cos();

            //altitude h: 0° at horizon, 90° at zenith => theta
            double h = Fun.Asin(sinPhi * sinDelta + cosPhi * cosDelta * cosH);
            double A = Fun.Atan2(sinH, cosH * sinPhi - delta.Tan() * cosPhi);

            return (h, A);
        }

        /// <summary>
        /// Transform a direction of an object in the sky expressed by height and azimuth to spherical coordinates as defined in Aardvark.
        /// Azimuth and phi have the same notation. Height is 0° at the horizon while in spherical coordinates pi/2.
        /// height -> theta
        /// azimuth -> phi
        /// </summary>
        /// <param name="height">height in radians</param>
        /// <param name="azimuth">azimuth in radians</param>
        /// <returns>phi and theta</returns>
        public static SphericalCoordinate SkyToSpherical(double height, double azimuth)
        {
            var theta = Constant.PiHalf - height;
            var phi = azimuth;
            return new SphericalCoordinate(theta, phi);
        }

        static M33d RotationZ(double a)
        {
            var ca = Fun.Cos(a);
            var sa = Fun.Sin(a);
            return new M33d(ca, sa, 0,
                           -sa, ca, 0,
                             0,  0, 1);
        }

        static M33d RotationY(double a)
        {
            var ca = Fun.Cos(a);
            var sa = Fun.Sin(a);
            return new M33d(ca, 0, -sa,
                             0, 1,   0,
                            sa, 0,  ca);
        }

        static M33d RotationX(double a)
        {
            var ca = Fun.Cos(a);
            var sa = Fun.Sin(a);
            return new M33d(1,   0,   0, 
                            0,  ca,  sa, 
                            0, -sa,  ca);
        }

        public static (double, double) CalcNutation(double jd)
        {
            // difference from J2000.0 in Julian centuries
            var T = (jd - J2000) * JulianCenturiesPerDay;

            var T2 = T * T;
            var T3 = T2 * T;
            
            var r = 1296000; // 360° in arcsec
            // Moon's mean anomaly
            var alpha1 = (485866.733 + (1325 * r + 715922.633) * T + 31.310 * T2 + 0.064 * T3) / 3600 * Constant.RadiansPerDegree;

            //Sun's mean anomaly
            var alpha2 = (1287099.804 + (99 * r + 1292581.224) * T - 0.577 * T2 - 0.012 * T3) / 3600 * Constant.RadiansPerDegree;

            //Moon's mean argument of latitude
            var alpha3 = (335778.877 + (1342 * r + 295263.137) * T - 13.257 * T2 + 0.011 * T3) / 3600 * Constant.RadiansPerDegree;

            // Moon's mean elongation from the sun
            var alpha4 = (1072261.307 + (1236 * r + 1105601.328) * T - 6.891 * T2 + 0.019 * T3) / 3600 * Constant.RadiansPerDegree;

            //Mean longitude of the ascending lunar node
            var alpha5 = (450160.280 - (5 * r + 482890.539) * T + 7.455 * T2 + 0.008 * T3) / 3600 * Constant.RadiansPerDegree;

            var alphas = new[] { alpha1, alpha2, alpha3, alpha4, alpha5 };

            var (delta_psi, delta_epsilon) = NutationModelCoefficients.Aggregate((0.0, 0.0), (x, row) =>
            {
                var alphaSum = alphas.Zip(row.Take(5), (a, b) => a * b).Sum();
                var r_psi = (row[6] * 1e-4 + row[7] * T) * Fun.Sin(alphaSum);
                var r_epsilon = (row[8] * 1e-4 + row[9] * T) * Fun.Cos(alphaSum);
                return (x.Item1 + r_psi, x.Item2 + r_epsilon);
            });

            delta_psi = delta_psi / 3600 * Constant.RadiansPerDegree;
            delta_epsilon = delta_epsilon / 3600 * Constant.RadiansPerDegree;

            return (delta_psi, delta_epsilon);
        }
        
        /// <summary>
        /// Gets the earth mean angle of obliquity for a given date.
        /// A 3rd order fit is used for the approximation:
        /// DE200 from the Jet Propulsion Laboratory's DE series (1984)
        /// </summary>
        /// <param name="jd">UTC time in Julian days</param>
        /// <returns>Earths angle obliquity in radians</returns>
        public static double GetEarthMeanObliquityDE200(double jd)
        {
            // difference from J2000.0 in Julian centuries
            var T = (jd - J2000) * JulianCenturiesPerDay;

            var T2 = T * T;
            var T3 = T2 * T;

            // earths angle of the obliquity of the ecliptic at the beginning of 2000
            // epsilon = 23°26′21′′448
            var epsilonJ2000arcsec = 23 * 3600 + 26 * 60 + 21.448;
            var epsilonArcsec = epsilonJ2000arcsec - 46.8150 * T - 0.00059 * T2 + 0.001813 * T3;

            // convert arcseconds to radians
            return epsilonArcsec / 3600 * Constant.RadiansPerDegree;
        }

        /// <summary>
        /// Gets the earth mean angle of obliquity for a given date.
        /// A 5rd order fit from the Astronomical Almanac (2010) is used for the approximation.
        /// </summary>
        /// <param name="jd">UTC time in Julian days</param>
        /// <returns>Earths angle obliquity in radians</returns>
        public static double GetEarthMeanObliquityAA2010(double jd)
        {
            // difference from J2000.0 in Julian centuries
            var T = (jd - J2000) * JulianCenturiesPerDay;

            var T2 = T * T;
            var T3 = T2 * T;
            var T4 = T2 * T2;
            var T5 = T3 * T2;

            // epsilon = 23°26′21′′406
            var epsilonJ2000arcsec = 23 * 3600 + 26 * 60 + 21.406;
            var epsilonArcsec = epsilonJ2000arcsec - 46.836769 * T - 0.0001831 * T2 + 0.00200340 * T3 - 5.76e-7 * T4 - 4.34e-8* T5;

            // convert arcseconds to radians
            return epsilonArcsec / 3600 * Constant.RadiansPerDegree;
        }

        /// <summary>
        /// Gets the earth mean angle of obliquity for a given date.
        /// A 10rd order fit for long term approximations from Laskar 1986 is used for the approximation,
        /// good to 0.02″ over 1000 years and several arcseconds over 10,000 years.
        /// </summary>
        /// <param name="jd">UTC time in Julian days</param>
        /// <returns>Earths angle obliquity in radians</returns>
        public static double GetEarthMeanObliquityLaskar(double jd)
        {
           // difference from J2000.0 in multiples of 10000 years
           var T = (jd - J2000) * JulianCenturiesPerDay * 100;

            var T2 = T * T;
            var T3 = T2 * T;
            var T4 = T2 * T2;
            var T5 = T3 * T2;
            var T6 = T3 * T3;
            var T7 = T4 * T3;
            var T8 = T4 * T4;
            var T9 = T5 * T4;
            var T10 = T5 * T5;

            // earths angle of the obliquity of the ecliptic at the beginning of 2000
            // epsilon = 23°26′21′′448
            var epsilonJ2000arcsec = 23 * 3600 + 26 * 60 + 21.448;
            var epsilonArcsec = epsilonJ2000arcsec - 4680.93 * T - 1.55 * T2 + 1999.25 * T3 - 51.38 * T4 - 249.67 * T5 - 39.05 * T6 + 7.12 * T7 + 27.87 * T8 + 5.79 * T9 + 2.45 * T10;

            // convert arcseconds to radians
            return epsilonArcsec / 3600 * Constant.RadiansPerDegree;
        }

        public static M33d BuildNutationTransform(double jd)
        {
            var epsilon = GetEarthMeanObliquityAA2010(jd);

            var (delta_psi, delta_epsilon) = CalcNutation(jd);

            // nutation
            return RotationX(-(epsilon + delta_epsilon)) * RotationZ(-delta_psi) * RotationX(epsilon);
        }

        public static M33d BuildPrecessionTransform(double jd)
        {
            // difference from J2000.0 in Julian centuries
            var T = (jd - J2000) * JulianCenturiesPerDay;

            var T2 = T * T;
            var T3 = T2 * T;

            // arcseconds -> radians
            var z = (2306.2181 * T + 1.09468 * T2 + 0.018203 * T3) / 3600 * Constant.RadiansPerDegree;
            var upsilon = (2004.3109 * T - 0.42665 * T2 - 0.041833 * T3) / 3600 * Constant.RadiansPerDegree;
            var zeta = (2306.2181 * T + 0.30188 * T2 + 0.017998 * T3) / 3600 * Constant.RadiansPerDegree;

            // precession
            return RotationZ(-z) * RotationY(upsilon) * RotationZ(-zeta);
        }

        /// <summary>
        /// Builds a transformation matrix from International Celestial Reference Frame (ICRF) to
        /// Celestial Ephemeris Pole (CEP) coordinates accounting for Precession and Nutation effects.
        /// https://gssc.esa.int/navipedia/index.php/ICRF_to_CEP
        /// </summary>
        static public M33d ICRFtoCEP(double jd)
        {
            var P = BuildPrecessionTransform(jd);
            var N = BuildNutationTransform(jd);

            return N * P;
        }

        // IAU1980 Theory of Nutation model
        //         |      Acoeff 1..5      |  Period  |    A0j   A1j   |    B0j   B1j |
        //         | ki1 ki2 ki3 ki4 ki5   |  (days)  |  x10-e4   ''   |  x10-e4   '' |
        static double[][] NutationModelCoefficients =
        {
            new [] { 0  , 0   ,0   ,0   ,1   , -6798.4, -171996, -174.2, 92025, 8.9   },
            new [] { 0  , 0   ,2   ,-2  ,2   , 182.6  , -13187 , -1.6  , 5736 ,  -3.1 },
            new [] { 0  , 0   ,2   ,0   ,2   , 13.7   , -2274  , -0.2  , 977  ,  -0.5 },
            new [] { 0  , 0   ,0   ,0   ,2   , -3399.2, 2062   , 0.2   , -895 ,  0.5  },
            new [] { 0  , -1  ,0   ,0   ,0   , -365.3 , -1426  , 3.4   , 54   ,  -0.1 },
            new [] { 1  , 0   ,0   ,0   ,0   , 27.6   , 712    , 0.1   , -7   ,  0.0  },
            new [] { 0  , 1   ,2   ,-2  ,2   , 121.7  , -517   , 1.2   , 224  ,  -0.6 },
            new [] { 0  , 0   ,2   ,0   ,1   , 13.6   , -386   , -0.4  , 200  ,  0.0  },
            new [] { 1  , 0   ,2   ,0   ,2   , 9.1    , -301   , 0     , 129  ,  -0.1 },
            new [] { 0  , -1  ,2   ,-2  ,2   , 365.2  , 217    , -0.5  , -95  ,  0.3  },
            new [] { -1 , 0   ,0   ,2   ,0   , 31.8   , 158    , 0     , -1   ,  0.0  },
            new [] { 0  , 0   ,2   ,-2  ,1   , 177.8  , 129    , 0.1   , -70  ,  0.0  },
            new [] { -1 , 0   ,2   ,0   ,2   , 27.1   , 123    , 0     , -53  ,  0.0  },
            new [] { 1  , 0   ,0   ,0   ,1   , 27.7   , 63     , 0.1   , -33  ,  0.0  },
            new [] { 0  , 0   ,0   ,2   ,0   , 14.8   , 63     , 0     , -2   ,  0.0  },
            new [] { -1 , 0   ,2   ,2   ,2   , 9.6    , -59    , 0     , 26   ,  0.0  },
            new [] { -1 , 0   ,0   ,0   ,1   , -27.4  , -58    , -0.1  , 32   ,  0.0  },
            new [] { 1  , 0   ,2   ,0   ,1   , 9.1    , -51    , 0     , 27   ,  0.0  },
            new [] { -2 , 0   ,0   ,2   ,0   , -205.9 , -48    , 0     , 1    ,  0.0  },
            new [] { -2 , 0   ,2   ,0   ,1   , 1305.5 , 46     , 0     , -24  ,  0.0  },
            new [] { 0  , 0   ,2   ,2   ,2   , 7.1    , -38    , 0     , 16   ,  0.0  },
            new [] { 2  , 0   ,2   ,0   ,2   , 6.9    , -31    , 0     , 13   ,  0.0  },
            new [] { 2  , 0   ,0   ,0   ,0   , 13.8   , 29     , 0     , -1   ,  0.0  },
            new [] { 1  , 0   ,2   ,-2  ,2   , 23.9   , 29     , 0     , -12  ,  0.0  },
            new [] { 0  , 0   ,2   ,0   ,0   , 13.6   , 26     , 0     , -1   ,  0.0  },
            new [] { 0  , 0   ,2   ,-2  ,0   , 173.3  , -22    , 0     , 0    ,  0.0  },
            new [] { -1 , 0   ,2   ,0   ,1   , 27     , 21     , 0     , -10  ,  0.0  },
            new [] { 0  , 2   ,0   ,0   ,0   , 182.6  , 17     , -0.1  , 0    ,  0.0  },
            new [] { 0  , 2   ,2   ,-2  ,2   , 91.3   , -16    , 0.1   , 7    ,  0.0  },
            new [] { -1 , 0   ,0   ,2   ,1   , 32     , 16     , 0     , -8   ,  0.0  },
            new [] { 0  , 1   ,0   ,0   ,1   , 386    , -15    , 0     , 9    ,  0.0  },
            new [] { 1  , 0   ,0   ,-2  ,1   , -31.7  , -13    , 0     , 7    ,  0.0  },
            new [] { 0  , -1  ,0   ,0   ,1   , -346.6 , -12    , 0     , 6    ,  0.0  },
            new [] { 2  , 0   ,-2  ,0   ,0   , -1095.2, 11     , 0     , 0    ,  0.0  },
            new [] { -1 , 0   ,2   ,2   ,1   , 9.5    , -10    , 0     , 5    ,  0.0  },
            new [] { 1  , 0   ,2   ,2   ,2   , 5.6    , -8     , 0     , 3    ,  0.0  },
            new [] { 0  , -1  ,2   ,0   ,2   , 14.2   , -7     , 0     , 3    ,  0.0  },
            new [] { 0  , 0   ,2   ,2   ,1   , 7.1    , -7     , 0     , 3    ,  0.0  },
            new [] { 1  , 1   ,0   ,-2  ,0   , -34.8  , -7     , 0     , 0    ,  0.0  },
            new [] { 0  , 1   ,2   ,0   ,2   , 13.2   , 7      , 0     , -3   ,  0.0  },
            new [] { -2 , 0   ,0   ,2   ,1   , -199.8 , -6     , 0     , 3    ,  0.0  },
            new [] { 0  , 0   ,0   ,2   ,1   , 14.8   , -6     , 0     , 3    ,  0.0  },
            new [] { 2  , 0   ,2   ,-2  ,2   , 12.8   , 6      , 0     , -3   ,  0.0  },
            new [] { 1  , 0   ,0   ,2   ,0   , 9.6    , 6      , 0     , 0    ,  0.0  },
            new [] { 1  , 0   ,2   ,-2  ,1   , 23.9   , 6      , 0     , -3   ,  0.0  },
            new [] { 0  , 0   ,0   ,-2  ,1   , -14.7  , -5     , 0     , 3    ,  0.0  },
            new [] { 0  , -1  ,2   ,-2  ,1   , 346.6  , -5     , 0     , 3    ,  0.0  },
            new [] { 2  , 0   ,2   ,0   ,1   , 6.9    , -5     , 0     , 3    ,  0.0  },
            new [] { 1  , -1  ,0   ,0   ,0   , 29.8   , 5      , 0     , 0    ,  0.0  },
            new [] { 1  , 0   ,0   ,-1  ,0   , 411.8  , -4     , 0     , 0    ,  0.0  },
            new [] { 0  , 0   ,0   ,1   ,0   , 29.5   , -4     , 0     , 0    ,  0.0  },
            new [] { 0  , 1   ,0   ,-2  ,0   , -15.4  , -4     , 0     , 0    ,  0.0  },
            new [] { 1  , 0   ,-2  ,0   ,0   , -26.9  , 4      , 0     , 0    ,  0.0  },
            new [] { 2  , 0   ,0   ,-2  ,1   , 212.3  , 4      , 0     , -2   ,  0.0  },
            new [] { 0  , 1   ,2   ,-2  ,1   , 119.6  , 4      , 0     , -2   ,  0.0  },
            new [] { 1  , 1   ,0   ,0   ,0   , 25.6   , -3     , 0     , 0    ,  0.0  },
            new [] { 1  , -1  ,0   ,-1  ,0   , -3232.9, -3     , 0     , 0    ,  0.0  },
            new [] { -1 , -1  ,2   ,2   ,2   , 9.8    , -3     , 0     , 1    ,  0.0  },
            new [] { 0  , -1  ,2   ,2   ,2   , 7.2    , -3     , 0     , 1    ,  0.0  },
            new [] { 1  , -1  ,2   ,0   ,2   , 9.4    , -3     , 0     , 1    ,  0.0  },
            new [] { 3  , 0   ,2   ,0   ,2   , 5.5    , -3     , 0     , 1    ,  0.0  },
            new [] { -2 , 0   ,2   ,0   ,2   , 1615.7 , -3     , 0     , 1    ,  0.0  },
            new [] { 1  , 0   ,2   ,0   ,0   , 9.1    , 3      , 0     , 0    ,  0.0  },
            new [] { -1 , 0   ,2   ,4   ,2   , 5.8    , -2     , 0     , 1    ,  0.0  },
            new [] { 1  , 0   ,0   ,0   ,2   , 27.8   , -2     , 0     , 1    ,  0.0  },
            new [] { -1 , 0   ,2   ,-2  ,1   , -32.6  , -2     , 0     , 1    ,  0.0  },
            new [] { 0  , -2  ,2   ,-2  ,1   , 6786.3 , -2     , 0     , 1    ,  0.0  },
            new [] { -2 , 0   ,0   ,0   ,1   , -13.7  , -2     , 0     , 1    ,  0.0  },
            new [] { 2  , 0   ,0   ,0   ,1   , 13.8   , 2      , 0     , -1   ,  0.0  },
            new [] { 3  , 0   ,0   ,0   ,0   , 9.2    , 2      , 0     , 0    ,  0.0  },
            new [] { 1  , 1   ,2   ,0   ,2   , 8.9    , 2      , 0     , -1   ,  0.0  },
            new [] { 0  , 0   ,2   ,1   ,2   , 9.3    , 2      , 0     , -1   ,  0.0  },
            new [] { 1  , 0   ,0   ,2   ,1   , 9.6    , -1     , 0     , 0    ,  0.0  },
            new [] { 1  , 0   ,2   ,2   ,1   , 5.6    , -1     , 0     , 1    ,  0.0  },
            new [] { 1  , 1   ,0   ,-2  ,1   , -34.7  , -1     , 0     , 0    ,  0.0  },
            new [] { 0  , 1   ,0   ,2   ,0   , 14.2   , -1     , 0     , 0    ,  0.0  },
            new [] { 0  , 1   ,2   ,-2  ,0   , 117.5  , -1     , 0     , 0    ,  0.0  },
            new [] { 0  , 1   ,-2  ,2   ,0   , -329.8 , -1     , 0     , 0    ,  0.0  },
            new [] { 1  , 0   ,-2  ,2   ,0   , 23.8   , -1     , 0     , 0    ,  0.0  },
            new [] { 1  , 0   ,-2  ,-2  ,0   , -9.5   , -1     , 0     , 0    ,  0.0  },
            new [] { 1  , 0   ,2   ,-2  ,0   , 32.8   , -1     , 0     , 0    ,  0.0  },
            new [] { 1  , 0   ,0   ,-4  ,0   , -10.1  , -1     , 0     , 0    ,  0.0  },
            new [] { 2  , 0   ,0   ,-4  ,0   , -15.9  , -1     , 0     , 0    ,  0.0  },
            new [] { 0  , 0   ,2   ,4   ,2   , 4.8    , -1     , 0     , 0    ,  0.0  },
            new [] { 0  , 0   ,2   ,-1  ,2   , 25.4   , -1     , 0     , 0    ,  0.0  },
            new [] { -2 , 0   ,2   ,4   ,2   , 7.3    , -1     , 0     , 1    ,  0.0  },
            new [] { 2  , 0   ,2   ,2   ,2   , 4.7    , -1     , 0     , 0    ,  0.0  },
            new [] { 0  , -1  ,2   ,0   ,1   , 14.2   , -1     , 0     , 0    ,  0.0  },
            new [] { 0  , 0   ,-2  ,0   ,1   , -13.6  , -1     , 0     , 0    ,  0.0  },
            new [] { 0  , 0   ,4   ,-2  ,2   , 12.7   , 1      , 0     , 0    ,  0.0  },
            new [] { 0  , 1   ,0   ,0   ,2   , 409.2  , 1      , 0     , 0    ,  0.0  },
            new [] { 1  , 1   ,2   ,-2  ,2   , 22.5   , 1      , 0     , -1   ,  0.0  },
            new [] { 3  , 0   ,2   ,-2  ,2   , 8.7    , 1      , 0     , 0    ,  0.0  },
            new [] { -2 , 0   ,2   ,2   ,2   , 14.6   , 1      , 0     , -1   ,  0.0  },
            new [] { -1 , 0   ,0   ,0   ,2   , -27.3  , 1      , 0     , -1   ,  0.0  },
            new [] { 0  , 0   ,-2  ,2   ,1   , -169   , 1      , 0     , 0    ,  0.0  },
            new [] { 0  , 1   ,2   ,0   ,1   , 13.1   , 1      , 0     , 0    ,  0.0  },
            new [] { -1 , 0   ,4   ,0   ,2   , 9.1    , 1      , 0     , 0    ,  0.0  },
            new [] { 2  , 1   ,0   ,-2  ,0   , 131.7  , 1      , 0     , 0    ,  0.0  },
            new [] { 2  , 0   ,0   ,2   ,0   , 7.1    , 1      , 0     , 0    ,  0.0  },
            new [] { 2  , 0   ,2   ,-2  ,1   , 12.8   , 1      , 0     , -1   ,  0.0  },
            new [] { 2  , 0   ,-2  ,0   ,1   , -943.2 , 1      , 0     , 0    ,  0.0  },
            new [] { 1  ,-1   ,0   ,-2  ,0   , -29.3  , 1      , 0     , 0    ,  0.0  },
            new [] { -1 , 0   ,0   ,1   ,1   , -388.3 , 1      , 0     , 0    ,  0.0  },
            new [] { -1 ,-1   ,0   ,2   ,1   , 35     , 1      , 0     , 0    ,  0.0  },
            new [] { 0  , 1   ,0   ,1   ,0   , 27.3   , 1      , 0     , 0    ,  0.0  }
        };

        /// <summary>
        /// Builds the transformation matrix from Celestial Ephemeris Pole (CEP) to 
        /// International Terrestrial Reference Frame (ITRF) coordinates for the given 
        /// date in Julian days (jd) and the Earth Orientation Parameters (EOP) xp and yp.
        /// https://gssc.esa.int/navipedia/index.php/CEP_to_ITRF
        /// 
        /// The ITRS is a reference system co-rotating with the Earth in its diurnal motion in space.
        /// The origin is at the earth's center of mass.
        /// The Z-axis is the earths rotation axis.
        /// The X-axis is the intersection of the orthogonal plane to the Z-axis and the Greenwich mean meridian.
        /// The Y-axis is orthogonal to the Z- and X-axis.
        /// </summary>
        /// <param name="jd">date in Julian days</param>
        /// <param name="xp">earth orientation parameter xp in radians</param>
        /// <param name="yp">earth orientation parameter yp in radians</param>
        /// <returns></returns>
        public static M33d CEPtoITRF(double jd, double xp, double yp)
        {
            // rotation around the CEP pole 
            var Tu = (jd - J2000) * JulianCenturiesPerDay;
            var Tu2 = Tu * Tu;
            var Tu3 = Tu2 * Tu;
            
            // GMST at 0h (sidereal time) / orientation relative to stars at 0h of given date
            var theta_G0_sec = (6 * 3600 + 41 * 60 + 50.54841 + 8640184.812866 * Tu + 0.093104 * Tu2 - 6.2e-6 * Tu3);
            var theta_G0_h = theta_G0_sec / 3600 % 24.0; // h (24 => 360°)

            // time of day
            var ut1 = Fun.Frac(jd) * 24; // ut = 12h + solar time (same as frac of jd)

            var theta_G_h = 1.002737909350795 * ut1 + theta_G0_h; // ~1° per hour (360° per day) + offset due to orbit around the sun
            var theta_G_rad = (theta_G_h / 24 * 360) * Constant.RadiansPerDegree; // hour angle to radians

            var N = BuildNutationTransform(jd);
            var alpha_E = Fun.Atan(N.M01 / N.M00);

            var Theta_G = theta_G_rad + alpha_E;
            
            var Rs = RotationZ(Theta_G);

            if (xp == 0 && yp == 0)
                return Rs;

            var Rm = RotationX(-xp) * RotationY(-yp);

            return Rm * Rs;
        }

        /// <summary>
        /// Local:
        /// Z-axis sky
        /// X-axis east
        /// Y-axis north
        /// </summary>
        /// <param name="longitudeInDegrees">positive to east</param>
        /// <param name="latitudeInDegrees">positive to north</param>
        /// <returns></returns>
        public static M33d ITRFtoLocal(double longitudeInDegrees, double latitudeInDegrees)
        {
            var rotLong = RotationZ(longitudeInDegrees * Constant.RadiansPerDegree);
            var rotLat = RotationY(latitudeInDegrees * Constant.RadiansPerDegree - Constant.PiHalf); // 90° -> 0, / -90° -> 180°
            var yToNorth = RotationZ(-Constant.PiHalf);

            return yToNorth * rotLat * rotLong;
        }

        /// <summary>
        /// Gets the angular diameter of a planet object and its current distance in AU
        /// </summary>
        /// <param name="planet"></param>
        /// <param name="distanceAU">distance in AU</param>
        /// <returns>angular diameter in radians</returns>
        public static double AngularDiameter(Planet planet, double distanceAU)
        {
            var dia = PlanetDiameters[(int)planet];
            var dist = distanceAU * AU;
            return AngularDiameter(dia, dist);
        }

        /// <summary>
        /// Gets the angular diameter of an object given its diameter and distance.
        /// Distance and diameter must be in the same unit scale.
        /// </summary>
        /// <param name="diameter">diameter</param>
        /// <param name="distance">distance</param>
        /// <returns>angular diameter in radians</returns>
        public static double AngularDiameter(double diameter, double distance)
        {
            return 2.0 * Fun.Atan(0.5 * diameter / distance);
        }

        /// <summary>
        /// Gets the mean anomaly of a planet for a given time
        /// 1.1 of https://aa.quae.nl/en/reken/hemelpositie.html
        /// </summary>
        /// <param name="planet"></param>
        /// <param name="jd"></param>
        /// <returns>mean anomaly in radians</returns>
        public static double GetMeanAnomaly(Planet planet, double jd)
        {
            // n = 0.9856076686° / a^(3/2)
            var planetIndex = (int)planet;
            var n = DerivedOrbitParameters[planetIndex].Item1 * Constant.RadiansPerDegree;

            var M0 = OrbitParameters[planetIndex].Item6 * Constant.RadiansPerDegree;
            return M0 + n * (jd - J2000);
        }

        /// <summary>
        /// Gets the distance to the sun of a planet object and its true anomaly
        /// 1.3 of https://aa.quae.nl/en/reken/hemelpositie.html
        /// </summary>
        /// <param name="planet">planet</param>
        /// <param name="v">true anomaly in radians</param>
        /// <returns>distance to the sun in AU</returns>
        public static double GetDistanceToTheSun(Planet planet, double v)
        {
            var planetIndex = (int)planet;
            var e = OrbitParameters[planetIndex].Item2;
            var den = DerivedOrbitParameters[planetIndex].Item2;
            return den / (1 + e * v.Cos());
        }

        /// <summary>
        /// Calculates the rectangular heliocentric ecliptic coordinates of a planet at a given Julian day measured in AU.
        /// The coordinates are in a right-handed coordinate system centered at the sun.
        /// The x and y coordinates are the distances to the vernal equinox and z the distance from the plane of the ecliptic.
        /// </summary>
        /// <param name="planet">Planet</param>
        /// <param name="jd">Julian day in UTC</param>
        /// <returns>rectangular heliocentric ecliptic coordinates in AU</returns>
        public static V3d RectangularHeliocentricEclipticCoordinates(Planet planet, double jd)
        {
            // 1. Mean Anomaly
            var M = GetMeanAnomaly(planet, jd);

            // 2. True Anomaly / Solve Kepler Equation
            var v = ApproximateTrueAnomaly(planet, M);

            // 3. Distance to the Sun
            var r = GetDistanceToTheSun(planet, v);

            // 4. rectangular heliocentric ecliptic coordinates
            var planetIndex = (int)planet;
            var i = OrbitParameters[planetIndex].Item3 * Constant.RadiansPerDegree;
            var w = OrbitParameters[planetIndex].Item4 * Constant.RadiansPerDegree;
            var O = OrbitParameters[planetIndex].Item5 * Constant.RadiansPerDegree;

            var cosO = O.Cos();
            var sinO = O.Sin();
            var cosWV = (w + v).Cos();
            var sinWV = (w + v).Sin();
            var cosI = i.Cos();
            var sinI = i.Sin();
            return r * new V3d(
                            cosO * cosWV - sinO * cosI * sinWV,
                            sinO * cosWV + cosO * cosI * sinWV,
                            sinI * sinWV
                        );
        }

        /// <summary>
        /// True anomaly approximation coefficients in degree
        /// </summary>
        public static double[][] AnomalyApproximationCoefficients = new double[][]
        {
            new double[] { 23.4400, 2.9818, 0.5255, 0.1058, 0.0241, 0.0055 }, // Mercury 
            new double[] {  0.7758, 0.0033                                 }, // Venus   
            new double[] {  1.9148, 0.0200, 0.0003                         }, // Earth   
            new double[] { 10.6912, 0.6228, 0.0503, 0.0046, 0.0005         }, // Mars    
            new double[] {  5.5549, 0.1683, 0.0071, 0.0003                 }, // Jupiter 
            new double[] {  6.3585, 0.2204, 0.0106, 0.0006                 }, // Saturn  
            new double[] {  5.3042, 0.1534, 0.0062, 0.0003                 }, // Uranus  
            new double[] {  1.0302, 0.0058                                 }, // Neptune 
            new double[] { 28.3150, 4.3408, 0.9214, 0.2235, 0.0627, 0.0174 }  // Pluto
        };

        /// <summary>
        /// Approximate True Anomaly / Equation of Center:
        /// Angular distance of the planet from the perihelion of the planet
        /// </summary>
        /// <param name="planet">planet</param>
        /// <param name="M">mean anomaly in radians</param>
        /// <returns>true anomaly in radians</returns>
        public static double ApproximateTrueAnomaly(Planet planet, double M)
        {
            var mi = M;
            var C = 0.0;
            foreach (var c in AnomalyApproximationCoefficients[(int)planet])
            {
                C += c * Fun.Sin(mi);
                mi += M;
            }

            return M + C * Constant.RadiansPerDegree; // convert to radians as coefficients are in degree
        }

        /// <summary>
        /// https://aa.quae.nl/en/reken/kepler.html
        /// </summary>
        /// <param name="M">Mean Anomaly in radians</param>
        /// <param name="e">eccentricity</param>
        /// <param name="a">aphelion in AU</param>
        /// <returns>True Anomaly in radians</returns>
        public static double CalculateTrueAnomaly(double M, double e, double a)
        {
            if (e == 0) // circular orbit
            {
                return M;
            }
            else
            {
                var d = e - 1; // parabolic excess
                var q = (a / d).Abs(); // perifocal distance

                if (e < 1)
                    M = M - Constant.PiTimesTwo * (M / Constant.PiTimesTwo).Round();

                var Mq = //e != 1.0 ? 
                            M / Fun.Sqrt(d.Abs().Pow(3.0));
                         // : t * Fun.Sqrt(R / q.Pow(3.0);

                var W = Fun.Sqrt(9.0 / 8.0) * Mq / e;
                var u = Fun.Pow(W + Fun.Sqrt(W.Square() + 1 / e.Pow(3)), 1.0 / 3.0);
                var T = u - 1 / (e * u);

                if (e == 1.0)
                    return 2 * Fun.Atan(T);

                var Es = T * Fun.Sqrt(2 * d.Abs());
                var Ei = Es;

                if (e > 1)
                {
                    var Eh = Fun.Asinh(M / e);
                    if (Fun.Abs(Eh) < 0.53 * (e * Fun.Sinh(Es) - Es - M).Abs())
                        Ei = Eh;
                }
                               
                var eps = 2.2e-16;
                double dEi, Bi, si, ci, di = 0.0;
                do
                {
                    if (e < 1) // elliptic orbit
                    {
                        si = e * Fun.Sin(Ei);
                        ci = 1 - e * Fun.Cos(Ei);
                        di = Ei - si - M;
                    }
                    else // e > 1 // hyperbolic orbit
                    {
                        si = e * Fun.Sinh(Ei);
                        ci = 1 - e * Fun.Cosh(Ei);
                        di = si - Ei - M;
                    }

                    dEi = di / ci;
                    Bi = Fun.Abs(2 * eps * Ei * ci / si);
                    Ei = Ei - dEi;

                } while (dEi.Square() >= Bi);

                var t = e < 1 ?
                          Fun.Sqrt((e + 1) / d.Abs()) * Fun.Tan(0.5 * Ei)
                        : Fun.Sqrt((e + 1) / d.Abs()) * Fun.Tanh(0.5 * Ei);
                
                return 2 * Fun.Atan(t);
            }
        }
    }
}
