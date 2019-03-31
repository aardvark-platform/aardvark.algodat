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
        public void CodecRoundtripFloat_Durable()
        {
            var x = 3.1415926f;
            var buffer = DurableCodec.Encode(x);

            var y = DurableCodec.Decode<float>(buffer);

            Assert.IsTrue(x == y);
        }

        [Test]
        public void CodecRoundtripFloatArray_Durable()
        {
            var xs = new float[] { 0.1f, 2.3f, 4.5f, 6.7f, 8.9f };
            var buffer = DurableCodec.EncodeArray(xs);

            var ys = DurableCodec.DecodeArray<float>(buffer);

            Assert.IsTrue(xs.Length == ys.Length);
            for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
        }



        [Test]
        public void CodecRoundtripGuid_Durable()
        {
            var x = Guid.NewGuid();
            var buffer = DurableCodec.Encode(x);

            var y = DurableCodec.Decode<Guid>(buffer);

            Assert.IsTrue(x == y);
        }

        [Test]
        public void CodecRoundtripGuidArray_Durable()
        {
            var xs = new Guid[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var buffer = DurableCodec.EncodeArray(xs);

            var ys = DurableCodec.DecodeArray<Guid>(buffer);

            Assert.IsTrue(xs.Length == ys.Length);
            for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
        }



        [Test]
        public void CodecRoundtripV3d_Durable()
        {
            var x = new V3d(1.23, 4.56, 7.89);
            var buffer = DurableCodec.Encode(x);

            var y = DurableCodec.Decode<V3d>(buffer);

            Assert.IsTrue(x == y);
        }

        [Test]
        public void CodecRoundtripV3dArray_Durable()
        {
            var xs = new V3d[] { V3d.IOO, V3d.OIO, V3d.OOI };
            var buffer = DurableCodec.EncodeArray(xs);

            var ys = DurableCodec.DecodeArray<V3d>(buffer);

            Assert.IsTrue(xs.Length == ys.Length);
            for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
        }
    }
}
