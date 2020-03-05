/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class DeleteExtensions
    {
        /// <summary>
        /// Returns new pointset with all points deleted which are inside.
        /// </summary>
        public static PointSet Delete(this PointSet node,
            Func<IPointCloudNode, bool> isNodeFullyInside,
            Func<IPointCloudNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            Storage storage, CancellationToken ct
            )
        {
            var root = Delete(node.Root.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, storage, ct, node.SplitLimit);
            var newId = Guid.NewGuid().ToString();
            var result = new PointSet(node.Storage, newId, root?.Id, node.SplitLimit);
            node.Storage.Add(newId, result);
            return result;
        }

        /// <summary>
        /// Returns new tree with all points deleted which are inside.
        /// </summary>
        public static IPointCloudNode Delete(this IPointCloudNode root,
            Func<IPointCloudNode, bool> isNodeFullyInside,
            Func<IPointCloudNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            Storage storage, CancellationToken ct,
            int splitLimit
            )
        {
            if (root == null) return null;

            var f = root as FilteredNode;
            if (f != null)
            {
                var filter = f.Filter as ISpatialFilter;
                if (filter != null)
                {
                    bool remove(IPointCloudNode n) => filter.IsFullyInside(n) && isNodeFullyInside(n);
                    bool keep(IPointCloudNode n) => filter.IsFullyOutside(n) || isNodeFullyOutside(n);
                    bool contains(V3d pt) => filter.Contains(pt) && isPositionInside(pt);
                    var res = f.Node.Delete(remove, keep, contains, storage, ct, splitLimit);
                    return FilteredNode.Create(res, f.Filter);
                }
                else
                {
                    throw new NotImplementedException("cannot delete on PointCloud with non-spatial filter");
                }
            }


            if (isNodeFullyInside(root)) return null;
            if (isNodeFullyOutside(root))
            {
                if (!root.IsMaterialized)
                {
                    root = root.Materialize();
                }
                return root;
            }

            if(root.IsLeaf)
            {
                var ps = root.HasPositions ? new List<V3f>() : null;
                var cs = root.HasColors ? new List<C4b>() : null;
                var ns = root.HasNormals ? new List<V3f>() : null;
                var js = root.HasIntensities ? new List<int>() : null;
                var ks = root.HasClassifications ? new List<byte>() : null;
                var vs = root.HasVelocities ? new List<V3f>() : null;
                var oldPs = root.Positions?.Value;
                var oldCs = root.Colors?.Value;
                var oldNs = root.Normals?.Value;
                var oldIs = root.Intensities?.Value;
                var oldKs = root.Classifications?.Value;
                var oldVs = root.Velocities?.Value;
                var bbabs = Box3d.Invalid;
                var bbloc = Box3f.Invalid;

                for (var i = 0; i < oldPs.Length; i++)
                {
                    var pabs = (V3d)oldPs[i] + root.Center;
                    if (!isPositionInside(pabs))
                    {
                        if (oldPs != null) ps.Add(oldPs[i]);
                        if (oldCs != null) cs.Add(oldCs[i]);
                        if (oldNs != null) ns.Add(oldNs[i]);
                        if (oldIs != null) js.Add(oldIs[i]);
                        if (oldKs != null) ks.Add(oldKs[i]);
                        if (oldVs != null) vs.Add(oldVs[i]);
                        bbabs.ExtendBy(pabs);
                        bbloc.ExtendBy(oldPs[i]);
                    }
                }

                if (ps.Count == 0) return null;

                var psa = ps.ToArray();
                var newId = Guid.NewGuid();
                var kd = psa.Length < 1 ? null : psa.BuildKdTree();
                
                Guid psId = Guid.NewGuid();
                Guid kdId = kd != null ? Guid.NewGuid() : Guid.Empty;
                Guid csId = cs != null ? Guid.NewGuid() : Guid.Empty;
                Guid nsId = ns != null ? Guid.NewGuid() : Guid.Empty;
                Guid isId = js != null ? Guid.NewGuid() : Guid.Empty;
                Guid ksId = ks != null ? Guid.NewGuid() : Guid.Empty;
                Guid vsId = vs != null ? Guid.NewGuid() : Guid.Empty;

                storage.Add(psId, psa);

                var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(Durable.Octree.NodeId, newId)
                    .Add(Durable.Octree.Cell, root.Cell)
                    .Add(Durable.Octree.BoundingBoxExactGlobal, bbabs)
                    .Add(Durable.Octree.BoundingBoxExactLocal, bbloc)
                    .Add(Durable.Octree.PositionsLocal3fReference, psId)
                    .Add(Durable.Octree.PointCountCell, ps.Count)
                    .Add(Durable.Octree.PointCountTreeLeafs, (long)ps.Count)
                    .Add(Durable.Octree.MaxTreeDepth, 0)
                    .Add(Durable.Octree.MinTreeDepth, 0)
                    ;


                if (kd != null)
                {
                    storage.Add(kdId, kd.Data);
                    data = data.Add(Durable.Octree.PointRkdTreeFDataReference, kdId);
                }
                if (cs != null)
                {
                    storage.Add(csId, cs.ToArray());
                    data = data.Add(Durable.Octree.Colors4bReference, csId);
                }
                if (ns != null)
                {
                    storage.Add(nsId, ns.ToArray());
                    data = data.Add(Durable.Octree.Normals3fReference, nsId);
                }
                if (js != null)
                {
                    storage.Add(isId, js.ToArray());
                    data = data.Add(Durable.Octree.Intensities1iReference, isId);
                }
                if (ks != null)
                {
                    storage.Add(ksId, ks.ToArray());
                    data = data.Add(Durable.Octree.Classifications1bReference, ksId);
                }
                if (vs != null)
                {
                    storage.Add(vsId, vs.ToArray());
                    data = data.Add(Durable.Octree.Velocities3fReference, ksId);
                }

                // MinTreeDepth MaxTreeDepth SubNodeIds??
                return new PointSetNode(data, storage, writeToStore: true);
            }
            else
            {
                var subnodes = root.Subnodes.Map((r) => r?.Value?.Delete(isNodeFullyInside, isNodeFullyOutside, isPositionInside, storage, ct, splitLimit));
                var pointCountTree = subnodes.Sum((n) => n != null ? n.PointCountTree : 0);
                if (pointCountTree == 0)
                {
                    return null;
                }
                else if (pointCountTree <= splitLimit)
                {
                    var psabs = root.HasPositions ? new List<V3d>() : null;
                    var cs = root.HasColors ? new List<C4b>() : null;
                    var ns = root.HasNormals ? new List<V3f>() : null;
                    var js = root.HasIntensities ? new List<int>() : null;
                    var ks = root.HasClassifications ? new List<byte>() : null;
                    var vs = root.HasVelocities ? new List<V3f>() : null;
                    foreach (var c in subnodes)
                    {
                        if (c != null) MergeExtensions.CollectEverything(c, psabs, cs, ns, js, ks, vs);
                    }
                    Debug.Assert(psabs.Count == pointCountTree);
                    var psa = psabs.MapToArray((p) => (V3f)(p - root.Center));
                    var kd = psa.Length < 1 ? null : psa.BuildKdTree();


                    Guid psId = Guid.NewGuid();
                    Guid kdId = kd != null ? Guid.NewGuid() : Guid.Empty;
                    Guid csId = cs != null ? Guid.NewGuid() : Guid.Empty;
                    Guid nsId = ns != null ? Guid.NewGuid() : Guid.Empty;
                    Guid isId = js != null ? Guid.NewGuid() : Guid.Empty;
                    Guid ksId = ks != null ? Guid.NewGuid() : Guid.Empty;
                    Guid vsId = ks != null ? Guid.NewGuid() : Guid.Empty;

                    var bbabs = new Box3d(psabs);

                    var newId = Guid.NewGuid();
                    storage.Add(psId, psa);

                    var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(Durable.Octree.NodeId, newId)
                    .Add(Durable.Octree.Cell, root.Cell)
                    .Add(Durable.Octree.BoundingBoxExactGlobal, bbabs)
                    .Add(Durable.Octree.BoundingBoxExactLocal, (Box3f)(bbabs - root.Center))
                    .Add(Durable.Octree.PositionsLocal3fReference, psId)
                    .Add(Durable.Octree.PointCountCell, (int)pointCountTree)
                    .Add(Durable.Octree.PointCountTreeLeafs, pointCountTree)
                    .Add(Durable.Octree.MaxTreeDepth, 0)
                    .Add(Durable.Octree.MinTreeDepth, 0)
                    ;
                    if (kd != null)
                    {
                        storage.Add(kdId, kd.Data);
                        data = data.Add(Durable.Octree.PointRkdTreeFDataReference, kdId);
                    }
                    if (cs != null)
                    {
                        storage.Add(csId, cs.ToArray());
                        data = data.Add(Durable.Octree.Colors4bReference, csId);
                    }
                    if (ns != null)
                    {
                        storage.Add(nsId, ns.ToArray());
                        data = data.Add(Durable.Octree.Normals3fReference, nsId);
                    }
                    if (js != null)
                    {
                        storage.Add(isId, js.ToArray());
                        data = data.Add(Durable.Octree.Intensities1iReference, isId);
                    }
                    if (ks != null)
                    {
                        storage.Add(ksId, ks.ToArray());
                        data = data.Add(Durable.Octree.Classifications1bReference, ksId);
                    }
                    if (vs != null)
                    {
                        storage.Add(vsId, vs.ToArray());
                        data = data.Add(Durable.Octree.Classifications1bReference, vsId);
                    }

                    // MinTreeDepth MaxTreeDepth SubNodeIds??
                    return new PointSetNode(data, storage, writeToStore: true);
                }
                else
                {
                    var bbabs = new Box3d(subnodes.Map(n => n != null ? n.BoundingBoxExactGlobal : Box3d.Invalid));
                    var subids = subnodes.Map(n => n != null ? n.Id : Guid.Empty);

                    var maxDepth = subnodes.Max(n => n != null ? n.MaxTreeDepth + 1 : 0);
                    var minDepth = subnodes.Min(n => n != null ? n.MinTreeDepth + 1 : 0);


                    var octreeSplitLimit = splitLimit;
                    var fractions = LodExtensions.ComputeLodFractions(subnodes);
                    var counts = LodExtensions.ComputeLodCounts(octreeSplitLimit, fractions);

                    // generate LoD data ...
                    var needsCs = subnodes.Any(x => x != null ? x.HasColors : false);
                    var needsNs = subnodes.Any(x => x != null ? x.HasNormals : false);
                    var needsIs = subnodes.Any(x => x != null ? x.HasIntensities : false);
                    var needsKs = subnodes.Any(x => x != null ? x.HasClassifications : false);
                    var needsVs = subnodes.Any(x => x != null ? x.HasVelocities : false);

                    var subcenters = subnodes.Map(x => x?.Center);
                    var lodPs = LodExtensions.AggregateSubPositions(counts, octreeSplitLimit, root.Center, subcenters, subnodes.Map(x => x?.Positions?.Value));
                    var lodCs = needsCs ? LodExtensions.AggregateSubArrays(counts, octreeSplitLimit, subnodes.Map(x => x?.Colors?.Value)) : null;
                    var lodIs = needsIs ? LodExtensions.AggregateSubArrays(counts, octreeSplitLimit, subnodes.Map(x => x?.Intensities?.Value)) : null;
                    var lodKs = needsKs ? LodExtensions.AggregateSubArrays(counts, octreeSplitLimit, subnodes.Map(x => x?.Classifications?.Value)) : null;
                    var lodVs = needsVs ? LodExtensions.AggregateSubArrays(counts, octreeSplitLimit, subnodes.Map(x => x?.Velocities?.Value)) : null;
                    var lodNs = needsNs ? LodExtensions.AggregateSubArrays(counts, octreeSplitLimit, subnodes.Map(x => x?.Normals?.Value)) : null;
                    var lodKd = lodPs.Length < 1 ? null : lodPs.BuildKdTree();


                    Guid psId = Guid.NewGuid();
                    Guid kdId = lodKd != null ? Guid.NewGuid() : Guid.Empty;
                    Guid csId = lodCs != null ? Guid.NewGuid() : Guid.Empty;
                    Guid nsId = lodNs != null ? Guid.NewGuid() : Guid.Empty;
                    Guid isId = lodIs != null ? Guid.NewGuid() : Guid.Empty;
                    Guid ksId = lodKs != null ? Guid.NewGuid() : Guid.Empty;
                    Guid vsId = lodVs != null ? Guid.NewGuid() : Guid.Empty;


                    var newId = Guid.NewGuid();
                    storage.Add(psId, lodPs);

                    var bbloc = new Box3f(lodPs);

                    // be inner node
                    var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(Durable.Octree.SubnodesGuids, subids)
                    .Add(Durable.Octree.NodeId, newId)
                    .Add(Durable.Octree.Cell, root.Cell)
                    .Add(Durable.Octree.BoundingBoxExactGlobal, bbabs)
                    .Add(Durable.Octree.BoundingBoxExactLocal, bbloc)
                    .Add(Durable.Octree.PositionsLocal3fReference, psId)
                    .Add(Durable.Octree.PointCountCell, lodPs.Length)
                    .Add(Durable.Octree.PointCountTreeLeafs, pointCountTree)
                    .Add(Durable.Octree.MaxTreeDepth, maxDepth)
                    .Add(Durable.Octree.MinTreeDepth, minDepth)
                    ;


                    if(lodKd != null)
                    {
                        storage.Add(kdId, lodKd.Data);
                        data = data.Add(Durable.Octree.PointRkdTreeFDataReference, kdId);
                    }
                    if (lodCs != null)
                    {
                        storage.Add(csId, lodCs);
                        data = data.Add(Durable.Octree.Colors4bReference, csId);
                    }
                    if (lodNs != null)
                    {
                        storage.Add(nsId, lodNs);
                        data = data.Add(Durable.Octree.Normals3fReference, nsId);
                    }
                    if (lodIs != null)
                    {
                        storage.Add(isId, lodIs);
                        data = data.Add(Durable.Octree.Intensities1iReference, isId);
                    }
                    if (lodKs != null)
                    {
                        storage.Add(ksId, lodKs);
                        data = data.Add(Durable.Octree.Classifications1bReference, ksId);
                    }
                    if (lodVs != null)
                    {
                        storage.Add(vsId, lodVs);
                        data = data.Add(Durable.Octree.Velocities3fReference, ksId);
                    }

                    // MinTreeDepth MaxTreeDepth SubNodeIds??
                    return new PointSetNode(data, storage, writeToStore: true);
                }
            }
        }
    }
}
