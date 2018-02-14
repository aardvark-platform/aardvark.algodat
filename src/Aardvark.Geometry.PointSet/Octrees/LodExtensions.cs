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
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Aardvark.Base;

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
            }, config.MaxDegreeOfParallelism, config.CancellationToken);
            
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
            if (self != null) callback?.Invoke();
            if (self.IsLeaf) return self.WithLod();

            if (self.Subnodes == null || self.Subnodes.Length != 8) throw new InvalidOperationException();

            ct.ThrowIfCancellationRequested();

            var subcells = self.Subnodes.Map(x => x?.Value.GenerateLod(octreeSplitLimit, callback, ct));
            var subcellsTotalCount = (long)subcells.Sum(x => x?.PointCountTree);
            
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

            // there are forced splits below the split limit now, so this no longer holds ... 
            //if (counts.Sum() != octreeSplitLimit) throw new InvalidOperationException($"{counts.Sum()} != {octreeSplitLimit}");

            // generate LoD data ...
            var lodPs = new V3f[octreeSplitLimit];
            var lodCs = new C4b[octreeSplitLimit];
            var i = 0;
            for (var ci = 0; ci < 8; ci++)
            {
                if (counts[ci] == 0) continue;
                var subcell = subcells[ci];
                if (subcell == null) continue;
                
                var subps = subcell.IsLeaf ? subcell.Positions.Value : subcell.LodPositions.Value;
                var subcs = subcell.IsLeaf ? subcell.Colors?.Value : subcell.LodColors?.Value;

                var jmax = subps.Length;
                var dj = (jmax + 0.49) / counts[ci];
                var oldI = i;
                for (var j = 0.0; j < jmax; j += dj)
                {
                    var jj = (int)j;
                    lodPs[i] = (V3f)(((V3d)subps[jj] + subcell.Center) - self.Center);
                    if (subcs != null) lodCs[i] = subcs[jj];
                    i++;
                }
                //Report.Line($"{i - oldI} from {counts[ci]} (dj = {dj}, subps.Length = {subps.Length})");
            }
            var lodKd = lodPs.BuildKdTree();

            // store LoD data ...
            var lodPsId = Guid.NewGuid();
            self.Storage.Add(lodPsId.ToString(), lodPs, ct);

            var lodKdId = Guid.NewGuid();
            self.Storage.Add(lodKdId.ToString(), lodKd.Data, ct);

            var lodCsId = lodCs != null ? (Guid?)Guid.NewGuid() : null;
            if (lodCs != null) self.Storage.Add(lodCsId.ToString(), lodCs, ct);

            var result = self.WithLod(lodPsId, lodCsId, lodKdId, subcells);
            return result;
        }
    }
}
