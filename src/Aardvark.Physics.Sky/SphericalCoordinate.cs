
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

}
