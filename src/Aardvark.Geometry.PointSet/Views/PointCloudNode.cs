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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
            string id, Cell cell, Box3d boundingBoxExact, long pointCountTree, PersistentRef<IPointCloudNode>[] subnodes,
            params (string attributeName, string attributeKey, object attributeValue)[] attributes
            )
        {
            Storage = storage;
            Id = id;
            Cell = cell;
            Center = cell.GetCenter();
            BoundingBoxExact = boundingBoxExact;
            PointCountTree = pointCountTree;
            SubNodes = subnodes;

            if (attributes != null)
            {
                foreach (var (attributeName, attributeKey, attributeValue) in attributes)
                {
                    var name = attributeName;
                    var value = attributeValue;

                    switch (name)
                    {
                        case PointCloudAttribute.PositionsAbsolute:
                            {
                                var c = Center;
                                name = PointCloudAttribute.Positions;
                                value = ((V3d[])value).Map(p => new V3f(p - c));
                                break;
                            }

                        case PointCloudAttribute.LodPositionsAbsolute:
                            {
                                var c = Center;
                                name = PointCloudAttribute.LodPositions;
                                value = ((V3d[])value).Map(p => new V3f(p - c));
                                break;
                            }

                        case PointCloudAttribute.KdTree:
                        case PointCloudAttribute.LodKdTree:
                            {
                                if (value is PointRkdTreeD<V3f[], V3f>)
                                {
                                    value = ((PointRkdTreeD<V3f[], V3f>)value).Data;
                                }
                                else if (value is PointRkdTreeDData)
                                {
                                    // OK
                                }
                                else if (value is V3d[])
                                {
                                    var c = Center;
                                    value = ((V3d[])value).Map(p => new V3f(p - c)).BuildKdTree();
                                }
                                else if (value is V3f[])
                                {
                                    value = ((V3f[])value).BuildKdTree().Data;
                                }
                                else
                                {
                                    throw new InvalidOperationException();
                                }
                                break;
                            }
                    }

                    if (value is Array || value is PointRkdTreeDData)
                    {
                        storage.StoreAttribute(name, attributeKey, value);
                        m_pRefs[name] = storage.CreatePersistentRef(name, attributeKey, value);
                    }
                    else
                    {
                        if (value is PointRkdTreeD<V3f[], V3f>) throw new InvalidOperationException();
                        m_pRefs[name] = value;
                    }
                    
                    m_pIds[name] = attributeKey;
                }
            }
        }

        private readonly Dictionary<string, string> m_pIds = new Dictionary<string, string>();
        private readonly Dictionary<string, object> m_pRefs = new Dictionary<string, object>();

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
        public PersistentRef<IPointCloudNode>[] SubNodes { get; }

        /// <summary></summary>
        public bool TryGetPropertyKey(string property, out string key) => m_pIds.TryGetValue(property, out key);

        /// <summary></summary>
        public bool TryGetPropertyValue(string property, out object value) => m_pRefs.TryGetValue(property, out value);

        /// <summary></summary>
        public FilterState FilterState => FilterState.FullyInside;

        /// <summary></summary>
        public JObject ToJson()
        {
            return JObject.FromObject(new
            {
                NodeType,
                Id,
                Cell,
                BoundingBoxExact = BoundingBoxExact.ToString(),
                PointCountTree,
                SubNodes = SubNodes.Map(x => x?.Id),
                Properties = m_pIds.Select(kv => new[] { kv.Key, kv.Value }).ToArray(),
                FilterState
            });
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
            var propertyIds = json["Properties"].ToObject<string[][]>();
            var filterState = Enum.Parse(typeof(FilterState), (string)json["FilterState"]);

            var subnodes = subnodeIds.Map(x => x != null ? new PersistentRef<IPointCloudNode>(x, (_id, _ct) => storage.GetPointCloudNode(x, resolver, _ct)) : null);

            return new PointCloudNode(storage, id, cell, boundingBoxExact, pointCountTree, subnodes);
        }

        /// <summary></summary>
        public string NodeType => Type;

        /// <summary></summary>
        public void Dispose() { }
    }
}
