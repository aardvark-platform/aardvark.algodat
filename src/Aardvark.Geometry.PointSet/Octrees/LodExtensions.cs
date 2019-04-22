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
using System.Threading.Tasks;
using Uncodium;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class LodExtensions
    {
        /// <summary>
        /// </summary>
        public static PointSet GenerateLod(this PointSet self, ImportConfig config)
        {
            var nodeCount = self.Root.Value.CountNodes();
            var loddedNodesCount = 0L;
            var result = self.GenerateLod(config.Key, () =>
            {
                config.CancellationToken.ThrowIfCancellationRequested();
                var i = Interlocked.Increment(ref loddedNodesCount);
                if (config.Verbose) Console.Write($"[Lod] {i}/{nodeCount}\r");
                if (i % 100 == 0) config.ProgressCallback(i / (double)nodeCount);
            }, config.MaxDegreeOfParallelism, config.CancellationToken);

            config.ProgressCallback(1.0);

            return result.Result;
        }

        /// <summary>
        /// </summary>
        private static async Task<PointSet> GenerateLod(this PointSet self, string key, Action callback, int maxLevelOfParallelism, CancellationToken ct)
        {
            if (self.IsEmpty) return self;
            var lod = await self.Root.Value.GenerateLod(self.SplitLimit, callback, ct);
            var result = new PointSet(self.Storage, key, lod.Id, self.SplitLimit);
            self.Storage.Add(key, result, ct);
            return result;
        }

        private static Task<V3f[]> EstimateNormals(this V3f[] points, PointRkdTreeD<V3f[], V3f> kdtree, int k)
        {
            return Task.Run(() => points.Map((p, i) =>
            {
                if (k > points.Length) k = points.Length;

                // find k closest points
                var closest = kdtree.GetClosest(p, float.MaxValue, k);
                if (closest.Count == 0) return V3f.Zero;

                // compute centroid of k closest points
                var c = points[closest[0].Index];
                for (var j = 1; j < k; j++) c += points[closest[j].Index];
                c /= k;

                // compute covariance matrix of k closest points relative to centroid
                var cvm = M33f.Zero;
                for (var j = 0; j < k; j++) cvm.AddOuterProduct(points[closest[j].Index] - c);
                cvm /= k;

                // solve eigensystem -> eigenvector for smallest eigenvalue gives normal 
                Eigensystems.Dsyevh3((M33d)cvm, out M33d q, out V3d w);
                return (V3f)((w.X < w.Y) ? ((w.X < w.Z) ? q.C0 : q.C2) : ((w.Y < w.Z) ? q.C1 : q.C2));
            })
            );
        }

        /// <summary>
        /// </summary>
        private static async Task<PointSetNode> GenerateLod(this PointSetNode self, long octreeSplitLimit, Action callback, CancellationToken ct)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));

            ct.ThrowIfCancellationRequested();

            if (self.IsLeaf)
            {
                if (!self.HasNormals)
                {
                    var ns = self.Positions.Value.EstimateNormals(self.KdTree.Value, 16);
                    var nsId = Guid.NewGuid();
                    self.Storage.Add(nsId, await ns, ct);
                    self = self.WithNormals(nsId);
                }
                callback?.Invoke();
                return self.WithLod();
            }

            if (self.Subnodes == null || self.Subnodes.Length != 8) throw new InvalidOperationException();

            var subcellsAsync = self.Subnodes.Map(x => x?.Value.GenerateLod(octreeSplitLimit, callback, ct));
            await Task.WhenAll(subcellsAsync.Where(x => x != null));
            var subcells = subcellsAsync.Map(x => x?.Result);
            var subcellsTotalCount = (long)subcells.Sum(x => x?.PointCountTree);

            var needsCs = subcells.Any(x => x != null ? (x.HasColors || x.HasLodColors) : false);
            var needsNs = subcells.Any(x => x != null ? (x.HasNormals || x.HasLodNormals) : false);
            var needsIs = subcells.Any(x => x != null ? (x.HasIntensities || x.HasLodIntensities) : false);

            var fractions = new double[8].SetByIndex(
                ci => subcells[ci] != null ? (subcells[ci].PointCountTree / (double)subcellsTotalCount) : 0.0
                );
            var remainder = 0.1;
            var counts = fractions.Map(x =>
            {
                var fn = octreeSplitLimit * x + remainder;
                var n = (int)fn;
                remainder = fn - n;
                return n;
            });
            var e = octreeSplitLimit - counts.Sum();
            if (e != 0) throw new InvalidOperationException();

            // generate LoD data ...
            var lodPs = new V3f[octreeSplitLimit];
            var lodCs = needsCs ? new C4b[octreeSplitLimit] : null;
            var lodNs = needsNs ? new V3f[octreeSplitLimit] : null;
            var lodIs = needsIs ? new int[octreeSplitLimit] : null;
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

            var result = self.WithLod(lodPsId, lodCsId, lodNsId, lodIsId, lodKdId, subcells);
            callback?.Invoke();
            return result;
        }
    }
}
