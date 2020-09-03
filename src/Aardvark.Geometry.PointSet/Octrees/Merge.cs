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
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class MergeExtensions
    {
        
        internal static int CollectEverything(IPointCloudNode self, List<V3d> ps, List<C4b> cs, List<V3f> ns, List<int> js, List<byte> ks, List<V3f> vs)
        {
            if (self == null) return 0;
            else if(self.IsLeaf)
            {
                if (self.HasPositions && ps != null)
                {
                    var off = self.Center;
                    ps.AddRange(self.Positions.Value.Map(p => off + (V3d)p));
                }

                if (self.HasColors && cs != null) cs.AddRange(self.Colors.Value);
                if (self.HasNormals && ns != null) ns.AddRange(self.Normals.Value);
                if (self.HasIntensities && js != null) js.AddRange(self.Intensities.Value);
                if (self.HasClassifications && ks != null) ks.AddRange(self.Classifications.Value);
                if (self.HasVelocities && vs != null) vs.AddRange(self.Velocities.Value);
                return 1;
            }
            else
            {
                var leafs = 0;
                foreach (var x in self.Subnodes)
                {
                    if (x != null)
                    {
                        leafs += CollectEverything(x.Value, ps, cs, ns, js, ks, vs);
                    }
                }
                return leafs;
            }
        }

        public static (IPointCloudNode,bool) CollapseLeafNodes(this IPointCloudNode self, ImportConfig config)
        {
            if (!self.IsTemporaryImportNode) throw new InvalidOperationException(
                "CollapseLeafNodes is only valid for temporary import nodes. Invariant 4aa0809d-4cb0-422b-97ee-fa5b6dc4785e."
                );

            if (self.PointCountTree == 0)
            {
                return (null, true);
            }
            else if (self.PointCountTree <= config.OctreeSplitLimit)
            {
                if (self.IsLeaf) return (self.WriteToStore(), true);

                var psla = new List<V3d>();
                var csla = new List<C4b>();
                var nsla = new List<V3f>();
                var jsla = new List<int>();
                var ksla = new List<byte>();
                var vsla = new List<V3f>();
                var leaves = CollectEverything(self, psla, csla, nsla, jsla, ksla, vsla);
                
                if (leaves <= 1)
                {
                    return (self.WriteToStore(), true);
                }
                else
                {
                    var chunk = new Chunk(
                        psla.Count > 0 ? psla : null,
                        csla.Count > 0 ? csla : null,
                        nsla.Count > 0 ? nsla : null,
                        jsla.Count > 0 ? jsla : null,
                        ksla.Count > 0 ? ksla : null,
                        vsla.Count > 0 ? vsla : null
                        );
                    if (config.NormalizePointDensityGlobal)
                    {
                        chunk = chunk.ImmutableFilterMinDistByCell(self.Cell, config.ParseConfig);
                    }

                    var node = InMemoryPointSet.Build(chunk, config.OctreeSplitLimit);
                    var psnode = node.ToPointSetNode(self.Storage, isTemporaryImportNode: true);
                    if (self.Cell != psnode.Cell)
                    {
                        return (JoinTreeToRootCell(self.Cell, psnode, config, collapse: false), true);
                    }
                    else
                    {
                        return (psnode, true);
                    }
                }
            }
            else
            {
                return (self.WriteToStore(), true);
            }
        }
        
        /// <summary>
        /// If cell is a leaf, it will be split once (non-recursive, without taking into account any split limit).
        /// If cell is not a leaf, this is an invalid operation.
        /// </summary>
        public static IPointCloudNode ForceSplitLeaf(this IPointCloudNode self, ImportConfig config)
        {
            if (!self.IsTemporaryImportNode) throw new InvalidOperationException(
                "ForceSplitLeaf is only valid for temporary import nodes. Invariant 3bfca971-be98-45b7-86e7-de436b78cefb."
                );

            if (self == null) throw new ArgumentNullException(nameof(self));
            if (self.IsLeaf == false) throw new InvalidOperationException();
            if (self.PointCountCell == 0) throw new InvalidOperationException();
            if (self.PointCountTree != self.PointCountCell) throw new InvalidOperationException();

            var subnodesPoints = new List<V3d>[8];
            var subnodesColors = self.HasColors ? new List<C4b>[8] : null;
            var subnodesNormals = self.HasNormals ? new List<V3f>[8] : null;
            var subnodesIntensities = self.HasIntensities ? new List<int>[8] : null;
            var subnodesClassifications = self.HasClassifications ? new List<byte>[8] : null;
            var subnodesVelocities = self.HasVelocities ? new List<V3f>[8] : null;

            var pa = self.PositionsAbsolute;
            var ca = self.Colors?.Value;
            var na = self.Normals?.Value;
            var ia = self.Intensities?.Value;
            var ka = self.Classifications?.Value;
            var va = self.Velocities?.Value;
            var imax = self.PointCountCell;
            if (pa.Length != imax) throw new InvalidOperationException();

            for (var i = 0; i < imax; i++)
            {
                var si = self.GetSubIndex(pa[i]);
                if (subnodesPoints[si] == null)
                {
                    subnodesPoints[si] = new List<V3d>();
                    if (subnodesColors != null) subnodesColors[si] = new List<C4b>();
                    if (subnodesNormals != null) subnodesNormals[si] = new List<V3f>();
                    if (subnodesIntensities != null) subnodesIntensities[si] = new List<int>();
                    if (subnodesClassifications != null) subnodesClassifications[si] = new List<byte>();
                    if (subnodesVelocities != null) subnodesVelocities[si] = new List<V3f>();
                }
                subnodesPoints[si].Add(pa[i]);
                if (subnodesColors != null) subnodesColors[si].Add(ca[i]);
                if (subnodesNormals != null) subnodesNormals[si].Add(na[i]);
                if (subnodesIntensities != null) subnodesIntensities[si].Add(ia[i]);
                if (subnodesClassifications != null) subnodesClassifications[si].Add(ka[i]);
                if (subnodesVelocities != null) subnodesVelocities[si].Add(va[i]);
            }

            var subnodes = new PointSetNode[8];
            for (var i = 0; i < 8; i++)
            {
                if (subnodesPoints[i] == null) continue;

                var subCell = self.Cell.GetOctant(i);
                if (!self.Cell.Contains(subCell)) throw new InvalidOperationException();
                if (self.Cell.Exponent != subCell.Exponent + 1) throw new InvalidOperationException();

                var chunk = new Chunk(
                    subnodesPoints[i],
                    subnodesColors?[i],
                    subnodesNormals?[i],
                    subnodesIntensities?[i],
                    subnodesClassifications?[i],
                    subnodesVelocities?[i],
                    subCell.BoundingBox
                    );
                if (config.NormalizePointDensityGlobal)
                {
                    chunk = chunk.ImmutableFilterMinDistByCell(subCell, config.ParseConfig);
                }
                var builder = InMemoryPointSet.Build(
                    subnodesPoints[i],
                    subnodesColors?[i],
                    subnodesNormals?[i],
                    subnodesIntensities?[i],
                    subnodesClassifications?[i],
                    subnodesVelocities?[i],
                    subCell,
                    int.MaxValue
                    );
                var subnode = builder.ToPointSetNode(config.Storage, isTemporaryImportNode: true);
                if (subnode.PointCountTree > subnodesPoints[i].Count) throw new InvalidOperationException();
                if (!self.Cell.Contains(subnode.Cell)) throw new InvalidOperationException();
                if (self.Cell.Exponent != subnode.Cell.Exponent + 1) throw new InvalidOperationException();
                
                subnodes[i] = subnode;
            }

            var bbExactGlobal = new Box3d(subnodes.Where(x => x != null).Select(x => x.BoundingBoxExactGlobal));

            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(PointSetNode.TemporaryImportNode, 0)
                .Add(Durable.Octree.NodeId, Guid.NewGuid())
                .Add(Durable.Octree.Cell, self.Cell)
                .Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal)
                .Add(Durable.Octree.PointCountTreeLeafs, self.PointCountTree)
                .Add(Durable.Octree.SubnodesGuids, subnodes.Map(x => x?.Id ?? Guid.Empty))
                ;
            var result = new PointSetNode(data, self.Storage, true);

            // POST
            if (result.IsLeaf) throw new InvalidOperationException();
            if (result.PointCountTree != self.PointCountTree) throw new InvalidOperationException();
            if (result.PointCountCell != 0) throw new InvalidOperationException();
            if (result.Subnodes.Sum(x => x?.Value?.PointCountTree) > self.PointCountTree) throw new InvalidOperationException();

            return result;
        }

        /// <summary>
        /// Returns union of trees as new tree (immutable operation).
        /// </summary>
        public static (IPointCloudNode, bool) Merge(this IPointCloudNode a, IPointCloudNode b, Action<long> pointsMergedCallback, ImportConfig config)
        {
            if (!a.IsTemporaryImportNode || !b.IsTemporaryImportNode) throw new InvalidOperationException(
                "Merge is only allowed on temporary import nodes. Invariant d53042e7-a032-47a9-98dc-034c0749a649."
                );

            if (a == null || a.PointCountTree == 0) { pointsMergedCallback?.Invoke(b?.PointCountTree ?? 0); return (b,true); }
            if (b == null || b.PointCountTree == 0) { pointsMergedCallback?.Invoke(a?.PointCountTree ?? 0); return (a,true); }

#if DEBUG && NEVERMORE
            if (config.Verbose) Report.Line($"[Merge] a = {a.Cell}, b = {b.Cell}");
#endif

            //// [4862, 28518, 0, 4], b = [4862, 28518, 0, 4]
            //if (a.Cell == new Cell(4862, 28518, 0, 4) && b.Cell == new Cell(4862, 28518, 0, 4)) Debugger.Break();


            if (a.PointCountTree + b.PointCountTree <= config.OctreeSplitLimit)
            {
                var psla = new List<V3d>();
                var csla = new List<C4b>();
                var nsla = new List<V3f>();
                var jsla = new List<int>();
                var ksla = new List<byte>();
                var vsla = new List<V3f>();

                CollectEverything(a, psla, csla, nsla, jsla, ksla, vsla);
                CollectEverything(b, psla, csla, nsla, jsla, ksla, vsla);

                var cell = ParentCell(a.Cell, b.Cell);
                var chunk = new Chunk(
                    psla.Count > 0 ? psla : null,
                    csla.Count > 0 ? csla : null,
                    nsla.Count > 0 ? nsla : null,
                    jsla.Count > 0 ? jsla : null,
                    ksla.Count > 0 ? ksla : null,
                    vsla.Count > 0 ? vsla : null
                    );
                if(config.NormalizePointDensityGlobal)
                {
                    chunk = chunk.ImmutableFilterMinDistByCell(cell, config.ParseConfig);
                }

                var storage = config.Storage;
                var ac = a.Center;
                var bc = b.Center;

                var psAbs = chunk.Positions?.ToArray();
                var ns = chunk.Normals?.ToArray();
                var cs = chunk.Colors?.ToArray();
                var js = chunk.Intensities?.ToArray();
                var ks = chunk.Classifications?.ToArray();
                var vs = chunk.Velocities?.ToArray();
                if (psAbs == null || psAbs.Length == 0) return (null,true);

                var bbExactGlobal = chunk.BoundingBox;

                Guid? psId = psAbs != null ? Guid.NewGuid() : (Guid?)null;
                //Guid? kdId = psAbs != null ? Guid.NewGuid() : (Guid?)null;
                Guid? nsId = ns != null ? Guid.NewGuid() : (Guid?)null;
                Guid? csId = cs != null ? Guid.NewGuid() : (Guid?)null;
                Guid? jsId = js != null ? Guid.NewGuid() : (Guid?)null;
                Guid? ksId = ks != null ? Guid.NewGuid() : (Guid?)null;
                Guid? vsId = vs != null ? Guid.NewGuid() : (Guid?)null;


                var center = cell.BoundingBox.Center;

                var ps = psAbs.Map(p => (V3f)(p - center));

                var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(PointSetNode.TemporaryImportNode, 0)
                    .Add(Durable.Octree.NodeId, Guid.NewGuid())
                    .Add(Durable.Octree.Cell, cell)
                    .Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal)
                    .Add(Durable.Octree.PointCountTreeLeafs, ps.LongLength)
                    ;

                if (psId.HasValue) { storage.Add(psId.Value, ps); data = data.Add(Durable.Octree.PositionsLocal3fReference, psId.Value); }
                if (nsId.HasValue) { storage.Add(nsId.Value, ns); data = data.Add(Durable.Octree.Normals3fReference, nsId.Value); }
                if (csId.HasValue) { storage.Add(csId.Value, cs); data = data.Add(Durable.Octree.Colors4bReference, csId.Value); }
                if (jsId.HasValue) { storage.Add(jsId.Value, js); data = data.Add(Durable.Octree.Intensities1iReference, jsId.Value); }
                if (ksId.HasValue) { storage.Add(ksId.Value, ks); data = data.Add(Durable.Octree.Classifications1bReference, ksId.Value); }
                if (vsId.HasValue) { storage.Add(vsId.Value, vs); data = data.Add(Durable.Octree.Velocities3fReference, vsId.Value); }
                //if (kdId.HasValue) { storage.Add(kdId.Value, kd.Data); data = data.Add(Durable.Octree.PointRkdTreeFDataReference, kdId.Value); }

                return (new PointSetNode(data, config.Storage, writeToStore: true), true);
            }


            // if A and B have identical root cells, then merge ...
            if (a.Cell == b.Cell)
            {
                var result = a.IsLeaf
                    ? (b.IsLeaf ? MergeLeafAndLeafWithIdenticalRootCell(a, b, config)
                                : MergeLeafAndTreeWithIdenticalRootCell(a, b, config))
                    : (b.IsLeaf ? MergeLeafAndTreeWithIdenticalRootCell(b, a, config)
                                : MergeTreeAndTreeWithIdenticalRootCell(a, b, pointsMergedCallback, config))
                    ;
                pointsMergedCallback?.Invoke(a.PointCountTree + b.PointCountTree);
                //if (result.PointCountTree != totalPointCountTree) throw new InvalidOperationException("Invariant c758d38b-3669-4a23-a1a8-e6c58ce9d4ca.");
                return result.CollapseLeafNodes(config);
            }

            // if A and B do not intersect ...
            if (!a.Cell.Intersects(b.Cell))
            {
                var rootCell = new Cell(new Box3d(a.BoundingBoxExactGlobal, b.BoundingBoxExactGlobal));
                var result = JoinNonOverlappingTrees(rootCell, a, b, pointsMergedCallback, config);
//#if DEBUG
//                if (!config.NormalizePointDensityGlobal && result.PointCountTree != totalPointCountTree) throw new InvalidOperationException();
//#endif
                pointsMergedCallback?.Invoke(a.PointCountTree + b.PointCountTree);
                //if (result.PointCountTree != totalPointCountTree) throw new InvalidOperationException("Invariant b1fb510e-cf81-4ef1-a049-ea8b34a0be2e.");
                return result.CollapseLeafNodes(config);
            }

            if (a.Cell.IsCenteredAtOrigin || b.Cell.IsCenteredAtOrigin)
            {
                // enumerate all non-IsCenteredAtOrigin (sub)cells of A and B
                var parts = new List<IPointCloudNode>();
                if (a.Cell.IsCenteredAtOrigin)
                {
                    if (a.IsLeaf)
                    {
                        // split A into 8 subcells to get rid of centered cell
                        return Merge(a.ForceSplitLeaf(config), b, pointsMergedCallback, config);
                    }
                    else
                    {
                        parts.AddRange(a.Subnodes.Select(x => x?.Value));
                    }
                }
                else
                {
                    parts.Add(a);
                }

                if (b.Cell.IsCenteredAtOrigin)
                {
                    if (b.IsLeaf)
                    {
                        // split B into 8 subcells to get rid of centered cell
                        return Merge(a, b.ForceSplitLeaf(config), pointsMergedCallback, config);
                    }
                    else
                    {
                        parts.AddRange(b.Subnodes.Select(x => x?.Value));
                    }
                }
                else
                {
                    parts.Add(b);
                }

                // special case: there is only 1 part -> finished
                parts = parts.Where(x => x != null).ToList();
                if (parts.Count == 0) throw new InvalidOperationException();
                if (parts.Count == 1)
                {
                    var r = parts.Single();
                    pointsMergedCallback?.Invoke(r.PointCountTree);
                    //if (r.PointCountTree != totalPointCountTree) throw new InvalidOperationException("Invariant 636bc5aa-5489-4007-ac4d-3d6dd5e9ebae.");
                    return r.CollapseLeafNodes(config);
                }

                // common case: multiple parts
                var rootCellBounds = new Box3d(a.Cell.BoundingBox, b.Cell.BoundingBox);
                var rootCell = new Cell(rootCellBounds);
                var roots = new IPointCloudNode[8];

                static int octant(Cell x)
                {
                    if (x.IsCenteredAtOrigin) throw new InvalidOperationException();
                    return (x.X >= 0 ? 1 : 0) + (x.Y >= 0 ? 2 : 0) + (x.Z >= 0 ? 4 : 0);
                }

                foreach (var x in parts)
                {
                    var oi = octant(x.Cell);
                    var oct = rootCell.GetOctant(oi);
                    if (roots[oi] == null)
                    {
                        if (x.Cell != oct)
                        {
                            if (!oct.Contains(x.Cell)) throw new InvalidOperationException();
                            roots[oi] = JoinTreeToRootCell(oct, x, config);
                        }
                        else
                        {
                            roots[oi] = x;
                        }
                    }
                    else
                    {
                        roots[oi] = Merge(roots[oi], x, pointsMergedCallback, config).Item1;
                    }

                    if (oct != roots[oi].Cell) throw new InvalidOperationException();
                }

                var pointCountTreeLeafs = roots.Where(x => x != null).Sum(x => x.PointCountTree);
                var bbExactGlobal = new Box3d(roots.Where(x => x != null).Select(x => x.BoundingBoxExactGlobal));
                pointsMergedCallback?.Invoke(pointCountTreeLeafs);

                var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(PointSetNode.TemporaryImportNode, 0)
                    .Add(Durable.Octree.NodeId, Guid.NewGuid())
                    .Add(Durable.Octree.Cell, rootCell)
                    .Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal)
                    .Add(Durable.Octree.PointCountTreeLeafs, pointCountTreeLeafs)
                    .Add(Durable.Octree.SubnodesGuids, roots.Map(n => n?.Id ?? Guid.Empty))
                    ;
                var result = new PointSetNode(data, config.Storage, writeToStore: true);
                //if (result.PointCountTree != totalPointCountTree) throw new InvalidOperationException("Invariant a2a79e9b-f93a-46b6-955f-0c028b6cb87f.");
                return result.CollapseLeafNodes(config);
            }
