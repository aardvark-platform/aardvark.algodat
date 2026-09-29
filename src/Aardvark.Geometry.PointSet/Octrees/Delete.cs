/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points;

public record struct PointDeleteAttributes(
    byte? Classification,
    int? PartIndex
);

/// <summary>
/// Immutable point deletion with cooperative cancellation. Checks surround lazy reads,
/// predicates, traversal, aggregation and persistence; an in-flight callback, storage
/// operation or synchronous helper must finish before cancellation can be observed.
/// Completed writes are not rolled back, and the original point set is not modified.
/// </summary>
public static class DeleteExtensions
{
    /// <summary>
    /// Returns new pointset without points specified as inside.
    /// Returns null, if no points are left or the input is null (even when cancelled).
    /// Node payloads use storage; the PointSet descriptor uses pointSet.Storage.
    /// Cancellation is cooperative and does not roll back completed writes.
    /// </summary>
    /// <exception cref="OperationCanceledException">Cancellation was observed; carries ct.</exception>
    public static PointSet? Delete(this PointSet? pointSet,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        Func<V3d, PointDeleteAttributes, bool> isPositionInside,
        Storage storage, CancellationToken ct
        )
    {
        if (pointSet == null) return null;
        ct.ThrowIfCancellationRequested();

        var root = Delete(Read(pointSet.Root, ct), isNodeFullyInside, isNodeFullyOutside, isPositionInside, storage, ct, pointSet.SplitLimit);
        ct.ThrowIfCancellationRequested();
        if (root == null) return null;

        var newId = Guid.NewGuid().ToString();
        var result = new PointSet(pointSet.Storage, newId, root.Id, pointSet.SplitLimit);
        ct.ThrowIfCancellationRequested();
        pointSet.Storage.Add(newId, result);
        ct.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>
    /// Deletes matching positions with cooperative cancellation; null input returns null.
    /// Completed writes are not rolled back. Storage routing is unchanged from the attribute overload.
    /// </summary>
    /// <exception cref="OperationCanceledException">Cancellation was observed; carries ct.</exception>
    public static PointSet? Delete(this PointSet? pointSet,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        Storage storage, CancellationToken ct
        )
    {
        if (pointSet == null) return null;
        ct.ThrowIfCancellationRequested();
        return pointSet.Delete(
            isNodeFullyInside,
            isNodeFullyOutside,
            (p, _) => isPositionInside(p),
            storage,
            ct
        );
    }

    /// <summary>
    /// Returns new octree with all points deleted which are inside.
    /// Returns null, if no points are left or the input is null (even when cancelled).
    /// Cancellation is cooperative and does not roll back completed immutable writes.
    /// </summary>
    /// <exception cref="OperationCanceledException">Cancellation was observed; carries ct.</exception>
    public static IPointCloudNode? Delete(this IPointCloudNode? root,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        Func<V3d, PointDeleteAttributes, bool> isPositionInside,
        Storage storage, CancellationToken ct,
        int splitLimit
        )
    {
        if (root == null) return null;
        ct.ThrowIfCancellationRequested();

        if (root is FilteredNode f)
        {
            if (f.Filter is ISpatialFilter filter)
            {
                bool remove(IPointCloudNode n)
                {
                    var inside = filter.IsFullyInside(n);
                    ct.ThrowIfCancellationRequested();
                    return inside && isNodeFullyInside(n);
                }
                bool keep(IPointCloudNode n)
                {
                    var outside = filter.IsFullyOutside(n);
                    ct.ThrowIfCancellationRequested();
                    return outside || isNodeFullyOutside(n);
                }
                bool contains(V3d pt, PointDeleteAttributes att)
                {
                    var inside = filter.Contains(pt);
                    ct.ThrowIfCancellationRequested();
                    return inside && isPositionInside(pt, att);
                }
                var res = f.Node.Delete(remove, keep, contains, storage, ct, splitLimit);
                ct.ThrowIfCancellationRequested();
                if (res == null) return null;
                var result = FilteredNode.CreateTransient(res, f.Filter);
                ct.ThrowIfCancellationRequested();
                result.WriteToStore();
                ct.ThrowIfCancellationRequested();
                return result;
            }
            else
            {
                throw new Exception("Delete is not supported on PointCloud with non-spatial filter. Error 1885c46f-2eef-4dfb-807b-439c1b9c673d.");
            }
        }


        var removeNode = isNodeFullyInside(root);
        ct.ThrowIfCancellationRequested();
        if (removeNode) return null;
        var keepNode = isNodeFullyOutside(root);
        ct.ThrowIfCancellationRequested();
        if (keepNode)
        {
            if (!root.IsMaterialized)
            {
                ct.ThrowIfCancellationRequested();
                root = root.Materialize();
            }
            ct.ThrowIfCancellationRequested();
            return root;
        }

        if (root.IsLeaf)
        {
            var ps = new List<V3f>();
            var cs = root.HasColors ? new List<C4b>() : null;
            var ns = root.HasNormals ? new List<V3f>() : null;
            var js = root.HasIntensities ? new List<int>() : null;
            var ks = root.HasClassifications ? new List<byte>() : null;
            var piis = root.HasPartIndices ? new List<int>() : null; //partIndex array indices
            var oldPs = Read(root.Positions, ct)!;
            var oldCs = Read(root.Colors, ct);
            var oldNs = Read(root.Normals, ct);
            var oldIs = Read(root.Intensities, ct);
            var oldKs = Read(root.Classifications, ct);
            var bbabs = Box3d.Invalid;
            var bbloc = Box3f.Invalid;
            var center = root.Center;

            for (var i = 0; i < oldPs.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var pabs = (V3d)oldPs[i] + center;
                byte? oldK = oldKs?[i];
                int? oldPi = piis != null ? PartIndexUtils.Get(root.PartIndices, i) : null;
                var atts = new PointDeleteAttributes(oldK, oldPi);
                ct.ThrowIfCancellationRequested();
                var remove = isPositionInside(pabs, atts);
                ct.ThrowIfCancellationRequested();
                if (!remove)
                {
                    ps.Add(oldPs[i]);
                    if (oldCs != null) cs!.Add(oldCs[i]);
                    if (oldNs != null) ns!.Add(oldNs[i]);
                    if (oldIs != null) js!.Add(oldIs[i]);
                    if (oldKs != null) ks!.Add(oldKs[i]);
                    if (piis != null) piis!.Add(i);
                    bbabs.ExtendBy(pabs);
                    bbloc.ExtendBy(oldPs[i]);
                }
            }

            ct.ThrowIfCancellationRequested();
            if (ps.Count == 0) return null;

            var pis = (piis != null) ? PartIndexUtils.Subset(root.PartIndices, piis) : null;
            ct.ThrowIfCancellationRequested();
            

            var psa = ps.ToArray();
            var newId = Guid.NewGuid();
            ct.ThrowIfCancellationRequested();
            var kd = psa.Length < 1 ? null : psa.BuildKdTree();
            ct.ThrowIfCancellationRequested();
            
            Guid psId = Guid.NewGuid();
            Guid kdId = kd != null ? Guid.NewGuid() : Guid.Empty;
            Guid csId = cs != null ? Guid.NewGuid() : Guid.Empty;
            Guid nsId = ns != null ? Guid.NewGuid() : Guid.Empty;
            Guid isId = js != null ? Guid.NewGuid() : Guid.Empty;
            Guid ksId = ks != null ? Guid.NewGuid() : Guid.Empty;
            Guid pisId = pis != null ? Guid.NewGuid() : Guid.Empty;

            ct.ThrowIfCancellationRequested();
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
                ct.ThrowIfCancellationRequested();
                storage.Add(kdId, kd.Data);
                data = data.Add(Durable.Octree.PointRkdTreeFDataReference, kdId);
            }
            if (cs != null)
            {
                ct.ThrowIfCancellationRequested();
                storage.Add(csId, cs.ToArray());
                data = data.Add(Durable.Octree.Colors4bReference, csId);
            }
            if (ns != null)
            {
                ct.ThrowIfCancellationRequested();
                storage.Add(nsId, ns.ToArray());
                data = data.Add(Durable.Octree.Normals3fReference, nsId);
            }
            if (js != null)
            {
                ct.ThrowIfCancellationRequested();
                storage.Add(isId, js.ToArray());
                data = data.Add(Durable.Octree.Intensities1iReference, isId);
            }
            if (ks != null)
            {
                ct.ThrowIfCancellationRequested();
                storage.Add(ksId, ks.ToArray());
                data = data.Add(Durable.Octree.Classifications1bReference, ksId);
            }
            if (pis != null)
            {
                var piRange = PartIndexUtils.GetRange(pis);
                data = data.Add(Durable.Octree.PartIndexRange, piRange!);

                if (pis is Array xs)
                {
                    ct.ThrowIfCancellationRequested();
                    storage.Add(pisId, xs);
                    var def = xs switch
                    {
                        byte[] => Durable.Octree.PerPointPartIndex1bReference,
                        short[] => Durable.Octree.PerPointPartIndex1sReference,
                        int[] => Durable.Octree.PerPointPartIndex1iReference,
                        _ => throw new Exception("[Delete] Unknown type. Invariant 95811F1A-5FFF-4EED-8C31-8C267CAB85A6.")
                    };
                    data = data.Add(def, pisId);
                }
                else
                {
                    data = data
                        .Add(PartIndexUtils.GetDurableDefForPartIndices(pis), pis)
                        ;
                }
            }

            return Persist(data, storage, ct);
        }
        else
        {
            var refs = root.Subnodes;
            var subnodes = new IPointCloudNode?[refs.Length];
            for (var i = 0; i < refs.Length; i++)
                subnodes[i] = Read(refs[i], ct)?.Delete(isNodeFullyInside, isNodeFullyOutside, isPositionInside, storage, ct, splitLimit);
            ct.ThrowIfCancellationRequested();

            var pointCountTree = subnodes.Sum((n) => n != null ? n.PointCountTree : 0);
            if (pointCountTree == 0)
            {
                return null;
            }
            else if (pointCountTree <= splitLimit)
            {
                var psabs = new List<V3d>();
                var cs = root.HasColors ? new List<C4b>() : null;
                var ns = root.HasNormals ? new List<V3f>() : null;
                var js = root.HasIntensities ? new List<int>() : null;
                var ks = root.HasClassifications ? new List<byte>() : null;
                var pis = (object?)null;
                foreach (var c in subnodes)
                {
                    ct.ThrowIfCancellationRequested();
                    if (c != null) MergeExtensions.CollectEverything(c, psabs, cs, ns, js, ks, ref pis, ct);
                }
                ct.ThrowIfCancellationRequested();
                Debug.Assert(psabs.Count == pointCountTree);
                var psa = psabs.MapToArray((p) => (V3f)(p - root.Center));
                ct.ThrowIfCancellationRequested();
                var kd = psa.Length < 1 ? null : psa.BuildKdTree();
                ct.ThrowIfCancellationRequested();


                Guid psId = Guid.NewGuid();
                Guid kdId = kd != null ? Guid.NewGuid() : Guid.Empty;
                Guid csId = cs != null ? Guid.NewGuid() : Guid.Empty;
                Guid nsId = ns != null ? Guid.NewGuid() : Guid.Empty;
                Guid isId = js != null ? Guid.NewGuid() : Guid.Empty;
                Guid ksId = ks != null ? Guid.NewGuid() : Guid.Empty;
                Guid pisId = pis != null ? Guid.NewGuid() : Guid.Empty;

                var bbabs = new Box3d(psabs);

                var newId = Guid.NewGuid();
                ct.ThrowIfCancellationRequested();
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
                    ct.ThrowIfCancellationRequested();
                    storage.Add(kdId, kd.Data);
                    data = data.Add(Durable.Octree.PointRkdTreeFDataReference, kdId);
                }
                if (cs != null)
                {
                    ct.ThrowIfCancellationRequested();
                    storage.Add(csId, cs.ToArray());
                    data = data.Add(Durable.Octree.Colors4bReference, csId);
                }
                if (ns != null)
                {
                    ct.ThrowIfCancellationRequested();
                    storage.Add(nsId, ns.ToArray());
                    data = data.Add(Durable.Octree.Normals3fReference, nsId);
                }
                if (js != null)
                {
                    ct.ThrowIfCancellationRequested();
                    storage.Add(isId, js.ToArray());
                    data = data.Add(Durable.Octree.Intensities1iReference, isId);
                }
                if (ks != null)
                {
                    ct.ThrowIfCancellationRequested();
                    storage.Add(ksId, ks.ToArray());
                    data = data.Add(Durable.Octree.Classifications1bReference, ksId);
                }
                ct.ThrowIfCancellationRequested();
                if (pis != null)
                {
                    var piRange = PartIndexUtils.GetRange(pis);
                    data = data.Add(Durable.Octree.PartIndexRange, piRange!);

                    if (pis is Array xs)
                    {
                        ct.ThrowIfCancellationRequested();
                        storage.Add(pisId, xs);
                        var def = xs switch
                        {
                            byte[] => Durable.Octree.PerPointPartIndex1bReference,
                            short[] => Durable.Octree.PerPointPartIndex1sReference,
                            int[] => Durable.Octree.PerPointPartIndex1iReference,
                            _ => throw new Exception("[Delete] Unknown type. Invariant 97DF0E9E-EE9C-4BC7-9D0C-0C0F262122B0.")
                        };
                        data = data.Add(def, pisId);
                    }
                    else
                    {
                        data = data
                            .Add(PartIndexUtils.GetDurableDefForPartIndices(pis), pis)
                            ;
                    }
                }

                return Persist(data, storage, ct);
            }
            else
            {
                var bbabs = new Box3d(subnodes.Map(n => n != null ? n.BoundingBoxExactGlobal : Box3d.Invalid));
                var subids = subnodes.Map(n => n != null ? n.Id : Guid.Empty);

                var maxDepth = subnodes.Max(n => n != null ? n.MaxTreeDepth + 1 : 0);
                var minDepth = subnodes.Min(n => n != null ? n.MinTreeDepth + 1 : 0);


                var octreeSplitLimit = splitLimit;
                var fractions = LodExtensions.ComputeLodFractions(subnodes);
                var aggregateCount = Math.Min(octreeSplitLimit, subnodes.Sum(x => x?.PointCountCell) ?? 0);
                var counts = LodExtensions.ComputeLodCounts(aggregateCount, fractions);

                // generate LoD data ...
                var needsCs = subnodes.Any(x => x != null && x.HasColors);
                var needsNs = subnodes.Any(x => x != null && x.HasNormals);
                var needsIs = subnodes.Any(x => x != null && x.HasIntensities);
                var needsKs = subnodes.Any(x => x != null && x.HasClassifications);
                var needsPis = subnodes.Any(x => x != null && x.HasPartIndices);

                var subcenters = subnodes.Map(x => x?.Center);

                ct.ThrowIfCancellationRequested();
                var lodPs = LodExtensions.AggregateSubPositions(counts, aggregateCount, root.Center, subcenters, ReadSubnodeAttributes(subnodes, x => x?.Positions?.Value, ct));
                var lodCs = needsCs ? LodExtensions.AggregateSubArrays(counts, aggregateCount, ReadSubnodeAttributes(subnodes, x => x?.Colors?.Value, ct)) : null;
                var lodNs = needsNs ? LodExtensions.AggregateSubArrays(counts, aggregateCount, ReadSubnodeAttributes(subnodes, x => x?.Normals?.Value, ct)) : null;
                var lodIs = needsIs ? LodExtensions.AggregateSubArrays(counts, aggregateCount, ReadSubnodeAttributes(subnodes, x => x?.Intensities?.Value, ct)) : null;
                var lodKs = needsKs ? LodExtensions.AggregateSubArrays(counts, aggregateCount, ReadSubnodeAttributes(subnodes, x => x?.Classifications?.Value, ct)) : null;
                ct.ThrowIfCancellationRequested();
                var lodKd = lodPs.Length < 1 ? null : lodPs.BuildKdTree();
                var (lodPis,lodPiRange) = needsPis ? LodExtensions.AggregateSubPartIndices(counts, aggregateCount, ReadSubnodeAttributes(subnodes, x => x?.PartIndices, ct)) : (null,null);
                ct.ThrowIfCancellationRequested();

                Guid psId = Guid.NewGuid();
                Guid kdId = lodKd != null ? Guid.NewGuid() : Guid.Empty;
                Guid csId = lodCs != null ? Guid.NewGuid() : Guid.Empty;
                Guid nsId = lodNs != null ? Guid.NewGuid() : Guid.Empty;
                Guid isId = lodIs != null ? Guid.NewGuid() : Guid.Empty;
                Guid ksId = lodKs != null ? Guid.NewGuid() : Guid.Empty;
                Guid pisId = lodPis != null ? Guid.NewGuid() : Guid.Empty;


                var newId = Guid.NewGuid();
                ct.ThrowIfCancellationRequested();
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


                if (lodKd != null)
                {
                    ct.ThrowIfCancellationRequested();
                    storage.Add(kdId, lodKd.Data);
                    data = data.Add(Durable.Octree.PointRkdTreeFDataReference, kdId);
                }
                if (lodCs != null)
                {
                    ct.ThrowIfCancellationRequested();
                    storage.Add(csId, lodCs);
                    data = data.Add(Durable.Octree.Colors4bReference, csId);
                }
                if (lodNs != null)
                {
                    ct.ThrowIfCancellationRequested();
                    storage.Add(nsId, lodNs);
                    data = data.Add(Durable.Octree.Normals3fReference, nsId);
                }
                if (lodIs != null)
                {
                    ct.ThrowIfCancellationRequested();
                    storage.Add(isId, lodIs);
                    data = data.Add(Durable.Octree.Intensities1iReference, isId);
                }
                if (lodKs != null)
                {
                    ct.ThrowIfCancellationRequested();
                    storage.Add(ksId, lodKs);
                    data = data.Add(Durable.Octree.Classifications1bReference, ksId);
                }
                ct.ThrowIfCancellationRequested();
                if (lodPis != null)
                {
                    data = data.Add(Durable.Octree.PartIndexRange, lodPiRange!);

                    if (lodPis is Array xs)
                    {
                        ct.ThrowIfCancellationRequested();
                        storage.Add(pisId, xs);
                        var def = xs switch
                        {
                            byte[] => Durable.Octree.PerPointPartIndex1bReference,
                            short[] => Durable.Octree.PerPointPartIndex1sReference,
                            int[] => Durable.Octree.PerPointPartIndex1iReference,
                            _ => throw new Exception("[Delete] Unknown type. Invariant CC407F87-5969-4989-9161-B809FDA46840.")
                        };
                        data = data.Add(def, pisId);
                    }
                    else
                    {
                        data = data
                            .Add(PartIndexUtils.GetDurableDefForPartIndices(lodPis), lodPis)
                            ;
                    }
                }

                return Persist(data, storage, ct);
            }
        } // if (root.IsLeaf)
    } // Delete
    /// <summary>
    /// Deletes matching positions with cooperative cancellation; null input returns null.
    /// Completed immutable writes are not rolled back.
    /// </summary>
    /// <exception cref="OperationCanceledException">Cancellation was observed; carries ct.</exception>
    public static IPointCloudNode? Delete(this IPointCloudNode? root,
        Func<IPointCloudNode, bool> isNodeFullyInside,
        Func<IPointCloudNode, bool> isNodeFullyOutside,
        Func<V3d, bool> isPositionInside,
        Storage storage, CancellationToken ct,
        int splitLimit
        )
    {
        if (root == null) return null;
        ct.ThrowIfCancellationRequested();
        return root.Delete(
            isNodeFullyInside,
            isNodeFullyOutside,
            (p, _) => isPositionInside(p),
            storage,
            ct,
            splitLimit
        );
    } // Delete

    private static T? Read<T>(PersistentRef<T>? reference, CancellationToken ct) where T : class
    {
        ct.ThrowIfCancellationRequested();
        var value = reference?.Value;
        ct.ThrowIfCancellationRequested();
        return value;
    }

    private static T[] ReadSubnodeAttributes<T>(IPointCloudNode?[] nodes, Func<IPointCloudNode?, T> read, CancellationToken ct)
    {
        var result = new T[nodes.Length];
        for (var i = 0; i < nodes.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            result[i] = read(nodes[i]);
        }
        ct.ThrowIfCancellationRequested();
        return result;
    }

    private static PointSetNode Persist(ImmutableDictionary<Durable.Def, object> data, Storage storage, CancellationToken ct)
    {
        // Construction and validation can load payloads; check again before publishing the node.
        ct.ThrowIfCancellationRequested();
        var result = new PointSetNode(data, storage, writeToStore: false);
        ct.ThrowIfCancellationRequested();
        result.CheckDerivedAttributes();
        ct.ThrowIfCancellationRequested();
        storage.Add(result.Id.ToString(), result);
        ct.ThrowIfCancellationRequested();
        return result;
    }

} // DeleteExtensions
// namespace
