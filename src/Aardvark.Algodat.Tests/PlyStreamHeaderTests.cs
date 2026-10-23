#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Aardvark.Base;
using Aardvark.Data.Points;
using NUnit.Framework;
using Ply.Net;
using PlyImport = Aardvark.Data.Points.Import.Ply;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PlyStreamHeaderTests
    {
        private sealed class InitialShortReadStream : Stream
        {
            private readonly MemoryStream inner;
            private readonly int limit;
            private readonly bool seekable;
            private bool firstAccess = true;
            public int Rewinds, DisposeCount;
            public InitialShortReadStream(byte[] bytes, int limit, bool seekable = true)
            { inner = new MemoryStream(bytes, writable: false); this.limit = limit; this.seekable = seekable; }
            private int Request(int count)
            {
                if (!firstAccess || count == 0) return count;
                firstAccess = false;
                return limit == 0 ? count : Math.Min(count, limit);
            }
            public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Request(count));
            public override int Read(Span<byte> buffer) => inner.Read(buffer.Slice(0, Request(buffer.Length)));
            public override int ReadByte() { firstAccess = false; return inner.ReadByte(); }
            public override bool CanRead => inner.CanRead;
            public override bool CanSeek => seekable;
            public override bool CanWrite => false;
            public override long Length => inner.Length;
            public override long Position
            {
                get => seekable ? inner.Position : throw new NotSupportedException();
                set { if (!seekable) throw new NotSupportedException(); if (value == 0) ++Rewinds; inner.Position = value; }
            }
            public override long Seek(long offset, SeekOrigin origin)
            { if (!seekable) throw new NotSupportedException(); if (offset == 0 && origin == SeekOrigin.Begin) ++Rewinds; return inner.Seek(offset, origin); }
            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { if (disposing) { ++DisposeCount; inner.Dispose(); } base.Dispose(disposing); }
        }

        private static readonly V3d[] Positions = { new V3d(1.25, 2, 3), new V3d(-2, 0, 0.5), new V3d(4.5, -1, 2) };
        private static readonly C4b[] Colors = { new C4b(255, 1, 2, 255), new C4b(3, 254, 4, 255), new C4b(5, 6, 253, 255) };
        private static readonly V3f[] Normals = { V3f.IOO, V3f.OIO, V3f.OOI };
        private static readonly int[] Intensities = { -2, 11, 400 };
        private static readonly byte[] Classes = { 0, 7, 255 };
        private static readonly string[] Properties =
        {
            "property float x", "property float y", "property float z",
            "property uchar red", "property uchar green", "property uchar blue",
            "property float nx", "property float ny", "property float nz",
            "property int intensity", "property uchar classification"
        };
        private static string[] HeaderLines(string format) => new[] { "ply", $"format {format} 1.0", "comment short-read fixture", "obj_info preserved", "element vertex 3" }
            .Concat(Properties).Concat(new[] { "element face 1", "property list uchar int vertex_indices", "end_header" }).ToArray();
        private static byte[] Header(string format, string newline) => Encoding.ASCII.GetBytes(string.Join(newline, HeaderLines(format)) + newline);
        private static byte[] FileBytes(bool binary, string newline)
        {
            using var output = new MemoryStream();
            var header = Header(binary ? "binary_little_endian" : "ascii", newline);
            output.Write(header, 0, header.Length);
            if (binary)
            {
                using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
                for (var i = 0; i < 3; ++i)
                {
                    writer.Write((float)Positions[i].X); writer.Write((float)Positions[i].Y); writer.Write((float)Positions[i].Z);
                    writer.Write(Colors[i].R); writer.Write(Colors[i].G); writer.Write(Colors[i].B);
                    writer.Write(Normals[i].X); writer.Write(Normals[i].Y); writer.Write(Normals[i].Z);
                    writer.Write(Intensities[i]); writer.Write(Classes[i]);
                }
                writer.Write((byte)3); writer.Write(0); writer.Write(1); writer.Write(2);
            }
            else
            {
                for (var i = 0; i < 3; ++i)
                {
                    var row = FormattableString.Invariant($"{Positions[i].X} {Positions[i].Y} {Positions[i].Z} {Colors[i].R} {Colors[i].G} {Colors[i].B} {Normals[i].X} {Normals[i].Y} {Normals[i].Z} {Intensities[i]} {Classes[i]}") + newline;
                    var bytes = Encoding.ASCII.GetBytes(row); output.Write(bytes, 0, bytes.Length);
                }
                var face = Encoding.ASCII.GetBytes("3 0 1 2" + newline); output.Write(face, 0, face.Length);
            }
            return output.ToArray();
        }
        private static void AssertOpen(InitialShortReadStream stream, string context)
        {
            Assert.That(stream.CanRead, Is.True, context);
            Assert.That(stream.DisposeCount, Is.Zero, context);
        }
        private static void CheckHeader(int limit, string format, string newline, bool logging)
        {
            var context = $"limit={limit}, format={format}, newline={newline.Length}, logging={logging}";
            var bytes = Header(format, newline);
            using var stream = new InitialShortReadStream(bytes, limit);
            var logs = new List<string>();
            var header = PlyParser.ParseHeader(stream, logging ? logs.Add : null);
            var expectedFormat = format == "ascii" ? PlyParser.Format.Ascii : format == "binary_little_endian" ? PlyParser.Format.BinaryLittleEndian : PlyParser.Format.BinaryBigEndian;
            Assert.That(header.Format, Is.EqualTo(expectedFormat), context);
            Assert.That(header.HeaderLines, Is.EqualTo(HeaderLines(format).Skip(1)), context);
            Assert.That(header.Elements.Select(e => e.Name), Is.EqualTo(new[] { "vertex", "face" }), context);
            Assert.That(header.Vertex!.Count, Is.EqualTo(3), context);
            Assert.That(header.Vertex.Properties.Select(p => p.Name), Is.EqualTo(new[] { "x", "y", "z", "red", "green", "blue", "nx", "ny", "nz", "intensity", "classification" }), context);
            Assert.That(header.Vertex.Properties.Select(p => p.DataType), Is.EqualTo(new[] { PlyParser.DataType.Float32, PlyParser.DataType.Float32, PlyParser.DataType.Float32, PlyParser.DataType.UInt8, PlyParser.DataType.UInt8, PlyParser.DataType.UInt8, PlyParser.DataType.Float32, PlyParser.DataType.Float32, PlyParser.DataType.Float32, PlyParser.DataType.Int32, PlyParser.DataType.UInt8 }), context);
            Assert.That(header.Face!.Count, Is.EqualTo(1), context);
            Assert.That(header.Face.Properties[0], Is.EqualTo(new PlyParser.Property(PlyParser.DataType.Int32, "vertex_indices", PlyParser.DataType.UInt8)), context);
            Assert.That(header.DataOffset, Is.EqualTo(bytes.Length - (newline == "\r\n" ? 1 : 0)), context);
            Assert.That(stream.Position, Is.EqualTo(header.DataOffset), context);
            Assert.That(stream.Rewinds, Is.EqualTo(1), context);
            Assert.That(stream.ReadByte(), Is.EqualTo(newline == "\r\n" ? '\n' : -1), context);
            Assert.That(logs, Is.EqualTo(logging ? HeaderLines(format).Skip(1).Select(s => "[PlyParser] " + s) : Array.Empty<string>()), context);
            AssertOpen(stream, context);
        }
        private static void CheckData(int limit, bool binary, string newline, bool logging)
        {
            var context = $"limit={limit}, binary={binary}, newline={newline.Length}, logging={logging}";
            var bytes = FileBytes(binary, newline);
            using var stream = new InitialShortReadStream(bytes, limit);
            var logs = new List<string>();
            var dataset = PlyParser.Parse(stream, 2, logging ? logs.Add : null);
            var data = dataset.Data.ToArray();
            var vertices = data.Where(e => e.Element.Type == PlyParser.ElementType.Vertex).ToArray();
            Assert.That(vertices.Select(e => e.Data[0].Data.Length), Is.EqualTo(binary ? new[] { 2, 1 } : new[] { 3 }), context);
            T[] Column<T>(string name) => vertices.SelectMany(v => (T[])v[name]!.Data).ToArray();
            Assert.That(Column<float>("x"), Is.EqualTo(Positions.Select(p => (float)p.X)), context);
            Assert.That(Column<float>("y"), Is.EqualTo(Positions.Select(p => (float)p.Y)), context);
            Assert.That(Column<float>("z"), Is.EqualTo(Positions.Select(p => (float)p.Z)), context);
            Assert.That(Column<byte>("red"), Is.EqualTo(Colors.Select(c => c.R)), context);
            Assert.That(Column<byte>("green"), Is.EqualTo(Colors.Select(c => c.G)), context);
            Assert.That(Column<byte>("blue"), Is.EqualTo(Colors.Select(c => c.B)), context);
            Assert.That(Column<float>("nx"), Is.EqualTo(Normals.Select(n => n.X)), context);
            Assert.That(Column<float>("ny"), Is.EqualTo(Normals.Select(n => n.Y)), context);
            Assert.That(Column<float>("nz"), Is.EqualTo(Normals.Select(n => n.Z)), context);
            Assert.That(Column<int>("intensity"), Is.EqualTo(Intensities), context);
            Assert.That(Column<byte>("classification"), Is.EqualTo(Classes), context);
            var face = data.Single(e => e.Element.Type == PlyParser.ElementType.Face);
            Assert.That(((int[][])face["vertex_indices"]!.Data)[0], Is.EqualTo(new[] { 0, 1, 2 }), context);
            Assert.That(stream.Position, Is.EqualTo(bytes.Length), context);
            Assert.That(logs.Count > 0, Is.EqualTo(logging), context);
            AssertOpen(stream, context);
        }
        private static void CheckChunks(int limit, bool binary, string newline, int size)
        {
            var context = $"limit={limit}, binary={binary}, newline={newline.Length}, chunkSize={size}";
            var bytes = FileBytes(binary, newline);
            using var stream = new InitialShortReadStream(bytes, limit);
            var config = ParseConfig.Default.WithMaxChunkPointCount(size).WithPartIndexOffset(42).WithEnabledPartIndices(true);
            var chunks = PlyImport.Chunks(stream, bytes.Length, config).ToArray();
            Assert.That(chunks.Select(c => c.Count), Is.EqualTo(binary && size < 3 ? size == 1 ? new[] { 1, 1, 1 } : new[] { 2, 1 } : new[] { 3 }), context);
            Assert.That(chunks.SelectMany(c => c.Positions), Is.EqualTo(Positions), context);
            Assert.That(chunks.SelectMany(c => c.Colors!), Is.EqualTo(Colors), context);
            Assert.That(chunks.SelectMany(c => c.Normals!), Is.EqualTo(Normals), context);
            Assert.That(chunks.SelectMany(c => c.Intensities!), Is.EqualTo(Intensities), context);
            Assert.That(chunks.SelectMany(c => c.Classifications!), Is.EqualTo(Classes), context);
            foreach (var chunk in chunks)
            {
                Assert.That(new[] { chunk.Positions.Count, chunk.Colors!.Count, chunk.Normals!.Count, chunk.Intensities!.Count, chunk.Classifications!.Count }, Is.All.EqualTo(chunk.Count), context);
                foreach (var p in chunk.Positions) Assert.That(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z), Is.True, context);
                Assert.That(chunk.BoundingBox, Is.EqualTo(new Box3d(chunk.Positions)), context);
                Assert.That(chunk.PartIndices, Is.EqualTo(42), context);
                Assert.That(chunk.PartIndexRange, Is.EqualTo(new Range1i(42, 42)), context);
            }
            Assert.That(stream.Position, Is.EqualTo(bytes.Length), context);
            AssertOpen(stream, context);
        }
        private static IEnumerable<(bool Binary, string Newline)> PayloadLayouts()
        {
            yield return (false, "\n"); yield return (false, "\r\n");
            // Binary CRLF framing and non-native endian decoding are deliberately not repaired here.
            yield return (true, "\n");
        }

        [Test]
        public void InitialShortReadsAcceptAllDeclaredHeaderFormatsAndLineEndings()
        {
            foreach (var limit in new[] { 1, 2 })
            foreach (var format in new[] { "ascii", "binary_little_endian", "binary_big_endian" })
            foreach (var newline in new[] { "\n", "\r\n" })
            foreach (var logging in new[] { false, true }) CheckHeader(limit, format, newline, logging);
        }
        [Test]
        public void InitialShortReadsPreserveCompleteAsciiAndLittleEndianData()
        {
            foreach (var limit in new[] { 1, 2 })
            foreach (var layout in PayloadLayouts())
            foreach (var logging in new[] { false, true }) CheckData(limit, layout.Binary, layout.Newline, logging);
        }
        [Test]
        public void InitialShortReadsKeepStreamChunkAttributesAligned()
        {
            foreach (var limit in new[] { 1, 2 })
            foreach (var layout in PayloadLayouts())
            foreach (var size in new[] { 1, 2, 10 }) CheckChunks(limit, layout.Binary, layout.Newline, size);
        }
        [Test]
        public void NormalReadsRetainHeaderPayloadChunkAndCallerOwnershipContracts()
        {
            foreach (var format in new[] { "ascii", "binary_little_endian", "binary_big_endian" })
            foreach (var newline in new[] { "\n", "\r\n" })
            foreach (var logging in new[] { false, true }) CheckHeader(0, format, newline, logging);
            foreach (var layout in PayloadLayouts())
            {
                foreach (var logging in new[] { false, true }) CheckData(0, layout.Binary, layout.Newline, logging);
                foreach (var size in new[] { 1, 2, 10 }) CheckChunks(0, layout.Binary, layout.Newline, size);
            }
        }
        [Test]
        public void InvalidMagicFullFirstLinesAndSeekFailuresRetainDiagnosticsAndOwnership()
        {
            foreach (var text in new[] { "", "p", "pl", "xly", "pLy", "plz", "PLY", "plyx\n", "ply \n", "ply\t\r\n", "ply\0\n" })
            foreach (var limit in new[] { 0, 1, 2 })
            {
                using var stream = new InitialShortReadStream(Encoding.ASCII.GetBytes(text), limit);
                Assert.That(Assert.Throws<Exception>(() => PlyParser.ParseHeader(stream, null))!.Message, Is.EqualTo("Not a ply file."), $"input={text}, limit={limit}");
                AssertOpen(stream, $"input={text}, limit={limit}");
            }
            using var nonseekable = new InitialShortReadStream(Header("ascii", "\n"), 0, seekable: false);
            Assert.Throws<NotSupportedException>(() => PlyParser.ParseHeader(nonseekable, null));
            AssertOpen(nonseekable, "non-seekable source");
            using var offset = new InitialShortReadStream(Encoding.ASCII.GetBytes("junk\n" + Encoding.ASCII.GetString(Header("ascii", "\n"))), 0);
            offset.Position = 5;
            Assert.That(Assert.Throws<Exception>(() => PlyParser.ParseHeader(offset, null))!.Message, Is.EqualTo("Not a ply file."));
            AssertOpen(offset, "rewind must still validate position-zero first line");
            using var invalidFormat = new InitialShortReadStream(Encoding.ASCII.GetBytes("ply\nformat bad 1.0\nend_header\n"), 0);
            Assert.That(Assert.Throws<Exception>(() => PlyParser.ParseHeader(invalidFormat, null))!.Message, Does.StartWith("Expected \"format [ascii|binary_little_endian|binary_big_endian] 1.0\""));
            AssertOpen(invalidFormat, "invalid format");
            using var throwingLog = new InitialShortReadStream(Header("ascii", "\n"), 0);
            var failure = new InvalidOperationException("log callback failed");
            Assert.That(Assert.Throws<InvalidOperationException>(() => PlyParser.ParseHeader(throwingLog, _ => throw failure)), Is.SameAs(failure));
            AssertOpen(throwingLog, "throwing log callback");
        }
    }
}