#if DEBUG
            if (a.Cell.Exponent == b.Cell.Exponent)
            {
                if (!a.Cell.IsCenteredAtOrigin && !b.Cell.IsCenteredAtOrigin) throw new InvalidOperationException(
                    $"merge {a.Cell} with {b.Cell}")
                    ;
            }
#endif

            // ... otherwise ensure that A's root cell is bigger than B's to reduce number of cases to handle ...
            if (a.Cell.Exponent < b.Cell.Exponent)
            {
                var result = Merge(b, a, pointsMergedCallback, config);
//#if DEBUG
//                if (!config.NormalizePointDensityGlobal && result.PointCountTree != totalPointCountTree) throw new InvalidOperationException();
//#endif
                return result;
            }

            // ... B must now be contained in exactly one of A's subcells
#if DEBUG
            var isExactlyOne = false;
#endif
            var processedPointCount = 0L;
            var subcells = a.Subnodes?.Map(x => x?.Value) ?? new IPointCloudNode[8];
            for (var i = 0; i < 8; i++)
            {
                var subcellIndex = a.Cell.GetOctant(i);
                if (subcellIndex.Contains(b.Cell))
                {
#if DEBUG
                    if (isExactlyOne) throw new InvalidOperationException();
                    isExactlyOne = true;
#endif
                    if (subcells[i] == null)
                    {
                        subcells[i] = JoinTreeToRootCell(subcellIndex, b, config);
                    }
                    else
                    {
                        subcells[i] = Merge(subcells[i], b, 
                            n => pointsMergedCallback?.Invoke(processedPointCount + n),
                            config).Item1;
                    }

                    processedPointCount += subcells[i].PointCountTree;
                    pointsMergedCallback?.Invoke(processedPointCount);
                }
            }
