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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// A view onto a set of non-overlapping point cloud nodes.
    /// </summary>
    public class MergedNodes : IPointCloudNode
    {
        /// <summary></summary>
        public const string Type = "MergedNodes";

        /// <summary>
        /// </summary>
        public static IPointCloudNode Create(Storage storage, IStoreResolver resolver, IEnumerable<IPointCloudNode> nodes, ImportConfig config)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));

            var ns = nodes.ToArray();
            switch (ns.Length)
            {
                case 0 : throw new ArgumentOutOfRangeException(nameof(nodes), "Need at least 1 node (0 given).");
                case 1 : return ns[0];
                default: return new MergedNodes(storage, resolver, ns, config);
            }
        }

        /// <summary>
        /// </summary>
        [JsonIgnore]
        public Storage Storage { get; }
        
        /// <summary>
        /// Property name -> key.
        /// </summary>
        public Dictionary<string, string> PropertyKeys { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Property name -> value.
        /// </summary>
        public Dictionary<string, object> PropertyValues { get; } = new Dictionary<string, object>();


        private MergedNodes(Storage storage, IStoreResolver resolver, string id, Cell cell, Box3d boundingBoxExact, long pointCountTree, string[] subnodeIds)
        {
            Storage = storage;
            Id = id;
            Cell = cell;
            Center = Cell.GetCenter();
            BoundingBoxExact = boundingBoxExact;
            PointCountTree = pointCountTree;

            if (subnodeIds != null)
            {
                SubNodes = new PersistentRef<IPointCloudNode>[8];
                for (var i = 0; i < 8; i++)
                {
                    var sid = subnodeIds[i];
                    if (sid == null) continue;
                    SubNodes[i] = new PersistentRef<IPointCloudNode>(sid, (_, ct) => storage.GetPointCloudNode(sid, resolver, ct));
                }
            }
        }

        /// <summary>
        /// </summary>
        private MergedNodes(Storage storage, IStoreResolver resolver, IPointCloudNode[] nodes, ImportConfig config)
        {
            Storage = storage;
            Id = Guid.NewGuid().ToString();
            Cell = new Cell(new Box3d(nodes.Select(x => x.Cell.BoundingBox)));
            Center = Cell.GetCenter();
            BoundingBoxExact = new Box3d(nodes.Select(x => x.BoundingBoxExact));
            PointCountTree = nodes.Sum(x => x.PointCountTree);
            
            var test = containedIn(Cell, nodes);
            if (test.Length != nodes.Length) throw new InvalidOperationException();
            IPointCloudNode[] containedIn(Cell c, IEnumerable<IPointCloudNode> ns)
                => ns.Where(x => c.Contains(x.Cell)).ToArray();

            // sort nodes into subcells
            var buckets = new List<IPointCloudNode>[8].SetByIndex(_ => new List<IPointCloudNode>());
            var subcells = Cell.Children;
            foreach (var n in nodes)
            {
                var notInserted = true;
                for (var i = 0; i < 8; i++)
                {
                    if (subcells[i].Contains(n.Cell))
                    {
                        buckets[i].Add(n);
                        notInserted = false;
                        break;
                    }
                }
                if (notInserted) throw new InvalidOperationException();
            }

            // set subcells
            SubNodes = new PersistentRef<IPointCloudNode>[8];
            for (var i = 0; i < 8; i++)
            {
                var bucket = buckets[i];

                if (bucket.Count == 0)
                {
                    SubNodes[i] = null;
                    continue;
                }

                if (bucket.Count == 1)
                {
                    if (bucket[0].Cell == subcells[i])
                    {
                        Console.WriteLine($"FOO -> {bucket[0].CountNodes()}");
                        var localId = bucket[0].Id;
                        SubNodes[i] = new PersistentRef<IPointCloudNode>(localId, (_id, _ct) => storage.GetPointCloudNode(localId, resolver, _ct));
                        bucket.Clear();
                        continue;
                    }
                }

                var subnode = Create(storage, resolver, bucket.ToArray(), config);
                SubNodes[i] = new PersistentRef<IPointCloudNode>(subnode.Id, (_id, _ct) => storage.GetPointCloudNode(_id, resolver, _ct));
            }

            // lod
            if (config.CreateOctreeLod)
            {
                throw new NotImplementedException();
            }

            // normals
            if (config.EstimateNormals != null)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Key.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// This node's index/bounds.
        /// </summary>
        public Cell Cell { get; }

        /// <summary>
        /// Center of this node's cell.
        /// </summary>
        public V3d Center { get; }

        /// <summary>
        /// Exact bounding box of all points in this tree.
        /// </summary>
        public Box3d BoundingBoxExact { get; }

        /// <summary></summary>
        public long PointCountTree { get; }

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] SubNodes { get; }
        
        /// <summary></summary>
        public bool TryGetPropertyKey(string property, out string key)
            => PropertyKeys.TryGetValue(property, out key);

        /// <summary></summary>
        public bool TryGetPropertyValue(string property, out object value)
            => PropertyValues.TryGetValue(property, out value);

        /// <summary></summary>
        public FilterState FilterState => FilterState.FullyInside;

        /// <summary></summary>
        public JObject ToJson() => JObject.FromObject(new
        {
            NodeType,
            Id,
            Cell,
            BoundingBoxExact = BoundingBoxExact.ToString(),
            PointCountTree,
            SubNodeIds = SubNodes?.Select(x => x?.Id).ToArray()
        });

        /// <summary></summary>
        public static MergedNodes Parse(JObject json, Storage storage, IStoreResolver resolver)
            => new MergedNodes(storage, resolver,
                (string)json["Id"],
                json["Cell"].ToObject<Cell>(),
                Box3d.Parse((string)json["BoundingBoxExact"]),
                (long)json["PointCountTree"],
                json["SubNodeIds"].ToObject<string[]>()
                )
            ;

        /// <summary></summary>
        public string NodeType => Type;

        /// <summary></summary>
        public void Dispose()
        {
            foreach (var n in SubNodes)
            {
                if (n == null) continue;
                if (n.TryGetValue(out IPointCloudNode value)) value.Dispose();
            }
        }
    }
}
