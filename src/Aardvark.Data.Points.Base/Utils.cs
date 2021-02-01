/*
   Aardvark Platform
   Copyright (C) 2006-2020  Aardvark Platform Team
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
using System.Runtime.CompilerServices;

#pragma warning disable CS1591

namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public static class Utils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DistLessThanL1(ref V2f a, ref V2f b, double dist)
        {
            var
            d = a.X - b.X; if (d < 0) d = -d; if (d >= dist) return false;
            d = a.Y - b.Y; if (d < 0) d = -d; if (d >= dist) return false;
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DistLessThanL1(ref V2d a, ref V2d b, double dist)
        {
            var
            d = a.X - b.X; if (d < 0) d = -d; if (d >= dist) return false;
            d = a.Y - b.Y; if (d < 0) d = -d; if (d >= dist) return false;
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DistLessThanL1(ref V3f a, ref V3f b, double dist)
        {
            var
            d = a.X - b.X; if (d < 0) d = -d; if (d >= dist) return false;
            d = a.Y - b.Y; if (d < 0) d = -d; if (d >= dist) return false;
            d = a.Z - b.Z; if (d < 0) d = -d; if (d >= dist) return false;
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DistLessThanL1(ref V3d a, ref V3d b, double dist)
        {
            var
            d = a.X - b.X; if (d < 0) d = -d; if (d >= dist) return false;
            d = a.Y - b.Y; if (d < 0) d = -d; if (d >= dist) return false;
            d = a.Z - b.Z; if (d < 0) d = -d; if (d >= dist) return false;
            return true;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DistLessThanL2(ref V2f a, ref V2f b, double distSquared)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dd = dx * dx + dy * dy;
            return dd < distSquared;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DistLessThanL2(ref V2d a, ref V2d b, double distSquared)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dd = dx * dx + dy * dy;
            return dd < distSquared;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DistLessThanL2(ref V3f a, ref V3f b, double distSquared)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;
            var dd = dx * dx + dy * dy + dz * dz;
            return dd < distSquared;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DistLessThanL2(ref V3d a, ref V3d b, double distSquared)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;
            var dd = dx * dx + dy * dy + dz * dz;
            return dd < distSquared;
        }

    }
}
