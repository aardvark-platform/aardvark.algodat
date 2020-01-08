using Aardvark.Base;
using System;

namespace Aardvark.Physics.Sky
{
    /// <summary>
    /// Represents a object in the sky using spherical coordinate in Aardvark coordinate notation.
    /// </summary>
    public struct SphericalCoordinate
    {
        /// <summary>
		/// Height of the object from zenith down in radians [0..PI] 0=zenith
		/// </summary>
		public double Theta;

        /// <summary>
        /// Position of the object from south over west, north, east to south in radians (azimuth)
        /// [0..2 PI] 0=2PI=south; PI/2=west, PI=north, 3 PI/2=east
        /// </summary>
        public double Phi;

        public SphericalCoordinate(double theta, double phi)
        {
            Theta = theta;
            Phi = phi;
        }
    }

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
        public static (SphericalCoordinate, double) Compute(DateTime time, int timeZone,  double longitudeInDegrees, double latitudeInDegrees)
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

            // The Perihelion and the Obliquity of the Ecliptic
            double PI = 102.9372;

            // The Ecliptic Coordinates 
            double lambda = v + (PI + 180.0) * Constant.RadiansPerDegree; //(Eq. 7)                

            var (h, A) = Astronomy.GeocentricToObserver(lambda, 0.0, jd, longitudeInDegrees, latitudeInDegrees);
            
            var crd = Astronomy.SkyToSpherical(h, A);

            var d = Astronomy.GetDistanceToTheSun(Planet.Earth, v) * Astronomy.AU;

            return (crd, d);
        }
    }

    /// <summary>
    /// Calculation of the moon position
    /// According to 4. https://aa.quae.nl/en/reken/hemelpositie.html
    /// </summary>
    public static class MoonPosition
    {
        /// <summary>
        /// Calculates the direction and distance to the moon for a given time and location.
        /// </summary>
        /// <param name="time">date and time</param>
        /// <param name="timeZone">time zone</param>
        /// <param name="longitudeInDegrees">GPS longitude coordinate (east)</param>
        /// <param name="latitudeInDegrees">GPS latitude coordinate</param>
        /// <returns>Direction to the moon as phi/theta/distance</returns>
        public static (SphericalCoordinate, double) Compute(DateTime time, int timeZone, double longitudeInDegrees, double latitudeInDegrees)
        {
            var jd = time.ComputeJulianDay() - (timeZone / 24.0);
            return Compute(jd, longitudeInDegrees, latitudeInDegrees);
        }

        /// <summary>
        /// Calculates the direction and distance to the moon for a given time and location.
        /// </summary>
        /// <param name="jd">UTC time in Julian days</param>
        /// <param name="longitudeInDegrees">GPS longitude coordinate (east)</param>
        /// <param name="latitudeInDegrees">GPS latitude coordinate</param>
        /// <returns>Direction to the moon as phi/theta in Aardvark spherical coordinates and distance in meters</returns>
        public static (SphericalCoordinate, double) Compute(double jd, double longitudeInDegrees, double latitudeInDegrees)
        {
            var dt = jd - Astronomy.J2000;

            // mean geocentric ecliptic longitude 
            var L = 218.316 + dt * 13.176396;

            // mean anomaly 
            var M = 134.963 + dt * 13.064993;

            // mean distance in km
            var F = 93.272 + dt * 13.229350;

            var mrad = M.RadiansFromDegrees();
            var lambda = L + 6.289 * mrad.Sin();
            var beta = 5.128 * F.RadiansFromDegrees().Sin();
            var distance = 385001 - 20905 * mrad.Cos();

            var (h, A) = Astronomy.GeocentricToObserver(lambda * Constant.RadiansPerDegree, beta * Constant.RadiansPerDegree, jd, longitudeInDegrees, latitudeInDegrees);

            var crd = Astronomy.SkyToSpherical(h, A);

            return (crd, distance * 1000);
        }
    }
}
