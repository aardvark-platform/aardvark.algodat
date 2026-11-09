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
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Ascii = Aardvark.Data.Points.Import.Ascii;
using static Aardvark.Data.Points.Import.Ascii;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ParsingTests
    {
        #region Filename and stream ownership

        private static readonly Token[] FileLayout =
        {
            Token.PositionX, Token.PositionY, Token.PositionZ, Token.Intensity,
            Token.ColorR, Token.ColorG, Token.ColorB,
            Token.NormalX, Token.NormalY, Token.NormalZ, Token.ColorA
        };

        private static ParseConfig FileConfig => ParseConfig.Default
            .WithReadBufferSizeInBytes(64).WithMaxChunkPointCount(1).WithPartIndexOffset(23);

        private static string FileText(int kind, int count, string newline = "\n")
            => string.Concat(Enumerable.Range(0, count).Select(i =>
                $"{i} {i + 1} {-i} {100 + i} {10 + i % 200} 20 30" + (kind == 2 ? " 0 0 1 201" : "") + newline));

        private static IEnumerable<Chunk> FilenameChunks(int kind, string path, ParseConfig config) => kind switch
        {
            0 => Pts.Chunks(path, config),
            1 => Yxh.Chunks(path, config),
            _ => Ascii.Chunks(path, FileLayout, config)
        };

        private static IEnumerable<Chunk> StreamChunks(int kind, Stream stream, ParseConfig config) => kind switch
        {
            0 => Pts.Chunks(stream, stream.Length, config),
            1 => Yxh.Chunks(stream, stream.Length, config),
            _ => Ascii.Chunks(stream, stream.Length, FileLayout, config)
        };

        private static void WithTextFile(int kind, int count, string newline, Action<string> action)
        {
            var path = Path.Combine(Path.GetTempPath(), "algodat-ascii-lifetime-" + Guid.NewGuid() + ".txt");
            try
            {
                File.WriteAllText(path, FileText(kind, count, newline), Encoding.ASCII);
                action(path);
            }
            finally { File.Delete(path); }
        }

        private static bool CanOpenExclusive(string path)
        {
            try
            {
                using var probe = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return true;
            }
            catch (IOException) { return false; }
        }

        private static void CheckFileChunks(Chunk[] chunks, int kind, int count, string context)
        {
            var rows = chunks.SelectMany(chunk => Enumerable.Range(0, chunk.Count).Select(i =>
                (position: chunk.Positions[i], color: chunk.Colors[i],
                 intensity: chunk.Intensities == null ? (int?)null : chunk.Intensities[i],
                 normal: chunk.Normals == null ? (V3f?)null : chunk.Normals[i],
                 part: chunk.TryGetPartIndices()[i]))).OrderBy(row => row.position.X).ToArray();
            Assert.That(rows.Length, Is.EqualTo(count), context);
            for (var i = 0; i < rows.Length; i++)
            {
                var message = context + $", point={i}";
                Assert.That(double.IsFinite(rows[i].position.LengthSquared), Is.True, message);
                Assert.That(rows[i].position, Is.EqualTo(new V3d(i, i + 1, -i)), message);
                Assert.That(rows[i].color, Is.EqualTo(new C4b(10 + i % 200, 20, 30, kind == 2 ? 201 : 255)), message);
                Assert.That(rows[i].intensity, Is.EqualTo(kind == 0 ? (int?)null : 100 + i), message);
                Assert.That(rows[i].normal, Is.EqualTo(kind == 2 ? (V3f?)new V3f(0, 0, 1) : null), message);
                Assert.That(rows[i].part, Is.EqualTo(23), message);
            }
            foreach (var chunk in chunks)
            {
                Assert.That(chunk.Classifications, Is.Null, context);
                Assert.That(chunk.PartIndexRange, Is.EqualTo(new Range1i(23)), context);
            }
        }

        [Test]
        public void FilenameSequencesOpenLazilyRepeatCompletelyAndKeepPointAttributes()
        {
            Assert.Multiple(() =>
            {
                for (var kind = 0; kind < 3; kind++)
                foreach (var count in new[] { 0, 2, 32 })
                foreach (var newline in new[] { "\n", "\r\n" })
                WithTextFile(kind, count, newline, path =>
                {
                    var context = $"format={kind}, count={count}, newline bytes={newline.Length}";
                    var chunks = FilenameChunks(kind, path, FileConfig);
                    Assert.That(CanOpenExclusive(path), Is.True, context + ", sequence created");
                    using (var unused = chunks.GetEnumerator())
                        Assert.That(CanOpenExclusive(path), Is.True, context + ", enumerator not advanced");
                    CheckFileChunks(chunks.ToArray(), kind, count, context + ", first enumeration");
                    Assert.That(CanOpenExclusive(path), Is.True, context + ", first completed");
                    CheckFileChunks(chunks.ToArray(), kind, count, context + ", second enumeration");
                    Assert.That(CanOpenExclusive(path), Is.True, context + ", second completed");
                });
            });
        }

        [Test]
        public void OwnedFilesCloseOnEarlyDisposalCancellationAndConfigurationExceptions()
        {
            Assert.Multiple(() =>
            {
                for (var kind = 0; kind < 3; kind++)
                for (var scenario = 0; scenario < 6; scenario++)
                WithTextFile(kind, 32, "\n", path =>
                {
                    var context = $"format={kind}, exit={scenario}";
                    using var cancel = new CancellationTokenSource();
                    var config = FileConfig.WithCancellationToken(cancel.Token);
                    if (scenario == 1) cancel.Cancel();
                    if (scenario == 3 || scenario == 4) config = config.WithReadBufferSizeInBytes(scenario == 3 ? 0 : -1);
                    using (var iterator = FilenameChunks(kind, path, config).GetEnumerator())
                    {
                        if (scenario == 0 || scenario == 2)
                        {
                            Assert.That(iterator.MoveNext(), Is.True, context);
                            Assert.That(CanOpenExclusive(path), Is.False, context + ", active owned stream");
                            if (scenario == 2)
                            {
                                cancel.Cancel();
                                Assert.Throws<OperationCanceledException>(() => iterator.MoveNext(), context);
                                Assert.That(CanOpenExclusive(path), Is.True, context + ", cancellation already unwound");
                            }
                        }
                        else if (scenario == 1) Assert.Throws<OperationCanceledException>(() => iterator.MoveNext(), context);
                        else if (scenario == 3 || scenario == 4) Assert.Throws<ArgumentException>(() => iterator.MoveNext(), context);
                        else
                        {
                            Assert.Throws<InvalidOperationException>(() =>
                            {
                                foreach (var chunk in FilenameChunks(kind, path, config)) throw new InvalidOperationException("consumer failed");
                            }, context);
                        }
                        if (scenario == 1 || scenario == 3 || scenario == 4)
                            Assert.That(CanOpenExclusive(path), Is.True, context + ", exception already unwound");
                    }
                    Assert.That(CanOpenExclusive(path), Is.True, context + ", disposed");
                });
                WithTextFile(2, 2, "\n", path =>
                {
                    var chunks = Ascii.Chunks(path, new[] { (Token)int.MaxValue }, FileConfig);
                    Assert.Throws<AggregateException>(() => chunks.ToArray(), "parser-task configuration failure");
                    Assert.That(CanOpenExclusive(path), Is.True, "parser failure released file");
                });
            });
        }

        [Test]
        public void FilenameOpenErrorsAreDeferredAndLaterEnumerationsOpenAfresh()
        {
            Assert.Multiple(() =>
            {
                for (var kind = 0; kind < 3; kind++)
                {
                    var path = Path.Combine(Path.GetTempPath(), "algodat-ascii-lifetime-" + Guid.NewGuid() + ".txt");
                    try
                    {
                        IEnumerable<Chunk> chunks = null;
                        Assert.DoesNotThrow(() => chunks = FilenameChunks(kind, path, FileConfig), $"format={kind}, missing file sequence");
                        if (chunks == null) continue;
                        using (var iterator = chunks.GetEnumerator())
                            Assert.Throws<FileNotFoundException>(() => iterator.MoveNext(), $"format={kind}, deferred missing file");
                        File.WriteAllText(path, FileText(kind, 2), Encoding.ASCII);
                        CheckFileChunks(chunks.ToArray(), kind, 2, $"format={kind}, created after failed enumeration");
                        File.WriteAllText(path, FileText(kind, 3), Encoding.ASCII);
                        CheckFileChunks(chunks.ToArray(), kind, 3, $"format={kind}, rewritten between enumerations");
                        Assert.That(CanOpenExclusive(path), Is.True, $"format={kind}, fresh file released");
                    }
                    finally { File.Delete(path); }
                    WithTextFile(kind, 2, "\n", existing =>
                    {
                        using (var blocker = File.Open(existing, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            IEnumerable<Chunk> chunks = null;
                            Assert.DoesNotThrow(() => chunks = FilenameChunks(kind, existing, FileConfig), $"format={kind}, exclusively held file sequence");
                            if (chunks != null) Assert.Throws<IOException>(() => chunks.ToArray(), $"format={kind}, deferred sharing error");
                        }
                        Assert.That(CanOpenExclusive(existing), Is.True, $"format={kind}, failed opening left no handle");
                    });
                }
            });
        }

        [Test]
        public void CallerStreamsStayOpenAndAreNotRewoundOnCompletionDisposalOrErrors()
        {
            for (var kind = 0; kind < 3; kind++)
            for (var scenario = 0; scenario < 4; scenario++)
            WithTextFile(kind, 32, "\n", path => Assert.Multiple(() =>
            {
                var context = $"format={kind}, caller-stream exit={scenario}";
                using var cancel = new CancellationTokenSource();
                var config = FileConfig.WithCancellationToken(cancel.Token);
                if (scenario == 2) cancel.Cancel();
                if (scenario == 3) config = config.WithReadBufferSizeInBytes(0);
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var chunks = StreamChunks(kind, stream, config);
                    if (scenario == 0)
                    {
                        CheckFileChunks(chunks.ToArray(), kind, 32, context);
                        Assert.That(chunks.ToArray(), Is.Empty, context + ", no implicit rewind");
                        stream.Position = 0;
                        CheckFileChunks(chunks.ToArray(), kind, 32, context + ", explicit rewind");
                    }
                    else if (scenario == 1)
                    {
                        using var iterator = chunks.GetEnumerator();
                        Assert.That(iterator.MoveNext(), Is.True, context);
                    }
                    else if (scenario == 2) Assert.Throws<OperationCanceledException>(() => chunks.ToArray(), context);
                    else Assert.Throws<ArgumentException>(() => chunks.ToArray(), context);
                    Assert.That(stream.CanRead, Is.True, context);
                    Assert.That(CanOpenExclusive(path), Is.False, context + ", still caller-owned");
                }
                Assert.That(CanOpenExclusive(path), Is.True, context + ", caller disposed");
            }));
        }

        #endregion

        #region ChunkStreamAtNewlines

        [Test]
        public void ChunkStreamAtNewlines_ThrowsIfStreamIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                Stream stream = null;
                stream.ChunkStreamAtNewlines(0, 0, CancellationToken.None).ToArray();
            });
        }

        [Test]
        public void ChunkStreamAtNewlines_ThrowsIfMaxChunkSizeIsZeroOrNegative()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var buffer = new byte[10];
                var ms = new MemoryStream(buffer);
                ms.ChunkStreamAtNewlines(10, 0, CancellationToken.None).ToArray();
            });

            Assert.Throws<ArgumentException>(() =>
            {
                var buffer = new byte[10];
                var ms = new MemoryStream(buffer);
                ms.ChunkStreamAtNewlines(10, -1, CancellationToken.None).ToArray();
            });
        }

        [Test]
        public void ChunkStreamAtNewlines_EmptyStreamGivesEmptySequence()
        {
            var buffer = Array.Empty<byte>();
            var ms = new MemoryStream(buffer);

            var xs = ms.ChunkStreamAtNewlines(10, 10, CancellationToken.None);
            ClassicAssert.IsTrue(!xs.Any());
        }

        [Test]
        public void ChunkStreamAtNewlines_ChunkingWithoutNewlinesDoesNotExceedGivenChunkSize()
        {
            var buffer = new byte[10];
            var ms = new MemoryStream(buffer);

            var xs = ms.ChunkStreamAtNewlines(10, 5, CancellationToken.None).ToArray();
            ClassicAssert.IsTrue(xs.Length == 2);
            ClassicAssert.IsTrue(xs[0].Count == 5);
            ClassicAssert.IsTrue(xs[1].Count == 5);
        }

        [Test]
        public void ChunkStreamAtNewlines_ChunkingWithNewlinesWorks()
        {
            var buffer = new byte[10] { 0, 0, 10, 0, 0, 10, 0, 0, 10, 0 };
            var ms = new MemoryStream(buffer);

            var xs = ms.ChunkStreamAtNewlines(10, 5, CancellationToken.None).ToArray();
            ClassicAssert.IsTrue(xs.Length == 4);
            ClassicAssert.IsTrue(xs[0].Count == 3);
            ClassicAssert.IsTrue(xs[1].Count == 3);
            ClassicAssert.IsTrue(xs[2].Count == 3);
            ClassicAssert.IsTrue(xs[3].Count == 1);
        }

        #endregion

        #region ParseBuffers

        [Test]
        public void ParseBuffers_Works()
        {
            var buffer = new byte[10] { 1, 1, 10, 2, 2, 10, 3, 3, 10, 4 };
            var ms = new MemoryStream(buffer);

            static Chunk parse(byte[] _, int __, double ___, int? ____) => new(new[] { V3d.Zero });

            var config = ParseConfig.Default.WithMinDist(0.0).WithMaxDegreeOfParallelism(0).WithVerbose(true);
            var xs = ms
                .ChunkStreamAtNewlines(10, 5, CancellationToken.None)
                .ParseBuffers(buffer.LongLength, parse, config)
                .ToArray();
            ClassicAssert.IsTrue(xs != null);
        }

        #endregion

        #region ASCII Parsing

        #region Float32

        private static void ParseAscii_Float32_Test(string txt, float result)
        {
            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.NormalX };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Normals[0].X.ApproximateEquals(result, 10e-7f));
        }
        [Test]
        public void ParseAscii_Float32()
        {
            ParseAscii_Float32_Test("1.2", 1.2f);
            ParseAscii_Float32_Test("123.4567", 123.4567f);
            ParseAscii_Float32_Test("123", 123);
            ParseAscii_Float32_Test("0.45678", 0.45678f);
            ParseAscii_Float32_Test(".314", 0.314f);
            ParseAscii_Float32_Test("0", 0);

            ParseAscii_Float32_Test("-1.2", -1.2f);
            ParseAscii_Float32_Test("-123.4567", -123.4567f);
            ParseAscii_Float32_Test("-123", -123);
            ParseAscii_Float32_Test("-0.45678", -0.45678f);
            ParseAscii_Float32_Test("-.314", -0.314f);
            ParseAscii_Float32_Test("-0", 0);
        }

        #endregion

        #region Float64

        private static void ParseAscii_Float64_Test(string txt, double result)
        {
            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.PositionX };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Positions[0].X.ApproximateEquals(result, 10e-15));
        }
        [Test]
        public void ParseAscii_Float64()
        {
            ParseAscii_Float64_Test("1.2", 1.2);
            ParseAscii_Float64_Test("123.456789101259", 123.456789101259);
            ParseAscii_Float64_Test("123", 123);
            ParseAscii_Float64_Test("0.45678", 0.45678);
            ParseAscii_Float64_Test(".314", 0.314);
            ParseAscii_Float64_Test("0", 0);

            ParseAscii_Float64_Test("-1.2", -1.2);
            ParseAscii_Float64_Test("-123.456789101259", -123.456789101259);
            ParseAscii_Float64_Test("-123", -123);
            ParseAscii_Float64_Test("-0.45678", -0.45678);
            ParseAscii_Float64_Test("-.314", -0.314);
            ParseAscii_Float64_Test("-0", 0);
        }

        #endregion

        #region Int

        private static void ParseAscii_Int_Test(string txt, int result)
        {
            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.Intensity };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Intensities[0] == result);
        }
        [Test]
        public void ParseAscii_Int()
        {
            ParseAscii_Int_Test("0", 0);
            ParseAscii_Int_Test("-0", 0);
            ParseAscii_Int_Test("1", 1);
            ParseAscii_Int_Test("-1", -1);
            ParseAscii_Int_Test("459", 459);
            ParseAscii_Int_Test("-459", -459);
            ParseAscii_Int_Test("2147483647", int.MaxValue);
            ParseAscii_Int_Test("-2147483647", -int.MaxValue);
            ParseAscii_Int_Test("-2147483648", int.MinValue);
        }

        #endregion

        #region Byte

        private static void ParseAscii_Byte_Test(string txt, int result, bool isInvalid)
        {
            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.ColorR };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            if (isInvalid)
            {
                ClassicAssert.IsTrue(data.IsEmpty);
            }
            else
            {
                ClassicAssert.IsTrue(data != null && data.Count == 1);
                ClassicAssert.IsTrue(data.Colors[0].R == result);
            }
        }
        [Test]
        public void ParseAscii_Byte()
        {
            ParseAscii_Byte_Test("-1", -1, true);
            ParseAscii_Byte_Test("-0", 0, true);
            ParseAscii_Byte_Test("0", 0, false);
            ParseAscii_Byte_Test("1", 1, false);
            ParseAscii_Byte_Test("42", 42, false);
            ParseAscii_Byte_Test("177", 177, false);
            ParseAscii_Byte_Test("254", 254, false);
            ParseAscii_Byte_Test("255", 255, false);
            ParseAscii_Byte_Test("256", 256, true);
        }

        #endregion

        #region FloatColor

        private static void ParseAscii_FloatColor_Test(string txt, int result, bool isInvalid)
        {
            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.ColorRf };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            if (isInvalid)
            {
                ClassicAssert.IsTrue(data.IsEmpty);
            }
            else
            {
                ClassicAssert.IsTrue(data != null && data.Count == 1);
                ClassicAssert.IsTrue(data.Colors[0].R == result);
            }
        }
        [Test]
        public void ParseAscii_FloatColor()
        {
            ParseAscii_FloatColor_Test("-1", -1, true);
            ParseAscii_FloatColor_Test("-0.0001", 0, true);

            ParseAscii_FloatColor_Test("-0", 0, false);
            ParseAscii_FloatColor_Test("0", 0, false);
            ParseAscii_FloatColor_Test("0.0", 0, false);
            ParseAscii_FloatColor_Test(".0", 0, false);
            ParseAscii_FloatColor_Test("0.5", 127, false);
            ParseAscii_FloatColor_Test("1", 255, false);
            ParseAscii_FloatColor_Test("1.0", 255, false);

            ParseAscii_FloatColor_Test("1.00001", 0, true);
            ParseAscii_FloatColor_Test("255", 0, true);
        }

        #endregion

        [Test]
        public void ParseAscii_Custom_Position()
        {
            var txt = @"1.2 3.4 5.6";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
        }

        [Test]
        public void ParseAscii_Custom_Color()
        {
            var txt = @"7 42 255 127";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.ColorR, Token.ColorG, Token.ColorB, Token.ColorA };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Colors[0] == new C4b(7, 42, 255, 127));
        }

        [Test]
        public void ParseAscii_Custom_ColorFloat()
        {
            var txt = @"0.0 0.5 1.0 0.8";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.ColorRf, Token.ColorGf, Token.ColorBf, Token.ColorAf };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Colors[0] == new C4b(0, 127, 255, 204));
        }

        [Test]
        public void ParseAscii_Custom_Normal()
        {
            var txt = @"0.0 0.1 0.8";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.NormalX, Token.NormalY, Token.NormalZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Normals[0] == new V3f(0.0, 0.1, 0.8));
        }

        [Test]
        public void ParseAscii_Custom_Intensity()
        {
            var txt = @"31415";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.Intensity };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Intensities[0] == 31415);
        }


        [Test]
        public void ParseAscii_SingleLine()
        {
            var txt = @"1.2 3.4 5.6 8 254 97 6543 0.1 0.2 0.3";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new [] { Token.PositionX, Token.PositionY, Token.PositionZ, Token.ColorR, Token.ColorG, Token.ColorB, Token.Intensity, Token.NormalX, Token.NormalY, Token.NormalZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            ClassicAssert.IsTrue(data.Colors[0] == new C4b(8, 254, 97));
            ClassicAssert.IsTrue(data.Intensities[0] == 6543);
            ClassicAssert.IsTrue(data.Normals[0] == new V3f(0.1, 0.2, 0.3));
        }

        [Test]
        public void ParseAscii_SingleLine_WithSkip()
        {
            var txt = @"1.2 3.4 5.6 8 254 97 6543 0.1 0.2 0.3";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ, Token.ColorR, Token.ColorG, Token.ColorB, Token.Skip, Token.NormalX, Token.NormalY, Token.NormalZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            ClassicAssert.IsTrue(data.Colors[0] == new C4b(8, 254, 97));
            ClassicAssert.IsTrue(data.Normals[0] == new V3f(0.1, 0.2, 0.3));
        }

        [Test]
        public void ParseAscii_EmptyLinesPre()
        {
            var txt = @"



1.2 3.4 5.6";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
        }

        [Test]
        public void ParseAscii_EmptyLinesPost()
        {
            var txt = @"1.2 3.4 5.6



";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);
            ClassicAssert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
        }

        [Test]
        public void ParseAscii_EmptyLinesIntermediate()
        {
            var txt = @"

1.2 3.4 5.6

5.5 6.6 7.7

";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 2);
            ClassicAssert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            ClassicAssert.IsTrue(data.Positions[1] == new V3d(5.5, 6.6, 7.7));
        }

        [Test]
        public void ParseAscii_XYZRGB_0()
        {
            var txt = @"
                1.2 3.4 5.6 8 254 97
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZRGB(buffer, buffer.Length, 0.0, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);

            ClassicAssert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            ClassicAssert.IsTrue(data.Colors[0] == new C4b(8, 254, 97));
        }

        [Test]
        public void ParseAscii_XYZRGB_PartialXYZRG()
        {
            var txt = @"
                1.2 3.4 5.6 8 254
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZRGB(buffer, buffer.Length, 0.0, partIndices: null);
            ClassicAssert.IsTrue(data.IsEmpty);
        }

        [Test]
        public void ParseAscii_XYZRGB_PartialXYZR()
        {
            var txt = @"
                1.2 3.4 5.6 8
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZRGB(buffer, buffer.Length, 0.0, partIndices: null);
            ClassicAssert.IsTrue(data.IsEmpty);
        }

        [Test]
        public void ParseAscii_XYZRGB_PartialLinesAreSkipped()
        {
            var txt = @"
                8.2 3.4 5.6
                1.2 3.4 5.6 10 20 30
                9.2 3.4 5.6
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZRGB(buffer, buffer.Length, 0.0, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);

            ClassicAssert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            ClassicAssert.IsTrue(data.Colors[0] == new C4b(10, 20, 30));
        }


        [Test]
        public void ParseAscii_XYZIRGB_0()
        {
            var txt = @"
                1.2 3.4 5.6 8765 8 254 97
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZIRGB(buffer, buffer.Length, 0.0, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);

            ClassicAssert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            ClassicAssert.IsTrue(data.Colors[0] == new C4b(8, 254, 97));
            ClassicAssert.IsTrue(data.Intensities[0] == 8765);
        }

        [Test]
        public void ParseAscii_XYZIRGB_PartialXYZRG()
        {
            var txt = @"
                1.2 3.4 5.6 8765 8 254
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZIRGB(buffer, buffer.Length, 0.0, partIndices: null);
            ClassicAssert.IsTrue(data.IsEmpty);
        }

        [Test]
        public void ParseAscii_XYZIRGB_PartialXYZR()
        {
            var txt = @"
                1.2 3.4 5.6 8765 8
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZIRGB(buffer, buffer.Length, 0.0, partIndices: null);
            ClassicAssert.IsTrue(data.IsEmpty);
        }

        [Test]
        public void ParseAscii_XYZIRGB_PartialLinesAreSkipped()
        {
            var txt = @"
                1.2 3.4 5.6 8765 
                1.2 3.4 5.6 8765 10 20 30
                1.2 3.4 5.6
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZIRGB(buffer, buffer.Length, 0.0, partIndices: null);
            ClassicAssert.IsTrue(data != null && data.Count == 1);

            ClassicAssert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            ClassicAssert.IsTrue(data.Colors[0] == new C4b(10, 20, 30));
            ClassicAssert.IsTrue(data.Intensities[0] == 8765);
        }

        #endregion
    }
}
