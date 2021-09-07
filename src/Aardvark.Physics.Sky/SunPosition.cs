using Aardvark.Base;
using System;

namespace Aardvark.Physics.Sky
{
    /// <summary>
    /// This class holds a sun position calculation
    /// based on Astronomy Answers by Dr Louis Strous
    /// https://www.aa.quae.nl/en/reken/zonpositie.html
    /// The accuracy is ~1°
    /// </summary>
    public static class SunPosition
    {
        /// <summary>
        /// Computes the spherical coordinates phi and theta of the sun and its distance.
        /// https://www.aa.quae.nl/en/reken/zonpositie.html by Dr Louis Strous
        /// <param name="time">date and time</param>
        /// <param name="timeZone">time zone</param>
        /// <param name="longitudeInDegrees">GPS longitude coordinate (east)</param>
        /// <param name="latitudeInDegrees">GPS latitude coordinate</param>
        /// </summary>
        public static (SphericalCoordinate, double) Compute(DateTime time, int timeZone, double longitudeInDegrees, double latitudeInDegrees)
        {
            var jd = time.ComputeJulianDay() - (timeZone / 24.0);
            return Compute(jd, longitudeInDegrees, latitudeInDegrees);
        }

        /// <summary>
        /// Computes the spherical coordinates phi and theta of the sun and its distance.
        /// https://www.aa.quae.nl/en/reken/zonpositie.html by Dr Louis Strous
        /// <param name="jd">UTC time in Julian days</param>
        /// <param name="longitudeInDegrees">GPS longitude coordinate (east)</param>
        /// <param name="latitudeInDegrees">GPS latitude coordinate</param>
        /// </summary>
        public static (SphericalCoordinate, double) Compute(double jd, double longitudeInDegrees, double latitudeInDegrees)
        {
            // The Mean Anomaly
            var M = Astronomy.GetMeanAnomaly(Planet.Earth, jd);
            var v = Astronomy.ApproximateTrueAnomaly(Planet.Earth, M);

            // The Perihelion of Earth
            var PI = Astronomy.GetPerihelion(Planet.Earth);

            // The Ecliptic Coordinates 
            double lambda = v + (PI + 180.0) * Constant.RadiansPerDegree; //(Eq. 7)                

            var (h, A) = Astronomy.GeocentricToObserver(lambda, 0.0, jd, longitudeInDegrees, latitudeInDegrees);

            var crd = Astronomy.SkyToSpherical(h, A);

            var d = Astronomy.GetDistanceToTheSun(Planet.Earth, v) * Astronomy.AU;

            return (crd, d);
        }

        /// <summary>
        /// Gets the Julian date when the sun crosses the median closest to the given date.
        /// https://www.aa.quae.nl/en/reken/zonpositie.html#8
        /// </summary>
        /// <param name="jd">Julian date of when the closest solar transit should be calculated</param>
        /// <param name="longitudeInDegrees">longitude of the meridian of interest in degrees</param>
        /// <returns>Time of transit in Julian days</returns>
        public static double SolarTransit(double jd, double longitudeInDegrees)
        {
            var (J0, J1, J2, J3) = Astronomy.GetSolarTimeCoefficients(Planet.Earth);

            var nx = (jd - Astronomy.J2000 - J0) / J3 - longitudeInDegrees / 360; // Eq. 33
            var n = Fun.Round(nx);

            var jx = jd + J3 * (n - nx); // Eq. 34

            var M = Astronomy.GetMeanAnomaly(Planet.Earth, jx); // in radians

            var PI = Astronomy.GetPerihelion(Planet.Earth); // in degrees

            var Lsun = M + (PI + 180) * Constant.RadiansPerDegree; // Eq. 28

            // first approximation of solar transit
            var jt = jx + J1 * Fun.Sin(M) + J2 * Fun.Sin(2 * Lsun); // Eq. 35

            // perform 1 iteration of refinement
            for (int i = 0; i < 1; i++)
            {
                // sun hour angle in degrees
                var H = HourAngleDeg(jt, longitudeInDegrees);

                // iteration of refinement
                jt = jt - H / 360 * J3; // Eq. 36  :  J3 = average length of a solar day
            }

            return jt;
        }

        /// <summary>
        /// Calculates the hour angle of the sun in degrees.
        /// https://aa.quae.nl/en/reken/hemelpositie.html#1_9
        /// The hour angle H is the offset to when the sun has passed the specified meridian and often also written in hours.
        /// H = sidereal time [theta] - right ascension [alpha]
        /// </summary>
        /// <param name="jd">Date and time in Julian days</param>
        /// <param name="longitudeInDegrees">Longitude of the meridian of interest in degrees</param>
        /// <returns>hour angle in degrees</returns>
        public static double HourAngleDeg(double jd, double longitudeInDegrees)
        {
            // sun right ascension in radians
            var alpha = GetRightAscension(jd);

            var lw = -longitudeInDegrees; // convert East to West

            // 8. sidereal time
            var theta =
                280.1470 +
                360.9856235 *
                 (jd - Astronomy.J2000) - lw;

            // 9. hour angle: how far (usually in hours, but here in degrees) the object (sun) has passed beyond the celestial meridian
            var h = theta * Constant.RadiansPerDegree - alpha; // (Eq. 31)
            return Fun.AngleDifference(0, h) * Constant.DegreesPerRadian;
        }

        /// <summary>
        /// https://www.aa.quae.nl/en/reken/zonpositie.html#10
        /// </summary>
        /// <param name="jd"></param>
        /// <param name="longitudeInDegrees"></param>
        /// <param name="latitudeInDegrees"></param>
        /// <returns></returns>
        public static (double, double) SunRiseAndSet(double jd, double longitudeInDegrees, double latitudeInDegrees)
        {
            return HorizonTransit(jd, longitudeInDegrees, latitudeInDegrees, -0.83);
        }

