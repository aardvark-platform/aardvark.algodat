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
using Aardvark.Data;
using Aardvark.Data.Points;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Immutable;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// A link to another octree node (possibly in another store).
    /// </summary>
    public class LinkedNode : IPointCloudNode
    {
        /// <summary></summary>
        public const string Type = "LinkedNode";

        private readonly IStoreResolver m_storeResolver;
        private WeakReference<IPointCloudNode> m_root;
        //private LruDictionary<string, object> m_cache;

        /// <summary>
        /// </summary>
        public string LinkedStoreName { get; }

        /// <summary>
        /// </summary>
        public string LinkedPointCloudKey { get; }

        /// <summary>
        /// Links to different octree.
        /// </summary>
        public LinkedNode(Storage storage, string linkedStoreName, string linkedPointCloudKey, IStoreResolver storeResolver)
            : this(storage, Guid.NewGuid(), linkedStoreName, linkedPointCloudKey, storeResolver) { }

        /// <summary>
        /// Links to different octree.
        /// </summary>
        public LinkedNode(Storage storage, Guid id, string linkedStoreName, string linkedPointCloudKey, IStoreResolver storeResolver)
        {
            m_storeResolver = storeResolver;
            var foo = m_storeResolver.Resolve(linkedStoreName, cache: default).GetPointSet(linkedPointCloudKey, default);
            var root = foo.Octree.Value;

            Storage = storage;
            Id = id;
            Cell = root.Cell;
            Center = Cell.GetCenter();
            BoundingBoxExactGlobal = root.BoundingBoxExactGlobal;
            PointCountTree = root.PointCountTree;
            LinkedStoreName = linkedStoreName;
            LinkedPointCloudKey = linkedPointCloudKey;
        }

        private LinkedNode(Storage storage, Guid id, Cell cell, Box3d boundingBoxExactGlobal, long pointCountTree, string linkedStoreName, string linkedPointCloudKey, IStoreResolver storeResolver)
        {
            Storage = storage;
            Id = id;
            Cell = cell;
            Center = Cell.GetCenter();
            BoundingBoxExactGlobal = boundingBoxExactGlobal;
            PointCountTree = pointCountTree;
            LinkedStoreName = linkedStoreName;
            LinkedPointCloudKey = linkedPointCloudKey;
        }

        /// <summary></summary>
        public IPointCloudNode Root
        {
            get
            {
                if (m_root != null && m_root.TryGetTarget(out IPointCloudNode r))
                {
                    return r;
                }

                var storage = m_storeResolver.Resolve(LinkedStoreName, cache: default);
                r = storage.GetPointSet(LinkedPointCloudKey, default).Octree.Value;
                m_root = new WeakReference<IPointCloudNode>(r);
                return r;
            }
        }

        /// <summary></summary>
        public Guid Id { get; }

        /// <summary></summary>
        public Cell Cell { get; }

        /// <summary></summary>
        public V3d Center { get; }

        /// <summary>
        /// Exact bounding box of all points in this tree.
        /// </summary>
        public Box3d BoundingBoxExactGlobal { get; }

        /// <summary></summary>
        public long PointCountTree { get; }

        /// <summary></summary>
        public float PointDistanceAverage => Root.PointDistanceAverage;

        /// <summary></summary>
        public float PointDistanceStandardDeviation => Root.PointDistanceStandardDeviation;

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] SubNodes => Root.SubNodes;

        /// <summary></summary>
        public bool Has(Durable.Def what) => Root.Has(what);

        /// <summary></summary>
        public bool TryGetValue(Durable.Def what, out object o) => Root.TryGetValue(what, out o);

        /// <summary></summary>
        [JsonIgnore]
        public Storage Storage { get; }

        ///// <summary></summary>
        //public bool TryGetPropertyKey(string property, out string key)
        //    => Root.TryGetPropertyKey(property, out key);

        ///// <summary></summary>
        //public bool TryGetPropertyValue(string property, out object value)
        //    => Root.TryGetPropertyValue(property, out value);

        /// <summary></summary>
        public FilterState FilterState => FilterState.FullyInside;

        /// <summary></summary>
        public JObject ToJson() => JObject.FromObject(new
        {
            NodeType,
            Id,
            Cell,
            BoundingBoxExactGlobal = BoundingBoxExactGlobal.ToString(),
            PointCountTree,
            LinkedStoreName,
            LinkedPointCloudKey
        });

        /// <summary></summary>
        public static LinkedNode Parse(JObject json, Storage storage, IStoreResolver resolver)
            => new LinkedNode(storage, (Guid)json["Id"],
                json["Cell"].ToObject<Cell>(),
                Box3d.Parse((string)json["BoundingBoxExactGlobal"]),
                (long)json["PointCountTree"],
                (string)json["LinkedStoreName"],
                (string)json["LinkedPointCloudKey"],
                resolver
                );

        /// <summary></summary>
        public string NodeType => Type;

        /// <summary></summary>
        public ImmutableDictionary<Durable.Def, object> Data => throw new NotImplementedException();

        /// <summary></summary>
        public void Dispose() => Storage?.Dispose();
    }
}
