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
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
        public class QueryGridResultCell
        {
            /// <summary>Grid cell bounding box.</summary>
            public Box2d Footprint { get; }

            /// <summary></summary>
            public IEnumerable<Chunk> Points { get; }

            public QueryGridResultCell(Box2d footprint, IEnumerable<Chunk> points)
            {
                Footprint = footprint;
                Points = points;
            }
        }

        /// <summary>
        /// </summary>
        public static IEnumerable<QueryGridResultCell> QueryGridXY(
            this PointSet self, V2d stride, int minCellExponent = int.MinValue
            )
            => QueryGridXY(self.Root.Value, stride, minCellExponent);

        /// <summary>
        /// </summary>
        public static IEnumerable<QueryGridResultCell> QueryGridXY(
            this IPointCloudNode self, V2d stride, int maxInMemoryPointCount = 10 * 1024 * 1024, int minCellExponent = int.MinValue
            )
        {
            var bbw = self.BoundingBoxExactGlobal;  // bounding box (world space)
            var bbt = new Box2l(                    // bounding box (tile space)
                new V2l((long)Math.Floor(bbw.Min.X / stride.X), (long)Math.Floor(bbw.Min.Y / stride.Y)),
                new V2l((long)Math.Floor(bbw.Max.X / stride.X) + 1L, (long)Math.Floor(bbw.Max.Y / stride.Y) + 1L)
                ) ;

            return QueryGridRecXY(bbt, stride, maxInMemoryPointCount, minCellExponent, new List<IPointCloudNode> { self });
        }

        private static IEnumerable<QueryGridResultCell> QueryGridRecInMemoryXY(Box2l bb, V2d stride, Chunk chunk)
        {
            var area = bb.Area;
            if (area == 0 || chunk.Count == 0) yield break;

            var q = new Box2d(bb.Min.X * stride.X, bb.Min.Y * stride.Y, bb.Max.X * stride.X, bb.Max.Y * stride.Y);

            var newChunk = chunk.ImmutableFilterByBoxXY(q);
            if (newChunk.Count == 0) yield break;

            if (area == 1)
            {
                yield return new QueryGridResultCell(q, new[] { newChunk });
            }
            else
            {
                var sbbs = bb.SplitAtCenter();
                foreach (var sbb in sbbs)
                {
                    if (sbb.Min.X == sbb.Max.X || sbb.Min.Y == sbb.Max.Y) continue;
                    var xs = QueryGridRecInMemoryXY(sbb, stride, newChunk);
                    foreach (var x in xs) yield return x;
                }
            }
        }
        private static IEnumerable<QueryGridResultCell> QueryGridRecXY(Box2l bb, V2d stride, int maxInMemoryPointCount, int minCellExponent, List<IPointCloudNode> roots)
        {
            var area = bb.Area;
            if (area == 0 || roots.Count == 0) yield break;

            var q = new Box2d(bb.Min.X * stride.X, bb.Min.Y * stride.Y, bb.Max.X * stride.X, bb.Max.Y * stride.Y);

            if (area == 1)
            {
                yield return new QueryGridResultCell(q, roots.SelectMany(root => root.QueryPointsInsideBoxXY(q)));
            }
            else
            {
                var newRoots = new List<IPointCloudNode>();
                foreach (var r in roots)
                {
                    if (r.IsLeaf) newRoots.Add(r);
                    else
                    {
                        var _bb = r.BoundingBoxExactGlobal.XY;
                        if (!q.Intersects(_bb)) { }
                        else if (q.Contains(_bb)) newRoots.Add(r);
                        else
                        {
                            var sub = r.Subnodes;
                            void add(int i) { if (sub[i] != null) { newRoots.Add(sub[i].Value); } }
                            var c = r.Center.XY;
                            if (q.Max.X < c.X)
                            {
                                // left cells
                                if (q.Max.Y < c.Y) { add(0); add(4); } // left/bottom
                                else if (q.Min.Y >= c.Y) { add(2); add(6); } // left/top
                                else { add(0); add(4); add(2); add(6); }
                            }
                            else if (q.Min.X >= c.X)
                            {
                                // right cells
                                if (q.Max.Y < c.Y) { add(1); add(5); } // right/bottom
                                else if (q.Min.Y >= c.Y) { add(3); add(7); } // right/top
                                else { add(1); add(5); add(3); add(7); }
                            }
                            else
                            {
                                // left/right cells
                                if (q.Max.Y < c.Y) { add(0); add(1); add(4); add(5); } // bottom
                                else if (q.Min.Y >= c.Y) { add(2); add(3); add(6); add(7); } // top
                                else { newRoots.Add(r); }
                            }
                        }
                    }
                }

                var sbbs = bb.SplitAtCenter();
                var total = newRoots.Sum(r => r.PointCountTree);
                if (total <= maxInMemoryPointCount)
                {
                    var chunk = Chunk.ImmutableMerge(newRoots.SelectMany(r => r.QueryPointsInsideBoxXY(q)));
                    foreach (var sbb in sbbs)
                    {
                        if (sbb.Min.X == sbb.Max.X || sbb.Min.Y == sbb.Max.Y) continue;
                        var xs = QueryGridRecInMemoryXY(sbb, stride, chunk);
                        foreach (var x in xs) yield return x;
                    }
                }
                else
                {
                    foreach (var sbb in sbbs)
                    {
                        if (sbb.Min.X == sbb.Max.X || sbb.Min.Y == sbb.Max.Y) continue;
                        var xs = QueryGridRecXY(sbb, stride, maxInMemoryPointCount, minCellExponent, newRoots);
                        foreach (var x in xs) yield return x;
                    }
                }
            }
        }
    }
}
