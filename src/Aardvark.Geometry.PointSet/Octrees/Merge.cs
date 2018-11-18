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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class MergeExtensions
    {
        /// <summary>
        /// If cell is a leaf, it will be split once (non-recursive, without taking into account any split limit).
        /// If cell is not a leaf, this is an invalid operation.
        /// </summary>
        public static PointSetNode ForceSplitLeaf(this PointSetNode cell, CancellationToken ct)
        {
            if (cell == null) throw new ArgumentNullException(nameof(cell));
            if (cell.IsNotLeaf) throw new InvalidOperationException();
            if (cell.PointCount == 0) throw new InvalidOperationException();
            if (cell.PointCountTree != cell.PointCount) throw new InvalidOperationException();

            var subnodesPoints = new List<V3d>[8];
            var subnodesColors = cell.HasColors ? new List<C4b>[8] : null;
            var subnodesNormals = cell.HasNormals ? new List<V3f>[8] : null;
            var subnodesIntensities = cell.HasIntensities ? new List<int>[8] : null;
            var subnodesClassifications = cell.HasClassifications ? new List<byte>[8] : null;

            var pa = cell.PositionsAbsolute;
            var ca = cell.Colors?.Value;
            var na = cell.Normals?.Value;
            var ia = cell.Intensities?.Value;
            var ka = cell.Classifications?.Value;
            var imax = cell.PointCount;
            if (pa.Length != imax) throw new InvalidOperationException();

            for (var i = 0; i < imax; i++)
            {
                var si = cell.GetSubIndex(pa[i]);
                if (subnodesPoints[si] == null)
                {
                    subnodesPoints[si] = new List<V3d>();
                    if (subnodesColors != null) subnodesColors[si] = new List<C4b>();
                    if (subnodesNormals != null) subnodesNormals[si] = new List<V3f>();
                    if (subnodesIntensities != null) subnodesIntensities[si] = new List<int>();
                    if (subnodesClassifications != null) subnodesClassifications[si] = new List<byte>();
                }
                subnodesPoints[si].Add(pa[i]);
                if (subnodesColors != null) subnodesColors[si].Add(ca[i]);
                if (subnodesNormals != null) subnodesNormals[si].Add(na[i]);
                if (subnodesIntensities != null) subnodesIntensities[si].Add(ia[i]);
                if (subnodesClassifications != null) subnodesClassifications[si].Add(ka[i]);
            }

            var subnodes = new PointSetNode[8];
            for (var i = 0; i < 8; i++)
            {
                if (subnodesPoints[i] == null) continue;

                var subCellIndex = cell.Cell.GetOctant(i);
                if (!cell.Cell.Contains(subCellIndex)) throw new InvalidOperationException();
                if (cell.Cell.Exponent != subCellIndex.Exponent + 1) throw new InvalidOperationException();

                var builder = InMemoryPointSet.Build(
                    subnodesPoints[i], subnodesColors?[i], subnodesNormals?[i], subnodesIntensities?[i], subnodesClassifications?[i],
                    subCellIndex, int.MaxValue
                    );
                var subnode = builder.ToPointSetNode(cell.Storage, ct: ct);
                if (subnode.PointCountTree > subnodesPoints[i].Count) throw new InvalidOperationException();
                if (!cell.Cell.Contains(subnode.Cell)) throw new InvalidOperationException();
                if (cell.Cell.Exponent != subnode.Cell.Exponent + 1) throw new InvalidOperationException();
                
                subnodes[i] = subnode;
            }

            var result = new PointSetNode(cell.Cell, imax, cell.BoundingBoxExactLocal, cell.AveragePointDistance, subnodes.Map(x => x?.Id), cell.Storage);

            // POST
            if (result.IsLeaf) throw new InvalidOperationException();
            if (result.PointCountTree != cell.PointCountTree) throw new InvalidOperationException();
            if (result.PointCount != 0) throw new InvalidOperationException();
            if (result.Subnodes.Sum(x => x?.Value?.PointCountTree) > cell.PointCountTree) throw new InvalidOperationException();

            return result;
        }

        /// <summary>
        /// Returns union of trees as new tree (immutable operation).
        /// </summary>
        public static PointSetNode Merge(this PointSetNode a, PointSetNode b, long octreeSplitLimit, CancellationToken ct)
        {
            if (a == null || a.PointCountTree == 0) return b;
            if (b == null || b.PointCountTree == 0) return a;

#if DEBUG
            var debugPointCountTree = a.PointCountTree + b.PointCountTree;
#endif

            // if A and B have identical root cells, then merge ...
            if (a.Cell == b.Cell)
            {
                var result = a.IsLeaf
                    ? (b.IsLeaf ? MergeLeafAndLeafWithIdenticalRootCell(a, b, octreeSplitLimit, ct)
                                : MergeLeafAndTreeWithIdenticalRootCell(a, b, octreeSplitLimit, ct))
                    : (b.IsLeaf ? MergeLeafAndTreeWithIdenticalRootCell(b, a, octreeSplitLimit, ct)
                                : MergeTreeAndTreeWithIdenticalRootCell(a, b, octreeSplitLimit, ct))
                    ;
                return result;
            }

            // if A and B do not intersect ...
            if (!a.Cell.Intersects(b.Cell))
            {
                var rootCell = new Cell(new Box3d(a.BoundingBox, b.BoundingBox));
                var result = JoinNonOverlappingTrees(rootCell, a, b, octreeSplitLimit, ct);
#if DEBUG
                if (result.PointCountTree != debugPointCountTree) throw new InvalidOperationException();
#endif
                return result;
            }

            if (a.IsCenteredAtOrigin || b.IsCenteredAtOrigin)
            {
                // enumerate all non-IsCenteredAtOrigin (sub)cells of A and B
                var parts = new List<PointSetNode>();
                if (a.IsCenteredAtOrigin)
                {
                    if (a.IsLeaf)
                    {
                        // split A into 8 subcells to get rid of centered cell
                        return Merge(a.ForceSplitLeaf(ct), b, octreeSplitLimit, ct);
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

                if (b.IsCenteredAtOrigin)
                {
                    if (b.IsLeaf)
                    {
                        // split B into 8 subcells to get rid of centered cell
                        return Merge(a, b.ForceSplitLeaf(ct), octreeSplitLimit, ct);
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
                if (parts.Count == 1) return parts.Single();

                // common case: multiple parts
                var rootCellBounds = new Box3d(a.Cell.BoundingBox, b.Cell.BoundingBox);
                var rootCell = new Cell(rootCellBounds);
                var roots = new PointSetNode[8];
                Func<Cell, int> octant = x =>
                {
                    if (x.IsCenteredAtOrigin) throw new InvalidOperationException();
                    return (x.X >= 0 ? 1 : 0) + (x.Y >= 0 ? 2 : 0) + (x.Z >= 0 ? 4 : 0);
                };
                foreach (var x in parts)
                {
                    var oi = octant(x.Cell);
                    var oct = rootCell.GetOctant(oi);
                    if (roots[oi] == null)
                    {
                        if (x.Cell != oct)
                        {
                            if (!oct.Contains(x.Cell)) throw new InvalidOperationException();
                            roots[oi] = JoinTreeToRootCell(oct, x);
                        }
                        else
                        {
                            roots[oi] = x;
                        }
                    }
                    else
                    {
                        roots[oi] = Merge(roots[oi], x, octreeSplitLimit, ct);
                    }

                    if (oct != roots[oi].Cell) throw new InvalidOperationException();
                }

                var pointCountTree = roots.Where(x => x != null).Sum(x => x.PointCountTree);
                var ebb = new Box3f(roots.Where(x => x != null).Select(x => x.BoundingBoxExactLocal));
                return new PointSetNode(rootCell, pointCountTree, ebb, 0.0f, roots.Map(n => n?.Id), a.Storage);
            }
#if DEBUG
            if (a.Cell.Exponent == b.Cell.Exponent)
            {
                if (!a.IsCenteredAtOrigin && !b.IsCenteredAtOrigin) throw new InvalidOperationException(
                    $"merge {a.Cell} with {b.Cell}")
                    ;
            }
#endif

            // ... otherwise ensure that A's root cell is bigger than B's to reduce number of cases to handle ...
            if (a.Cell.Exponent < b.Cell.Exponent)
            {
                var result = Merge(b, a, octreeSplitLimit, ct);
#if DEBUG
                if (result.PointCountTree != debugPointCountTree) throw new InvalidOperationException();
#endif
                return result;
            }

            // ... B must now be contained in exactly one of A's subcells
#if DEBUG
            var isExactlyOne = false;
#endif
            var subcells = a.Subnodes?.Map(x => x?.Value) ?? new PointSetNode[8];
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
                        subcells[i] = JoinTreeToRootCell(subcellIndex, b);
                    }
                    else
                    {
                        subcells[i] = Merge(subcells[i], b, octreeSplitLimit, ct);
                    }
                }
            }
#if DEBUG
            if (!isExactlyOne) throw new InvalidOperationException();
#endif
            PointSetNode result2 = null;
            if (a.IsLeaf)
            {
                result2 = a.ToInnerNode(subcells);
                result2 = InjectPointsIntoTree(
                    a.PositionsAbsolute, a.Colors?.Value, a.Normals?.Value, a.Intensities?.Value, a.Classifications?.Value,
                    result2, result2.Cell, octreeSplitLimit, a.Storage, ct
                    );
            }
            else
            {
                result2 = a.WithSubNodes(subcells);
            }
#if DEBUG
            // this no longer holds due to removal of duplicate points
            //if (result2.PointCountTree != debugPointCountTree) throw new InvalidOperationException();
#endif
            return result2;
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
        
        private static PointSetNode JoinNonOverlappingTrees(Cell rootCell, PointSetNode a, PointSetNode b, long octreeSplitLimit, CancellationToken ct)
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
            if (b.IsCenteredAtOrigin)
            {
#if DEBUG
                // PRE: if 'b' is centered, than 'a' cannot be centered
                // (because then 'a' and 'b' would overlap, and we join non-overlapping trees here)
                if (a.IsCenteredAtOrigin) throw new InvalidOperationException();
#endif
                Fun.Swap(ref a, ref b);
#if DEBUG
                // POST: 'a' is centered, 'b' is not centered
                if (!a.IsCenteredAtOrigin) throw new InvalidOperationException();
                if (b.IsCenteredAtOrigin) throw new InvalidOperationException();
#endif
            }
            #endregion
            
            #region CASE 1 of 2: one tree is centered (must be 'a', since if it originally was 'b' we would have swapped)

            if (rootCell.IsCenteredAtOrigin && a.IsCenteredAtOrigin)
            {
                #region special case: split 'a' into subcells to get rid of centered cell containing points
                if (a.IsLeaf)
                {
                    return JoinNonOverlappingTrees(rootCell, a.ForceSplitLeaf(ct), b, octreeSplitLimit, ct);
                }
                #endregion
#if DEBUG
                if (a.PointCount != 0) throw new InvalidOperationException();
#endif

                var subcells = new PointSetNode[8];
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
                            var merged = Merge(aSub, b, octreeSplitLimit, ct);
                            subcells[i] = JoinTreeToRootCell(rootCellOctant, merged);
                        }
                        else
                        {
                            // CASE: aSub contained
                            subcells[i] = JoinTreeToRootCell(rootCellOctant, aSub);
                        }
                    }
                    else
                    {
                        if (bIsContained)
                        {
                            // CASE: b contained
                            subcells[i] = JoinTreeToRootCell(rootCellOctant, b);
                        }
                        else
                        {
                            // CASE: none contained -> empty subcell
                            subcells[i] = null;
                        }
                    }
                }
                var ebb = new Box3f(subcells.Where(x => x != null).Select(x => x.BoundingBoxExactLocal));
                var result = new PointSetNode(rootCell, a.PointCountTree + b.PointCountTree, ebb, 0.0f, subcells.Map(x => x?.Id), a.Storage);
#if DEBUG
                if (result.PointCountTree != a.PointCountTree + b.PointCountTree) throw new InvalidOperationException();
                if (result.PointCountTree != result.Subnodes.Sum(x => x?.Value?.PointCountTree)) throw new InvalidOperationException();
#endif
                return result;
            }

            #endregion

            #region CASE 2 of 2: no tree is centered

            else
            {
#if DEBUG
                // PRE: no tree is centered
                if (a.IsCenteredAtOrigin) throw new InvalidOperationException();
                if (b.IsCenteredAtOrigin) throw new InvalidOperationException();
#endif

                var subcells = new PointSetNode[8];
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
                        subcells[i] = JoinTreeToRootCell(subcell, a);
                        if (doneB) break;
                        doneA = true;
                    }
                    if (subcell.Intersects(b.Cell))
                    {
#if DEBUG
                        if (subcell.Intersects(a.Cell)) throw new InvalidOperationException();
#endif
                        subcells[i] = JoinTreeToRootCell(subcell, b);
                        if (doneA == true) break;
                        doneB = true;
                    }
                }
                var ebb = new Box3f(subcells.Where(x => x != null).Select(x => x.BoundingBoxExactLocal));
                var result = new PointSetNode(rootCell, a.PointCountTree + b.PointCountTree, ebb, 0.0f, subcells.Map(x => x?.Id), a.Storage);
