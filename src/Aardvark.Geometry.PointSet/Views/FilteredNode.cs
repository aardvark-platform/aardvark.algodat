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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        public FilteredNode(Guid id, IPointCloudNode node, IFilter filter, HashSet<int> activePoints = null)
        {
            Id = id;
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
            : this(Guid.NewGuid(), node, filter)
        { }

        #endregion

        /// <summary></summary>
        public Storage Storage => Node.Storage;

        /// <summary></summary>
        public Guid Id { get; }

        /// <summary></summary>
        public Cell Cell => Node.Cell;

        /// <summary></summary>
        public V3d Center => Node.Center;

        /// <summary></summary>
        public Box3d BoundingBoxExactGlobal => Node.BoundingBoxExactGlobal;

        /// <summary></summary>
        public long PointCountTree => Node.PointCountTree;

        /// <summary></summary>
        public float PointDistanceAverage => Node.PointDistanceAverage;

        /// <summary></summary>
        public float PointDistanceStandardDeviation => Node.PointDistanceStandardDeviation;

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] Subnodes
        {
            get
            {
                if (Node.Subnodes == null) return null;

                if (m_subnodes_cache == null)
                {
                    m_subnodes_cache = new PersistentRef<IPointCloudNode>[8];
                    for (var i = 0; i < 8; i++)
                    {
                        var id = (Id + "." + i).ToGuid();
                        var n0 = Node.Subnodes[i]?.Value;
                        var n = n0 != null ? new FilteredNode(id, n0, Filter) : null;
                        m_subnodes_cache[i] = new PersistentRef<IPointCloudNode>(id.ToString(), _ => n, _ => (true, n));
                    }
                }
                return m_subnodes_cache;
            }
        }

        /// <summary></summary>
        public bool Has(Durable.Def what) => Node.Has(what);

        /// <summary></summary>
        public bool TryGetValue(Durable.Def what, out object o) => Node.TryGetValue(what, out o);

        /// <summary></summary>
        public void Dispose() => Node.Dispose();

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

        /// <summary>
        /// </summary>
        public IPointCloudNode WithUpsert(Durable.Def def, object x)
        {
            throw new NotImplementedException();
        }

        /// <summary></summary>
        public IPointCloudNode WriteToStore()
        {
            throw new NotImplementedException();
        }

        /// <summary></summary>
        public IPointCloudNode WithSubNodes(PointSetNode[] subnodes)
        {
            throw new NotImplementedException();
        }

        /// <summary></summary>
        public IPointCloudNode WithSubNodes(IPointCloudNode[] subnodes)
        {
            throw new NotImplementedException();
        }

        /// <summary></summary>
        private ImmutableDictionary<Durable.Def, object> Data => throw new NotImplementedException();

        /// <summary></summary>
        public bool HasPositions => throw new NotImplementedException();

        /// <summary></summary>
        public V3f[] Positions => throw new NotImplementedException();

        /// <summary></summary>
        public V3d[] PositionsAbsolute => throw new NotImplementedException();

        /// <summary></summary>
        public int PointCountCell => throw new NotImplementedException();

        PersistentRef<V3f[]> IPointCloudNode.Positions => throw new NotImplementedException();

        /// <summary></summary>
        public bool HasColors => throw new NotImplementedException();

        /// <summary></summary>
        public bool HasNormals => throw new NotImplementedException();

        /// <summary></summary>
        public bool HasIntensities => throw new NotImplementedException();

        /// <summary></summary>
        public bool HasClassifications => throw new NotImplementedException();

        /// <summary></summary>
        public bool IsLeaf => throw new NotImplementedException();

        /// <summary></summary>
        public bool HasKdTree => throw new NotImplementedException();

        /// <summary></summary>
        public PersistentRef<PointRkdTreeF<V3f[], V3f>> KdTree => throw new NotImplementedException();

        /// <summary></summary>
        public PersistentRef<C4b[]> Colors => throw new NotImplementedException();

        /// <summary></summary>
        public PersistentRef<V3f[]> Normals => throw new NotImplementedException();

        /// <summary></summary>
        public PersistentRef<int[]> Intensities => throw new NotImplementedException();

        /// <summary></summary>
        public PersistentRef<byte[]> Classifications => throw new NotImplementedException();
    }
}
