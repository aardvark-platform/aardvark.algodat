/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Base.Sorting;
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using static Aardvark.Data.Durable;

namespace Aardvark.Geometry.Points;


/// <summary>
/// A filtered view onto a point cloud.
/// Instances may be shared across threads: all derived state is computed lazily and thread-safely.
/// </summary>
public class FilteredNode : IPointCloudNode
{
    #region Construction

    /// <summary>
    /// Creates a permanent FilteredNode, which is written to the store.
    /// </summary>
    public static IPointCloudNode Create(Guid id, IPointCloudNode node, IFilter filter)
    {
        if (node.IsTemporaryImportNode) throw new InvalidOperationException(
            "FilteredNode cannot be created from temporary import node. Invariant b9c2dca3-1510-4ea7-959f-6a0737c707fa."
            );
        return new FilteredNode(id, writeToStore: true, node, filter);
    }

    /// <summary>
    /// Creates a permanent FilteredNode, which is written to the store.
    /// </summary>
    public static IPointCloudNode Create(IPointCloudNode node, IFilter filter)
        => Create(Guid.NewGuid(), node, filter);

    /// <summary>
    /// Creates an in-memory FilteredNode, which is not written to the store.
    /// </summary>
    public static IPointCloudNode CreateTransient(Guid id, IPointCloudNode node, IFilter filter)
    {
        if (node.IsTemporaryImportNode) throw new InvalidOperationException(
            "FilteredNode cannot be created from temporary import node. Invariant e2b1d90c-38b8-4e05-a832-97ca4936ed3c."
            );
        return new FilteredNode(id, writeToStore: false, node, filter);
    }

    /// <summary>
    /// Creates an in-memory FilteredNode, which is not written to the store.
    /// </summary>
    public static IPointCloudNode CreateTransient(IPointCloudNode node, IFilter filter)
        => CreateTransient(Guid.NewGuid(), node, filter);

    /// <summary>
    /// </summary>
    private FilteredNode(Guid id, bool writeToStore, IPointCloudNode node, IFilter filter)
    {
        Id = id;
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Filter = filter ?? throw new ArgumentNullException(nameof(filter));

        if (filter.IsFullyInside(node)) m_activePoints = null;
        else if (filter.IsFullyOutside(node)) m_activePoints = [];
        else m_activePoints = Filter.FilterPoints(node, m_activePoints);

        // Derived state is computed on first access. A FilteredNode may be queried from several
        // threads at once (e.g. it is cached in the store's LRU), so every lazy value is
        // published exactly once (ExecutionAndPublication).
        const LazyThreadSafetyMode mode = LazyThreadSafetyMode.ExecutionAndPublication;
        m_subnodes = new(ComputeSubnodes, mode);
        m_subsetIndexArray = new(ComputeSubsetIndexArray, mode);
        m_positions = new(ComputePositions, mode);
        m_boundingBoxExactLocal = new(ComputeBoundingBoxExactLocal, mode);
        m_kdTree = new(ComputeKdTree, mode);
        m_colors = new(() => SubsetOf(Node.Colors), mode);
        m_normals = new(() => SubsetOf(Node.Normals), mode);
        m_intensities = new(() => SubsetOf(Node.Intensities), mode);
        m_classifications = new(() => SubsetOf(Node.Classifications), mode);
        m_partIndices1i = new(ComputePartIndices1i, mode);
        m_partIndexRange = new(ComputePartIndexRange, mode);

        if (writeToStore) WriteToStore();
    }

    #endregion

    #region Properties

    private readonly HashSet<int>? m_activePoints;

    private readonly Lazy<PersistentRef<IPointCloudNode>?[]?> m_subnodes;
    private readonly Lazy<int[]?> m_subsetIndexArray;
    private readonly Lazy<PersistentRef<V3f[]>> m_positions;
    private readonly Lazy<Box3f> m_boundingBoxExactLocal;
    private readonly Lazy<PersistentRef<PointRkdTreeF<V3f[], V3f>>> m_kdTree;
    private readonly Lazy<PersistentRef<C4b[]>?> m_colors;
    private readonly Lazy<PersistentRef<V3f[]>?> m_normals;
    private readonly Lazy<PersistentRef<int[]>?> m_intensities;
    private readonly Lazy<PersistentRef<byte[]>?> m_classifications;
    private readonly Lazy<int[]?> m_partIndices1i;
    private readonly Lazy<Range1i?> m_partIndexRange;

