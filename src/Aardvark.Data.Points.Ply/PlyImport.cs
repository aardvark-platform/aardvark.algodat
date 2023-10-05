/*
   Copyright (C) 2017-2022. Aardvark Platform Team.
    
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
using Ply.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aardvark.Data.Points.Import
{
    /// <summary>
    /// Importer for PLY format.
    /// </summary>
    [PointCloudFileFormatAttribute]
    public static class Ply
    {
        /// <summary>
        /// Pts file format.
        /// </summary>
        public static readonly PointCloudFileFormat PlyFormat;

        static Ply()
        {
            PlyFormat = new PointCloudFileFormat("PLY", new[] { ".ply" }, PlyInfo, Chunks);
            PointCloudFileFormat.Register(PlyFormat);
        }

        /// <summary>
        /// Gets general info for PLY (.ply) file.
        /// </summary>
        public static PointFileInfo PlyInfo(string filename, ParseConfig config)
        {
            var fileSizeInBytes = new FileInfo(filename).Length;
            var info = PlyParser.ParseHeader(filename);
            var infoVertices = info.Elements.FirstOrDefault(x => x.Type == PlyParser.ElementType.Vertex);
            return new PointFileInfo(filename, PlyFormat, fileSizeInBytes, infoVertices?.Count ?? 0, Box3d.Invalid);
        }

        /// <summary>
        /// Parses PLY (.ply) file.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(string filename, ParseConfig config)
            => PlyParser.Parse(filename, config.MaxChunkPointCount, config.Verbose ? (s => Report.Line(s)) : null).Chunks(config);

        /// <summary>
        /// Parses PLY (.ply) file.
        /// </summary>
#pragma warning disable IDE0060 // Remove unused parameter
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ParseConfig config)
#pragma warning restore IDE0060 // Remove unused parameter
            => PlyParser.Parse(stream, config.MaxChunkPointCount, config.Verbose ? (s => Report.Line(s)) : null).Chunks(config);

        /// <summary>
        /// Parses Ply.Net dataset.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this PlyParser.Dataset data)
            => Chunks(data, ParseConfig.Default);

        /// <summary>
        /// Parses Ply.Net dataset.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this PlyParser.Dataset data, ParseConfig config)
        {
            object? partIndex = config.EnabledProperties.PartIndices ? config.PartIndexOffset : null;

            var vds = data.Data.Where(x => x.Element.Type == PlyParser.ElementType.Vertex);
            
            foreach (var vd in vds)
            {
                var count = vd.Data[0].Data.Length;

                #region positions

                var x = vd["x"];
                var y = vd["y"];
                var z = vd["z"];
                if (x == null && y == null && z == null) throw new Exception("No vertex data.");
                
                var ps = new V3d[count];

                if (x != null) extractP(x, (v, i) => ps[i].X = v);
                if (y != null) extractP(y, (v, i) => ps[i].Y = v);
                if (z != null) extractP(z, (v, i) => ps[i].Z = v);

                static void extractP(PlyParser.PropertyData x, Action<double, int> a)
                {
                    var o = x.Data;
                    switch (x.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[] )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[]  )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[] )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[]   )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[]  )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.Int64  : { var xs = (long[]  )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.UInt64 : { var xs = (ulong[] )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.Float32: { var xs = (float[] )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        default: throw new Exception($"Data type {x.Property.DataType} not supported.");
                    }
                }

                #endregion

                #region colors

                var cr = vd["red"];
                var cg = vd["green"];
                var cb = vd["blue"];
                var ca = vd["alpha"];
                var hasColors = cr != null || cg != null || cb != null || ca != null;
                var cs = hasColors ? new C4b[count] : null;
                if (hasColors && ca == null) for (var i = 0; i < count; i++) cs![i].A = 255;

                if (cr != null) extractC(cr, (v, i) => cs![i].R = v);
                if (cg != null) extractC(cg, (v, i) => cs![i].G = v);
                if (cb != null) extractC(cb, (v, i) => cs![i].B = v);
                if (ca != null) extractC(ca, (v, i) => cs![i].A = v);

                static void extractC(PlyParser.PropertyData x, Action<byte, int> a)
                {
                    var o = x.Data;
                    switch (x.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[] )o; for (var i = 0; i < xs.Length; i++) a((byte)xs[i], i); break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[]  )o; for (var i = 0; i < xs.Length; i++) a((byte)xs[i], i); break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[] )o; for (var i = 0; i < xs.Length; i++) a((byte)xs[i], i); break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < xs.Length; i++) a((byte)xs[i], i); break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[]   )o; for (var i = 0; i < xs.Length; i++) a((byte)xs[i], i); break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[]  )o; for (var i = 0; i < xs.Length; i++) a((byte)xs[i], i); break; }
                        case PlyParser.DataType.Int64  : { var xs = (long[]  )o; for (var i = 0; i < xs.Length; i++) a((byte)xs[i], i); break; }
                        case PlyParser.DataType.UInt64 : { var xs = (ulong[] )o; for (var i = 0; i < xs.Length; i++) a((byte)xs[i], i); break; }
                        case PlyParser.DataType.Float32: { var xs = (float[] )o; for (var i = 0; i < xs.Length; i++) a((byte)(xs[i] * 255.999), i); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < xs.Length; i++) a((byte)(xs[i] * 255.999), i); break; }
                        default: throw new Exception($"Data type {x.Property.DataType} not supported.");
                    }
                }

                #endregion

                #region normals

                var nx = vd["nx"];
                var ny = vd["ny"];
                var nz = vd["nz"];
                var hasNormals = nx != null || ny != null || nz != null;
                var ns = hasNormals ? new V3f[count] : null;

                if (nx != null) extractN(nx, (v, i) => ns![i].X = v);
                if (ny != null) extractN(ny, (v, i) => ns![i].Y = v);
                if (nz != null) extractN(nz, (v, i) => ns![i].Z = v);

                static void extractN(PlyParser.PropertyData x, Action<float, int> a)
                {
                    var o = x.Data;
                    switch (x.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[] )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[]  )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[] )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[]   )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[]  )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.Int64  : { var xs = (long[]  )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.UInt64 : { var xs = (ulong[] )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.Float32: { var xs = (float[] )o; for (var i = 0; i < xs.Length; i++) a(xs[i], i); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < xs.Length; i++) a((float)xs[i], i); break; }
                        default: throw new Exception($"Data type {x.Property.DataType} not supported.");
                    }
                }

                #endregion

                #region intensities

                var j = vd["scalar_intensity"] ?? vd["intensity"];
                var hasIntensities = j != null;
                var js = hasIntensities ? new int[count] : null;

                if (j != null)
                {
                    var o = j.Data;
                    switch (j.Property.DataType)
                    {
                        case PlyParser.DataType.Int8:    { var xs = (sbyte[] )o; for (var i = 0; i < count; i++) js![i] = xs[i]; break; }
                        case PlyParser.DataType.UInt8:   { var xs = (byte[]  )o; for (var i = 0; i < count; i++) js![i] = xs[i]; break; }
                        case PlyParser.DataType.Int16:   { var xs = (short[] )o; for (var i = 0; i < count; i++) js![i] = xs[i]; break; }
                        case PlyParser.DataType.UInt16:  { var xs = (ushort[])o; for (var i = 0; i < count; i++) js![i] = xs[i]; break; }
                        case PlyParser.DataType.Int32:   { var xs = (int[]   )o; for (var i = 0; i < count; i++) js![i] = xs[i]; break; }
                        case PlyParser.DataType.UInt32:  { rescale<uint  >(x => (double)x); break; }
                        case PlyParser.DataType.Int64:   { rescale<long  >(x => (double)x); break; }
                        case PlyParser.DataType.UInt64:  { rescale<ulong >(x => (double)x); break; }
                        case PlyParser.DataType.Float32: { rescale<float >(x => (double)x); break; }
                        case PlyParser.DataType.Float64: { rescale<double>(x => (double)x); break; }
                        default: throw new Exception($"Data type {j.Property.DataType} not supported.");
                    }

                    void rescale<T>(Func<T, double> toDouble)
                    {
                        var xs = ((T[])o).Map(toDouble);
                        var min = xs.Min();
                        var max = xs.Max();
                        if (min >= 0 && max <= 255)
                        {
                            for (var i = 0; i < count; i++) js![i] = (byte)xs[i];
                        }
                        else if (min == max)
                        {
                            for (var i = 0; i < count; i++) js![i] = 255;
                        }
                        else
                        {
                            var f = 255.999 / (max - min);
                            for (var i = 0; i < count; i++) js![i] = (byte)((xs[i] - min) * f);
                        }
                    }
                }

                #endregion

                #region classifications

                var k = vd["scalar_classification"] ?? vd["classification"];
                var hasClassification = k != null;
                var ks = hasClassification ? new byte[count] : null;

                if (k != null)
                {
                    var o = k.Data;
                    switch (k.Property.DataType)
                    {
                        case PlyParser.DataType.Int8:    { var xs = (sbyte[] )o; for (var i = 0; i < count; i++) ks![i] = (byte)xs[i]; break; }
                        case PlyParser.DataType.UInt8:   { var xs = (byte[]  )o; for (var i = 0; i < count; i++) ks![i] = (byte)xs[i]; break; }
                        case PlyParser.DataType.Int16:   { var xs = (short[] )o; for (var i = 0; i < count; i++) ks![i] = (byte)xs[i]; break; }
                        case PlyParser.DataType.UInt16:  { var xs = (ushort[])o; for (var i = 0; i < count; i++) ks![i] = (byte)xs[i]; break; }
                        case PlyParser.DataType.Int32:   { var xs = (int[]   )o; for (var i = 0; i < count; i++) ks![i] = (byte)xs[i]; break; }
                        case PlyParser.DataType.UInt32:  { var xs = (uint[]  )o; for (var i = 0; i < count; i++) ks![i] = (byte)xs[i]; break; }
                        case PlyParser.DataType.Int64:   { var xs = (long[]  )o; for (var i = 0; i < count; i++) ks![i] = (byte)xs[i]; break; }
                        case PlyParser.DataType.UInt64:  { var xs = (ulong[] )o; for (var i = 0; i < count; i++) ks![i] = (byte)xs[i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[] )o; for (var i = 0; i < count; i++) ks![i] = (byte)xs[i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ks![i] = (byte)xs[i]; break; }
                        default: throw new Exception($"Data type {k.Property.DataType} not supported.");
                    }
                }

                #endregion

                var chunk = new Chunk(ps, cs, ns, js, ks, parts: partIndex, bbox: null);

                yield return chunk;
            }
        }
    }
}