#if DEBUG
            if (!isExactlyOne) throw new InvalidOperationException();
#endif
            IPointCloudNode result2 = null;
            if (a.IsLeaf)
            {
                result2 = a.WithSubNodes(subcells);
                result2 = InjectPointsIntoTree(
                    a.PositionsAbsolute, a.Colors?.Value, a.Normals?.Value, a.Intensities?.Value, a.Classifications?.Value, a.Velocities?.Value,
                    result2, result2.Cell, config);
            }
            else
            {
                result2 = a.WithSubNodes(subcells);
            }
#if DEBUG
            // this no longer holds due to removal of duplicate points
            //if (result2.PointCountTree != debugPointCountTree) throw new InvalidOperationException();
#endif
            pointsMergedCallback?.Invoke(result2.PointCountTree);
            //if (result2.PointCountTree != totalPointCountTree) throw new InvalidOperationException("Invariant a2a79e9b-f93a-46b6-955f-0c028b6cb87f.");
            return result2.CollapseLeafNodes(config);
        }
        
        private static T[] Concat<T>(T[] xs, T[] ys)
        {
            if (xs == null && ys == null) return null;
            if ((xs == null) != (ys == null)) throw new InvalidOperationException();
            var rs = new T[xs.Length + ys.Length];
            Array.Copy(xs, 0, rs, 0, xs.Length);
            Array.Copy(ys, 0, rs, xs.Length, ys.Length);
            return rs;
        }
        
        private static IPointCloudNode JoinNonOverlappingTrees(Cell rootCell, IPointCloudNode a, IPointCloudNode b,
            Action<long> pointsMergedCallback, ImportConfig config
            )
        {
#region Preconditions

            // PRE: ensure that trees 'a' and 'b' do not intersect,
            // because we are joining non-overlapping trees here
            if (a.Cell == b.Cell || a.Cell.Intersects(b.Cell)) throw new InvalidOperationException();

            // PRE: we further assume, that both trees are non-empty
            if (a.PointCountTree == 0 && b.PointCountTree == 0) throw new InvalidOperationException();

#endregion

#region Case reduction
            // REDUCE CASES:
            // if one tree ('a' or 'b') is centered at origin, then ensure that 'a' is centered
            // (by swapping 'a' and 'b' if necessary)
            if (b.Cell.IsCenteredAtOrigin)
            {
#if DEBUG
                // PRE: if 'b' is centered, than 'a' cannot be centered
                // (because then 'a' and 'b' would overlap, and we join non-overlapping trees here)
                if (a.Cell.IsCenteredAtOrigin) throw new InvalidOperationException();
#endif
                Fun.Swap(ref a, ref b);
#if DEBUG
                // POST: 'a' is centered, 'b' is not centered
                if (!a.Cell.IsCenteredAtOrigin) throw new InvalidOperationException();
                if (b.Cell.IsCenteredAtOrigin) throw new InvalidOperationException();
#endif
            }
#endregion
            
#region CASE 1 of 2: one tree is centered (must be 'a', since if it originally was 'b' we would have swapped)

            if (rootCell.IsCenteredAtOrigin && a.Cell.IsCenteredAtOrigin)
            {
#region special case: split 'a' into subcells to get rid of centered cell containing points
                if (a.IsLeaf)
                {
                    return JoinNonOverlappingTrees(rootCell, a.ForceSplitLeaf(config), b, pointsMergedCallback, config);
                }
#endregion
#if DEBUG
                if (a.PointCountCell != 0) throw new InvalidOperationException();
#endif

                var subcells = new IPointCloudNode[8];
                for (var i = 0; i < 8; i++)
                {
                    var rootCellOctant = rootCell.GetOctant(i);

                    var aSub = a.Subnodes[i]?.Value;
                    var bIsContained = rootCellOctant.Contains(b.Cell);
#if DEBUG
                    if (!bIsContained && rootCellOctant.Intersects(b.Cell)) throw new InvalidOperationException();
#endif

                    if (aSub != null)
                    {
                        if (bIsContained)
                        {
                            // CASE: both contained
                            var merged = Merge(aSub, b, pointsMergedCallback, config).Item1;
                            subcells[i] = JoinTreeToRootCell(rootCellOctant, merged, config);
                        }
                        else
                        {
                            // CASE: aSub contained
                            subcells[i] = JoinTreeToRootCell(rootCellOctant, aSub, config);
                        }
                    }
                    else
                    {
                        if (bIsContained)
                        {
                            // CASE: b contained
                            subcells[i] = JoinTreeToRootCell(rootCellOctant, b, config);
                        }
                        else
                        {
                            // CASE: none contained -> empty subcell
                            subcells[i] = null;
                        }
                    }
                }

                var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(PointSetNode.TemporaryImportNode, 0)
                    .Add(Durable.Octree.NodeId, Guid.NewGuid())
                    .Add(Durable.Octree.Cell, rootCell)
                    .Add(Durable.Octree.PointCountTreeLeafs, a.PointCountTree + b.PointCountTree)
                    .Add(Durable.Octree.SubnodesGuids, subcells.Map(x => x?.Id ?? Guid.Empty))
                    ;
                var result = new PointSetNode(data, config.Storage, writeToStore: false).CollapseLeafNodes(config).Item1;
#if DEBUG
                //if (result.PointCountTree != a.PointCountTree + b.PointCountTree) throw new InvalidOperationException();
                if (result.PointCountTree != result.Subnodes.Sum(x => x?.Value?.PointCountTree)) throw new InvalidOperationException();
#endif
                //pointsMergedCallback?.Invoke(result.PointCountTree);
                return result;
            }

#endregion

#region CASE 2 of 2: no tree is centered

            else
            {
#if DEBUG
                // PRE: no tree is centered
                if (a.Cell.IsCenteredAtOrigin) throw new InvalidOperationException();
                if (b.Cell.IsCenteredAtOrigin) throw new InvalidOperationException();
#endif

                var subcells = new IPointCloudNode[8];
                var doneA = false;
                var doneB = false;
                for (var i = 0; i < 8; i++)
                {
                    var subcell = rootCell.GetOctant(i);
                    if (subcell.Contains(a.Cell))
                    {
#if DEBUG
                        if (subcell.Intersects(b.Cell)) throw new InvalidOperationException();
#endif
                        subcells[i] = JoinTreeToRootCell(subcell, a, config);
                        if (doneB) break;
                        doneA = true;
                    }
                    if (subcell.Intersects(b.Cell))
                    {
#if DEBUG
                        if (subcell.Intersects(a.Cell)) throw new InvalidOperationException();
#endif
                        subcells[i] = JoinTreeToRootCell(subcell, b, config);
                        if (doneA == true) break;
                        doneB = true;
                    }
                }

                var pointCountTree = subcells.Sum(x => x?.PointCountTree);
                var bbExactGlobal = new Box3d(subcells.Where(x => x != null).Select(x => x.BoundingBoxExactGlobal));

                var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(PointSetNode.TemporaryImportNode, 0)
                    .Add(Durable.Octree.NodeId, Guid.NewGuid())
                    .Add(Durable.Octree.Cell, rootCell)
                    .Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal)
                    .Add(Durable.Octree.PointCountTreeLeafs, a.PointCountTree + b.PointCountTree)
                    .Add(Durable.Octree.SubnodesGuids, subcells.Map(x => x?.Id ?? Guid.Empty))
                    ;
                var result = new PointSetNode(data, config.Storage, writeToStore: false).CollapseLeafNodes(config).Item1;

#if DEBUG
                if (result.PointCountTree != a.PointCountTree + b.PointCountTree) throw new InvalidOperationException();
                if (result.PointCountTree != pointCountTree) throw new InvalidOperationException(
                    $"Invariant d2957ed7-d12c-461c-ae79-5181a4197654. {result.PointCountTree} != {pointCountTree}."
                    );
#endif
                //pointsMergedCallback?.Invoke(result.PointCountTree);
                return result;
            }

