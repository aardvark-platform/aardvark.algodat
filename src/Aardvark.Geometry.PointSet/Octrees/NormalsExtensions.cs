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
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class NormalsExtensions
    {
        /// <summary>
        /// </summary>
        public static PointCloudNode RegenerateNormals(this PointCloudNode self,
            Func<IList<V3d>, IList<V3f>> estimateNormals,
            Action<double> callback, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            callback?.Invoke(1.0);
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns new PointSet with regenerated normals using given estimateNormals function.
        /// </summary>
        public static PointSet RegenerateNormals(this PointSet self,
            Func<IList<V3d>, IList<V3f>> estimateNormals,
            Action callback, CancellationToken ct
            )
        {
            if (self.IsEmpty) return self;
#pragma warning disable CS0618 // Type or member is obsolete
            var lod = self.Root.Value.RegenerateNormals(estimateNormals, callback, ct);
#pragma warning restore CS0618 // Type or member is obsolete
            var key = Guid.NewGuid().ToString();
            var result = new PointSet(self.Storage, key, lod.Id, self.SplitLimit);
            self.Storage.Add(key, result, ct);
            return result;
        }

        /// <summary>
        /// Returns new PointSet with regenerated normals using given estimateNormals function.
        /// </summary>
        public static PointSet RegenerateNormals(this PointSet self,
            Func<IList<V3f>, PointRkdTreeD<V3f[], V3f>, IList<V3f>> estimateNormals,
            Action callback, CancellationToken ct
            )
        {
            if (self.IsEmpty) return self;
#pragma warning disable CS0618 // Type or member is obsolete
            var lod = self.Root.Value.RegenerateNormals(estimateNormals, callback, ct);
#pragma warning restore CS0618 // Type or member is obsolete
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

                // create node with new normals
                var r = self.WithNormals(nsId);

                return r;
            }

            if (self.Subnodes == null || self.Subnodes.Length != 8) throw new InvalidOperationException();
            
            var subcells = self.Subnodes.Map(x => x?.Value.RegenerateNormals(estimateNormals, callback, ct));
            var subcellsTotalCount = (long)subcells.Sum(x => x?.PointCountTree);
            var octreeSplitLimit = self.Positions.Value.Length;
            
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

            // generate Normals ...
            var lodNs = new V3f[octreeSplitLimit];
            var i = 0;
            for (var ci = 0; ci < 8; ci++)
            {
                if (counts[ci] == 0) continue;
                var subcell = subcells[ci];
                if (subcell == null) continue;
                
                var subns = subcell.Normals.Value;

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
            
            var result = self.WithSubNodes(subcells);
            return result;
        }

        /// <summary>
        /// Returns new tree with regenerated normals using given estimateNormals function.
        /// </summary>
        private static PointSetNode RegenerateNormals(this PointSetNode self,
            Func<IList<V3f>, PointRkdTreeD<V3f[], V3f>, IList<V3f>> estimateNormals,
            Action callback, CancellationToken ct
            )
        {
            if (self == null) throw new ArgumentNullException(nameof(self));

            ct.ThrowIfCancellationRequested();

            callback?.Invoke();

            if (self.IsLeaf)
            {
                // generate and store normals
                var ns = estimateNormals(self.Positions.Value, self.KdTree.Value).ToArray();
                var nsId = Guid.NewGuid();
                self.Storage.Add(nsId, ns, ct);

                // create node with new normals and LoD normals
                var r = self.WithNormals(nsId);

                return r;
            }

            if (self.Subnodes == null || self.Subnodes.Length != 8) throw new InvalidOperationException();

            var subcells = self.Subnodes.Map(x => x?.Value.RegenerateNormals(estimateNormals, callback, ct));
            var subcellsTotalCount = (long)subcells.Sum(x => x?.PointCountTree);
            var octreeSplitLimit = self.Positions.Value.Length;

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

                var subns = subcell.Normals.Value;

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

            var result = self.WithSubNodes(subcells);
            return result;
        }
    }
}
