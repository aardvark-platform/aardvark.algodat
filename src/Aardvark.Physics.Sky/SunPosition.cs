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
}
