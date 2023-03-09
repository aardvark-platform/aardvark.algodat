using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aardvark.Data.Photometry
{
    /// <summary>
    /// Utility functions for using photometic data
    /// </summary>
    public static class Photometry
    {
        /// <summary>
        /// Returns the direction vector of the given c and gamma angles.
        /// The angles are expected to be in radians.
        /// The coordinate system is defined as:
        /// [ 0, 0,-1] = Gamma 0°
        /// [ 0, 0, 1] = Gamma 180°
        /// [ 1, 0, 0] = C0
        /// [ 0, 1, 0] = C90
        /// [-1, 1, 0] = C180
        /// [ 0,-1, 0] = C270
        /// NOTE: gamma/theta is mapped differntly than in Aardvark Conversion.CartesianFromSpherical
        /// </summary>
        public static V3d SphericalToCartesian(double cInRadians, double gammaInRadians)
        {
            var s = gammaInRadians.Sin();
            return new V3d(cInRadians.Cos() * s, cInRadians.Sin() * s, -gammaInRadians.Cos());
        }

        /// <summary>
        /// Returns (c, gamma) angles in radians of the direction vector (normalized).
        /// The coordinate system is defined as:
        /// [ 0, 0,-1] = Gamma 0°
        /// [ 0, 0, 1] = Gamma 180°
        /// [ 1, 0, 0] = C0
        /// [ 0, 1, 0] = C90
        /// [-1, 1, 0] = C180
        /// [ 0,-1, 0] = C270
        /// NOTE: gamma/theta is mapped differntly than in Aardvark Conversion.CartesianFromSpherical
        /// </summary>
        public static (double, double) CartesianToSpherical(V3d v)
        {
            // [0,0,-1] = 0°
            // [0,0, 1] = 180°
            var gamma = Constant.Pi - Fun.AcosClamped(v.Z);

            // C0:   atan2( 0  1)  =   0
            // C90:  atan2( 1  0)  =  90
            // C180: atan2( 0 -1)  = 180/-180
            // C270: atan2(-1  0)  = -90
            // normalize [-pi..pi] to [0..1] -> invert vector and add 180°
            var c = Fun.Atan2(-v.Y, -v.X) + Constant.Pi; // atan2: -pi..pi -> 0..2pi

            return (c, gamma);
        }
    }
}
