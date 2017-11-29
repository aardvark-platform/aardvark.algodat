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
using System;
using System.Runtime.CompilerServices;

namespace Aardvark.Base
{
    public static class BitPack
    {
        public static Array UnpackIntegers(byte[] buffer, int bits)
        {
            switch (bits)
            {
                case 2: return OptimizedUnpackInt2(buffer);
                case 4: return OptimizedUnpackInt4(buffer);
                case 8: return OptimizedUnpackInt8(buffer);
                case 12: return OptimizedUnpackInt12(buffer);
                case 16: return OptimizedUnpackInt16(buffer);
                case 20: return OptimizedUnpackInt20(buffer);
                case 24: return OptimizedUnpackInt24(buffer);
                case 32: return OptimizedUnpackInt32(buffer);
                case 64: return OptimizedUnpackInt64(buffer);
            }

            if (bits <= 32)
            {
                var bb = new BitBuffer(buffer, bits);
                var count = (buffer.Length * 8) / bits;
                var data = bb.ReadUInts(bits, count);
                return data;
            }

            if (bits <= 64)
            {
                throw new NotImplementedException($"BitPack.UnpackIntegers({bits})");
            }

            throw new NotImplementedException($"BitPack.UnpackIntegers({bits})");
        }
        public static byte[] OptimizedUnpackInt2(byte[] buffer)
        {
            checked
            {
                var xs = new byte[buffer.Length * 4];
                for (int i = 0, j = 0; i < xs.Length;)
                {
                    byte x = buffer[j++];
                    xs[i++] = (byte)((x >> 0) & 0b11);
                    xs[i++] = (byte)((x >> 2) & 0b11);
                    xs[i++] = (byte)((x >> 4) & 0b11);
                    xs[i++] = (byte)((x >> 6) & 0b11);
                }
                return xs;
            }
        }
        public static byte[] OptimizedUnpackInt4(byte[] buffer)
        {
            checked
            {
                var xs = new byte[buffer.Length * 2];
                for (int i = 0, j = 0; i < xs.Length;)
                {
                    byte x = buffer[j++];
                    xs[i++] = (byte)((x >> 0) & 0b1111);
                    xs[i++] = (byte)((x >> 4) & 0b1111);
                }
                return xs;
            }
        }
        public static byte[] OptimizedUnpackInt8(byte[] buffer) => buffer;
        public static short[] OptimizedUnpackInt12(byte[] buffer)
        {
            checked
            {
                var xs = new short[buffer.Length / 3 * 2];
                for (int i = 0, j = 0; i < xs.Length;)
                {
                    var x0 = buffer[j++]; var x1 = buffer[j++]; var x2 = buffer[j++];
                    xs[i++] = (short)(x0 + ((x1 & 0b00001111) << 8));
                    xs[i++] = (short)((x1 >> 4) + (x2 << 4));
                }
                return xs;
            }
        }
        public static short[] OptimizedUnpackInt16(byte[] buffer)
        {
            if (buffer.Length % 2 != 0) throw new ArgumentException($"Expected buffer length multiple of 2 bytes, but is {buffer.Length} bytes.");
            var xs = new short[buffer.Length / 2];
            for (int i = 0, j = 0; i < xs.Length; j += 2) xs[i++] = BitConverter.ToInt16(buffer, j);
            return xs;
        }
        public static int[] OptimizedUnpackInt20(byte[] buffer)
        {
            checked
            {
                if ((buffer.Length * 8) % 20 != 0) throw new ArgumentException($"Expected buffer length multiple of 20 bits, but is {buffer.Length} bytes.");
                var xs = new int[buffer.Length / 5 * 2];
                for (int i = 0, j = 0; i < xs.Length;)
                {
                    var x0 = buffer[j++]; var x1 = buffer[j++]; var x2 = buffer[j++]; var x3 = buffer[j++]; var x4 = buffer[j++];
                    xs[i++] = x0 + (x1 << 8) + ((x2 & 0b1111) << 16);
                    xs[i++] = (x2 >> 4) + (x3 << 4) + (x4 << 12);
                }
                return xs;
            }
        }
        public static int[] OptimizedUnpackInt24(byte[] buffer)
        {
            checked
            {
                if (buffer.Length % 3 != 0) throw new ArgumentException($"Expected buffer length multiple of 24 bits, but is {buffer.Length} bytes.");
                var xs = new int[buffer.Length / 3];
                for (int i = 0, j = 0; i < xs.Length;)
                {
                    var x0 = buffer[j++]; var x1 = buffer[j++]; var x2 = buffer[j++];
                    xs[i++] = x0 + (x1 << 8) + (x2 << 8);
                }
                return xs;
            }
        }
        public static int[] OptimizedUnpackInt32(byte[] buffer)
        {
            if (buffer.Length % 4 != 0) throw new ArgumentException($"Expected buffer length multiple of 4 bytes, but is {buffer.Length} bytes.");
            var xs = new int[buffer.Length / 4];
            for (int i = 0, j = 0; i < xs.Length; j += 4)
            {
                //var foo = new[] { buffer[j + 3], buffer[j + 2], buffer[j + 1], buffer[j + 0] };
                xs[i++] = BitConverter.ToInt32(buffer, j);
            }
            return xs;
        }
        public static long[] OptimizedUnpackInt64(byte[] buffer)
        {
            if (buffer.Length % 8 != 0) throw new ArgumentException($"Expected buffer length multiple of 8 bytes, but is {buffer.Length} bytes.");
            var xs = new long[buffer.Length / 8];
            for (int i = 0, j = 0; i < xs.Length; j += 8) xs[i++] = BitConverter.ToInt64(buffer, j);
            return xs;
        }

