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
        #region roundtrip all overloads

        [Test]
        public void RoundtripFloat()
        {
            var x = 3.1415926f;

            // encode 1
            var buffer = DurableCodec.Encode(x);

            {
                var index = 0;
                DurableCodec.Decode(buffer, ref index, out float y);
                Assert.IsTrue(x == y);
                Assert.IsTrue(index == 4);
            }

            {
                var index = 0;
                var y = DurableCodec.Decode<float>(buffer, ref index);
                Assert.IsTrue(x == y);
                Assert.IsTrue(index == 4);
            }

            {
                DurableCodec.Decode(buffer, out float y);
                Assert.IsTrue(x == y);
            }

            {
                var y = DurableCodec.Decode<float>(buffer);
                Assert.IsTrue(x == y);
            }

            // encode 2
            {
                var buffer2 = new byte[1024];
                var index = 0;
                DurableCodec.Encode(buffer2, ref index, x);
                Assert.IsTrue(index == 4);

                var y = DurableCodec.Decode<float>(buffer);
                Assert.IsTrue(x == y);
            }
        }

        [Test]
        public void RoundtripFloatArray()
        {
            var xs = new float[] { 0.1f, 2.3f, 4.5f, 6.7f, 8.9f };

            // encode 1
            var buffer = DurableCodec.EncodeArray(xs);

            {
                var index = 0;
                DurableCodec.DecodeArray(buffer, ref index, out float[] ys);
                Assert.IsTrue(xs.Length == ys.Length);
                for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
                Assert.IsTrue(index == 24);
            }

            {
                var index = 0;
                var ys = DurableCodec.DecodeArray<float>(buffer, ref index);
                Assert.IsTrue(xs.Length == ys.Length);
                for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
                Assert.IsTrue(index == 24);
            }

            {
                DurableCodec.DecodeArray(buffer, out float[] ys);
                Assert.IsTrue(xs.Length == ys.Length);
                for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
            }

            {
                var ys = DurableCodec.DecodeArray<float>(buffer);
                Assert.IsTrue(xs.Length == ys.Length);
                for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
            }

            // encode 2
            {
                var buffer2 = new byte[1024];
                var index = 0;
                DurableCodec.EncodeArray(buffer2, ref index, xs);
                Assert.IsTrue(index == 24);

                var ys = DurableCodec.DecodeArray<float>(buffer);
                Assert.IsTrue(xs.Length == ys.Length);
                for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
            }
        }

        #endregion

        #region roundtrip guid

        [Test]
        public void RoundtripGuid()
        {
            var x = Guid.NewGuid();
            var buffer = DurableCodec.Encode(x);

            var y = DurableCodec.Decode<Guid>(buffer);

            Assert.IsTrue(x == y);
        }

        [Test]
        public void RoundtripGuidArray()
        {
            var xs = new Guid[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var buffer = DurableCodec.EncodeArray(xs);

            var ys = DurableCodec.DecodeArray<Guid>(buffer);

            Assert.IsTrue(xs.Length == ys.Length);
            for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
        }

        #endregion

        #region rountrip v3d

        [Test]
        public void RoundtripV3d()
        {
            var x = new V3d(1.23, 4.56, 7.89);
            var buffer = DurableCodec.Encode(x);

            var y = DurableCodec.Decode<V3d>(buffer);

            Assert.IsTrue(x == y);
        }

        [Test]
        public void RoundtripV3dArray()
        {
            var xs = new V3d[] { V3d.IOO, V3d.OIO, V3d.OOI };
            var buffer = DurableCodec.EncodeArray(xs);

            var ys = DurableCodec.DecodeArray<V3d>(buffer);

            Assert.IsTrue(xs.Length == ys.Length);
            for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
        }

        #endregion

        #region roundtrip string

        [Test]
        public void RoundtripStringUtf8()
        {
            var x = "Hello World!";
            var buffer = DurableCodec.EncodeString(x);

            var y = DurableCodec.DecodeString(buffer);

            Assert.IsTrue(x == y);
        }

        [Test]
        public void RoundtripStringUtf8Array()
        {
            var xs = new [] { "Hello", "World", "!" };
            var buffer = DurableCodec.EncodeStringArray(xs);

            var ys = DurableCodec.DecodeStringArray(buffer);

            Assert.IsTrue(xs.Length == ys.Length);
            for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
        }

        #endregion

        #region special cases

        [Test]
        public void CanRoundtripEmptyArray()
        {
            var xs = new Guid[0];
            var buffer = DurableCodec.EncodeArray(xs);

            var ys = DurableCodec.DecodeArray<Guid>(buffer);

            Assert.IsTrue(xs.Length == ys.Length);
            for (var i = 0; i < xs.Length; i++) Assert.IsTrue(xs[i] == ys[i]);
        }

        [Test]
        public void EncodeNullArrayFails()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                DurableCodec.EncodeArray<Guid>(null);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                var buffer = new byte[1024];
                var index = 0;
                DurableCodec.EncodeArray<Guid>(buffer, ref index, null);
            });
        }

        [Test]
        public void EncodeToNullBufferFails()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var x = Guid.NewGuid();
                var buffer = default(byte[]);
                var index = 0;
                DurableCodec.Encode(buffer, ref index, x);
            });

            Assert.Throws<ArgumentNullException>(() =>
            {
                var xs = new Guid[0];
                var buffer = default(byte[]);
                var index = 0;
                DurableCodec.EncodeArray(buffer, ref index, xs);
            });
        }

        #endregion
    }
}