    /// <summary></summary>
    public Guid Id { get; }

    /// <summary></summary>
    public bool IsTemporaryImportNode => false;

    /// <summary> </summary>
    public IPointCloudNode Node { get; }

    /// <summary></summary>
    public IFilter Filter { get; }

    /// <summary></summary>
    public Storage Storage => Node.Storage;

    /// <summary></summary>
    public bool IsMaterialized => false;

    /// <summary></summary>
    public bool IsEmpty => Node.IsEmpty;

    /// <summary></summary>
    public IPointCloudNode Materialize()
    {
        var newId = Guid.NewGuid();
        var data = ImmutableDictionary<Def, object>.Empty
            .Add(Octree.NodeId, newId)
            .Add(Octree.Cell, Cell)
            .Add(Octree.BoundingBoxExactLocal, BoundingBoxExactLocal)
            ;

        if (IsLeaf)
        {
            data = data.Add(Octree.BoundingBoxExactGlobal, BoundingBoxExactGlobal);
        }
        else
        {
            var subnodes = Subnodes.Map(x => x?.Value.Materialize());
            var subnodeIds = subnodes.Map(x => x?.Id ?? Guid.Empty);
            var bbExactGlobal = new Box3d(subnodes.Where(x => x != null).Select(x => x!.BoundingBoxExactGlobal));
            data = data
                .Add(Octree.SubnodesGuids, subnodeIds)
                .Add(Octree.BoundingBoxExactGlobal, bbExactGlobal)
                ;
        }

        if (HasPositions)
        {
            var id = Guid.NewGuid();
            Storage.Add(id, Positions.Value);
            data = data.Add(Octree.PositionsLocal3fReference, id);
        }

        if (HasKdTree)
        {
            var id = Guid.NewGuid();
            Storage.Add(id, KdTree.Value.Data);
            data = data.Add(Octree.PointRkdTreeFDataReference, id);
        }

        if (HasColors)
        {
            var id = Guid.NewGuid();
            Storage.Add(id, Colors.Value);
            data = data.Add(Octree.Colors4bReference, id);
        }

        if (HasNormals)
        {
            var id = Guid.NewGuid();
            Storage.Add(id, Normals.Value);
            data = data.Add(Octree.Normals3fReference, id);
        }

        if (HasClassifications)
        {
            var id = Guid.NewGuid();
            Storage.Add(id, Classifications.Value);
            data = data.Add(Octree.Classifications1bReference, id);
        }

        if (HasIntensities)
        {
            var id = Guid.NewGuid();
            Storage.Add(id, Intensities.Value);
            data = data.Add(Octree.Intensities1iReference, id);
        }

        var result = new PointSetNode(data, Storage, writeToStore: true);
        return result;
    }

    /// <summary></summary>
    public Cell Cell => Node.Cell;

    /// <summary></summary>
    public V3d Center => Node.Center;

    /// <summary></summary>
    public long PointCountTree
    {
        get
        {
            if(m_activePoints == null)
            {
                return Node.PointCountTree;
            }
            else if(m_activePoints.Count == 0L)
            {
                return 0L;
            }
            else
            {
                var ratio = (double)m_activePoints.Count / (double)Node.Positions.Value.Length;
                return (long)((double)Node.PointCountTree * ratio);
            }
        }
    }

    /// <summary></summary>
    public bool Has(Def what) => Node.Has(what);

    /// <summary></summary>
    public bool TryGetValue(Def what, [NotNullWhen(true)]out object? o) => Node.TryGetValue(what, out o);

    /// <summary></summary>
    public IReadOnlyDictionary<Def, object> Properties => Node.Properties;

    /// <summary></summary>
    public PersistentRef<IPointCloudNode>[]? Subnodes => m_subnodes.Value!;

