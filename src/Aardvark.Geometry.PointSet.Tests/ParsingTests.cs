/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

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

            Func<byte[], int, double, Chunk?> parse = (_, __, ___) => new Chunk(new[] { V3d.Zero });

            var xs = ms
                .ChunkStreamAtNewlines(10, 5, CancellationToken.None)
                .ParseBuffers(buffer.LongLength, parse, 0.0, 0, true, CancellationToken.None)
                .ToArray();
            Assert.IsTrue(xs != null);
        }

        #endregion

        #region ASCII Parsing
        
        [Test]
        public void ParseAscii_XYZRGB_0()
        {
            var txt =
                "1.2 3.4 5.6 8 254 97\n"
                ;

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = HighPerformanceParsing.ParseLinesXYZRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data.HasValue && data.Value.Count == 1);

            Assert.IsTrue(data.Value.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Value.Colors[0] == new C4b(8, 254, 97));
        }

        [Test]
        public void ParseAscii_XYZRGB_XYZRG()
        {
            var txt =
                "1.2 3.4 5.6 8 254\n"
                ;

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = HighPerformanceParsing.ParseLinesXYZRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data.HasValue && data.Value.Count == 1);

            Assert.IsTrue(data.Value.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Value.Colors[0] == new C4b(8, 254, 0));
        }

        [Test]
        public void ParseAscii_XYZRGB_XYZR()
        {
            var txt =
                "1.2 3.4 5.6 8\n"
                ;

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = HighPerformanceParsing.ParseLinesXYZRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data.HasValue && data.Value.Count == 1);

            Assert.IsTrue(data.Value.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Value.Colors[0] == new C4b(8, 0, 0));
        }

        [Test]
        public void ParseAscii_XYZRGB_XYZ()
        {
            var txt =
                "1.2 3.4 5.6\n" +
                "1.2 3.4 5.6 10 20 30\n" +
                "1.2 3.4 5.6\n"
                ;

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = HighPerformanceParsing.ParseLinesXYZRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data.HasValue && data.Value.Count == 1);
        }


        [Test]
        public void ParseAscii_XYZIRGB_0()
        {
            var txt =
                "1.2 3.4 5.6 8765 8 254 97\n"
                ;

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = HighPerformanceParsing.ParseLinesXYZIRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data.HasValue && data.Value.Count == 1);

            Assert.IsTrue(data.Value.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Value.Colors[0] == new C4b(8, 254, 97));
            Assert.IsTrue(data.Value.Intensities[0] == 8765);
        }

        [Test]
        public void ParseAscii_XYZIRGB_XYZRG()
        {
            var txt =
                "1.2 3.4 5.6 8765 8 254\n"
                ;

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = HighPerformanceParsing.ParseLinesXYZIRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data.HasValue && data.Value.Count == 1);

            Assert.IsTrue(data.Value.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Value.Colors[0] == new C4b(8, 254, 0));
            Assert.IsTrue(data.Value.Intensities[0] == 8765);
        }

        [Test]
        public void ParseAscii_XYZIRGB_XYZR()
        {
            var txt =
                "1.2 3.4 5.6 8765 8\n"
                ;

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = HighPerformanceParsing.ParseLinesXYZIRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data.HasValue && data.Value.Count == 1);

            Assert.IsTrue(data.Value.Positions[0] == new V3d(1.2, 3.4, 5.6));
            Assert.IsTrue(data.Value.Colors[0] == new C4b(8, 0, 0));
            Assert.IsTrue(data.Value.Intensities[0] == 8765);
        }

        [Test]
        public void ParseAscii_XYZIRGB_XYZ()
        {
            var txt =
                "1.2 3.4 5.6 8765 \n" +
                "1.2 3.4 5.6 8765 10 20 30\n" +
                "1.2 3.4 5.6\n"
                ;

            var buffer = Encoding.ASCII.GetBytes(txt);
            var data = HighPerformanceParsing.ParseLinesXYZIRGB(buffer, buffer.Length, 0.0);
            Assert.IsTrue(data.HasValue && data.Value.Count == 2);
        }

        #endregion
    }
}
