/*
   Copyright (C) 2017-2022. Stefan Maierhofer.
    
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
            PlyFormat = new PointCloudFileFormat("PLY", new[] { ".ply" }, LaszipInfo, Chunks);
            PointCloudFileFormat.Register(PlyFormat);
        }

        /// <summary>
        /// Gets general info for PLY (.ply) file.
        /// </summary>
        public static PointFileInfo LaszipInfo(string filename, ParseConfig config)
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
            => PlyParser.Parse(filename, config.Verbose ? (s => Report.Line(s)) : null).Chunks(config);

        /// <summary>
        /// Parses PLY (.ply) file.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, ParseConfig config)
            => PlyParser.Parse(stream, config.Verbose ? (s => Report.Line(s)) : null).Chunks(config);

        private static IEnumerable<Chunk> Chunks(this PlyParser.Dataset data, ParseConfig config)
        {
            if (data.Header.Vertex == null) throw new Exception("No vertex data.");
            var totalCount = data.Header.Vertex.Count;

            var x = data.Vertices.TryGetProperty("x");
            var y = data.Vertices.TryGetProperty("y");
            var z = data.Vertices.TryGetProperty("z");
            if (x == null && y == null && z == null) throw new Exception("No vertex data.");

            for (var i0 = 0; i0 < totalCount; i0 += config.MaxChunkPointCount)
            {
                var count = Math.Min(config.MaxChunkPointCount, totalCount - i0);

                #region positions

                var ps = new V3d[count];

                if (x != null)
                {
                    var o = data.Vertices.Properties[x];
                    switch (x.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {x.DataType} not supported.");
                    }
                }
                if (y != null)
                {
                    var o = data.Vertices.Properties[y];
                    switch (y.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {y.DataType} not supported.");
                    }
                }
                if (z != null)
                {
                    var o = data.Vertices.Properties[z];
                    switch (z.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {z.DataType} not supported.");
                    }
                }

                #endregion

                #region colors

                var cr = data.Vertices.TryGetProperty("red") ?? data.Vertices.TryGetProperty("diffuse_red");
                var cg = data.Vertices.TryGetProperty("green") ?? data.Vertices.TryGetProperty("diffuse_green");
                var cb = data.Vertices.TryGetProperty("blue") ?? data.Vertices.TryGetProperty("blue");
                var hasColors = cr != null || cg != null || cb != null;
                var cs = hasColors ? new C4b[count] : null;
                if (hasColors) for (var i = 0; i < count; i++) cs![i].A = 255;

                if (cr != null)
                {
                    var o = data.Vertices.Properties[cr];
                    switch (cr.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) cs![i].R = (byte)(xs[i0 + i] * 255.999); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) cs![i].R = (byte)(xs[i0 + i] * 255.999); break; }
                        default: throw new Exception($"Data type {cr.DataType} not supported.");
                    }
                }
                if (cg != null)
                {
                    var o = data.Vertices.Properties[cg];
                    switch (cg.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) cs![i].G = (byte)(xs[i0 + i] * 255.999); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) cs![i].G = (byte)(xs[i0 + i] * 255.999); break; }
                        default: throw new Exception($"Data type {cg.DataType} not supported.");
                    }
                }
                if (cb != null)
                {
                    var o = data.Vertices.Properties[cb];
                    switch (cb.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) cs![i].B = (byte)(xs[i0 + i] * 255.999); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) cs![i].B = (byte)(xs[i0 + i] * 255.999); break; }
                        default: throw new Exception($"Data type {cb.DataType} not supported.");
                    }
                }

                #endregion

                #region normals

                var nx = data.Vertices.TryGetProperty("nx");
                var ny = data.Vertices.TryGetProperty("ny");
                var nz = data.Vertices.TryGetProperty("nz");
                var hasNormals = nx != null || ny != null || nz != null;
                var ns = hasNormals ? new V3f[count] : null;

                if (nx != null)
                {
                    var o = data.Vertices.Properties[nx];
                    switch (nx.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {nx.DataType} not supported.");
                    }
                }
                if (ny != null)
                {
                    var o = data.Vertices.Properties[ny];
                    switch (ny.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {ny.DataType} not supported.");
                    }
                }
                if (nz != null)
                {
                    var o = data.Vertices.Properties[nz];
                    switch (nz.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {nz.DataType} not supported.");
                    }
                }

                #endregion

                #region intensities

                var j = data.Vertices.TryGetProperty("scalar_intensity");
                var hasIntensities = j != null;
                var js = hasIntensities ? new int[count] : null;

                if (j != null)
                {
                    var o = data.Vertices.Properties[j];
                    switch (j.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) js![i] = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) js![i] = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) js![i] = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) js![i] = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) js![i] = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; var min = xs.Min(); var max = xs.Max(); var f = (max - min) * 255.999; for (var i = 0; i < count; i++) js![i] = (byte)((xs[i0 + i] - min) * f); break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; var min = xs.Min(); var max = xs.Max(); var f = (max - min) * 255.999; for (var i = 0; i < count; i++) js![i] = (byte)((xs[i0 + i] - min) * f); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; var min = xs.Min(); var max = xs.Max(); var f = (max - min) * 255.999; for (var i = 0; i < count; i++) js![i] = (byte)((xs[i0 + i] - min) * f); break; }
                        default: throw new Exception($"Data type {j.DataType} not supported.");
                    }
                }

                #endregion

                var chunk = new Chunk(ps, cs, ns, js);
                yield return chunk;
            }
        }
    }
}
