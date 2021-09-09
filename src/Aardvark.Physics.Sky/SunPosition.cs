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
        /// </summary>
        /// <param name="time">date and time</param>
        /// <param name="timeZone">time zone</param>
        /// <param name="longitudeInDegrees">Longitude GPS coordinate in degrees east</param>
        /// <param name="latitudeInDegrees">Latitude GPS coordinate in degrees north</param>
        /// <returns>Sun spherical coordinates and distance</returns>
        public static (SphericalCoordinate, double) Compute(DateTime time, int timeZone, double longitudeInDegrees, double latitudeInDegrees)
        {
            var jd = time.ComputeJulianDay() - (timeZone / 24.0);
            return Compute(jd, longitudeInDegrees, latitudeInDegrees);
        }

        /// <summary>
        /// Computes the spherical coordinates phi and theta of the sun and its distance.
        /// https://www.aa.quae.nl/en/reken/zonpositie.html by Dr Louis Strous
        /// </summary>
        /// <param name="jd">UTC time in Julian days</param>
        /// <param name="longitudeInDegrees">Longitude GPS coordinate in degrees east</param>
        /// <param name="latitudeInDegrees">Latitude GPS coordinate in degrees north</param>
        /// <returns>Sun spherical coordinates and distance</returns>
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
        /// <param name="longitudeInDegrees">longitude of the meridian of interest in degrees east</param>
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
        /// <param name="longitudeInDegrees">Longitude of the meridian of interest in degrees east</param>
        /// <returns>hour angle in degrees</returns>
        public static double HourAngleDeg(double jd, double longitudeInDegrees)
        {
            // sun right ascension in radians
            var alpha = GetRightAscension(jd);

            var lw = -longitudeInDegrees; // convert East to West

            // local sidereal time
            var theta = Astronomy.SideralTime(jd) - lw * Constant.RadiansPerDegree;
            
            // 9. hour angle: how far (usually in hours, but here in degrees) the object (sun) has passed beyond the celestial meridian
            var h = theta - alpha; // (Eq. 31)
            return Fun.AngleDifference(0, h) * Constant.DegreesPerRadian;
        }

        /// <summary>
        /// Calculates the time of sun rise and set closest to the specified day.
        /// The sun rise and set is specified as the time when the top of the solar disk touches the horizon as seen at sea level 
        /// and also accounts for the refraction due to the atmosphere and thereby the time when the sun declination is -0.83°.
        /// If no solution if found double.NaN is returned.
        /// https://www.aa.quae.nl/en/reken/zonpositie.html#10
        /// </summary>
        /// <param name="jd">Date and time in Julian days</param>
        /// <param name="longitudeInDegrees">Longitude GPS coordinate in degrees east</param>
        /// <param name="latitudeInDegrees">Latitude GPS coordinate in degrees north</param>
        /// <returns>Time of sun rise and set in Julian days</returns>
        public static (double, double) SunRiseAndSet(double jd, double longitudeInDegrees, double latitudeInDegrees)
        {
            return HorizonTransit(jd, longitudeInDegrees, latitudeInDegrees, -0.83);
        }

        /// <summary>
        /// Calculates the time where the civil dusk starts and the civil dawn ends closest to the date specified.
        /// The civil dusk is defined when the geometric center of the sun (declination) is -6 degrees below the horizon.
        /// If no solution if found double.NaN is returned.
        /// </summary>
        public static (double, double) CivilDuskAndDawn(double jd, double longitudeInDegrees, double latitudeInDegrees)
        {
            return HorizonTransit(jd, longitudeInDegrees, latitudeInDegrees, -6);
        }

        /// <summary>
        /// Calculates the time where the nautical dusk starts and the nautical dawn ends closest to the date specified.
        /// The nautical dusk is defined when the geometric center of the sun (declination) is -12 degrees below the horizon.
        /// If no solution if found double.NaN is returned.
        /// </summary>
        public static (double, double) NauticalDuskAndDawn(double jd, double longitudeInDegrees, double latitudeInDegrees)
        {
            return HorizonTransit(jd, longitudeInDegrees, latitudeInDegrees, -12);
        }

        /// <summary>
        /// Calculates the time where the astronomical dusk starts and the astronomical dawn ends closest to the date specified.
        /// The astronomical dusk is defined when the geometric center of the sun (declination) is -18 degrees below the horizon.
        /// If no solution if found double.NaN is returned.
        /// </summary>
        public static (double, double) AstronomicalDuskAndDawn(double jd, double longitudeInDegrees, double latitudeInDegrees)
        {
            return HorizonTransit(jd, longitudeInDegrees, latitudeInDegrees, -18);
        }

        /// <summary>
        /// Calculates the previous and next time where the center of the solar disk is at specified declination closest to the given date.
        /// If no solution if found double.NaN is returned.
        /// https://www.aa.quae.nl/en/reken/zonpositie.html#10
        /// </summary>
        /// <param name="jd">Date and time in Julian days</param>
        /// <param name="longitudeInDegrees">Longitude GPS coordinate in degrees east</param>
        /// <param name="latitudeInDegrees">Latitude GPS coordinate in degrees north</param>
        /// <param name="h0InDegrees">Declination angle in degrees</param>
        /// <returns>Time of previous and next horizon transition</returns>
        public static (double, double) HorizonTransit(double jd, double longitudeInDegrees, double latitudeInDegrees, double h0InDegrees)
        {
            var jtransit = SolarTransit(jd, longitudeInDegrees);

            // sun declination at solar transit in radians
            var delta = GetDeclination(jtransit);

            var phi = latitudeInDegrees * Constant.RadiansPerDegree; // latitude?
            var cosPhi = Fun.Cos(phi);
            var sinPhi = Fun.Sin(phi);

            var h0 = h0InDegrees * Constant.RadiansPerDegree; // sun declination angle when top of solar disk touches the horizon on sea level (includes disk radius + refraction of atmosphere)
            var sinh0 = Fun.Sin(h0);

            // early exit if initial approximation is already not found
            var tmp = (sinh0 - sinPhi * Fun.Sin(delta)) / (cosPhi * Fun.Cos(delta));
            if (tmp < -1 || tmp > 1) return (double.NaN, double.NaN);

            var ht = Fun.Acos(tmp) * Constant.DegreesPerRadian;

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

                var htrise = Fun.Acos((sinh0 - sinPhi * Fun.Sin(deltaRise)) / (cosPhi * Fun.Cos(delta))) * Constant.DegreesPerRadian;
                var htset = Fun.Acos((sinh0 - sinPhi * Fun.Sin(deltaSet)) / (cosPhi * Fun.Cos(delta))) * Constant.DegreesPerRadian;

                // iteration of refinement
                jrise = jrise - (hrise + htrise) / 360 * J3; // Eq. 50  :  J3 = average length of a solar day
                jset = jset - (hset - htset) / 360 * J3; // Eq. 51  :  J3 = average length of a solar day
            }

            return (jrise, jset);
        }

        /// <summary>
        /// Gets the equatorial coordinates [declination (delta), right ascension (alpha)] of the sun in radians at the specified Julian date.
        /// </summary>
        /// <param name="jd">Date and time in Julian days</param>
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
        public static double GetDeclination(double jd)
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
        public static double GetRightAscension(double jd)
        {
            // sun ecliptic longitudinal coordinate
            var lambda = GetEclipticLongitude(jd);

            // earths angle of obliquity
            var epsilon = Astronomy.GetEarthMeanObliquityAA2010(jd);

            // sun right ascension in radians
            return Astronomy.RightAscension(lambda, 0.0, epsilon);
        }

        /// <summary>
        /// Calculates the previous and next time where the sun declination is 0 closest to the specified time.
        /// https://www.aa.quae.nl/en/reken/zonpositie.html#10
        /// </summary>
        /// <param name="jd">Date and time in Julian days</param>
        /// <param name="longitudeInDegrees">Longitude GPS coordinate in degrees east</param>
        /// <param name="latitudeInDegrees">Latitude GPS coordinate in degrees north</param>
        /// <returns>Time of the previous and next horizon transit</returns>
        public static (double, double) HorizonTransit(double jd, double longitudeInDegrees, double latitudeInDegrees)
        {
            var jdtransit = SolarTransit(jd, longitudeInDegrees);

            var jdOffset = HorizonTransitOffset(jdtransit, latitudeInDegrees);

            return (jdtransit - jdOffset, jdtransit + jdOffset);
        }

        /// <summary>
        /// Calculates the time offset to where the sun declination is equal to 0° given the time of
        /// the solar transit and a GPS latitude coordinate.
        /// https://www.aa.quae.nl/en/reken/zonpositie.html#10
        /// </summary>
        /// <param name="jdSolarTransit">Date and time of solar transit in Julian days</param>
        /// <param name="latitudeInDegrees">Latitude GPS coordinate in degrees north</param>
        /// <returns>Time offset</returns>
        public static double HorizonTransitOffset(double jdSolarTransit, double latitudeInDegrees)
        {
            // sun ecliptic longitude
            var lambda = GetEclipticLongitude(jdSolarTransit);

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

            return jdOffset;
        }
    }
}
