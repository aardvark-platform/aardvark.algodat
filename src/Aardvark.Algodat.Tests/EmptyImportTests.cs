/*
    Copyright (C) 2006-2026. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class EmptyImportTests
    {
        public enum ImportEntryPoint
        {
            MapReduce,
            Chunks,
            Import,
        }

        private const int SplitLimit = 7;

        private static ImportConfig CreateConfig(Storage storage, string key, Action<double> progress)
            => ImportConfig.Default
                .WithStorage(storage)
                .WithKey(key!)
                .WithOctreeSplitLimit(SplitLimit)
                .WithMaxDegreeOfParallelism(2)
                .WithMinDist(0.0)
                .WithEnabledPartIndices(false)
                .WithProgressCallback(progress);

        private static PointSet RunChunkImport(
            ImportEntryPoint entryPoint,
            bool allEmpty,
            ImportConfig config)
        {
            IEnumerable<Chunk> chunks = allEmpty
                ? new[] { Chunk.Empty, new Chunk(Array.Empty<V3d>()), Chunk.Empty }
                : Array.Empty<Chunk>();

            return entryPoint switch
            {
                ImportEntryPoint.MapReduce => chunks.MapReduce(config),
                ImportEntryPoint.Chunks => PointCloud.Chunks(chunks, config),
                ImportEntryPoint.Import => PointCloud.Import(chunks, config),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint)),
            };
        }

        private static PointSet RunGenericChunkImport(
            ImportEntryPoint entryPoint,
            bool allEmpty,
            ImportConfig config)
        {
            IEnumerable<GenericChunk> chunks = allEmpty
                ? new[] { GenericChunk.Empty, GenericChunk.Empty, GenericChunk.Empty }
                : Array.Empty<GenericChunk>();

            return entryPoint switch
            {
                ImportEntryPoint.MapReduce => chunks.MapReduce(config),
                ImportEntryPoint.Chunks => PointCloud.Chunks(chunks, config),
                ImportEntryPoint.Import => PointCloud.Import(chunks, config),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint)),
            };
        }

        private static void AssertCanonicalEmpty(
            PointSet result,
            Storage storage,
            string expectedKey,
            IReadOnlyList<double> progress,
            bool isTopLevel)
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.IsEmpty, Is.True);
                Assert.That(result.PointCount, Is.Zero);
                Assert.That(result.Root.Id, Is.EqualTo(Guid.Empty.ToString()));
                Assert.That(result.Root.Value, Is.SameAs(PointSetNode.Empty));
                Assert.That(result.Root.Value.PointCountCell, Is.Zero);
                Assert.That(result.Root.Value.PointCountTree, Is.Zero);
                Assert.That(result.Id, Is.EqualTo(expectedKey));
                Assert.That(result.SplitLimit, Is.EqualTo(SplitLimit));
                Assert.That(result.Storage, Is.SameAs(storage));
                Assert.That(progress, Is.Not.Empty);
                Assert.That(progress[^1], Is.EqualTo(1.0));
                if (isTopLevel) Assert.That(progress[0], Is.EqualTo(0.0));
            });

            var reloaded = storage.GetPointSet(expectedKey);
            Assert.That(reloaded, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(reloaded!.IsEmpty, Is.True);
                Assert.That(reloaded.PointCount, Is.Zero);
                Assert.That(reloaded.Root.Id, Is.EqualTo(Guid.Empty.ToString()));
                Assert.That(reloaded.Root.Value, Is.SameAs(PointSetNode.Empty));
                Assert.That(reloaded.Root.Value.PointCountCell, Is.Zero);
                Assert.That(reloaded.Root.Value.PointCountTree, Is.Zero);
                Assert.That(reloaded.Id, Is.EqualTo(expectedKey));
                Assert.That(reloaded.SplitLimit, Is.EqualTo(SplitLimit));
                Assert.That(reloaded.Storage, Is.SameAs(storage));
            });
        }

        [TestCase(ImportEntryPoint.MapReduce, false)]
        [TestCase(ImportEntryPoint.MapReduce, true)]
        [TestCase(ImportEntryPoint.Chunks, false)]
        [TestCase(ImportEntryPoint.Chunks, true)]
        [TestCase(ImportEntryPoint.Import, false)]
        [TestCase(ImportEntryPoint.Import, true)]
        public void EmptyChunkInputsHaveCanonicalResults(ImportEntryPoint entryPoint, bool allEmpty)
        {
            using var storage = PointCloud.CreateInMemoryStore(cache: default);
            var progress = new List<double>();
            var key = $"empty-chunk-{entryPoint}-{allEmpty}";
            var config = CreateConfig(storage, key, progress.Add);

            var result = RunChunkImport(entryPoint, allEmpty, config);

            AssertCanonicalEmpty(result, storage, key, progress, entryPoint != ImportEntryPoint.MapReduce);
        }

        [TestCase(ImportEntryPoint.MapReduce, false)]
        [TestCase(ImportEntryPoint.MapReduce, true)]
        [TestCase(ImportEntryPoint.Chunks, false)]
        [TestCase(ImportEntryPoint.Chunks, true)]
        [TestCase(ImportEntryPoint.Import, false)]
        [TestCase(ImportEntryPoint.Import, true)]
        public void EmptyGenericChunkInputsHaveCanonicalResults(ImportEntryPoint entryPoint, bool allEmpty)
        {
            using var storage = PointCloud.CreateInMemoryStore(cache: default);
            var progress = new List<double>();
            var key = $"empty-generic-{entryPoint}-{allEmpty}";
            var config = CreateConfig(storage, key, progress.Add);

            var result = RunGenericChunkImport(entryPoint, allEmpty, config);

            AssertCanonicalEmpty(result, storage, key, progress, entryPoint != ImportEntryPoint.MapReduce);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptyMapReducePersistsGeneratedEffectiveKey(bool generic)
        {
            using var storage = PointCloud.CreateInMemoryStore(cache: default);
            var progress = new List<double>();
            var config = CreateConfig(storage, key: null, progress.Add);

            var result = generic
                ? Array.Empty<GenericChunk>().MapReduce(config)
                : Array.Empty<Chunk>().MapReduce(config);

            Assert.That(Guid.TryParse(result.Id, out _), Is.True);
            AssertCanonicalEmpty(result, storage, result.Id, progress, isTopLevel: false);
        }

        [Test]
        public void EmptyGenerateLodReportsCompletionWithoutChangingResult()
        {
            using var storage = PointCloud.CreateInMemoryStore(cache: default);
            var empty = new PointSet(storage, "empty-lod", Guid.Empty, SplitLimit);
            var progress = new List<double>();
            var config = CreateConfig(storage, "unused-lod-key", progress.Add);

            var result = empty.GenerateLod(config);

            Assert.That(result, Is.SameAs(empty));
            Assert.That(progress, Is.EqualTo(new[] { 1.0 }));
        }

        [Test]
        public void NonemptyChunkAndGenericChunkImportsRemainEquivalent()
        {
            var positions = new[]
            {
                new V3d(-2, -1, 0),
                new V3d(-1,  2, 1),
                new V3d( 0, -2, 2),
                new V3d( 1,  1, 3),
                new V3d( 2, -1, 4),
                new V3d( 3,  2, 5),
                new V3d( 4, -2, 6),
                new V3d( 5,  1, 7),
            };
            var colors = positions.Map((_, i) => new C4b((byte)(i + 1), (byte)(i + 11), (byte)(i + 21), (byte)255));
            var chunk = new Chunk(positions, colors, null, null, null, null, null, null);
            var genericChunk = chunk.ToGenericChunk();

            using var chunkStorage = PointCloud.CreateInMemoryStore(cache: default);
            using var genericStorage = PointCloud.CreateInMemoryStore(cache: default);
            var chunkProgress = new ConcurrentQueue<double>();
            var genericProgress = new ConcurrentQueue<double>();
            var chunkConfig = CreateConfig(chunkStorage, "nonempty-chunk", chunkProgress.Enqueue);
            var genericConfig = CreateConfig(genericStorage, "nonempty-generic", genericProgress.Enqueue);

            var fromChunk = PointCloud.Import(new[] { chunk }, chunkConfig);
            var fromGeneric = PointCloud.Import(new[] { genericChunk }, genericConfig);
            var chunkPositions = fromChunk.QueryAllPoints().SelectMany(x => x.Positions).OrderBy(x => x.X).ToArray();
            var genericPositions = fromGeneric.QueryAllPoints().SelectMany(x => x.Positions).OrderBy(x => x.X).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(fromChunk.IsEmpty, Is.False);
                Assert.That(fromGeneric.IsEmpty, Is.False);
                Assert.That(fromChunk.PointCount, Is.EqualTo(positions.Length));
                Assert.That(fromGeneric.PointCount, Is.EqualTo(positions.Length));
                Assert.That(fromChunk.SplitLimit, Is.EqualTo(SplitLimit));
                Assert.That(fromGeneric.SplitLimit, Is.EqualTo(SplitLimit));
                Assert.That(fromChunk.Id, Is.EqualTo("nonempty-chunk"));
                Assert.That(fromGeneric.Id, Is.EqualTo("nonempty-generic"));
                Assert.That(chunkPositions, Is.EqualTo(positions));
                Assert.That(genericPositions, Is.EqualTo(positions));
                Assert.That(chunkPositions, Is.EqualTo(genericPositions));
                Assert.That(chunkProgress.Last(), Is.EqualTo(1.0));
                Assert.That(genericProgress.Last(), Is.EqualTo(1.0));
            });
        }
    }
}
