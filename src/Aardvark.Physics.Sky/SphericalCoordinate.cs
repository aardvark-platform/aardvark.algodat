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

namespace Aardvark.Physics.Sky
{
    /// <summary>
    /// Represents a object in the sky using spherical coordinate in Aardvark coordinate notation.
    /// </summary>
    public struct SphericalCoordinate(double theta, double phi)
    {
        /// <summary>
		/// Height of the object from zenith down in radians [0..PI] 0=zenith
		/// </summary>
		public double Theta = theta;

        /// <summary>
        /// Position of the object from south over west, north, east to south in radians (azimuth)
        /// [0..2 PI] 0=2PI=south; PI/2=west, PI=north, 3 PI/2=east
        /// </summary>
        public double Phi = phi;
    }

}
