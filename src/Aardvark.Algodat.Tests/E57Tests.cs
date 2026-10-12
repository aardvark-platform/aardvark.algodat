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
using Aardvark.Data.Points;
using Aardvark.Data.Points.Import;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.IO;
using System.Linq;
using static Aardvark.Data.E57.ASTM_E57;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class E57Tests
    {
        [TestCase(0)]
        [TestCase(16)]
        [TestCase(936)] // data packet header crosses a checksum boundary
        [TestCase(940)] // data packet starts on the next page
        public void E57_CompressedVector_DataStartGap(int gap)
        {
            using var original = File.OpenRead(Path.Combine(Config.TestDataDir, "test.e57"));
            var expected = E57.ChunksFull(original, original.Length, ParseConfig.Default).Single();
            using var stream = CreateE57Stream(gap);
            var actual = E57.ChunksFull(stream, stream.Length, ParseConfig.Default).Single();

            Assert.That(actual.Count, Is.EqualTo(3));
            Assert.That(actual.Positions, Is.EqualTo(expected.Positions));
            Assert.That(actual.RawData.Keys, Is.EquivalentTo(expected.RawData.Keys));
            foreach (var (semantic, values) in expected.RawData)
            {
                Assert.That(actual.RawData[semantic], Is.EqualTo(values), $"{semantic}");
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void E57_CompressedVector_IndexPacket(bool beforeData)
        {
            // Place a 32-byte leaf index packet before or after the existing data packet.
            using var stream = CreateE57Stream(32);
            var dataStart = beforeData ? 112L : 80L;
            var indexStart = beforeData ? 80L : 160L;
            if (!beforeData)
            {
                var packet = new byte[80];
                stream.Position = 112;
                stream.ReadExactly(packet);
                stream.Position = dataStart;
                stream.Write(packet);
            }

            using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            stream.Position = 64;
            writer.Write(dataStart);
            writer.Write(indexStart);
            stream.Position = indexStart;
            writer.Write((byte)0); // index packet type
            writer.Write((byte)0); // reserved
            writer.Write((ushort)31); // packet length minus one
            writer.Write((ushort)1); // entry count
            writer.Write(new byte[10]); // leaf index level and reserved bytes
            writer.Write(0L); // first record index
            writer.Write(dataStart);

            var actual = E57.ChunksFull(stream, stream.Length, ParseConfig.Default.WithMaxChunkPointCount(1)).Single();
            Assert.That(actual.Count, Is.EqualTo(3));
            Assert.That(actual.Positions[0], Is.EqualTo(new Aardvark.Base.V3d(1, 2, 9)));
        }

        [TestCase(31)]
        [TestCase(33)] // only one byte of the packet header remains in the section
        public void E57_CompressedVector_InvalidSectionLength(long length)
        {
            using var stream = CreateE57Stream(0);
            stream.Position = 56; // compressed vector section length
            stream.Write(BitConverter.GetBytes(length));

            Assert.That(() => E57.ChunksFull(stream, stream.Length, ParseConfig.Default).ToArray(),
                Throws.TypeOf<InvalidDataException>());
        }

        [TestCase(79)] // inside the section header
        [TestCase(160)] // no room for the declared nonempty point data
        [TestCase(161)] // beyond the section end
        public void E57_CompressedVector_DataStartOutsideSection(long offset)
        {
            using var stream = CreateE57Stream(0);
            stream.Position = 64; // compressed vector data start
            stream.Write(BitConverter.GetBytes(offset));

            Assert.That(() => E57.ChunksFull(stream, stream.Length, ParseConfig.Default).ToArray(),
                Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void E57_CompressedVector_EmptySection()
        {
            using var stream = CreateE57Stream(0);
            var scan = E57FileHeader.Parse(stream, stream.Length, false).E57Root.Data3D.Single();
            scan.Points.RecordCount = 0;
            stream.Position = 56;
            stream.Write(BitConverter.GetBytes(32L)); // section contains only its header

            Assert.That(scan.Points.ReadDataFull(1, scan.Sem2Index, scan.IntensityLimits, false, false), Is.Empty);
        }

        [TestCase(1, 0)]
        [TestCase(1, 3)]
        [TestCase(1, 83)] // packet extends four bytes beyond the section
        [TestCase(0, 14)] // index packet is shorter than its header
        [TestCase(0, 83)]
        public void E57_CompressedVector_InvalidPacketLength(int packetType, int lengthMinus1)
        {
            using var stream = CreateE57Stream(0);
            stream.Position = 80;
            stream.WriteByte((byte)packetType);
            stream.Position = 82;
            stream.Write(BitConverter.GetBytes((ushort)lengthMinus1));

            Assert.That(() => E57.ChunksFull(stream, stream.Length, ParseConfig.Default).ToArray(),
                Throws.TypeOf<InvalidDataException>());
        }

        [TestCase(84, 65535)] // bytestream length table cannot fit in the packet
        [TestCase(86, 80)] // first bytestream extends beyond the packet
        public void E57_CompressedVector_ByteStreamsOutsidePacket(int offset, int value)
        {
            using var stream = CreateE57Stream(0);
            stream.Position = offset;
            stream.Write(BitConverter.GetBytes((ushort)value));

            Assert.That(() => E57.ChunksFull(stream, stream.Length, ParseConfig.Default).ToArray(),
                Throws.TypeOf<InvalidDataException>());
        }

        private static MemoryStream CreateE57Stream(int gap)
        {
            // Reuse the three-point fixture, inserting logical bytes before its
            // data packet and repaging it so gaps can cross checksum boundaries.
            var source = File.ReadAllBytes(Path.Combine(Config.TestDataDir, "test.e57"))
                .Chunk(1024).SelectMany(page => page.Take(1020)).ToArray();
            const int dataStart = 80;
            var logical = new byte[source.Length + gap];
            Array.Copy(source, 0, logical, 0, dataStart);
            Array.Copy(source, dataStart, logical, dataStart + gap, source.Length - dataStart);

            var physical = new byte[(logical.Length + 1019) / 1020 * 1024];
            var xmlOffset = (E57LogicalOffset)new E57PhysicalOffset(BitConverter.ToInt64(source, 24));
            BitConverter.GetBytes((long)physical.Length).CopyTo(logical, 16);
            BitConverter.GetBytes(((E57PhysicalOffset)new E57LogicalOffset(xmlOffset.Value + gap)).Value).CopyTo(logical, 24);
            BitConverter.GetBytes(BitConverter.ToInt64(source, 56) + gap).CopyTo(logical, 56);
            BitConverter.GetBytes(((E57PhysicalOffset)new E57LogicalOffset(dataStart + gap)).Value).CopyTo(logical, 64);

            // These tests parse without checksum verification; CRC slots stay zero.
            for (var offset = 0; offset < logical.Length; offset += 1020)
            {
                Array.Copy(logical, offset, physical, offset / 1020 * 1024, Math.Min(1020, logical.Length - offset));
            }
            return new MemoryStream(physical);
        }

        [Test]
        public void E57_Addresses_InvalidPhysicalOffsets()
        {
            new E57PhysicalOffset(1019);
            Assert.That(() => new E57PhysicalOffset(1020), Throws.Exception);
            Assert.That(() => new E57PhysicalOffset(1021), Throws.Exception);
            Assert.That(() => new E57PhysicalOffset(1022), Throws.Exception);
            Assert.That(() => new E57PhysicalOffset(1023), Throws.Exception);
            new E57PhysicalOffset(1024);

            new E57PhysicalOffset(2043);
            Assert.That(() => new E57PhysicalOffset(2044), Throws.Exception);
            Assert.That(() => new E57PhysicalOffset(2045), Throws.Exception);
            Assert.That(() => new E57PhysicalOffset(2046), Throws.Exception);
            Assert.That(() => new E57PhysicalOffset(2047), Throws.Exception);
            new E57PhysicalOffset(2048);
        }

        [Test]
        public void E57_Addresses_PhysicalPlusPhysical()
        {
            ClassicAssert.IsTrue((new E57PhysicalOffset(100) + new E57PhysicalOffset(234)).Value == 334);
            ClassicAssert.IsTrue((new E57PhysicalOffset(600) + new E57PhysicalOffset(1000)).Value == 1600);
        }
        [Test]
        public void E57_Addresses_PhysicalPlusLogical()
        {
            E57LogicalOffset x = new E57PhysicalOffset(1000) + new E57LogicalOffset(100);
            ClassicAssert.IsTrue(x.Value == 1100);
        }
        [Test]
        public void E57_Addresses_PhysicalToLogical_1()
        {
            ClassicAssert.IsTrue(((E57LogicalOffset)new E57PhysicalOffset(0)).Value == 0);
            ClassicAssert.IsTrue(((E57LogicalOffset)new E57PhysicalOffset(1019)).Value == 1019);
            ClassicAssert.IsTrue(((E57LogicalOffset)new E57PhysicalOffset(1024)).Value == 1020);
            ClassicAssert.IsTrue(((E57LogicalOffset)new E57PhysicalOffset(2048)).Value == 2040);
            ClassicAssert.IsTrue(((E57LogicalOffset)new E57PhysicalOffset(3072)).Value == 3060);

            ClassicAssert.IsTrue(((E57LogicalOffset)new E57PhysicalOffset(1500)).Value == 1496);
        }
        [Test]
        public void E57_Addresses_PhysicalToLogical_2()
        {
            ClassicAssert.IsTrue(((E57LogicalOffset)new E57PhysicalOffset(1019)).Value == 1019);
            ClassicAssert.IsTrue(((E57LogicalOffset)new E57PhysicalOffset(1024)).Value == 1020);
            ClassicAssert.IsTrue(((E57LogicalOffset)new E57PhysicalOffset(1025)).Value == 1021);
        }

        [Test]
        public void E57_Addresses_LogicalPlusLogical()
        {
            ClassicAssert.IsTrue((new E57LogicalOffset(100) + new E57LogicalOffset(234)).Value == 334);
            ClassicAssert.IsTrue((new E57LogicalOffset(600) + new E57LogicalOffset(1000)).Value == 1600);
        }
        public void E57_Addresses_LogicalToPhysical()
        {
            ClassicAssert.IsTrue(((E57PhysicalOffset)new E57LogicalOffset(0)).Value == 0);
            ClassicAssert.IsTrue(((E57PhysicalOffset)new E57LogicalOffset(1019)).Value == 1019);
            ClassicAssert.IsTrue(((E57PhysicalOffset)new E57LogicalOffset(1020)).Value == 1024);
            ClassicAssert.IsTrue(((E57PhysicalOffset)new E57LogicalOffset(2039)).Value == 2043);
            ClassicAssert.IsTrue(((E57PhysicalOffset)new E57LogicalOffset(2040)).Value == 2048);
            ClassicAssert.IsTrue(((E57PhysicalOffset)new E57LogicalOffset(3059)).Value == 3067);
            ClassicAssert.IsTrue(((E57PhysicalOffset)new E57LogicalOffset(3060)).Value == 3072);
        }
    }
}
