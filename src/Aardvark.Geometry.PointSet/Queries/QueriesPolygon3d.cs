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
using Aardvark.Base;
using Aardvark.Data.Points;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static partial class Queries
    {
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
            var bounds = polygon.BoundingBox3d(maxDistance);
            var plane = polygon.GetPlane3d();
            var w2p = plane.GetWorldToPlane();
            var poly2d = new Polygon2d(polygon.GetPointArray().Map(p => w2p.TransformPos(p).XY));
            return QueryPoints(node,
                n => false,
                n => !n.BoundingBox.Intersects(bounds),
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
            var bounds = polygons.Map(x => x.BoundingBox3d(maxDistance));
            var planes = polygons.Map(x => x.GetPlane3d());
            var w2p = planes.Map(x => x.GetWorldToPlane());
            var poly2d = polygons.Map((x, i) => new Polygon2d(x.GetPointArray().Map(p => w2p[i].TransformPos(p).XY)));
            return QueryPoints(node,
                n => false,
                n => !bounds.Any(b => n.BoundingBox.Intersects(b)),
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
            var bounds = polygon.BoundingBox3d(maxDistance);
            var plane = polygon.GetPlane3d();
            var w2p = plane.GetWorldToPlane();
            var poly2d = new Polygon2d(polygon.GetPointArray().Map(p => w2p.TransformPos(p).XY));
            return QueryPoints(node,
                n => !n.BoundingBox.Intersects(bounds),
                n => false,
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
            var bounds = polygons.Map(x => x.BoundingBox3d(maxDistance));
            var planes = polygons.Map(x => x.GetPlane3d());
            var w2p = planes.Map(x => x.GetWorldToPlane());
            var poly2d = polygons.Map((x, i) => new Polygon2d(x.GetPointArray().Map(p => w2p[i].TransformPos(p).XY)));
            return QueryPoints(node,
                n => !bounds.Any(b => n.BoundingBox.Intersects(b)),
                n => false,
                p => !planes.Any((plane, i) => polygons[i].Contains(plane, w2p[i], poly2d[i], maxDistance, p, out double d)),
                minCellExponent
                );
        }
    }
}