    private PersistentRef<IPointCloudNode>?[]? ComputeSubnodes()
    {
        if (Node.Subnodes == null) return null;

        var result = new PersistentRef<IPointCloudNode>?[8];
        for (var i = 0; i < 8; i++)
        {
            var subCell = Cell.GetOctant(i);

            var spatial = Filter as ISpatialFilter;

            if (spatial != null && spatial.IsFullyInside(subCell.BoundingBox))
            {
                result[i] = Node.Subnodes[i];
            }
            else if (spatial != null && spatial.IsFullyOutside(subCell.BoundingBox))
            {
                result[i] = null;
            }
            else
            {
                var id = (Id + "." + i).ToGuid();
                var n0 = Node.Subnodes[i]?.Value;
                if (n0 != null)
                {
                    if (Filter.IsFullyInside(n0))
                    {
                        result[i] = Node.Subnodes[i];
                    }
                    else if (Filter.IsFullyOutside(n0))
                    {
                        result[i] = null;
                    }
                    else
                    {
                        var n = new FilteredNode(id, false, n0, Filter);
                        result[i] = new PersistentRef<IPointCloudNode>(id, n);
                    }
                }
                else
                {
                    result[i] = null;
                }
            }
        }
        return result;
    }

    /// <summary></summary>
    public int PointCountCell => m_activePoints == null ? Node.PointCountCell : m_activePoints.Count;

    /// <summary></summary>
    public bool IsLeaf => Node.IsLeaf;

    #region Positions

    /// <summary></summary>
    public bool HasPositions => Node.HasPositions;

    /// <summary></summary>
    public PersistentRef<V3f[]> Positions => m_positions.Value;

    /// <summary>
    /// Point positions (absolute). Returns a new array on each call (like PointSetNode), so callers own it.
    /// </summary>
    public V3d[] PositionsAbsolute
    {
        get
        {
            var c = Center;
            return Positions.Value.Map(p => (V3d)p + c);
        }
    }

    private PersistentRef<V3f[]> ComputePositions()
        => SubsetOf(Node.Positions) ?? throw new InvalidOperationException("Invariant 8a3f0c1e-2d44-4b8e-9c47-5f0c2b7a1d90.");

    private Box3f ComputeBoundingBoxExactLocal()
    {
        var psLocal = Positions.Value;
        return psLocal.Length > 0 ? new Box3f(psLocal) : Box3f.Invalid;
    }

    private PersistentRef<PointRkdTreeF<V3f[], V3f>> ComputeKdTree()
        => new(Guid.NewGuid(), Positions.Value.BuildKdTree());

    #endregion

    #region BoundingBoxExactLocal

    /// <summary></summary>
    public bool HasBoundingBoxExactLocal => Node.HasBoundingBoxExactLocal;

    /// <summary></summary>
    public Box3f BoundingBoxExactLocal => m_boundingBoxExactLocal.Value;

    #endregion

    #region BoundingBoxExactGlobal

    /// <summary></summary>
    public bool HasBoundingBoxExactGlobal => Node.HasBoundingBoxExactGlobal;

    /// <summary></summary>
    public Box3d BoundingBoxExactGlobal
    {
        get
        {
            if (Filter is ISpatialFilter sf) return sf.Clip(Node.BoundingBoxExactGlobal);
            else return Node.BoundingBoxExactGlobal;
        }
    }

    public Box3d BoundingBoxApproximate
    {
        get
        {
            if (Filter is ISpatialFilter sf) return sf.Clip(Node.BoundingBoxApproximate);
            else return Node.BoundingBoxApproximate;
        }
    }
    #endregion

    #region KdTree

    /// <summary></summary>

    [MemberNotNullWhen(true, nameof(KdTree))]
    public bool HasKdTree => Node.HasKdTree;

    /// <summary></summary>
    public PersistentRef<PointRkdTreeF<V3f[], V3f>> KdTree => m_kdTree.Value;

    #endregion

    #region Colors

    /// <summary></summary>

    [MemberNotNullWhen(true, nameof(Colors))]
    public bool HasColors => Node.HasColors;

    /// <summary></summary>
    public PersistentRef<C4b[]>? Colors => m_colors.Value;

    #endregion

    #region Normals

    /// <summary></summary>

    [MemberNotNullWhen(true, nameof(Normals))]
    public bool HasNormals => Node.HasNormals;

    /// <summary></summary>
    public PersistentRef<V3f[]>? Normals => m_normals.Value;

    #endregion

    #region Intensities

    /// <summary></summary>

    [MemberNotNullWhen(true, nameof(Intensities))]
    public bool HasIntensities => Node.HasIntensities;

    /// <summary></summary>
    public PersistentRef<int[]>? Intensities => m_intensities.Value;

    #endregion

    #region Classifications

    /// <summary></summary>

    [MemberNotNullWhen(true, nameof(Classifications))]
    public bool HasClassifications => Node.HasClassifications;

