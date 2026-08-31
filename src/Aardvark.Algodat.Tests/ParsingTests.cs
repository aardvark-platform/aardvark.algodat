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
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Aardvark.Data.Points.Import.Ascii;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ParsingTests
    {
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

        private static readonly TimeSpan s_synchronizationTimeout = TimeSpan.FromSeconds(15);

        private static IEnumerable<Aardvark.Data.Points.Buffer> CreateNumberedBuffers(int count)
        {
            for (var i = 0; i < count; i++)
                yield return Aardvark.Data.Points.Buffer.Create(new[] { (byte)i }, 0, 1);
        }

        private static void UpdatePeak(ref int peak, int value)
        {
            var current = Volatile.Read(ref peak);
            while (value > current)
            {
                var previous = Interlocked.CompareExchange(ref peak, value, current);
                if (previous == current) return;
                current = previous;
            }
        }

        private static (Chunk[] Chunks, int Peak) RunSynchronizedParse(
            int bufferCount, int maxDegreeOfParallelism, int maxChunkPointCount)
        {
            var expectedParallelism = Math.Min(
                bufferCount,
                maxDegreeOfParallelism > 0 ? maxDegreeOfParallelism : Environment.ProcessorCount);
            using var entered = new CountdownEvent(expectedParallelism);
            using var release = new ManualResetEventSlim(false);
            var active = 0;
            var peak = 0;
            var started = 0;

            Chunk Parse(byte[] data, int count, double minDist, int? partIndex)
            {
                var current = Interlocked.Increment(ref active);
                UpdatePeak(ref peak, current);
                var invocation = Interlocked.Increment(ref started);
                if (invocation <= expectedParallelism) entered.Signal();
                try
                {
                    release.Wait();
                    return new Chunk(new[] { new V3d(data[0], 0.0, 0.0) });
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            }

            var config = ParseConfig.Default
                .WithMaxDegreeOfParallelism(maxDegreeOfParallelism)
                .WithMaxChunkPointCount(maxChunkPointCount)
                .WithVerbose(false);
            var parseTask = Task.Run(() => CreateNumberedBuffers(bufferCount)
                .ParseBuffers(bufferCount, Parse, config)
                .ToArray());

            var reachedConfiguredParallelism = false;
            var activeWhileBlocked = 0;
            try
            {
                reachedConfiguredParallelism = entered.Wait(s_synchronizationTimeout);
                activeWhileBlocked = Volatile.Read(ref active);
            }
            finally
            {
                release.Set();
            }

            ClassicAssert.IsTrue(
                parseTask.Wait(s_synchronizationTimeout),
                "Synchronized ParseBuffers run did not complete.");
            ClassicAssert.IsTrue(
                reachedConfiguredParallelism,
                $"Expected {expectedParallelism} parsers to overlap, but observed {activeWhileBlocked}.");
            ClassicAssert.AreEqual(expectedParallelism, activeWhileBlocked);
            ClassicAssert.LessOrEqual(peak, expectedParallelism);

            var chunks = parseTask.Result;
            ClassicAssert.AreEqual(bufferCount, chunks.Length);
            CollectionAssert.AreEqual(
                Enumerable.Range(0, bufferCount),
                chunks.SelectMany(x => x.Positions).Select(p => (int)p.X).OrderBy(x => x));
            return (chunks, peak);
        }

        [Test]
        public void ParseBuffers_DegreeOfOnePreventsParserOverlap()
        {
            var result = RunSynchronizedParse(
                bufferCount: 8,
                maxDegreeOfParallelism: 1,
                maxChunkPointCount: 128);

            ClassicAssert.AreEqual(1, result.Peak);
        }

        [Test]
        public void ParseBuffers_ConfiguredDegreeAllowsButBoundsParserOverlap()
        {
            var result = RunSynchronizedParse(
                bufferCount: 16,
                maxDegreeOfParallelism: 3,
                maxChunkPointCount: 128);

            ClassicAssert.AreEqual(3, result.Peak);
        }

        [Test]
        public void ParseBuffers_MaxChunkPointCountDoesNotControlParserConcurrency()
        {
            var result = RunSynchronizedParse(
                bufferCount: 8,
                maxDegreeOfParallelism: 2,
                maxChunkPointCount: 1);

            ClassicAssert.AreEqual(2, result.Peak);
        }

        [Test]
        public void ParseBuffers_ParallelCompletionReturnsEveryOutputExactlyOnce()
        {
            var result = RunSynchronizedParse(
                bufferCount: 64,
                maxDegreeOfParallelism: 4,
                maxChunkPointCount: 97);

            ClassicAssert.AreEqual(64, result.Chunks.Sum(x => x.Count));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void ParseBuffers_NonPositiveDegreeUsesMapParallelDefault(int configuredDegree)
        {
            var result = RunSynchronizedParse(
                bufferCount: 4,
                maxDegreeOfParallelism: configuredDegree,
                maxChunkPointCount: 1);

            ClassicAssert.AreEqual(Math.Min(Environment.ProcessorCount, 4), result.Peak);
        }

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
            ClassicAssert.AreEqual(4, xs.Length);
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
