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
using Aardvark.Data.E57;
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
        [Test]
        public void StandardAcquisitionTimesAreParsedIndependently()
        {
            foreach (var mask in new[] { 1, 2, 3 })
            foreach (var start in new[] { 0.0, 1261872000.125 })
            foreach (var atomic in new[] { false, true })
            {
                var end = start + 7.75;
                var metadata = ((mask & 1) != 0 ? AcquisitionTime("acquisitionStart", start, atomic) : "")
                    + ((mask & 2) != 0 ? AcquisitionTime("acquisitionEnd", end, !atomic) : "");
                using var stream = CreateAcquisitionStream(metadata);
                var scan = E57FileHeader.Parse(stream, stream.Length, false).E57Root.Data3D.Single();
                var context = $"mask={mask}, start={start:R}, atomic={atomic}";
                CheckAcquisitionTime(scan.AcquisitonStart, (mask & 1) != 0 ? start : null, atomic, context + ", start");
                CheckAcquisitionTime(scan.AcquisitonEnd, (mask & 2) != 0 ? end : null, !atomic, context + ", end");
            }
        }

        [Test]
        public void AbsentAndLegacyAcquisitionMetadataKeepExistingBehavior()
        {
            foreach (var mask in new[] { 0, 1, 2, 3 })
            foreach (var atomic in new[] { false, true })
            {
                var metadata = ((mask & 1) != 0 ? AcquisitionTime("acquisitonStart", 0.5, atomic) : "")
                    + ((mask & 2) != 0 ? AcquisitionTime("acquisitonEnd", 8.75, !atomic) : "");
                using var stream = CreateAcquisitionStream(metadata);
                var scan = E57FileHeader.Parse(stream, stream.Length, false).E57Root.Data3D.Single();
                var context = $"legacy mask={mask}, atomic={atomic}";
                CheckAcquisitionTime(scan.AcquisitonStart, (mask & 1) != 0 ? 0.5 : null, atomic, context + ", start");
                CheckAcquisitionTime(scan.AcquisitonEnd, (mask & 2) != 0 ? 8.75 : null, !atomic, context + ", end");
            }
        }

        [Test]
        public void StandardAcquisitionElementsTakePrecedenceOverLegacyNames()
        {
            foreach (var mask in new[] { 1, 2, 3 })
            foreach (var standardFirst in new[] { false, true })
            {
                // A zero standard value is still present, and must not select the legacy value.
                var standard = ((mask & 1) != 0 ? AcquisitionTime("acquisitionStart", 0, true) : "")
                    + ((mask & 2) != 0 ? AcquisitionTime("acquisitionEnd", 1261872008.875, false) : "");
                var legacy = AcquisitionTime("acquisitonStart", 300.5, false) + AcquisitionTime("acquisitonEnd", 301.75, true);
                using var stream = CreateAcquisitionStream(standardFirst ? standard + legacy : legacy + standard);
                var scan = E57FileHeader.Parse(stream, stream.Length, false).E57Root.Data3D.Single();
                var context = $"standard mask={mask}, standardFirst={standardFirst}";
                CheckAcquisitionTime(scan.AcquisitonStart, (mask & 1) != 0 ? 0 : 300.5, (mask & 1) != 0, context + ", start");
                CheckAcquisitionTime(scan.AcquisitonEnd, (mask & 2) != 0 ? 1261872008.875 : 301.75, (mask & 2) == 0, context + ", end");
            }
            foreach (var suffix in new[] { "Start", "End" })
            {
                var legacy = AcquisitionTime("acquisiton" + suffix, 300.5, false);
                using var stream = CreateAcquisitionStream($"<acquisition{suffix} type=\"Structure\"/>" + legacy);
                Assert.That(() => E57FileHeader.Parse(stream, stream.Length, false), Throws.Exception,
                    $"A present but malformed acquisition{suffix} must not fall back to legacy metadata.");
                using var ignoredLegacy = CreateAcquisitionStream(AcquisitionTime("acquisition" + suffix, 0.5, true)
                    + $"<acquisiton{suffix} type=\"Structure\"/>");
                var scan = E57FileHeader.Parse(ignoredLegacy, ignoredLegacy.Length, false).E57Root.Data3D.Single();
                CheckAcquisitionTime(suffix == "Start" ? scan.AcquisitonStart : scan.AcquisitonEnd,
                    0.5, true, $"malformed legacy {suffix} ignored");
            }
        }

        [Test]
        public void ChunkTimestampsUseAcquisitionStartWithoutChangingPointData()
        {
            var gpsEpoch = new DateTimeOffset(1980, 1, 6, 0, 0, 0, TimeSpan.Zero);
            var start2020 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1250000);
            var standardStart = AcquisitionTime("acquisitionStart", 1261872000.125, true);
            var standardEnd = AcquisitionTime("acquisitionEnd", 1261872008.875, false);
            var legacyStart = AcquisitionTime("acquisitonStart", 0.5, false);
            using var referenceStream = CreateAcquisitionStream("");
            var reference = E57.ChunksFull(referenceStream, referenceStream.Length, ParseConfig.Default).Single();
            Assert.That(reference.Positions, Is.EqualTo(new[] {
                new Aardvark.Base.V3d(1, 2, 3), new Aardvark.Base.V3d(4, 5, 6), new Aardvark.Base.V3d(-1, -2, -3) }));
            Assert.That(reference.RawData[PointPropertySemantics.TimeStamp], Is.EqualTo(new[] { 0.0, 0.25, 2.5 }));

            var cases = new[]
            {
                ("standard start", standardStart, start2020),
                ("standard both", standardStart + standardEnd, start2020),
                ("standard precedence", legacyStart + standardStart + standardEnd, start2020),
                ("legacy start", legacyStart, gpsEpoch.AddTicks(5000000)),
                ("legacy start with standard end", legacyStart + standardEnd, gpsEpoch.AddTicks(5000000)),
                ("end only", standardEnd, gpsEpoch),
                ("absent", "", gpsEpoch)
            };
            foreach (var (context, metadata, start) in cases)
            {
                using var stream = CreateAcquisitionStream(metadata);
                var source = stream.ToArray();
                var chunk = E57.ChunksFull(stream, stream.Length, ParseConfig.Default).Single();
                var positions = chunk.Positions.ToArray();
                var raw = chunk.RawData.ToDictionary(kv => kv.Key, kv => (Array)kv.Value.Clone());
                var timestamps = chunk.Timestamps;
                Assert.That(timestamps, Is.EqualTo(new[] { start, start.AddTicks(2500000), start.AddTicks(25000000) }), context);
                Assert.That(chunk.Count, Is.EqualTo(3), context);
                Assert.That(chunk.Positions, Is.EqualTo(reference.Positions), context);
                Assert.That(chunk.RawData.Keys, Is.EquivalentTo(reference.RawData.Keys), context);
                foreach (var (semantic, values) in reference.RawData)
                    Assert.That(chunk.RawData[semantic], Is.EqualTo(values), $"{context}, {semantic}");
                // Reading or modifying the returned timestamps must not alter source data.
                timestamps[0] = DateTimeOffset.MinValue;
                Assert.That(chunk.Timestamps[0], Is.EqualTo(start), context);
                Assert.That(chunk.Positions, Is.EqualTo(positions), context);
                foreach (var (semantic, values) in raw)
                    Assert.That(chunk.RawData[semantic], Is.EqualTo(values), $"{context}, unchanged {semantic}");
                Assert.That(stream.ToArray(), Is.EqualTo(source), context);
            }
        }

        private static string AcquisitionTime(string name, double seconds, bool atomic) => FormattableString.Invariant(
            $"<{name} type=\"Structure\"><dateTimeValue type=\"Float\">{seconds:R}</dateTimeValue><isAtomicClockReferenced type=\"Integer\">{(atomic ? 1 : 0)}</isAtomicClockReferenced></{name}>");

        private static void CheckAcquisitionTime(E57DateTime actual, double? seconds, bool atomic, string context)
        {
            if (!seconds.HasValue) { Assert.That(actual, Is.Null, context); return; }
            Assert.That(actual, Is.Not.Null, context);
            Assert.That(actual.DateTimeValue, Is.EqualTo(seconds.Value), context);
            Assert.That(actual.IsAtomicClockReferenced, Is.EqualTo(atomic), context);
            var epoch = new DateTimeOffset(1980, 1, 6, 0, 0, 0, TimeSpan.Zero);
            Assert.That(actual.DateTime, Is.EqualTo(epoch.AddTicks((long)(seconds.Value * TimeSpan.TicksPerSecond))), context);
        }

        private static MemoryStream CreateAcquisitionStream(string metadata)
        {
            // Three records with double XYZ/time offsets, float intensity and byte RGB.
            // All binary offsets are before the first checksum boundary.
            const int packetLength = 140;
            const int xmlOffset = 80 + packetLength;
            var xml = System.Text.Encoding.UTF8.GetBytes($@"<e57Root type=""Structure"" xmlns=""http://www.astm.org/COMMIT/E57/2010-e57-v1.0"">
                <formatName type=""String"">ASTM E57 3D Imaging Data File</formatName>
                <guid type=""String"">00000000-0000-0000-0000-000000000001</guid>
                <versionMajor type=""Integer"">1</versionMajor><versionMinor type=""Integer"">0</versionMinor>
                <data3D type=""Vector"" allowHeterogeneousChildren=""1""><vectorChild type=""Structure"">
                    <guid type=""String"">00000000-0000-0000-0000-000000000002</guid>{metadata}
                    <cartesianBounds type=""Structure""><xMinimum type=""Float"">-1</xMinimum><xMaximum type=""Float"">4</xMaximum>
                        <yMinimum type=""Float"">-2</yMinimum><yMaximum type=""Float"">5</yMaximum>
                        <zMinimum type=""Float"">-3</zMinimum><zMaximum type=""Float"">6</zMaximum></cartesianBounds>
                    <intensityLimits type=""Structure""><intensityMinimum type=""Float"">10</intensityMinimum><intensityMaximum type=""Float"">30</intensityMaximum></intensityLimits>
                    <points type=""CompressedVector"" fileOffset=""48"" recordCount=""3""><prototype type=""Structure"">
                        <cartesianX type=""Float"" precision=""double""/><cartesianY type=""Float"" precision=""double""/>
                        <cartesianZ type=""Float"" precision=""double""/><timeStamp type=""Float"" precision=""double""/>
                        <intensity type=""Float"" precision=""single""/>
                        <colorRed type=""Integer"" minimum=""0"" maximum=""255""/><colorGreen type=""Integer"" minimum=""0"" maximum=""255""/>
                        <colorBlue type=""Integer"" minimum=""0"" maximum=""255""/>
                    </prototype><codecs type=""Vector"" allowHeterogeneousChildren=""1""/></points>
                </vectorChild></data3D><images2D type=""Vector"" allowHeterogeneousChildren=""1""/></e57Root>");
            using var logical = new MemoryStream();
            using (var writer = new BinaryWriter(logical, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("ASTM-E57"));
                writer.Write(1U); writer.Write(0U);
                writer.Write((long)((xmlOffset + xml.Length + 1019) / 1020 * 1024));
                writer.Write((long)xmlOffset); writer.Write((long)xml.Length); writer.Write(1024L);
                writer.Write((byte)1); writer.Write(new byte[7]);
                writer.Write(32L + packetLength); writer.Write(80L); writer.Write(0L);
                writer.Write((byte)1); writer.Write((byte)0); writer.Write((ushort)(packetLength - 1)); writer.Write((ushort)8);
                foreach (var length in new ushort[] { 24, 24, 24, 24, 12, 3, 3, 3 }) writer.Write(length);
                foreach (var value in new[] { 1.0, 4, -1, 2, 5, -2, 3, 6, -3, 0, 0.25, 2.5 }) writer.Write(value);
                foreach (var value in new[] { 10f, 20f, 30f }) writer.Write(value);
                writer.Write(new byte[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 });
                writer.Write((byte)0); // packet alignment
                Assert.That(logical.Position, Is.EqualTo(xmlOffset));
                writer.Write(xml);
            }
            var bytes = logical.ToArray();
            var physical = new byte[(bytes.Length + 1019) / 1020 * 1024];
            // Match the other in-memory fixtures: checksum verification is off, CRC slots stay zero.
            for (var offset = 0; offset < bytes.Length; offset += 1020)
                Array.Copy(bytes, offset, physical, offset / 1020 * 1024, Math.Min(1020, bytes.Length - offset));
            return new MemoryStream(physical);
        }

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
