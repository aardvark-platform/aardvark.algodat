/*
    Copyright (C) 2006-2026. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
*/
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using Info = Aardvark.Geometry.Points.ExportExtensions.ExportPointSetInfo;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class ExportPointSetTests
{
    public enum View { Ordinary, Partial, All, Empty, Nested, NestedEmpty }

    private static readonly Durable.Def ExtraReference = new(
        new Guid("f70d65bb-8eaa-40ad-9a63-b762a71e6db1"), "ExportTests.ExtraReference",
        "Opaque external payload for export regression tests.", Durable.Primitives.GuidDef.Id, false);

    private sealed class Backend
    {
        public readonly Dictionary<string, byte[]> Data = new();
        public readonly List<string> Reads = new();
        public readonly List<string> Writes = new();
        public bool Available = true;
        public bool ReadOnly;
        public Action<string> OnRead;
        public Action<string> OnWrite;

        public Storage Open(int cacheSize = 0) => new(
            (key, value, encode) =>
            {
                Assert.That(Available && !ReadOnly, Is.True, "unexpected write to source");
                Data[key] = (byte[])encode().Clone();
                Writes.Add(key);
                OnWrite?.Invoke(key);
            },
            key =>
            {
                Assert.That(Available, Is.True, "destination tried to access closed source");
                Reads.Add(key);
                OnRead?.Invoke(key);
                return Data.TryGetValue(key, out var bytes) ? (byte[])bytes.Clone() : null;
            },
            (_, _, _) => throw new AssertionException("Export must not read slices."),
            key => Data.Remove(key), () => { }, () => { },
            cacheSize == 0 ? null : new LruDictionary<string, object>(cacheSize));
    }

    private sealed record Row(V3d Position, C4b Color, V3f Normal, int Intensity, byte Classification, int Part);

    private sealed class Fixture
    {
        public const string Key = "export.json";
        public readonly Backend Source = new();
        public readonly List<Row> Leaves = new();
        public readonly List<Guid> Nodes = new();
        public readonly List<(Guid Id, Guid Backing, string Filter)> Wrappers = new();
        public readonly Guid Root;
        public readonly Guid BackingRoot;
        public readonly View Kind;
        public long Count => Leaves.Count;

        public Fixture(int depth, View kind, bool compressed = false)
        {
            Kind = kind;
            using var storage = Source.Open();
            BackingRoot = Build(new Cell(0, 0, 0, depth), depth);
            Root = BackingRoot;
            if (kind == View.Nested || kind == View.NestedEmpty)
                Root = Wrap(Root, new FilterClassification(0, 1, 2));
            if (kind != View.Ordinary)
                Root = Wrap(Root, kind switch
                {
                    View.All => new FilterClassification(0, 1, 2, 3),
                    View.Empty or View.NestedEmpty => new FilterClassification(255),
                    _ => new FilterClassification(1, 2)
                });
            storage.Add(Key, new PointSet(storage, Key, Root, 4));
            Source.Reads.Clear();
            Source.Writes.Clear();
            Source.ReadOnly = true;

            Guid Wrap(Guid backing, IFilter filter)
            {
                // This is exactly the permanent FilteredNode schema. Building it
                // directly also lets export tests detect accidental filter evaluation.
                var id = Guid.NewGuid();
                var text = filter.Serialize().ToString();
                var map = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(Durable.Octree.NodeId, id)
                    .Add(FilteredNode.Defs.FilteredNodeRootId, backing)
                    .Add(FilteredNode.Defs.FilteredNodeFilter, text);
                storage.Add(id, FilteredNode.Defs.FilteredNode, map, compressed);
                Wrappers.Add((id, backing, text));
                return id;
            }

            Guid Build(Cell cell, int remaining)
            {
                Guid[] children = null;
                if (remaining > 0)
                {
                    children = new Guid[8];
                    children[0] = Build(cell.GetOctant(0), remaining - 1);
                    children[7] = Build(cell.GetOctant(7), remaining - 1);
                }
                var tag = Nodes.Count * 4;
                var ps = Enumerable.Range(0, 4).Select(i => new V3f((float)((i - 1.5) * cell.BoundingBox.Size.X / 8), 0.1f, -0.1f)).ToArray();
                var cs = Enumerable.Range(0, 4).Select(i => new C4b(tag + i, i + 10, 60, 255)).ToArray();
                var ns = Enumerable.Range(0, 4).Select(i => new V3f(i, 1, -1)).ToArray();
                var js = Enumerable.Range(tag + 100, 4).ToArray();
                var ks = new byte[] { 0, 1, 2, 3 };
                var qs = Enumerable.Range(tag + 1000, 4).ToArray();
                var id = Guid.NewGuid();
                var map = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(Durable.Octree.NodeId, id)
                    .Add(Durable.Octree.Cell, cell)
                    .Add(Durable.Octree.BoundingBoxExactLocal, new Box3f(ps))
                    .Add(Durable.Octree.BoundingBoxExactGlobal, cell.BoundingBox)
                    .Add(Durable.Octree.PointCountCell, 4)
                    .Add(Durable.Octree.PointCountTreeLeafs, 4L << remaining)
                    .Add(Durable.Octree.PositionsLocal3fCentroid, V3f.Zero)
                    .Add(Durable.Octree.PositionsLocal3fDistToCentroidStdDev, 1.0f)
                    .Add(Durable.Octree.MinTreeDepth, remaining)
                    .Add(Durable.Octree.MaxTreeDepth, remaining)
                    .Add(Durable.Octree.PartIndexRange, new Range1i(qs));
                Add(Durable.Octree.PositionsLocal3fReference, ps);
                Add(Durable.Octree.Colors4bReference, cs);
                Add(Durable.Octree.Normals3fReference, ns);
                Add(Durable.Octree.Intensities1iReference, js);
                Add(Durable.Octree.Classifications1bReference, ks);
                Add(Durable.Octree.PerPointPartIndex1iReference, qs);
                Add(ExtraReference, new byte[] { 4, 2, 0, 255 });
                var kdId = Guid.NewGuid();
                storage.Add(kdId, ps.BuildKdTree().Data);
                map = map.Add(Durable.Octree.PointRkdTreeFDataReference, kdId);
                if (children != null) map = map.Add(Durable.Octree.SubnodesGuids, children);
                else for (var i = 0; i < 4; i++) Leaves.Add(new Row((V3d)ps[i] + cell.BoundingBox.Center, cs[i], ns[i], js[i], ks[i], qs[i]));
                storage.Add(id, Durable.Octree.Node, map, compressed);
                Nodes.Add(id);
                return id;

                void Add(Durable.Def def, Array values)
                {
                    var key = Guid.NewGuid();
                    storage.Add(key, values);
                    map = map.Add(def, key);
                }
            }
        }

        public Row[] Selected => Leaves.Where(x => Kind switch
        {
            View.Empty or View.NestedEmpty => false,
            View.Ordinary or View.All => true,
            _ => x.Classification == 1 || x.Classification == 2
        }).ToArray();
    }

    private static Row[] ReadRows(PointSet pointset) => pointset.QueryAllPoints().SelectMany(chunk =>
    {
        var qs = (int[])chunk.PartIndices;
        return Enumerable.Range(0, chunk.Count).Select(i => new Row(chunk.Positions[i], chunk.Colors[i], chunk.Normals[i], chunk.Intensities[i], chunk.Classifications[i], qs[i]));
    }).ToArray();

    private static ImmutableDictionary<Durable.Def, object> Map(byte[] bytes)
        => (ImmutableDictionary<Durable.Def, object>)Aardvark.Data.Codec.Deserialize(StorageExtensions.UnGZip(bytes)).Item2;

    [Test]
    public void ExportReopensWithCompleteReferenceClosure(
        [Values(0, 2)] int depth, [Values] View view,
        [Values(0, 4096)] int sourceCache, [Values(0, 4096)] int destinationCache,
        [Values(false, true)] bool compressed)
    {
        var fixture = new Fixture(depth, view, compressed);
        var destination = new Backend();
        using (var source = fixture.Source.Open(sourceCache))
        using (var target = destination.Open(destinationCache))
        {
            var updates = new List<Info>();
            var result = source.ExportPointSet(Fixture.Key, target, info =>
            {
                updates.Add(info);
                Assert.That(double.IsFinite(info.Progress), Is.True);
                Assert.That(info.Progress, Is.InRange(0.0, 1.0));
                if (info.Progress == 1) Assert.That(destination.Data.Keys, Is.EquivalentTo(fixture.Source.Data.Keys), "completion preceded a dependency");
            }, false, default);
            Assert.That(result.PointCountTree, Is.EqualTo(fixture.Count), "counts describe the copied backing tree, not the filtered estimate");
            Assert.That(result.ProcessedLeafPointCount, Is.EqualTo(fixture.Count));
            Assert.That(updates.Last(), Is.SameAs(result));
            Assert.That(updates.Select(x => x.Progress), Is.Ordered);
            Assert.That(updates.Count(x => x.Progress == 1), Is.EqualTo(1));
            Assert.That(fixture.Source.Reads, Is.EquivalentTo(fixture.Source.Data.Keys), "read exactly once per stored record, without a pre-scan or filter evaluation");
            Assert.That(destination.Writes, Is.EquivalentTo(fixture.Source.Data.Keys));
            Assert.That(fixture.Source.Writes, Is.Empty);
        }
        fixture.Source.Available = false;

        using var reopened = destination.Open(destinationCache);
        var restored = reopened.GetPointSet(Fixture.Key);
        Assert.That(restored.Id, Is.EqualTo(Fixture.Key));
        Assert.That(restored.SplitLimit, Is.EqualTo(4));
        Assert.That(restored.Root.Id, Is.EqualTo(fixture.Root.ToString()));
        Assert.That(ReadRows(restored), Is.EquivalentTo(fixture.Selected));
        foreach (var wrapper in fixture.Wrappers)
        {
            var node = (FilteredNode)reopened.GetPointCloudNode(wrapper.Id);
            Assert.That(node.Id, Is.EqualTo(wrapper.Id));
            Assert.That(node.Node.Id, Is.EqualTo(wrapper.Backing));
            Assert.That(JsonNode.DeepEquals(node.Filter.Serialize(), JsonNode.Parse(wrapper.Filter)), Is.True);
        }
        foreach (var id in fixture.Nodes)
        {
            var node = reopened.GetPointCloudNode(id);
            Assert.That(node.Id, Is.EqualTo(id));
            Assert.That(node.PointCountCell, Is.EqualTo(4));
            var positions = node.Positions.Value;
            Assert.That(node.HasKdTree, Is.True);
            var hit = node.KdTree.Value.GetClosest(positions[0], 0.001f, 1).Single();
            Assert.That(hit.Index, Is.EqualTo(0));
            Assert.That(hit.Dist, Is.EqualTo(0));
        }
        // Compare every persisted node map and every opaque payload independently
        // of the public view query, including data outside the selected region.
        foreach (var (key, bytes) in fixture.Source.Data)
        {
            Assert.That(destination.Data.ContainsKey(key), Is.True);
            if (key == Fixture.Key) continue;
            Assert.That(StorageExtensions.UnGZip(destination.Data[key]), Is.EqualTo(StorageExtensions.UnGZip(bytes)), key);
        }
    }

    [Test]
    public void ExportDoesNotInterpretFiltersOrAttributePayloads([Values(false, true)] bool compressed)
    {
        var fixture = new Fixture(2, View.Nested, compressed);
        foreach (var wrapper in fixture.Wrappers)
        {
            var map = Map(fixture.Source.Data[wrapper.Id.ToString()]).SetItem(FilteredNode.Defs.FilteredNodeFilter, "{\"Type\":\"FutureOpaqueFilter\"}");
            fixture.Source.Data[wrapper.Id.ToString()] = map.DurableEncode(FilteredNode.Defs.FilteredNode, compressed);
        }
        foreach (var id in fixture.Nodes)
        {
            var map = Map(fixture.Source.Data[id.ToString()]);
            foreach (var item in map.Where(x => x.Key.Type == Durable.Primitives.GuidDef.Id && x.Key != Durable.Octree.NodeId))
                fixture.Source.Data[((Guid)item.Value).ToString()] = new byte[] { 42 };
        }
        var destination = new Backend();
        using var source = fixture.Source.Open();
        using var target = destination.Open();
        var info = source.ExportPointSet(Fixture.Key, target, null, false, default);
        Assert.That(info.Progress, Is.EqualTo(1));
        Assert.That(destination.Data.Keys, Is.EquivalentTo(fixture.Source.Data.Keys));
        Assert.That(fixture.Source.Reads, Is.EquivalentTo(fixture.Source.Data.Keys));
    }

    [Test]
    public void EmptyPointSetCompletes([Values(0, 4096)] int cache, [Values(false, true)] bool verbose)
    {
        var source = new Backend();
        var destination = new Backend();
        using var from = source.Open(cache);
        using var to = destination.Open(cache);
        from.Add(Fixture.Key, new PointSet(from, Fixture.Key));
        var updates = new List<Info>();
        var info = from.ExportPointSet(Fixture.Key, to, updates.Add, verbose, default);
        Assert.That(info.PointCountTree, Is.Zero);
        Assert.That(info.ProcessedLeafPointCount, Is.Zero);
        Assert.That(info.Progress, Is.EqualTo(1));
        Assert.That(updates, Is.EqualTo(new[] { info }));
        Assert.That(destination.Data.Keys, Is.EqualTo(new[] { Fixture.Key }));
        source.Available = false;
        using var reopened = destination.Open(cache);
        Assert.That(reopened.GetPointSet(Fixture.Key).IsEmpty, Is.True);
    }

    [Test]
    public void CachedMetadataDoesNotResolveItsRoot()
    {
        var fixture = new Fixture(2, View.Nested);
        using var source = fixture.Source.Open(4096);
        using var target = new Backend().Open();
        var metadata = new PointSet(source, Fixture.Key, fixture.Root, 4)
        {
            Root = new PersistentRef<IPointCloudNode>(fixture.Root.ToString(), _ => throw new AssertionException("resolved root"), (string _, out IPointCloudNode value) => throw new AssertionException("resolved cached root"))
        };
        source.Cache.Add(Fixture.Key, metadata, 1, default);
        Assert.That(source.ExportPointSet(Fixture.Key, target, null, false, default).Progress, Is.EqualTo(1));
        Assert.That(fixture.Source.Reads, Does.Not.Contain(Fixture.Key));
    }

    [Test]
    public void LegacyMetadataAliasesRemainSupported([Values(false, true)] bool lowerCase, [Values(false, true)] bool splitLimit)
    {
        var fixture = new Fixture(0, View.Partial);
        var json = new JsonObject { ["Id"] = Fixture.Key, ["RootCellId"] = fixture.Root.ToString() };
        if (splitLimit) json["SplitLimit"] = 13;
        var text = json.ToJsonString();
        if (lowerCase) text = text.Replace("Id\"", "id\"").Replace("RootCell", "rootcell").Replace("SplitLimit", "splitlimit");
        fixture.Source.Data[Fixture.Key] = System.Text.Encoding.UTF8.GetBytes(text);
        using var source = fixture.Source.Open();
        var destination = new Backend();
        using var target = destination.Open();
        source.ExportPointSet(Fixture.Key, target, null, false, default);
        fixture.Source.Available = false;
        Assert.That(target.GetPointSet(Fixture.Key).SplitLimit, Is.EqualTo(splitLimit ? 13 : 8192));
        Assert.That(ReadRows(target.GetPointSet(Fixture.Key)), Is.EquivalentTo(fixture.Selected));
    }

    [Test]
    public void PreCancellationDoesNotReadOrWrite()
    {
        var source = new Backend();
        var destination = new Backend();
        using var from = source.Open();
        using var to = destination.Open();
        Assert.Throws<OperationCanceledException>(() => from.ExportPointSet(Fixture.Key, to, _ => Assert.Fail(), false, new CancellationToken(true)));
        Assert.That(source.Reads, Is.Empty);
        Assert.That(destination.Writes, Is.Empty);
    }

    [Test]
    public void CancellationDuringStorageNeverReportsCompletion([Values(false, true)] bool onRead, [Values(1, 2, 3, 10, -1)] int operation)
    {
        var fixture = new Fixture(2, View.NestedEmpty);
        var destination = new Backend();
        using var source = fixture.Source.Open();
        using var target = destination.Open();
        using var cts = new CancellationTokenSource();
        var count = 0;
        var stopAt = operation == -1 ? fixture.Source.Data.Count : operation;
        void Cancel(string _) { if (++count == stopAt) cts.Cancel(); }
        if (onRead) fixture.Source.OnRead = Cancel; else destination.OnWrite = Cancel;
        var updates = new List<Info>();
        Assert.Throws<OperationCanceledException>(() => source.ExportPointSet(Fixture.Key, target, updates.Add, false, cts.Token));
        Assert.That(updates.All(x => x.Progress < 1), Is.True);
        Assert.That(count, Is.EqualTo(stopAt), "I/O continued after cancellation");
    }

    [Test]
    public void CallbackCancellationAndExceptionsPropagate([Values(false, true)] bool cancel)
    {
        var fixture = new Fixture(2, View.Partial);
        using var source = fixture.Source.Open();
        using var target = new Backend().Open();
        using var cts = new CancellationTokenSource();
        var called = 0;
        void Progress(Info info)
        {
            called++;
            Assert.That(info.Progress, Is.LessThan(1));
            if (cancel) cts.Cancel(); else throw new ApplicationException("callback failure");
        }
        if (cancel) Assert.Throws<OperationCanceledException>(() => source.ExportPointSet(Fixture.Key, target, Progress, false, cts.Token));
        else Assert.Throws<ApplicationException>(() => source.ExportPointSet(Fixture.Key, target, Progress, false, cts.Token));
        Assert.That(called, Is.EqualTo(1));
    }

    [Test]
    public void MissingDependenciesDoNotComplete([Values(false, true)] bool missingNode)
    {
        var fixture = new Fixture(2, View.Nested);
        var missing = missingNode ? fixture.Nodes[0] : (Guid)Map(fixture.Source.Data[fixture.Nodes[0].ToString()])[ExtraReference];
        fixture.Source.Data.Remove(missing.ToString());
        using var source = fixture.Source.Open();
        using var target = new Backend().Open();
        var updates = new List<Info>();
        Assert.Throws<InvalidOperationException>(() => source.ExportPointSet(Fixture.Key, target, updates.Add, false, default));
        Assert.That(updates.All(x => x.Progress < 1), Is.True);
    }

    [Test]
    public void PermanentViewsCreatedThroughPublicApiReopen([Values(0, 2)] int depth, [Values(false, true)] bool nested)
    {
        var fixture = new Fixture(depth, View.Ordinary);
        fixture.Source.ReadOnly = false;
        using var source = fixture.Source.Open();
        IPointCloudNode root = source.GetPointCloudNode(fixture.BackingRoot);
        root = FilteredNode.Create(root, new FilterClassification(0, 1, 2));
        if (nested) root = FilteredNode.Create(root, new FilterClassification(1, 2));
        source.Add(Fixture.Key, new PointSet(source, Fixture.Key, root, 4));
        fixture.Source.ReadOnly = true;
        var destination = new Backend();
        using (var target = destination.Open()) source.ExportPointSet(Fixture.Key, target, null, false, default);
        fixture.Source.Available = false;
        using var reopened = destination.Open();
        Assert.That(reopened.GetPointSet(Fixture.Key).Root.Id, Is.EqualTo(root.Id.ToString()));
        Assert.That(ReadRows(reopened.GetPointSet(Fixture.Key)), Is.EquivalentTo(fixture.Leaves.Where(x => x.Classification < 3 && (!nested || x.Classification > 0))));
    }

    [Test]
    public void ZeroPointBackingLeavesCompleteAfterTheirBlobs([Values(false, true)] bool zeroTotal, [Values(false, true)] bool wrapped)
    {
        var fixture = new Fixture(zeroTotal ? 0 : 1, wrapped ? View.Empty : View.Ordinary);
        var lastLeaf = fixture.Nodes[zeroTotal ? 0 : 1];
        var map = Map(fixture.Source.Data[lastLeaf.ToString()]);
        // All standard per-point payloads are external, including empty arrays.
        foreach (var entry in map.Where(x => x.Key.Type == Durable.Primitives.GuidDef.Id && x.Key != Durable.Octree.NodeId && x.Key != ExtraReference))
            fixture.Source.Data[((Guid)entry.Value).ToString()] = entry.Key == Durable.Octree.PointRkdTreeFDataReference
                ? Aardvark.Geometry.Points.Codec.PointRkdTreeFDataToBuffer(Array.Empty<V3f>().BuildKdTree().Data)
                : Array.Empty<byte>();
        map = map.SetItem(Durable.Octree.PointCountCell, 0).SetItem(Durable.Octree.PointCountTreeLeafs, 0L);
        fixture.Source.Data[lastLeaf.ToString()] = map.DurableEncode(Durable.Octree.Node, false);
        if (!zeroTotal)
        {
            var root = Map(fixture.Source.Data[fixture.BackingRoot.ToString()]).SetItem(Durable.Octree.PointCountTreeLeafs, 4L);
            fixture.Source.Data[fixture.BackingRoot.ToString()] = root.DurableEncode(Durable.Octree.Node, false);
        }
        var destination = new Backend();
        using var source = fixture.Source.Open();
        using var target = destination.Open();
        var completions = 0;
        var result = source.ExportPointSet(Fixture.Key, target, info =>
        {
            if (info.Progress != 1) return;
            completions++;
            Assert.That(destination.Data.Keys, Is.EquivalentTo(fixture.Source.Data.Keys));
        }, true, default);
        Assert.That(result.Progress, Is.EqualTo(1));
        Assert.That(result.PointCountTree, Is.EqualTo(zeroTotal ? 0 : 4));
        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void LegacyBinaryNodeFallbackKeepsIdsAndPayloads([Values(false, true)] bool useNodeId)
    {
        var fixture = new Fixture(0, View.Ordinary);
        var original = Map(fixture.Source.Data[fixture.BackingRoot.ToString()]);
        var kdId = (Guid)original[Durable.Octree.PointRkdTreeFDataReference];
        var kd = Aardvark.Geometry.Points.Codec.BufferToPointRkdTreeFData(fixture.Source.Data[kdId.ToString()]);
        fixture.Source.Data[kdId.ToString()] = Aardvark.Geometry.Points.Codec.PointRkdTreeDDataToBuffer(new PointRkdTreeDData
        {
            AxisArray = kd.AxisArray, PermArray = kd.PermArray, RadiusArray = kd.RadiusArray.Select(x => (double)x).ToArray()
        });
        using (var bytes = new MemoryStream())
        {
            using var writer = new BinaryWriter(bytes);
            writer.Write((uint)(1 | 2 | 4 | 8 | 16 | 1024));
            writer.Write(fixture.BackingRoot.ToByteArray());
            writer.Write(0L); writer.Write(0L); writer.Write(0L); writer.Write(0);
            writer.Write(4L);
            foreach (var field in new[] { Durable.Octree.PositionsLocal3fReference, Durable.Octree.Colors4bReference,
                Durable.Octree.Normals3fReference, Durable.Octree.Intensities1iReference,
                Durable.Octree.PointRkdTreeFDataReference, Durable.Octree.Classifications1bReference })
                writer.Write(((Guid)original[field]).ToByteArray());
            fixture.Source.Data[fixture.BackingRoot.ToString()] = bytes.ToArray();
        }
        // Supplying a node ID intentionally creates the legacy ersatz metadata.
        fixture.Source.ReadOnly = !useNodeId;
        using var source = fixture.Source.Open();
        var destination = new Backend();
        using var target = destination.Open();
        var result = source.ExportPointSet(useNodeId ? fixture.BackingRoot.ToString() : Fixture.Key, target, null, false, default);
        Assert.That(result.Progress, Is.EqualTo(1));
        fixture.Source.Available = false;
        var metadataKey = useNodeId ? fixture.Source.Writes.Single() : Fixture.Key;
        var pointset = target.GetPointSet(metadataKey);
        Assert.That(pointset.Root.Id, Is.EqualTo(fixture.BackingRoot.ToString()));
        var node = pointset.Root.Value;
        Assert.That(node.PositionsAbsolute, Is.EqualTo(fixture.Leaves.Select(x => x.Position)));
        Assert.That(node.Colors.Value, Is.EqualTo(fixture.Leaves.Select(x => x.Color)));
        Assert.That(node.Normals.Value, Is.EqualTo(fixture.Leaves.Select(x => x.Normal)));
        Assert.That(node.Intensities.Value, Is.EqualTo(fixture.Leaves.Select(x => x.Intensity)));
        Assert.That(node.Classifications.Value, Is.EqualTo(fixture.Leaves.Select(x => x.Classification)));
        Assert.That(node.KdTree.Value.GetClosest(node.Positions.Value[0], 0.001f, 1).Single().Index, Is.Zero);
        Assert.That(destination.Data[kdId.ToString()], Is.EqualTo(fixture.Source.Data[kdId.ToString()]));
    }

    [Test]
    public void MissingAndInvalidMetadataRetainNodeFallback([Values(false, true)] bool invalid)
    {
        var backend = new Backend();
        if (invalid) backend.Data[Fixture.Key] = System.Text.Encoding.UTF8.GetBytes("{}");
        using var source = backend.Open();
        using var target = new Backend().Open();
        Assert.Throws<Exception>(() => source.ExportPointSet(Fixture.Key, target, _ => Assert.Fail(), false, default));
    }

    [Test]
    public void StorageCancellationExceptionsAreNotTreatedAsLegacyData([Values(false, true)] bool metadata)
    {
        var fixture = new Fixture(0, View.Ordinary);
        fixture.Source.OnRead = key => { if ((key == Fixture.Key) == metadata) throw new OperationCanceledException(); };
        using var source = fixture.Source.Open();
        using var target = new Backend().Open();
        Assert.Throws<OperationCanceledException>(() => source.ExportPointSet(Fixture.Key, target, _ => Assert.Fail(), false, default));
        Assert.That(fixture.Source.Reads.Count, Is.EqualTo(metadata ? 1 : 2));
    }

    [Test]
    public void LegacyEmptyRootMetadataCompletes([Values(null, "")] string rootId)
    {
        var backend = new Backend();
        using var source = backend.Open();
        using var target = new Backend().Open();
        source.Add(Fixture.Key, new JsonObject { ["Id"] = Fixture.Key, ["RootCellId"] = rootId }.ToJsonString());
        Assert.That(source.ExportPointSet(Fixture.Key, target, null, false, default).Progress, Is.EqualTo(1));
        Assert.That(target.GetPointSet(Fixture.Key).IsEmpty, Is.True);
    }

    [Test]
    public void OlderDurableMapsWithoutCountsStillExport([Values(false, true)] bool omitLeafCount)
    {
        var fixture = new Fixture(0, View.Ordinary);
        var map = Map(fixture.Source.Data[fixture.BackingRoot.ToString()]).Remove(Durable.Octree.PointCountTreeLeafs);
        if (omitLeafCount) map = map.Remove(Durable.Octree.PointCountCell);
        map = map.SetItem(ExtraReference, Guid.Empty);
        fixture.Source.Data[fixture.BackingRoot.ToString()] = map.DurableEncode(Durable.Octree.Node, false);
        using var source = fixture.Source.Open();
        using var target = new Backend().Open();
        var info = source.ExportPointSet(Fixture.Key, target, null, false, default);
        Assert.That(info.Progress, Is.EqualTo(1));
        Assert.That(info.PointCountTree, Is.EqualTo(4));
        Assert.That(fixture.Source.Reads, Does.Not.Contain(Guid.Empty.ToString()));
        fixture.Source.Available = false;
        Assert.That(ReadRows(target.GetPointSet(Fixture.Key)), Is.EquivalentTo(fixture.Selected));
    }

    [Test]
    public void RepeatedExportsReuseOnlyCachedMetadata([Values(0, 4096)] int cache, [Values(View.Ordinary, View.Nested)] View view)
    {
        var fixture = new Fixture(2, view);
        var destination = new Backend();
        using var source = fixture.Source.Open(cache);
        using var target = destination.Open();
        for (var round = 0; round < 3; round++)
        {
            fixture.Source.Reads.Clear();
            destination.Data.Clear();
            source.ExportPointSet(Fixture.Key, target, null, false, default);
            Assert.That(destination.Data.Keys, Is.EquivalentTo(fixture.Source.Data.Keys));
            var expected = fixture.Source.Data.Keys.Where(key => cache == 0 || round == 0 || key != Fixture.Key);
            Assert.That(fixture.Source.Reads, Is.EquivalentTo(expected));
        }
    }

    [Test]
    public void CancellationBeforeNodeIdFallbackDoesNotCreateMetadata()
    {
        var fixture = new Fixture(0, View.Ordinary);
        using var cts = new CancellationTokenSource();
        fixture.Source.OnRead = _ => cts.Cancel();
        using var source = fixture.Source.Open();
        var destination = new Backend();
        using var target = destination.Open();
        Assert.Throws<OperationCanceledException>(() => source.ExportPointSet(fixture.BackingRoot.ToString(), target, _ => Assert.Fail(), false, cts.Token));
        Assert.That(fixture.Source.Reads.Count, Is.EqualTo(1));
        Assert.That(fixture.Source.Writes, Is.Empty);
        Assert.That(destination.Writes, Is.Empty);
    }

    [Test]
    public void ProgressSnapshotsAreImmutable()
    {
        var first = new Info(10);
        var next = first.AddProcessedLeafPoints(4);
        Assert.That(first.ProcessedLeafPointCount, Is.Zero);
        Assert.That(next.PointCountTree, Is.EqualTo(10));
        Assert.That(next.Progress, Is.EqualTo(0.4));
        Assert.That(next.AddProcessedLeafPoints(6).Progress, Is.EqualTo(1));
        Assert.That(new Info(0).Progress, Is.EqualTo(1));
    }
}