    /// <summary></summary>
    public PersistentRef<byte[]>? Classifications => m_classifications.Value;

    #endregion

    #region PartIndices

    /// <summary>
    /// True if this node has a PartIndexRange.
    /// </summary>
    [JsonIgnore]
    [MemberNotNullWhen(true, nameof(PartIndexRange))]
    public bool HasPartIndexRange => Node.HasPartIndexRange;

    /// <summary>
    /// Octree. Min and max part index in octree.
    /// </summary>
    public Range1i? PartIndexRange => m_partIndexRange.Value;

    private Range1i? ComputePartIndexRange()
    {
        if (!HasPartIndices) return null;
        if (SubsetIndexArray == null) return Node.PartIndexRange;
        if (TryGetPartIndices(out var qs)) return new Range1i(qs);
        throw new Exception($"Expected part indices exist. Error 8d191c4a-042c-4179-ac30-8a10aab16436.");
    }

    /// <summary>
    /// True if this node has part indices.
    /// </summary>
    [MemberNotNullWhen(true, nameof(PartIndices))]
    public bool HasPartIndices => Node.HasPartIndices;

    /// <summary>
    /// Octree. Per-point or per-cell part indices.
    /// </summary>
    public object? PartIndices => SubsetIndexArray != null ? PartIndexUtils.Subset(Node.PartIndices, SubsetIndexArray) : Node.PartIndices;

    /// <summary>
    /// Get per-point part indices as an int array (regardless of internal representation).
    /// Returns false if node has no part indices.
    /// </summary>
    public bool TryGetPartIndices([NotNullWhen(true)] out int[]? result)
    {
        result = m_partIndices1i.Value;
        return result != null;
    }

    private int[]? ComputePartIndices1i()
    {
        var qs = PartIndices;
        switch (qs)
        {
            case null: return null;
            case int x: return new int[PointCountCell].Set(x);
            case uint x: checked { return new int[PointCountCell].Set((int)x); }
            case byte[] xs: return xs.Map(x => (int)x);
            case short[] xs: return xs.Map(x => (int)x);
            case int[] xs: return xs;
            default:
                throw new Exception(
                $"Unexpected type {qs.GetType().FullName}. " +
                $"Error ccc0b898-fe4f-4373-ac15-42da763fe5ab."
                );
        }
    }

    #endregion

    #region Velocities

    /// <summary>
    /// Deprecated. Always returns false. Use custom attributes instead.
    /// </summary>
    [Obsolete("Use custom attributes instead.")]
#pragma warning disable CS0618 // Type or member is obsolete
    [MemberNotNullWhen(true, nameof(Velocities))]
#pragma warning restore CS0618 // Type or member is obsolete
    public bool HasVelocities => false;

    /// <summary>
    /// Deprecated. Always returns null. Use custom attributes instead.
    /// </summary>
    [Obsolete("Use custom attributes instead.")]
    public PersistentRef<V3f[]>? Velocities => null;

    #endregion

    #region CentroidLocal

    /// <summary></summary>
    public bool HasCentroidLocal => Node.HasCentroidLocal;

    /// <summary></summary>
    public V3f CentroidLocal => Node.CentroidLocal;

    /// <summary></summary>
    public bool HasCentroidLocalStdDev => Node.HasCentroidLocalStdDev;

    /// <summary></summary>
    public float CentroidLocalStdDev => Node.CentroidLocalStdDev;

    #endregion

    #region TreeDepth

    /// <summary></summary>
    public bool HasMinTreeDepth => Node.HasMinTreeDepth;

    /// <summary></summary>
    public int MinTreeDepth => Node.MinTreeDepth;

    /// <summary></summary>
    public bool HasMaxTreeDepth => Node.HasMaxTreeDepth;

    /// <summary></summary>
    public int MaxTreeDepth => Node.MaxTreeDepth;

    #endregion

    #region PointDistance

    /// <summary></summary>
    public bool HasPointDistanceAverage => Node.HasPointDistanceAverage;

    /// <summary>
    /// Average distance of points in this cell.
    /// </summary>
    public float PointDistanceAverage => Node.PointDistanceAverage;

    /// <summary></summary>
    public bool HasPointDistanceStandardDeviation => Node.HasPointDistanceStandardDeviation;

