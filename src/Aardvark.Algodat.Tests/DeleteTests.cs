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
using System.Text.Json.Nodes;
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

        private sealed class CancellationFixture : IDisposable
        {
            public readonly Dictionary<string, byte[]> Data = new();
            public readonly Storage Storage;
            public readonly PointSet Points;
            public readonly IPointCloudNode Root;
            public readonly Dictionary<string, byte[]> Original;
            public readonly HashSet<string> NodeKeys = new();
            public int Reads, Writes;
            public Action<string> OnRead;
            public Action<string, object> OnWrite;
            private readonly bool attributes;

            public CancellationFixture(int splitLimit, bool attributes = true)
            {
                this.attributes = attributes;
                Storage = new Storage(
                    (key, value, encode) => { Data[key] = encode(); Writes++; OnWrite?.Invoke(key, value); },
                    key => { Reads++; OnRead?.Invoke(key); return Data.TryGetValue(key, out var bytes) ? bytes : null; },
                    (_, _, _) => throw new AssertionException("Unexpected slice read"),
                    _ => throw new AssertionException("Unexpected removal"), () => { },
                    () => throw new AssertionException("Unexpected flush"), cache: null);
                Root = Build(new Cell(0, 0, 0, 2), Enumerable.Range(0, 64).ToArray());
                Points = new PointSet(Storage, "source", Root.Id, splitLimit);

                IPointCloudNode Build(Cell cell, int[] ids)
                {
                    var children = ids.Length <= splitLimit ? null : Enumerable.Range(0, 8).Select(o =>
                    {
                        var child = cell.GetOctant(o);
                        var selected = ids.Where(i => child.BoundingBox.Contains(Position(i))).ToArray();
                        return selected.Length == 0 ? null : Build(child, selected);
                    }).ToArray();
                    var sample = children == null ? ids : ids.Take(splitLimit).ToArray();
                    var positions = sample.Select(i => (V3f)(Position(i) - cell.GetCenter())).ToArray();
                    var data = ImmutableDictionary<Durable.Def, object>.Empty
                        .Add(Durable.Octree.NodeId, Guid.NewGuid()).Add(Durable.Octree.Cell, cell)
                        .Add(Durable.Octree.PointCountCell, sample.Length).Add(Durable.Octree.PointCountTreeLeafs, (long)ids.Length)
                        .Add(Durable.Octree.BoundingBoxExactGlobal, new Box3d(ids.Select(Position)))
                        .Add(Durable.Octree.BoundingBoxExactLocal, new Box3f(positions))
                        .Add(Durable.Octree.MinTreeDepth, children == null ? 0 : children.Where(n => n != null).Min(n => n.MinTreeDepth) + 1)
                        .Add(Durable.Octree.MaxTreeDepth, children == null ? 0 : children.Where(n => n != null).Max(n => n.MaxTreeDepth) + 1);
                    void Add(Durable.Def key, Array value)
                    {
                        var id = Guid.NewGuid(); Storage.Add(id, value); data = data.Add(key, id);
                    }
                    Add(Durable.Octree.PositionsLocal3fReference, positions);
                    var kdId = Guid.NewGuid(); Storage.Add(kdId, positions.BuildKdTree().Data);
                    data = data.Add(Durable.Octree.PointRkdTreeFDataReference, kdId);
                    if (attributes)
                    {
                        Add(Durable.Octree.Colors4bReference, sample.Select(Color).ToArray());
                        Add(Durable.Octree.Normals3fReference, sample.Select(Normal).ToArray());
                        Add(Durable.Octree.Intensities1iReference, sample.Select(i => 1000 + i).ToArray());
                        Add(Durable.Octree.Classifications1bReference, sample.Select(i => (byte)i).ToArray());
                        Add(Durable.Octree.PerPointPartIndex1iReference, sample.Select(i => 10000 + i).ToArray());
                        data = data.Add(Durable.Octree.PartIndexRange, new Range1i(10000 + ids.Min(), 10000 + ids.Max()));
                    }
                    if (children != null) data = data.Add(Durable.Octree.SubnodesGuids, children.Select(n => n?.Id ?? Guid.Empty).ToArray());
                    var node = new PointSetNode(data, Storage, writeToStore: true);
                    NodeKeys.Add(node.Id.ToString());
                    return node;
                }
                Original = Data.ToDictionary(x => x.Key, x => (byte[])x.Value.Clone());
                Reset();
            }

            public void Reset() { Reads = Writes = 0; OnRead = null; OnWrite = null; }
            public void AssertSourceUnchanged()
            {
                Reset();
                foreach (var item in Original) Assert.That(Data[item.Key], Is.EqualTo(item.Value), item.Key);
                AssertSelection(Points.Root.Value, Enumerable.Range(0, 64).ToArray(), attributes);
            }
            public void Dispose() => Storage.Dispose();
        }

        private static V3d Position(int i) => new((i >> 4) + 0.125, ((i >> 2) & 3) + 0.125, (i & 3) + 0.125);
        private static int PointId(V3d p) => (int)p.X * 16 + (int)p.Y * 4 + (int)p.Z;
        private static C4b Color(int i) => new((byte)i, (byte)(i + 64), (byte)(255 - i), (byte)255);
        private static V3f Normal(int i) => i % 3 == 0 ? V3f.IOO : i % 3 == 1 ? V3f.OIO : V3f.OOI;

        private static void AssertSelection(IPointCloudNode root, int[] expected, bool attributes, string context = "source")
        {
            var ids = root.QueryAllPoints().SelectMany(c => c.Positions).Select(PointId).OrderBy(i => i).ToArray();
            Assert.That(ids, Is.EqualTo(expected.OrderBy(i => i)), $"{context}: leaf selection");
            root.ForEachNode(false, n =>
            {
                Assert.That((n.HasColors, n.HasNormals, n.HasIntensities, n.HasClassifications, n.HasPartIndices),
                    Is.EqualTo((attributes, attributes, attributes, attributes, attributes)), $"{context}: attribute presence");
                var positions = n.Positions.Value;
                var colors = n.Colors?.Value;
                var normals = n.Normals?.Value;
                var intensities = n.Intensities?.Value;
                var classifications = n.Classifications?.Value;
                var parts = n.PartIndices;
                for (var i = 0; i < positions.Length; i++)
                {
                    var id = PointId(n.Center + (V3d)positions[i]);
                    Assert.That(expected, Does.Contain(id), $"{context}: node {n.Id}, point {i}");
                    if (colors != null) Assert.That(colors[i], Is.EqualTo(Color(id)), $"{context}: color {id}");
                    if (normals != null) Assert.That(normals[i], Is.EqualTo(Normal(id)), $"{context}: normal {id}");
                    if (intensities != null) Assert.That(intensities[i], Is.EqualTo(1000 + id), $"{context}: intensity {id}");
                    if (classifications != null) Assert.That(classifications[i], Is.EqualTo((byte)id), $"{context}: classification {id}");
                    if (parts != null) Assert.That(PartIndexUtils.Get(parts, i), Is.EqualTo(10000 + id), $"{context}: part {id}");
                }
            });
        }

        private static object Delete(CancellationFixture f, int overload, CancellationToken ct,
            Func<IPointCloudNode, bool> inside, Func<IPointCloudNode, bool> outside,
            Func<V3d, PointDeleteAttributes, bool> position, bool nullInput = false,
            IPointCloudNode root = null, int? splitLimit = null, Storage destination = null)
        {
            root ??= f.Root;
            var points = new PointSet(f.Storage, "input", root.Id, splitLimit ?? f.Points.SplitLimit)
            {
                Root = ReferenceEquals(root, f.Root) ? f.Points.Root : new PersistentRef<IPointCloudNode>(root.Id, root)
            };
            var storage = destination ?? f.Storage;
            return overload switch
            {
                0 => (nullInput ? null : points).Delete(inside, outside, position, storage, ct),
                1 => (nullInput ? null : points).Delete(inside, outside, p => position(p, default), storage, ct),
                2 => (nullInput ? null : root).Delete(inside, outside, position, storage, ct, points.SplitLimit),
                3 => (nullInput ? null : root).Delete(inside, outside, p => position(p, default), storage, ct, points.SplitLimit),
                _ => throw new ArgumentOutOfRangeException(nameof(overload))
            };
        }

        private static void AssertCancelled(TestDelegate action, CancellationToken token, string context)
        {
            var error = Assert.Throws<OperationCanceledException>(action, context);
            if (error != null) Assert.That(error.CancellationToken, Is.EqualTo(token), context);
        }

        [Test]
        public void NullDeletionRemainsAPassthrough()
        {
            using var f = new CancellationFixture(128);
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            foreach (var ct in new[] { CancellationToken.None, cts.Token })
                for (var overload = 0; overload < 4; overload++)
                    Assert.That(Delete(f, overload, ct, _ => throw new AssertionException("inside"),
                        _ => throw new AssertionException("outside"), (_, _) => throw new AssertionException("point"), nullInput: true), Is.Null);
            Assert.That((f.Reads, f.Writes), Is.EqualTo((0, 0)));
        }

        [Test]
        public void PreCancelledDeletionDoesNotReadWriteOrCallPredicates()
        {
            foreach (var split in new[] { 128, 8, 2 })
            foreach (var view in new[] { "plain", "empty", "spatial", "non-spatial" })
            using (var f = new CancellationFixture(split))
            using (var cts = new CancellationTokenSource())
            {
                var root = view switch
                {
                    "empty" => PointSetNode.Empty,
                    "spatial" => FilteredNode.CreateTransient(f.Root, new FilterInsideBox3d(new Box3d(V3d.Zero, new V3d(2, 4, 4)))),
                    "non-spatial" => FilteredNode.CreateTransient(f.Root, new FilterClassification(new HashSet<byte> { 1 })),
                    _ => f.Root
                };
                cts.Cancel();
                for (var overload = 0; overload < 4; overload++)
                {
                    f.Reset();
                    var calls = 0;
                    AssertCancelled(() => Delete(f, overload, cts.Token, _ => { calls++; return false; },
                        _ => { calls++; return false; }, (_, _) => { calls++; return false; }, root: root), cts.Token, $"split={split}, view={view}, overload={overload}");
                    Assert.That((calls, f.Reads, f.Writes), Is.EqualTo((0, 0, 0)));
                }
                f.AssertSourceUnchanged();
            }
        }

        [Test]
        public void NodeCallbackCancellationPreventsKeepDropAndFurtherWork()
        {
            foreach (var split in new[] { 128, 8 })
            foreach (var callback in new[] { "inside", "outside" })
            foreach (var answer in new[] { false, true })
            for (var overload = 0; overload < 4; overload++)
            using (var f = new CancellationFixture(split))
            using (var cts = new CancellationTokenSource())
            {
                var calls = 0;
                bool Cancel() { calls++; cts.Cancel(); return answer; }
                AssertCancelled(() => Delete(f, overload, cts.Token,
                    _ => callback == "inside" ? Cancel() : false,
                    _ => callback == "outside" ? Cancel() : throw new AssertionException("outside after cancellation"),
                    (_, _) => throw new AssertionException("point after cancellation")), cts.Token, $"split={split}, callback={callback}, answer={answer}, overload={overload}");
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(f.Writes, Is.Zero);
                f.AssertSourceUnchanged();
            }
        }

        [Test]
        public void PointCallbackCancellationStopsLeafAndRecursiveScans()
        {
            foreach (var split in new[] { 128, 8, 2 })
            foreach (var cancelAt in new[] { 1, 3, 64 })
            foreach (var remove in new[] { false, true })
            for (var overload = 0; overload < 4; overload++)
            using (var f = new CancellationFixture(split))
            using (var cts = new CancellationTokenSource())
            {
                var calls = 0;
                AssertCancelled(() => Delete(f, overload, cts.Token, _ => false, _ => false, (_, _) =>
                {
                    if (++calls == cancelAt) cts.Cancel();
                    return remove;
                }), cts.Token, $"split={split}, cancelAt={cancelAt}, remove={remove}, overload={overload}");
                Assert.That(calls, Is.EqualTo(cancelAt));
                if (cancelAt == 1 || remove) Assert.That(f.Writes, Is.Zero);
                f.AssertSourceUnchanged();
            }
        }

        [Test]
        public void CancellationFromLazyReadsStopsBeforePredicatesOrTheNextRead()
        {
            foreach (var split in new[] { 128, 8 })
            using (var f = new CancellationFixture(split))
            {
                var leaf = f.Root.IsLeaf ? f.Root : f.Root.Subnodes.First(r => r != null).Value;
                var partKey = ((Guid)leaf.Properties[Durable.Octree.PerPointPartIndex1iReference]).ToString();
                var keys = new[] { f.Root.Id.ToString(), leaf.Id.ToString(), leaf.Positions.Id, leaf.Colors.Id,
                    leaf.Normals.Id, leaf.Intensities.Id, leaf.Classifications.Id, partKey };
                foreach (var key in keys.Distinct())
                for (var overload = 0; overload < 4; overload++)
                using (var cts = new CancellationTokenSource())
                {
                    f.Reset();
                    if (overload >= 2 && key == f.Root.Id.ToString()) continue;
                    var atRead = -1;
                    var occurrences = 0;
                    var isNode = f.NodeKeys.Contains(key);
                    // A node decode itself validates five attribute arrays synchronously.
                    // For attribute cases, cancel at the subsequent explicit deletion read.
                    var cancelAt = !isNode && key != partKey && (overload < 2 || split < 64) ? 2 : 1;
                    f.OnRead = k => { if (k == key && ++occurrences == cancelAt) { atRead = f.Reads; cts.Cancel(); } };
                    AssertCancelled(() => Delete(f, overload, cts.Token,
                        _ => { Assert.That(cts.IsCancellationRequested, Is.False); return false; },
                        _ => { Assert.That(cts.IsCancellationRequested, Is.False); return false; },
                        (_, _) => { Assert.That(cts.IsCancellationRequested, Is.False); return false; }), cts.Token, $"split={split}, key={key}, overload={overload}");
                    Assert.That(atRead, Is.GreaterThan(0));
                    Assert.That(f.Reads, Is.EqualTo(atRead + (isNode ? 5 : 0)), "only the in-flight node decode may finish");
                    Assert.That(f.Writes, Is.Zero);
                }
                f.AssertSourceUnchanged();
            }
        }

        [Test]
        public void CancellationAtEachWritePreventsSuccessWithoutRollingBackPayloads()
        {
            for (var overload = 0; overload < 4; overload++)
            foreach (var split in new[] { 128, 8 })
            {
                using var control = new CancellationFixture(split);
                Delete(control, overload, CancellationToken.None, _ => false, _ => false, (p, _) => PointId(p) % 2 == 0);
                var writes = control.Writes;
                foreach (var cancelAt in Enumerable.Range(1, writes))
                using (var f = new CancellationFixture(split))
                using (var cts = new CancellationTokenSource())
                {
                    f.OnWrite = (_, _) => { if (f.Writes == cancelAt) cts.Cancel(); };
                    AssertCancelled(() => Delete(f, overload, cts.Token, _ => false, _ => false, (p, _) => PointId(p) % 2 == 0), cts.Token,
                        $"split={split}, overload={overload}, write={cancelAt}/{writes}");
                    Assert.That(f.Writes, Is.EqualTo(cancelAt), "write after cancellation");
                    Assert.That(f.Data.Count, Is.GreaterThan(f.Original.Count), "completed immutable writes are not rolled back");
                    f.AssertSourceUnchanged();
                }
            }
        }

        [Test]
        public void CancellationDuringCollapseAndLodStopsFurtherCollection()
        {
            foreach (var originalSplit in new[] { 8, 2 })
            foreach (var newSplit in new[] { 8, 64 })
            using (var f = new CancellationFixture(originalSplit))
            using (var cts = new CancellationTokenSource())
            {
                var readsAtCancel = -1;
                var completionReads = 0;
                var kept = 0;
                f.OnRead = key =>
                {
                    if (kept == 8 && !cts.IsCancellationRequested)
                    {
                        readsAtCancel = f.Reads;
                        completionReads = f.NodeKeys.Contains(key) ? 5 : 0;
                        cts.Cancel();
                    }
                };
                AssertCancelled(() => Delete(f, 2, cts.Token, _ => false, n =>
                {
                    if (n.Id == f.Root.Id) return false;
                    kept++;
                    return true;
                }, (_, _) => throw new AssertionException("kept subtree was scanned"), splitLimit: newSplit), cts.Token,
                    $"originalSplit={originalSplit}, newSplit={newSplit}");
                Assert.That(kept, Is.EqualTo(8));
                Assert.That(readsAtCancel, Is.GreaterThan(0));
                Assert.That(f.Reads, Is.EqualTo(readsAtCancel + completionReads), "only the in-flight node decode may finish");
                Assert.That(f.Writes, Is.Zero);
                f.AssertSourceUnchanged();
            }
        }

        private sealed class CancellationFilter : ISpatialFilter
        {
            private readonly FilterInsideBox3d inner = new(new Box3d(V3d.Zero, new V3d(2, 4, 4)));
            public Action<string> After;
            public bool IsFullyInside(IPointCloudNode n) { var value = inner.IsFullyInside(n); After?.Invoke("inside"); return value; }
            public bool IsFullyOutside(IPointCloudNode n) { var value = inner.IsFullyOutside(n); After?.Invoke("outside"); return value; }
            public bool Contains(V3d p) { var value = inner.Contains(p); After?.Invoke("contains"); return value; }
            public bool IsFullyInside(Box3d b) => inner.IsFullyInside(b);
            public bool IsFullyOutside(Box3d b) => inner.IsFullyOutside(b);
            public Box3d Clip(Box3d b) => inner.Clip(b);
            public HashSet<int> FilterPoints(IPointCloudNode n, HashSet<int> selected = null) => inner.FilterPoints(n, selected);
            public JsonNode Serialize() => inner.Serialize();
            public bool Equals(IFilter other) => ReferenceEquals(this, other);
        }

        [Test]
        public void SpatialFilterCancellationStopsUserCallbacksAndViewPublication()
        {
            foreach (var stage in new[] { "inside", "outside", "contains", "publication", "store" })
            using (var f = new CancellationFixture(128))
            using (var cts = new CancellationTokenSource())
            {
                var filter = new CancellationFilter();
                var root = FilteredNode.CreateTransient(f.Root, filter);
                var calls = 0;
                f.Reset();
                filter.After = name => { if (name == stage || stage == "publication" && calls > 0 && name == "inside") cts.Cancel(); };
                f.OnWrite = (_, _) => { if (stage == "store" && f.Writes == 9) cts.Cancel(); };
                AssertCancelled(() => Delete(f, 2, cts.Token,
                    _ => { Assert.That(cts.IsCancellationRequested, Is.False); return false; },
                    _ => { Assert.That(cts.IsCancellationRequested, Is.False); return false; },
                    (_, _) => { Assert.That(cts.IsCancellationRequested, Is.False); calls++; return false; }, root: root), cts.Token, stage);
                Assert.That(f.Writes, Is.EqualTo(stage == "publication" ? 8 : stage == "store" ? 9 : 0), "completed writes are not rolled back");
                filter.After = null;
                f.AssertSourceUnchanged();
            }
        }

        public class MaterializationProbe : DispatchProxy
        {
            public IPointCloudNode Inner;
            public Action Materializing;
            protected override object Invoke(MethodInfo method, object[] args) => method.Name switch
            {
                "get_IsMaterialized" => false,
                "Materialize" => Materialize(),
                _ => method.Invoke(Inner, args)
            };
            private IPointCloudNode Materialize() { Materializing(); return Inner; }
        }

        [Test]
        public void KeepShortcutChecksCancellationAroundMaterialization()
        {
            foreach (var cancelAt in new[] { "outside", "materialize", "none" })
            using (var f = new CancellationFixture(128))
            using (var cts = new CancellationTokenSource())
            {
                var node = DispatchProxy.Create<IPointCloudNode, MaterializationProbe>();
                var probe = (MaterializationProbe)node;
                probe.Inner = f.Root;
                var calls = 0;
                probe.Materializing = () => { calls++; if (cancelAt == "materialize") cts.Cancel(); };
                object Invoke() => Delete(f, 2, cts.Token, _ => false,
                    _ => { if (cancelAt == "outside") cts.Cancel(); return true; },
                    (_, _) => throw new AssertionException("Unexpected point predicate"), root: node);
                if (cancelAt == "none") Assert.That(Invoke(), Is.SameAs(f.Root));
                else AssertCancelled(() => Invoke(), cts.Token, cancelAt);
                Assert.That(calls, Is.EqualTo(cancelAt == "outside" ? 0 : 1), cancelAt);
                f.AssertSourceUnchanged();
            }
        }

        [Test]
        public void UncancelledDeletionPreservesSelectionAttributesAndStorageRouting()
        {
            foreach (var split in new[] { 128, 8, 2 })
            foreach (var attributes in new[] { false, true })
            foreach (var mode in new[] { "keep", "drop", "half", "collapse", "spatial" })
            for (var overload = 0; overload < 4; overload++)
            using (var f = new CancellationFixture(split, attributes))
            using (var destination = new CancellationFixture(128, false))
            using (var cts = new CancellationTokenSource())
            {
                var root = mode == "spatial" ? FilteredNode.CreateTransient(f.Root, new FilterInsideBox3d(new Box3d(V3d.Zero, new V3d(2, 4, 4)))) : f.Root;
                f.Reset(); destination.Reset();
                var result = Delete(f, overload, overload % 2 == 0 ? CancellationToken.None : cts.Token,
                    _ => mode == "drop", _ => mode == "keep", (p, a) =>
                    {
                        var id = PointId(p);
                        if (attributes && overload % 2 == 0)
                        {
                            Assert.That(a.Classification, Is.EqualTo((byte)id));
                            Assert.That(a.PartIndex, Is.EqualTo(10000 + id));
                        }
                        return mode == "collapse" ? id != 0 : id % 2 == 0;
                    }, root: root, destination: destination.Storage);
                if (mode == "drop") Assert.That(result, Is.Null);
                else
                {
                    // Node payloads go to the supplied destination; the PointSet descriptor stays in its source store.
                    var node = result is PointSet points ? (mode == "keep" ? f.Root : destination.Storage.GetPointCloudNode(points.Root.Id)) : (IPointCloudNode)result;
                    var expected = Enumerable.Range(0, 64).Where(i => mode == "keep" || mode == "collapse" && i == 0 || mode != "collapse" && i % 2 != 0 && (mode != "spatial" || i < 32)).ToArray();
                    AssertSelection(node, expected, attributes, $"split={split}, attributes={attributes}, mode={mode}, overload={overload}");
                    if (mode == "collapse") Assert.That(node.IsLeaf, Is.True);
                    if (overload < 2)
                    {
                        Assert.That(((PointSet)result).Storage, Is.SameAs(f.Storage));
                        Assert.That(f.Data.ContainsKey(((PointSet)result).Id), Is.True);
                    }
                    if (mode == "keep" && overload >= 2) Assert.That(result, Is.SameAs(f.Root));
                    if (mode != "keep") Assert.That(destination.Writes, Is.GreaterThan(0));
                }
                f.AssertSourceUnchanged();
            }
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
