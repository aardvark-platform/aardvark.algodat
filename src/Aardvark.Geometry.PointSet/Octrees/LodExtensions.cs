/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Data.Points;
using System;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class Lod
    {
        /// <summary></summary>
        public static double[] ComputeLodFractions(long[] counts)
        {
            if (counts == null) return null;
            if (counts.Length != 8) throw new ArgumentOutOfRangeException();

            var sum = 0L;
            for (var i = 0; i < 8; i++) sum += counts[i];

            var fractions = new double[8];
            for (var i = 0; i < 8; i++) fractions[i] = counts[i] / (double)sum;

            return fractions;
        }

        /// <summary></summary>
        public static double[] ComputeLodFractions(IPointCloudNode[] subnodes)
        {
            if (subnodes == null) return null;
            if (subnodes.Length != 8) throw new ArgumentOutOfRangeException();

            var counts = new long[8];
            for (var i = 0; i < 8; i++) counts[i] = subnodes[i] != null ? subnodes[i].PointCountTree : 0;
            return ComputeLodFractions(counts);
        }

        /// <summary></summary>
        public static double[] ComputeLodFractions(PersistentRef<IPointCloudNode>[] subnodes)
        {
            if (subnodes == null) return null;
            if (subnodes.Length != 8) throw new ArgumentOutOfRangeException();

            var unpacked = new IPointCloudNode[8];
            for (var i = 0; i < 8; i++) unpacked[i] = subnodes[i]?.Value;
            return ComputeLodFractions(unpacked);
        }
        
        /// <summary></summary>
        public static int[] ComputeLodCounts(int splitLimit, double[] fractions)
        {
            if (fractions == null) return null;
            if (fractions.Length != 8) throw new ArgumentOutOfRangeException();

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

        ///// <summary></summary>
        //public static T[] Foo<T>(int[] counts, int splitLimit, T[][] xss)
        //{
        //    var i = 0;
        //    for (var ci = 0; ci < 8; ci++)
        //    {
        //        if (counts[ci] == 0) continue;
        //        var subcell = xss[ci];
        //        if (subcell == null) continue;

        //        var rs = new T[splitLimit];
        //        var jmax = subcell.Length;
        //        var dj = (jmax + 0.49) / counts[ci];
        //        var oldI = i;
        //        for (var j = 0.0; j < jmax; j += dj)
        //        {
        //            var jj = (int)j;
        //            rs[i] = (V3f)(((V3d)subcell[jj] + subcell.Center) - self.Center);
        //            i++;
        //        }
        //    }
        //}
    }

    /// <summary>
    /// </summary>
    public static class LodExtensions
    {
        /// <summary>
        /// </summary>
        public static PointSet GenerateLod(this PointSet self, ImportConfig config)
        {
            if (config.CreateOctreeLod == false) return self;

            var nodeCount = self.Octree.Value.CountNodes();
            var loddedNodesCount = 0L;
            var result = self.GenerateLod(config.Key, () =>
            {
                config.CancellationToken.ThrowIfCancellationRequested();
                var i = Interlocked.Increment(ref loddedNodesCount);
                if (config.Verbose) Console.Write($"[Lod] {i}/{nodeCount}\r");
                if (i % 100 == 0) config.ProgressCallback(i / (double)nodeCount);
            }, config.MaxDegreeOfParallelism, config.CancellationToken);

            config.ProgressCallback(1.0);

            return result;
        }

        /// <summary>
        /// </summary>
        private static PointSet GenerateLod(this PointSet self, string key, Action callback, int maxLevelOfParallelism, CancellationToken ct)
        {
            if (self.IsEmpty) return self;
#pragma warning disable CS0618 // Type or member is obsolete
            var lod = self.Root.Value.GenerateLod((int)self.SplitLimit, callback, ct);
#pragma warning restore CS0618 // Type or member is obsolete
            var result = new PointSet(self.Storage, key, lod.Id, self.SplitLimit);
            self.Storage.Add(key, result, ct);
            return result;
        }

        /// <summary>
        /// </summary>
        private static PointSetNode GenerateLod(this PointSetNode self, int octreeSplitLimit, Action callback, CancellationToken ct)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));

            ct.ThrowIfCancellationRequested();

            callback?.Invoke();

            if (self.IsLeaf) return self.WithLod();

            if (self.HasLodPositions) return self; // cell already has lod data -> done

            if (self.Subnodes == null || self.Subnodes.Length != 8) throw new InvalidOperationException();

            var subcells = self.Subnodes.Map(x => x?.Value.GenerateLod(octreeSplitLimit, callback, ct));
            var subcellsTotalCount = (long)subcells.Sum(x => x?.PointCountTree);

            var needsCs = subcells.Any(x => x != null ? (x.HasColors || x.HasLodColors) : false);
            var needsNs = subcells.Any(x => x != null ? (x.HasNormals || x.HasLodNormals) : false);
            var needsIs = subcells.Any(x => x != null ? (x.HasIntensities || x.HasLodIntensities) : false);
            var needsKs = subcells.Any(x => x != null ? (x.HasClassifications || x.HasLodClassifications) : false);

            var fractions = Lod.ComputeLodFractions(subcells);
            var counts = Lod.ComputeLodCounts(octreeSplitLimit, fractions);

            // generate LoD data ...
            var lodPs = new V3f[octreeSplitLimit];
            var lodCs = needsCs ? new C4b[octreeSplitLimit] : null;
            var lodNs = needsNs ? new V3f[octreeSplitLimit] : null;
            var lodIs = needsIs ? new int[octreeSplitLimit] : null;
            var lodKs = needsKs ? new byte[octreeSplitLimit] : null;
            var i = 0;
            for (var ci = 0; ci < 8; ci++)
            {
                if (counts[ci] == 0) continue;
                var subcell = subcells[ci];
                if (subcell == null) continue;
                
                var subps = subcell.IsLeaf ? subcell.Positions.Value : subcell.LodPositions.Value;
                var subcs = needsCs ? (subcell.IsLeaf ? subcell.Colors.Value : subcell.LodColors.Value) : null;
                var subns = needsNs ? (subcell.IsLeaf ? subcell.Normals.Value : subcell.LodNormals.Value) : null;
                var subis = needsIs ? (subcell.IsLeaf ? subcell.Intensities.Value : subcell.LodIntensities.Value) : null;
                var subks = needsKs ? (subcell.IsLeaf ? subcell.Classifications.Value : subcell.LodClassifications.Value) : null;

                var jmax = subps.Length;
                var dj = (jmax + 0.49) / counts[ci];
                var oldI = i;
                for (var j = 0.0; j < jmax; j += dj)
                {
                    var jj = (int)j;
                    lodPs[i] = (V3f)(((V3d)subps[jj] + subcell.Center) - self.Center);
                    if (needsCs) lodCs[i] = subcs[jj];
                    if (needsNs) lodNs[i] = subns[jj];
                    if (needsIs) lodIs[i] = subis[jj];
                    if (needsKs) lodKs[i] = subks[jj];
                    i++;
                }
            }
            var lodKd = lodPs.BuildKdTree();

            // store LoD data ...
            var lodPsId = Guid.NewGuid();
            self.Storage.Add(lodPsId, lodPs, ct);

            var lodKdId = Guid.NewGuid();
            self.Storage.Add(lodKdId, lodKd.Data, ct);
            
            var lodCsId = needsCs ? (Guid?)Guid.NewGuid() : null;
            if (needsCs) self.Storage.Add(lodCsId.Value, lodCs, ct);

            var lodNsId = needsNs ? (Guid?)Guid.NewGuid() : null;
            if (needsNs) self.Storage.Add(lodNsId.Value, lodNs, ct);

            var lodIsId = needsIs ? (Guid?)Guid.NewGuid() : null;
            if (needsIs) self.Storage.Add(lodIsId.Value, lodIs, ct);

            var lodKsId = needsKs ? (Guid?)Guid.NewGuid() : null;
            if (needsKs) self.Storage.Add(lodKsId.Value, lodKs, ct);

            var result = self.WithLod(lodPsId, lodCsId, lodNsId, lodIsId, lodKdId, lodKsId, subcells);
            return result;
        }
    }
}
