using Aardvark.Base;
using System;

namespace Aardvark.Physics.Sky
{
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
