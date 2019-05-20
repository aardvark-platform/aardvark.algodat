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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// A point cloud node.
    /// </summary>
    public class PointCloudNode : IPointCloudNode
    {
        /// <summary></summary>
        public const string Type = "PointCloudNode";

        /// <summary>
        /// </summary>
        public PointCloudNode(Storage storage,
            string id, Cell cell, Box3d boundingBoxExact, long pointCountTree, PersistentRef<IPointCloudNode>[] subnodes, bool storeOnCreation,
            params (Durable.Def attributeName, Guid attributeKey, object attributeValue)[] attributes
            )
            : this(storage, id, cell, boundingBoxExact, pointCountTree, subnodes, storeOnCreation, ImmutableDictionary<Durable.Def, object>.Empty, attributes)
        { }

        /// <summary>
        /// </summary>
        public PointCloudNode(Storage storage,
            string id, Cell cell, Box3d boundingBoxExact, long pointCountTree, PersistentRef<IPointCloudNode>[] subnodes, bool storeOnCreation,
            ImmutableDictionary<Durable.Def, object> data,
            params (Durable.Def attributeName, Guid attributeKey, object attributeValue)[] attributes
            )
        {
            Storage = storage;
            Id = id;
            Cell = cell;
            Center = cell.GetCenter();
            BoundingBoxExact = boundingBoxExact;
            PointCountTree = pointCountTree;
            SubNodes = subnodes;
            Data = data;

            throw new NotImplementedException();

            //if (attributes != null)
            //{
            //    foreach (var (attributeName, attributeKey, attributeValue) in attributes)
            //    {
            //        var name = attributeName;
            //        var value = attributeValue;

            //        switch (name)
            //        {
            //            case PointCloudAttribute.PositionsAbsolute:
            //                {
            //                    var c = Center;
            //                    name = PointCloudAttribute.Positions;
            //                    value = ((V3d[])value).Map(p => new V3f(p - c));
            //                    break;
            //                }
                            
            //            case PointCloudAttribute.KdTree:
            //                {
            //                    if (value is PointRkdTreeD<V3f[], V3f>)
            //                    {
            //                        value = ((PointRkdTreeD<V3f[], V3f>)value).Data;
            //                    }
            //                    else if (value is PointRkdTreeDData)
            //                    {
            //                        // OK
            //                    }
            //                    else if (value is V3d[])
            //                    {
            //                        var c = Center;
            //                        value = ((V3d[])value).Map(p => new V3f(p - c)).BuildKdTree();
            //                    }
            //                    else if (value is V3f[])
            //                    {
            //                        value = ((V3f[])value).BuildKdTree().Data;
            //                    }
            //                    else
            //                    {
            //                        throw new InvalidOperationException();
            //                    }
            //                    break;
            //                }
            //        }

            //        if (storeOnCreation && (value is Array || value is PointRkdTreeDData))
            //        {
            //            if (storeOnCreation) storage.StoreAttribute(name, attributeKey, value);
            //            m_pRefs[name] = storage.CreatePersistentRef(name, attributeKey, null);
            //        }
            //        else
            //        {
            //            if (value is PointRkdTreeD<V3f[], V3f>) throw new InvalidOperationException();
            //            m_pRefs[name] = value;
            //        }
                    
            //        m_pIds[name] = attributeKey;
            //    }
            //}

            //if (storeOnCreation) storage.Add(Id, this);
        }

        /// <summary>
        /// </summary>
        private PointCloudNode(Storage storage,
            string id, Cell cell, Box3d boundingBoxExact, long pointCountTree, PersistentRef<IPointCloudNode>[] subnodes,
            ImmutableDictionary<Durable.Def, object> data,
            params (string attributeName, string attributeKey)[] attributes
            )
        {
            Storage = storage;
            Id = id;
            Cell = cell;
            Center = cell.GetCenter();
            BoundingBoxExact = boundingBoxExact;
            PointCountTree = pointCountTree;
            SubNodes = subnodes;
            Data = data;

            throw new NotImplementedException();
            //if (attributes != null)
            //{
            //    foreach (var (attributeName, attributeKey) in attributes)
            //    {
            //        m_pRefs[attributeName] = storage.CreatePersistentRef(attributeName, attributeKey, default);
            //        m_pIds[attributeName] = attributeKey;
            //    }
            //}
        }

        private Dictionary<Durable.Def, Guid> m_pIds = new Dictionary<Durable.Def, Guid>();
        private Dictionary<Durable.Def, object> m_pRefs = new Dictionary<Durable.Def, object>();

        /// <summary></summary>
        public Storage Storage { get; }

        /// <summary></summary>
        public string Id { get; }

        /// <summary></summary>
        public Cell Cell { get; }

        /// <summary></summary>
        public V3d Center { get; }

        /// <summary></summary>
        public Box3d BoundingBoxExact { get; }

        /// <summary></summary>
        public long PointCountTree { get; }

        /// <summary></summary>
        public float PointDistanceAverage { get; }

        /// <summary></summary>
        public float PointDistanceStandardDeviation { get; }

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] SubNodes { get; }

        /// <summary></summary>
        public ImmutableDictionary<Durable.Def, object> Data { get; }

        ///// <summary></summary>
        //public bool TryGetPropertyKey(string property, out string key) => m_pIds.TryGetValue(property, out key);

        ///// <summary></summary>
        //public bool TryGetPropertyValue(string property, out object value) => m_pRefs.TryGetValue(property, out value);

        /// <summary></summary>
        public FilterState FilterState => FilterState.FullyInside;

        /// <summary></summary>
        public JObject ToJson()
        {
            throw new NotImplementedException();
            //return JObject.FromObject(new
            //{
            //    NodeType,
            //    Id,
            //    Cell,
            //    BoundingBoxExact = BoundingBoxExact.ToString(),
            //    PointCountTree,
            //    SubNodes = SubNodes?.Map(x => x?.Id),
            //    Properties = m_pIds.Select(kv => new[] { kv.Key, kv.Value }).ToArray(),
            //    FilterState
            //});
        }

        /// <summary></summary>
        public static PointCloudNode Parse(JObject json, Storage storage, IStoreResolver resolver)
        {
            var nodetype = (string)json["NodeType"];
            if (nodetype != Type) throw new InvalidOperationException($"Expected node type '{Type}', but was '{nodetype}'.");

            var id = (string)json["Id"];
            var cell = json["Cell"].ToObject<Cell>();
            var boundingBoxExact = Box3d.Parse((string)json["BoundingBoxExact"]);
            var pointCountTree = (long)json["PointCountTree"];
            var subnodeIds = json["SubNodes"].ToObject<string[]>();
            var attributes = json["Properties"].ToObject<string[][]>().Map(x => (x[0], x[1]));
            var filterState = Enum.Parse(typeof(FilterState), (string)json["FilterState"]);

            var subnodes = subnodeIds?.Map(x => x != null
                ? new PersistentRef<IPointCloudNode>(x, _ => storage.GetPointCloudNode(x, resolver), _ => storage.TryGetPointCloudNode(x)) 
                : null
                );

            return new PointCloudNode(storage, id, cell, boundingBoxExact, pointCountTree, subnodes, ImmutableDictionary<Durable.Def, object>.Empty, attributes);
        }

        /// <summary></summary>
        public string NodeType => Type;

        /// <summary></summary>
        public void Dispose() { }

        #region With...
        
        /// <summary>
        /// Gets node with added lod attributes.
        /// </summary>
        internal PointCloudNode WithData(
            Guid positionsId, Guid kdTreeId, Guid colorsId, Guid normalsId, Guid intensitiesId, Guid classificationsId
            )
        {
            var attributes = m_pIds.Select(kv => (kv.Key, kv.Value)).ToList();
            
            TryAdd(Durable.Octree.Classifications1bReference, classificationsId);
            TryAdd(Durable.Octree.Colors3bReference, colorsId);
            TryAdd(Durable.Octree.Intensities1iReference, intensitiesId);
            TryAdd(Durable.Octree.PointRkdTreeFDataReference, kdTreeId);
            TryAdd(Durable.Octree.Normals3fReference, normalsId);
            TryAdd(Durable.Octree.PositionsLocal3fReference, positionsId);

            throw new NotImplementedException();
            //return new PointCloudNode(Storage, Id, Cell, BoundingBoxExact, PointCountTree, SubNodes, CellAttributes, attributes.ToArray());

            void TryAdd(Durable.Def key, Guid id)
            {
                if (id == null) return;
                if (m_pIds.ContainsKey(key)) throw new InvalidOperationException();
                attributes.Add((key, id));
            }
        }

        //internal PointCloudNode WithCellAttributes(ImmutableDictionary<Guid, object> atts)
        //{
        //    var dict = CellAttributes;
        //    foreach (var kvp in atts) dict = dict.Add(kvp.Key, kvp.Value);

        //    var res = new PointCloudNode(Storage, Id, Cell, BoundingBoxExact, PointCountTree, SubNodes, dict)
        //    {
        //        m_pIds = m_pIds,
        //        m_pRefs = m_pRefs
        //    };
        //    return res;
        //}

        /// <summary></summary>
        public bool TryGetCellAttribute<T>(Durable.Def id, out T value)
        {
            if(Data.TryGetValue(id, out object v) && v is T)
            {
                value = (T)v;
                return true;
            }
            else
            {
                value = default;
                return false;
            }
        }

        #endregion
    }
}
