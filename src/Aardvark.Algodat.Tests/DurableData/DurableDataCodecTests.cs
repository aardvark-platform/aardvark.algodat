/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Base.DurableDataCodec
{
    [TestFixture]
    public class DurableDataCodecTests
    {
        [Test]
        public void CodecRoundtripFloat()
        {
            var x = 3.1415926f;
            var buffer = new byte[Marshal.SizeOf<float>()];
            var index = 0;
            Codec.Write(buffer, ref index, x);

            index = 0;
            var y = Codec.Read<float>(buffer, ref index);
            
            Assert.IsTrue(x == y);
        }

        [Test]
        public void CodecRoundtripFloatArray()
        {
            var xs = new float[] { 0.1f, 2.3f, 4.5f, 6.7f, 8.9f };
            var buffer = new byte[xs.Length * Marshal.SizeOf<float>()];
            var index = 0;
            Codec.Write(buffer, ref index, xs);

            index = 0;
            var ys = new float[xs.Length];
            Codec.Read(buffer, ref index, ref ys);

            Assert.IsTrue(xs.Length == ys.Length);
            for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
        }

        [Test]
        public void CodecRoundtripV3d()
        {
            var x = new V3d(1.23, 4.56, 7.89);
            var buffer = new byte[Marshal.SizeOf<V3d>()];
            var index = 0;
            Codec.Write(buffer, ref index, x);

            index = 0;
            var y = Codec.Read<V3d>(buffer, ref index);

            Assert.IsTrue(x == y);
        }

        [Test]
        public void CodecRoundtripV3dArray()
        {
            var xs = new V3d[] { V3d.IOO, V3d.OIO, V3d.OOI };
            var buffer = new byte[xs.Length * Marshal.SizeOf<V3d>()];
            var index = 0;
            Codec.Write(buffer, ref index, xs);

            index = 0;
            var ys = new V3d[xs.Length];
            Codec.Read(buffer, ref index, ref ys);

            Assert.IsTrue(xs.Length == ys.Length);
            for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
        }
    }
}
