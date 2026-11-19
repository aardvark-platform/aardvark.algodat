/*
    Copyright (C) 2006-2026. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program. If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class ImportReductionCancellationTests
{
    [Test]
    public void CancellingFirstReductionStopsLaterMergesWithoutPublishingResult()
    {
        Assert.Multiple(() =>
        {
            foreach (var generic in new[] { false, true })
            foreach (var count in new[] { 4, 8 })
            {
                var context = $"generic={generic}, chunks={count}";
                using var cancellation = new CancellationTokenSource();
                using var backend = PointCloud.CreateInMemoryStore();
                var writes = new ConcurrentQueue<(string Key, PointSet Cloud)>();
                using var storage = ObserveWrites(backend, writes);
                var progress = new ConcurrentQueue<double>();
                var key = "cancelled-reduction";
                var config = Configure(storage, key, cancellation.Token).WithProgressCallback(p =>
                {
                    if (p <= 0.5) return; // Mapping occupies the first half.
                    progress.Enqueue(p);
                    if (progress.Count == 1) cancellation.Cancel();
                });

                var error = Assert.Throws<OperationCanceledException>(() => Reduce(CreateChunks(count, true), generic, config), context);
                Assert.That(error.CancellationToken, Is.EqualTo(cancellation.Token), context + ", supplied token");
                Assert.That(progress.ToArray(), Has.Length.EqualTo(1), context + ", later reductions must not start");
                Assert.That(writes.Select(w => w.Cloud.PointCount).ToArray(), Is.EqualTo(new long[] { 4 }), context + ", only the active merge may finish");
                Assert.That(writes.Any(w => w.Key == key), Is.False, context + ", no result publication");
                Assert.That(storage.GetPointSet(key), Is.Null, context + ", requested key absent");
                // Completed intermediate writes are deliberately not rolled back.
                foreach (var write in writes)
                    Assert.That(storage.GetPointSet(write.Key).PointCount, Is.EqualTo(4), context + ", completed write remains");
            }
        });
    }

    [Test]
    public void UncancelledReductionsPreservePointsAttributesAndPublication()
    {
        foreach (var generic in new[] { false, true })
        foreach (var count in new[] { 4, 8 })
        foreach (var attributes in new[] { false, true })
        foreach (var cancellable in new[] { false, true })
        {
            var context = $"generic={generic}, chunks={count}, attributes={attributes}, cancellable={cancellable}";
            using var cancellation = new CancellationTokenSource();
            using var backend = PointCloud.CreateInMemoryStore();
            var writes = new ConcurrentQueue<(string Key, PointSet Cloud)>();
            using var storage = ObserveWrites(backend, writes);
            var progress = new ConcurrentQueue<double>();
            var key = "completed-reduction";
            var config = Configure(storage, key, cancellable ? cancellation.Token : CancellationToken.None)
                .WithProgressCallback(p => { if (p > 0.5) progress.Enqueue(p); });
            var source = CreateChunks(count, attributes);
            var result = Reduce(source, generic, config);

            Assert.That(cancellation.IsCancellationRequested, Is.False, context);
            Assert.That(result.PointCount, Is.EqualTo(2 * count), context);
            Assert.That(writes.Count(w => w.Key != key), Is.EqualTo(count - 1), context + ", all pairwise merges");
            Assert.That(writes.Count(w => w.Key == key), Is.EqualTo(1), context + ", one publication");
            Assert.That(progress.Count, Is.EqualTo(count), context + ", reduction and final progress");
            Assert.That(progress.Last(), Is.EqualTo(1.0), context + ", completion");
            CheckPoints(result, source, attributes, context);
            storage.Cache.Clear();
            var reloaded = storage.GetPointSet(key);
            Assert.That(reloaded.Root.Id, Is.EqualTo(result.Root.Id), context + ", persisted root");
            CheckPoints(reloaded, source, attributes, context + ", reload");
        }
    }

    private static ImportConfig Configure(Storage storage, string key, CancellationToken token)
        => ImportConfig.Default.WithStorage(storage).WithKey(key).WithOctreeSplitLimit(2)
            .WithMaxDegreeOfParallelism(1).WithEnabledPartIndices(false).WithCancellationToken(token);

    private static PointSet Reduce(Chunk[] source, bool generic, ImportConfig config)
        => generic ? source.Select(c => c.ToGenericChunk()).MapReduce(config) : source.MapReduce(config);

    private static Storage ObserveWrites(Storage backend, ConcurrentQueue<(string Key, PointSet Cloud)> writes)
        => new(
            add: (key, value, create) =>
            {
                backend.f_add(key, value, create);
                if (value is PointSet cloud) writes.Enqueue((key, cloud));
            },
            get: backend.f_get, getSlice: backend.f_getSlice, remove: backend.f_remove,
            dispose: () => { }, flush: backend.f_flush, cache: backend.Cache);

    private static Chunk[] CreateChunks(int count, bool attributes)
        => Enumerable.Range(0, count).Select(chunk =>
        {
            var ids = new[] { 2 * chunk, 2 * chunk + 1 };
            return new Chunk(
                ids.Select(i => new V3d(4 * chunk + (i % 2 == 0 ? 0.125 : 0.375), 0.125, 0.125)).ToArray(),
                attributes ? ids.Select(i => new C4b((byte)(i + 11), (byte)(i + 31), (byte)(i + 51), (byte)201)).ToArray() : null,
                attributes ? ids.Select(i => new V3f(i + 1, 2, 3)).ToArray() : null,
                attributes ? ids.Select(i => i + 1000).ToArray() : null,
                attributes ? ids.Select(i => (byte)(i + 71)).ToArray() : null,
                null, null, null);
        }).ToArray();

    private static void CheckPoints(PointSet actual, Chunk[] source, bool attributes, string context)
    {
        var expected = source.SelectMany(c => c.Positions).OrderBy(p => p.X).ToArray();
        var chunks = actual.QueryAllPoints().ToArray();
        var positions = chunks.SelectMany(c => c.Positions).OrderBy(p => p.X).ToArray();
        Assert.That(expected.Concat(positions).All(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z)), Is.True, context + ", finite positions");
        Assert.That(positions, Is.EqualTo(expected), context + ", all full-resolution positions");
        foreach (var c in chunks)
        {
            Assert.That(c.HasColors && c.HasNormals && c.HasIntensities && c.HasClassifications, Is.EqualTo(attributes), context + ", attribute presence");
            if (!attributes)
            {
                Assert.That(c.HasColors || c.HasNormals || c.HasIntensities || c.HasClassifications, Is.False, context + ", absent attributes");
                continue;
            }
            for (var i = 0; i < c.Count; i++)
            {
                var id = Array.IndexOf(expected, c.Positions[i]);
                Assert.That(id, Is.GreaterThanOrEqualTo(0), context + ", point identity");
                Assert.That(c.Colors[i], Is.EqualTo(new C4b((byte)(id + 11), (byte)(id + 31), (byte)(id + 51), (byte)201)), context + $", color id={id}");
                Assert.That(c.Normals[i], Is.EqualTo(new V3f(id + 1, 2, 3)), context + $", normal id={id}");
                Assert.That(c.Intensities[i], Is.EqualTo(id + 1000), context + $", intensity id={id}");
                Assert.That(c.Classifications[i], Is.EqualTo((byte)(id + 71)), context + $", classification id={id}");
            }
        }
    }
}
