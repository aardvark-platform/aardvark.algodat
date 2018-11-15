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
        /// Original (non-overlapping) nodes.
        /// </summary>
        public IPointCloudNode[] Nodes { get; }

        /// <summary>
        /// Property name -> key.
        /// </summary>
        public Dictionary<string, string> PropertyKeys { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Property name -> value.
        /// </summary>
        public Dictionary<string, object> PropertyValues { get; } = new Dictionary<string, object>();

        /// <summary>
        /// </summary>
        private MergedNodes(Storage storage, IStoreResolver resolver, IPointCloudNode[] nodes, ImportConfig config)
        {
            Storage = storage;
            Nodes = nodes;
            Id = Nodes.Select(x => x.Id).ToGuid().ToString();
            Cell = new Cell(new Box3d(Nodes.Select(x => x.Cell.BoundingBox)));
            Center = Cell.GetCenter();
            PointCountTree = Nodes.Sum(x => x.PointCountTree);
            BoundingBoxExact = new Box3d(Nodes.Select(x => x.BoundingBoxExact));
            
            var test = containedIn(Cell, Nodes);
            if (test.Length != Nodes.Length) throw new InvalidOperationException();
            IPointCloudNode[] containedIn(Cell c, IEnumerable<IPointCloudNode> ns)
                => ns.Where(x => c.Contains(x.Cell)).ToArray();

            // sort nodes into subcells
            var buckets = new List<IPointCloudNode>[8].SetByIndex(_ => new List<IPointCloudNode>());
            var subcells = Cell.Children;
            foreach (var n in Nodes)
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
            Subnodes = new PersistentRef<IPointCloudNode>[8];
            for (var i = 0; i < 8; i++)
            {
                var bucket = buckets[i];

                if (bucket.Count == 0)
                {
                    Subnodes[i] = null;
                    continue;
                }

                if (bucket.Count == 1)
                {
                    if (bucket[0].Cell == subcells[i])
                    {
                        Subnodes[i] = new PersistentRef<IPointCloudNode>(bucket[0].Id, storage.GetPointCloudNode);
                        bucket.Clear();
                        continue;
                    }
                }

                var subnode = new MergedNodes(storage, resolver, bucket.ToArray(), config);
                Subnodes[i] = new PersistentRef<IPointCloudNode>(subnode.Id, storage.GetPointCloudNode);
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
        public PersistentRef<IPointCloudNode>[] Subnodes { get; }
        
        /// <summary></summary>
        public bool TryGetPropertyKey(string property, out string key)
            => PropertyKeys.TryGetValue(property, out key);

        /// <summary></summary>
        public bool TryGetPropertyValue(string property, out object value)
            => PropertyValues.TryGetValue(property, out value);

        /// <summary></summary>
        public FilterState FilterState => FilterState.FullyInside;

        /// <summary>
        /// </summary>
        public JObject Serialize()
        {
            throw new NotImplementedException();
        }

        /// <summary></summary>
        public void Dispose()
        {
            foreach (var x in Nodes) x.Dispose();
        }
    }
}
