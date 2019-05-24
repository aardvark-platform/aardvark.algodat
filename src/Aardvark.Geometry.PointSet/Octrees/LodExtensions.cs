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
using Uncodium;

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

        /// <summary></summary>
        public static V3f[] AggregateSubPositions(int[] counts, int splitLimit, V3d center, V3d?[] subCenters, V3f[][] xss)
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

        /// <summary></summary>
        public static T[] AggregateSubArrays<T>(int[] counts, int splitLimit, T[][] xss)
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
    }

    /// <summary>
    /// </summary>
    public static class LodExtensions
    {
        /// <summary>
        /// </summary>
        public static PointSet GenerateLod(this PointSet self, ImportConfig config)
        {
            var nodeCount = self.Octree?.Value?.CountNodes() ?? 0;
            var loddedNodesCount = 0L;
            var result = self.GenerateLod(config, () =>
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
        private static PointSet GenerateLod(this PointSet self, ImportConfig config, Action callback, int maxLevelOfParallelism, CancellationToken ct)
        {
            var key = config.Key;

            if (self.IsEmpty) return self;
#pragma warning disable CS0618 // Type or member is obsolete
            var lod = self.Root.Value.GenerateLod(0, config, self.SplitLimit, callback, ct);
#pragma warning restore CS0618 // Type or member is obsolete
            var result = new PointSet(self.Storage, key, lod.Id, self.SplitLimit);
            self.Storage.Add(key, result);
            return result;
        }

        private static V3f[] EstimateNormals(this V3f[] points, PointRkdTreeD<V3f[], V3f> kdtree, int k)
        {
            return points.Map((p, i) =>
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
            });
        }

        /// <summary>
        /// </summary>
        private static PointSetNode GenerateLod(this PointSetNode self, int level, ImportConfig config, int splitLimit, Action callback, CancellationToken ct)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));

            ct.ThrowIfCancellationRequested();

            callback?.Invoke();

            if (self.IsLeaf)
            {
                if (!self.HasNormals)
                {
                    var ns = self.Positions.Value.EstimateNormals(self.KdTree.Value, 16);
                    var nsId = Guid.NewGuid();
                    self.Storage.Add(nsId, ns);
                    self = self.WithNormals(nsId);
                }

                var simple = new SimpleNode(self);
                var changed = false;

                foreach(var att in config.CellAttributes)
                {
                    var o = att.ComputeValue(simple);
                    simple = simple.WithCellAttributes(simple.CellAttributes.Add(att.Id, o));
                    changed = true;
                }

                if (changed) return simple.Persist();
                else return self;
            }

            if (self.HasPositions) return self; // cell already has data -> done

            if (self.Subnodes == null || self.Subnodes.Length != 8) throw new InvalidOperationException();

            var subcells = 
                //level < 8 ?
                //self.Subnodes.MapParallel((x,__) => x?.Value.GenerateLod(level + 1, config, splitLimit, callback, ct), Environment.ProcessorCount).ToArray() :
                self.Subnodes.Map(x => x?.Value.GenerateLod(level + 1, config, splitLimit, callback, ct));

            var subcenters = subcells.Map(x => x?.Center);
            var subcellsTotalCount = (long)subcells.Sum(x => x?.PointCountTree);

            var needsCs = subcells.Any(x => x != null ? x.HasColors : false);
            var needsNs = subcells.Any(x => x != null ? x.HasNormals : false);
            var needsIs = subcells.Any(x => x != null ? x.HasIntensities : false);
            var needsKs = subcells.Any(x => x != null ? x.HasClassifications : false);

            var fractions = Lod.ComputeLodFractions(subcells);
            var counts = Lod.ComputeLodCounts(splitLimit, fractions);

            // generate LoD data ...
            var lodPs = Lod.AggregateSubPositions(counts, splitLimit, self.Center, subcenters, subcells.Map(x => x?.GetPositions()?.Value));
            var lodCs = needsCs ? Lod.AggregateSubArrays(counts, splitLimit, subcells.Map(x => x?.GetColors()?.Value)) : null;
            var lodIs = needsIs ? Lod.AggregateSubArrays(counts, splitLimit, subcells.Map(x => x?.GetIntensities()?.Value)) : null;
            var lodKs = needsKs ? Lod.AggregateSubArrays(counts, splitLimit, subcells.Map(x => x?.GetClassifications()?.Value)) : null;
            var lodKd = lodPs.BuildKdTree();
            var lodNs = needsNs ? lodPs.EstimateNormals(lodKd, 16) : null;


            var attributes = new List<(string, string, object)>();
            // store LoD data ...
            var lodPsId = Guid.NewGuid();
            self.Storage.Add(lodPsId, lodPs);
            attributes.Add((PointCloudAttribute.Positions, lodPsId.ToString(), lodPs));

            var lodKdId = Guid.NewGuid();
            self.Storage.Add(lodKdId, lodKd.Data);
            attributes.Add((PointCloudAttribute.KdTree, lodKdId.ToString(), lodKd));

            var lodCsId = needsCs ? (Guid?)Guid.NewGuid() : null;
            if (needsCs)
            {
                self.Storage.Add(lodCsId.Value, lodCs);
                attributes.Add((PointCloudAttribute.Colors, lodCsId.Value.ToString(), lodCs));
            }

            var lodNsId = needsNs ? (Guid?)Guid.NewGuid() : null;
            if (needsNs)
            {
                self.Storage.Add(lodNsId.Value, lodNs);
                attributes.Add((PointCloudAttribute.Normals, lodNsId.Value.ToString(), lodNs));
            }


            var lodIsId = needsIs ? (Guid?)Guid.NewGuid() : null;
            if (needsIs)
            {
                self.Storage.Add(lodIsId.Value, lodIs);
                attributes.Add((PointCloudAttribute.Intensities, lodIsId.Value.ToString(), lodIs));
            }

            var lodKsId = needsKs ? (Guid?)Guid.NewGuid() : null;
            if (needsKs)
            {
                self.Storage.Add(lodKsId.Value, lodKs);
                attributes.Add((PointCloudAttribute.Classifications, lodKsId.Value.ToString(), lodKs));
            }


            SimpleNode node = new SimpleNode(self);
            foreach(var att in config.CellAttributes)
            {
                var dict = node.CellAttributes.Add(att.Id, att.ComputeValue(node));
                node = node.WithCellAttributes(dict);
            }

            var result = self.WithData(node.CellAttributes, subcellsTotalCount, lodPsId, lodCsId, lodNsId, lodIsId, lodKdId, lodKsId, subcells);
            return result;
        }

        /// <summary>
        /// </summary>
        private static PointCloudNode GenerateLod(this PointCloudNode self, ImportConfig cfg, CancellationToken ct)
        {
            var splitLimit = cfg.OctreeSplitLimit;
            if (self == null) throw new ArgumentNullException(nameof(self));

            ct.ThrowIfCancellationRequested();

            if (self.IsLeaf())
            {
                Report.Error("wrongness (no normals / etc.)");
                return self;
            }

            if (self.HasPositions()) return self; // cell already has lod data -> done

            if (self.SubNodes == null || self.SubNodes.Length != 8) throw new InvalidOperationException();

            var subcells = self.SubNodes.Map(x => x?.Value);
            var subcenters = subcells.Map(x => x?.Center);
            var subcellsTotalCount = (long)subcells.Sum(x => x?.PointCountTree);

            var needsCs = subcells.Any(x => x != null ? (x.HasColors()) : false);
            var needsNs = subcells.Any(x => x != null ? (x.HasNormals()) : false);
            var needsIs = subcells.Any(x => x != null ? (x.HasIntensities()) : false);
            var needsKs = subcells.Any(x => x != null ? (x.HasClassifications()) : false);

            var fractions = Lod.ComputeLodFractions(subcells);
            var counts = Lod.ComputeLodCounts(splitLimit, fractions);

            // generate LoD data ...
            var lodPs = Lod.AggregateSubPositions(counts, splitLimit, self.Center, subcenters, subcells.Map(x => x?.GetPositions()?.Value));
            var lodCs = needsCs ? Lod.AggregateSubArrays(counts, splitLimit, subcells.Map(x => x?.GetColors()?.Value)) : null;
            var lodIs = needsIs ? Lod.AggregateSubArrays(counts, splitLimit, subcells.Map(x => x?.GetIntensities()?.Value)) : null;
            var lodKs = needsKs ? Lod.AggregateSubArrays(counts, splitLimit, subcells.Map(x => x?.GetClassifications()?.Value)) : null;
            var lodKd = lodPs.BuildKdTree();
            var lodNs = 
                    needsNs ? 
                        (cfg.EstimateNormalsKdTree == null ?
                            (cfg.EstimateNormals == null ?
                                Lod.AggregateSubArrays(counts, splitLimit, subcells.Map(x => x?.GetNormals()?.Value)) :
                                cfg.EstimateNormals(lodPs.MapToList(p => (V3d)p + self.Center)).ToArray()
                            ) :
                            cfg.EstimateNormalsKdTree(lodKd, lodPs)
                        ) : 
                        null;

            // store LoD data ...
            var lodPsId = Guid.NewGuid().ToString();
            self.Storage.Add(lodPsId, lodPs);

            var lodKdId = Guid.NewGuid().ToString();
            self.Storage.Add(lodKdId, lodKd.Data);

            var lodCsId = needsCs ? Guid.NewGuid().ToString() : null;
            if (needsCs) self.Storage.Add(lodCsId, lodCs);

            var lodNsId = needsNs ? Guid.NewGuid().ToString() : null;
            if (needsNs) self.Storage.Add(lodNsId, lodNs);

            var lodIsId = needsIs ? Guid.NewGuid().ToString() : null;
            if (needsIs) self.Storage.Add(lodIsId, lodIs);

            var lodKsId = needsKs ? Guid.NewGuid().ToString() : null;
            if (needsKs) self.Storage.Add(lodKsId, lodKs);

            var result = self.WithData(lodPsId, lodKdId, lodCsId, lodNsId, lodIsId, lodKsId);
            return result;
        }
    }
}