        public static (double, double) HorizonTransit(double jd, double longitudeInDegrees, double latitudeInDegrees, double h0InDegrees)
        {
            var jtransit = SolarTransit(jd, longitudeInDegrees);

            // sun declination at jtransit in radians
            var delta = GetDeclination(jtransit);

            var phi = latitudeInDegrees * Constant.RadiansPerDegree; // latitude?
            var cosPhi = Fun.Cos(phi);
            var sinPhi = Fun.Sin(phi);

            var h0 = h0InDegrees * Constant.RadiansPerDegree; // sun declination angle when top of solar disk touches the horizon on sea level (includes disk radius + refraction of atmosphere)
            var sinh0 = Fun.Sin(h0);

            var ht = Fun.Acos((sinh0 - sinPhi * Fun.Sin(delta)) / (cosPhi * Fun.Cos(delta))) * Constant.DegreesPerRadian;

            var J3 = Astronomy.GetSolarTimeCoefficients(Planet.Earth).Item4;

            var jrise = jtransit - (ht / 360) * J3;
            var jset = jtransit + (ht / 360) * J3;

            // perform 2 iteration of refinement
            for (int i = 0; i < 2; i++)
            {
                // sun hour angle in degrees
                var hrise = HourAngleDeg(jrise, longitudeInDegrees);
                var hset = HourAngleDeg(jset, longitudeInDegrees);

                var deltaRise = GetDeclination(jrise);
                var deltaSet = GetDeclination(jset);

                var htrise = Fun.Acos((sinh0 - sinPhi * Fun.Sin(deltaRise)) / (cosPhi * Fun.Cos(deltaRise))) * Constant.DegreesPerRadian;
                var htset = Fun.Acos((sinh0 - sinPhi * Fun.Sin(deltaSet)) / (cosPhi * Fun.Cos(deltaSet))) * Constant.DegreesPerRadian;

                // iteration of refinement
                jrise = jrise - (hrise + htrise) / 360 * J3; // Eq. 50  :  J3 = average length of a solar day
                jset = jset - (hset - htset) / 360 * J3; // Eq. 51  :  J3 = average length of a solar day
            }

            return (jrise, jset);
        }

        /// <summary>
        /// Gets the equatorial coordinates [declination (delta), right ascension (alpha)]of the sun in radians at the specified Julian date.
        /// </summary>
        /// <param name="jd">time in Julian days</param>
        /// <returns>declination and right ascension in radians</returns>
        public static (double, double) GetEquatorialCoordinates(double jd)
        {
            // sun ecliptic longitudinal coordinate
            var lambda = GetEclipticLongitude(jd);

            // earths angle of obliquity
            var epsilon = Astronomy.GetEarthMeanObliquityAA2010(jd);

            // sun equatorial coordinates
            return Astronomy.GeocentricEclipticToEquatorialCoodinates(lambda, 0.0, epsilon);
        }

        /// <summary>
        /// Calculates the sun ecliptic longitudinal coordinate (lambda) in radians.
        /// </summary>
        static double GetEclipticLongitude(double jd)
        {
            // The Mean Anomaly
            var M = Astronomy.GetMeanAnomaly(Planet.Earth, jd);
            var v = Astronomy.ApproximateTrueAnomaly(Planet.Earth, M);

            // The Perihelion of Earth
            var PI = Astronomy.GetPerihelion(Planet.Earth);

            // sun ecliptic longitudinal coordinate (lambda)
            return v + (PI + 180.0) * Constant.RadiansPerDegree;
        }

        /// <summary>
        /// Gets the sun declination (delta) in radians.
        /// </summary>
        static double GetDeclination(double jd)
        {
            // sun ecliptic longitudinal coordinate
            var lambda = GetEclipticLongitude(jd);

            // earths angle of obliquity
            var epsilon = Astronomy.GetEarthMeanObliquityAA2010(jd);

            // sun declination in radians
            return Astronomy.Declination(lambda, 0.0, epsilon);
        }

        /// <summary>
        /// Gets the sun right ascension (alpha) in radians.
        /// </summary>
        static double GetRightAscension(double jd)
        {
            // sun ecliptic longitudinal coordinate
            var lambda = GetEclipticLongitude(jd);

            // earths angle of obliquity
            var epsilon = Astronomy.GetEarthMeanObliquityAA2010(jd);

            // sun right ascension in radians
            return Astronomy.RightAscension(lambda, 0.0, epsilon);
        }

        public static (double, double) HorizonTransit(double jd, double longitudeInDegrees, double latitudeInDegrees)
        {
            var jdtransit = SolarTransit(jd, longitudeInDegrees);

            // sun ecliptic longitude
            var lambda = GetEclipticLongitude(jdtransit);

            var phi = latitudeInDegrees * Constant.RadiansPerDegree;

            var H1 = 22.137;
            var H3 = 0.599;
            var H5 = 0.016;
            var J3 = 1.0;

            var tanPhi = Fun.Tan(phi);
            var sinLsun = Fun.Sin(lambda);
            var h0 = 90
                    + H1 * sinLsun * tanPhi
                    + H3 * sinLsun.Pown(3) * tanPhi * (3 + tanPhi.Pown(2))
                    + H5 * sinLsun.Pown(5) * tanPhi * (15 + 10 * tanPhi.Pown(2) + 3 * tanPhi.Pown(4));

            var jdOffset = h0 / 360 * J3;

            return (jdtransit - jdOffset, jdtransit + jdOffset);
        }
    }
}
