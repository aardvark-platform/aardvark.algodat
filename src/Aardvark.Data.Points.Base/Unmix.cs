using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public static class ChunkExtensions
    {
        /// <summary>
        /// </summary>
        public static IEnumerable<Chunk> ImmutableUnmixOutOfCore(this IEnumerable<Chunk> chunks, string tmpdir, int binsExponent, ParseConfig config)
        {
            var binsExponentFactor = 1.0 / Math.Pow(2.0, binsExponent);
            try
            {
                Report.BeginTimed("ImmutableUnmixOutOfCore");
                tmpdir = Path.Combine(tmpdir, Guid.NewGuid().ToString());
                Directory.CreateDirectory(tmpdir);

                var root = default(Cell?);
                var hasNormals = false;
                var hasColors = false;
                var hasIntensities = false;
                var hasClassifications = false;
                var hasVelocities = false;

                var countChunks = 0L;
                var countOriginal = 0L;
                Report.BeginTimed("processing chunks");
                var lockedFilenames = new HashSet<string>();
                Parallel.ForEach(chunks, chunk =>
                {
                    countChunks++;
                    countOriginal += chunk.Count;

                    hasNormals = chunk.HasNormals;
                    hasColors = chunk.HasColors;
                    hasIntensities = chunk.HasIntensities;
                    hasClassifications = chunk.HasClassifications;
                    hasVelocities = chunk.HasVelocities;

                    var _ps = chunk.Positions;
                    var _ns = chunk.Normals;
                    var _js = chunk.Intensities;
                    var _cs = chunk.Colors;
                    var _ks = chunk.Classifications;
                    var _vs = chunk.Velocities;

                    // binning
                    var map = new Dictionary<V3l, List<int>>();
                    for (var i = 0; i < chunk.Count; i++)
                    {
                        var p = _ps[i];
                        var key = binsExponent == 0 ? new V3l(p) : new V3l(p * binsExponentFactor);
                        if (!map.TryGetValue(key, out var value)) map[key] = value = new List<int>();
                        value.Add(i);
                    }

                    // store cells
                    foreach (var kv in map)
                    {
                        var cell = new Cell(kv.Key.X, kv.Key.Y, kv.Key.Z, binsExponent);
                        root = root.HasValue ? new Cell(new Box3d(root.Value.BoundingBox, cell.BoundingBox)) : cell;

                        var filename = Path.Combine(tmpdir, $"{kv.Key.X}_{kv.Key.Y}_{kv.Key.Z}");
                        while (true)
                        {
                            lock (lockedFilenames)
                            {
                                if (lockedFilenames.Add(filename)) break;
                            }
                            Task.Delay(100);
                        }
                        using (var f = File.Open(filename, FileMode.Append, FileAccess.Write, FileShare.None))
                        using (var bw = new BinaryWriter(f))
                        {
                            var ia = kv.Value;
                            foreach (var i in ia)
                            {
                                var p = _ps[i];
                                bw.Write(p.X); bw.Write(p.Y); bw.Write(p.Z);
                                if (hasNormals) { var n = _ns[i]; bw.Write(n.X); bw.Write(n.Y); bw.Write(n.Z); }
                                if (hasIntensities) { var j = _js[i]; bw.Write(j); }
                                if (hasColors) { var c = _cs[i]; var x = c.R + c.G << 8 + c.B << 16; bw.Write(x); }
                                if (hasClassifications) { var k = _ks[i]; bw.Write(k); }
                                if (hasVelocities) { var v = _vs[i]; bw.Write(v.X); bw.Write(v.Y); bw.Write(v.Z); }
                            }
                        }
                        lock (lockedFilenames)
                        {
                            lockedFilenames.Remove(filename);
                        }
                    }
                });
                Report.EndTimed();

                Report.Line($"[ImmutableUnmixOutOfCore] chunk count = {countChunks:N0}");
                Report.Line($"[ImmutableUnmixOutOfCore] root cell   = {root:N0}");
                Report.Line($"[ImmutableUnmixOutOfCore] point count = {countOriginal:N0}");

                // construct hierarchy
                Report.BeginTimed("constructing hierarchy");
                foreach (var path in Directory.EnumerateFiles(tmpdir))
                {
                    var filename = Path.GetFileName(path);
                    var ts = filename.Split('_');
                    var cell = new Cell(long.Parse(ts[0]), long.Parse(ts[1]), long.Parse(ts[2]), binsExponent);
                    var stack = new Stack<string>();
                    while (cell.Exponent < root.Value.Exponent)
                    {
                        cell = cell.Parent;
                        stack.Push($"{cell.X}_{cell.Y}_{cell.Z}_{cell.Exponent}");
                    }
                    var dir = tmpdir;
                    while (stack.Count > 0) dir = Path.Combine(dir, stack.Pop());
                    try
                    {
                        Directory.CreateDirectory(dir);
                        File.Move(path, Path.Combine(dir, filename));
                    }
                    catch (Exception e)
                    {
                        Report.Error(e.ToString());
                        Report.Error($"[dir ] {dir}");
                        Report.Error($"[move] {path} -> {Path.Combine(dir, filename)}");
                    }
                }
                Report.EndTimed();

                // filter min distance
                Report.BeginTimed("filtering min distance");
                var countFiltered = 0L;
                Parallel.ForEach(Directory.EnumerateFiles(tmpdir, "*", SearchOption.AllDirectories), path =>
                {
                    var filename = Path.GetFileName(path);
                    var ts = filename.Split('_');
                    var cell = new Cell(long.Parse(ts[0]), long.Parse(ts[1]), long.Parse(ts[2]), binsExponent);

                    var _ps = new List<V3d>();
                    var _ns = hasNormals ? new List<V3f>() : null;
                    var _js = hasIntensities ? new List<int>() : null;
                    var _cs = hasColors ? new List<C4b>() : null;
                    var _ks = hasClassifications ? new List<byte>() : null;
                    var _vs = hasVelocities ? new List<V3f>() : null;

                    using (var f = File.Open(path, FileMode.Open, FileAccess.Read))
                    using (var br = new BinaryReader(f))
                    {
                        try
                        {
                            while (br.BaseStream.Position < br.BaseStream.Length)
                            {
                                _ps.Add(new V3d(br.ReadDouble(), br.ReadDouble(), br.ReadDouble()));
                                if (hasNormals) _ns.Add(new V3f(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                                if (hasIntensities) _js.Add(br.ReadInt32());
                                if (hasColors) { var x = br.ReadInt32(); _cs.Add(new C4b(x & 0xff, (x >> 8) & 0xff, (x >> 16) & 0xff)); }
                                if (hasClassifications) _ks.Add(br.ReadByte());
                                if (hasVelocities) _vs.Add(new V3f(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                            }
                        }
                        catch (Exception e)
                        {
                            Report.Error(e.ToString());
                            return;
                        }
                    }

                    var chunk = new Chunk(_ps, _cs, _ns, _js, _ks, _vs);
                    var chunkFiltered = chunk.ImmutableFilterMinDistByCell(cell, config);
                    countFiltered += chunkFiltered.Count;

                    //Report.Line($"[{cell}] {countFiltered:N0}/{countOriginal:N0} ({countOriginal- countFiltered:N0})");

                    using (var f = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    using (var bw = new BinaryWriter(f))
                    {
                        for (var i = 0; i < chunkFiltered.Count; i++)
                        {
                            var p = chunkFiltered.Positions[i];
                            bw.Write(p.X); bw.Write(p.Y); bw.Write(p.Z);
                            if (hasNormals) { var n = chunkFiltered.Normals[i]; bw.Write(n.X); bw.Write(n.Y); bw.Write(n.Z); }
                            if (hasIntensities) { var j = chunkFiltered.Intensities[i]; bw.Write(j); }
                            if (hasColors) { var c = chunkFiltered.Colors[i]; var x = c.R + c.G << 8 + c.B << 16; bw.Write(x); }
                            if (hasClassifications) { var k = chunkFiltered.Classifications[i]; bw.Write(k); }
                            if (hasVelocities) { var v = chunkFiltered.Velocities[i]; bw.Write(v.X); bw.Write(v.Y); bw.Write(v.Z); }
                        }
                    }
                });
                Report.Line($"{countFiltered:N0}/{countOriginal:N0} (removed {countOriginal - countFiltered:N0} points)");
                Report.EndTimed();

                // return final chunks
                var ps = new List<V3d>();
                var ns = hasNormals ? new List<V3f>() : null;
                var js = hasIntensities ? new List<int>() : null;
                var cs = hasColors ? new List<C4b>() : null;
                var ks = hasClassifications ? new List<byte>() : null;
                var vs = hasVelocities ? new List<V3f>() : null;
                foreach (var path in Directory.EnumerateFiles(tmpdir, "*", SearchOption.AllDirectories))
                {
                    using (var f = File.Open(path, FileMode.Open, FileAccess.Read))
                    using (var br = new BinaryReader(f))
                    {
                        try
                        {
                            while (br.BaseStream.Position < br.BaseStream.Length)
                            {
                                ps.Add(new V3d(br.ReadDouble(), br.ReadDouble(), br.ReadDouble()));
                                if (hasNormals) ns.Add(new V3f(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                                if (hasIntensities) js.Add(br.ReadInt32());
                                if (hasColors) { var x = br.ReadInt32(); cs.Add(new C4b(x & 0xff, (x >> 8) & 0xff, (x >> 16) & 0xff)); }
                                if (hasClassifications) ks.Add(br.ReadByte());
                                if (hasVelocities) vs.Add(new V3f(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                            }
                        }
                        catch (Exception e)
                        {
                            Report.Error(e.ToString());
                            ps = new List<V3d>();
                            ns = hasNormals ? new List<V3f>() : null;
                            js = hasIntensities ? new List<int>() : null;
                            cs = hasColors ? new List<C4b>() : null;
                            ks = hasClassifications ? new List<byte>() : null;
                            vs = hasVelocities ? new List<V3f>() : null;
                            continue;
                        }
                    }
                    File.Delete(path);

                    if (ps.Count >= config.MaxChunkPointCount)
                    {
                        yield return new Chunk(ps, cs, ns, js, ks, vs);
                        ps = new List<V3d>();
                        ns = hasNormals ? new List<V3f>() : null;
                        js = hasIntensities ? new List<int>() : null;
                        cs = hasColors ? new List<C4b>() : null;
                        ks = hasClassifications ? new List<byte>() : null;
                        vs = hasVelocities ? new List<V3f>() : null;
                    }
                }
                // rest?
                if (ps.Count >= 0)
                {
                    yield return new Chunk(ps, cs, ns, js, ks, vs);
                }
            }
            finally
            {
                try
                {
                    Report.BeginTimed("deleting temporary data");
                    Directory.Delete(tmpdir, true);
                    Report.EndTimed();
                }
                catch (Exception e)
                {
                    Report.Warn(e.ToString());
                }

                Report.EndTimed();
            }
        }

        /// <summary>
        /// Merges many chunks into a single chunk. 
        /// </summary>
        public static Chunk Union(this IEnumerable<Chunk> chunks)
        {
            var result = Chunk.Empty;
            foreach (var chunk in chunks) result = result.Union(chunk);
            return result;
        }
    }
}