        public static byte[] Pack8(int[] data) => data.Map(x => (byte)x);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int BitCountInBytes(int n)
        {
            var c = n / 8;
            if (n % 8 != 0) c++;
            return c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetBits(ulong x, int start, int count)
        {
            //if (count < 1 || count > 8) throw new ArgumentOutOfRangeException(nameof(count));
            //if (start < 0 || start + count > 64) throw new ArgumentOutOfRangeException(nameof(start));
            var y = (uint)(x >> start);
            var mask = (1u << count) - 1;
            var r = y & mask;
            return (byte)r;
        }
        
        public class BitBuffer
        {
            public readonly byte[] Buffer;
            public readonly int LengthInBits;
            private int _i;
            private int _ibit;
            public BitBuffer(int lengthInBits)
            {
                Buffer = new byte[BitCountInBytes(lengthInBits)];
                LengthInBits = lengthInBits;
                _i = 0; _ibit = 0;
            }
            public BitBuffer(byte[] buffer, int bits)
            {
                Buffer = buffer;
                LengthInBits = ((buffer.Length * 8) / bits) * bits;
                _i = 0; _ibit = 0;
            }
            public void PushBits(byte x, int bitCount)
            {
                if (bitCount < 1 || bitCount > 8) throw new ArgumentOutOfRangeException(nameof(bitCount));
                if (_i * 8 + _ibit + bitCount > LengthInBits) throw new InvalidOperationException();

                var numberOfBitsRemainingInCurrentBufferByte = 8 - _ibit;
                var numberOfLeastSignificantBitsToTakeFromX = Math.Min(bitCount, numberOfBitsRemainingInCurrentBufferByte);
                var a = (byte)(GetBits(x, 0, numberOfLeastSignificantBitsToTakeFromX) << _ibit);
                Buffer[_i] |= a;
                _ibit += numberOfLeastSignificantBitsToTakeFromX;
                if (_ibit == 8) { _ibit = 0; _i++; }

                if (numberOfLeastSignificantBitsToTakeFromX < bitCount)
                {
                    var numberOfMostSignificantBitsToTakeFromCurrentBufferByte = bitCount - numberOfLeastSignificantBitsToTakeFromX;
                    var b = GetBits(x, numberOfLeastSignificantBitsToTakeFromX, numberOfMostSignificantBitsToTakeFromCurrentBufferByte);
                    Buffer[_i] = b;
                    _ibit = numberOfMostSignificantBitsToTakeFromCurrentBufferByte;
                }
            }
            public void PushBits(ulong x, int bitCount)
            {
                for (var i = 0; i < 64; i += 8, bitCount -= 8)
                {
                    var b = (byte)(x >> i);
                    if (bitCount <= 8) { PushBits(b, bitCount); return; }
                    PushBits(b, 8);
                }
            }

            public uint GetByte(int startBit, int bitCount)
            {
                if (bitCount > 8) bitCount = 8;
                var i = startBit >> 3;
                var shift = startBit % 8;
                var a = (uint)Buffer[i++] >> shift;
                var shift2 = 8 - shift;
                var b = (shift2 < bitCount) ? ((uint)Buffer[i] << shift2) : 0;
                return (a | b) & ((1u << bitCount) - 1);
            }

            public uint GetUInt(int startBit, int bitCount)
            {
                if (bitCount < 1 || bitCount > 32) throw new ArgumentOutOfRangeException(nameof(bitCount));
                if (startBit + bitCount > LengthInBits) throw new InvalidOperationException();
                
                var x = GetByte(startBit, bitCount);
                if (bitCount < 9) return x;
                x |= GetByte(startBit += 8, bitCount -= 8) << 8;
                if (bitCount < 9) return x;
                x |= GetByte(startBit += 8, bitCount -= 8) << 16;
                if (bitCount < 9) return x;
                x |= GetByte(startBit += 8, bitCount -= 8) << 24;
                return x;
            }

            public ulong GetULong(int startBit, int bitCount)
            {
                if (bitCount < 1 || bitCount > 64) throw new ArgumentOutOfRangeException(nameof(bitCount));
                if (startBit + bitCount > LengthInBits) throw new InvalidOperationException();

                return bitCount > 32
                    ? GetUInt(startBit, 32) | GetUInt(startBit + 32, bitCount - 32)
                    : GetUInt(startBit, bitCount);
            }

            public uint[] ReadUInts(int bits, int count)
            {
                var data = new uint[count];
                for (int i = 0, j = 0; i < count; i++, j += bits) data[i] = GetUInt(j, bits);
                return data;
            }
        }

        /// <summary>
        /// </summary>
        public static byte[] Pack(uint[] xs, int bits)
        {
            var buffer = new BitBuffer(xs.Length * bits);
            for (var i = 0; i < xs.Length; i++) buffer.PushBits(xs[i], bits);
            return buffer.Buffer;
        }

        /// <summary>
        /// </summary>
        public static void Unpack(byte[] buffer, int bits, int count, Action<ulong, int> nextValueAndIndex)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bits < 1 || bits > 64) throw new ArgumentException($"Argument 'bits' must be in range [1,64], but is {bits}.", nameof(bits));

            var bitbuffer = new BitBuffer(buffer, bits);
            for (int i = 0, j = 0; i < count; i++)
            {
                nextValueAndIndex(bitbuffer.GetULong(j, bits), i);
                j += bits;
            }
        }
    }
}