#endregion
        }

        private static Cell ParentCell(Cell a, Cell b)
        {
            return new Cell(Box.Union(a.BoundingBox, b.BoundingBox));
            //if (a == b) return a;
            //if (a.IsCenteredAtOrigin && b.IsCenteredAtOrigin)  return new Cell(Fun.Max(a.Exponent, b.Exponent));

            //var oa = new V3i(Math.Sign(a.X + 0.5), Math.Sign(a.Y + 0.5), Math.Sign(a.Z + 0.5));
            //var ob = new V3i(Math.Sign(b.X + 0.5), Math.Sign(b.Y + 0.5), Math.Sign(b.Z + 0.5));

            //if(oa == ob)
            //{
            //    if (b.Exponent < a.Exponent) return ParentCell(b.Parent, a);
            //    else if (a.Exponent < b.Exponent) return ParentCell(a.Parent, b);
            //    else return ParentCell(a.Parent, b.Parent);
            //}
            //else
            //{
            //    return new Cell()
            //}

        }

        internal static IPointCloudNode JoinTreeToRootCell(Cell rootCell, IPointCloudNode a, ImportConfig config, bool collapse = true)
        {
            if (!rootCell.Contains(a.Cell)) throw new InvalidOperationException();

            if (a.Cell.IsCenteredAtOrigin)
            {
                throw new InvalidOperationException();
            }
            if (rootCell == a.Cell) return a;

            var subcells = new IPointCloudNode[8];
            for (var i = 0; i < 8; i++)
            {
                var subcell = rootCell.GetOctant(i);
                if (subcell == a.Cell) { subcells[i] = a; break; }
                if (subcell.Contains(a.Cell)) { subcells[i] = JoinTreeToRootCell(subcell, a, config, collapse); break; }
            }

            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(PointSetNode.TemporaryImportNode, 0)
                .Add(Durable.Octree.NodeId, Guid.NewGuid())
                .Add(Durable.Octree.Cell, rootCell)
                .Add(Durable.Octree.BoundingBoxExactGlobal, a.BoundingBoxExactGlobal)
                .Add(Durable.Octree.PointCountTreeLeafs, a.PointCountTree)
                .Add(Durable.Octree.SubnodesGuids, subcells.Map(x => x?.Id ?? Guid.Empty))
                ;

            var result = (IPointCloudNode)new PointSetNode(data, config.Storage, writeToStore: false);
            if (collapse) result = result.CollapseLeafNodes(config).Item1;
            else result = result.WriteToStore();
            
//#if DEBUG
//            if (result.PointCountTree != a.PointCountTree) throw new InvalidOperationException("Invariant 13b94065-4eac-4602-bc65-677869178dac.");
//#endif
            return result;
        }

        private static IPointCloudNode MergeLeafAndLeafWithIdenticalRootCell(IPointCloudNode a, IPointCloudNode b, ImportConfig config)
        {
            if (!a.IsTemporaryImportNode || !b.IsTemporaryImportNode) throw new InvalidOperationException(
                "MergeLeafAndLeafWithIdenticalRootCell is only valid for temporary import nodes. Invariant 2d68b9d2-a001-47a8-b481-87488f33b85d."
                );

            if (a.IsLeaf == false || b.IsLeaf == false) throw new InvalidOperationException();
            if (a.Cell != b.Cell) throw new InvalidOperationException();
            if (b.PositionsAbsolute == null) throw new InvalidOperationException();
            if (a.HasColors != b.HasColors) throw new InvalidOperationException();
            if (a.HasNormals != b.HasNormals) throw new InvalidOperationException();
            if (a.HasIntensities != b.HasIntensities) throw new InvalidOperationException();
            if (a.HasClassifications != b.HasClassifications) throw new InvalidOperationException();
            if (a.HasVelocities != b.HasVelocities) throw new InvalidOperationException();

            var cell = a.Cell;

            var ps = Concat(a.PositionsAbsolute, b.PositionsAbsolute);
            var cs = Concat(a.Colors?.Value, b.Colors?.Value);
            var ns = Concat(a.Normals?.Value, b.Normals?.Value);
            var js = Concat(a.Intensities?.Value, b.Intensities?.Value);
            var ks = Concat(a.Classifications?.Value, b.Classifications?.Value);
            var vs = Concat(a.Velocities?.Value, b.Velocities?.Value);

            var chunk = new Chunk(ps, cs, ns, js, ks, vs, cell.BoundingBox);
            if (config.NormalizePointDensityGlobal)
            {
                chunk = chunk.ImmutableFilterMinDistByCell(cell, config.ParseConfig);
            }
            var result = InMemoryPointSet.Build(chunk, cell, config.OctreeSplitLimit).ToPointSetNode(config.Storage, isTemporaryImportNode: true);
            //if (a.PointCountTree + b.PointCountTree != result.PointCountTree) throw new InvalidOperationException("Invariant 369b10a2-f905-41c6-b016-1dbf8a68832d.");
            if (a.Cell != result.Cell) throw new InvalidOperationException("Invariant 771d781a-6d37-4017-a890-4f72a96a01a8.");
            return result;
        }

        private static IPointCloudNode MergeLeafAndTreeWithIdenticalRootCell(IPointCloudNode a, IPointCloudNode b, ImportConfig config)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.IsLeaf == false || b.IsLeaf == true) throw new InvalidOperationException();
            if (a.Cell != b.Cell) throw new InvalidOperationException();

            var result = InjectPointsIntoTree(a.PositionsAbsolute, a.Colors?.Value, a.Normals?.Value, a.Intensities?.Value, a.Classifications?.Value, a.Velocities?.Value, b, a.Cell, config);
            //if (a.PointCountTree + b.PointCountTree != result.PointCountTree) throw new InvalidOperationException("Invariant db336387-4d1a-42fd-a582-48e8cac50fba.");
            if (a.Cell != result.Cell) throw new InvalidOperationException("Invariant 55551919-1a11-4ea9-bb4e-6f1a6b15e3d5.");
            return result;
        }

        private static IPointCloudNode MergeTreeAndTreeWithIdenticalRootCell(IPointCloudNode a, IPointCloudNode b,
            Action<long> pointsMergedCallback,
            ImportConfig config
            )
        {
            if (a.IsLeaf || b.IsLeaf) throw new InvalidOperationException();
            if (a.Cell != b.Cell) throw new InvalidOperationException();
            if (a.PointCountCell > 0) throw new InvalidOperationException();
            if (b.PointCountCell > 0) throw new InvalidOperationException();

            var pointCountTree = 0L;
            var subcells = new IPointCloudNode[8];
            var subcellsDebug = new int[8];
            for (var i = 0; i < 8; i++)
            {
                var octant = a.Cell.GetOctant(i);
                var x = a.Subnodes[i]?.Value;
                var y = b.Subnodes[i]?.Value;

                if (a.Subnodes[i] != null && a.Subnodes[i]?.Value == null) throw new InvalidOperationException("Invariant 5571b3ac-a807-4318-9d07-d0843664b142.");
                if (b.Subnodes[i] != null && b.Subnodes[i]?.Value == null) throw new InvalidOperationException("Invariant 5eecb345-3460-4f9a-948c-efa29dea26b9.");

                if (x != null)
                {
                    if (y != null)
                    {
                        subcells[i] = Merge(x, y, pointsMergedCallback, config).Item1;
                        //if (x.PointCountTree + y.PointCountTree != subcells[i].PointCountTree) throw new InvalidOperationException("Invariant 82072553-7271-4448-b74d-735d44eb03b0.");
                        pointCountTree += subcells[i].PointCountTree;
                        subcellsDebug[i] = 0;
                    }
                    else
                    {
                        subcells[i] = x;
                        pointCountTree += x.PointCountTree;
                        //if (subcells[i].PointCountTree != x.PointCountTree) throw new InvalidOperationException();
                        subcellsDebug[i] = 1;
                    }
                }
                else
                {
                    if (y != null)
                    {
                        subcells[i] = y;
                        pointCountTree += y.PointCountTree;

                        //if (subcells[i].PointCountTree != y.PointCountTree) throw new InvalidOperationException();
                        subcellsDebug[i] = 2;
                    }
                    else
                    {
                        subcells[i] = null;
                        subcellsDebug[i] = 3;
                    }
                }
            }

            var result = a
                .WithUpsert(Durable.Octree.PointCountTreeLeafs, pointCountTree)
                .WithUpsert(Durable.Octree.SubnodesGuids, subcells.Map(x => x?.Id ?? Guid.Empty))
                .WithUpsert(Durable.Octree.BoundingBoxExactGlobal, new Box3d(subcells.Where(n => n != null).Select(n => n.BoundingBoxExactGlobal)))
                .CollapseLeafNodes(config).Item1
                ;

            //pointsMergedCallback?.Invoke(result.PointCountTree);
            //if (a.PointCountTree + b.PointCountTree != pointCountTree) throw new InvalidOperationException("Invariant 3db845c1-9d20-42b9-beb4-81684d47b1eb.");
            if (a.Cell != result.Cell) throw new InvalidOperationException("Invariant 97239777-8a0c-4158-853b-e9ebef63fda8.");
            return result;
        }

        private static IPointCloudNode InjectPointsIntoTree(
            IList<V3d> psAbsolute, IList<C4b> cs, IList<V3f> ns, IList<int> js, IList<byte> ks, IList<V3f> vs,
            IPointCloudNode a, Cell cell, ImportConfig config
            )
        {
            if (a != null && !a.IsTemporaryImportNode) throw new InvalidOperationException(
                "InjectPointsIntoTree is only valid for temporary import nodes. Invariant 0b0c48dc-8500-4ad6-a3dd-9c00f6d0b1d9."
                );

            if (a == null)
            {
                var chunk = new Chunk(psAbsolute, cs, ns, js, ks, vs, cell.BoundingBox);
                if (config.NormalizePointDensityGlobal)
                {
                    chunk = chunk.ImmutableFilterMinDistByCell(cell, config.ParseConfig);
                }

                var result0 = InMemoryPointSet.Build(chunk, cell, config.OctreeSplitLimit).ToPointSetNode(config.Storage, isTemporaryImportNode: true);
                //if (result0.PointCountTree != psAbsolute.Count) throw new InvalidOperationException("Invariant db6c1efb-32c3-4fc1-a9c8-a573442d593b.");
                if (result0.Cell != cell) throw new InvalidOperationException("Invariant 266f3ced-7aea-4efd-b4f0-1c3e04fafb08.");
                return result0;
            }

            if (a.Cell != cell) throw new InvalidOperationException("Invariant f447b6e5-52ef-4535-b8e4-e2aabedaef9e.");

            if (a.IsLeaf)
            {
                if (cs != null && !a.HasColors) throw new InvalidOperationException("Invariant 64d98ee7-5b08-4de7-9086-a38e707eb354.");
                if (cs == null && a.HasColors) throw new InvalidOperationException("Invariant 7c1cb6cb-16fe-40aa-83f4-61c0c2e50ec9.");
                if (ns != null && !a.HasNormals) throw new InvalidOperationException("Invariant 12263f36-1d5d-4c2b-aa1e-9f96f80047f2.");
                if (ns == null && a.HasNormals) throw new InvalidOperationException("Invariant 1e35a025-9a10-4bee-993b-109090c85b50.");

                var newPs = new List<V3d>(psAbsolute); newPs.AddRange(a.PositionsAbsolute);
                var newCs = cs != null ? new List<C4b>(cs) : null; newCs?.AddRange(a.Colors.Value);
                var newNs = ns != null ? new List<V3f>(ns) : null; newNs?.AddRange(a.Normals.Value);
                var newJs = js != null ? new List<int>(js) : null; newJs?.AddRange(a.Intensities.Value);
                var newKs = ks != null ? new List<byte>(ks) : null; newKs?.AddRange(a.Classifications.Value);
                var newVs = vs != null ? new List<V3f>(vs) : null; newVs?.AddRange(a.Velocities.Value);

                var chunk = new Chunk(newPs, newCs, newNs, newJs, newKs, newVs, cell.BoundingBox);

                if (config.NormalizePointDensityGlobal)
                {
                    chunk = chunk.ImmutableFilterMinDistByCell(cell, config.ParseConfig);
                }
                var result0 = InMemoryPointSet.Build(chunk, cell, config.OctreeSplitLimit).ToPointSetNode(config.Storage, isTemporaryImportNode: true);
                //if (a.PointCountTree + psAbsolute.Count != result0.PointCountTree) throw new InvalidOperationException("Invariant 9bda6d47-26d8-42e3-8d94-3db2d4436fec.");
                if (result0.Cell != cell) throw new InvalidOperationException("Invariant 2c11816c-da18-464e-9c2c-fad53301b41b.");
                return result0;
            }

            var pss = new List<V3d>[8];
            var css = cs != null ? new List<C4b>[8] : null;
            var nss = ns != null ? new List<V3f>[8] : null;
            var iss = js != null ? new List<int>[8] : null;
            var kss = ks != null ? new List<byte>[8] : null;
            var vss = vs != null ? new List<V3f>[8] : null;
            for (var i = 0; i < psAbsolute.Count; i++)
            {
                var j = a.GetSubIndex(psAbsolute[i]);
                if (pss[j] == null)
                {
                    pss[j] = new List<V3d>();
                    if (cs != null) css[j] = new List<C4b>();
                    if (ns != null) nss[j] = new List<V3f>();
                    if (js != null) iss[j] = new List<int>();
                    if (ks != null) kss[j] = new List<byte>();
                    if (vs != null) vss[j] = new List<V3f>();
                }
                pss[j].Add(psAbsolute[i]);
                if (cs != null) css[j].Add(cs[i]);
                if (ns != null) nss[j].Add(ns[i]);
                if (js != null) iss[j].Add(js[i]);
                if (ks != null) kss[j].Add(ks[i]);
                if (vs != null) vss[j].Add(vs[i]);
            }

            if (pss.Sum(x => x?.Count) != psAbsolute.Count) throw new InvalidOperationException();

            var subcells = new IPointCloudNode[8];
            for (var j = 0; j < 8; j++)
            {
                var x = a.Subnodes[j]?.Value;
                if (pss[j] != null)
                {
                    subcells[j] = InjectPointsIntoTree(pss[j], css?[j], nss?[j], iss?[j], kss?[j], vss?[j], x, cell.GetOctant(j), config);
                }
                else
                {
                    subcells[j] = x;
                }
            }

            var result = a.WithSubNodes(subcells).CollapseLeafNodes(config).Item1;
            //if (a.PointCountTree + psAbsolute.Count != result.PointCountTree) throw new InvalidOperationException("Invariant d6117e5b-3031-4ff7-9a38-d34695c5a869.");
            if (result.Cell != cell) throw new InvalidOperationException("Invariant 04aa0996-2942-41e5-bfdb-0c6841e2f12f.");
            return result;
        }
    }
}
