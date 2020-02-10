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

        public InlineConfig(bool collapse, bool gzipped, int? positionsRoundedToNumberOfDigits)
        {
            Collapse = collapse;
            GZipped = gzipped;
            PositionsRoundedToNumberOfDigits = positionsRoundedToNumberOfDigits;
        }

        public InlineConfig(bool collapse, bool gzipped) : this(collapse, gzipped, null) { }
    }

    /// <summary>
    /// </summary>
    public static class InlineExtensions
    {
        /// <summary>
        /// Experimental!
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
        /// Experimental!
        /// Inlines and exports pointset to another store.
        /// </summary>
        public static void InlineOctree(this Storage storage, Guid key, Storage exportStore, InlineConfig config)
            => InlineOctree(storage, key.ToString(), exportStore, config);

        /// <summary>
        /// Experimental!
        /// Inlines and exports pointset to another store.
        /// </summary>
        public static void InlineOctree(this Storage storage, IPointCloudNode root, Storage exportStore, InlineConfig config)
        {
            var totalNodeCount = root.CountNodes(outOfCore: true);
            Report.Line($"total node count = {totalNodeCount:N0}");

            // export octree (recursively)
            ExportInlinedNode(root.Id);

            void ExportInlinedNode(Guid key)
            {
                if (key == Guid.Empty) return;

                var (def, node) = GetNodeDataFromKey(storage, key);
                var isInnerNode = node.TryGetValue(Durable.Octree.SubnodesGuids, out var subnodeGuids);

                // when collapsing -> don't export child nodes,
                // because data has already been exported with parent node
                if (config.Collapse && !isInnerNode) return;

                // inline node data and export ...
                var inline = storage.ConvertToInline(node, config);
                exportStore.Add(key, def, inline, config.GZipped);
                Report.Line($"exported {key} (node)");

                // export children ...
                if (isInnerNode)
                {
                    foreach (var x in (Guid[])subnodeGuids)
                    {
                        ExportInlinedNode(x);
                    }
                }
            }
        }

        private static (Durable.Def, IReadOnlyDictionary<Durable.Def, object>) GetNodeDataFromKey(Storage storage, Guid key)
        {
            var def = Durable.Octree.Node;
            var raw = default(object);
            try
            {
                (def, raw) = storage.GetDurable(key);
            }
            catch
            {
                var n = storage.GetPointCloudNode(key);
                raw = n.Properties;
            }

            return (def, raw as IReadOnlyDictionary<Durable.Def, object>);
        }

        private static IEnumerable<KeyValuePair<Durable.Def, object>> ConvertToInline(
            this Storage storage,
            IReadOnlyDictionary<Durable.Def, object> node,
            InlineConfig config
            )
        {
            var cell = (Cell)node[Durable.Octree.Cell];
            var bbExactGlobal = (Box3d)node[Durable.Octree.BoundingBoxExactGlobal];
            var pointCountCell = (int)node[Durable.Octree.PointCountCell];
            var pointCountTree = (long)node[Durable.Octree.PointCountTreeLeafs];
            node.TryGetValue(Durable.Octree.SubnodesGuids, out var subnodeGuids);

            var ps = default(V3f[]);
            var cs = default(C4b[]);

            if (config.Collapse)
            {
                if (subnodeGuids != null)
                {
                    var cellCenter = cell.GetCenter();
                    var guids = ((Guid[])subnodeGuids);
                    
                    ps = guids
                        .Where(g => g != Guid.Empty)
                        .SelectMany(key =>
                            {
                                var (_, n) = GetNodeDataFromKey(storage, key);
                                var delta = ((Cell)n[Durable.Octree.Cell]).GetCenter() - cellCenter;
                                var r = n[Durable.Octree.PositionsLocal3fReference];
                                var xs = storage
                                    .GetV3fArray(((Guid)r).ToString())
                                    .Map(x => (V3f)((V3d)x + delta))
                                ;
                                return xs;
                            })
                        .ToArray();
                    
                    cs = guids
                        .Where(g => g != Guid.Empty)
                        .SelectMany(key =>
                        {
                            var (_, n) = GetNodeDataFromKey(storage, key);
                            var r = n[Durable.Octree.Colors4bReference];
                            var xs = storage
                                .GetC4bArray(((Guid)r).ToString())
                            ;
                            return xs;
                        })
                        .ToArray();

                    var isNewLeaf = guids
                        .All(key =>
                        {
                            if (key == Guid.Empty) return true;
                            var (_, n) = GetNodeDataFromKey(storage, key);
                            n.TryGetValue(Durable.Octree.SubnodesGuids, out var grandChildren);
                            return grandChildren == null;
                        });
                    if (isNewLeaf) subnodeGuids = null;

                    if (ps.Length != cs.Length) throw new InvalidOperationException(
                       $"Different number of positions ({ps.Length}) and colors ({cs.Length}). " +
                       "Invariant ac1cdac5-b7a2-4557-9383-ae80929af999."
                       );
                }   
                else
                {
                    throw new InvalidOperationException(
                        "Can't collapse leaf node. " +
                        "Invariant a055891c-2444-46fb-97f7-5a822a172653."
                        );
                }
            }
            else
            {
                var psRef = node[Durable.Octree.PositionsLocal3fReference];
                ps = storage.GetV3fArray(((Guid)psRef).ToString());

                var csRef = node[Durable.Octree.Colors4bReference];
                cs = storage.GetC4bArray(((Guid)csRef).ToString());
            }

            // optionally round positions
            if (config.PositionsRoundedToNumberOfDigits.HasValue)
            {
                ps = ps.Map(x => x.Round(config.PositionsRoundedToNumberOfDigits.Value));
            }

            // result
            KeyValuePair<Durable.Def, object> Entry(Durable.Def def, object o) => 
                new KeyValuePair<Durable.Def, object>(def, o);

            yield return Entry(Durable.Octree.Cell, cell);
            yield return Entry(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal);

            if (subnodeGuids != null)
                yield return Entry(Durable.Octree.SubnodesGuids, subnodeGuids);

            yield return Entry(Durable.Octree.PointCountCell, pointCountCell);
            yield return Entry(Durable.Octree.PointCountTreeLeafs, pointCountTree);
            yield return Entry(Durable.Octree.PointCountTreeLeafsFloat64, (double)pointCountTree);
            yield return Entry(Durable.Octree.PositionsLocal3f, ps);

            //if (node.TryGetValue(Durable.Octree.Normals3fReference, out var nsRef))
            //{
            //    var ns = storage.GetV3fArray(((Guid)nsRef).ToString());
            //    yield return Entry(Durable.Octree.Normals3f, ns);
            //}

            yield return Entry(Durable.Octree.Colors3b, cs.Map(x => new C3b(x)));
        }
    }
}
