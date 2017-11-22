/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Linq;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class Queries
    {
        #region Ray3d, Line3d

        /// <summary>
        /// Points within given distance of a ray.
        /// </summary>
        public static IEnumerable<PointsNearObject<Line3d>> QueryPointsNearRay(
            this PointSet self, Ray3d ray, double maxDistanceToRay
            )
        {
            ray.Direction = ray.Direction.Normalized;
            var data = self.Root.Value;
            var bbox = data.BoundingBox;

            var line = Clip(bbox, ray);
            if (!line.HasValue) return Enumerable.Empty<PointsNearObject<Line3d>>();

            return self.QueryPointsNearLineSegment(line.Value, maxDistanceToRay);
        }

        /// <summary>
        /// Points within given distance of a line segment.
        /// </summary>
        public static IEnumerable<PointsNearObject<Line3d>> QueryPointsNearLineSegment(
            this PointSet self, Line3d lineSegment, double maxDistanceToRay
            )
            => QueryPointsNearLineSegment(self.Root.Value, lineSegment, maxDistanceToRay);

        /// <summary>
        /// Points within given distance of a line segment.
        /// </summary>
        public static IEnumerable<PointsNearObject<Line3d>> QueryPointsNearLineSegment(
            this PointSetNode node, Line3d lineSegment, double maxDistanceToRay
            )
        {
            if (!node.BoundingBox.Intersects(lineSegment))
            {
                yield break;
            }
            else if (node.PointCount > 0)
            {
                var center = node.Center;
                var ia = node.KdTree.Value.GetClosestToLine((V3f)(lineSegment.P0 - center), (V3f)(lineSegment.P1 - center), (float)maxDistanceToRay, 1000);
                if (ia.Count > 0)
                {
                    var ps = new V3d[ia.Count];
                    var cs = new C4b[ia.Count];
                    var ds = new double[ia.Count];
                    for (var i = 0; i < ia.Count; i++)
                    {
                        var index = (int)ia[i].Index;
                        ps[i] = center + (V3d)node.Positions.Value[index];
                        if (node.HasColors) cs[i] = node.Colors.Value[index];
                        ds[i] = ia[i].Dist;
                    }
                    var chunk = new PointsNearObject<Line3d>(lineSegment, maxDistanceToRay, ps, cs, ds);
                    yield return chunk;
                }
            }
            else if (node.Subnodes != null)
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    foreach (var x in QueryPointsNearLineSegment(n.Value, lineSegment, maxDistanceToRay)) yield return x;
                }
            }
        }
        
        /// <summary>
        /// Clips given ray on box, or returns null if ray does not intersect box.
        /// </summary>
        private static Line3d? Clip(Box3d box, Ray3d ray0)
        {
            ray0.Direction = ray0.Direction.Normalized;

            if (!box.Intersects(ray0, out double t0)) return null;
            var p0 = ray0.GetPointOnRay(t0);

            var ray1 = new Ray3d(ray0.GetPointOnRay(t0 + box.Size.Length), -ray0.Direction);
            if (!box.Intersects(ray1, out double t1)) throw new InvalidOperationException();
            var p1 = ray1.GetPointOnRay(t1);

            return new Line3d(p0, p1);
        }

        #endregion

        #region Plane3d

        /// <summary>
        /// All points within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlane(
            this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNearPlane(self.Root.Value, plane, maxDistance, minCellExponent);

        /// <summary>
        /// All points within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlane(
            this PointSetNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => plane.Contains(maxDistance, node.BoundingBox),
                n => !node.BoundingBox.Intersects(plane, maxDistance),
                p => Math.Abs(plane.Height(p)) <= maxDistance,
                minCellExponent
                );

        /// <summary>
        /// All points within maxDistance of ANY of the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlanes(
            this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNearPlanes(self.Root.Value, planes, maxDistance, minCellExponent);

        /// <summary>
        /// All points within maxDistance of ANY of the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPlanes(
            this PointSetNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => planes.Any(plane => plane.Contains(maxDistance, node.BoundingBox)),
                n => !planes.Any(plane => node.BoundingBox.Intersects(plane, maxDistance)),
                p => planes.Any(plane => Math.Abs(plane.Height(p)) <= maxDistance),
                minCellExponent
                );

        /// <summary>
        /// All points NOT within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlane(
            this PointSet self, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNotNearPlane(self.Root.Value, plane, maxDistance, minCellExponent);

        /// <summary>
        /// All points NOT within maxDistance of given plane.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlane(
            this PointSetNode node, Plane3d plane, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => !node.BoundingBox.Intersects(plane, maxDistance),
                n => plane.Contains(maxDistance, node.BoundingBox),
                p => Math.Abs(plane.Height(p)) > maxDistance,
                minCellExponent
                );

        /// <summary>
        /// All points NOT within maxDistance of ALL the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlanes(
            this PointSet self, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNotNearPlanes(self.Root.Value, planes, maxDistance, minCellExponent);

        /// <summary>
        /// All points NOT within maxDistance of ALL the given planes.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPlanes(
            this PointSetNode node, Plane3d[] planes, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPoints(node,
                n => !planes.Any(plane => node.BoundingBox.Intersects(plane, maxDistance)),
                n => planes.Any(plane => plane.Contains(maxDistance, node.BoundingBox)),
                p => !planes.Any(plane => Math.Abs(plane.Height(p)) <= maxDistance),
                minCellExponent
                );

        #endregion

        #region Polygon3d

        /// <summary>
        /// All points within maxDistance of given polygon.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPolygon(
            this PointSet self, Polygon3d polygon, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNearPolygon(self.Root.Value, polygon, maxDistance, minCellExponent);

        /// <summary>
        /// All points within maxDistance of given polygon.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPolygon(
            this PointSetNode node, Polygon3d polygon, double maxDistance, int minCellExponent = int.MinValue
            )
        {
            var plane = polygon.GetPlane3d();
            var w2p = plane.GetWorldToPlane();
            var poly2d = new Polygon2d(polygon.GetPointArray().Map(p => w2p.TransformPos(p).XY));
            return QueryPoints(node,
                n => plane.Contains(maxDistance, node.BoundingBox),
                n => !node.BoundingBox.Intersects(plane, maxDistance),
                p => polygon.Contains(plane, w2p, poly2d, maxDistance, p, out double d),
                minCellExponent
                );
        }

        /// <summary>
        /// All points within maxDistance of ANY of the given polygons.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPolygons(
            this PointSet self, Polygon3d[] polygons, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNearPolygons(self.Root.Value, polygons, maxDistance, minCellExponent);

        /// <summary>
        /// All points within maxDistance of ANY of the given polygons.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNearPolygons(
            this PointSetNode node, Polygon3d[] polygons, double maxDistance, int minCellExponent = int.MinValue
            )
        {
            var planes = polygons.Map(x => x.GetPlane3d());
            var w2p = planes.Map(x => x.GetWorldToPlane());
            var poly2d = polygons.Map((x, i) => new Polygon2d(x.GetPointArray().Map(p => w2p[i].TransformPos(p).XY)));
            return QueryPoints(node,
                n => !planes.Any(plane => !plane.Contains(maxDistance, node.BoundingBox)),
                n => !planes.Any(plane => node.BoundingBox.Intersects(plane, maxDistance)),
                p => planes.Any((plane, i) => polygons[i].Contains(plane, w2p[i], poly2d[i], maxDistance, p, out double d)),
                minCellExponent
                );
        }

        /// <summary>
        /// All points NOT within maxDistance of given polygon.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPolygon(
            this PointSet self, Polygon3d polygon, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNotNearPolygon(self.Root.Value, polygon, maxDistance, minCellExponent);

        /// <summary>
        /// All points NOT within maxDistance of given polygon.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPolygon(
            this PointSetNode node, Polygon3d polygon, double maxDistance, int minCellExponent = int.MinValue
            )
        {
            var plane = polygon.GetPlane3d();
            var w2p = plane.GetWorldToPlane();
            var poly2d = new Polygon2d(polygon.GetPointArray().Map(p => w2p.TransformPos(p).XY));
            return QueryPoints(node,
                n => !node.BoundingBox.Intersects(plane, maxDistance),
                n => plane.Contains(maxDistance, node.BoundingBox),
                p => !polygon.Contains(plane, w2p, poly2d, maxDistance, p, out double d),
                minCellExponent
                );
        }

        /// <summary>
        /// All points NOT within maxDistance of ALL the given polygons.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPolygons(
            this PointSet self, Polygon3d[] polygons, double maxDistance, int minCellExponent = int.MinValue
            )
            => QueryPointsNotNearPolygons(self.Root.Value, polygons, maxDistance, minCellExponent);

        /// <summary>
        /// All points NOT within maxDistance of ALL the given polygons.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsNotNearPolygons(
            this PointSetNode node, Polygon3d[] polygons, double maxDistance, int minCellExponent = int.MinValue
            )
        {
            var planes = polygons.Map(x => x.GetPlane3d());
            var w2p = planes.Map(x => x.GetWorldToPlane());
            var poly2d = polygons.Map((x, i) => new Polygon2d(x.GetPointArray().Map(p => w2p[i].TransformPos(p).XY)));
            return QueryPoints(node,
                n => !planes.Any(plane => node.BoundingBox.Intersects(plane, maxDistance)),
                n => !planes.Any(plane => !plane.Contains(maxDistance, node.BoundingBox)),
                p => !planes.Any((plane, i) => polygons[i].Contains(plane, w2p[i], poly2d[i], maxDistance, p, out double d)),
                minCellExponent
                );
        }

        #endregion

        #region Hull3d (convex hull)

        /// <summary>
        /// All points inside convex hull (including boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInsideConvexHull(
            this PointSet self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
            => QueryPointsInsideConvexHull(self.Root.Value, convexHull, minCellExponent);

        /// <summary>
        /// All points inside convex hull (including boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInsideConvexHull(
            this PointSetNode self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
        {
            foreach (var x in self.ForEachNodeIntersecting(convexHull, true, minCellExponent))
            {
                if (x.IsFullyInside)
                {
                    foreach (var y in x.Cell.ForEachNode())
                    {
                        if (y.PointCount == 0) continue;
                        var chunk = new Chunk(y.PositionsAbsolute, y.Colors?.Value);
                        yield return chunk;
                    }
                }
                else
                {
                    var n = x.Cell;
                    if (n.PointCount == 0) continue;
                    var ps = new List<V3d>();
                    var cs = n.HasColors ? new List<C4b>() : null;
                    var positionsAbsolute = n.PositionsAbsolute;
                    for (var i = 0; i < positionsAbsolute.Length; i++)
                    {
                        if (convexHull.Contains(positionsAbsolute[i]))
                        {
                            ps.Add(positionsAbsolute[i]);
                            if (n.HasColors) cs.Add(n.Colors.Value[i]);
                        }
                    }
                    var item = Tuple.Create(ps, cs);

                    var chunk = new Chunk(ps, cs);
                    yield return chunk;
                }
            }
        }

        /// <summary>
        /// All points outside convex hull (excluding boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsOutsideConvexHull(
            this PointSet self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
            => QueryPointsOutsideConvexHull(self.Root.Value, convexHull, minCellExponent);

        /// <summary>
        /// All points outside convex hull (excluding boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsOutsideConvexHull(
            this PointSetNode self, Hull3d convexHull, int minCellExponent = int.MinValue
            )
            => QueryPointsInsideConvexHull(self, convexHull.Reversed(), minCellExponent);

        #endregion

        #region Box3d

        /// <summary>
        /// All points inside axis-aligned box (including boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInsideBox(
            this PointSet self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => QueryPointsInsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        /// <summary>
        /// All points inside axis-aligned box (including boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInsideBox(
            this PointSetNode self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => QueryPointsInsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        /// <summary>
        /// All points outside axis-aligned box (excluding boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsOutsideBox(
            this PointSet self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => QueryPointsOutsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        /// <summary>
        /// All points outside axis-aligned box (excluding boundary).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsOutsideBox(
            this PointSetNode self, Box3d boundingBox, int minCellExponent = int.MinValue
            )
            => QueryPointsOutsideConvexHull(self, Hull3d.Create(boundingBox), minCellExponent);

        #endregion

        #region View frustum

        /// <summary>
        /// Returns points inside view frustum (defined by viewProjection and canonicalViewVolume).
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInViewFrustum(
            this PointSet self, M44d viewProjection, Box3d canonicalViewVolume
            )
        {
            var t = viewProjection.Inverse;
            var cs = canonicalViewVolume.ComputeCorners().Map(t.TransformPosProj);
            var hull = new Hull3d(new[]
            {
                new Plane3d(cs[0], cs[2], cs[1]), // near
                new Plane3d(cs[5], cs[7], cs[4]), // far
                new Plane3d(cs[0], cs[1], cs[4]), // bottom
                new Plane3d(cs[1], cs[3], cs[5]), // left
                new Plane3d(cs[4], cs[6], cs[0]), // right
                new Plane3d(cs[3], cs[2], cs[7]), // top
            });

            return QueryPointsInsideConvexHull(self, hull);
        }

        #endregion

        #region Octree levels

        /// <summary>
        /// Max tree depth.
        /// </summary>
        public static int CountOctreeLevels(this PointSetNode root)
        {
            if (root == null) return 0;
            if (root.Subnodes == null) return 1;
            return root.Subnodes.Select(n => CountOctreeLevels(n?.Value)).Max() + 1;
        }

        /// <summary>
        /// Finds deepest octree level which still contains less than given number of points. 
        /// </summary>
        public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
            this PointSet self, long maxPointCount
            )
            => GetMaxOctreeLevelWithLessThanGivenPointCount(self.Root.Value, maxPointCount);

        /// <summary>
        /// Finds deepest octree level which still contains less than given number of points. 
        /// </summary>
        public static int GetMaxOctreeLevelWithLessThanGivenPointCount(
            this PointSetNode node, long maxPointCount
            )
        {
            var imax = node.CountOctreeLevels();
            for (var i = 0; i < imax; i++)
            {
                var count = node.CountPointsInOctreeLevel(i);
                if (count >= maxPointCount) return i - 1;
            }

            return imax - 1;
        }

        /// <summary>
        /// Gets total number of points in all cells at given octree level.
        /// </summary>
        public static long CountPointsInOctreeLevel(
            this PointSet self, int level
            )
        {
            return CountPointsInOctreeLevel(self.Root.Value, level);
        }

        /// <summary>
        /// Gets total number of lod-points in all cells at given octree level.
        /// </summary>
        public static long CountPointsInOctreeLevel(
            this PointSetNode node, int level
            )
        {
            if (level < 0) return 0;

            if (level == 0 || node.IsLeaf)
            {
                return node.LodPointCount;
            }
            else
            {
                var nextLevel = level - 1;
                var sum = 0L;
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    sum += CountPointsInOctreeLevel(n.Value, nextLevel);
                }
                return sum;
            }
        }

        /// <summary>
        /// Returns points in given octree level, where level 0 is the root node.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
            this PointSet self, int level
            )
            => QueryPointsInOctreeLevel(self.Root.Value, level);

        /// <summary>
        /// Returns lod points for given octree depth/front, where level 0 is the root node.
        /// Front will include leafs higher up than given level.
        /// </summary>
        public static IEnumerable<Chunk> QueryPointsInOctreeLevel(
            this PointSetNode node, int level
            )
        {
            if (level < 0) yield break;

            if (level == 0 || node.IsLeaf)
            {
                var ps = node.LodPositionsAbsolute;
                var cs = node?.LodColors?.Value;
                var chunk = new Chunk(ps, cs);
                yield return chunk;
            }
            else
            {
                if (node.Subnodes == null) yield break;

                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    foreach (var x in QueryPointsInOctreeLevel(n.Value, level - 1)) yield return x;
                }
            }
        }

        #endregion

        #region Resampling

        /// <summary>
        /// Returns points resampled to be no closer than minimal distance.
        /// </summary>
        public static IEnumerable<Chunk> QueryNormalizedDensity(
            this PointSet self, double minDistance
            )
            => QueryNormalizedDensity(self.Root.Value, minDistance);

        /// <summary>
        /// Returns points resampled to be no closer than minimal distance.
        /// </summary>
        public static IEnumerable<Chunk> QueryNormalizedDensity(
            PointSetNode node, double minDistance
            )
        {
            if (node.PointCount > 0)
            {
                var positions = node.Positions.Value;
                var colors = node?.Colors?.Value;
                var vs = new bool[positions.Length].Set(true);
                var kd = node.KdTree.Value;
                for (var i = 0; i < positions.Length; i++)
                {
                    if (!vs[i]) continue;
                    var ia = kd.GetClosest(positions[i], (float)minDistance, 100);
                    foreach (var x in ia) if (x.Index != i) vs[x.Index] = false;
                }

                var ps = new List<V3d>(); var cs = new List<C4b>();
                var center = node.Center;
                for (var i = 0; i < vs.Length; i++)
                {
                    if (vs[i] == false) continue;
                    ps.Add(center + (V3d)positions[i]);
                    if (node.HasColors) cs.Add(colors[i]);
                }

                var chunk = new Chunk(ps.ToArray(), cs.ToArray());
                yield return chunk;
            }

            if (node.Subnodes == null) yield break;
            for (var i = 0; i < 8; i++)
            {
                var n = node.Subnodes[i];
                if (n == null) continue;
                foreach (var x in QueryNormalizedDensity(n.Value, minDistance)) yield return x;
            }
        }

        /// <summary>
        /// Returns tree with density resampled to given minimum distance.
        /// </summary>
        public static PointSet Resample(this PointSet self, double minDistance)
        {
            var root = Resample(self.Root.Value, minDistance, self.SplitLimit);
            return new PointSet(self.Storage, $"{self.Id}_resampled_{minDistance}", root.Id, self.SplitLimit);
        }

        /// <summary>
        /// Returns tree with density resampled to given minimum distance.
        /// </summary>
        public static PointSetNode Resample(this PointSetNode node, double minDistance, long octreeSplitLimit)
        {
            // resample cell
            var resampledCellPoints = node.PointCount > 0
                ? Resample(node.KdTree.Value, node.Positions.Value, node?.Colors?.Value, minDistance)
                : Tuple.Create(new V3f[0], new C4b[0])
                ;
            var resampledCellPointsCount = resampledCellPoints.Item1.Length;
            
            // resample subcells
            var resampledSubcells = node.Subnodes != null
                ? node.Subnodes.Map(subnode => subnode.Value?.Resample(minDistance, octreeSplitLimit))
                : null
                ;
            var resampledSubcellsPointCount = resampledSubcells != null
                ? resampledSubcells.Map(n => n != null ? n.PointCountTree : 0).Sum()
                : 0
                ;

            // optionally, merge subcells into cell (if in total there are less points than given split limit)
            if (resampledSubcells != null && resampledCellPointsCount + resampledSubcellsPointCount <= octreeSplitLimit)
            {
                var center = node.Center;
                var chunks = new List<Chunk>();
                for (var i = 0; i < 8; i++)
                {
                    var n = resampledSubcells[i];
                    if (n == null) continue;
                    foreach (var x in QueryAllPoints(n)) chunks.Add(x);
                }

                var ps = Enumerable.Concat(resampledCellPoints.Item1, chunks.SelectMany(x => x.Positions.Select(p => (V3f)(p - center)))).ToArray();
                var cs = Enumerable.Concat(resampledCellPoints.Item2, chunks.SelectMany(x => x.Colors)).ToArray();
                var mergedCell = PointSetNode.Create(node.Cell, ps, cs, null, node.Storage);
                return mergedCell;
            }

            var result = PointSetNode.Create(node.Cell, resampledCellPoints.Item1, resampledCellPoints.Item2, resampledSubcells, node.Storage);
            return result;
        }

        private static Tuple<V3f[], C4b[]> Resample(PointRkdTreeD<V3f[], V3f> kd, V3f[] ps, C4b[] cs, double minDistance)
        {
            var count = ps.Length;
            var vs = new bool[count].Set(true);
            for (var i = 0; i < count; i++)
            {
                if (!vs[i]) continue;
                var ia = kd.GetClosest(ps[i], (float)minDistance, 100);
                for (var j = 0; j < ia.Count; j++)
                {
                    var index = ia[j].Index;
                    if (index != i) vs[index] = false;
                }
            }

            var psl = new List<V3f>();
            var csl = new List<C4b>();
            for (var i = 0; i < count; i++)
            {
                if (vs[i] == false) continue;
                psl.Add(ps[i]);
                csl.Add(cs[i]);
            }
            
            return Tuple.Create(psl.ToArray(), csl.ToArray());
        }

        #endregion

        #region All points

        /// <summary>
        /// Returns all points in pointset.
        /// </summary>
        public static IEnumerable<Chunk> QueryAllPoints(this PointSet self) => QueryAllPoints(self.Root.Value);

        /// <summary>
        /// Returnd all points in tree.
        /// </summary>
        public static IEnumerable<Chunk> QueryAllPoints(this PointSetNode node)
            => node.QueryPoints(_ => true, _ => false, _ => true);

        #endregion

        /// <summary>
        /// </summary>
        public static IEnumerable<Chunk> QueryPoints(this PointSet node,
            Func<PointSetNode, bool> isNodeFullyInside,
            Func<PointSetNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            int minCellExponent = int.MinValue
            )
            => QueryPoints(node.Root.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);

        /// <summary>
        /// </summary>
        /// <param name="node"></param>
        /// <param name="isNodeFullyInside"></param>
        /// <param name="isNodeFullyOutside"></param>
        /// <param name="isPositionInside"></param>
        /// <param name="minCellExponent">Limit traversal depth to minCellExponent (inclusive).</param>
        /// <returns></returns>
        public static IEnumerable<Chunk> QueryPoints(this PointSetNode node,
            Func<PointSetNode, bool> isNodeFullyInside,
            Func<PointSetNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            int minCellExponent = int.MinValue
            )
        {
            if (node.Cell.Exponent < minCellExponent) yield break;

            if (isNodeFullyOutside(node)) yield break;
            
            if (node.IsLeaf || node.Cell.Exponent == minCellExponent)
            {
                if (isNodeFullyInside(node))
                {
                    if (node.HasPositions)
                    {
                        yield return new Chunk(node.PositionsAbsolute, node.Colors?.Value);
                    }
                    else if (node.HasLodPositions)
                    {
                        yield return new Chunk(node.LodPositionsAbsolute, node.LodColors?.Value);
                    }
                    yield break;
                }
                
                var psRaw = node.HasPositions ? node.PositionsAbsolute : node.LodPositionsAbsolute;
                var csRaw = node.HasColors ? node.Colors?.Value : node.LodColors?.Value;
                var ps = new List<V3d>();
                var cs = csRaw != null ? new List<C4b>() : null;
                for (var i = 0; i < psRaw.Length; i++)
                {
                    var p = psRaw[i];
                    if (isPositionInside(p))
                    {
                        ps.Add(p);
                        if (csRaw != null) cs.Add(csRaw[i]);
                    }
                }
                if (ps.Count > 0)
                {
                    yield return new Chunk(ps, cs);
                }
            }
            else
            {
                for (var i = 0; i < 8; i++)
                {
                    var n = node.Subnodes[i];
                    if (n == null) continue;
                    var xs = QueryPoints(n.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, minCellExponent);
                    foreach (var x in xs) yield return x;
                }
            }
        }
    }
}
