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
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Points
{
    public class InlineConfig
    {
        /// <summary>
        /// Collapse child nodes making each node 8 times as big.
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

        public Action<double> Progress { get; }

        public InlineConfig(bool collapse, bool gzipped, int? positionsRoundedToNumberOfDigits, Action<double> progress)
        {
            Collapse = collapse;
            GZipped = gzipped;
            PositionsRoundedToNumberOfDigits = positionsRoundedToNumberOfDigits;
            Progress = progress;
        }

        public InlineConfig(bool collapse, bool gzipped, Action<double> progress) : this(collapse, gzipped, null, progress) { }

        public InlineConfig(bool collapse, bool gzipped) : this(collapse, gzipped, null, null) { }
    }

    public class InlinedNode
    {
        public Guid NodeId { get; }
        public Cell Cell { get; }
        public Box3d BoundingBoxExactGlobal { get; }
        public Guid[] SubnodesGuids { get; }
        public int PointCountCell { get; }
        public long PointCountTreeLeafs { get; }
        public V3f[] PositionsLocal3f { get; }
        public C3b[] Colors3b { get; }

        public InlinedNode(
            Guid nodeId, Cell cell, Box3d boundingBoxExactGlobal, 
            Guid[] subnodesGuids, 
            int pointCountCell, long pointCountTreeLeafs,
            V3f[] positionsLocal3f, C3b[] colors3b
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
        }

        public InlinedNode WithSubnodesGuids(Guid[] newSubnodesGuids) => new InlinedNode(
            NodeId, Cell, BoundingBoxExactGlobal, newSubnodesGuids, PointCountCell, PointCountTreeLeafs, PositionsLocal3f, Colors3b
            );

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

            return result;
        }

        /// <summary>
        /// Binary encodes (and optionally gzips) this InlinedNode as Durable.Octree.Node.
        /// </summary>
        public byte[] Encode(bool gzip) => this.ToDurableMap().Encode(Durable.Octree.Node, gzip);
    }

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
            var centroid = root.PositionsLocal3f.Average();
            Centroid = (V3d)centroid + center;
            CentroidStdDev = (root.PositionsLocal3f.Sum(p => (p - centroid).LengthSquared) / root.PositionsLocal3f.Length).Sqrt();
        }
    }

    /// <summary>
    /// </summary>
    public static class InlineExtensions
    {
        /// <summary>
        /// Enumerate inlined octree nodes.
        /// </summary>
        public static InlinedNodes EnumerateOctreeInlined(
            this Storage storage, string key, InlineConfig config
            )
        {
            if (storage.TryGetOctree(key, out var root))
            {
                return EnumerateOctreeInlined(storage, root, config);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Key {key} not found in store. Invariant bca69ffa-8a8e-430d-a588-9f2fbbf1c43d."
                    );
            }
        }

        /// <summary>
        /// Enumerate inlined octree nodes.
        /// </summary>
        public static InlinedNodes EnumerateOctreeInlined(
            this Storage storage, Guid key, InlineConfig config
            )
            => EnumerateOctreeInlined(storage, key.ToString(), config);

        /// <summary>
        /// Enumerate inlined octree nodes.
        /// </summary>
        public static InlinedNodes EnumerateOctreeInlined(
            this Storage storage, IPointCloudNode root, InlineConfig config
            )
        {
            var processedNodeCount = 0L;
            var totalNodeCount = root.CountNodes(outOfCore: true); 
            var totalNodeCountD = (double)totalNodeCount;
            var survive = new HashSet<Guid> { root.Id };
            var nodes = EnumerateRec(root.Id);

            var r = nodes.First();
            return new InlinedNodes(
                config, r, nodes, totalNodeCount
                ); ;

            IEnumerable<InlinedNode> EnumerateRec(Guid key)
            {
                var node = storage.GetNodeDataFromKey(key);
                var isLeafNode = !node.TryGetValue(Durable.Octree.SubnodesGuids, out var subnodeGuids);

                config.Progress?.Invoke(++processedNodeCount / totalNodeCountD);

                if (config.Collapse && isLeafNode && !survive.Contains(key)) yield break;

                var inline = storage.ConvertToInline(node, config, survive);
                survive.Remove(key);
                yield return inline;

                if (subnodeGuids != null)
                {
                    foreach (var g in (Guid[])subnodeGuids)
                    {
                        if (g == Guid.Empty) continue;
                        foreach (var n in EnumerateRec(g)) yield return n;
                    }
                }
            }
        }

        /// <summary>
        /// Inlines and exports pointset to another store.
        /// </summary>
        public static void InlineOctree(this Storage storage, string key, Storage exportStore, InlineConfig config)
        {
            if (storage.TryGetOctree(key, out var root))
            {
                InlineOctree(storage, root, exportStore, config);
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
        public static void InlineOctree(this Storage storage, Guid key, Storage exportStore, InlineConfig config)
            => InlineOctree(storage, key.ToString(), exportStore, config);

        /// <summary>
        /// Inlines and exports pointset to another store.
        /// </summary>
        public static void InlineOctree(
            this Storage storageSource, 
            IPointCloudNode root, 
            Storage storageTarget, 
            InlineConfig config
            )
        {
            Report.BeginTimed("inlining octree");

            var totalNodeCount = root.CountNodes(outOfCore: true);
            var newSplitLimit = root.PointCountCell * 8;
            Report.Line($"root              = {root.Id}");
            Report.Line($"split limit       = {root.PointCountCell,36:N0}");
            Report.Line($"split limit (new) = {newSplitLimit,36:N0}");
            Report.Line($"total node count  = {totalNodeCount,36:N0}");

            // export octree
            var exported = EnumerateOctreeInlined(storageSource, root.Id, config);
            foreach (var x in exported.Nodes)
            {
                var inlined = x.ToDurableMap();
                storageTarget.Add(x.NodeId, Durable.Octree.Node, inlined, config.GZipped);
            }

            Report.EndTimed();
        }



        private static InlinedNode ConvertToInline(
            this Storage storage,
            IReadOnlyDictionary<Durable.Def, object> node,
            InlineConfig config,
            HashSet<Guid> survive
            )
        {
            var id = (Guid)node[Durable.Octree.NodeId];
            var cell = (Cell)node[Durable.Octree.Cell];
            var cellCenter = cell.GetCenter();
            var bbExactGlobal = (Box3d)node[Durable.Octree.BoundingBoxExactGlobal];
            var pointCountCell = (int)node[Durable.Octree.PointCountCell];
            var pointCountTree = (long)node[Durable.Octree.PointCountTreeLeafs];
            node.TryGetValue(Durable.Octree.SubnodesGuids, out var subnodeGuids);
            var hasColors = node.ContainsKey(Durable.Octree.Colors4bReference);

            var ps = default(V3f[]);
            var cs = default(C4b[]);

            if (config.Collapse && subnodeGuids != null)
            {
                var guids = ((Guid[])subnodeGuids);

                ps = guids
                    .Where(g => g != Guid.Empty)
                    .SelectMany(k =>
                    {
                        var n = storage.GetNodeDataFromKey(k);
                        var nCell = ((Cell)n[Durable.Octree.Cell]);
                        var nCenter = nCell.GetCenter();
                        var delta = nCenter - cellCenter;
                        var xs = storage.GetNodePositions(n).Map(x => (V3d)x + nCenter);
                        //Report.Line($"    {k} -> {xs.Length}");
                        return xs;
                    })
                    .ToArray()
                    .Map(p => (V3f)(p - cellCenter));

                if (hasColors)
                {
                    cs = guids
                        .Where(g => g != Guid.Empty)
                        .Select(k => storage.GetNodeDataFromKey(k))
                        .SelectMany(n => storage.GetNodeColors(n))
                        .ToArray();
                }

                var guids2 = guids
                    .Map(k =>
                    {
                        if (k == Guid.Empty) return Guid.Empty;
                        var n = storage.GetNodeDataFromKey(k);
                        return n.ContainsKey(Durable.Octree.SubnodesGuids) ? k : Guid.Empty;
                    });
                var isNewLeaf = guids2.All(k => k == Guid.Empty);
                if (isNewLeaf)
                {
                    subnodeGuids = null;
                }
                else
                {
                    foreach (var g in guids) if (g != Guid.Empty) survive.Add(g);
                }

                if (hasColors && ps.Length != cs.Length) throw new InvalidOperationException(
                    $"Different number of positions ({ps.Length}) and colors ({cs.Length}). " +
                    "Invariant ac1cdac5-b7a2-4557-9383-ae80929af999."
                    );
            }
            else
            {
                var psRef = node[Durable.Octree.PositionsLocal3fReference];
                ps = storage.GetV3fArray(((Guid)psRef).ToString());

                if (hasColors)
                {
                    var csRef = node[Durable.Octree.Colors4bReference];
                    cs = storage.GetC4bArray(((Guid)csRef).ToString());
                }
            }

            // optionally round positions
            if (config.PositionsRoundedToNumberOfDigits.HasValue)
            {
                ps = ps.Map(x => x.Round(config.PositionsRoundedToNumberOfDigits.Value));
            }

            // result
            pointCountCell = ps.Length;
            var cs3 = cs?.Map(x => new C3b(x));
            var result = new InlinedNode(id, cell, bbExactGlobal, (Guid[])subnodeGuids, pointCountCell, pointCountTree, ps, cs3);
            return result;
        }

        private static IReadOnlyDictionary<Durable.Def, object> GetNodeDataFromKey(this Storage storage, Guid key)
        {
            var (_, raw) = storage.GetDurable(key);
            if (raw != null) return (raw as IReadOnlyDictionary<Durable.Def, object>);

            var n = storage.GetPointCloudNode(key);
            if (n != null) return (n.Properties);

            return null;
        }

        private static V3f[] GetNodePositions(this Storage storage, IReadOnlyDictionary<Durable.Def, object> n)
        {
            if (n.TryGetValue(Durable.Octree.PositionsLocal3fReference, out var r))
            {
                return storage.GetV3fArray(((Guid)r).ToString());
            }
            else if (n.TryGetValue(Durable.Octree.PositionsLocal3f, out var ps))
            {
                return (V3f[])ps;
            }
            else
            {
                throw new InvalidOperationException("No positions. Invariant 167b491b-8e58-4e28-88ed-f0a69590465e.");
            }
        }

        private static C4b[] GetNodeColors(this Storage storage, IReadOnlyDictionary<Durable.Def, object> n)
        {
            if (n.TryGetValue(Durable.Octree.Colors4bReference, out var r))
            {
                return storage.GetC4bArray(((Guid)r).ToString());
            }
            else if (n.TryGetValue(Durable.Octree.Colors3b, out var cs))
            {
                return ((C3b[])cs).Map(c => new C4b(c));
            }
            else
            {
                throw new InvalidOperationException("No colors. Invariant 8516dbaf-9765-44ab-949c-79986514f1d1.");
            }
        }
    }
}
