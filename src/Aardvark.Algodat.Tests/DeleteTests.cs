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
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static Aardvark.Base.MultimethodTest;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class DeleteTests
    {
        public static PointSet CreateRandomPointsInUnitCube(int n, int splitLimit)
        {
            var r = new Random();
            var ps = new V3d[n];
            for (var i = 0; i < n; i++)
            {
                ref var p = ref ps[i];
                p.X = r.NextDouble();
                p.Y = r.NextDouble();
                p.Z = r.NextDouble();
            }
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(splitLimit)
                ;
            var chunk = new Chunk(ps);
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            return PointCloud.Chunks(chunk, config);
        }

        public static PointSet CreateRegularPointsInUnitCube(int n, int splitLimit)
        {
            var ps = new List<V3d>();
            var step = 1.0 / n;
            var start = step * 0.5;
            for (var x = start; x < 1.0; x += step)
                for (var y = start; y < 1.0; y += step)
                    for (var z = start; z < 1.0; z += step)
                        ps.Add(new V3d(x, y, z));
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(splitLimit)
                ;
            var chunk = new Chunk(ps);
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            return PointCloud.Chunks(chunk, config);
        }

        public static PointSet CreateRandomClassifiedPoints(int n, int splitLimit)
        {
            var ps = new List<V3d>();
            var ks = new List<byte>();
            var rand = new RandomSystem();
            for (var x = 0; x < n; x++)
            {
                ps.Add(rand.UniformV3d());
                ks.Add((byte)rand.UniformInt(4));
            }
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("testaa")
                .WithOctreeSplitLimit(splitLimit)
                ;
            var chunk = new Chunk(ps, null, null, null, ks, null, null, null);
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            return PointCloud.Chunks(chunk, config);
        }
        public static PointSet CreateRandomPointsWithPartIndices(int n, int splitLimit)
        {
            var ps = new List<V3d>();
            var pis = new List<int>();
            var rand = new RandomSystem();
            for (var x = 0; x < n; x++)
            {
                ps.Add(rand.UniformV3d());
                pis.Add(rand.UniformInt(4));
            }
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("testaa")
                .WithOctreeSplitLimit(splitLimit)
                .WithEnabledPartIndices(true)
                ;
            var chunk = new Chunk(ps, null, null, null, null, pis.ToArray(), new Range1i(0,4), null);
            return PointCloud.Chunks(chunk, config);
        }

        [Test]
        public void DeleteCollapsesNodes()
        {
            var q = new Box3d(new V3d(0.25), new V3d(1.0));
            var a = CreateRegularPointsInUnitCube(21, 8192);
            a.ValidateTree();

            var b = a.Delete(n => q.Contains(n.BoundingBoxExactGlobal), n => !(q.Contains(n.BoundingBoxExactGlobal) || q.Intersects(n.BoundingBoxExactGlobal)), p => q.Contains(p), a.Storage, CancellationToken.None);

            Console.WriteLine("{0}", b.PointCount);
            ClassicAssert.IsNotNull(b.Root);
            ClassicAssert.IsNotNull(b.Root.Value);
            ClassicAssert.IsTrue(b.Root.Value.IsLeaf);
            b.ValidateTree();
        }


        [Test]
        public void CanDeletePoints()
        {
            var q = new Box3d(new V3d(0.3), new V3d(0.7));

            var a = CreateRegularPointsInUnitCube(10, 1);
            ClassicAssert.IsTrue(a.QueryAllPoints().SelectMany(chunk => chunk.Positions).Any(p => q.Contains(p)));

            var b = a.Delete(n => q.Contains(n.BoundingBoxExactGlobal), n => !(q.Contains(n.BoundingBoxExactGlobal) || q.Intersects(n.BoundingBoxExactGlobal)), p => q.Contains(p), a.Storage, CancellationToken.None);
            ClassicAssert.IsTrue(b.Root?.Value.NoPointIn(p => q.Contains(p)));
            ClassicAssert.IsTrue(a.PointCount > b.PointCount);
            ClassicAssert.IsTrue(!b.QueryAllPoints().SelectMany(chunk => chunk.Positions).Any(p => q.Contains(p)));
            b.ValidateTree();
        }

        [Test]
        public void DeleteNothing()
        {
            var a = CreateRegularPointsInUnitCube(10, 1);
            var b = a.Delete(n => false, n => true, p => false, a.Storage, CancellationToken.None);

            b.ValidateTree();
            ClassicAssert.IsTrue(a.PointCount == b.PointCount);
            ClassicAssert.IsTrue(a.Id != b.Id);
        }

        [Test]
        public void DeleteDelete()
        {
            for (int i = 0; i < 1; i++)
            {
                var q1 = new Box3d(new V3d(0.0), new V3d(0.1));
                var a = CreateRandomPointsInUnitCube(50000, 1024);
                
                // 1. delete a subset of points
                var b = a.Delete(n => q1.Contains(n.BoundingBoxExactGlobal), n => !(q1.Contains(n.BoundingBoxExactGlobal) || q1.Intersects(n.BoundingBoxExactGlobal)), p => q1.Contains(p), a.Storage, CancellationToken.None);
                b.ValidateTree();
                ClassicAssert.IsTrue(b.Root?.Value.NoPointIn(p => q1.Contains(p)));

                // 2. delete ALL remaining points
                var c = b.Delete(n => true, n => false, p => true, a.Storage, CancellationToken.None);
                // if all points are deleted, then 'Delete' returns null
                ClassicAssert.Null(c);
            }
        }


        [Test]
        public void DeleteAllButOne()
        {
            var a = CreateRegularPointsInUnitCube(2, 8);

            var q1 = new Box3d(new V3d(0.0), new V3d(0.5));
            var b = 
                a.Delete(
                    n => false, 
                    n => false, 
                    p => !q1.Contains(p), 
                    a.Storage, 
                    CancellationToken.None
                );

            ClassicAssert.IsTrue(b.Root?.Value.NoPointIn(p => !q1.Contains(p)));
            b.ValidateTree();
            ClassicAssert.IsTrue(b.PointCount == 1);
            ClassicAssert.IsTrue(a.Id != b.Id);
        }

        [Test]
        public void DeleteAll()
        {
            var a = CreateRegularPointsInUnitCube(10, 1);
            var b = a.Delete(n => true, n => false, p => true, a.Storage, CancellationToken.None);

            // if all points are deleted, then 'Delete' returns null
            ClassicAssert.Null(b);
        }

        [Test]
        public void DeleteWithClassifications()
        {
            for (int i = 0; i < 10; i++)
            {
                var q1 = new Box3d(new V3d(0.0), new V3d(0.23));
                var a = CreateRandomClassifiedPoints(10000, 256);
                var b = a.Delete(n => q1.Contains(n.BoundingBoxExactGlobal), n => !(q1.Contains(n.BoundingBoxExactGlobal) || q1.Intersects(n.BoundingBoxExactGlobal)), p => q1.Contains(p), a.Storage, CancellationToken.None);
                b.ValidateTree();
                ClassicAssert.IsTrue(b.Root?.Value.NoPointIn(p => q1.Contains(p)));
                var c = b.Root?.Value.Delete(n => false, n => false, (p,att) => att.Classification==0, a.Storage, CancellationToken.None,256);
                // Did it really delete the classification 0u?
                c.ForEachNode(false, (node) => node.Classifications?.Value.ForEach((k) => ClassicAssert.IsTrue(k != 0)));
            }
        }
        [Test]
        public void DeleteWithPartIndices()
        {
            for (int i = 0; i < 10; i++)
            {
                var q1 = new Box3d(new V3d(0.0), new V3d(0.23));
                var a = CreateRandomPointsWithPartIndices(10000, 256);
                var b = a.Delete(n => q1.Contains(n.BoundingBoxExactGlobal), n => !(q1.Contains(n.BoundingBoxExactGlobal) || q1.Intersects(n.BoundingBoxExactGlobal)), p => q1.Contains(p), a.Storage, CancellationToken.None);
                b.ValidateTree();
                ClassicAssert.IsTrue(b.Root?.Value.NoPointIn(p => q1.Contains(p)));
                var c = b.Root?.Value.Delete(n => false, n => false, (p, att) => att.PartIndex == 1, a.Storage, CancellationToken.None, 256);
                // Did it really delete the partIndex 1?
                Action<IPointCloudNode> test =
                    (node) =>
                    {
                        node.TryGetPartIndices(out int[] indices);
                        indices.ForEach((pi) => ClassicAssert.IsFalse(pi == 1));
                    };
                c.ForEachNode(false, test);
            }
        }
    }
}