#if DEBUG
                if (result.PointCountTree != a.PointCountTree + b.PointCountTree) throw new InvalidOperationException();
                if (result.PointCountTree != result.Subnodes.Sum(x => x?.Value?.PointCountTree)) throw new InvalidOperationException();
#endif
                return result;
            }

            #endregion
        }

        private static PointSetNode JoinTreeToRootCell(Cell rootCell, PointSetNode a)
        {
            if (!rootCell.Contains(a.Cell)) throw new InvalidOperationException();
            if (a.IsCenteredAtOrigin)
            {
                throw new InvalidOperationException();
            }
            if (rootCell == a.Cell) return a;

            var subcells = new PointSetNode[8];
            for (var i = 0; i < 8; i++)
            {
                var subcell = rootCell.GetOctant(i);
                if (subcell == a.Cell) { subcells[i] = a; break; }
                if (subcell.Contains(a.Cell)) { subcells[i] = JoinTreeToRootCell(subcell, a); break; }
            }
            var ebb = new Box3f(subcells.Where(x => x != null).Select(x => x.BoundingBoxExactLocal));
            var result = new PointSetNode(rootCell, a.PointCountTree, ebb, 0.0f, subcells.Map(x => x?.Id), a.Storage);
#if DEBUG
            if (result.PointCountTree != a.PointCountTree) throw new InvalidOperationException();
#endif
            return result;
        }

        private static PointSetNode MergeLeafAndLeafWithIdenticalRootCell(PointSetNode a, PointSetNode b, long octreeSplitLimit, CancellationToken ct)
        {
            if (a.IsNotLeaf || b.IsNotLeaf) throw new InvalidOperationException();
            if (a.Cell != b.Cell) throw new InvalidOperationException();
            if (b.PositionsAbsolute == null) throw new InvalidOperationException();
            if (a.HasColors != b.HasColors) throw new InvalidOperationException();
            if (a.HasNormals != b.HasNormals) throw new InvalidOperationException();
            if (a.HasIntensities != b.HasIntensities) throw new InvalidOperationException();
            if (a.HasClassifications != b.HasClassifications) throw new InvalidOperationException();

            var ps = Concat(a.PositionsAbsolute, b.PositionsAbsolute);
            var cs = Concat(a.Colors?.Value, b.Colors?.Value);
            var ns = Concat(a.Normals?.Value, b.Normals?.Value);
            var js = Concat(a.Intensities?.Value, b.Intensities?.Value);
            var ks = Concat(a.Classifications?.Value, b.Classifications?.Value);
            var result = InMemoryPointSet.Build(ps, cs, ns, js, ks, a.Cell, octreeSplitLimit).ToPointSetNode(a.Storage, ct: ct);
            return result;
        }

        private static PointSetNode MergeLeafAndTreeWithIdenticalRootCell(PointSetNode a, PointSetNode b, long octreeSplitLimit, CancellationToken ct)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.IsNotLeaf || b.IsLeaf) throw new InvalidOperationException();
            if (a.Cell != b.Cell) throw new InvalidOperationException();

            var center = a.Center;
            var result = InjectPointsIntoTree(
                a.PositionsAbsolute, a.Colors?.Value, a.Normals?.Value, a.Intensities?.Value, a.Classifications?.Value,
                b, a.Cell, octreeSplitLimit, a.Storage, ct
                );
            return result;
        }

        private static PointSetNode MergeTreeAndTreeWithIdenticalRootCell(PointSetNode a, PointSetNode b, long octreeSplitLimit, CancellationToken ct)
        {
            if (a.IsLeaf || b.IsLeaf) throw new InvalidOperationException();
            if (a.Cell != b.Cell) throw new InvalidOperationException();
            if (a.PointCount > 0) throw new InvalidOperationException();
            if (b.PointCount > 0) throw new InvalidOperationException();

            var pointCountTree = 0L;
            var subcells = new PointSetNode[8];
            for (var i = 0; i < 8; i++)
            {
                var octant = a.Cell.GetOctant(i);
                var x = a.Subnodes[i]?.Value;
                var y = b.Subnodes[i]?.Value;

                if (x != null)
                {
                    if (y != null)
                    {
                        subcells[i] = Merge(x, y, octreeSplitLimit, ct);
                        pointCountTree += x.PointCountTree + y.PointCountTree;
                    }
                    else
                    {
                        subcells[i] = x;
                        pointCountTree += x.PointCountTree;
                        if (subcells[i].PointCountTree != x.PointCountTree) throw new InvalidOperationException();
                    }
                }
                else
                {
                    if (y != null)
                    {
                        subcells[i] = y;
                        pointCountTree += y.PointCountTree;

                        if (subcells[i].PointCountTree != y.PointCountTree) throw new InvalidOperationException();
                    }
                    else
                    {
                        subcells[i] = null;
                    }
                }
            }

            var ebb = new Box3f(subcells.Where(x => x != null).Select(x => x.BoundingBoxExactLocal));
            var result = new PointSetNode(a.Cell, pointCountTree, ebb, 0.0f, subcells.Map(x => x?.Id), a.Storage);
            return result;
        }

        private static PointSetNode InjectPointsIntoTree(
            IList<V3d> psAbsolute, IList<C4b> cs, IList<V3f> ns, IList<int> js, IList<byte> ks,
            PointSetNode a, Cell cell, long octreeSplitLimit, Storage storage,
            CancellationToken ct
            )
        {
            if (a == null)
            {
                var result0 = InMemoryPointSet.Build(psAbsolute, cs, ns, js, ks, cell, octreeSplitLimit).ToPointSetNode(storage, ct: ct);
                if (result0.PointCountTree > psAbsolute.Count) throw new InvalidOperationException();
                return result0;
            }

            if (a.Cell != cell) throw new InvalidOperationException();

            if (a.IsLeaf)
            {
                if (cs != null && !a.HasColors) throw new InvalidOperationException();
                if (cs == null && a.HasColors) throw new InvalidOperationException();
                if (ns != null && !a.HasNormals) throw new InvalidOperationException();
                if (ns == null && a.HasNormals) throw new InvalidOperationException();

                var newPs = new List<V3d>(psAbsolute); newPs.AddRange(a.PositionsAbsolute);
                var newCs = cs != null ? new List<C4b>(cs) : null; newCs?.AddRange(a.Colors.Value);
                var newNs = ns != null ? new List<V3f>(ns) : null; newNs?.AddRange(a.Normals.Value);
                var newIs = js != null ? new List<int>(js) : null; newIs?.AddRange(a.Intensities.Value);
                var newKs = ks != null ? new List<byte>(ks) : null; newKs?.AddRange(a.Classifications.Value);
                var result0 = InMemoryPointSet.Build(newPs, newCs, newNs, newIs, newKs, cell, octreeSplitLimit).ToPointSetNode(a.Storage, ct: ct);
                return result0;
            }

            var pss = new List<V3d>[8];
            var css = cs != null ? new List<C4b>[8] : null;
            var nss = ns != null ? new List<V3f>[8] : null;
            var iss = js != null ? new List<int>[8] : null;
            var kss = ks != null ? new List<byte>[8] : null;
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
                }
                pss[j].Add(psAbsolute[i]);
                if (cs != null) css[j].Add(cs[i]);
                if (ns != null) nss[j].Add(ns[i]);
                if (js != null) iss[j].Add(js[i]);
                if (ks != null) kss[j].Add(ks[i]);
            }

            if (pss.Sum(x => x?.Count) != psAbsolute.Count) throw new InvalidOperationException();

            var subcells = new PointSetNode[8];
            for (var j = 0; j < 8; j++)
            {
                var x = a.Subnodes[j]?.Value;
                if (pss[j] != null)
                {
                    subcells[j] = InjectPointsIntoTree(pss[j], css?[j], nss?[j], iss?[j], kss?[j], x, cell.GetOctant(j), octreeSplitLimit, storage, ct);
                }
                else
                {
                    subcells[j] = x;
                }
            }

            return a.WithSubNodes(subcells);
        }
    }
}
