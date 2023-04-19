/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using System;

namespace Aardvark.Geometry.Clustering
{
    #region Hashing

    public static class V3dClusteringHashExtensions
    {
        public static int HashCode1(this V2d point, double epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);

            return HashCode.Combine((int)xi, (int)yi);
        }

        public static int HashCode1(this V2d point, V2d epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);

            return HashCode.Combine((int)xi, (int)yi);
        }

        public static int HashCode1(this V3d point, double epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);
            var zi = (long)Math.Floor(point.Z / epsilon);

            return HashCode.Combine((int)xi, (int)yi, (int)zi);
        }

        public static int HashCode1(this V3d point, V3d epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);
            var zi = (long)Math.Floor(point.Z / epsilon.Z);

            return HashCode.Combine((int)xi, (int)yi, (int)zi);
        }

        public static int HashCode1of4(this V2d point, V2d epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);

            return HashCode.Combine((int)(xi >> 1), (int)(yi >> 1));
        }

        public static void HashCodes4(this V2d point, V2d epsilon, int[] hca)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);

            int xh0 = (int)(xi >> 1), xh1 = xh0 - 1 + ((int)(xi & 1) << 1);
            int yh0 = (int)(yi >> 1), yh1 = yh0 - 1 + ((int)(yi & 1) << 1);

            hca[0] = HashCode.Combine(xh0, yh0);
            hca[1] = HashCode.Combine(xh1, yh0);
            hca[2] = HashCode.Combine(xh0, yh1);
            hca[3] = HashCode.Combine(xh1, yh1);
        }

        public static int HashCode1of4(this V2d point, double epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);

            return HashCode.Combine((int)(xi >> 1), (int)(yi >> 1));
        }

        public static void HashCodes4(this V2d point, double epsilon, int[] hca)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);

            int xh0 = (int)(xi >> 1), xh1 = xh0 - 1 + ((int)(xi & 1) << 1);
            int yh0 = (int)(yi >> 1), yh1 = yh0 - 1 + ((int)(yi & 1) << 1);

            hca[0] = HashCode.Combine(xh0, yh0);
            hca[1] = HashCode.Combine(xh1, yh0);
            hca[2] = HashCode.Combine(xh0, yh1);
            hca[3] = HashCode.Combine(xh1, yh1);
        }

        public static int HashCode1of8(this V3d point, double epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);
            var zi = (long)Math.Floor(point.Z / epsilon);

            return HashCode.Combine((int)(xi >> 1), (int)(yi >> 1), (int)(zi >> 1));
        }

        public static void HashCodes8(this V3d point, double epsilon, int[] hca)
        {
            var xi = (long)Math.Floor(point.X / epsilon);
            var yi = (long)Math.Floor(point.Y / epsilon);
            var zi = (long)Math.Floor(point.Z / epsilon);

            int xh0 = (int)(xi >> 1), xh1 = xh0 - 1 + ((int)(xi & 1) << 1);
            int yh0 = (int)(yi >> 1), yh1 = yh0 - 1 + ((int)(yi & 1) << 1);
            int zh0 = (int)(zi >> 1), zh1 = zh0 - 1 + ((int)(zi & 1) << 1);

            hca[0] = HashCode.Combine(xh0, yh0, zh0);
            hca[1] = HashCode.Combine(xh1, yh0, zh0);
            hca[2] = HashCode.Combine(xh0, yh1, zh0);
            hca[3] = HashCode.Combine(xh1, yh1, zh0);
            hca[4] = HashCode.Combine(xh0, yh0, zh1);
            hca[5] = HashCode.Combine(xh1, yh0, zh1);
            hca[6] = HashCode.Combine(xh0, yh1, zh1);
            hca[7] = HashCode.Combine(xh1, yh1, zh1);
        }

        public static int HashCode1of8(this V3d point, V3d epsilon)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);
            var zi = (long)Math.Floor(point.Z / epsilon.Z);

            return HashCode.Combine((int)(xi >> 1), (int)(yi >> 1), (int)(zi >> 1));
        }

        public static void HashCodes8(this V3d point, V3d epsilon, int[] hca)
        {
            var xi = (long)Math.Floor(point.X / epsilon.X);
            var yi = (long)Math.Floor(point.Y / epsilon.Y);
            var zi = (long)Math.Floor(point.Z / epsilon.Z);

            int xh0 = (int)(xi >> 1), xh1 = xh0 - 1 + ((int)(xi & 1) << 1);
            int yh0 = (int)(yi >> 1), yh1 = yh0 - 1 + ((int)(yi & 1) << 1);
            int zh0 = (int)(zi >> 1), zh1 = zh0 - 1 + ((int)(zi & 1) << 1);

            hca[0] = HashCode.Combine(xh0, yh0, zh0);
            hca[1] = HashCode.Combine(xh1, yh0, zh0);
            hca[2] = HashCode.Combine(xh0, yh1, zh0);
            hca[3] = HashCode.Combine(xh1, yh1, zh0);
            hca[4] = HashCode.Combine(xh0, yh0, zh1);
            hca[5] = HashCode.Combine(xh1, yh0, zh1);
            hca[6] = HashCode.Combine(xh0, yh1, zh1);
            hca[7] = HashCode.Combine(xh1, yh1, zh1);
        }

        public static int HashCode1of16(
                this V3d normal, double dist,
                double epsNormal, double epsDist)
        {
            var xi = (long)Math.Floor(normal.X / epsNormal);
            var yi = (long)Math.Floor(normal.Y / epsNormal);
            var zi = (long)Math.Floor(normal.Z / epsNormal);
            var di = (long)Math.Floor(dist / epsDist);

            return HashCode.Combine((int)(xi >> 1), (int)(yi >> 1), (int)(zi >> 1), (int)(di >> 1));
        }

        public static void HashCodes16(
                this V3d normal, double dist,
                double epsNormal, double epsDist,
                int[] hca)
        {
            var xi = (long)Math.Floor(normal.X / epsNormal);
            var yi = (long)Math.Floor(normal.Y / epsNormal);
            var zi = (long)Math.Floor(normal.Z / epsNormal);
            var di = (long)Math.Floor(dist / epsDist);

            int xh0 = (int)(xi >> 1), xh1 = xh0 - 1 + ((int)(xi & 1) << 1);
            int yh0 = (int)(yi >> 1), yh1 = yh0 - 1 + ((int)(yi & 1) << 1);
            int zh0 = (int)(zi >> 1), zh1 = zh0 - 1 + ((int)(zi & 1) << 1);
            int dh0 = (int)(di >> 1), dh1 = dh0 - 1 + ((int)(di & 1) << 1);

            hca[0] = HashCode.Combine(xh0, yh0, zh0, dh0);
            hca[1] = HashCode.Combine(xh1, yh0, zh0, dh0);
            hca[2] = HashCode.Combine(xh0, yh1, zh0, dh0);
            hca[3] = HashCode.Combine(xh1, yh1, zh0, dh0);
            hca[4] = HashCode.Combine(xh0, yh0, zh1, dh0);
            hca[5] = HashCode.Combine(xh1, yh0, zh1, dh0);
            hca[6] = HashCode.Combine(xh0, yh1, zh1, dh0);
            hca[7] = HashCode.Combine(xh1, yh1, zh1, dh0);
            hca[8] = HashCode.Combine(xh0, yh0, zh0, dh1);
            hca[9] = HashCode.Combine(xh1, yh0, zh0, dh1);
            hca[10] = HashCode.Combine(xh0, yh1, zh0, dh1);
            hca[11] = HashCode.Combine(xh1, yh1, zh0, dh1);
            hca[12] = HashCode.Combine(xh0, yh0, zh1, dh1);
            hca[13] = HashCode.Combine(xh1, yh0, zh1, dh1);
            hca[14] = HashCode.Combine(xh0, yh1, zh1, dh1);
            hca[15] = HashCode.Combine(xh1, yh1, zh1, dh1);
        }
    }

    #endregion
}
