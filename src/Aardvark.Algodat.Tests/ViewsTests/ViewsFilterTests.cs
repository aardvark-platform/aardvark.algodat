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
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Immutable;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ViewsFilterTests
    {
        private static readonly Random r = new();
        private static V3f RandomPosition() => new(r.NextDouble(), r.NextDouble(), r.NextDouble());
        private static V3f[] RandomPositions(int n) => new V3f[n].SetByIndex(_ => RandomPosition());
        private static int[] RandomIntensities(int n) => new int[n].SetByIndex(_ => -999 + r.Next(1998));

        private static IPointCloudNode CreateNode(Storage storage, V3f[] psGlobal, int[] intensities = null)
        {
            var id = Guid.NewGuid();
            var cell = new Cell(psGlobal);
            var center = (V3f)cell.GetCenter();
            var bbGlobal = (Box3d)new Box3f(psGlobal);
            var bbLocal = (Box3f)bbGlobal - center;

            var psLocal = psGlobal.Map(p => p - center);

            var psLocalId = Guid.NewGuid();
            storage.Add(psLocalId, psLocal);

            var kdLocal = psLocal.BuildKdTree();
            var kdLocalId = Guid.NewGuid();
            storage.Add(kdLocalId, kdLocal.Data);

            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.NodeId, id)
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.BoundingBoxExactGlobal, bbGlobal)
                .Add(Durable.Octree.BoundingBoxExactLocal, bbLocal)
                .Add(Durable.Octree.PointCountTreeLeafs, psLocal.LongLength)
                .Add(Durable.Octree.PositionsLocal3fReference, psLocalId)
                .Add(Durable.Octree.PointRkdTreeFDataReference, kdLocalId)
                .Add(Durable.Octree.PerCellPartIndex1i, 42)
                .Add(Durable.Octree.PartIndexRange, new Range1i(42, 42))
                ;

            if (intensities != null)
            {
                var jsId = Guid.NewGuid();
                storage.Add(jsId, intensities);
                data = data.Add(Durable.Octree.Intensities1iReference, jsId);
            }

            var result = new PointSetNode(data, storage, writeToStore: true);
            return result;
        }

        private static void CheckPartIndices(IPointCloudNode n)
        {
            if (n.TryGetPartIndices(out var qs))
            {
                ClassicAssert.True(qs.Length == n.PointCountCell);
                ClassicAssert.True(qs.All(q => q == 42));
            }
            else
            {
                Assert.Fail("Node has no part indices.");
            }
        }

        #region FilterInsideBox3d

        [Test]
        public void FilterInsideBox3d_AllInside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterInsideBox3d(a.BoundingBoxExactGlobal));
            ClassicAssert.IsTrue(f.HasPositions);
            var ps = f.PositionsAbsolute;
            ClassicAssert.IsTrue(ps.Length == 100);

            CheckPartIndices(f);
        }

        [Test]
        public void FilterInsideBox3d_AllOutside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterInsideBox3d(a.BoundingBoxExactGlobal + V3d.IOO));
            ClassicAssert.IsTrue(f.PointCountCell == 0);

            CheckPartIndices(f);
        }

        [Test]
        public void FilterInsideBox3d_StoreAddGet()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterInsideConvexHull3d(new Hull3d(a.BoundingBoxExactGlobal + V3d.IOO * 0.1)));
            var g = f.Id;
            var k = g.ToString();
            storage.Add(k, f);
            //var data = storage.GetByteArray(k);
            //var f2 = FilteredNode.Decode(storage, data);

            var ps = storage.GetPointCloudNode(g);

            ClassicAssert.IsTrue(ps.Id == f.Id);

            var f2 = (FilteredNode)ps;
            var f1 = (FilteredNode)f;
            ClassicAssert.IsTrue(f2.Node.Id == f1.Node.Id); // how to compare nodes structurally?
            var f2s = f2.Filter.Serialize().ToString();
            var f1s = f1.Filter.Serialize().ToString();
            ClassicAssert.IsTrue(f2s == f1s); // how to compare filters structurally ?
        }

        [Test]
        public void FilterInsideBox3d_Partial()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterInsideBox3d(new Box3d(new V3d(0, 0, 0), new V3d(1, 1, 0.5))));
            ClassicAssert.IsTrue(f.HasPositions);
            var ps = f.PositionsAbsolute;
            var count = ps.Count(p => p.Z <= 0.5);
            ClassicAssert.IsTrue(ps.Length == count);

            CheckPartIndices(f);
        }

        #endregion

        #region FilterInsideBox3d

        [Test]
        public void FilterOutsideBox3d_AllInside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterOutsideBox3d(a.BoundingBoxExactGlobal + V3d.IOO));
            ClassicAssert.IsTrue(f.HasPositions);
            var ps = f.PositionsAbsolute;
            ClassicAssert.IsTrue(ps.Length == 100);

            CheckPartIndices(f);
        }

        [Test]
        public void FilterOutsideBox3d_AllOutside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterOutsideBox3d(a.BoundingBoxExactGlobal));
            ClassicAssert.IsTrue(f.PointCountCell == 0);

            CheckPartIndices(f);
        }

        [Test]
        public void FilterOutsideBox3d_Partial()
        {
            using var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, new[] { -.25f, 0, .25f, .5f, .75f, 1 }
                .Select(z => new V3f(.25f, .25f, z)).ToArray());
            var box = new Box3d(V3d.Zero, new V3d(1, 1, .5));
            var expected = a.PositionsAbsolute.Where(p => !ClosedBoxContains(box, p)).ToArray();
            var f = FilteredNode.Create(a, new FilterOutsideBox3d(box));
            Assert.That(expected.Length, Is.EqualTo(3));
            Assert.That(f.PositionsAbsolute, Is.EqualTo(expected), "Both Z boundaries are excluded; source order is retained.");
            CheckPartIndices(f);
        }

        private static bool ClosedBoxContains(Box3d box, V3d p) =>
            p.X >= box.Min.X && p.X <= box.Max.X &&
            p.Y >= box.Min.Y && p.Y <= box.Max.Y &&
            p.Z >= box.Min.Z && p.Z <= box.Max.Z;

        [Test]
        public void FilterOutsideBox3d_SelectionDomainsExcludeClosedBoundaries()
        {
            foreach (var offset in new[] { V3d.Zero, new V3d(8, -4, 2) })
            {
                using var storage = PointCloud.CreateInMemoryStore(cache: default);
                var values = new[] { -.25, 0, .5, 1, 1.25 };
                var positions = Enumerable.Range(0, 125).Select(i =>
                {
                    var j = i * 53 % 125;
                    return (V3f)(offset + new V3d(values[j / 25], values[j / 5 % 5], values[j % 5]));
                }).ToArray();
                var node = CreateNode(storage, positions);
                var stored = node.PositionsAbsolute;
                var original = node.Positions.Value.ToArray();
                foreach (var box in new[] { new Box3d(offset, offset + V3d.III),
                    new Box3d(offset + new V3d(0, 0, .5), offset + new V3d(1, 1, .5)),
                    new Box3d(offset + new V3d(.5), offset + new V3d(.5)),
                    new Box3d(offset + new V3d(2), offset + new V3d(3)),
                    new Box3d(offset - V3d.III, offset + new V3d(2)) })
                {
                    var filter = new FilterOutsideBox3d(box);
                    foreach (var selected in new HashSet<int>[] { null, new(),
                        new(Enumerable.Range(0, stored.Length)),
                        new(Enumerable.Range(0, stored.Length).Reverse().Where(i => i % 3 != 1)),
                        new(Enumerable.Range(0, stored.Length).Where(i => ClosedBoxContains(box, stored[i]))),
                        new(Enumerable.Range(0, stored.Length).Where(i => !ClosedBoxContains(box, stored[i]))) })
                    {
                        var before = selected?.ToArray();
                        var domain = selected ?? new HashSet<int>(Enumerable.Range(0, stored.Length));
                        var expected = domain.Where(i => !ClosedBoxContains(box, stored[i])).ToArray();
                        var actual = filter.FilterPoints(node, selected);
                        var context = $"offset={offset}, box={box}, domain={(selected == null ? "all" : string.Join(',', before))}";
                        Assert.That(actual.OrderBy(i => i), Is.EqualTo(expected.OrderBy(i => i)), context);
                        if (selected != null)
                        {
                            Assert.That(actual, Is.Not.SameAs(selected), context);
                            Assert.That(selected.ToArray(), Is.EqualTo(before), context);
                        }
                        Assert.That(node.Positions.Value, Is.EqualTo(original), context);
                    }
                    foreach (var p in stored)
                        Assert.That(filter.Contains(p), Is.EqualTo(!ClosedBoxContains(box, p)), $"box={box}, position={p}");
                }
            }
        }

        [Test]
        public void FilterOutsideBox3d_WholeNodeAcceptanceRequiresStrictSeparation()
        {
            var filter = new FilterOutsideBox3d(Box3d.Unit);
            foreach (var x in new[] { -1, 0, 1 })
            foreach (var y in new[] { -1, 0, 1 })
            foreach (var z in new[] { -1, 0, 1 })
            {
                if (x == 0 && y == 0 && z == 0) continue;
                double Min(int side) => side < 0 ? -1 : side > 0 ? 1 : .25;
                double Max(int side) => side < 0 ? 0 : side > 0 ? 2 : .75;
                var touching = new Box3d(new V3d(Min(x), Min(y), Min(z)), new V3d(Max(x), Max(y), Max(z)));
                Assert.That(filter.IsFullyInside(touching), Is.False, $"Touching face/edge/corner: {touching}");
                Assert.That(filter.IsFullyOutside(touching), Is.False, $"Partly outside: {touching}");
                var shift = .125 * new V3d(x, y, z);
                var separated = new Box3d(touching.Min + shift, touching.Max + shift);
                Assert.That(filter.IsFullyInside(separated), Is.True, $"Strictly separated: {separated}");
            }
            foreach (var x in new[] { 0.0, .5, 1 })
            foreach (var y in new[] { 0.0, .5, 1 })
            foreach (var z in new[] { 0.0, .5, 1 })
            {
                var p = new V3d(x, y, z);
                var degenerate = new Box3d(p, p);
                Assert.That(filter.IsFullyInside(degenerate), Is.False, $"Closed-box point bounds: {p}");
                Assert.That(filter.IsFullyOutside(degenerate), Is.True, $"Closed-box point bounds: {p}");
            }
            Assert.That(filter.IsFullyInside(Box3d.Unit), Is.False);
            Assert.That(filter.IsFullyOutside(Box3d.Unit), Is.True);
            Assert.That(filter.IsFullyInside(new Box3d(new V3d(-1), new V3d(2))), Is.False);
            Assert.That(filter.IsFullyOutside(new Box3d(new V3d(-1), new V3d(2))), Is.False);
        }

        [Test]
        public void FilterOutsideBox3d_AttributedViewsAndReloadsKeepOutsidePointsInSourceOrder()
        {
            var leaf = new[] { new V3d(-.25, .5, .5), new V3d(0, .5, .5), new V3d(.25, .5, .5),
                new V3d(1, .5, .5), new V3d(1.25, .5, .5), new V3d(.5, 1.25, .5) };
            var grid = Enumerable.Range(0, 343).Select(i =>
                new V3d((i / 49 - 1) * .25, (i / 7 % 7 - 1) * .25, (i % 7 - 1) * .25)).ToArray();
            foreach (var positions in new[] { leaf, grid })
            {
                using var storage = PointCloud.CreateInMemoryStore(cache: new LruDictionary<string, object>(1L << 24));
                var ids = Enumerable.Range(0, positions.Length).ToArray();
                var chunk = new Chunk(positions,
                    ids.Select(i => new C4b((byte)i, (byte)(i >> 8), (byte)(255 - i % 256), (byte)255)).ToArray(),
                    ids.Select(i => i % 3 == 0 ? V3f.IOO : i % 3 == 1 ? V3f.OIO : V3f.OOI).ToArray(),
                    ids.Select(i => 1000 + i).ToArray(), ids.Select(i => (byte)(i % 31)).ToArray(),
                    ids.Select(i => 20000 + i).ToArray(), null, null);
                var set = PointCloud.Chunks(chunk, ImportConfig.Default.WithStorage(storage).WithKey("outside")
                    .WithOctreeSplitLimit(positions.Length == leaf.Length ? 64 : 4));
                var source = set.Root.Value;
                Assert.That(source.IsLeaf(), Is.EqualTo(positions.Length == leaf.Length));
                // Read expected values from stored absolute coordinates and aligned stored attributes.
                (V3d Position, C4b Color, V3f Normal, int Intensity, byte Classification, int Part)[] Rows(IPointCloudNode node)
                    => node.QueryAllPoints().SelectMany(c =>
                    {
                        var parts = c.TryGetPartIndices();
                        return Enumerable.Range(0, c.Count).Select(i =>
                            (c.Positions[i], c.Colors[i], c.Normals[i], c.Intensities[i], c.Classifications[i], parts[i]));
                    }).ToArray();
                var rows = Rows(source);
                Assert.That(rows.Length, Is.EqualTo(positions.Length));
                foreach (var box in new[] { Box3d.Unit, new Box3d(new V3d(.49), new V3d(.51)),
                    new Box3d(new V3d(0, 0, .5), new V3d(1, 1, .5)),
                    new Box3d(new V3d(.5), new V3d(.5)), new Box3d(new V3d(2), new V3d(3)),
                    new Box3d(new V3d(-1), new V3d(2)) })
                {
                    var expected = rows.Where(row => !ClosedBoxContains(box, row.Position)).ToArray();
                    var filter = new FilterOutsideBox3d(box);
                    var view = FilteredNode.Create(source, filter);
                    var viewId = view.Id;
                    var serialized = filter.Serialize().ToJsonString();
                    var context = $"points={positions.Length}, box={box}";
                    Assert.That(Rows(view), Is.EqualTo(expected), context);
                    Assert.That(view.PositionsAbsolute, Is.EqualTo(source.PositionsAbsolute.Where(p => !ClosedBoxContains(box, p))), context);
                    for (var round = 0; round < 2; round++)
                    {
                        storage.Cache.Clear();
                        var reloaded = (FilteredNode)storage.GetPointCloudNode(viewId);
                        Assert.That(Rows(reloaded), Is.EqualTo(expected), $"reload={round}, {context}");
                        Assert.That(reloaded.Filter.Serialize().ToJsonString(), Is.EqualTo(serialized), context);
                        Assert.That(reloaded.Filter.Equals(filter), Is.True, context);
                    }
                    Assert.That(Rows(source), Is.EqualTo(rows), $"Source unchanged: {context}");
                }
            }
        }

        #endregion

        #region FilterIntensity

        [Test]
        public void FilterIntensity_AllInside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var intensities = RandomIntensities(100);
            var a = CreateNode(storage, RandomPositions(100), intensities);

            var f = FilteredNode.Create(a, new FilterIntensity(new Range1i(-1000, +1000)));
            ClassicAssert.IsTrue(f.HasIntensities);
            var js = f.Intensities.Value;
            ClassicAssert.IsTrue(js.Length == 100);

            CheckPartIndices(f);
        }
        
        [Test]
        public void FilterIntensity_AllOutside()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100), RandomIntensities(100));

            var f = FilteredNode.Create(a, new FilterIntensity(new Range1i(-30000, -10000)));
            ClassicAssert.IsTrue(f.PointCountCell == 0);

            CheckPartIndices(f);
        }

        [Test]
        public void FilterIntensity_Partial()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var intensities = RandomIntensities(100);
            intensities[17] = 10000;
            intensities[42] = 20000;
            var a = CreateNode(storage, RandomPositions(100), intensities);

            var f = FilteredNode.Create(a, new FilterIntensity(new Range1i(10000, 30000)));
            ClassicAssert.IsTrue(f.HasIntensities);
            var js = f.Intensities.Value;
            ClassicAssert.IsTrue(js.Length == 2);
            ClassicAssert.IsTrue(js[0] == 10000);
            ClassicAssert.IsTrue(js[1] == 20000);

            CheckPartIndices(f);
        }

        #endregion

        #region Serialization

        [Test]
        public void Serialize_FilterInsideBox3d()
        {
            var f = new FilterInsideBox3d(Box3d.Unit);
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterOutsideBox3d()
        {
            var f = new FilterOutsideBox3d(Box3d.Unit);
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterInsideConvexHull3d()
        {
            var f = new FilterInsideConvexHull3d(new Hull3d(Box3d.Unit));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterInsideConvexHulls3d_A()
        {
            var f = new FilterInsideConvexHulls3d(new Hull3d(Box3d.Unit), new Hull3d(Box3d.Unit.Translated(new V3d(-1, 3.14, 12345.67))));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterInsideConvexHulls3d_B()
        {
            var f = new FilterInsideConvexHulls3d(Box2d.Unit.ToPolygon2dCCW(), Range1d.Unit, Trafo3d.Translation(1,2,-3));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterInsidePrismXY()
        {
            var f = new FilterInsidePrismXY(Box2d.Unit.ToPolygon2dCCW(), Range1d.Unit);
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterInsideSphere3d()
        {
            var f = new FilterInsideSphere3d(new Sphere3d(new V3d(1,2,3), 4));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterClassification()
        {
            var f = new FilterClassification(new byte[] { 1, 2, 3, 4, 5 });
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterIntensity()
        {
            var f = new FilterIntensity(new Range1i(-5, +17));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterNormalDirection()
        {
            var f = new FilterNormalDirection(V3f.ZAxis, 0.1f);
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterOr()
        {
            var f = new FilterOr(new FilterInsideBox3d(Box3d.Unit), new FilterOutsideBox3d(Box3d.Unit));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }
        [Test]
        public void Serialize_FilterAnd()
        {
            var f = new FilterAnd(new FilterInsideBox3d(Box3d.Unit), new FilterOutsideBox3d(Box3d.Unit));
            var json = f.Serialize().ToString();
            var g = Filter.Deserialize(json);
            ClassicAssert.True(f.Equals(g));
        }

        #endregion

        #region FilteredNode

        [Test]
        public void EncodeDecodeRoundtrip()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = (FilteredNode)FilteredNode.Create(a, new FilterInsideBox3d(a.BoundingBoxExactGlobal + new V3d(0.5, 0.0, 0.0)));
            var buffer = ((IPointCloudNode)f).Encode();
            ClassicAssert.IsTrue(buffer != null);

            CheckPartIndices(f);

            var g = FilteredNode.Decode(storage, buffer);
            ClassicAssert.IsTrue(f.Id == g.Id);
            ClassicAssert.IsTrue(f.Node.Id == g.Node.Id);

            CheckPartIndices(g);

            var fFilterJson = f.Filter.Serialize().ToString();
            var gFilterJson = g.Filter.Serialize().ToString();
            ClassicAssert.IsTrue(fFilterJson == gFilterJson);
        }

        #endregion

        #region Delete

        [Test]
        public void CanDeletePoints()
        {
            var q = new Box3d(new V3d(0.3), new V3d(0.7));
            var q1 = new Box3d(new V3d(0.4), new V3d(0.6));

            var a = DeleteTests.CreateRegularPointsInUnitCube(10, 1024).Root.Value;
            var store = ((PointSetNode)a).Storage;
            a.ForEachNode(true, n =>
            {
                ClassicAssert.IsTrue(store.GetPointCloudNode(n.Id) != null);
            });

            var f = FilteredNode.Create(a, new FilterInsideBox3d(q));

            var b = f.Delete(
                n => q1.Contains(n.BoundingBoxExactGlobal),
                n => !(q1.Contains(n.BoundingBoxExactGlobal) || q1.Intersects(n.BoundingBoxExactGlobal)),
                p => q1.Contains(p), a.Storage, default, 1024);

            ClassicAssert.IsTrue(a.PointCountTree > b.PointCountTree);

            ClassicAssert.IsTrue(!b.QueryAllPoints().SelectMany(chunk => chunk.Positions).Any(p => q1.Contains(p)));

            ClassicAssert.IsTrue(b.HasCentroidLocal);

            CheckPartIndices(f);
        }

        #endregion
    }
}
