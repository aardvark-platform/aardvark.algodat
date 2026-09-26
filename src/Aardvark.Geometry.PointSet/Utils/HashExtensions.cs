/*
    Copyright (C) 2006-2024. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Aardvark.Base;

namespace Aardvark.Geometry.Points;

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

    /// <summary>
    /// Hashes the stored Normal.X, Normal.Y, Normal.Z and Distance as four
    /// little-endian IEEE 754 doubles, without normalization. The digest bytes
    /// form a Guid. This replaces the old Point-based fingerprint; callers must
    /// invalidate caches keyed by the old plane/hull fingerprints.
    /// </summary>
    public static Guid ComputeMd5Hash(this Plane3d plane)
        => ComputeGeometryHash(writer => writer.WritePlane(plane));

    /// <summary>
    /// Hashes ordered plane coefficient records using the scalar encoding,
    /// without a collection header. A singleton hashes like its scalar plane.
    /// </summary>
    /// <exception cref="NullReferenceException">The array is null.</exception>
    public static Guid ComputeMd5Hash(this Plane3d[] planes)
        => ComputeGeometryHash(writer => {
            foreach (var plane in planes) writer.WritePlane(plane);
        });

    /// <summary>
    /// Hashes ordered plane coefficient records using the scalar encoding,
    /// enumerating the sequence once without a collection header.
    /// </summary>
    /// <exception cref="NullReferenceException">The sequence is null.</exception>
    public static Guid ComputeMd5Hash(this IEnumerable<Plane3d> planes)
        => ComputeGeometryHash(writer => {
            foreach (var plane in planes) writer.WritePlane(plane);
        });

    #endregion

    #region Hull3d

    /// <summary>
    /// Hashes the little-endian Int32 plane count followed by ordered plane
    /// coefficient records, without normalization or reordering. An invalid hull
    /// (null PlaneArray) returns Guid.Empty. This replaces the old unframed,
    /// Point-based fingerprint; callers must invalidate caches using it.
    /// </summary>
    public static Guid ComputeMd5Hash(this Hull3d hull)
    {
        if (hull.PlaneArray == null) return Guid.Empty;
        return ComputeGeometryHash(writer => writer.WriteHull(hull));
    }

    /// <summary>
    /// Hashes ordered hull records using the scalar count/coefficients encoding.
    /// A valid singleton hashes like its scalar hull; an empty array differs from
    /// one empty hull. A null array returns Guid.Empty.
    /// </summary>
    /// <exception cref="NullReferenceException">An element has a null PlaneArray.</exception>
    public static Guid ComputeMd5Hash(this Hull3d[] hulls)
    {
        if (hulls == null) return Guid.Empty;
        return ComputeGeometryHash(writer => {
            foreach (var hull in hulls) writer.WriteHull(hull);
        });
    }

    /// <summary>
    /// Hashes ordered hull records using the scalar count/coefficients encoding,
    /// enumerating the sequence once. A null sequence returns Guid.Empty.
    /// </summary>
    /// <exception cref="NullReferenceException">An element has a null PlaneArray.</exception>
    public static Guid ComputeMd5Hash(this IEnumerable<Hull3d> hulls)
    {
        if (hulls == null) return Guid.Empty;
        return ComputeGeometryHash(writer => {
            foreach (var hull in hulls) writer.WriteHull(hull);
        });
    }

    #endregion

    private static Guid ComputeGeometryHash(Action<GeometryHashWriter> writeData)
    {
        using var writer = new GeometryHashWriter();
        writeData(writer);
        return writer.Finish();
    }

    // Geometry framing can push a growing MemoryStream across a capacity boundary.
    // Feed bounded batches to MD5 instead, with no input-size-dependent allocations
    // or count pass. Keep unrelated vector/color hashing on its established path.
    private sealed class GeometryHashWriter : IDisposable
    {
        private const int BufferSize = 4096;
        private readonly byte[] m_buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        private int m_count;
        private IncrementalHash? m_hash;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePlane(Plane3d plane)
        {
            var bytes = Reserve(32);
            BinaryPrimitives.WriteInt64LittleEndian(bytes, BitConverter.DoubleToInt64Bits(plane.Normal.X));
            BinaryPrimitives.WriteInt64LittleEndian(bytes.Slice(8), BitConverter.DoubleToInt64Bits(plane.Normal.Y));
            BinaryPrimitives.WriteInt64LittleEndian(bytes.Slice(16), BitConverter.DoubleToInt64Bits(plane.Normal.Z));
            BinaryPrimitives.WriteInt64LittleEndian(bytes.Slice(24), BitConverter.DoubleToInt64Bits(plane.Distance));
        }

        public void WriteHull(Hull3d hull)
        {
            var planes = hull.PlaneArray;
            var count = planes.Length; // Invalid elements retain their NullReferenceException contract.
            BinaryPrimitives.WriteInt32LittleEndian(Reserve(4), count);
            foreach (var plane in planes) WritePlane(plane);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Span<byte> Reserve(int size)
        {
            if (m_count + size > BufferSize) Flush();
            var bytes = m_buffer.AsSpan(m_count, size);
            m_count += size;
            return bytes;
        }

        private void Flush()
        {
            // Lazy creation also keeps rented-buffer cleanup exception-safe if
            // creating the platform hash provider fails.
            m_hash ??= IncrementalHash.CreateHash(HashAlgorithmName.MD5);
            m_hash.AppendData(m_buffer, 0, m_count);
            m_count = 0;
        }

        public Guid Finish()
        {
            Flush();
            return new Guid(m_hash!.GetHashAndReset());
        }

        public void Dispose()
        {
            m_hash?.Dispose();
            ArrayPool<byte>.Shared.Return(m_buffer);
        }
    }
    
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
