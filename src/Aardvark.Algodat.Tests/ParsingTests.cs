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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

        [TestCase("1e3", 1000.0f)]
        [TestCase("1E+3", 1000.0f)]
        [TestCase("-2e-2", -0.02f)]
        [TestCase("+.5e+2", 50.0f)]
        [TestCase("5.e-1", 0.5f)]
        [TestCase("0e9999", 0.0f)]
        [TestCase("1.25e2 ", 125.0f)]
        [TestCase("1.25e2\t", 125.0f)]
        [TestCase("1.25e2\r", 125.0f)]
        [TestCase("1.25e2\n", 125.0f)]
        [TestCase("1.25e2\r\n", 125.0f)]
        public void ParseAscii_Float32ScientificNotationAndDelimiters(string text, float expected)
            => ParseAscii_Float32_Test(text, expected);

        [TestCase(".")]
        [TestCase("+")]
        [TestCase("-.")]
        [TestCase("e3")]
        [TestCase("1e")]
        [TestCase("1e+")]
        [TestCase("1e-")]
        [TestCase("1..2")]
        [TestCase("1e2x")]
        public void ParseAscii_Float32MalformedTokensAreSkippedAndParsingRecovers(string malformed)
        {
            var buffer = Encoding.ASCII.GetBytes($"{malformed}\n2.5e0\n");
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, new[] { Token.NormalX }, partIndices: null);

            ClassicAssert.AreEqual(1, data.Count);
            ClassicAssert.IsTrue(data.Normals[0].X.ApproximateEquals(2.5f, 1e-6f));
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

        [TestCase("1e3", 1000.0)]
        [TestCase("1E+3", 1000.0)]
        [TestCase("-2e-12", -2e-12)]
        [TestCase("+.5e+2", 50.0)]
        [TestCase("5.e-1", 0.5)]
        [TestCase("0e9999", 0.0)]
        [TestCase("1.25e2 ", 125.0)]
        [TestCase("1.25e2\t", 125.0)]
        [TestCase("1.25e2\r", 125.0)]
        [TestCase("1.25e2\n", 125.0)]
        [TestCase("1.25e2\r\n", 125.0)]
        public void ParseAscii_Float64ScientificNotationAndDelimiters(string text, double expected)
            => ParseAscii_Float64_Test(text, expected);

        [TestCase(".")]
        [TestCase("+")]
        [TestCase("-.")]
        [TestCase("e3")]
        [TestCase("1e")]
        [TestCase("1e+")]
        [TestCase("1e-")]
        [TestCase("1..2")]
        [TestCase("1e2x")]
        public void ParseAscii_Float64MalformedTokensAreSkippedAndParsingRecovers(string malformed)
        {
            var buffer = Encoding.ASCII.GetBytes($"{malformed}\n2.5e0\n");
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, new[] { Token.PositionX }, partIndices: null);

            ClassicAssert.AreEqual(1, data.Count);
            ClassicAssert.IsTrue(data.Positions[0].X.ApproximateEquals(2.5, 1e-15));
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

        [TestCase("\n")]
        [TestCase("\r\n")]
        public void ParseAscii_MultilineRecordsEndingInNormalsSupportCommonLineEndings(string newline)
        {
            var text = $"1e0 2e0 3e0 1e-1 2e-1 3e-1{newline}4e0 5e0 6e0 4e-1 5e-1 6e-1{newline}";
            var buffer = Encoding.ASCII.GetBytes(text);
            var layout = new[]
            {
                Token.PositionX, Token.PositionY, Token.PositionZ,
                Token.NormalX, Token.NormalY, Token.NormalZ
            };

            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);
            var durableData = LineParsers.CustomDurable(buffer, buffer.Length, 0.0, layout, partIndices: null);

            ClassicAssert.AreEqual(2, data.Count);
            CollectionAssert.AreEqual(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) }, data.Positions);
            ClassicAssert.IsTrue(data.Normals[0].ApproximateEquals(new V3f(0.1, 0.2, 0.3), 1e-6f));
            ClassicAssert.IsTrue(data.Normals[1].ApproximateEquals(new V3f(0.4, 0.5, 0.6), 1e-6f));
            CollectionAssert.AreEqual(data.Positions, durableData.Positions);
            CollectionAssert.AreEqual(data.Normals, durableData.Normals);
        }

        [Test]
        public void ParseAscii_TrailingWhitespaceAfterLastFieldIsAccepted()
        {
            var buffer = Encoding.ASCII.GetBytes("1e0\t2e0 3e0 \t  \r\n");
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ };

            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);

            ClassicAssert.AreEqual(1, data.Count);
            ClassicAssert.AreEqual(new V3d(1, 2, 3), data.Positions[0]);
        }

        [TestCase("1e")]
        [TestCase("1e+")]
        [TestCase("-.")]
        public void ParseAscii_IncompleteFloatingTokensAtBufferEndAreRejected(string text)
        {
            var buffer = Encoding.ASCII.GetBytes(text);
            var doubles = LineParsers.Custom(buffer, buffer.Length, 0.0, new[] { Token.PositionX }, partIndices: null);
            var floats = LineParsers.Custom(buffer, buffer.Length, 0.0, new[] { Token.NormalX }, partIndices: null);

            ClassicAssert.IsTrue(doubles.IsEmpty);
            ClassicAssert.IsTrue(floats.IsEmpty);
        }

        [TestCase("1 2")]
        [TestCase("1 2 ")]
        [TestCase("1 2\n")]
        [TestCase("1 2\r\n")]
        public void ParseAscii_TruncatedRecordsAreRejected(string text)
        {
            var buffer = Encoding.ASCII.GetBytes(text);
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ };

            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout, partIndices: null);

            ClassicAssert.IsTrue(data.IsEmpty);
        }

        [Test]
        public void ParseAscii_ScannersRespectLogicalBufferEnd()
        {
            var floatBuffer = Encoding.ASCII.GetBytes("1e3x");
            var floatData = LineParsers.Custom(floatBuffer, 3, 0.0, new[] { Token.PositionX }, partIndices: null);
            ClassicAssert.AreEqual(1, floatData.Count);
            ClassicAssert.AreEqual(1000.0, floatData.Positions[0].X);

            var byteBuffer = Encoding.ASCII.GetBytes("255x");
            var byteData = LineParsers.Custom(byteBuffer, 3, 0.0, new[] { Token.ColorR }, partIndices: null);
            ClassicAssert.AreEqual(1, byteData.Count);
            ClassicAssert.AreEqual(255, byteData.Colors[0].R);

            var skipBuffer = Encoding.ASCII.GetBytes("1 opaque");
            var skipData = LineParsers.Custom(skipBuffer, skipBuffer.Length, 0.0, new[] { Token.PositionX, Token.Skip }, partIndices: null);
            ClassicAssert.AreEqual(1, skipData.Count);
            ClassicAssert.AreEqual(1.0, skipData.Positions[0].X);
        }

        [Test]
        public void ParseAscii_ScientificNotationPreservesMinimumDistanceFiltering()
        {
            var buffer = Encoding.ASCII.GetBytes("0e0 0 0\n1e-1 0 0\n1e0 0 0\n");
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ };

            var data = LineParsers.Custom(buffer, buffer.Length, 0.5, layout, partIndices: null);

            CollectionAssert.AreEqual(new[] { V3d.Zero, V3d.XAxis }, data.Positions);
        }

        #endregion
    }
}
