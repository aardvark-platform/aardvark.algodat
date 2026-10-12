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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ViewsFilterTests
    {
        private static readonly Random r = new();
        private static readonly Durable.Def CustomIntAttribute = new(
            new Guid("ef96d640-cb31-4b4a-9a01-4c0bf17f52b1"),
            "Tests.CustomIntAttribute", "Custom per-point integer values.",
            Durable.Primitives.Int32Array.Id, true
            );
        private static readonly Durable.Def CustomVectorAttribute = new(
            new Guid("21877b7e-aee6-47fe-b12f-28668d7fca6c"),
            "Tests.CustomVectorAttribute", "Custom per-point vector values.",
            Durable.Aardvark.V3fArray.Id, true
            );

        private static V3f RandomPosition() => new(r.NextDouble(), r.NextDouble(), r.NextDouble());
        private static V3f[] RandomPositions(int n) => new V3f[n].SetByIndex(_ => RandomPosition());
        private static int[] RandomIntensities(int n) => new int[n].SetByIndex(_ => -999 + r.Next(1998));

        private static IPointCloudNode CreateNode(
            Storage storage,
            V3f[] psGlobal,
            int[] intensities = null,
            IReadOnlyDictionary<Durable.Def, object> properties = null
            )
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

            if (properties != null) data = data.AddRange(properties);

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
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var a = CreateNode(storage, RandomPositions(100));

            var f = FilteredNode.Create(a, new FilterOutsideBox3d(new Box3d(new V3d(0, 0, 0), new V3d(1, 1, 0.5))));
            ClassicAssert.IsTrue(f.HasPositions);
            var ps = f.PositionsAbsolute;
            var count = ps.Count(p => p.Z <= 0.5);
            ClassicAssert.IsTrue(ps.Length == count);

            CheckPartIndices(f);
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
        public void CustomAttributes_AllInsideUseBackingValues()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var positions = CustomAttributePositions();
            var (integers, vectors) = CustomAttributeValues();
            var source = CreateNode(storage, positions, properties: CustomProperties(integers, vectors));
            var filtered = FilteredNode.CreateTransient(source, new FilterInsideBox3d(source.BoundingBoxExactGlobal));

            ClassicAssert.AreSame(source.Properties, filtered.Properties);
            ClassicAssert.IsTrue(filtered.TryGetValue(CustomIntAttribute, out var integerValue));
            ClassicAssert.IsTrue(filtered.TryGetValue(CustomVectorAttribute, out var vectorValue));
            ClassicAssert.AreSame(integers, integerValue);
            ClassicAssert.AreSame(vectors, vectorValue);
        }

        [Test]
        public void CustomAttributes_PartialAndEmptyStayAligned()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var positions = CustomAttributePositions();
            var (integers, vectors) = CustomAttributeValues();
            var source = CreateNode(storage, positions, properties: CustomProperties(integers, vectors));

            var partial = FilteredNode.CreateTransient(source, new FilterInsideBox3d(
                new Box3d(new V3d(0.3, -1.0, -1.0), new V3d(0.8, 1.0, 1.0))
                ));
            AssertCustomAttributes(partial, new[] { 40, 70 }, new[] { vectors[1], vectors[2] });

            var empty = FilteredNode.CreateTransient(source, new FilterInsideBox3d(
                new Box3d(new V3d(2.0, -1.0, -1.0), new V3d(3.0, 1.0, 1.0))
                ));
            AssertCustomAttributes(empty, Array.Empty<int>(), Array.Empty<V3f>());
        }

        [Test]
        public void CustomAttributes_NestedAndEncodedViewsStayAligned()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var positions = CustomAttributePositions();
            var (integers, vectors) = CustomAttributeValues();
            var source = CreateNode(storage, positions, properties: CustomProperties(integers, vectors));
            var first = FilteredNode.CreateTransient(source, new FilterInsideBox3d(
                new Box3d(new V3d(0.3, -1.0, -1.0), new V3d(1.0, 1.0, 1.0))
                ));
            var nested = FilteredNode.CreateTransient(first, new FilterInsideBox3d(
                new Box3d(new V3d(0.6, -1.0, -1.0), new V3d(1.0, 1.0, 1.0))
                ));

            AssertCustomAttributes(nested, new[] { 70, 90 }, new[] { vectors[2], vectors[3] });

            var decoded = FilteredNode.Decode(storage, first.Encode());
            AssertCustomAttributes(decoded, new[] { 40, 70, 90 }, new[] { vectors[1], vectors[2], vectors[3] });
        }

        [Test]
        public void CustomAttributes_PreserveStructuralMetadata()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var positions = new V3f[8].SetByIndex(i => new V3f((i + 0.5) / 8.0, 0.0, 0.0));
            var subnodeIds = new Guid[8];
            var properties = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.SubnodesGuids, subnodeIds);
            var source = CreateNode(storage, positions, properties: properties);
            var filtered = FilteredNode.CreateTransient(source, new FilterInsideBox3d(
                new Box3d(new V3d(0.25, -1.0, -1.0), new V3d(0.75, 1.0, 1.0))
                ));

            ClassicAssert.AreSame(subnodeIds, filtered.Properties[Durable.Octree.SubnodesGuids]);
            ClassicAssert.IsTrue(filtered.TryGetValue(Durable.Octree.SubnodesGuids, out var value));
            ClassicAssert.AreSame(subnodeIds, value);
            ClassicAssert.AreSame(source.Properties[Durable.Octree.NodeId], filtered.Properties[Durable.Octree.NodeId]);
            ClassicAssert.AreSame(source.Properties[Durable.Octree.PositionsLocal3fReference], filtered.Properties[Durable.Octree.PositionsLocal3fReference]);
        }

        [Test]
        public void CustomAttributes_ConcurrentColdReadsShareValues()
        {
            using var storage = PointCloud.CreateInMemoryStore(cache: default);
            const int count = 4096;
            var positions = Enumerable.Range(0, count).Select(i => new V3f(((i * 2053) % count + 0.5) / count, 0, 0)).ToArray();
            var integers = Enumerable.Range(0, count).Select(i => 10 * i).ToArray();
            var vectors = Enumerable.Range(0, count).Select(i => new V3f(i, -i, 2 * i)).ToArray();
            var children = new Guid[8];
            var source = CreateNode(storage, positions, properties: CustomProperties(integers, vectors)
                .Add(Durable.Octree.SubnodesGuids, children));
            foreach (var kind in new[] { "partial", "nested", "empty", "full", "nested-empty", "nested-full" })
            foreach (var access in new[] { "same-key", "different-key", "mixed-accessors" })
            for (var round = 0; round < 6; round++)
            {
                var view = source;
                if (kind.StartsWith("nested")) view = FilteredNode.CreateTransient(view, new FilterInsideBox3d(
                    new Box3d(new V3d(0.25, -1, -1), new V3d(0.875, 1, 1))));
                var backing = view;
                var box = kind.EndsWith("full") ? backing.BoundingBoxExactGlobal : kind.EndsWith("empty")
                    ? new Box3d(new V3d(2, -1, -1), new V3d(3, 1, 1))
                    : new Box3d(new V3d(0.5, -1, -1), new V3d(0.75, 1, 1));
                view = FilteredNode.CreateTransient(backing, new FilterInsideBox3d(box));
                var indices = Enumerable.Range(0, count).Where(i => box.Contains((V3d)positions[i])).ToArray();
                var values = new object[access == "different-key" ? 2 : 8];
                var dictionaries = new IReadOnlyDictionary<Durable.Def, object>[values.Length];
                using var start = new Barrier(values.Length);
                var tasks = Enumerable.Range(0, values.Length).Select(worker => Task.Factory.StartNew(() =>
                {
                    if (!start.SignalAndWait(TimeSpan.FromSeconds(10))) throw new TimeoutException("cold-reader start");
                    var key = access == "different-key" && worker % 2 != 0 ? CustomVectorAttribute : CustomIntAttribute;
                    if (access == "mixed-accessors" && worker % 2 == 0)
                    {
                        dictionaries[worker] = view.Properties;
                        values[worker] = dictionaries[worker][key];
                    }
                    else
                    {
                        if (!view.TryGetValue(key, out values[worker])) throw new InvalidOperationException("missing custom value");
                    }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
                var context = $"kind={kind}, access={access}, round={round}";
                Assert.That(Task.WaitAll(tasks, TimeSpan.FromSeconds(20)), Is.True, context + ", readers complete");
                var properties = view.Properties;
                for (var worker = 0; worker < values.Length; worker++)
                {
                    var key = access == "different-key" && worker % 2 != 0 ? CustomVectorAttribute : CustomIntAttribute;
                    var label = context + $", worker={worker}, key={key.Name}";
                    Assert.That(ReferenceEquals(values[worker], properties[key]), Is.True, label + ", shared projection");
                    Assert.That(view.TryGetValue(key, out var again), Is.True, label);
                    Assert.That(again, Is.SameAs(values[worker]), label + ", stable lookup");
                    Assert.That(dictionaries[worker] ?? properties, Is.SameAs(properties), label + ", shared dictionary");
                    if (key == CustomIntAttribute)
                    {
                        Assert.That(values[worker].GetType(), Is.EqualTo(typeof(int[])), label);
                        Assert.That((int[])values[worker], Is.EqualTo(indices.Select(i => integers[i])), label);
                    }
                    else
                    {
                        Assert.That(values[worker].GetType(), Is.EqualTo(typeof(V3f[])), label);
                        Assert.That((V3f[])values[worker], Is.EqualTo(indices.Select(i => vectors[i])), label);
                    }
                }
                foreach (var key in new[] { Durable.Octree.SubnodesGuids, Durable.Octree.NodeId, Durable.Octree.Cell,
                    Durable.Octree.PositionsLocal3fReference, Durable.Octree.PerCellPartIndex1i })
                {
                    Assert.That(view.TryGetValue(key, out var value), Is.True, context + $", metadata={key.Name}");
                    Assert.That(value, Is.SameAs(source.Properties[key]), context + $", metadata={key.Name}");
                    Assert.That(properties[key], Is.SameAs(value), context + $", metadata={key.Name}");
                }
                if (kind.EndsWith("full")) Assert.That(properties, Is.SameAs(backing.Properties), context);
                Assert.That(view.PositionsAbsolute, Is.EqualTo(indices.Select(i => (V3d)positions[i])), context + ", sorted alignment");
            }
            Assert.That((int[])source.Properties[CustomIntAttribute], Is.EqualTo(Enumerable.Range(0, count).Select(i => 10 * i)), "unchanged source integers");
            Assert.That((V3f[])source.Properties[CustomVectorAttribute], Is.EqualTo(Enumerable.Range(0, count).Select(i => new V3f(i, -i, 2 * i))), "unchanged source vectors");
        }

        public class SelectivePropertyProbe : DispatchProxy
        {
            public IPointCloudNode Node;
            public bool Selective = true;
            protected override object Invoke(MethodInfo method, object[] args)
            {
                if (Selective && (method.Name == "get_Properties" ||
                    method.Name == "TryGetValue" && (Durable.Def)args[0] != CustomIntAttribute))
                    throw new InvalidOperationException("Selective lookup read unrelated properties.");
                try { return method.Invoke(Node, args); }
                catch (TargetInvocationException e) { throw e.InnerException; }
            }
        }

        [Test]
        public void CustomAttributes_SelectiveLookupDoesNotEnumerateProperties()
        {
            using var storage = PointCloud.CreateInMemoryStore(cache: default);
            var (integers, vectors) = CustomAttributeValues();
            var proxy = DispatchProxy.Create<IPointCloudNode, SelectivePropertyProbe>();
            var probe = (SelectivePropertyProbe)proxy;
            probe.Node = CreateNode(storage, CustomAttributePositions(), properties: CustomProperties(integers, vectors));
            var view = FilteredNode.CreateTransient(proxy, new FilterInsideBox3d(
                new Box3d(new V3d(0.3, -1, -1), new V3d(0.8, 1, 1))));
            Assert.That(view.TryGetValue(CustomIntAttribute, out var value), Is.True);
            Assert.That(value, Is.EqualTo(new[] { 40, 70 }));
            Assert.That(view.TryGetValue(CustomIntAttribute, out var again), Is.True);
            Assert.That(again, Is.SameAs(value));
            probe.Selective = false;
            Assert.That(view.Properties[CustomIntAttribute], Is.SameAs(value));
            Assert.That(view.TryGetValue(CustomVectorAttribute, out var vectorValue), Is.True);
            Assert.That(vectorValue, Is.EqualTo(new[] { vectors[1], vectors[2] }));
            Assert.That(view.Properties[CustomVectorAttribute], Is.SameAs(vectorValue));
            var missing = new Durable.Def(Guid.NewGuid(), "Tests.Missing", "Missing value", Durable.Primitives.Int32.Id, false);
            Assert.That(view.TryGetValue(missing, out var absent), Is.False);
            Assert.That(absent, Is.Null);
        }

        [Test]
        public void CustomLineQueryKeepsFilteredAttributesAligned()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var positions = CustomAttributePositions();
            var (integers, vectors) = CustomAttributeValues();
            var source = CreateNode(storage, positions, properties: CustomProperties(integers, vectors));
            var filtered = FilteredNode.CreateTransient(source, new FilterInsideBox3d(
                new Box3d(new V3d(0.6, -1.0, -1.0), new V3d(1.0, 1.0, 1.0))
                ));

            var chunks = filtered.QueryPointsNearLineSegmentCustom(
                new Line3d(new V3d(0.7, -1.0, 0.0), new V3d(0.7, 1.0, 0.0)),
                0.25,
                CustomIntAttribute
                ).ToArray();
            var pairs = chunks
                .SelectMany(chunk => chunk.PositionsAsV3d.Zip(
                    (int[])chunk.Data[CustomIntAttribute],
                    (position, value) => (position, value)
                    ))
                .OrderBy(pair => pair.position.X)
                .ToArray();

            ClassicAssert.AreEqual(2, pairs.Length);
            ClassicAssert.AreEqual(0.7, pairs[0].position.X, 1e-6);
            ClassicAssert.AreEqual(70, pairs[0].value);
            ClassicAssert.AreEqual(0.9, pairs[1].position.X, 1e-6);
            ClassicAssert.AreEqual(90, pairs[1].value);
        }

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

        private static V3f[] CustomAttributePositions() => new[]
        {
            new V3f(0.1, 0.0, 0.0),
            new V3f(0.4, 0.0, 0.0),
            new V3f(0.7, 0.0, 0.0),
            new V3f(0.9, 0.0, 0.0)
        };

        private static (int[] integers, V3f[] vectors) CustomAttributeValues() => (
            new[] { 10, 40, 70, 90 },
            new[]
            {
                new V3f(1.0, 10.0, 100.0),
                new V3f(4.0, 40.0, 400.0),
                new V3f(7.0, 70.0, 700.0),
                new V3f(9.0, 90.0, 900.0)
            }
            );

        private static ImmutableDictionary<Durable.Def, object> CustomProperties(int[] integers, V3f[] vectors)
            => ImmutableDictionary<Durable.Def, object>.Empty
                .Add(CustomIntAttribute, integers)
                .Add(CustomVectorAttribute, vectors);

        private static void AssertCustomAttributes(IPointCloudNode node, int[] expectedIntegers, V3f[] expectedVectors)
        {
            ClassicAssert.IsTrue(node.TryGetValue(CustomIntAttribute, out var integerValue));
            var integers = (int[])integerValue;
            var properties = node.Properties;
            ClassicAssert.AreSame(integers, properties[CustomIntAttribute]);
            ClassicAssert.AreSame(properties, node.Properties);
            ClassicAssert.AreEqual(typeof(int[]), integers.GetType());
            CollectionAssert.AreEqual(expectedIntegers, integers);

            var vectors = (V3f[])properties[CustomVectorAttribute];
            ClassicAssert.IsTrue(node.TryGetValue(CustomVectorAttribute, out var vectorValue));
            ClassicAssert.AreSame(vectors, vectorValue);
            ClassicAssert.AreEqual(typeof(V3f[]), vectors.GetType());
            CollectionAssert.AreEqual(expectedVectors, vectors);
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
