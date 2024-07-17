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
