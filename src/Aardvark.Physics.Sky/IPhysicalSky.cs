/*
    Aardvark Platform
    Copyright (C) 2006-2025  Aardvark Platform Team
    https://aardvark.graphics
    
    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at
    
        http://www.apache.org/licenses/LICENSE-2.0
    
    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
*/
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

    public class Sky(double sunPhi, double sunTheta)
    {
        /// <summary>
        /// Position of sun from south over west, north, east to south.
        /// in radians [0..2 PI] 0=2PI=south; PI/2=west, PI=north, 3PI/2=east
        /// </summary>
        public readonly double SunPhi = sunPhi;

        /// <summary>
        /// Height of sun from zenith down in radians [0..PI] 0=zenith
        /// </summary>
        public readonly double SunTheta = sunTheta;

        public readonly V3d SunVec = V3dFromPhiTheta(sunPhi, sunTheta);

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
    }
}
