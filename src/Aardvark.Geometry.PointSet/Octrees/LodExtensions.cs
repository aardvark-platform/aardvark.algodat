/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class LodExtensions
    {
        #region RegenerateNormals

        /// <summary>
        /// Returns new PointSet with regenerated normals using given estimateNormals function.
        /// </summary>
        public static PointSet RegenerateNormals(this PointSet self,
            Func<IList<V3d>, IList<V3f>> estimateNormals,
            Action callback, CancellationToken ct
            )
        {
            if (self.IsEmpty) return self;
            var lod = self.Root.Value.RegenerateNormals(estimateNormals, callback, ct);
            var key = Guid.NewGuid().ToString();
            var result = new PointSet(self.Storage, key, lod.Id, self.SplitLimit);
            self.Storage.Add(key, result, ct);
            return result;
        }

        /// <summary>
        /// Returns new tree with regenerated normals using given estimateNormals function.
        /// </summary>
        private static PointSetNode RegenerateNormals(this PointSetNode self,
            Func<IList<V3d>, IList<V3f>> estimateNormals,
            Action callback, CancellationToken ct
            )
        {
            if (self == null) throw new ArgumentNullException(nameof(self));

            ct.ThrowIfCancellationRequested();

            callback?.Invoke();

            if (self.IsLeaf)
            {
                // generate and store normals
                var ns = estimateNormals(self.PositionsAbsolute).ToArray();
                var nsId = Guid.NewGuid();
                self.Storage.Add(nsId, ns, ct);

                // create node with new normals and LoD normals
                var r = self.WithNormals(nsId).WithLod();

                return r;
            }

            if (self.Subnodes == null || self.Subnodes.Length != 8) throw new InvalidOperationException();
            
            var subcells = self.Subnodes.Map(x => x?.Value.RegenerateNormals(estimateNormals, callback, ct));
            var subcellsTotalCount = (long)subcells.Sum(x => x?.PointCountTree);
            var octreeSplitLimit = self.LodPositions.Value.Length;
            
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

            // generate LodNormals ...
            var lodNs = new V3f[octreeSplitLimit];
            var i = 0;
            for (var ci = 0; ci < 8; ci++)
            {
                if (counts[ci] == 0) continue;
                var subcell = subcells[ci];
                if (subcell == null) continue;
                
                var subns = subcell.LodNormals.Value;

                var jmax = subns.Length;
                var dj = (jmax + 0.49) / counts[ci];
                var oldI = i;
                for (var j = 0.0; j < jmax; j += dj)
                {
                    var jj = (int)j;
                    lodNs[i] = subns[jj];
                    i++;
                }
            }

            // store LoD data ...
            var lodNsId = Guid.NewGuid();
            self.Storage.Add(lodNsId, lodNs, ct);
            
            var result = self.WithLod(self.LodPositionsId, self.LodColorsId, lodNsId, self.LodIntensitiesId, self.LodKdTreeId, subcells);
            return result;
        }

        #endregion

        #region GenerateLod

        /// <summary>
        /// </summary>
        public static PointSet GenerateLod(this PointSet self, ImportConfig config)
        {
            if (config.CreateOctreeLod == false) return self;

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

            return result;
        }

        /// <summary>
        /// </summary>
        private static PointSet GenerateLod(this PointSet self, string key, Action callback, int maxLevelOfParallelism, CancellationToken ct)
        {
            if (self.IsEmpty) return self;
            var lod = self.Root.Value.GenerateLod(self.SplitLimit, callback, ct);
            var result = new PointSet(self.Storage, key, lod.Id, self.SplitLimit);
            self.Storage.Add(key, result, ct);
            return result;
        }

        /// <summary>
        /// </summary>
        private static PointSetNode GenerateLod(this PointSetNode self, long octreeSplitLimit, Action callback, CancellationToken ct)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));

            ct.ThrowIfCancellationRequested();

            callback?.Invoke();

            if (self.IsLeaf) return self.WithLod();

            if (self.Subnodes == null || self.Subnodes.Length != 8) throw new InvalidOperationException();

            var subcells = self.Subnodes.Map(x => x?.Value.GenerateLod(octreeSplitLimit, callback, ct));
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
            return result;
        }

        #endregion
    }
}
