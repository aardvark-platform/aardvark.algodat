using Aardvark.Base;

namespace Aardvark.Physics.Sky
{
    public interface IPhysicalSky
    {
        /// <summary>
        /// Gets the sky color for the supplied direction in linear sRGB fitted to [0,1].
        /// </summary>
        C3f GetColor(V3d normalizedViewVec);

        /// <summary>
        /// Gets the sky radiance in cd/m² for the supplied direction in XYZ color space.
        /// </summary>
        C3f GetRadiance(V3d normalizedViewVec);
    }

    public static class IPhysicalSkyExtensions
    {
        /// <summary>
        /// Gets the sky color for the supplied spherical coordinate in linear sRGB fitted to [0,1].
        /// Phi: [0..2 PI] 0=2 PI=south; PI/2=west, PI=north, 3 PI/2=east
        /// Theta: Height of sun from zenith down in radians [0..PI] 0=zenith
        /// </summary>
        public static C3f GetColor(this IPhysicalSky sky, double phi, double theta)
        {
            return sky.GetRadiance(Sky.V3dFromPhiTheta(phi, theta));
        }

        /// <summary>
        /// Gets the sky radiance in cd/m² for the spherical coordinate angle in XYZ color space.
        /// Phi: [0..2 PI] 0=2 PI=south; PI/2=west, PI=north, 3 PI/2=east
        /// Theta: Height of sun from zenith down in radians [0..PI] 0=zenith
        /// </summary>
        public static C3f GetRadiance(this IPhysicalSky sky, double phi, double theta)
        {
            return sky.GetRadiance(Sky.V3dFromPhiTheta(phi, theta));
        }
    }

    public class Sky
    {
        /// <summary>
        /// Position of sun from south over west, north, east to south.
        /// in radians [0..2 PI] 0=2PI=south; PI/2=west, PI=north, 3PI/2=east
        /// </summary>
        public readonly double SunPhi;

        /// <summary>
        /// Height of sun from zenith down in radians [0..PI] 0=zenith
        /// </summary>
        public readonly double SunTheta;

        public readonly V3d SunVec;

        public static V3d V3dFromPhiTheta(double phi, double theta)
        {
            var sinTheta = -Fun.Sin(theta);
            return new V3d(sinTheta * phi.Sin(), sinTheta * phi.Cos(), Fun.Cos(theta));
        }

        /// <summary>
        /// Computes a V2d holding the spherical coordinates phi and theta
        /// from the supplied V3d vector.
        /// </summary>
        /// <returns>A V2d which holds Phi in X and Theta in Y.</returns>
        public static V2d PhiThetaFromV3d(V3d vec)
        {
            return new V2d(Fun.Atan2(-vec.X, -vec.Y), Fun.Atan2(vec.XY.Length, vec.Z));
        }

        public Sky(double sunPhi, double sunTheta)
        {
            SunPhi = sunPhi;
            SunTheta = sunTheta;
            SunVec = V3dFromPhiTheta(sunPhi, sunTheta);
        }
    }
}
