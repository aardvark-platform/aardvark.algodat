using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Aardvark.Base;
using Aardvark.Data.Points;
using PlyImport = Aardvark.Data.Points.Import.Ply;
using NUnit.Framework;
using Ply.Net;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PlyAsciiParserTests
    {
        private static readonly string[] Header = {
            "ply", "format ascii 1.0", "element empty_before 0", "property int marker", "element vertex 3",
            "property double x", "property double y", "property double z",
            "property uchar red", "property uchar green", "property uchar blue", "property uchar alpha",
            "property float nx", "property float ny", "property float nz", "property int intensity", "property uchar classification",
            "element calibration 3", "property int label", "property double weight",
            "element face 3", "property list uchar int vertex_indices", "property uchar material",
            "element empty_after 0", "property list uchar int refs", "end_header"
        };
        private static readonly string[][] Rows = {
            new[] { "10 20 30 1 2 3 11 0 0 1 201 7", "12 24 36 4 5 6 22 0 1 0 202 8", "14 28 42 7 8 9 33 1 0 0 203 9" },
            new[] { "11 0.5", "22 1.25", "33 -2.5" },
            new[] { "3 0 1 2 7", "0 8", "2 2 1 9" }
        };
        private static readonly string[] Elements = { "vertex", "calibration", "face" };
        private static string Document(IEnumerable<string> rows, string newline, bool blanks, bool finalNewline)
        {
            var lines = blanks ? rows.SelectMany(r => new[] { "", r }) : rows;
            return string.Join(newline, Header) + newline + string.Join(newline, lines)
                + (finalNewline ? (blanks ? newline + newline : newline) : "");
        }
        private static MemoryStream Stream(string text) => new MemoryStream(Encoding.ASCII.GetBytes(text), writable: false);
        private static IEnumerable<string> Prefix(int element, int completed) => Enumerable.Concat(Rows.Take(element).SelectMany(x => x), Rows[element].Take(completed));
        private static ParseConfig Config(bool logging, int maximum = 1) => ParseConfig.Default.WithVerbose(logging).WithMaxChunkPointCount(maximum).WithEnabledPartIndices(false);
        private static void Eof(EndOfStreamException error, string element, int completed, string context)
        {
            if (error == null) return; // Assert.Throws already records a missing exception inside Assert.Multiple.
            Assert.That(error.Message, Does.Contain("element '" + element + "'"), context + ": element identity");
            Assert.That(error.Message, Does.Contain($"read {completed} of 3 rows"), context + ": completed/declared rows");
        }

        [Test]
        public void MissingElementRowsThrowBeforeReturningAnyDataset()
        {
            Assert.Multiple(() => {
                foreach (var element in Enumerable.Range(0, 3))
                foreach (var completed in Enumerable.Range(0, 3))
                foreach (var newline in new[] { "\n", "\r\n" })
                foreach (var logging in new[] { false, true })
                foreach (var blanks in new[] { false, true })
                foreach (var finalNewline in new[] { false, true })
                {
                    var context = $"element={Elements[element]}, completed={completed}/3, CRLF={newline.Length == 2}, logging={logging}, blanks={blanks}, finalNewline={finalNewline}";
                    using var stream = Stream(Document(Prefix(element, completed), newline, blanks, finalNewline));
                    var logs = new List<string>(); PlyParser.Dataset result = null;
                    Action<string> log = logging ? logs.Add : null;
                    var error = Assert.Throws<EndOfStreamException>(() => result = PlyParser.Parse(stream, 1, log), context);
                    Eof(error, Elements[element], completed, context);
                    Assert.That(result, Is.Null, context + ": no partially populated dataset");
                    Assert.That(logs.Count > 0, Is.EqualTo(logging), context + ": header logging selection");
                }
            });
        }

        [Test]
        public void TruncatedAsciiChunksPublishNeitherPointsNorAttributes()
        {
            Assert.Multiple(() => {
                foreach (var element in Enumerable.Range(0, 3))
                foreach (var completed in Enumerable.Range(0, 3))
                foreach (var newline in new[] { "\n", "\r\n" })
                foreach (var logging in new[] { false, true })
                foreach (var blanks in new[] { false, true })
                foreach (var finalNewline in new[] { false, true })
                {
                    var context = $"element={Elements[element]}, completed={completed}/3, CRLF={newline.Length == 2}, logging={logging}, blanks={blanks}, finalNewline={finalNewline}";
                    using var stream = Stream(Document(Prefix(element, completed), newline, blanks, finalNewline));
                    var published = new List<Chunk>();
                    var error = Assert.Throws<EndOfStreamException>(() => {
                        foreach (var chunk in PlyImport.Chunks(stream, stream.Length, Config(logging))) published.Add(chunk);
                    }, context);
                    Eof(error, Elements[element], completed, context);
                    Assert.That(published, Is.Empty, context + ": no point or attribute publication, even after complete vertices");
                }
            });
        }

        private static void CompleteDataset(PlyParser.Dataset dataset, string context)
        {
            Assert.That(dataset.Header.Format, Is.EqualTo(PlyParser.Format.Ascii), context);
            var data = dataset.Data.ToArray();
            Assert.That(data.Select(d => d.Element.Name), Is.EqualTo(new[] { "empty_before", "vertex", "calibration", "face", "empty_after" }), context);
            Assert.That(data.Select(d => d.Element.Count), Is.EqualTo(new[] { 0, 3, 3, 3, 0 }), context);
            Assert.That(data[0]["marker"].Data, Is.TypeOf<int[]>().And.Empty, context);
            Assert.That(data[4]["refs"].Data, Is.TypeOf<int[][]>().And.Empty, context);
            var vertex = data[1];
            var expected = new (string Name, Array Values)[] {
                ("x", new[] { 10.0, 12, 14 }), ("y", new[] { 20.0, 24, 28 }), ("z", new[] { 30.0, 36, 42 }),
                ("red", new byte[] { 1, 4, 7 }), ("green", new byte[] { 2, 5, 8 }), ("blue", new byte[] { 3, 6, 9 }), ("alpha", new byte[] { 11, 22, 33 }),
                ("nx", new float[] { 0, 0, 1 }), ("ny", new float[] { 0, 1, 0 }), ("nz", new float[] { 1, 0, 0 }),
                ("intensity", new[] { 201, 202, 203 }), ("classification", new byte[] { 7, 8, 9 })
            };
            Assert.That(vertex.Data.Select(p => p.Property.Name), Is.EqualTo(expected.Select(e => e.Name)), context);
            foreach (var e in expected)
            {
                Assert.That(vertex[e.Name].Data.GetType(), Is.EqualTo(e.Values.GetType()), context + ": type " + e.Name);
                Assert.That(vertex[e.Name].Data, Is.EqualTo(e.Values), context + ": values " + e.Name);
            }
            Assert.That(data[2]["label"].Data, Is.EqualTo(new[] { 11, 22, 33 }), context);
            Assert.That(data[2]["weight"].Data, Is.EqualTo(new[] { .5, 1.25, -2.5 }), context);
            Assert.That(data[3]["vertex_indices"].Data, Is.EqualTo(new[] { new[] { 0, 1, 2 }, Array.Empty<int>(), new[] { 2, 1 } }), context + ": lists including zero length");
            Assert.That(data[3]["material"].Data, Is.EqualTo(new byte[] { 7, 8, 9 }), context);
        }
        private static void CompleteChunk(Chunk chunk, string context)
        {
            Assert.That(chunk.Count, Is.EqualTo(3), context);
            Assert.That(chunk.Positions, Is.EqualTo(new[] { new V3d(10,20,30), new V3d(12,24,36), new V3d(14,28,42) }), context);
            Assert.That(chunk.Colors, Is.EqualTo(new[] { new C4b(1,2,3,11), new C4b(4,5,6,22), new C4b(7,8,9,33) }), context);
            Assert.That(chunk.Normals, Is.EqualTo(new[] { V3f.ZAxis, V3f.YAxis, V3f.XAxis }), context);
            Assert.That(chunk.Intensities, Is.EqualTo(new[] { 201, 202, 203 }), context);
            Assert.That(chunk.Classifications, Is.EqualTo(new byte[] { 7, 8, 9 }), context);
            Assert.That(chunk.BoundingBox, Is.EqualTo(new Box3d(new V3d(10,20,30), new V3d(14,28,42))), context + ": bounds exclude fabricated origin");
            foreach (var p in chunk.Positions)
                Assert.That(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z), Is.True, context + ": finite position");
            foreach (var n in chunk.Normals)
                Assert.That(float.IsFinite(n.X) && float.IsFinite(n.Y) && float.IsFinite(n.Z), Is.True, context + ": finite normal");
        }

        [Test]
        public void CompleteMultiElementFilesRetainTypesValuesEmptyRowsAndLists()
        {
            foreach (var newline in new[] { "\n", "\r\n" })
            foreach (var logging in new[] { false, true })
            foreach (var blanks in new[] { false, true })
            foreach (var finalNewline in new[] { false, true })
            {
                var context = $"CRLF={newline.Length == 2}, logging={logging}, blanks={blanks}, finalNewline={finalNewline}";
                var text = Document(Rows.SelectMany(x => x), newline, blanks, finalNewline);
                using (var stream = Stream(text))
                {
                    var logs = new List<string>(); Action<string> log = logging ? logs.Add : null;
                    CompleteDataset(PlyParser.Parse(stream, 1, log), context);
                    Assert.That(logs.Count > 0, Is.EqualTo(logging), context);
                }
                foreach (var maximum in new[] { 1, 32768 })
                {
                    using var stream = Stream(text);
                    var chunks = PlyImport.Chunks(stream, stream.Length, Config(logging, maximum)).ToArray();
                    Assert.That(chunks.Length, Is.EqualTo(1), context + ": existing ASCII batching");
                    CompleteChunk(chunks[0], context + $", maximum={maximum}");
                }
                var zero = string.Join(newline, new[] { "ply", "format ascii 1.0", "element vertex 0", "property double x", "property double y", "property double z", "element face 0", "property list uchar int vertex_indices", "end_header" }) + (finalNewline ? newline : "");
                using (var stream = Stream(zero))
                {
                    var dataset = PlyParser.Parse(stream, 1);
                    Assert.That(dataset.Data.Select(e => e.Element.Count), Is.EqualTo(new[] { 0, 0 }), context + ": zero elements");
                    Assert.That(dataset.Data.Single(e => e.Element.Name == "vertex")["x"].Data, Is.TypeOf<double[]>().And.Empty, context);
                }
                using (var stream = Stream(zero))
                {
                    var chunks = PlyImport.Chunks(stream, stream.Length, Config(logging)).ToArray();
                    Assert.That(chunks.Length, Is.EqualTo(1), context + ": existing empty-vertex chunk");
                    Assert.That(chunks[0].Positions, Is.Empty, context); Assert.That(chunks[0].BoundingBox, Is.EqualTo(Box3d.Invalid), context);
                }
            }
        }
    }
}
