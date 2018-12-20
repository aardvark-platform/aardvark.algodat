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

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// A filtered view onto a point cloud.
    /// </summary>
    public class FilteredNode : IPointCloudNode
    {
        /// <summary></summary>
        public const string Type = "FilteredNode";

        /// <summary> </summary>
        public IPointCloudNode Node { get; }

        /// <summary></summary>
        public IFilter Filter { get; }

        /// <summary></summary>
        public FilterState FilterState { get; }

        private readonly HashSet<int> m_activePoints;
        private PersistentRef<IPointCloudNode>[] m_subnodes_cache;

        #region Construction

        /// <summary></summary>
        public FilteredNode(string id, IPointCloudNode node, IFilter filter, HashSet<int> activePoints = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Node = node ?? throw new ArgumentNullException(nameof(node));
            Filter = filter ?? throw new ArgumentNullException(nameof(filter));
            FilterState = Node.GetFilterState(Filter);

            m_activePoints = activePoints;
            if (FilterState == FilterState.Partial)
            {
                m_activePoints = Filter.FilterPoints(Node, m_activePoints);
            }
        }

        /// <summary></summary>
        public FilteredNode(IPointCloudNode node, IFilter filter)
            : this(Guid.NewGuid().ToString(), node, filter)
        { }

        #endregion

        /// <summary></summary>
        public Storage Storage => Node.Storage;

        /// <summary></summary>
        public string Id { get; }

        /// <summary></summary>
        public Cell Cell => Node.Cell;

        /// <summary></summary>
        public V3d Center => Node.Center;

        /// <summary></summary>
        public Box3d BoundingBoxExact => Node.BoundingBoxExact;

        /// <summary></summary>
        public long PointCountTree => Node.PointCountTree;

        /// <summary></summary>
        public float PointDistanceAverage => Node.PointDistanceAverage;

        /// <summary></summary>
        public float PointDistanceStandardDeviation => Node.PointDistanceStandardDeviation;

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] SubNodes
        {
            get
            {
                if (Node.SubNodes == null) return null;

                if (m_subnodes_cache == null)
                {
                    m_subnodes_cache = new PersistentRef<IPointCloudNode>[8];
                    for (var i = 0; i < 8; i++)
                    {
                        var id = (Id + "." + i).ToGuid().ToString();
                        var n0 = Node.SubNodes[i]?.Value;
                        var n = n0 != null ? new FilteredNode(id, n0, Filter) : null;
                        m_subnodes_cache[i] = new PersistentRef<IPointCloudNode>(id, _ => n, _ => (true, n));
                    }
                }
                return m_subnodes_cache;
            }
        }

        /// <summary></summary>
        public void Dispose() => Node.Dispose();

        /// <summary></summary>
        public bool TryGetPropertyKey(string property, out string key)
        {
            if (Node.TryGetPropertyKey(property, out string originalKey))
            {
                key = (Id + originalKey).ToGuid().ToString();
                return true;
            }
            else
            {
                key = null;
                return false;
            }
        }

        private PersistentRef<T[]> GetSubArray<T>(object originalValue)
        {
            var pref = ((PersistentRef<T[]>)originalValue);
            switch (FilterState)
            {
                case FilterState.FullyInside: return pref;
                case FilterState.FullyOutside: return null;
                case FilterState.Partial:
                    var key = (Id + pref.Id).ToGuid().ToString();
                    var xs = pref.Value.Where((_, i) => m_activePoints.Contains(i)).ToArray();
                    return new PersistentRef<T[]>(key, _ => xs, _ => (true, xs));
                default:
                    throw new InvalidOperationException($"Unknown FilterState {FilterState}.");
            }

        }
        /// <summary></summary>
        public bool TryGetPropertyValue(string property, out object value)
        {
            if (Node.TryGetPropertyValue(property, out object originalValue))
            {

                switch (property)
                {
                    case PointCloudAttribute.Classifications: value = GetSubArray<byte>(originalValue); break;

                    case PointCloudAttribute.Colors: value = GetSubArray<C4b>(originalValue); break;

                    case PointCloudAttribute.Intensities: value = GetSubArray<int>(originalValue); break;
                        
                    case PointCloudAttribute.Normals:
                    case PointCloudAttribute.Positions: value = GetSubArray<V3f>(originalValue); break;

                    case PointCloudAttribute.KdTree: throw new NotImplementedException();

                    default: throw new InvalidOperationException($"Cannot convert '{property}' to property.");
                }
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }

        /// <summary></summary>
        public JObject ToJson() => JObject.FromObject(new 
        {
            
        });

        /// <summary></summary>
        public static FilteredNode Parse(JObject json) => throw new NotImplementedException();

        /// <summary></summary>
        public string NodeType => Type;
    }
}
