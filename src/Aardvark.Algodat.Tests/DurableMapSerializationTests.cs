using Aardvark.Base;
using Aardvark.Data;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Entry = System.Collections.Generic.KeyValuePair<Aardvark.Data.Durable.Def, object>;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class DurableMapSerializationTests
    {
        private enum Output { Bytes, Writer, Stream, NonSeekableWriter, NonSeekableStream }
        private static readonly Durable.Def[] MapDefinitions = { Durable.Octree.Node, Durable.Primitives.DurableMap };

        [Test]
        public void SingleUseAndConsumptiveEntriesKeepCompleteMapBytesAndRoundTrips()
        {
            foreach (var count in new[] { 0, 1, 8 })
            foreach (var reverse in new[] { false, true })
            foreach (var consumptive in new[] { false, true })
            foreach (var def in MapDefinitions)
            foreach (Output output in Enum.GetValues(typeof(Output)))
            {
                var entries = Entries(count, reverse); var source = new ObservedEntries(entries, strict: !consumptive, consumptive: consumptive);
                var context = $"count={count}, reverse={reverse}, consumptive={consumptive}, def={def.Name}, output={output}";
                var bytes = Serialize(def, source, output);
                Assert.That(bytes, Is.EqualTo(Codec.Serialize(def, entries)), context + " wire order");
                CheckRoundTrip(def, entries, bytes, context);
                CheckOnePass(source, count, context);
            }
        }

        [Test]
        public void NestedSingleUseMapsKeepEveryLevelAndItsEntryOrder()
        {
            foreach (var count in new[] { 0, 1, 4 })
            foreach (var depth in new[] { 1, 3 })
            foreach (var consumptive in new[] { false, true })
            foreach (var def in MapDefinitions)
            foreach (Output output in Enum.GetValues(typeof(Output)))
            {
                var expected = Entries(count, true);
                for (var i = 0; i < depth; i++) expected = new[] { new Entry(Durable.Octree.Node, expected), new Entry(Durable.Octree.PointCountCell, i + 1) };
                var sources = new List<ObservedEntries>();
                ObservedEntries Wrap(Entry[] entries)
                {
                    var values = entries.Select(e => new Entry(e.Key, e.Value is Entry[] nested ? (object)Wrap(nested) : e.Value)).ToArray();
                    var source = new ObservedEntries(values, strict: !consumptive, consumptive: consumptive); sources.Add(source); return source;
                }
                var root = Wrap(expected); var context = $"nested count={count}, depth={depth}, consumptive={consumptive}, def={def.Name}, output={output}";
                var bytes = Serialize(def, root, output);
                Assert.That(bytes, Is.EqualTo(Codec.Serialize(def, expected)), context + " nested bytes");
                CheckRoundTrip(def, expected, bytes, context);
                Assert.That(sources.Count, Is.EqualTo(depth + 1), context);
                foreach (var source in sources) CheckOnePass(source, source.Entries.Length, context);
            }
        }

        [Test]
        public void CountedMapsKeepDictionaryBytesWithoutCopyingAndRetainPrimitiveHeaders()
        {
            foreach (var count in new[] { 0, 1, 8 })
            foreach (var reverse in new[] { false, true })
            foreach (var def in MapDefinitions)
            foreach (Output output in Enum.GetValues(typeof(Output)))
            {
                var entries = Entries(count, reverse); var dictionary = entries.ToDictionary(e => e.Key, e => e.Value);
                var maps = new IEnumerable<Entry>[] { entries, entries.ToList(), dictionary, dictionary.ToImmutableDictionary(), new ReadOnlyDictionary<Durable.Def, object>(dictionary) };
                foreach (var map in maps)
                {
                    var ordered = map.ToArray(); var context = $"counted count={count}, reverse={reverse}, type={map.GetType()}, def={def.Name}, output={output}";
                    var bytes = Serialize(def, map, output);
                    Assert.That(bytes, Is.EqualTo(Codec.Serialize(def, ordered)), context);
                    CheckRoundTrip(def, ordered, bytes, context);
                    if (!reverse && count <= 1) Assert.That(bytes, Is.EqualTo(GoldenSmallMap(def, count)), context + " fixed header/count/entry bytes");
                }
                foreach (var kind in new[] { 0, 1, 2 })
                {
                    var observed = new ObservedEntries(entries, strict: true); CountedEntries counted = kind == 0 ? new GenericCounted(observed) : kind == 1 ? new ReadOnlyCounted(observed) : new UntypedCounted(observed);
                    var bytes = Serialize(def, counted, output); var context = $"count interface={kind}, count={count}, def={def.Name}, output={output}";
                    Assert.That(bytes, Is.EqualTo(Codec.Serialize(def, entries)), context);
                    CheckRoundTrip(def, entries, bytes, context); CheckOnePass(observed, count, context);
                    Assert.That(counted.CountReads, Is.EqualTo(1), context + " reuse count");
                }
            }
            foreach (Output output in Enum.GetValues(typeof(Output)))
            {
                using var expected = new MemoryStream(); using (var writer = new BinaryWriter(expected, System.Text.Encoding.UTF8, true)) { writer.Write(Durable.Primitives.Int32.Id.ToByteArray()); writer.Write(42); }
                Assert.That(Serialize(Durable.Primitives.Int32, 42, output), Is.EqualTo(expected.ToArray()), "primitive scalar header " + output);
            }
        }

        [Test]
        public void SourceFailuresPropagateAndDisposeEnumeratorsWithoutChangingWriterOwnership()
        {
            foreach (var def in MapDefinitions)
            foreach (Output output in Enum.GetValues(typeof(Output)))
            foreach (var failureAt in new[] { -2, 0, 1, 3 })
            foreach (var counted in new[] { false, true })
            {
                var source = new ObservedEntries(Entries(3, false), failureAt: failureAt); object map = counted ? (object)new GenericCounted(source) : source;
                var caught = Assert.Catch<IOException>(() => Serialize(def, map, output)); var context = $"failureAt={failureAt}, counted={counted}, output={output}, def={def.Name}";
                Assert.That(caught, Is.SameAs(source.Failure), context + " original exception");
                Assert.That(source.Enumerations, Is.EqualTo(1), context);
                Assert.That(source.Disposals, Is.EqualTo(failureAt == -2 ? 0 : 1), context);
            }
            foreach (Output output in Enum.GetValues(typeof(Output)))
            {
                var source = new ObservedEntries(new[] { new Entry(Durable.Octree.PointCountCell, "not an integer") });
                Assert.Throws<InvalidCastException>(() => Serialize(Durable.Octree.Node, source, output));
                Assert.That(source.Disposals, Is.EqualTo(source.Enumerations), "encoding failure releases all enumerators " + output);
            }
        }

        private static Entry[] Entries(int count, bool reverse)
        {
            var all = new[] {
                new Entry(Durable.Octree.PointCountCell, 3),
                new Entry(Durable.Octree.NodeId, new Guid("2f6f2f9e-1ed5-40cb-ae67-8130b1c93d4b")),
                new Entry(Durable.Octree.Cell, new Cell(1, 2, 3, 0)),
                new Entry(Durable.Octree.PositionsLocal3f, new[] { new V3f(0.25f, -0.5f, 0.75f), new V3f(1, 2, 3), new V3f(-1, -2, -3) }),
                new Entry(Durable.Octree.Colors4b, new[] { C4b.Red, C4b.Green, C4b.Blue }),
                new Entry(Durable.Octree.Classifications1b, new byte[] { 1, 7, 255 }),
                new Entry(Durable.Octree.PointCountTreeLeafs, 3L),
                new Entry(Durable.Octree.BoundingBoxExactGlobal, new Box3d(new V3d(-1), new V3d(3)))
            };
            return reverse ? all.Take(count).Reverse().ToArray() : all.Take(count).ToArray();
        }

        private static byte[] GoldenSmallMap(Durable.Def def, int count)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
            {
                writer.Write(def.Id.ToByteArray()); writer.Write(count);
                if (count == 1) { writer.Write(Durable.Octree.PointCountCell.Id.ToByteArray()); writer.Write(3); }
            }
            return stream.ToArray();
        }

        private static byte[] Serialize(Durable.Def def, object value, Output output)
        {
            if (output == Output.Bytes) return Codec.Serialize(def, value);
            var streamOverload = output == Output.Stream || output == Output.NonSeekableStream;
            using var stream = new WriteSink(output != Output.NonSeekableWriter && output != Output.NonSeekableStream);
            if (streamOverload)
            {
                try { Codec.Serialize((Stream)stream, def, value); }
                finally { Assert.That(stream.Disposed, Is.True, "Stream overload owns its stream"); Assert.That(stream.SeekAccesses, Is.Zero); }
            }
            else
            {
                using var writer = new BinaryWriter(stream);
                try { Codec.Serialize(writer, def, value); }
                finally { Assert.That(stream.Disposed, Is.False, "BinaryWriter overload leaves caller ownership"); Assert.That(stream.SeekAccesses, Is.Zero); }
                writer.Flush(); return stream.Bytes;
            }
            return stream.Bytes;
        }

        private static void CheckRoundTrip(Durable.Def def, Entry[] expected, byte[] bytes, string context)
        {
            using var stream = new MemoryStream(bytes); using var reader = new BinaryReader(stream);
            var (decodedDef, value) = Codec.Deserialize(reader);
            Assert.That(decodedDef, Is.EqualTo(def), context + " definition");
            Assert.That(stream.Position, Is.EqualTo(stream.Length), context + " consume complete record");
            void Check(Entry[] entries, object map)
            {
                Assert.That(map, Is.InstanceOf<IReadOnlyDictionary<Durable.Def, object>>(), context);
                var decoded = (IReadOnlyDictionary<Durable.Def, object>)map; Assert.That(decoded.Count, Is.EqualTo(entries.Length), context);
                foreach (var entry in entries)
                {
                    Assert.That(decoded.ContainsKey(entry.Key), Is.True, context + " key=" + entry.Key.Name);
                    if (entry.Value is Entry[] nested) Check(nested, decoded[entry.Key]);
                    else Assert.That(decoded[entry.Key], Is.EqualTo(entry.Value), context + " value=" + entry.Key.Name);
                }
            }
            Check(expected, value);
        }

        private static void CheckOnePass(ObservedEntries source, int count, string context)
        {
            Assert.That(source.Enumerations, Is.EqualTo(1), context + " one enumeration");
            Assert.That(source.Moves, Is.EqualTo(count + 1), context + " complete traversal");
            Assert.That(source.Disposals, Is.EqualTo(1), context + " disposed");
        }

        private sealed class ObservedEntries : IEnumerable<Entry>
        {
            public readonly Entry[] Entries;
            public readonly IOException Failure = new IOException("entry source failure");
            public int Enumerations, Moves, Disposals;
            private readonly bool strict, consumptive; private readonly int failureAt; private int cursor;
            public ObservedEntries(Entry[] entries, bool strict = false, bool consumptive = false, int failureAt = -1) { Entries = entries; this.strict = strict; this.consumptive = consumptive; this.failureAt = failureAt; }
            public IEnumerator<Entry> GetEnumerator()
            {
                Enumerations++;
                if (strict && Enumerations > 1) throw new InvalidOperationException("Source may only be enumerated once.");
                if (failureAt == -2) throw Failure;
                return Enumerate();
            }
            private IEnumerator<Entry> Enumerate()
            {
                try
                {
                    var i = consumptive ? cursor : 0;
                    while (true)
                    {
                        Moves++;
                        if (i == failureAt) throw Failure;
                        if (i == Entries.Length) yield break;
                        var entry = Entries[i++]; if (consumptive) cursor = i; yield return entry;
                    }
                }
                finally { Disposals++; }
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private abstract class CountedEntries : IEnumerable<Entry>
        {
            protected readonly ObservedEntries Source; public int CountReads;
            protected CountedEntries(ObservedEntries source) => Source = source;
            public int Count { get { CountReads++; return Source.Entries.Length; } }
            public IEnumerator<Entry> GetEnumerator() => Source.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        private sealed class ReadOnlyCounted : CountedEntries, IReadOnlyCollection<Entry> { public ReadOnlyCounted(ObservedEntries source) : base(source) { } }
        private sealed class GenericCounted : CountedEntries, ICollection<Entry>
        {
            public GenericCounted(ObservedEntries source) : base(source) { }
            public bool IsReadOnly => true;
            public void CopyTo(Entry[] array, int index) => throw new InvalidOperationException("Counted maps must not be copied.");
            public void Add(Entry entry) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public bool Contains(Entry entry) => Source.Entries.Contains(entry);
            public bool Remove(Entry entry) => throw new NotSupportedException();
        }
        private sealed class UntypedCounted : CountedEntries, ICollection
        {
            public UntypedCounted(ObservedEntries source) : base(source) { }
            public bool IsSynchronized => false;
            public object SyncRoot => this;
            public void CopyTo(Array array, int index) => throw new InvalidOperationException("Counted maps must not be copied.");
        }
        private sealed class WriteSink : Stream
        {
            private readonly MemoryStream inner = new MemoryStream(); private readonly bool seekable;
            public bool Disposed; public int SeekAccesses;
            public WriteSink(bool seekable) => this.seekable = seekable;
            public byte[] Bytes => inner.ToArray();
            public override bool CanRead => false;
            public override bool CanSeek => seekable;
            public override bool CanWrite => !Disposed;
            public override long Length { get { SeekAccesses++; if (!seekable) throw new NotSupportedException(); return inner.Length; } }
            public override long Position { get { SeekAccesses++; if (!seekable) throw new NotSupportedException(); return inner.Position; } set { SeekAccesses++; if (!seekable) throw new NotSupportedException(); inner.Position = value; } }
            public override void Flush() => inner.Flush();
            public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) { SeekAccesses++; if (!seekable) throw new NotSupportedException(); return inner.Seek(offset, origin); }
            public override void SetLength(long value) { SeekAccesses++; if (!seekable) throw new NotSupportedException(); inner.SetLength(value); }
            protected override void Dispose(bool disposing) { if (disposing) { Disposed = true; inner.Dispose(); } base.Dispose(disposing); }
        }
    }
}
