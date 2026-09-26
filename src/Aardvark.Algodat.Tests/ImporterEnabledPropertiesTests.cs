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
using Aardvark.Data.Points.Import;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Text;
using AsciiImporter = Aardvark.Data.Points.Import.Ascii;
using PlyImporter = Aardvark.Data.Points.Import.Ply;
using static Aardvark.Data.Points.Import.Ascii;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ImporterEnabledPropertiesTests
    {
        private const string CustomAsciiText =
            "1 2 3 0.1 0.2 0.3 10 20 30 40 7\n" +
            "4 5 6 0.4 0.5 0.6 50 60 70 80 9\n";

        private const string PtsText =
            "2\n" +
            "1 2 3 99 10 20 30\n" +
            "4 5 6 88 40 50 60\n";

        private const string YxhText =
            "1 2 3 7 10 20 30\n" +
            "4 5 6 9 40 50 60\n";

        private const string PlyText =
            "ply\n" +
            "format ascii 1.0\n" +
            "element vertex 3\n" +
            "property double x\n" +
            "property double y\n" +
            "property double z\n" +
            "property float nx\n" +
            "property float ny\n" +
            "property float nz\n" +
            "property uchar red\n" +
            "property uchar green\n" +
            "property uchar blue\n" +
            "property uchar alpha\n" +
            "property ushort intensity\n" +
            "property uchar classification\n" +
            "end_header\n" +
            "1 2 3 0 0 1 10 20 30 40 7 2\n" +
            "4 5 6 1 0 0 50 60 70 80 200 4\n" +
            "7 8 9 0 1 0 90 100 110 120 42 6\n";

        private static readonly V3d[] ExpectedPositions =
        {
            new V3d(1, 2, 3),
            new V3d(4, 5, 6),
        };

        private static Token[] CreateCustomLayout() => new[]
        {
            Token.PositionX, Token.PositionY, Token.PositionZ,
            Token.NormalX, Token.NormalY, Token.NormalZ,
            Token.ColorR, Token.ColorG, Token.ColorB, Token.ColorA,
            Token.Intensity,
        };

        private static ParseConfig Selection(
            bool colors = true,
            bool normals = true,
            bool intensities = true,
            bool classifications = true,
            bool partIndices = false)
            => ParseConfig.Default
                .WithMaxChunkPointCount(1024)
                .WithReadBufferSizeInBytes(1024)
                .WithEnabledColors(colors)
                .WithEnabledNormals(normals)
                .WithEnabledIntensities(intensities)
                .WithEnabledClassifications(classifications)
                .WithEnabledPartIndices(partIndices)
                .WithPartIndexOffset(42);

        private static MemoryStream StreamFor(string text)
            => new(Encoding.ASCII.GetBytes(text), writable: false);

        private static string WriteTempFile(string extension, string text)
        {
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{extension}");
            File.WriteAllText(path, text, Encoding.ASCII);
            return path;
        }

        private static void AssertSelection(
            Chunk chunk,
            bool colors,
            bool normals,
            bool intensities,
            bool classifications,
            bool partIndices)
        {
            Assert.Multiple(() =>
            {
                Assert.That(chunk.HasColors, Is.EqualTo(colors));
                Assert.That(chunk.HasNormals, Is.EqualTo(normals));
                Assert.That(chunk.HasIntensities, Is.EqualTo(intensities));
                Assert.That(chunk.HasClassifications, Is.EqualTo(classifications));
                Assert.That(chunk.HasPartIndices, Is.EqualTo(partIndices));
                if (colors) Assert.That(chunk.Colors!.Count, Is.EqualTo(chunk.Count));
                if (normals) Assert.That(chunk.Normals!.Count, Is.EqualTo(chunk.Count));
                if (intensities) Assert.That(chunk.Intensities!.Count, Is.EqualTo(chunk.Count));
                if (classifications) Assert.That(chunk.Classifications!.Count, Is.EqualTo(chunk.Count));
                if (partIndices) Assert.That(chunk.PartIndices, Is.EqualTo(42));
            });
        }

        private static Chunk ParseCustom(Token[] layout, ParseConfig config)
        {
            using var stream = StreamFor(CustomAsciiText);
            return AsciiImporter.Chunks(stream, stream.Length, layout, config).Single();
        }

        [TestCase("colors")]
        [TestCase("normals")]
        [TestCase("intensities")]
        public void CustomAsciiHonorsEachStandardPropertyFlag(string disabled)
        {
            var layout = CreateCustomLayout();
            var original = (Token[])layout.Clone();
            var config = Selection(
                colors: disabled != "colors",
                normals: disabled != "normals",
                intensities: disabled != "intensities");

            var chunk = ParseCustom(layout, config);

            AssertSelection(
                chunk,
                colors: disabled != "colors",
                normals: disabled != "normals",
                intensities: disabled != "intensities",
                classifications: false,
                partIndices: false);
            Assert.That(layout, Is.EqualTo(original), "The caller's layout was modified.");
            Assert.That(chunk.Positions, Is.EqualTo(ExpectedPositions));
            if (chunk.HasColors) Assert.That(chunk.Colors, Is.EqualTo(new[] { new C4b(10, 20, 30, 40), new C4b(50, 60, 70, 80) }));
            if (chunk.HasNormals) Assert.That(chunk.Normals, Is.EqualTo(new[] { new V3f(0.1f, 0.2f, 0.3f), new V3f(0.4f, 0.5f, 0.6f) }));
            if (chunk.HasIntensities) Assert.That(chunk.Intensities, Is.EqualTo(new[] { 7, 9 }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CustomAsciiAllDisabledKeepsPartIndexSelectionIndependent(bool partIndices)
        {
            var layout = CreateCustomLayout();
            var original = (Token[])layout.Clone();
            var chunk = ParseCustom(layout, Selection(false, false, false, false, partIndices));

            AssertSelection(chunk, false, false, false, false, partIndices);
            Assert.That(chunk.Positions, Is.EqualTo(ExpectedPositions));
            Assert.That(layout, Is.EqualTo(original), "The caller's layout was modified.");
        }

        [Test]
        public void CustomAsciiFileAndStreamSelectionsMatch()
        {
            var layout = CreateCustomLayout();
            var config = Selection();
            var path = WriteTempFile(".txt", CustomAsciiText);
            try
            {
                var fromFile = AsciiImporter.Chunks(path, layout, config).Single();
                var fromStream = ParseCustom(layout, config);
                var disabledFromFile = AsciiImporter.Chunks(path, layout, Selection(false, false, false, false, true)).Single();

                AssertSelection(fromFile, true, true, true, false, false);
                AssertSelection(disabledFromFile, false, false, false, false, true);
                Assert.That(disabledFromFile.Positions, Is.EqualTo(fromFile.Positions));
                Assert.That(fromFile.Positions, Is.EqualTo(fromStream.Positions));
                Assert.That(fromFile.Colors, Is.EqualTo(fromStream.Colors));
                Assert.That(fromFile.Normals, Is.EqualTo(fromStream.Normals));
                Assert.That(fromFile.Intensities, Is.EqualTo(fromStream.Intensities));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestCase("pts", true, true, false)]
        [TestCase("pts", false, true, true)]
        [TestCase("yxh", true, true, false)]
        [TestCase("yxh", false, true, true)]
        [TestCase("yxh", true, false, true)]
        [TestCase("yxh", false, false, true)]
        public void BuiltInAsciiImportersHonorSelection(string format, bool colors, bool intensities, bool partIndices)
        {
            var text = format == "pts" ? PtsText : YxhText;
            using var stream = StreamFor(text);
            var config = Selection(colors, normals: false, intensities, classifications: false, partIndices);
            var chunk = format == "pts"
                ? Pts.Chunks(stream, stream.Length, config).Single()
                : Yxh.Chunks(stream, stream.Length, config).Single();
            var hasIntensities = format == "yxh" && intensities;

            AssertSelection(chunk, colors, false, hasIntensities, false, partIndices);
            Assert.That(chunk.Positions, Is.EqualTo(ExpectedPositions));
            if (colors) Assert.That(chunk.Colors, Is.EqualTo(new[] { new C4b(10, 20, 30, 255), new C4b(40, 50, 60, 255) }));
            if (hasIntensities) Assert.That(chunk.Intensities, Is.EqualTo(new[] { 7, 9 }));
        }

        private static Chunk ParsePly(ParseConfig config)
        {
            using var stream = StreamFor(PlyText);
            return PlyImporter.Chunks(stream, stream.Length, config).Single();
        }

        [TestCase("none", true, true, true, true)]
        [TestCase("colors", false, true, true, true)]
        [TestCase("normals", true, false, true, true)]
        [TestCase("intensities", true, true, false, true)]
        [TestCase("classifications", true, true, true, false)]
        [TestCase("all", false, false, false, false)]
        public void PlyHonorsEveryStandardPropertyFlag(
            string _, bool colors, bool normals, bool intensities, bool classifications)
        {
            var partIndices = !colors && !normals && !intensities && !classifications;
            var chunk = ParsePly(Selection(colors, normals, intensities, classifications, partIndices));

            AssertSelection(chunk, colors, normals, intensities, classifications, partIndices);
            Assert.That(chunk.Positions, Is.EqualTo(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6), new V3d(7, 8, 9) }));
            if (colors) Assert.That(chunk.Colors, Is.EqualTo(new[] { new C4b(10, 20, 30, 40), new C4b(50, 60, 70, 80), new C4b(90, 100, 110, 120) }));
            if (normals) Assert.That(chunk.Normals, Is.EqualTo(new[] { V3f.OOI, V3f.IOO, V3f.OIO }));
            if (intensities) Assert.That(chunk.Intensities, Is.EqualTo(new[] { 7, 200, 42 }));
            if (classifications) Assert.That(chunk.Classifications, Is.EqualTo(new byte[] { 2, 4, 6 }));
        }

        [Test]
        public void PlyFileAndStreamSelectionsMatch()
        {
            var path = WriteTempFile(".ply", PlyText);
            try
            {
                var config = Selection();
                var fromFile = PlyImporter.Chunks(path, config).Single();
                var fromStream = ParsePly(config);
                var disabledFromFile = PlyImporter.Chunks(path, Selection(false, false, false, false, true)).Single();

                AssertSelection(disabledFromFile, false, false, false, false, true);
                Assert.That(disabledFromFile.Positions, Is.EqualTo(fromFile.Positions));
                Assert.That(fromFile.Positions, Is.EqualTo(fromStream.Positions));
                Assert.That(fromFile.Colors, Is.EqualTo(fromStream.Colors));
                Assert.That(fromFile.Normals, Is.EqualTo(fromStream.Normals));
                Assert.That(fromFile.Intensities, Is.EqualTo(fromStream.Intensities));
                Assert.That(fromFile.Classifications, Is.EqualTo(fromStream.Classifications));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestCase("las")]
        [TestCase("laz")]
        public void LaszipFileAndStreamSelectionPreservesPublicParserBehavior(string extension)
        {
            var path = Path.Combine(Config.TestDataDir, $"test.{extension}");
            var raw = LASZip.Parser.ReadPoints(path, 1024, verbose: false).Single();
            var all = Laszip.Chunks(path, Selection()).Single();

            Assert.Multiple(() =>
            {
                Assert.That(raw.Positions.Length, Is.EqualTo(3));
                Assert.That(raw.Colors, Is.Not.Null);
                Assert.That(raw.Intensities, Is.Not.Null);
                Assert.That(raw.Classifications, Is.Not.Null);
                Assert.That(raw.ReturnNumbers, Is.Not.Null);
                Assert.That(raw.NumberOfReturnsOfPulses, Is.Not.Null);
            });
            AssertSelection(all, true, false, true, true, false);
            Assert.That(all.Colors, Is.EqualTo(new[] { new C4b(152, 153, 154, 255), new C4b(255, 0, 128, 255), new C4b(17, 42, 201, 255) }));
            Assert.That(all.Intensities, Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(all.Classifications, Is.EqualTo(new byte[] { 0, 0, 0 }));

            foreach (var disabled in new[] { "colors", "intensities", "classifications" })
            {
                var chunk = Laszip.Chunks(path, Selection(
                    colors: disabled != "colors",
                    normals: false,
                    intensities: disabled != "intensities",
                    classifications: disabled != "classifications")).Single();
                AssertSelection(
                    chunk,
                    disabled != "colors",
                    false,
                    disabled != "intensities",
                    disabled != "classifications",
                    false);
            }

            using var stream = File.OpenRead(path);
            var selected = Laszip.Chunks(stream, stream.Length, Selection(false, false, false, false, true)).Single();
            AssertSelection(selected, false, false, false, false, true);
            Assert.That(selected.Positions, Is.EqualTo(all.Positions));
        }

        [Test]
        public void E57SelectionDoesNotAffectChunksFullOrPartIndices()
        {
            var path = Path.Combine(Config.TestDataDir, "test.e57");
            var all = E57.Chunks(path, Selection()).Single();
            AssertSelection(all, true, false, true, true, false);
            Assert.That(all.Colors, Is.EqualTo(new[] { new C4b(152, 153, 154, 255), new C4b(255, 0, 128, 255), new C4b(17, 42, 201, 255) }));
            Assert.That(all.Intensities, Is.EqualTo(new[] { 5300, 65535, 0 }));
            Assert.That(all.Classifications, Is.EqualTo(new byte[] { 0, 0, 0 }));

            var noColors = E57.Chunks(path, Selection(colors: false)).Single();
            AssertSelection(noColors, false, false, true, true, false);
            var noIntensities = E57.Chunks(path, Selection(intensities: false)).Single();
            AssertSelection(noIntensities, true, false, false, true, false);
            var noNormals = E57.Chunks(path, Selection(normals: false)).Single();
            AssertSelection(noNormals, true, false, true, true, false);
            var noClassifications = E57.Chunks(path, Selection(classifications: false)).Single();
            AssertSelection(noClassifications, true, false, true, false, false);

            var disabled = Selection(false, false, false, false, true);
            using (var stream = File.OpenRead(path))
            {
                var selected = E57.Chunks(stream, stream.Length, disabled).Single();
                AssertSelection(selected, false, false, false, false, true);
                Assert.That(selected.Positions, Is.EqualTo(all.Positions));
            }

            var full = E57.ChunksFull(path, disabled).Single();
            Assert.That(full.Colors, Is.Not.Null);
            Assert.That(full.Intensities, Is.Not.Null);
            Assert.That(full.Colors.Length, Is.EqualTo(full.Count));
            Assert.That(full.Intensities.Length, Is.EqualTo(full.Count));
        }

        [TestCase("pts")]
        [TestCase("ply")]
        [TestCase("las")]
        [TestCase("laz")]
        [TestCase("e57")]
        public void HighLevelImportsSuppressDisabledSourceAttributesButGenerateLodNormals(string format)
        {
            string path;
            var delete = false;
            switch (format)
            {
                case "pts": path = WriteTempFile(".pts", "3\n" + PtsText.Substring(2) + "7 8 9 77 70 80 90\n"); delete = true; break;
                case "ply": path = WriteTempFile(".ply", PlyText); delete = true; break;
                default: path = Path.Combine(Config.TestDataDir, $"test.{format}"); break;
            }

            try
            {
                using var storage = PointCloud.CreateInMemoryStore(cache: default);
                var enabled = new EnabledProperties()
                    .WithColors(false)
                    .WithNormals(false)
                    .WithIntensities(false)
                    .WithClassifications(false)
                    .WithPartIndices(false);
                var config = ImportConfig.Default
                    .WithStorage(storage)
                    .WithKey($"selection-{format}")
                    .WithOctreeSplitLimit(16)
                    .WithMaxChunkPointCount(1024)
                    .WithEnabledProperties(enabled);

                var pointSet = PointCloud.Import(path, config);

                Assert.Multiple(() =>
                {
                    Assert.That(pointSet.PointCount, Is.EqualTo(3));
                    Assert.That(pointSet.HasColors, Is.False);
                    Assert.That(pointSet.HasIntensities, Is.False);
                    Assert.That(pointSet.HasClassifications, Is.False);
                    Assert.That(pointSet.HasPartIndices, Is.False);
                    Assert.That(pointSet.HasNormals, Is.True, "LoD generation should still synthesize normals.");
                });
            }
            finally
            {
                if (delete) File.Delete(path);
            }
        }
    }
}
