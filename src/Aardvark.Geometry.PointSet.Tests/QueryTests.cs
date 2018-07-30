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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class QueryTests
    {
        private static PointSet CreateRandomPointsInUnitCube(int n, int splitLimit)
        {
            var r = new Random();
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(splitLimit)
                ;
            return PointCloud.Chunks(new Chunk(ps, null), config);
        }

        private static PointSet CreateClusteredPointsInUnitCube(int n, int splitLimit)
        {
            var r = new Random();
            V3d randomPos() => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
            var ps = new V3d[n];
            for (var i = 0; i < n / 2; i++) ps[i] = randomPos();
            for (var i = n / 2 + 1; i < n; i++) ps[i] = randomPos();
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(splitLimit)
                ;
            return PointCloud.Chunks(new Chunk(ps, null), config);
        }

        private static PointSet CreateRegularPointsInUnitCube(int n, int splitLimit)
        {
            var ps = new List<V3d>();
            var step = 1.0 / n;
            var start = step * 0.5;
            for (var x = start; x < 1.0; x += step)
                for (var y = start; y < 1.0; y += step)
                    for (var z = start; z < 1.0; z += step)
                        ps.Add(new V3d(x, y, z));
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore())
                .WithKey("test")
                .WithOctreeSplitLimit(splitLimit)
                ;
            return PointCloud.Chunks(new Chunk(ps, null), config);
        }

        #region Ray3d, Line3d

        [Test]
        public void CanQueryPointsAlongRay()
        {
            var filename = Config.TEST_FILE_NAME_PTS;
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");

            var config = ImportConfig.Default
                .WithInMemoryStore()
                .WithKey("key1")
                .WithOctreeSplitLimit(1000)
                .WithReadBufferSizeInBytes(64 * 1024 * 1024)
                ;
            var pointset = PointCloud.Import(filename, config);

            var ray1 = new Ray3d(new V3d(0.1, -1.0, -0.2), V3d.OIO);
            var ray2 = new Ray3d(new V3d(0.1, -0.5, -0.2), V3d.OIO);

            var count1 = 0;
            var count2 = 0;

            foreach (var x in pointset.QueryPointsNearRay(ray1, 0.1)) count1 += x.Positions.Length;
            foreach (var x in pointset.QueryPointsNearRay(ray2, 0.1)) count2 += x.Positions.Length;

            Assert.IsTrue(count1 >= count2);
        }

        #endregion

        #region V3d

        [Test]
        public void CanQueryPointsNearPoint_1()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 1024);
            Assert.IsTrue(pointset.Root.Value.IsLeaf);

            var ps = pointset.QueryPointsNearPoint(new V3d(0.5, 0.5, 0.5), 1.0, 10000);
            Assert.IsTrue(ps.Count == 1024);
        }

        [Test]
        public void CanQueryPointsNearPoint_2()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 32);
            Assert.IsTrue(pointset.Root.Value.IsNotLeaf);

            var ps = pointset.QueryPointsNearPoint(new V3d(0.5, 0.5, 0.5), 1.0, 10000);
            Assert.IsTrue(ps.Count == 1024);
        }

        [Test]
        public void CanQueryPointsNearPoint_3()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 32);
            var ps = pointset.QueryPointsNearPoint(new V3d(2.5, 0.5, 0.5), 1.0, 10000);
            Assert.IsTrue(ps.Count == 0);
        }

        [Test]
        public void CanQueryPointsNearPoint_4()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 32);
            var ps = pointset.QueryPointsNearPoint(new V3d(0.75, 0.5, 0.25), 0.25, 10000);
            Assert.IsTrue(ps.Count < 1024);
        }

        [Test]
        public void CanQueryPointsNearPoint_5()
        {
            var pointset = CreateClusteredPointsInUnitCube(1024, 32);
            var xs = pointset.QueryAllPoints().SelectMany(x => x.Positions).ToArray();

            var nonEmtpyResultCount = 0;
            var rand = new Random();
            for (var round = 0; round < 1000; round++)
            {
                var query = new V3d(rand.NextDouble() * 3 - 1, rand.NextDouble() * 3 - 1, rand.NextDouble() * 3 - 1);
                var maxDistanceToPoint = rand.NextDouble();
                var maxCount = rand.Next(1024 + 1);

                var correctResult = new HashSet<V3d>(xs
                    .Where(x => (x - query).Length <= maxDistanceToPoint)
                    .OrderBy(x => (x - query).Length)
                    .Take(maxCount)
                    );

                var ps = pointset.QueryPointsNearPoint(query, maxDistanceToPoint, maxCount);
                var queryResult = new HashSet<V3d>(ps.Positions);

                Assert.IsTrue(queryResult.Count == correctResult.Count);
                foreach (var x in correctResult) Assert.IsTrue(queryResult.Contains(x));

                if (queryResult.Count > 0) nonEmtpyResultCount++;
            }

            if (nonEmtpyResultCount == 0) Assert.Inconclusive();
        }

        #endregion

        #region Plane3d

        [Test]
        public void CanQueryPointsNearPlane_1()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 64);

            var q = new Plane3d(V3d.ZAxis, new V3d(0.5, 0.5, 0.5));

            var ps = pointset.QueryPointsNearPlane(q, 0.1).SelectMany(x => x.Positions).ToList();
            Assert.IsTrue(pointset.PointCount > ps.Count);

            var bb = new Box3d(new V3d(0.0, 0.0, 0.4), new V3d(1.0, 1.0, 0.6));
            foreach (var p in ps) Assert.IsTrue(bb.Contains(p));
        }

        [Test]
        public void CanQueryPointsNearPlane_2()
        {
            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNearPlane(new Plane3d(V3d.ZAxis, new V3d(0, 0, 0.3)), 0.1)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 4 * 4);
        }

        [Test]
        public void CanQueryPointsNearPlane_3()
        {
            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNearPlane(new Plane3d(V3d.ZAxis, new V3d(0, 0, 0.3)), 0.2)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 2 * 4 * 4);
        }

        [Test]
        public void CanQueryPointsNearPlanes_1()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 64);

            var q = new[]
            {
                new Plane3d(V3d.ZAxis, new V3d(0.5, 0.5, 0.5)),
                new Plane3d(V3d.XAxis, new V3d(0.7, 0.5, 0.5))
            };

            var ps = pointset.QueryPointsNearPlanes(q, 0.1).SelectMany(x => x.Positions).ToList();
            Assert.IsTrue(pointset.PointCount > ps.Count);

            var bb1 = new Box3d(new V3d(0.0, 0.0, 0.4), new V3d(1.0, 1.0, 0.6));
            var bb2 = new Box3d(new V3d(0.6, 0.0, 0.0), new V3d(0.8, 1.0, 1.0));
            //var wrongs = ps.Where(p => !bb1.Contains(p) && !bb2.Contains(p)).ToArray();
            foreach (var p in ps) Assert.IsTrue(bb1.Contains(p) || bb2.Contains(p));
        }

        [Test]
        public void CanQueryPointsNearPlanes_2()
        {
            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNearPlanes(new[]
                {
                    new Plane3d(V3d.ZAxis, new V3d(0, 0, 0.3)),
                    new Plane3d(V3d.XAxis, new V3d(0.8, 0, 0))
                }, 0.1)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 4 * 4 + 4 * 4 - 4);
        }
        
        [Test]
        public void CanQueryPointsNearPlanes_3()
        {
            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNearPlanes(new[]
                {
                    new Plane3d(V3d.ZAxis, new V3d(0, 0, 0.3)),
                    new Plane3d(V3d.XAxis, new V3d(0.8, 0, 0))
                }, 0.2)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 32 + 32 - 16);
        }
        
        [Test]
        public void CanQueryPointsNotNearPlane_1()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 64);

            var q = new Plane3d(V3d.ZAxis, new V3d(0.5, 0.5, 0.5));

            var ps = pointset.QueryPointsNotNearPlane(q, 0.1).SelectMany(x => x.Positions).ToList();
            Assert.IsTrue(pointset.PointCount > ps.Count);

            var bb = new Box3d(new V3d(0.0, 0.0, 0.4), new V3d(1.0, 1.0, 0.6));
            foreach (var p in ps) Assert.IsTrue(!bb.Contains(p));
        }

        [Test]
        public void CanQueryPointsNotNearPlane_2()
        {
            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNotNearPlane(new Plane3d(V3d.ZAxis, new V3d(0, 0, 0.3)), 0.1)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 64 - 16);
        }

        [Test]
        public void CanQueryPointsNotNearPlane_3()
        {
            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNotNearPlane(new Plane3d(V3d.ZAxis, new V3d(0, 0, 0.3)), 0.2)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 64 - 32);
        }
        
        [Test]
        public void CanQueryPointsNotNearPlanes_1()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 64);

            var q = new[]
            {
                new Plane3d(V3d.ZAxis, new V3d(0.5, 0.5, 0.5)),
                new Plane3d(V3d.XAxis, new V3d(0.7, 0.5, 0.5))
            };

            var ps = pointset.QueryPointsNotNearPlanes(q, 0.1).SelectMany(x => x.Positions).ToList();
            Assert.IsTrue(pointset.PointCount > ps.Count);

            var bb1 = new Box3d(new V3d(0.0, 0.0, 0.4), new V3d(1.0, 1.0, 0.6));
            var bb2 = new Box3d(new V3d(0.6, 0.0, 0.0), new V3d(0.8, 1.0, 1.0));
            foreach (var p in ps) Assert.IsTrue(!bb1.Contains(p) && !bb2.Contains(p));
        }

        [Test]
        public void CanQueryPointsNotNearPlanes_2()
        {
            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNotNearPlanes(new[]
                {
                    new Plane3d(V3d.ZAxis, new V3d(0, 0, 0.3)),
                    new Plane3d(V3d.XAxis, new V3d(0.8, 0, 0))
                }, 0.1)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 64 - (16 + 16 - 4));
        }

        [Test]
        public void CanQueryPointsNotNearPlanes_3()
        {
            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNotNearPlanes(new[]
                {
                    new Plane3d(V3d.ZAxis, new V3d(0, 0, 0.3)),
                    new Plane3d(V3d.XAxis, new V3d(0.8, 0, 0))
                }, 0.2)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 64 - (32 + 32 - 16));
        }

        #endregion

        #region Polygon3d

        [Test]
        public void Polygon3dBoundingBox()
        {
            var poly = new Polygon3d(V3d.OOO, V3d.IOO, V3d.IIO);
            var bb = poly.BoundingBox3d(0.5);

            Assert.IsTrue(bb.Min == new V3d(0.0, 0.0, -0.5));
            Assert.IsTrue(bb.Max == new V3d(1.0, 1.0, 0.5));
        }

        [Test]
        public void CanQueryPointsNearPolygon_1()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 64);

            var q = new Polygon3d(new V3d(0.4, 0.4, 0.5), new V3d(0.6, 0.4, 0.5), new V3d(0.6, 0.6, 0.5), new V3d(0.4, 0.6, 0.5));

            var ps = pointset.QueryPointsNearPolygon(q, 0.1).SelectMany(x => x.Positions).ToList();
            Assert.IsTrue(pointset.PointCount > ps.Count);

            var bb = new Box3d(new V3d(0.4, 0.4, 0.4), new V3d(0.6, 0.6, 0.6));
            foreach (var p in ps) Assert.IsTrue(bb.Contains(p));
        }
        
        [Test]
        public void CanQueryPointsNearPolygon_2()
        {
            var q = new Polygon3d(
                new V3d(.0, .0, .3), new V3d(.25, .0, .3), new V3d(.5, .5, .3), new V3d(.0, .5, .3)
                );

            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNearPolygon(q, 0.1)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 3);
        }

        [Test]
        public void CanQueryPointsNearPolygon_3()
        {
            var q = new Polygon3d(
                new V3d(.0, .0, .3), new V3d(.25, .0, .3), new V3d(.5, .5, .3), new V3d(.0, .5, .3)
                );

            var pc = CreateRegularPointsInUnitCube(4, 1);
            var rs = pc
                .QueryPointsNearPolygon(q, 0.2)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 2 * 3);
        }

        [Test]
        public void CanQueryPointsNearPolygon_4()
        {
            var q = new Polygon3d(
                new V3d(.0, .0, .3), new V3d(.25, .0, .3), new V3d(.5, .5, .3), new V3d(.0, .5, .3)
                );

            var rs = CreateRegularPointsInUnitCube(8, 1)
                .QueryPointsNearPolygon(q, 0.2, -2)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 2 * 3);
        }

        [Test]
        public void CanQueryPointsNearPolygon_Performance()
        {
            var sw = new Stopwatch();
            var pointset = CreateRandomPointsInUnitCube(1024 * 1024, 32);

            var q = new Polygon3d(new V3d(0.4, 0.4, 0.5), new V3d(0.41, 0.4, 0.5), new V3d(0.41, 0.41, 0.5), new V3d(0.4, 0.41, 0.5));
            var plane = new Plane3d(new V3d(0.4, 0.4, 0.5), new V3d(0.41, 0.4, 0.5), new V3d(0.41, 0.41, 0.5));

            sw.Restart();
            var ps0 = pointset.QueryPointsNearPolygon(q, 0.01).SelectMany(x => x.Positions).ToList();
            var t0 = sw.Elapsed.TotalSeconds;

            sw.Restart();
            var ps1 = pointset.QueryPointsNearPlane(plane, 0.01).ToList();
            var t1 = sw.Elapsed.TotalSeconds;

            Assert.IsTrue(t0 * 10 < t1);
        }

        [Test]
        public void CanQueryPointsNearPolygons_1()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 64);

            var q = new[]
            {
                new Polygon3d(new V3d(0.4, 0.4, 0.5), new V3d(0.6, 0.4, 0.5), new V3d(0.6, 0.6, 0.5), new V3d(0.4, 0.6, 0.5)),
                new Polygon3d(new V3d(0.5, 0.4, 0.4), new V3d(0.5, 0.6, 0.4), new V3d(0.5, 0.6, 0.6), new V3d(0.5, 0.4, 0.6))
            };

            var ps = pointset.QueryPointsNearPolygons(q, 0.1).SelectMany(x => x.Positions).ToList();
            Assert.IsTrue(pointset.PointCount > ps.Count);

            var bb1 = new Box3d(new V3d(0.4, 0.4, 0.4), new V3d(0.6, 0.6, 0.6));
            var bb2 = new Box3d(new V3d(0.4, 0.4, 0.4), new V3d(0.6, 0.6, 0.6));
            foreach (var p in ps) Assert.IsTrue(bb1.Contains(p) || bb2.Contains(p));
        }

        [Test]
        public void CanQueryPointsNearPolygons_2()
        {
            var q = new[]
            {
                new Polygon3d(new V3d(.0, .0, .3), new V3d(.25, .0, .3), new V3d(.5, .5, .3), new V3d(.0, .5, .3)),
                new Polygon3d(new V3d(1, 1, .8), new V3d(1, .5, .8), new V3d(.5, .75, .8))
            };

            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNearPolygons(q, 0.1)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 3 + 2);
        }

        [Test]
        public void CanQueryPointsNearPolygons_3()
        {
            var q = new[]
            {
                new Polygon3d(new V3d(.0, .0, .3), new V3d(.25, .0, .3), new V3d(.5, .5, .3), new V3d(.0, .5, .3)),
                new Polygon3d(new V3d(1, 1, .8), new V3d(1, .5, .8), new V3d(.5, .75, .8))
            };

            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNearPolygons(q, 0.2)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 2 * (3 + 2));
        }

        [Test]
        public void CanQueryPointsNotNearPolygon_1()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 64);

            var q = new Polygon3d(new V3d(0.4, 0.4, 0.5), new V3d(0.6, 0.4, 0.5), new V3d(0.6, 0.6, 0.5), new V3d(0.4, 0.6, 0.5));

            var ps = pointset.QueryPointsNotNearPolygon(q, 0.1).SelectMany(x => x.Positions).ToList();
            Assert.IsTrue(pointset.PointCount > ps.Count);

            var bb = new Box3d(new V3d(0.4, 0.4, 0.4), new V3d(0.6, 0.6, 0.6));
            foreach (var p in ps) Assert.IsTrue(!bb.Contains(p));
        }
        
        [Test]
        public void CanQueryPointsNotNearPolygon_2()
        {
            var q = new Polygon3d(
                new V3d(.0, .0, .3), new V3d(.25, .0, .3), new V3d(.5, .5, .3), new V3d(.0, .5, .3)
                );

            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNotNearPolygon(q, 0.1)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 64 - 3);
        }

        [Test]
        public void CanQueryPointsNotNearPolygon_3()
        {
            var q = new Polygon3d(
                new V3d(.0, .0, .3), new V3d(.25, .0, .3), new V3d(.5, .5, .3), new V3d(.0, .5, .3)
                );

            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNotNearPolygon(q, 0.2)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 64 - 2 * 3);
        }
        
        [Test]
        public void CanQueryPointsNotNearPolygons_1()
        {
            var pointset = CreateRandomPointsInUnitCube(1024, 64);

            var q = new[]
            {
                new Polygon3d(new V3d(0.4, 0.4, 0.5), new V3d(0.6, 0.4, 0.5), new V3d(0.6, 0.6, 0.5), new V3d(0.4, 0.6, 0.5)),
                new Polygon3d(new V3d(0.5, 0.4, 0.4), new V3d(0.5, 0.6, 0.4), new V3d(0.5, 0.6, 0.6), new V3d(0.5, 0.4, 0.6))
            };

            var ps = pointset.QueryPointsNotNearPolygons(q, 0.1).SelectMany(x => x.Positions).ToList();
            Assert.IsTrue(pointset.PointCount > ps.Count);

            var bb1 = new Box3d(new V3d(0.4, 0.4, 0.4), new V3d(0.6, 0.6, 0.6));
            var bb2 = new Box3d(new V3d(0.4, 0.4, 0.4), new V3d(0.6, 0.6, 0.6));
            foreach (var p in ps) Assert.IsTrue(!bb1.Contains(p) && !bb2.Contains(p));
        }

        [Test]
        public void CanQueryPointsNotNearPolygons_2()
        {
            var q = new[]
            {
                new Polygon3d(new V3d(.0, .0, .3), new V3d(.25, .0, .3), new V3d(.5, .5, .3), new V3d(.0, .5, .3)),
                new Polygon3d(new V3d(1, 1, .8), new V3d(1, .5, .8), new V3d(.5, .75, .8))
            };

            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNotNearPolygons(q, 0.1)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 64 - (3 + 2));
        }

        [Test]
        public void CanQueryPointsNotNearPolygons_3()
        {
            var q = new[]
            {
                new Polygon3d(new V3d(.0, .0, .3), new V3d(.25, .0, .3), new V3d(.5, .5, .3), new V3d(.0, .5, .3)),
                new Polygon3d(new V3d(1, 1, .8), new V3d(1, .5, .8), new V3d(.5, .75, .8))
            };

            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsNotNearPolygons(q, 0.2)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 64 - (2 * (3 + 2)));
        }

        #endregion

        #region Box3d

        [Test]
        public void CanQueryPointsInsideBox_1()
        {
            var filename = Config.TEST_FILE_NAME_PTS;
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            var config = ImportConfig.Default
                .WithInMemoryStore()
                .WithKey("key1")
                .WithOctreeSplitLimit(16 * 1024)
                .WithReadBufferSizeInBytes(128 * 1024 * 1024)
                ;
            var pointset = PointCloud.Import(filename, config);

            var box = Box3d.FromMinAndSize(new V3d(0.5, 0.5, 0.0), new V3d(0.5, 0.5, 0.5));
            var result = new List<V3d>();
            foreach (var x in pointset.QueryPointsInsideBox(box)) result.AddRange(x.Positions);
            Assert.IsTrue(result.Count > 0 && result.Count < pointset.PointCount);

            var resultBounds = new Box3d(result);
            Assert.IsTrue(box.Contains(resultBounds));
        }

        [Test]
        public void CanQueryPointsInsideBox_2()
        {
            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsInsideBox(Box3d.Unit)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 4 * 4 * 4);
        }

        [Test]
        public void CanQueryPointsOutsideBox_1()
        {
            var rs = CreateRegularPointsInUnitCube(4, 1)
                .QueryPointsOutsideBox(Box3d.Unit)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(rs.Length == 64 - 4 * 4 * 4);
        }

        #endregion

        #region ForEachNodeIntersecting

        [Test]
        public void ForEachNodeIntersecting_Works()
        {
            var storage = PointCloud.CreateInMemoryStore();
            var pointcloud = CreateClusteredPointsInUnitCube(1000, 10);
            var ns = pointcloud.Root.Value.ForEachNodeIntersecting(Hull3d.Create(Box3d.Unit), true).ToArray();
            Assert.IsTrue(ns.Length > 0);
        }

        #endregion

        #region Octree levels

        private static PointSet _CreateRandomPointSetForOctreeLevelTests()
        {
            var r = new Random();
            var storage = PointSetTests.CreateStorage();

            var ps = new V3d[51200].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var cs = ps.Map(_ => C4b.White);

            var config = ImportConfig.Default.WithKey("Test").WithOctreeSplitLimit(1);
            return PointSet
                .Create(storage, "test", ps.ToList(), cs.ToList(), null, null, 100, true, CancellationToken.None)
                .GenerateLod(config)
                ;
        }

        [Test]
        public void QueryOctreeLevel()
        {
            var pointset = _CreateRandomPointSetForOctreeLevelTests();

            var depth = pointset.Root.Value.CountOctreeLevels();
            Assert.IsTrue(depth > 0);

            for (var i = 0; i < depth; i++)
            {
                var countNodes = 0;
                var countPoints = 0;
                foreach (var x in pointset.QueryPointsInOctreeLevel(i)) { countNodes++; countPoints += x.Count; }
                Assert.IsTrue(countPoints > 0);
                Assert.IsTrue(countNodes <= Math.Pow(8, i));
            }
        }

        [Test]
        public void QueryOctreeLevelWithBounds()
        {
            var pointset = _CreateRandomPointSetForOctreeLevelTests();
            var bounds = Box3d.FromMinAndSize(new V3d(0.2, 0.4, 0.8), new V3d(0.2, 0.15, 0.1));

            var depth = pointset.Root.Value.CountOctreeLevels();
            Assert.IsTrue(depth > 0);

            for (var i = 1; i < depth; i++)
            {
                var countNodes0 = 0;
                var countPoints0 = 0;
                var countNodes1 = 0;
                var countPoints1 = 0;
                foreach (var x in pointset.QueryPointsInOctreeLevel(i)) { countNodes0++; countPoints0 += x.Count; }
                foreach (var x in pointset.QueryPointsInOctreeLevel(i, bounds)) { countNodes1++; countPoints1 += x.Count; }
                Assert.IsTrue(countPoints0 > countPoints1);
                Assert.IsTrue(countNodes0 > countNodes1);
            }
        }

        [Test]
        public void QueryOctreeLevel_NegativeLevel()
        {
            var pointset = _CreateRandomPointSetForOctreeLevelTests();

            var depth = pointset.Root.Value.CountOctreeLevels();
            Assert.IsTrue(depth > 0);
            
            foreach (var _ in pointset.QueryPointsInOctreeLevel(-1)) Assert.Fail();
        }

        [Test]
        public void QueryOctreeLevel_StopsAtLeafs()
        {
            var pointset = _CreateRandomPointSetForOctreeLevelTests();

            var depth = pointset.Root.Value.CountOctreeLevels();
            Assert.IsTrue(depth > 0);

            // query octree level depth*2 -> should not crash and give number of original points
            var countNodes = 0;
            var countPoints = 0;
            foreach (var x in pointset.QueryPointsInOctreeLevel(depth * 2)) { countNodes++; countPoints += x.Count; }
            Assert.IsTrue(countPoints == 51200);
        }

        [Test]
        public void CountPointsInOctreeLevel()
        {
            var pointset = _CreateRandomPointSetForOctreeLevelTests();

            var depth = pointset.Root.Value.CountOctreeLevels();
            Assert.IsTrue(depth > 0);

            var countPoints = 0L;
            for (var i = 0; i < depth; i++)
            {
                var c = pointset.CountPointsInOctreeLevel(i);
                Assert.IsTrue(c > countPoints);
                countPoints = c;
            }
        }

        [Test]
        public void CountPointsInOctreeLevelWithBounds()
        {
            var pointset = _CreateRandomPointSetForOctreeLevelTests();
            var bounds = Box3d.FromMinAndSize(new V3d(0.2, 0.4, 0.8), new V3d(0.2, 0.15, 0.1));

            var depth = pointset.Root.Value.CountOctreeLevels();
            Assert.IsTrue(depth > 0);
            
            for (var i = 1; i < depth; i++)
            {
                var c0 = pointset.CountPointsInOctreeLevel(i);
                var c1 = pointset.CountPointsInOctreeLevel(i, bounds);
                Assert.IsTrue(c0 > c1);
            }
        }

        [Test]
        public void CountPointsInOctreeLevel_StopsAtLeafs()
        {
            var pointset = _CreateRandomPointSetForOctreeLevelTests();

            var depth = pointset.Root.Value.CountOctreeLevels();
            Assert.IsTrue(depth > 0);

            // query point count at level depth*2 -> should not crash and give number of original points
            var countPoints = pointset.CountPointsInOctreeLevel(depth * 2);
            Assert.IsTrue(countPoints == 51200);
        }

        [Test]
        public void CountPointsInOctreeLevel_NegativeLevel()
        {
            var pointset = _CreateRandomPointSetForOctreeLevelTests();
            
            var countPoints = pointset.CountPointsInOctreeLevel(-1);
            Assert.IsTrue(countPoints == 0);
        }

        [Test]
        public void GetMaxOctreeLevelWithLessThanGivenPointCount()
        {
            var pointset = _CreateRandomPointSetForOctreeLevelTests();

            var depth = pointset.Root.Value.CountOctreeLevels();
            Assert.IsTrue(depth > 0);

            var l0 = pointset.GetMaxOctreeLevelWithLessThanGivenPointCount(0);
            Assert.IsTrue(l0 == -1);

            var l1 = pointset.GetMaxOctreeLevelWithLessThanGivenPointCount(100);
            Assert.IsTrue(l1 == -1);

            var l2 = pointset.GetMaxOctreeLevelWithLessThanGivenPointCount(101);
            Assert.IsTrue(l2 == 0);

            var l3 = pointset.GetMaxOctreeLevelWithLessThanGivenPointCount(800);
            Assert.IsTrue(l3 == 0);

            var l4 = pointset.GetMaxOctreeLevelWithLessThanGivenPointCount(801);
            Assert.IsTrue(l4 == 1);

            var l5 = pointset.GetMaxOctreeLevelWithLessThanGivenPointCount(51200);
            Assert.IsTrue(l5 == depth - 2);

            var l6 = pointset.GetMaxOctreeLevelWithLessThanGivenPointCount(51201);
            Assert.IsTrue(l6 == depth - 1);
        }

        #endregion

        #region QueryPoints (generic query traversal, base for most other queries)

        [Test]
        public void CanQueryPointsWithEverythingInside_Single()
        {
            var storage = PointCloud.CreateInMemoryStore();
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var root = InMemoryPointSet.Build(ps, null, null, null, Cell.Unit, 1).ToPointSetCell(storage, ct: CancellationToken.None);

            var rs = root.QueryPoints(cell => true, cell => false, p => true).SelectMany(x => x.Positions).ToArray();
            Assert.IsTrue(rs.Length == 1);
            Assert.IsTrue(rs[0] == new V3d(0.5, 0.5, 0.5));
        }

        [Test]
        public void CanQueryPointsWithEverythingInside_Many()
        {
            var root = CreateRegularPointsInUnitCube(4, 1).Root.Value;
            Assert.IsTrue(root.PointCountTree == 4 * 4 * 4);

            var rs1 = root.QueryPoints(cell => true, cell => false, p => true).SelectMany(x => x.Positions).ToArray();
            Assert.IsTrue(rs1.Length == 4 * 4 * 4);

            var rs2 = root.QueryPoints(cell => false, cell => false, p => true).SelectMany(x => x.Positions).ToArray();
            Assert.IsTrue(rs2.Length == 4 * 4 * 4);
        }

        [Test]
        public void CanQueryPointsWithEverythingOutside_Single()
        {
            var storage = PointCloud.CreateInMemoryStore();
            var ps = new List<V3d> { new V3d(0.5, 0.5, 0.5) };
            var root = InMemoryPointSet.Build(ps, null, null, null, Cell.Unit, 1).ToPointSetCell(storage, ct: CancellationToken.None);

            var rs = root.QueryPoints(cell => false, cell => true, p => false).SelectMany(x => x.Positions).ToArray();
            Assert.IsTrue(rs.Length == 0);
        }

        [Test]
        public void CanQueryPointsWithEverythingOutside_Many()
        {
            var root = CreateRegularPointsInUnitCube(4, 1).Root.Value;
            Assert.IsTrue(root.PointCountTree == 4 * 4 * 4);

            var rs1 = root.QueryPoints(cell => false, cell => true, p => false).SelectMany(x => x.Positions).ToArray();
            Assert.IsTrue(rs1.Length == 0);

            var rs2 = root.QueryPoints(cell => false, cell => false, p => false).SelectMany(x => x.Positions).ToArray();
            Assert.IsTrue(rs2.Length == 0);
        }

        #endregion
    }
}
