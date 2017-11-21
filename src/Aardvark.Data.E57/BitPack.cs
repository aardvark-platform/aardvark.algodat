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

namespace Aardvark.Base
{
    public static class BitPack
    {
        public static Array UnpackIntegers(byte[] buffer, int bits)
        {
            switch (bits)
            {
                case 2: return UnpackInt2(buffer);
                case 4: return UnpackInt4(buffer);
                case 8: return UnpackInt8(buffer);
                case 12: return UnpackInt12(buffer);
                case 16: return UnpackInt16(buffer);
                case 20: return UnpackInt20(buffer);
                case 24: return UnpackInt24(buffer);
                case 32: return UnpackInt32(buffer);
                case 64: return UnpackInt64(buffer);
                default: throw new NotImplementedException($"BitPack.UnpackIntegers({bits})");
            }
        }
        public static byte[] UnpackInt2(byte[] buffer)
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
        public static byte[] UnpackInt4(byte[] buffer)
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
        public static byte[] UnpackInt8(byte[] buffer) => buffer;
        public static short[] UnpackInt12(byte[] buffer)
        {
            checked
            {
                if ((buffer.Length * 8) % 12 != 0) throw new ArgumentException($"Expected buffer length multiple of 12 bits, but is {buffer.Length} bytes.");
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
        public static short[] UnpackInt16(byte[] buffer)
        {
            if (buffer.Length % 2 != 0) throw new ArgumentException($"Expected buffer length multiple of 2 bytes, but is {buffer.Length} bytes.");
            var xs = new short[buffer.Length / 2];
            for (int i = 0, j = 0; i < xs.Length; j += 2) xs[i++] = BitConverter.ToInt16(buffer, j);
            return xs;
        }
        public static int[] UnpackInt20(byte[] buffer)
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
        public static int[] UnpackInt24(byte[] buffer)
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
        public static int[] UnpackInt32(byte[] buffer)
        {
            if (buffer.Length % 4 != 0) throw new ArgumentException($"Expected buffer length multiple of 4 bytes, but is {buffer.Length} bytes.");
            var xs = new int[buffer.Length / 4];
            for (int i = 0, j = 0; i < xs.Length; j += 4) xs[i++] = BitConverter.ToInt32(buffer, j);
            return xs;
        }
        public static long[] UnpackInt64(byte[] buffer)
        {
            if (buffer.Length % 8 != 0) throw new ArgumentException($"Expected buffer length multiple of 8 bytes, but is {buffer.Length} bytes.");
            var xs = new long[buffer.Length / 8];
            for (int i = 0, j = 0; i < xs.Length; j += 8) xs[i++] = BitConverter.ToInt64(buffer, j);
            return xs;
        }
    }
}
