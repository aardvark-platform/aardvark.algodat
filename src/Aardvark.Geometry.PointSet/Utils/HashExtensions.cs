/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class HashExtensions
    {
        #region V[234][fdli]

        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V2f x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V2d x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V2l x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V2i x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V2f[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V2d[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V2l[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V2i[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V2f> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V2d> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V2l> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V2i> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); }
            });
        }

        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V3f x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V3d x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V3l x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V3i x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V3f[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); bw.Write(xs[i].Z); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V3d[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); bw.Write(xs[i].Z); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V3l[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); bw.Write(xs[i].Z); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V3i[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); bw.Write(xs[i].Z); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V3f> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V3d> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V3l> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V3i> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); }
            });
        }

        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V4f x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); bw.Write(x.W); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V4d x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); bw.Write(x.W); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V4l x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); bw.Write(x.W); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V4i x)
        => ComputeMd5Hash(bw => { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); bw.Write(x.W); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V4f[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); bw.Write(xs[i].Z); bw.Write(xs[i].W); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V4d[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); bw.Write(xs[i].Z); bw.Write(xs[i].W); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V4l[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); bw.Write(xs[i].Z); bw.Write(xs[i].W); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this V4i[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].X); bw.Write(xs[i].Y); bw.Write(xs[i].Z); bw.Write(xs[i].W); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V4f> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); bw.Write(x.W); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V4d> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); bw.Write(x.W); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V4l> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); bw.Write(x.W); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<V4i> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.X); bw.Write(x.Y); bw.Write(x.Z); bw.Write(x.W); }
            });
        }

        #endregion

        #region C[34][bf]

        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this C3b x)
         => ComputeMd5Hash(bw => { bw.Write(x.R); bw.Write(x.G); bw.Write(x.B); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this C3f x)
         => ComputeMd5Hash(bw => { bw.Write(x.R); bw.Write(x.G); bw.Write(x.B); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this C4b x)
         => ComputeMd5Hash(bw => { bw.Write(x.R); bw.Write(x.G); bw.Write(x.B); bw.Write(x.A); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this C4f x)
         => ComputeMd5Hash(bw => { bw.Write(x.R); bw.Write(x.G); bw.Write(x.B); bw.Write(x.A); });
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this C3b[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].R); bw.Write(xs[i].G); bw.Write(xs[i].B); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this C3f[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].R); bw.Write(xs[i].G); bw.Write(xs[i].B); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this C4b[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].R); bw.Write(xs[i].G); bw.Write(xs[i].B); bw.Write(xs[i].A); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this C4f[] xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                for (var i = 0; i < xs.Length; i++) { bw.Write(xs[i].R); bw.Write(xs[i].G); bw.Write(xs[i].B); bw.Write(xs[i].A); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<C3b> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.R); bw.Write(x.G); bw.Write(x.B);}
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<C3f> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.R); bw.Write(x.G); bw.Write(x.B); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<C4b> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.R); bw.Write(x.G); bw.Write(x.B); bw.Write(x.A); }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<C4f> xs)
        {
            if (xs == null) return Guid.Empty;
            return ComputeMd5Hash(bw => {
                foreach (var x in xs) { bw.Write(x.R); bw.Write(x.G); bw.Write(x.B); bw.Write(x.A); }
            });
        }

        #endregion

        #region Plane3d

        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this Plane3d plane)
        {
            return ComputeMd5Hash(bw => {
                bw.Write(plane.Point.X); bw.Write(plane.Point.Y); bw.Write(plane.Point.Z);
                bw.Write(plane.Distance);
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this Plane3d[] planes)
        {
            return ComputeMd5Hash(bw => {
                foreach (var plane in planes)
                {
                    bw.Write(plane.Point.X); bw.Write(plane.Point.Y); bw.Write(plane.Point.Z);
                    bw.Write(plane.Distance);
                }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<Plane3d> planes)
        {
            return ComputeMd5Hash(bw => {
                foreach (var plane in planes)
                {
                    bw.Write(plane.Point.X); bw.Write(plane.Point.Y); bw.Write(plane.Point.Z);
                    bw.Write(plane.Distance);
                }
            });
        }

        #endregion

        #region Hull3d

        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this Hull3d hull)
        {
            if (hull.PlaneArray == null) return Guid.Empty;

            return ComputeMd5Hash(bw => {
                foreach (var plane in hull.PlaneArray)
                {
                    bw.Write(plane.Point.X); bw.Write(plane.Point.Y); bw.Write(plane.Point.Z);
                    bw.Write(plane.Distance);
                }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this Hull3d[] hulls)
        {
            if (hulls == null) return Guid.Empty;

            return ComputeMd5Hash(bw => {
                foreach (var hull in hulls)
                {
                    foreach (var plane in hull.PlaneArray)
                    {
                        bw.Write(plane.Point.X); bw.Write(plane.Point.Y); bw.Write(plane.Point.Z);
                        bw.Write(plane.Distance);
                    }
                }
            });
        }
        /// <summary>Computes MD5 hash of given data.</summary>
        public static Guid ComputeMd5Hash(this IEnumerable<Hull3d> hulls)
        {
            if (hulls == null) return Guid.Empty;

            return ComputeMd5Hash(bw => {
                foreach (var hull in hulls)
                {
                    foreach (var plane in hull.PlaneArray)
                    {
                        bw.Write(plane.Point.X); bw.Write(plane.Point.Y); bw.Write(plane.Point.Z);
                        bw.Write(plane.Distance);
                    }
                }
            });
        }

        #endregion
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Guid ComputeMd5Hash(Action<BinaryWriter> writeDataToHash)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);
            writeDataToHash(bw);
            ms.Seek(0, SeekOrigin.Begin);
            return new Guid(MD5.Create().ComputeHash(ms));
        }
    }
}
