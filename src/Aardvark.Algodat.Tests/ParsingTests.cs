/*
    Copyright (C) 2006-2022. Aardvark Platform Team. http://github.com/aardvark-platform.
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
            var buffer = new byte[0];
            var ms = new MemoryStream(buffer);

            var xs = ms.ChunkStreamAtNewlines(10, 10, CancellationToken.None);
            Assert.IsTrue(xs.Count() == 0);
        }

        [Test]
        public void ChunkStreamAtNewlines_ChunkingWithoutNewlinesDoesNotExceedGivenChunkSize()
        {
            var buffer = new byte[10];
            var ms = new MemoryStream(buffer);

            var xs = ms.ChunkStreamAtNewlines(10, 5, CancellationToken.None).ToArray();
            Assert.IsTrue(xs.Length == 2);
            Assert.IsTrue(xs[0].Count == 5);
            Assert.IsTrue(xs[1].Count == 5);
        }

        [Test]
        public void ChunkStreamAtNewlines_ChunkingWithNewlinesWorks()
        {
            var buffer = new byte[10] { 0, 0, 10, 0, 0, 10, 0, 0, 10, 0 };
            var ms = new MemoryStream(buffer);

            var xs = ms.ChunkStreamAtNewlines(10, 5, CancellationToken.None).ToArray();
            Assert.IsTrue(xs.Length == 4);
            Assert.IsTrue(xs[0].Count == 3);
            Assert.IsTrue(xs[1].Count == 3);
            Assert.IsTrue(xs[2].Count == 3);
            Assert.IsTrue(xs[3].Count == 1);
        }

        #endregion

        #region ParseBuffers

        [Test]
        public void ParseBuffers_Works()
        {
            var buffer = new byte[10] { 1, 1, 10, 2, 2, 10, 3, 3, 10, 4 };
            var ms = new MemoryStream(buffer);

            static Chunk parse(byte[] _, int __, double ___) => new Chunk(new[] { V3d.Zero });

            var xs = ms
                .ChunkStreamAtNewlines(10, 5, CancellationToken.None)
                .ParseBuffers(buffer.LongLength, parse, 0.0, 0, true, CancellationToken.None)
                .ToArray();
            Assert.IsTrue(xs != null);
        }

        #endregion

        #region ASCII Parsing

        #region Float32

        private static void ParseAscii_Float32_Test(string txt, float result)
        {
            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.NormalX };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Normals[0].X.ApproximateEquals(result, 10e-7f));
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
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Positions[0].X.ApproximateEquals(result, 10e-15));
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
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Intensities[0] == result);
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
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            if (isInvalid)
            {
                Assert.IsTrue(data == null);
            }
            else
            {
                Assert.IsTrue(data != null && data.Count == 1);
                Assert.IsTrue(data.Colors[0].R == result);
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
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            if (isInvalid)
            {
                Assert.IsTrue(data == null);
            }
            else
            {
                Assert.IsTrue(data != null && data.Count == 1);
                Assert.IsTrue(data.Colors[0].R == result);
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
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
        }

        [Test]
        public void ParseAscii_Custom_Color()
        {
            var txt = @"7 42 255 127";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.ColorR, Token.ColorG, Token.ColorB, Token.ColorA };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Colors[0] == new C4b(7, 42, 255, 127));
        }

        [Test]
        public void ParseAscii_Custom_ColorFloat()
        {
            var txt = @"0.0 0.5 1.0 0.8";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.ColorRf, Token.ColorGf, Token.ColorBf, Token.ColorAf };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Colors[0] == new C4b(0, 127, 255, 204));
        }

        [Test]
        public void ParseAscii_Custom_Normal()
        {
            var txt = @"0.0 0.1 0.8";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.NormalX, Token.NormalY, Token.NormalZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Normals[0] == new V3f(0.0, 0.1, 0.8));
        }

        [Test]
        public void ParseAscii_Custom_Intensity()
        {
            var txt = @"31415";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.Intensity };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Intensities[0] == 31415);
        }


        [Test]
        public void ParseAscii_SingleLine()
        {
            var txt = @"1.2 3.4 5.6 8 254 97 6543 0.1 0.2 0.3";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new [] { Token.PositionX, Token.PositionY, Token.PositionZ, Token.ColorR, Token.ColorG, Token.ColorB, Token.Intensity, Token.NormalX, Token.NormalY, Token.NormalZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Colors[0] == new C4b(8, 254, 97));
            Assert.IsTrue(data.Intensities[0] == 6543);
            Assert.IsTrue(data.Normals[0] == new V3f(0.1, 0.2, 0.3));
        }

        [Test]
        public void ParseAscii_SingleLine_WithSkip()
        {
            var txt = @"1.2 3.4 5.6 8 254 97 6543 0.1 0.2 0.3";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ, Token.ColorR, Token.ColorG, Token.ColorB, Token.Skip, Token.NormalX, Token.NormalY, Token.NormalZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Colors[0] == new C4b(8, 254, 97));
            Assert.IsTrue(data.Normals[0] == new V3f(0.1, 0.2, 0.3));
        }

        [Test]
        public void ParseAscii_EmptyLinesPre()
        {
            var txt = @"



1.2 3.4 5.6";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
        }

        [Test]
        public void ParseAscii_EmptyLinesPost()
        {
            var txt = @"1.2 3.4 5.6



";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var layout = new[] { Token.PositionX, Token.PositionY, Token.PositionZ };
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 1);
            Assert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
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
            var data = LineParsers.Custom(buffer, buffer.Length, 0.0, layout);
            Assert.IsTrue(data != null && data.Count == 2);
            Assert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Positions[1] == new V3d(5.5, 6.6, 7.7));
        }

        [Test]
        public void ParseAscii_XYZRGB_0()
        {
            var txt = @"
                1.2 3.4 5.6 8 254 97
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data != null && data.Count == 1);

            Assert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Colors[0] == new C4b(8, 254, 97));
        }

        [Test]
        public void ParseAscii_XYZRGB_PartialXYZRG()
        {
            var txt = @"
                1.2 3.4 5.6 8 254
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data == null);
        }

        [Test]
        public void ParseAscii_XYZRGB_PartialXYZR()
        {
            var txt = @"
                1.2 3.4 5.6 8
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data == null);
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
            var data = LineParsers.XYZRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data != null && data.Count == 1);

            Assert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Colors[0] == new C4b(10, 20, 30));
        }


        [Test]
        public void ParseAscii_XYZIRGB_0()
        {
            var txt = @"
                1.2 3.4 5.6 8765 8 254 97
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZIRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data != null && data.Count == 1);

            Assert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Colors[0] == new C4b(8, 254, 97));
            Assert.IsTrue(data.Intensities[0] == 8765);
        }

        [Test]
        public void ParseAscii_XYZIRGB_PartialXYZRG()
        {
            var txt = @"
                1.2 3.4 5.6 8765 8 254
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZIRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data == null);
        }

        [Test]
        public void ParseAscii_XYZIRGB_PartialXYZR()
        {
            var txt = @"
                1.2 3.4 5.6 8765 8
                ";

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = LineParsers.XYZIRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data == null);
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
            var data = LineParsers.XYZIRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data != null && data.Count == 1);

            Assert.IsTrue(data.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Colors[0] == new C4b(10, 20, 30));
            Assert.IsTrue(data.Intensities[0] == 8765);
        }

        #endregion
    }
}
