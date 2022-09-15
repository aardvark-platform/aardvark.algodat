/*
    Copyright (C) 2006-2022. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Aardvark.Geometry.Points
{
    public class InlineConfig
    {
        /// <summary>
        /// Collapse child nodes making each node appr. 8 times as big.
        /// E.g. for an octree with split limit 8192, this would result in an octree with split limit 65536.
        /// </summary>
        public bool Collapse { get; }

        /// <summary>
        /// GZip inlined node.
        /// </summary>
        public bool GZipped { get; }

        /// <summary>
        /// Optionally round positions to given number of digits.
        /// </summary>
        public int? PositionsRoundedToNumberOfDigits { get; }

        /// <summary>
        /// Progress callback [0,1].
        /// </summary>
        public Action<double>? Progress { get; }

        /// <summary></summary>
        /// <param name="collapse">Collapse child nodes making each node appr. 8 times as big.</param>
        /// <param name="gzipped">GZip inlined node.</param>
        /// <param name="positionsRoundedToNumberOfDigits">Optionally round positions to given number of digits.</param>
        /// <param name="progress">Progress callback [0,1].</param>
        public InlineConfig(bool collapse, bool gzipped, int? positionsRoundedToNumberOfDigits, Action<double>? progress)
        {
            Collapse = collapse;
            GZipped = gzipped;
            PositionsRoundedToNumberOfDigits = positionsRoundedToNumberOfDigits;
            Progress = progress;
        }

        /// <summary></summary>
        /// <param name="collapse">Collapse child nodes making each node appr. 8 times as big.</param>
        /// <param name="gzipped">GZip inlined node.</param>
        /// <param name="progress">Progress callback [0,1].</param>
        public InlineConfig(bool collapse, bool gzipped, Action<double>? progress) : this(collapse, gzipped, null, progress) { }

        /// <summary></summary>
        /// <param name="collapse">Collapse child nodes making each node appr. 8 times as big.</param>
        /// <param name="gzipped">GZip inlined node.</param>
        public InlineConfig(bool collapse, bool gzipped) : this(collapse, gzipped, null, null) { }
    }

    /// <summary>
    /// Compact representation of an octree node, without references to external data (except subnodes).
    /// </summary>
    public class InlinedNode
    {
        public Guid NodeId { get; }
        public Cell Cell { get; }
        public Box3d BoundingBoxExactGlobal { get; }
        public Guid[]? SubnodesGuids { get; }
        public int PointCountCell { get; }
        public long PointCountTreeLeafs { get; }
        public V3f[] PositionsLocal3f { get; }
        public C3b[]? Colors3b { get; }
        public byte[]? Classifications1b { get; }
        public byte[]? Intensities1b { get; }

        public InlinedNode(
            Guid nodeId, Cell cell, Box3d boundingBoxExactGlobal,
            Guid[]? subnodesGuids,
            int pointCountCell, long pointCountTreeLeafs,
            V3f[] positionsLocal3f, C3b[]? colors3b, byte[]? classifications1b, byte[]? intensities1b
            )
        {
            NodeId = nodeId;
            Cell = cell;
            BoundingBoxExactGlobal = boundingBoxExactGlobal;
            SubnodesGuids = subnodesGuids;
            PointCountCell = pointCountCell;
            PointCountTreeLeafs = pointCountTreeLeafs;
            PositionsLocal3f = positionsLocal3f;
            Colors3b = colors3b;
            Classifications1b = classifications1b;
            Intensities1b = intensities1b;
        }

        public InlinedNode(byte[] buffer, bool gzipped)
        {
            if (gzipped) buffer = buffer.UnGZip();
            var map = buffer.DurableDecode<IReadOnlyDictionary<Durable.Def, object>>();

            NodeId = (Guid)map[Durable.Octree.NodeId];
            Cell = (Cell)map[Durable.Octree.Cell];
            BoundingBoxExactGlobal = (Box3d)map[Durable.Octree.BoundingBoxExactGlobal];
            SubnodesGuids = map.TryGetValue(Durable.Octree.SubnodesGuids, out var gs) ? (Guid[]?)gs : null;
            PointCountCell = (int)map[Durable.Octree.PointCountCell];
            PointCountTreeLeafs = (long)map[Durable.Octree.PointCountTreeLeafs];
            PositionsLocal3f = (V3f[])map[Durable.Octree.PositionsLocal3f];
            Colors3b = map.TryGetValue(Durable.Octree.Colors3b, out var cs) ? (C3b[]?)cs : null;
            Classifications1b = map.TryGetValue(Durable.Octree.Classifications1b, out var ks) ? (byte[]?)ks : null;
            Intensities1b = map.TryGetValue(Durable.Octree.Intensities1b, out var js) ? (byte[]?)js : null;
        }

        // DO NOT REMOVE -> backwards compatibility
        [Obsolete("Use other constructor instead.")]
        public InlinedNode(
            Guid nodeId, Cell cell, Box3d boundingBoxExactGlobal,
            Guid[]? subnodesGuids,
            int pointCountCell, long pointCountTreeLeafs, 
            V3f[] positionsLocal3f, C3b[]? colors3b
            )
            : this(nodeId, cell, boundingBoxExactGlobal, subnodesGuids, pointCountCell, pointCountTreeLeafs, positionsLocal3f, colors3b, classifications1b: null, intensities1b: null)
        { }

        public V3d[] PositionsGlobal3d
        {
            get
            {
                var c = Cell.GetCenter();
                return PositionsLocal3f.Map(p => c + (V3d)p);
            }
        }

        public List<KeyValuePair<Durable.Def, object>> ToDurableMap()
        {
            var result = new List<KeyValuePair<Durable.Def, object>>();

            void AddResultEntry(Durable.Def def, object o) => result.Add(new KeyValuePair<Durable.Def, object>(def, o));

            AddResultEntry(Durable.Octree.NodeId, NodeId);
            AddResultEntry(Durable.Octree.Cell, Cell);
            AddResultEntry(Durable.Octree.BoundingBoxExactGlobal, BoundingBoxExactGlobal);

            if (SubnodesGuids != null)
            {
                AddResultEntry(Durable.Octree.SubnodesGuids, SubnodesGuids);
            }

            AddResultEntry(Durable.Octree.PointCountCell, PointCountCell);
            AddResultEntry(Durable.Octree.PointCountTreeLeafs, PointCountTreeLeafs);
            AddResultEntry(Durable.Octree.PointCountTreeLeafsFloat64, (double)PointCountTreeLeafs);
            AddResultEntry(Durable.Octree.PositionsLocal3f, PositionsLocal3f);

            //if (node.TryGetValue(Durable.Octree.Normals3fReference, out var nsRef))
            //{
            //    var ns = storage.GetV3fArray(((Guid)nsRef).ToString());
            //    yield return Entry(Durable.Octree.Normals3f, ns);
            //}

            if (Colors3b != null)
            {
                AddResultEntry(Durable.Octree.Colors3b, Colors3b);
            }

            if (Classifications1b != null)
            {
                AddResultEntry(Durable.Octree.Classifications1b, Classifications1b);
            }

            if (Intensities1b != null)
            {
                AddResultEntry(Durable.Octree.Intensities1b, Intensities1b);
            }

            return result;
        }

        /// <summary>
        /// Binary encodes (and optionally gzips) this InlinedNode as a Durable.Octree.Node.
        /// </summary>
        public byte[] Encode(bool gzip) => this.ToDurableMap().DurableEncode(Durable.Octree.Node, gzip);
    }

    /// <summary>
    /// Set of inlined nodes and related metadata.
    /// </summary>
    public class InlinedNodes
    {
        public InlineConfig Config { get; }
        public InlinedNode Root { get; }
        public IEnumerable<InlinedNode> Nodes { get; }
        public long TotalNodeCount { get; }

        public Box3d BoundingBoxExactGlobal => Root.BoundingBoxExactGlobal;
        public Cell Cell => Root.Cell;
        public long PointCountTreeLeafs => Root.PointCountTreeLeafs;
        public Guid RootId => Root.NodeId;
        public V3d Centroid { get; }
        public double CentroidStdDev { get; }

        public InlinedNodes(InlineConfig config, InlinedNode root, IEnumerable<InlinedNode> nodes, long totalNodeCount)
        {
            Config = config;
            Root = root;
            Nodes = nodes;
            TotalNodeCount = totalNodeCount;

            var center = root.Cell.GetCenter();
            var centroid = root.PositionsLocal3f.ComputeCentroid();
            Centroid = (V3d)centroid + center;
            CentroidStdDev = (root.PositionsLocal3f.Sum(p => (p - centroid).LengthSquared) / root.PositionsLocal3f.Length).Sqrt();
        }
    }

    /// <summary>
    /// </summary>
    public static class InlineExtensions
    {
        /// <summary>
        /// Enumerate inlined (self-contained, no external data is referenced) octree nodes.
        /// </summary>
        public static InlinedNodes EnumerateOctreeInlined(
            this Storage storage, string key, InlineConfig config
            )
        {
            if (storage.TryGetOctree(key, out var root))
            {
                return root.EnumerateOctreeInlined(config);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Key {key} not found in store. Invariant bca69ffa-8a8e-430d-a588-9f2fbbf1c43d."
                    );
            }
        }

        /// <summary>
        /// Enumerate inlined (self-contained, no external data is referenced) octree nodes.
        /// </summary>
        public static InlinedNodes EnumerateOctreeInlined(
            this Storage storage, Guid key, InlineConfig config
            )
            => EnumerateOctreeInlined(storage, key.ToString(), config);


        /// <summary>
        /// Enumerate inlined (self-contained, no external data is referenced) octree nodes.
        /// </summary>
        public static InlinedNodes EnumerateOctreeInlined(
            this PointSet pointset, InlineConfig config
            )
            => pointset.Root.Value.EnumerateOctreeInlined(config);

        /// <summary>
        /// Enumerate inlined (self-contained, no external data is referenced) octree nodes.
        /// </summary>
        public static InlinedNodes EnumerateOctreeInlined(
            this IPointCloudNode root, InlineConfig config
            )
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            var inlinedRoot = root.ConvertToInline(config, new HashSet<Guid> { root.Id });

            return new InlinedNodes(
                config, 
                inlinedRoot,
                EnumerateRec(root, config),
                -1
                );

            static IEnumerable<InlinedNode> EnumerateRec(IPointCloudNode root, InlineConfig config)
            {
                var survive = new HashSet<Guid> { root.Id };
                foreach (var x in EnumerateRecImpl(root, survive, config, 0L)) yield return x;
            }

            static IEnumerable<InlinedNode> EnumerateRecImpl(IPointCloudNode node, HashSet<Guid> survive, InlineConfig config, long processedNodeCount)
            {
                var isLeafNode = node.IsLeaf;

                config.Progress?.Invoke(++processedNodeCount);

                if (config.Collapse && isLeafNode && !survive.Contains(node.Id)) yield break;

                var inline = node.ConvertToInline(config, survive);
                survive.Remove(node.Id);
                yield return inline;

                if (node.Subnodes != null)
                {
                    foreach (var x in node.Subnodes)
                    {
                        if (x != null && x.TryGetValue(out var subnode))
                        {
                            foreach (var n in EnumerateRecImpl(subnode, survive, config, processedNodeCount)) yield return n;
                        }
                    }
                }
            }
        }

        #region ExportInlinedPointCloud

        /// <summary>
        /// Inlines and exports point cloud to another store.
        /// </summary>
        public static void ExportInlinedPointCloud(this Storage sourceStore, string key, Storage targetStore, InlineConfig config)
        {
            if (sourceStore.TryGetOctree(key, out var root))
            {
                ExportInlinedPointCloud(root, targetStore, config);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Key {key} not found in store. Invariant 0f7a7e31-f6c7-494b-a734-18c00dee3383."
                    );
            }
        }

        /// <summary>
        /// Inlines and exports pointset to another store.
        /// </summary>
        public static void ExportInlinedPointCloud(this Storage sourceStore, Guid key, Storage targetStore, InlineConfig config)
            => ExportInlinedPointCloud(sourceStore, key.ToString(), targetStore, config);

        /// <summary>
        /// Inlines and exports pointset to another store.
        /// </summary>
        public static void ExportInlinedPointCloud(this IPointCloudNode root, Storage targetStore, InlineConfig config)
        {
            Report.BeginTimed("inlining octree");

            var totalNodeCount = root.CountNodes(outOfCore: true);
            var newSplitLimit = config.Collapse ? root.PointCountCell * 8 : root.PointCountCell;
            Report.Line($"root              = {root.Id}");
            Report.Line($"split limit       = {root.PointCountCell,36:N0}");
            Report.Line($"split limit (new) = {newSplitLimit,36:N0}");
            Report.Line($"total node count  = {totalNodeCount,36:N0}");

            // export octree
            var exported = root.EnumerateOctreeInlined(config);
            foreach (var x in exported.Nodes)
            {
                var inlined = x.ToDurableMap();
                targetStore.Add(x.NodeId, Durable.Octree.Node, inlined, config.GZipped);
            }

            Report.EndTimed();
        }

        #endregion

        #region Helpers

        private static InlinedNode ConvertToInline(
            this IPointCloudNode node,
            InlineConfig config,
            HashSet<Guid> survive
            )
        {
            var id = node.Id;
            var cell = node.Cell;
            var cellCenter = cell.GetCenter();
            var bbExactGlobal = node.BoundingBoxExactGlobal;
            var pointCountCell = node.PointCountCell;
            var pointCountTree = node.PointCountTree;
            var hasColors = node.HasColors;
            var hasClassifications = node.HasClassifications;
            var hasIntensities = node.HasIntensities;
            var subnodes = node.Subnodes?.Map(x => x?.TryGetValue());
            var isNotLeaf = !node.IsLeaf;

            var ps = default(V3f[]);
            var cs = default(C4b[]);
            var ks = default(byte[]);
            var js = default(byte[]);

            static byte[] rescaleIntensities(int[] js32)
            {
                if (js32.Length == 0) return Array.Empty<byte>();

                var min = js32.Min();
                var max = js32.Max();

                if (min >= 0 && max <= 255)
                {
                    return js32.Map(x => (byte)x);
                }
                else if (min == max)
                {
                    return js32.Map(_ => (byte)255);
                }
                else
                {
                    var f = 255.999 / (max - min);
                    return js32.Map(x => (byte)((x - min) * f));
                }
            }

            Guid[]? subnodeGuids = null;
            if (config.Collapse && isNotLeaf)
            {
                if (subnodes == null) throw new Exception("Assertion failed. Error 42565d4a-2e91-4961-a310-095b503fe6f1.");
                var nonEmptySubNodes = subnodes.Where(x => x.HasValue && x.Value.hasValue).Select(x => x!.Value.value).ToArray();

                ps = nonEmptySubNodes
                    .SelectMany(n =>
                    {
                        var nCell = n.Cell;
                        var nCenter = nCell.GetCenter();
                        var delta = nCenter - cellCenter;
                        var xs = n.Positions.Value.Map(x => (V3f)((V3d)x + delta));
                        return xs;
                    })
                    .ToArray();

                if (hasColors)
                {
                    cs = nonEmptySubNodes
                        .SelectMany(n => n.Colors.Value)
                        .ToArray();
                }

                if (hasClassifications)
                {
                    ks = nonEmptySubNodes
                        .SelectMany(n => n.Classifications.Value)
                        .ToArray();
                }

                if (hasIntensities)
                {
                    var js32 = nonEmptySubNodes
                        .SelectMany(n => n.Intensities.Value)
                        .ToArray();

                    js = rescaleIntensities(js32);
                }

                var guids2 = subnodes
                    .Map(nref =>
                    {
                        if (nref.HasValue && nref.Value.hasValue)
                        {
                            var n = nref.Value.value;
                            return !n.IsLeaf ? n.Id : Guid.Empty;
                        }
                        else
                        {
                            return Guid.Empty;
                        }
                    });

                var isNewLeaf = guids2.All(k => k == Guid.Empty);
                if (!isNewLeaf)
                {
                    subnodeGuids = subnodes.Map(x => x.HasValue && x.Value.hasValue ? x.Value.value.Id : Guid.Empty);
                    foreach (var g in nonEmptySubNodes) survive.Add(g.Id);
                }
            }
            else
            {
                if (isNotLeaf)
                {
                    subnodeGuids = subnodes.Map(x => x.HasValue && x.Value.hasValue ? x.Value.value.Id : Guid.Empty);
                }

                ps = node.Positions.Value;
                if (hasColors) cs = node.Colors.Value;
                if (hasClassifications) ks = node.Classifications.Value;
                if (hasIntensities) js = rescaleIntensities(node.Intensities.Value);
                if (isNotLeaf) subnodeGuids = subnodes.Map(x => x.HasValue && x.Value.hasValue ? x.Value.value.Id : Guid.Empty);
            }



            // fix color array if it has inconsistent length
            // (might have been created by an old Aardvark.Geometry.PointSet version)
            if (hasColors && cs!.Length != ps.Length)
            {
                Report.ErrorNoPrefix($"[ConvertToInline] inconsistent length: {ps.Length} positions, but {cs.Length} colors.");

                var csFixed = new C4b[ps.Length];
                if (csFixed.Length > 0)
                {
                    var lastColor = cs[cs.Length - 1];
                    var imax = Math.Min(ps.Length, cs.Length);
                    for (var i = 0; i < imax; i++) csFixed[i] = cs[i];
                    for (var i = imax; i < ps.Length; i++) csFixed[i] = lastColor;
                }
                cs = csFixed;
            }

            // optionally round positions
            if (config.PositionsRoundedToNumberOfDigits.HasValue)
            {
                ps = ps.Map(x => x.Round(config.PositionsRoundedToNumberOfDigits.Value));
            }

            // result
            pointCountCell = ps.Length;
            var cs3b = cs?.Map(x => new C3b(x));
            var result = new InlinedNode(id, cell, bbExactGlobal, subnodeGuids, pointCountCell, pointCountTree, ps, cs3b, ks, js);
            return result;
        }

        #endregion
    }
}
