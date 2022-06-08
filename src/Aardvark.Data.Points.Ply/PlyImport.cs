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
            var vd = data.Vertex; 
            if (vd == null) throw new Exception("No vertex data.");

            var totalCount = data.Header.Vertex!.Count;

            var x = vd["x"];
            var y = vd["y"];
            var z = vd["z"];
            if (x == null && y == null && z == null) throw new Exception("No vertex data.");

            for (var i0 = 0; i0 < totalCount; i0 += config.MaxChunkPointCount)
            {
                var count = Math.Min(config.MaxChunkPointCount, totalCount - i0);

                #region positions

                var ps = new V3d[count];

                if (x != null)
                {
                    var o = x.Data;
                    switch (x.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ps[i].X = xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {x.Property.DataType} not supported.");
                    }
                }
                if (y != null)
                {
                    var o = y.Data;
                    switch (y.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ps[i].Y = xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {y.Property.DataType} not supported.");
                    }
                }
                if (z != null)
                {
                    var o = z.Data;
                    switch (z.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ps[i].Z = xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {z.Property.DataType} not supported.");
                    }
                }

                #endregion

                #region colors

                var cr = vd["red"];
                var cg = vd["green"];
                var cb = vd["blue"];
                var hasColors = cr != null || cg != null || cb != null;
                var cs = hasColors ? new C4b[count] : null;
                if (hasColors) for (var i = 0; i < count; i++) cs![i].A = 255;

                if (cr != null)
                {
                    var o = cr.Data;
                    switch (cr.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) cs![i].R = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) cs![i].R = (byte)(xs[i0 + i] * 255.999); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) cs![i].R = (byte)(xs[i0 + i] * 255.999); break; }
                        default: throw new Exception($"Data type {cr.Property.DataType} not supported.");
                    }
                }
                if (cg != null)
                {
                    var o = cg.Data;
                    switch (cg.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) cs![i].G = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) cs![i].G = (byte)(xs[i0 + i] * 255.999); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) cs![i].G = (byte)(xs[i0 + i] * 255.999); break; }
                        default: throw new Exception($"Data type {cg.Property.DataType} not supported.");
                    }
                }
                if (cb != null)
                {
                    var o = cb.Data;
                    switch (cb.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) cs![i].B = (byte)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) cs![i].B = (byte)(xs[i0 + i] * 255.999); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) cs![i].B = (byte)(xs[i0 + i] * 255.999); break; }
                        default: throw new Exception($"Data type {cb.Property.DataType} not supported.");
                    }
                }

                #endregion

                #region normals

                var nx = vd["nx"];
                var ny = vd["ny"];
                var nz = vd["nz"];
                var hasNormals = nx != null || ny != null || nz != null;
                var ns = hasNormals ? new V3f[count] : null;

                if (nx != null)
                {
                    var o = nx.Data;
                    switch (nx.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ns![i].X = (float)xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {nx.Property.DataType} not supported.");
                    }
                }
                if (ny != null)
                {
                    var o = ny.Data;
                    switch (ny.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ns![i].Y = (float)xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {ny.Property.DataType} not supported.");
                    }
                }
                if (nz != null)
                {
                    var o = nz.Data;
                    switch (nz.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; for (var i = 0; i < count; i++) ns![i].Z = (float)xs[i0 + i]; break; }
                        default: throw new Exception($"Data type {nz.Property.DataType} not supported.");
                    }
                }

                #endregion

                #region intensities

                var j = vd["scalar_intensity"];
                var hasIntensities = j != null;
                var js = hasIntensities ? new int[count] : null;

                if (j != null)
                {
                    var o = j.Data;
                    switch (j.Property.DataType)
                    {
                        case PlyParser.DataType.Int8   : { var xs = (sbyte[]) o; for (var i = 0; i < count; i++) js![i] = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt8  : { var xs = (byte[])  o; for (var i = 0; i < count; i++) js![i] = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int16  : { var xs = (short[]) o; for (var i = 0; i < count; i++) js![i] = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt16 : { var xs = (ushort[])o; for (var i = 0; i < count; i++) js![i] = xs[i0 + i]; break; }
                        case PlyParser.DataType.Int32  : { var xs = (int[])   o; for (var i = 0; i < count; i++) js![i] = xs[i0 + i]; break; }
                        case PlyParser.DataType.UInt32 : { var xs = (uint[])  o; var min = xs.Min(); var max = xs.Max(); var f = (max - min) * 255.999; for (var i = 0; i < count; i++) js![i] = (byte)((xs[i0 + i] - min) * f); break; }
                        case PlyParser.DataType.Float32: { var xs = (float[]) o; var min = xs.Min(); var max = xs.Max(); var f = (max - min) * 255.999; for (var i = 0; i < count; i++) js![i] = (byte)((xs[i0 + i] - min) * f); break; }
                        case PlyParser.DataType.Float64: { var xs = (double[])o; var min = xs.Min(); var max = xs.Max(); var f = (max - min) * 255.999; for (var i = 0; i < count; i++) js![i] = (byte)((xs[i0 + i] - min) * f); break; }
                        default: throw new Exception($"Data type {j.Property.DataType} not supported.");
                    }
                }

                #endregion

                var chunk = new Chunk(ps, cs, ns, js);
                yield return chunk;
            }
        }
    }
}
