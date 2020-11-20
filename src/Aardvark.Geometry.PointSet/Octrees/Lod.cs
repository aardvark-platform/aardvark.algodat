/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Points
{
    public static class LodExtensions
    {
        private static double[] ComputeLodFractions(long[] counts)
        {
            if (counts == null) return null;
            if (counts.Length != 8) throw new ArgumentOutOfRangeException(nameof(counts));

            var sum = 0L;
            for (var i = 0; i < 8; i++) sum += counts[i];

            var fractions = new double[8];
            for (var i = 0; i < 8; i++) fractions[i] = counts[i] / (double)sum;

            return fractions;
        }

        internal static double[] ComputeLodFractions(IPointCloudNode[] subnodes)
        {
            if (subnodes == null) return null;
            if (subnodes.Length != 8) throw new ArgumentOutOfRangeException(nameof(subnodes));

            var counts = new long[8];
            for (var i = 0; i < 8; i++) counts[i] = subnodes[i] != null ? subnodes[i].PointCountTree : 0;
            return ComputeLodFractions(counts);
        }

        internal static int[] ComputeLodCounts(int aggregateCount, double[] fractions)
        {
            if (fractions == null) return null;
            if (fractions.Length != 8) throw new ArgumentOutOfRangeException(nameof(fractions));

            var remainder = 0.1;
            var counts = new int[8];
            for (var i = 0; i < 8; i++)
            {
                var fn = aggregateCount * fractions[i] + remainder;
                var n = (int)fn;
                remainder = fn - n;
                counts[i] = n;
            };

            var e = aggregateCount - counts.Sum();
            if (e != 0) throw new InvalidOperationException();

            return counts;
        }

        internal static V3f[] AggregateSubPositions(int[] counts, int aggregateCount, V3d center, V3d?[] subCenters, V3f[][] xss)
        {
            var rs = new V3f[aggregateCount];
            var i = 0;
            for (var ci = 0; ci < 8; ci++)
            {
                if (counts[ci] == 0) continue;
                var xs = xss[ci];
                var c = subCenters[ci].Value;

                var jmax = xs.Length;
                var dj = (jmax + 0.49) / counts[ci];
                for (var j = 0.0; j < jmax; j += dj)
                {
                    rs[i++] = new V3f((V3d)xs[(int)j] + c - center);
                }
            }
            return rs;
        }

        internal static T[] AggregateSubArrays<T>(int[] counts, int aggregateCount, T[][] xss)
        {
            var rs = new T[aggregateCount];
            var i = 0;
            for (var ci = 0; ci < 8; ci++)
            {
                if (counts[ci] == 0) continue;
                var xs = xss[ci];

                var jmax = xs.Length;
                if (jmax <= counts[ci])
                {
                    xs.CopyTo(0, xs.Length, rs, i);
                    i += xs.Length;
                }
                else
                {
                    var dj = (jmax + 0.49) / counts[ci];
                    for (var j = 0.0; j < jmax; j += dj)
                    {
                        rs[i++] = xs[(int)j];
                    }
                }
            }

            if (i < aggregateCount)
            {
                Array.Resize(ref rs, i);
                return rs;
            }
            else return rs;
        }

        internal static Array AggregateSubArrays(int[] counts, int splitLimit, object[] arrays)
        {
            var t = arrays.First(x => x != null).GetType().GetElementType();
            return arrays.First(x => x != null) switch
            {
                Guid[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (Guid[])x)),
                string[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (string[])x)),
                byte[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (byte[])x)),
                sbyte[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (sbyte[])x)),
                short[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (short[])x)),
                ushort[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (ushort[])x)),
                int[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (int[])x)),
                uint[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (uint[])x)),
                long[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (long[])x)),
                ulong[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (ulong[])x)),
                float[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (float[])x)),
                double[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (double[])x)),
                decimal[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (decimal[])x)),
                V2d[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V2d[])x)),
                V2f[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V2f[])x)),
                V2i[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V2i[])x)),
                V2l[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V2l[])x)),
                V3d[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V3d[])x)),
                V3f[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V3f[])x)),
                V3i[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V3i[])x)),
                V3l[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V3l[])x)),
                V4d[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V4d[])x)),
                V4f[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V4f[])x)),
                V4i[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V4i[])x)),
                V4l[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (V4l[])x)),
                M22d[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (M22f[])x)),
                M22f[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (M22d[])x)),
                M33d[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (M33f[])x)),
                M33f[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (M33d[])x)),
                M44d[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (M44f[])x)),
                M44f[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (M44d[])x)),
                Trafo2d[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (Trafo2d[])x)),
                Trafo2f[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (Trafo2f[])x)),
                Trafo3d[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (Trafo3d[])x)),
                Trafo3f[] _ => AggregateSubArrays(counts, splitLimit, arrays.Map(x => (Trafo3f[])x)),
                _ => throw new Exception($"LoD aggregation for type {t} not supported. Error 13c77814-f323-41fb-a7b6-c164973b7b02.")
            };
        }

        private static async Task<PointSet> GenerateLod(this PointSet self, string key, Action callback, CancellationToken ct)
        {
            try
            {
                if (self.IsEmpty) return self;

                if (!(self.Root.Value is PointSetNode root)) throw new InvalidOperationException(
                    "GenerateLod is only valid for PointSetNodes. Invariant 1d530d98-9ea4-4281-894f-bb91a6b8a2cf."
                    );

                var lod = await root.GenerateLod(self.SplitLimit, callback, ct);
                var result = new PointSet(self.Storage, key, lod.Id, self.SplitLimit);
                self.Storage.Add(key, result);
                return result;
            }
            catch (Exception e)
            {
                Report.Error(e.ToString());
                throw;
            }
        }

        /// <summary>
        /// Returns new octree with LOD data created.
        /// </summary>
        public static PointSet GenerateLod(this PointSet self, ImportConfig config)
        {
            if (self.Root == null) return self;

            var nodeCount = self.Root?.Value?.CountNodes(true) ?? 0;
            var loddedNodesCount = 0L;
            if (config.Verbose) Console.WriteLine();
            var result = self.GenerateLod(config.Key, () =>
            {
                config.CancellationToken.ThrowIfCancellationRequested();
                var i = Interlocked.Increment(ref loddedNodesCount);
                if (config.Verbose) Console.Write($"[Lod] {i}/{nodeCount}\r");
                if (i % 100 == 0) config.ProgressCallback(loddedNodesCount / (double)nodeCount);
            }, config.CancellationToken);

            result.Wait();

            config.ProgressCallback(1.0);

            return result.Result;
        }

        internal static async Task<IPointCloudNode> GenerateLod(this PointSetNode self,
            int octreeSplitLimit, Action callback,
            CancellationToken ct)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            ct.ThrowIfCancellationRequested();

            try
            {
                var originalId = self.Id;
                var upsertData = ImmutableDictionary<Durable.Def, object>.Empty;

                if (self.IsLeaf)
                {
                    var kd = self.KdTree?.Value;

                    if (kd == null)
                    {
                        kd = await self.Positions.Value.BuildKdTreeAsync();
                        var kdKey = Guid.NewGuid();
                        self.Storage.Add(kdKey, kd.Data);
                        upsertData = upsertData.Add(Durable.Octree.PointRkdTreeFDataReference, kdKey);
                    }

                    if (!self.HasNormals)
                    {
                        var ns = await self.Positions.Value.EstimateNormalsAsync(16, kd);
                        var nsId = Guid.NewGuid();
                        self.Storage.Add(nsId, ns);
                        upsertData = upsertData.Add(Durable.Octree.Normals3fReference, nsId);
                    }

                    if (upsertData.Count > 0) self = self.With(upsertData);
                    self = self.Without(PointSetNode.TemporaryImportNode);
                    if (self.Id != originalId) self = self.WriteToStore();

                    return self;
                }

                if (self.Subnodes == null || self.Subnodes.Length != 8) throw new InvalidOperationException();

                var subcellsAsync = self.Subnodes.Map(x => (x?.Value as PointSetNode)?.GenerateLod(octreeSplitLimit, callback, ct));
                await Task.WhenAll(subcellsAsync.Where(x => x != null));
                var subcells = subcellsAsync.Map(x => x?.Result);
                var fractions = ComputeLodFractions(subcells);
                var aggregateCount = Math.Min(octreeSplitLimit, subcells.Sum(x => x?.PointCountCell) ?? 0);
                var counts = ComputeLodCounts(aggregateCount, fractions);

                // generate LoD data ...

                var firstNonEmptySubnode = subcells.First(n => n != null);
                var lodAttributeCandidates = firstNonEmptySubnode.Properties.Keys.Where(x => x.IsArray && x != Durable.Octree.PositionsLocal3f).ToArray();

                // ... shift relative positions
                //     and from lod-positions build kd-tree and generate normals ...
                var subcenters = subcells.Map(x => x?.Center);
                var lodPs = AggregateSubPositions(counts, aggregateCount, self.Center, subcenters, subcells.Map(x => x?.Positions?.Value));
                var lodKd = await lodPs.BuildKdTreeAsync();
                var lodNs = await lodPs.EstimateNormalsAsync(16, lodKd); // Lod.AggregateSubArrays(counts, octreeSplitLimit, subcells.Map(x => x?.GetNormals3f()?.Value))

                // ... generate lod for all other attributes
                foreach (var def in lodAttributeCandidates)
                {
                    if (def == Durable.Octree.SubnodesGuids) continue;
                    var lod = AggregateSubArrays(counts, octreeSplitLimit, subcells.Map(x => x?.Properties[def]));
                    upsertData = upsertData.Add(def, lod);
                }

                var subnodeIds = subcells.Map(x => x != null ? x.Id : Guid.Empty);
                upsertData = upsertData.Add(Durable.Octree.SubnodesGuids, subnodeIds);

                // store LoD data ...
                //var lodPsKey = Guid.NewGuid();
                //self.Storage.Add(lodPsKey, lodPs);
                var lodKdKey = Guid.NewGuid();
                self.Storage.Add(lodKdKey, lodKd.Data);
                upsertData = upsertData
                    .Add(Durable.Octree.PositionsLocal3f, lodPs)
                    .Add(Durable.Octree.PointCountCell, lodPs.Length)
                    .Add(Durable.Octree.PointRkdTreeFDataReference, lodKdKey)
                    //.Add(Durable.Octree.PositionsLocal3fReference, lodPsKey)
                    ;


                self = self.Without(PointSetNode.TemporaryImportNode);

                if (self.Id != originalId)
                {
                    self = self.WriteToStore();
                }

                return self;
            }
            finally
            {
                callback?.Invoke();
            }
        }
    }
}
