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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// A view onto a set of non-overlapping point cloud nodes.
    /// </summary>
    public class ViewMerged : IPointCloudNode
    {
        /// <summary>
        /// </summary>
        public static IPointCloudNode Create(Storage storage, IEnumerable<IPointCloudNode> nodes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));

            var ns = nodes.ToArray();
            switch (ns.Length)
            {
                case 0 : throw new ArgumentOutOfRangeException(nameof(nodes), "Need at least 1 node (0 given).");
                case 1 : return ns[0];
                default: return new ViewMerged(storage, ns);
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
        public Dictionary<string, string> Properties { get; }
        
        /// <summary>
        /// </summary>
        private ViewMerged(Storage storage, IPointCloudNode[] nodes)
        {
            Storage = storage;
            Nodes = nodes.ToArray();

            Id = Guid.NewGuid().ToString();
            Cell = new Cell(new Box3d(Nodes.Select(x => x.Cell.BoundingBox)));
            Center = Cell.GetCenter();
            PointCountTree = nodes.Sum(x => x.PointCountTree);
            
            var test = containedIn(Cell, Nodes);
            if (test.Length != Nodes.Length) throw new InvalidOperationException();

            IPointCloudNode[] containedIn(Cell c, IEnumerable<IPointCloudNode> ns)
                => ns.Where(x => c.Contains(x.Cell)).ToArray();
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
            => Properties.TryGetValue(property, out key);

        /// <summary>
        /// </summary>
        public bool TryGetPropertyValue(string property, out object value)
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
