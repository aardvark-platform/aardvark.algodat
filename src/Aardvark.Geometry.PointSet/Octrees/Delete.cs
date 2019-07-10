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
            var root = Delete(node.Root.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, storage, ct);
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
            Storage storage, CancellationToken ct
            )
        {
            //Report.Error($"[Delete] {root.GetType().Name}.Delete({root.Id})");
            if (root == null) return null;
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
                var oldPs = root.Positions?.Value;
                var oldCs = root.Colors?.Value;
                var oldNs = root.Normals?.Value;
                var oldIs = root.Intensities?.Value;
                var oldKs = root.Classifications?.Value;
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
                        bbabs.ExtendBy(pabs);
                        bbloc.ExtendBy(oldPs[i]);
                    }
                }

                if (ps.Count == 0) return null;

                Guid psId = Guid.NewGuid();
                Guid kdId = Guid.NewGuid();
                Guid csId = cs != null ? Guid.NewGuid() : Guid.Empty;
                Guid nsId = ns != null ? Guid.NewGuid() : Guid.Empty;
                Guid isId = js != null ? Guid.NewGuid() : Guid.Empty;
                Guid ksId = ks != null ? Guid.NewGuid() : Guid.Empty;

                var psa = ps.ToArray();
                var newId = Guid.NewGuid();
                
                storage.Add(kdId, psa.BuildKdTree().Data);
                storage.Add(psId, psa);

                var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(Durable.Octree.NodeId, newId)
                    .Add(Durable.Octree.Cell, root.Cell)
                    .Add(Durable.Octree.BoundingBoxExactGlobal, bbabs)
                    .Add(Durable.Octree.BoundingBoxExactLocal, bbloc)
                    .Add(Durable.Octree.PositionsLocal3fReference, psId)
                    .Add(Durable.Octree.PointRkdTreeFDataReference, kdId)
                    .Add(Durable.Octree.PointCountCell, ps.Count)
                    .Add(Durable.Octree.PointCountTreeLeafs, (long)ps.Count)
                    .Add(Durable.Octree.MaxTreeDepth, 0)
                    .Add(Durable.Octree.MinTreeDepth, 0)
                    ;


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

                // MinTreeDepth MaxTreeDepth SubNodeIds??
                return new PointSetNode(data, storage, writeToStore: true);
            }
            else
            {
                var subnodes = root.Subnodes.Map((r) => r?.Value?.Delete(isNodeFullyInside, isNodeFullyOutside, isPositionInside, storage, ct));
                var pointCountTree = subnodes.Sum((n) => n != null ? n.PointCountTree : 0);
                if (pointCountTree == 0)
                {
                    return null;
                }
                else if (pointCountTree < 8192)
                {
                    var psabs = root.HasPositions ? new List<V3d>() : null;
                    var cs = root.HasColors ? new List<C4b>() : null;
                    var ns = root.HasNormals ? new List<V3f>() : null;
                    var js = root.HasIntensities ? new List<int>() : null;
                    var ks = root.HasClassifications ? new List<byte>() : null;

                    foreach(var c in subnodes)
                    {
                        if (c != null) MergeExtensions.CollectEverything(c, psabs, cs, ns, js, ks);
                    }
                    Debug.Assert(psabs.Count == pointCountTree);
                    var psa = psabs.MapToArray((p) => (V3f)(p - root.Center));

                    Guid psId = Guid.NewGuid();
                    Guid kdId = Guid.NewGuid();
                    Guid csId = cs != null ? Guid.NewGuid() : Guid.Empty;
                    Guid nsId = ns != null ? Guid.NewGuid() : Guid.Empty;
                    Guid isId = js != null ? Guid.NewGuid() : Guid.Empty;
                    Guid ksId = ks != null ? Guid.NewGuid() : Guid.Empty;

                    var bbabs = new Box3d(psabs);

                    var newId = Guid.NewGuid();
                    storage.Add(kdId, psa.BuildKdTree().Data);
                    storage.Add(psId, psa);

                    var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(Durable.Octree.NodeId, newId)
                    .Add(Durable.Octree.Cell, root.Cell)
                    .Add(Durable.Octree.BoundingBoxExactGlobal, bbabs)
                    .Add(Durable.Octree.BoundingBoxExactLocal, (Box3f)(bbabs - root.Center))
                    .Add(Durable.Octree.PositionsLocal3fReference, psId)
                    .Add(Durable.Octree.PointRkdTreeFDataReference, kdId)
                    .Add(Durable.Octree.PointCountCell, (int)pointCountTree)
                    .Add(Durable.Octree.PointCountTreeLeafs, pointCountTree)
                    .Add(Durable.Octree.MaxTreeDepth, 0)
                    .Add(Durable.Octree.MinTreeDepth, 0)
                    ;
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

                    // MinTreeDepth MaxTreeDepth SubNodeIds??
                    return new PointSetNode(data, storage, writeToStore: true);
                }
                else
                {
                    var bbabs = new Box3d(subnodes.Map(n => n != null ? n.BoundingBoxExactGlobal : Box3d.Invalid));
                    var subids = subnodes.Map(n => n != null ? n.Id : Guid.Empty);

                    var maxDepth = subnodes.Max(n => n != null ? n.MaxTreeDepth + 1 : 0);
                    var minDepth = subnodes.Min(n => n != null ? n.MinTreeDepth + 1 : 0);


                    var octreeSplitLimit = 8192;
                    var fractions = LodExtensions.ComputeLodFractions(subnodes);
                    var counts = LodExtensions.ComputeLodCounts(octreeSplitLimit, fractions);

                    // generate LoD data ...
                    var needsCs = subnodes.Any(x => x != null ? x.HasColors : false);
                    var needsNs = subnodes.Any(x => x != null ? x.HasNormals : false);
                    var needsIs = subnodes.Any(x => x != null ? x.HasIntensities : false);
                    var needsKs = subnodes.Any(x => x != null ? x.HasClassifications : false);

                    var subcenters = subnodes.Map(x => x?.Center);
                    var lodPs = LodExtensions.AggregateSubPositions(counts, octreeSplitLimit, root.Center, subcenters, subnodes.Map(x => x?.Positions?.Value));
                    var lodCs = needsCs ? LodExtensions.AggregateSubArrays(counts, octreeSplitLimit, subnodes.Map(x => x?.Colors?.Value)) : null;
                    var lodIs = needsIs ? LodExtensions.AggregateSubArrays(counts, octreeSplitLimit, subnodes.Map(x => x?.Intensities?.Value)) : null;
                    var lodKs = needsKs ? LodExtensions.AggregateSubArrays(counts, octreeSplitLimit, subnodes.Map(x => x?.Classifications?.Value)) : null;
                    var lodNs = needsNs ? LodExtensions.AggregateSubArrays(counts, octreeSplitLimit, subnodes.Map(x => x?.Normals?.Value)) : null;
                    var lodKd = lodPs.BuildKdTree();


                    Guid psId = Guid.NewGuid();
                    Guid kdId = Guid.NewGuid();
                    Guid csId = lodCs != null ? Guid.NewGuid() : Guid.Empty;
                    Guid nsId = lodNs != null ? Guid.NewGuid() : Guid.Empty;
                    Guid isId = lodIs != null ? Guid.NewGuid() : Guid.Empty;
                    Guid ksId = lodKs != null ? Guid.NewGuid() : Guid.Empty;

                    
                    var newId = Guid.NewGuid();
                    storage.Add(kdId, lodKd.Data);
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
                    .Add(Durable.Octree.PointRkdTreeFDataReference, kdId)
                    .Add(Durable.Octree.PointCountCell, lodPs.Length)
                    .Add(Durable.Octree.PointCountTreeLeafs, pointCountTree)
                    .Add(Durable.Octree.MaxTreeDepth, maxDepth)
                    .Add(Durable.Octree.MinTreeDepth, minDepth)
                    ;
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

                    // MinTreeDepth MaxTreeDepth SubNodeIds??
                    return new PointSetNode(data, storage, writeToStore: true);
                }
            }


            Guid? newPsId = null;
            Guid? newCsId = null;
            Guid? newNsId = null;
            Guid? newIsId = null;
            Guid? newKsId = null;
            Guid? newKdId = null;

            try // A
            {
                var psAbsolute = root.HasPositions ? new List<V3d>() : null;
                var ps = root.HasPositions ? new List<V3f>() : null;
                var cs = root.HasColors ? new List<C4b>() : null;
                var ns = root.HasNormals ? new List<V3f>() : null;
                var js = root.HasIntensities ? new List<int>() : null;
                var ks = root.HasClassifications ? new List<byte>() : null;
                var oldPsAbsolute = root.PositionsAbsolute;
                var oldPs = root.Positions?.Value;
                var oldCs = root.Colors?.Value;
                var oldNs = root.Normals?.Value;
                var oldIs = root.Intensities?.Value;
                var oldKs = root.Classifications?.Value;

                try // B
                {
                    for (var i = 0; i < oldPsAbsolute.Length; i++)
                    {
                        if (!isPositionInside(oldPsAbsolute[i]))
                        {
                            if (oldPsAbsolute != null) psAbsolute.Add(oldPsAbsolute[i]);
                            if (oldPs != null) ps.Add(oldPs[i]);
                            if (oldCs != null) cs.Add(oldCs[i]);
                            if (oldNs != null) ns.Add(oldNs[i]);
                            if (oldIs != null) js.Add(oldIs[i]);
                            if (oldKs != null) ks.Add(oldKs[i]);
                        }
                    }

                    try //C
                    {
                        var newId = Guid.NewGuid();
                        //Report.Error($"[Delete] create {newId}");
                        var data = ImmutableDictionary<Durable.Def, object>.Empty
                            .Add(Durable.Octree.NodeId, newId)
                            .Add(Durable.Octree.Cell, root.Cell)
                            ;

                        if (root.HasPositions)
                        {
                            newPsId = Guid.NewGuid();
                            var psa = ps.ToArray();
                            storage.Add(newPsId.Value, psa);

                            newKdId = Guid.NewGuid();
                            storage.Add(newKdId.Value, psa.Length != 0 ? psa.BuildKdTree().Data : new PointRkdTreeFData());

                            data = data
                                .Add(Durable.Octree.PositionsLocal3fReference, newPsId)
                                .Add(Durable.Octree.PointRkdTreeFDataReference, newKdId)
                                ;
                        }

                        if (root.HasColors)
                        {
                            newCsId = Guid.NewGuid();
                            storage.Add(newCsId.Value, cs.ToArray());

                            data = data.Add(Durable.Octree.Colors4bReference, newCsId);
                        }

                        if (root.HasNormals)
                        {
                            newNsId = Guid.NewGuid();
                            storage.Add(newNsId.Value, ns.ToArray());

                            data = data.Add(Durable.Octree.Normals3fReference, newNsId);
                        }

                        if (root.HasIntensities)
                        {
                            newIsId = Guid.NewGuid();
                            storage.Add(newIsId.Value, js.ToArray());

                            data = data.Add(Durable.Octree.Intensities1iReference, newIsId);
                        }

                        if (root.HasClassifications)
                        {
                            newKsId = Guid.NewGuid();
                            storage.Add(newKsId.Value, ks.ToArray());

                            data = data.Add(Durable.Octree.Classifications1bReference, newKsId);
                        }
                        try
                        { //D

                            var newSubnodes = root.Subnodes?.Map(n => n?.Value.Delete(isNodeFullyInside, isNodeFullyOutside, isPositionInside, storage, ct));
                            if (newSubnodes != null && newSubnodes.All(n => n == null)) newSubnodes = null;
                            if (ps.Count == 0 && newSubnodes == null) return null;

                            try
                            {  //E
                                var pointCountTreeLeafs = newSubnodes != null ? (newSubnodes.Sum(n => n != null ? n.PointCountTree : 0)) : ps.Count;
                                data = data.Add(Durable.Octree.PointCountTreeLeafs, pointCountTreeLeafs);

                                try
                                {  //F
                                    var bbExactGlobal =
                            newSubnodes == null
                            ? new Box3d(psAbsolute)
                            : new Box3d(newSubnodes.Where(x => x != null).Select(x => x.BoundingBoxExactGlobal))
                            ;
                                    data = data.Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal);

                                    if (newSubnodes != null)
                                    {
                                        var newSubnodesIds = newSubnodes.Map(x => x?.Id ?? Guid.Empty);
                                        data = data.Add(Durable.Octree.SubnodesGuids, newSubnodesIds);
                                    }

                                    var result = new PointSetNode(data, storage, writeToStore: true);
#if DEBUG
                                    if (result.Id != newId) throw new InvalidOperationException("Invariant 0c351a17-c4bb-40fc-94ba-04fc6a26ca7e.");
                                    if (storage.GetPointCloudNode(newId) == null) throw new InvalidOperationException("Invariant a5ae64fa-4b60-40a5-88a7-15adc038d6bb.");
                                    if (newSubnodes != null)
                                    {
                                        foreach (var id in result.SubnodeIds)
                                            if (id.HasValue && id != Guid.Empty && storage.GetPointCloudNode(id.Value) == null) throw new InvalidOperationException("Invariant ef9f1b2c-91c4-4471-9f5e-f00a71f84033.");
                                    }
#endif
                                    return result;

                                }
                                catch { Report.Line("DELETE F"); throw; }
                            }
                            catch { Report.Line("DELETE E"); throw; }
                        }
                        catch { Report.Line("DELETE D"); throw; }
                    }
                    catch { Report.Line("DELETE C"); throw; }
                }
                catch { Report.Line("DELETE B"); throw; }
            }
            catch { Report.Line("DELETE A"); throw; }
        }
    }
}
