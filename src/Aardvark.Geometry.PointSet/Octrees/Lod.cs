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
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
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

        internal static int[] ComputeLodCounts(int splitLimit, double[] fractions)
        {
            if (fractions == null) return null;
            if (fractions.Length != 8) throw new ArgumentOutOfRangeException(nameof(fractions));

            var remainder = 0.1;
            var counts = new int[8];
            for (var i = 0; i < 8; i++)
            {
                var fn = splitLimit * fractions[i] + remainder;
                var n = (int)fn;
                remainder = fn - n;
                counts[i] = n;
            };

            var e = splitLimit - counts.Sum();
            if (e != 0) throw new InvalidOperationException();

            return counts;
        }

        internal static V3f[] AggregateSubPositions(int[] counts, int splitLimit, V3d center, V3d?[] subCenters, V3f[][] xss)
        {
            var rs = new V3f[splitLimit];
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

        internal static T[] AggregateSubArrays<T>(int[] counts, int splitLimit, T[][] xss)
        {
            var rs = new T[splitLimit];
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

            if(i < splitLimit)
            {
                Array.Resize(ref rs, i);
                return rs;
            }
            else return rs;
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

                if (self.IsLeaf)
                {
                    if (!self.HasKdTree)
                    {
                        var kd = await self.Positions.Value.BuildKdTreeAsync();
                        var kdKey = Guid.NewGuid();
                        self.Storage.Add(kdKey, kd.Data);
                        self = self
                            .WithUpsert(Durable.Octree.PointRkdTreeFDataReference, kdKey)
                            ;
                    }

                    if (!self.HasNormals)
                    {
                        var ns = await self.Positions.Value.EstimateNormalsAsync(16, self.KdTree.Value);
                        var nsId = Guid.NewGuid();
                        self.Storage.Add(nsId, ns);
                        self = self
                            .WithUpsert(Durable.Octree.Normals3fReference, nsId)
                            ;
                    }

                    self = self.Without(PointSetNode.TemporaryImportNode);
                    if (self.Id != originalId) self = self.WriteToStore();

                    return self;
                }

                if (self.Subnodes == null || self.Subnodes.Length != 8) throw new InvalidOperationException();

                var subcellsAsync = self.Subnodes.Map(x => (x?.Value as PointSetNode)?.GenerateLod(octreeSplitLimit, callback, ct));
                await Task.WhenAll(subcellsAsync.Where(x => x != null));
                var subcells = subcellsAsync.Map(x => x?.Result);
                var subcellsTotalCount = (long)subcells.Sum(x => x?.PointCountTree);
                var fractions = ComputeLodFractions(subcells);
                var counts = ComputeLodCounts(octreeSplitLimit, fractions);

                // generate LoD data ...
                var needsCs = subcells.Any(x => x != null ? x.HasColors : false);
                var needsNs = subcells.Any(x => x != null ? x.HasNormals : false);
                var needsIs = subcells.Any(x => x != null ? x.HasIntensities : false);
                var needsKs = subcells.Any(x => x != null ? x.HasClassifications : false);
                var needsVs = subcells.Any(x => x != null ? x.HasVelocities : false);

                var subcenters = subcells.Map(x => x?.Center);
                var lodPs = AggregateSubPositions(counts, octreeSplitLimit, self.Center, subcenters, subcells.Map(x => x?.Positions?.Value));
                var lodCs = needsCs ? AggregateSubArrays(counts, octreeSplitLimit, subcells.Map(x => x?.Colors?.Value)) : null;
                var lodIs = needsIs ? AggregateSubArrays(counts, octreeSplitLimit, subcells.Map(x => x?.Intensities?.Value)) : null;
                var lodKs = needsKs ? AggregateSubArrays(counts, octreeSplitLimit, subcells.Map(x => x?.Classifications?.Value)) : null;
                var lodVs = needsVs ? AggregateSubArrays(counts, octreeSplitLimit, subcells.Map(x => x?.Velocities?.Value)) : null;
                var lodKd = await lodPs.BuildKdTreeAsync();
                var lodNs = await lodPs.EstimateNormalsAsync(16, lodKd); // Lod.AggregateSubArrays(counts, octreeSplitLimit, subcells.Map(x => x?.GetNormals3f()?.Value))

                var subnodeIds = subcells.Map(x => x != null ? x.Id : Guid.Empty);
                self = self.WithUpsert(Durable.Octree.SubnodesGuids, subnodeIds);

                // store LoD data ...
                var lodPsKey = Guid.NewGuid();
                self.Storage.Add(lodPsKey, lodPs);
                var lodKdKey = Guid.NewGuid();
                self.Storage.Add(lodKdKey, lodKd.Data);
                self = self
                    .WithUpsert(Durable.Octree.PointCountCell, lodPs.Length)
                    .WithUpsert(Durable.Octree.PointRkdTreeFDataReference, lodKdKey)
                    .WithUpsert(Durable.Octree.PositionsLocal3fReference, lodPsKey)
                    ;

                if (needsCs)
                {
                    var key = Guid.NewGuid();
                    self.Storage.Add(key, lodCs);
                    self = self.WithUpsert(Durable.Octree.Colors4bReference, key);
                }

                if (needsNs)
                {
                    var key = Guid.NewGuid();
                    self.Storage.Add(key, lodNs);
                    self = self.WithUpsert(Durable.Octree.Normals3fReference, key);
                }

                if (needsIs)
                {
                    var key = Guid.NewGuid();
                    self.Storage.Add(key, lodIs);
                    self = self.WithUpsert(Durable.Octree.Intensities1iReference, key);
                }

                if (needsKs)
                {
                    var key = Guid.NewGuid();
                    self.Storage.Add(key, lodKs);
                    self = self.WithUpsert(Durable.Octree.Classifications1bReference, key);
                }

                if (needsVs)
                {
                    var key = Guid.NewGuid();
                    self.Storage.Add(key, lodVs);
                    self = self.WithUpsert(Durable.Octree.Velocities3fReference, key);
                }

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