    /// <summary>
    /// Standard deviation of distance of points in this cell.
    /// </summary>
    public float PointDistanceStandardDeviation => Node.PointDistanceStandardDeviation;

    #endregion

    /// <summary>
    /// Returns the subset of the given per-point attribute that survives the filter
    /// (or the original reference if all points are inside). Pure; results are memoized by the callers' Lazy fields.
    /// </summary>
    private PersistentRef<T[]>? SubsetOf<T>(PersistentRef<T[]>? originalValue)
    {
        if (originalValue == null) return null;
        // should be empty not null, right?
        if (m_activePoints == null) return originalValue;

        var key = (Id + originalValue.Id).ToGuid().ToString();
        var xs = originalValue.Value.Where((_, i) => m_activePoints.Contains(i)).ToArray();
        return new PersistentRef<T[]>(key, xs);
    }

    private int[]? SubsetIndexArray => m_subsetIndexArray.Value;

    private int[]? ComputeSubsetIndexArray()
    {
        if (m_activePoints == null) return null;

        var xs = m_activePoints.ToArray();
        xs.QuickSortAscending();
        return xs;
    }

    #endregion

    #region Not supported ...

    /// <summary>
    /// FilteredNode does not support With.
    /// </summary>
    public IPointCloudNode With(IReadOnlyDictionary<Durable.Def, object> replacements)
        => throw new InvalidOperationException("Invariant 3de7dad1-668d-4104-838b-552eae03f7a8.");

    /// <summary>
    /// FilteredNode does not support WithSubNodes.
    /// </summary>
    public IPointCloudNode WithSubNodes(IPointCloudNode?[] subnodes)
        => throw new InvalidOperationException("Invariant 62e6dab8-133a-452d-8d8c-f0b0eb5f286c.");

    #endregion

    #region Durable codec

    /// <summary></summary>
    public static class Defs
    {
        /// <summary></summary>
        public static readonly Def FilteredNode = new(
            new Guid("a5dd1687-ea0b-4735-9be1-b74b969e0673"),
            "Octree.FilteredNode", "Octree.FilteredNode. A filtered octree node.",
            Primitives.DurableMap.Id, false
            );

        /// <summary></summary>
        public static readonly Def FilteredNodeRootId = new(
            new Guid("f9a7c994-35b3-4d50-b5b0-80af05896987"),
            "Octree.FilteredNode.RootId", "Octree.FilteredNode. Node id of the node to be filtered.",
            Primitives.GuidDef.Id, false
            );

        /// <summary></summary>
        public static readonly Def FilteredNodeFilter = new(
            new Guid("1d2298b6-df47-4170-8fc2-4bd899ea6153"),
            "Octree.FilteredNode.Filter", "Octree.FilteredNode. Filter definition as UTF8-encoded JSON string.",
            Primitives.StringUTF8.Id, false
            );
    }

    /// <summary></summary>
    public IPointCloudNode WriteToStore()
    {
        this.CheckDerivedAttributes();
        var buffer = Encode();
        Storage.Add(Id, buffer);
        return this;
    }

    /// <summary>
    /// </summary>
    public byte[] Encode()
    {
        var filter = Filter.Serialize().ToString();

        var x = ImmutableDictionary<Def, object>.Empty
            .Add(Octree.NodeId, Id)
            .Add(Defs.FilteredNodeRootId, Node.Id)
            .Add(Defs.FilteredNodeFilter, filter)
            ;

        return Data.Codec.Serialize(Defs.FilteredNode, x);
    }

    /// <summary>
    /// </summary>
    public static FilteredNode Decode(Storage storage, byte[] buffer)
    {
        var r = Data.Codec.Deserialize(buffer);
        if (r.Item1 != Defs.FilteredNode) throw new InvalidOperationException("Invariant c03cfd90-a083-44f2-a00f-cb36b1735f37.");
        var data = (ImmutableDictionary<Def, object>)r.Item2;
        var id = (Guid)data.Get(Octree.NodeId);
        var filterString = (string)data.Get(Defs.FilteredNodeFilter);
        var filter = Points.Filter.Deserialize(filterString);
        var rootId = (Guid)data.Get(Defs.FilteredNodeRootId);
        var root = storage.GetPointCloudNode(rootId) ?? throw new InvalidOperationException("Invariant 7fd48e32-9741-4241-b68f-6a0a0d261ec8.");
        return new FilteredNode(id, false, root, filter);
    }

    #endregion
}
