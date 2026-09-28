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
using System;
using System.Collections.Generic;
using Aardvark.Base;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class BitPackTests
    {
        private static readonly Random random = new();

        #region BitPacker

        [Test]
        public void BitPacker_1()
        {
            var buffer = new BitPacker(1);
            var xs = buffer.UnpackUInts(new byte[] { 0b10110011 });
            ClassicAssert.IsTrue(xs.Length == 8);
            ClassicAssert.IsTrue(xs[0] == 1);
            ClassicAssert.IsTrue(xs[1] == 1);
            ClassicAssert.IsTrue(xs[2] == 0);
            ClassicAssert.IsTrue(xs[3] == 0);
            ClassicAssert.IsTrue(xs[4] == 1);
            ClassicAssert.IsTrue(xs[5] == 1);
            ClassicAssert.IsTrue(xs[6] == 0);
            ClassicAssert.IsTrue(xs[7] == 1);
        }

        [Test]
        public void BitPacker_2()
        {
            var buffer = new BitPacker(2);
            var xs = buffer.UnpackUInts(new byte[] { 0b10110011 });
            ClassicAssert.IsTrue(xs.Length == 4);
            ClassicAssert.IsTrue(xs[0] == 0b11);
            ClassicAssert.IsTrue(xs[1] == 0b00);
            ClassicAssert.IsTrue(xs[2] == 0b11);
            ClassicAssert.IsTrue(xs[3] == 0b10);
        }

        [Test]
        public void BitPacker_3()
        {
            var buffer = new BitPacker(3);
            var xs = buffer.UnpackUInts(new byte[] { 0b10110011 });
            ClassicAssert.IsTrue(xs.Length == 2);
            ClassicAssert.IsTrue(xs[0] == 0b011);
            ClassicAssert.IsTrue(xs[1] == 0b110);
            xs = buffer.UnpackUInts(new byte[] { 0b10110011 }); ;
            ClassicAssert.IsTrue(xs.Length == 3);
            ClassicAssert.IsTrue(xs[0] == 0b110);
            ClassicAssert.IsTrue(xs[1] == 0b001);
            ClassicAssert.IsTrue(xs[2] == 0b011);
        }

        [Test]
        public void BitPacker_4()
        {
            var buffer = new BitPacker(4);
            var xs = buffer.UnpackUInts(new byte[] { 0b10110011 });
            ClassicAssert.IsTrue(xs.Length == 2);
            ClassicAssert.IsTrue(xs[0] == 0b0011);
            ClassicAssert.IsTrue(xs[1] == 0b1011);
            xs = buffer.UnpackUInts(new byte[] { 0b01100111 }); ;
            ClassicAssert.IsTrue(xs.Length == 2);
            ClassicAssert.IsTrue(xs[0] == 0b0111);
            ClassicAssert.IsTrue(xs[1] == 0b0110);
        }

        [Test]
        public void BitPacker_8()
        {
            var buffer = new BitPacker(8);
            var xs = buffer.UnpackUInts(new byte[] { 0b10110011, 0b01100111, 0b11001110 });
            ClassicAssert.IsTrue(xs.Length == 3);
            ClassicAssert.IsTrue(xs[0] == 0b10110011);
            ClassicAssert.IsTrue(xs[1] == 0b01100111);
            ClassicAssert.IsTrue(xs[2] == 0b11001110);
            xs = buffer.UnpackUInts(new byte[] { 0b10101010 }); ;
            ClassicAssert.IsTrue(xs.Length == 1);
            ClassicAssert.IsTrue(xs[0] == 0b10101010);
        }

        [Test]
        public void BitPacker_10()
        {
            var buffer = new BitPacker(10);
            var xs = buffer.UnpackUInts(new byte[] { 0b10110011, 0b01100111, 0b11001110 });
            ClassicAssert.IsTrue(xs.Length == 2);
            ClassicAssert.IsTrue(xs[0] == 0b1110110011);
            ClassicAssert.IsTrue(xs[1] == 0b1110011001);
            xs = buffer.UnpackUInts(new byte[] { 0b10101010 }); ;
            ClassicAssert.IsTrue(xs.Length == 1);
            ClassicAssert.IsTrue(xs[0] == 0b1010101100);
        }

        [Test]
        public void BitPacker_16()
        {
            var buffer = new BitPacker(16);
            var xs = buffer.UnpackUInts(new byte[] { 0b10110011, 0b01100111, 0b11001110 });
            ClassicAssert.IsTrue(xs.Length == 1);
            ClassicAssert.IsTrue(xs[0] == 0b0110011110110011);
            xs = buffer.UnpackUInts(new byte[] { 0b10101010 }); ;
            ClassicAssert.IsTrue(xs.Length == 1);
            ClassicAssert.IsTrue(xs[0] == 0b1010101011001110);
        }

        #endregion

        #region BitBuffer

        [Test]
        public void BitBuffer_1()
        {
            var buffer = new BitPack.BitBuffer(1);
            buffer.PushBits(0b1, 1);
            ClassicAssert.IsTrue(buffer.Buffer[0] == 0b00000001);
        }
        [Test]
        public void BitBuffer_2()
        {
            var buffer = new BitPack.BitBuffer(2);
            buffer.PushBits(0b1, 1);
            buffer.PushBits(0b1, 1);
            ClassicAssert.IsTrue(buffer.Buffer[0] == 0b00000011);
        }
        [Test]
        public void BitBuffer_3()
        {
            var buffer = new BitPack.BitBuffer(8);
            buffer.PushBits(0b1, 1);
            buffer.PushBits(0b10101, 5);
            buffer.PushBits(0b10, 2);
            ClassicAssert.IsTrue(buffer.Buffer[0] == 0b10101011);
        }
        [Test]
        public void BitBuffer_4()
        {
            var buffer = new BitPack.BitBuffer(8);
            buffer.PushBits(0b10101010, 8);
            ClassicAssert.IsTrue(buffer.Buffer[0] == 0b10101010);
        }
        [Test]
        public void BitBuffer_5()
        {
            var buffer = new BitPack.BitBuffer(10);
            buffer.PushBits(0b10101, 5);
            buffer.PushBits(0b10101, 5);
            ClassicAssert.IsTrue(buffer.Buffer[0] == 0b10110101);
            ClassicAssert.IsTrue(buffer.Buffer[1] == 0b00000010);
        }
        [Test]
        public void BitBuffer_6()
        {
            var buffer = new BitPack.BitBuffer(20);
            buffer.PushBits(0b10101010, 8);
            buffer.PushBits(0b11001100, 8);
            buffer.PushBits(0b1110, 4);
            ClassicAssert.IsTrue(buffer.Buffer[0] == 0b10101010);
            ClassicAssert.IsTrue(buffer.Buffer[1] == 0b11001100);
            ClassicAssert.IsTrue(buffer.Buffer[2] == 0b00001110);
        }
        [Test]
        public void BitBuffer_7()
        {
            var buffer = new BitPack.BitBuffer(20);
            buffer.PushBits(0b1110_11001100_10101010, 20);
            ClassicAssert.IsTrue(buffer.Buffer[0] == 0b10101010);
            ClassicAssert.IsTrue(buffer.Buffer[1] == 0b11001100);
            ClassicAssert.IsTrue(buffer.Buffer[2] == 0b00001110);
        }
        [Test]
        public void BitBuffer_8()
        {
            var buffer = new BitPack.BitBuffer(64);
            buffer.PushBits(ulong.MaxValue, 64);
            for (var i = 0; i < 8; i++) ClassicAssert.IsTrue(buffer.Buffer[i] == 0b11111111);
        }
        [Test]
        public void BitBuffer_9()
        {
            var buffer = new BitPack.BitBuffer(64);
            buffer.PushBits(ulong.MaxValue, 21);
            ClassicAssert.IsTrue(buffer.Buffer[0] == 0b11111111);
            ClassicAssert.IsTrue(buffer.Buffer[1] == 0b11111111);
            ClassicAssert.IsTrue(buffer.Buffer[2] == 0b00011111);
        }
        [Test]
        public void BitBuffer_10()
        {
            var buffer = new BitPack.BitBuffer(40);
            buffer.PushBits(0b11010101_11110000_11101110_11001100_10101010UL, 40);
            ClassicAssert.IsTrue(buffer.Buffer[0] == 0b10101010);
            ClassicAssert.IsTrue(buffer.Buffer[1] == 0b11001100);
            ClassicAssert.IsTrue(buffer.Buffer[2] == 0b11101110);
            ClassicAssert.IsTrue(buffer.Buffer[3] == 0b11110000);
            ClassicAssert.IsTrue(buffer.Buffer[4] == 0b11010101);
        }

        [Test]
        public void BitBuffer_GetByte_1()
        {
            var buffer = new BitPack.BitBuffer(40);
            buffer.PushBits(0b11010101_11110000_11101110_11001100_10101010UL, 40);
            ClassicAssert.IsTrue(buffer.GetByte(00, 5) == 0b01010);
            ClassicAssert.IsTrue(buffer.GetByte(05, 5) == 0b00101);
            ClassicAssert.IsTrue(buffer.GetByte(10, 5) == 0b10011);
            ClassicAssert.IsTrue(buffer.GetByte(15, 5) == 0b11101);
            ClassicAssert.IsTrue(buffer.GetByte(20, 5) == 0b01110);
            ClassicAssert.IsTrue(buffer.GetByte(25, 5) == 0b11000);
            ClassicAssert.IsTrue(buffer.GetByte(30, 5) == 0b10111);
            ClassicAssert.IsTrue(buffer.GetByte(35, 5) == 0b11010);
        }

        [Test]
        public void BitBuffer_GetUInt_1()
        {
            var buffer = new BitPack.BitBuffer(40);
            buffer.PushBits(0b11010101_11110000_11101110_11001100_10101010UL, 40);
            ClassicAssert.IsTrue(buffer.GetUInt(00, 5) == 0b01010);
            ClassicAssert.IsTrue(buffer.GetUInt(05, 5) == 0b00101);
            ClassicAssert.IsTrue(buffer.GetUInt(10, 5) == 0b10011);
            ClassicAssert.IsTrue(buffer.GetUInt(15, 5) == 0b11101);
            ClassicAssert.IsTrue(buffer.GetUInt(20, 5) == 0b01110);
            ClassicAssert.IsTrue(buffer.GetUInt(25, 5) == 0b11000);
            ClassicAssert.IsTrue(buffer.GetUInt(30, 5) == 0b10111);
            ClassicAssert.IsTrue(buffer.GetUInt(35, 5) == 0b11010);
        }

        [Test]
        public void BitBuffer_GetULong_1()
        {
            var buffer = new BitPack.BitBuffer(40);
            buffer.PushBits(0b11010101_11110000_11101110_11001100_10101010UL, 40);
            ClassicAssert.IsTrue(buffer.GetULong(00, 5) == 0b01010);
            ClassicAssert.IsTrue(buffer.GetULong(05, 5) == 0b00101);
            ClassicAssert.IsTrue(buffer.GetULong(10, 5) == 0b10011);
            ClassicAssert.IsTrue(buffer.GetULong(15, 5) == 0b11101);
            ClassicAssert.IsTrue(buffer.GetULong(20, 5) == 0b01110);
            ClassicAssert.IsTrue(buffer.GetULong(25, 5) == 0b11000);
            ClassicAssert.IsTrue(buffer.GetULong(30, 5) == 0b10111);
            ClassicAssert.IsTrue(buffer.GetULong(35, 5) == 0b11010);
        }

        #endregion

        #region BitCountInBytes

        [Test]
        public void BitCountInBytes_1() => ClassicAssert.IsTrue(BitPack.BitCountInBytes(0) == 0);
        [Test]
        public void BitCountInBytes_2() => ClassicAssert.IsTrue(BitPack.BitCountInBytes(1) == 1);
        [Test]
        public void BitCountInBytes_3() => ClassicAssert.IsTrue(BitPack.BitCountInBytes(8) == 1);
        [Test]
        public void BitCountInBytes_4() => ClassicAssert.IsTrue(BitPack.BitCountInBytes(9) == 2);
        [Test]
        public void BitCountInBytes_5() => ClassicAssert.IsTrue(BitPack.BitCountInBytes(17) == 3);

        #endregion

        #region GetBits

        [Test]
        public void GetBits_1()
        {
            ulong x = 0b10101010_11001100_01100110_10011101;
            ClassicAssert.IsTrue(BitPack.GetBits(x, 0, 1) == 0b1);
        }
        [Test]
        public void GetBits_2()
        {
            ulong x = 0b10101010_11001100_01100110_10011101;
            ClassicAssert.IsTrue(BitPack.GetBits(x, 1, 1) == 0b0);
        }
        [Test]
        public void GetBits_3()
        {
            ulong x = 0b10101010_11001100_01100110_10011101;
            ClassicAssert.IsTrue(BitPack.GetBits(x, 2, 4) == 0b0111);
        }
        [Test]
        public void GetBits_4()
        {
            ulong x = 0b10101010_11001100_01100110_10011101;
            ClassicAssert.IsTrue(BitPack.GetBits(x, 3, 8) == 0b11010011);
        }
        [Test]
        public void GetBits_5()
        {
            ulong x = 0b10101010_11001100_01100110_10011101;
            ClassicAssert.IsTrue(BitPack.GetBits(x, 17, 6) == 0b100110);
        }

        #endregion

        #region Independent decoding regressions

        // Set individual wire bits without using Pack, PushBits, or any production decoder.
        // Ones outside the requested values detect leaking prefix/padding bits.
        private static byte[] EncodeIndependently(ulong[] values, int bits, int start = 0)
        {
            var result = new byte[(start + values.Length * bits + 7) / 8];
            Array.Fill(result, (byte)255);
            for (var i = 0; i < values.Length; i++)
                for (var bit = 0; bit < bits; bit++)
                {
                    var position = start + i * bits + bit;
                    var mask = (byte)(1 << (position % 8));
                    if (((values[i] >> bit) & 1UL) == 0) result[position / 8] &= (byte)~mask;
                    else result[position / 8] |= mask;
                }
            return result;
        }

        private static ulong[] Patterns(int bits)
        {
            var mask = bits == 64 ? ulong.MaxValue : (1UL << bits) - 1;
            var values = new List<ulong> { 0, mask, 0x0123456789abcdefUL & mask, 0xfedcba9876543210UL & mask };
            for (var bit = 0; bit < bits; bit++) values.Add(1UL << bit);
            var state = 0x9e3779b97f4a7c15UL ^ (ulong)bits;
            for (var i = 0; i < 8; i++)
            {
                state ^= state << 13; state ^= state >> 7; state ^= state << 17;
                values.Add(state & mask);
            }
            return values.ToArray();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Unpack24PreservesEveryByte(bool dispatch)
        {
            var bytes = new byte[] { 0, 0, 1, 0x56, 0x34, 0x12, 0xff, 0xff, 0xff, 0, 0xff, 1 };
            var original = (byte[])bytes.Clone();
            var actual = dispatch ? BitPack.UnpackIntegers(bytes, 24) : BitPack.OptimizedUnpackInt24(bytes);
            Assert.That(actual, Is.TypeOf<int[]>().And.EqualTo(new[] { 0x010000, 0x123456, 0xffffff, 0x01ff00 }));
            Assert.That(bytes, Is.EqualTo(original));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Unpack24MatchesIndependentPatterns(bool dispatch)
        {
            var values = Patterns(24);
            var bytes = EncodeIndependently(values, 24);
            var expected = Array.ConvertAll(values, x => (int)x);
            var actual = dispatch ? BitPack.UnpackIntegers(bytes, 24) : BitPack.OptimizedUnpackInt24(bytes);
            Assert.That(actual, Is.TypeOf<int[]>().And.EqualTo(expected));
        }

        [Test]
        public void GetULongPreservesDistinctWordHalves()
        {
            var bytes = new byte[] { 0x10, 0x32, 0x54, 0x76, 0x98, 0xba, 0xdc, 0xfe };
            var buffer = new BitPack.BitBuffer(bytes, 64);
            Assert.That(buffer.GetULong(0, 64), Is.EqualTo(0xfedcba9876543210UL));
            Assert.That(buffer.GetULong(0, 32), Is.EqualTo(0x76543210UL));
            Assert.That(buffer.GetULong(32, 32), Is.EqualTo(0xfedcba98UL));
        }

        [Test]
        public void GetULongPreservesAllWidthsAndOffsets([Range(1, 64)] int bits, [Range(0, 7)] int offset)
        {
            var expected = Patterns(bits);
            var start = 8 + offset;
            var bytes = EncodeIndependently(expected, bits, start);
            var original = (byte[])bytes.Clone();
            var buffer = new BitPack.BitBuffer(bytes, 1);
            // Each pattern is tested at every within-byte offset across these eight cases.
            for (var i = expected.Length - 1; i >= 0; i--)
            {
                var bit = start + i * bits;
                Assert.That(buffer.GetULong(bit, bits), Is.EqualTo(expected[i]), $"value {i}");
                if (bits <= 32) Assert.That(buffer.GetUInt(bit, bits), Is.EqualTo((uint)expected[i]));
            }
            Assert.That(bytes, Is.EqualTo(original));
        }

        [Test]
        public void UnpackCallbacksPreserveValuesIndicesAndRequestedCount([Range(1, 64)] int bits)
        {
            var expected = Patterns(bits);
            var bytes = EncodeIndependently(expected, bits);
            var original = (byte[])bytes.Clone();
            foreach (var count in new[] { 0, 1, expected.Length - 1, expected.Length })
            {
                var seen = 0;
                BitPack.Unpack(bytes, bits, count, (value, index) =>
                {
                    Assert.That(index, Is.EqualTo(seen));
                    Assert.That(value, Is.EqualTo(expected[index]));
                    seen++;
                });
                Assert.That(seen, Is.EqualTo(count));
            }
            Assert.That(bytes, Is.EqualTo(original));
        }

        [Test]
        public void DispatcherKeepsNarrowReturnTypes([Range(1, 32)] int bits)
        {
            var mask = bits == 32 ? uint.MaxValue : (1UL << bits) - 1;
            var expected = new[] { 0UL, 1UL, mask, mask / 2, 0xabcdefUL & mask, 0x76543210UL & mask, 0UL, mask };
            var actual = BitPack.UnpackIntegers(EncodeIndependently(expected, bits), bits);
            var type = bits switch
            {
                2 or 4 or 8 => typeof(byte[]),
                12 or 16 => typeof(short[]),
                20 or 24 or 32 => typeof(int[]),
                _ => typeof(uint[])
            };
            Assert.That(actual, Is.TypeOf(type));
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (var i = 0; i < actual.Length; i++)
            {
                // Signed dispatcher storage retains the complete underlying unsigned bits.
                var value = actual.GetValue(i) switch
                {
                    byte x => (ulong)x,
                    short x => unchecked((ushort)x),
                    int x => unchecked((uint)x),
                    uint x => x,
                    _ => throw new InvalidOperationException()
                };
                Assert.That(value, Is.EqualTo(expected[i]));
            }
        }

        [Test]
        public void DispatcherKeepsSigned64BitStorage()
        {
            var expected = Patterns(64);
            var actual = BitPack.UnpackIntegers(EncodeIndependently(expected, 64), 64);
            Assert.That(actual, Is.TypeOf<long[]>().And.EqualTo(Array.ConvertAll(expected, x => unchecked((long)x))));
        }

        [Test]
        public void DispatcherStillRejectsUnsupportedWideWidths([Range(33, 63)] int bits)
            => Assert.Throws<Exception>(() => BitPack.UnpackIntegers(new byte[16], bits));

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(65)]
        public void InvalidReadWidthsKeepTheirExceptions(int bits)
        {
            var buffer = new BitPack.BitBuffer(64);
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetUInt(0, bits)).ParamName, Is.EqualTo("bitCount"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => buffer.GetULong(0, bits)).ParamName, Is.EqualTo("bitCount"));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => BitPack.UnpackIntegers(Array.Empty<byte>(), bits)).ParamName, Is.EqualTo("bits"));
            Assert.That(Assert.Throws<ArgumentException>(() => BitPack.Unpack(Array.Empty<byte>(), bits, 0, (_, _) => { })).ParamName, Is.EqualTo("bits"));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(5)]
        public void Unpack24RejectsIncompleteTriples(int length)
        {
            Assert.Throws<ArgumentException>(() => BitPack.OptimizedUnpackInt24(new byte[length]));
            Assert.Throws<ArgumentException>(() => BitPack.UnpackIntegers(new byte[length], 24));
        }

        [Test]
        public void GetULongChecksLogicalLength([Values(1, 32, 33, 64)] int bits, [Values(0, 7)] int offset)
        {
            var buffer = new BitPack.BitBuffer(offset + bits - 1);
            Assert.Throws<InvalidOperationException>(() => buffer.GetULong(offset, bits));
            if (bits <= 32) Assert.Throws<InvalidOperationException>(() => buffer.GetUInt(offset, bits));
        }

        [Test]
        public void UnpackTruncationStopsBeforeAnIncompleteValue([Values(24, 32, 33, 63, 64)] int bits)
        {
            var values = new[] { 0UL, 1UL, 0UL, 1UL };
            var bytes = EncodeIndependently(values, bits);
            Array.Resize(ref bytes, bytes.Length - 1);
            var seen = 0;
            Assert.Throws<InvalidOperationException>(() => BitPack.Unpack(bytes, bits, values.Length, (value, index) =>
            {
                Assert.That(index, Is.EqualTo(seen));
                Assert.That(value, Is.EqualTo(values[index]));
                seen++;
            }));
            Assert.That(seen, Is.EqualTo(bytes.Length * 8 / bits));
        }

        [Test]
        public void EmptyAndZeroCountReadsKeepTheirBehavior([Values(1, 24, 32, 33, 64)] int bits)
        {
            var bytes = Array.Empty<byte>();
            var buffer = new BitPack.BitBuffer(bytes, bits);
            Assert.That(buffer.LengthInBits, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => buffer.GetULong(0, bits));
            BitPack.Unpack(bytes, bits, 0, (_, _) => Assert.Fail("Unexpected callback"));
            BitPack.Unpack(bytes, bits, -1, (_, _) => Assert.Fail("Unexpected callback"));
            if (bits <= 32 || bits == 64) Assert.That(BitPack.UnpackIntegers(bytes, bits).Length, Is.Zero);
            Assert.That(BitPack.OptimizedUnpackInt24(bytes), Is.Empty);
            Assert.Throws<ArgumentNullException>(() => BitPack.Unpack(null, bits, 0, (_, _) => { }));
        }

        [TestCase(32)]
        [TestCase(33)]
        [TestCase(64)]
        public void WarmedGetULongDoesNotAllocate(int bits)
        {
            var expected = bits == 64 ? ulong.MaxValue : (1UL << bits) - 1;
            var buffer = new BitPack.BitBuffer(EncodeIndependently(new[] { expected }, bits), 1);
            var checksum = 0UL;
            for (var i = 0; i < 4096; i++) checksum ^= buffer.GetULong(0, bits);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 4097; i++) checksum ^= buffer.GetULong(0, bits);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(checksum, Is.EqualTo(expected));
        }

        #endregion

        #region PackUnpack
        
        private void PackUnpack(int BITS)
        {
            int N = random.Next(50, 100);
            int MAX = 1 << ((BITS > 30) ? 30 : BITS);
            var data = new uint[N].SetByIndex(i => (uint)random.Next(MAX));
            var buffer = BitPack.Pack(data, BITS);
            ClassicAssert.IsTrue(buffer.Length == BitPack.BitCountInBytes(N * BITS));

            var unpacked = new uint[N];
            BitPack.Unpack(buffer, BITS, N, (x, i) => unpacked[i] = (uint)x);
            for (var i = 0; i < data.Length; i++) ClassicAssert.IsTrue(data[i] == unpacked[i]);
        }

        [Test]
        public void PackUnpack_Special_1()
        {
            int BITS = 9;
            const int N = 1;
            var data = new uint[N] { 212 };
            var buffer = BitPack.Pack(data, BITS);
            ClassicAssert.IsTrue(buffer.Length == BitPack.BitCountInBytes(N * BITS));

            var unpacked = new int[N];
            BitPack.Unpack(buffer, BITS, N, (x, i) => unpacked[i] = (int)x);
            for (var i = 0; i < data.Length; i++) ClassicAssert.IsTrue(data[i] == unpacked[i]);
        }
        [Test]
        public void PackUnpack_Special_2()
        {
            int BITS = 9;
            const int N = 1;
            var data = new uint[N] { 270 };
            var buffer = BitPack.Pack(data, BITS);
            ClassicAssert.IsTrue(buffer.Length == BitPack.BitCountInBytes(N * BITS));

            var unpacked = new int[N];
            BitPack.Unpack(buffer, BITS, N, (x, i) => unpacked[i] = (int)x);
            for (var i = 0; i < data.Length; i++) ClassicAssert.IsTrue(data[i] == unpacked[i]);
        }
        [Test]
        public void PackUnpack_Special_3()
        {
            int BITS = 31;
            const int N = 1;
            var data = new uint[N] { 270 };
            var buffer = BitPack.Pack(data, BITS);
            ClassicAssert.IsTrue(buffer.Length == BitPack.BitCountInBytes(N * BITS));

            var unpacked = new int[N];
            BitPack.Unpack(buffer, BITS, N, (x, i) => unpacked[i] = (int)x);
            for (var i = 0; i < data.Length; i++) ClassicAssert.IsTrue(data[i] == unpacked[i]);
        }

        [Test] public void PackUnpack01() => PackUnpack(1);
        [Test] public void PackUnpack02() => PackUnpack(2);
        [Test] public void PackUnpack03() => PackUnpack(3);
        [Test] public void PackUnpack04() => PackUnpack(4);
        [Test] public void PackUnpack05() => PackUnpack(5);
        [Test] public void PackUnpack06() => PackUnpack(6);
        [Test] public void PackUnpack07() => PackUnpack(7);
        [Test] public void PackUnpack08() => PackUnpack(8);
        [Test] public void PackUnpack09() => PackUnpack(9);
        [Test] public void PackUnpack10() => PackUnpack(10);
        [Test] public void PackUnpack11() => PackUnpack(11);
        [Test] public void PackUnpack12() => PackUnpack(12);
        [Test] public void PackUnpack13() => PackUnpack(13);
        [Test] public void PackUnpack14() => PackUnpack(14);
        [Test] public void PackUnpack15() => PackUnpack(15);
        [Test] public void PackUnpack16() => PackUnpack(16);
        [Test] public void PackUnpack17() => PackUnpack(17);
        [Test] public void PackUnpack18() => PackUnpack(18);
        [Test] public void PackUnpack19() => PackUnpack(19);
        [Test] public void PackUnpack20() => PackUnpack(20);
        [Test] public void PackUnpack21() => PackUnpack(21);
        [Test] public void PackUnpack22() => PackUnpack(22);
        [Test] public void PackUnpack23() => PackUnpack(23);
        [Test] public void PackUnpack24() => PackUnpack(24);
        [Test] public void PackUnpack25() => PackUnpack(25);
        [Test] public void PackUnpack26() => PackUnpack(26);
        [Test] public void PackUnpack27() => PackUnpack(27);
        [Test] public void PackUnpack28() => PackUnpack(28);
        [Test] public void PackUnpack29() => PackUnpack(29);
        [Test] public void PackUnpack30() => PackUnpack(30);
        [Test] public void PackUnpack31() => PackUnpack(31);
        [Test] public void PackUnpack32() => PackUnpack(32);
        
        #endregion
    }
}
