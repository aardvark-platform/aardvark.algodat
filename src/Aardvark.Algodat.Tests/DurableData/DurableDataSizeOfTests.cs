///*
//    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
//    This program is free software: you can redistribute it and/or modify
//    it under the terms of the GNU Affero General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//    This program is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU Affero General Public License for more details.
//    You should have received a copy of the GNU Affero General Public License
//    along with this program.  If not, see <http://www.gnu.org/licenses/>.
//*/
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Runtime.InteropServices;
//using System.Runtime.Serialization;
//using Aardvark.Base;
//using NUnit.Framework;

//namespace Aardvark.Base.DurableDataCodec
//{
//    [TestFixture]
//    public class DurableDataSizeOfTests
//    {
//        [Test]
//        public void SizeOfPrimitives()
//        {
//            Assert.IsTrue(DurableCodec.SizeOf<byte>() == DurablePrimitiveTypes.OfType<byte>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<sbyte>() == DurablePrimitiveTypes.OfType<sbyte>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<short>() == DurablePrimitiveTypes.OfType<short>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<ushort>() == DurablePrimitiveTypes.OfType<ushort>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<int>() == DurablePrimitiveTypes.OfType<int>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<uint>() == DurablePrimitiveTypes.OfType<uint>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<long>() == DurablePrimitiveTypes.OfType<long>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<ulong>() == DurablePrimitiveTypes.OfType<ulong>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<float>() == DurablePrimitiveTypes.OfType<float>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<double>() == DurablePrimitiveTypes.OfType<double>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Guid>() == DurablePrimitiveTypes.OfType<Guid>().ElementSizeInBytes);
//        }

//        [Test]
//        public void SizeOfRanges()
//        {
//            Assert.IsTrue(DurableCodec.SizeOf<Cell>() == DurablePrimitiveTypes.OfType<Cell>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Range1sb>() == DurablePrimitiveTypes.OfType<Range1sb>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Range1b>() == DurablePrimitiveTypes.OfType<Range1b>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Range1s>() == DurablePrimitiveTypes.OfType<Range1s>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Range1us>() == DurablePrimitiveTypes.OfType<Range1us>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Range1i>() == DurablePrimitiveTypes.OfType<Range1i>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Range1ui>() == DurablePrimitiveTypes.OfType<Range1ui>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Range1l>() == DurablePrimitiveTypes.OfType<Range1l>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Range1ul>() == DurablePrimitiveTypes.OfType<Range1ul>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Box2i>() == DurablePrimitiveTypes.OfType<Box2i>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Box3i>() == DurablePrimitiveTypes.OfType<Box3i>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Box2l>() == DurablePrimitiveTypes.OfType<Box2l>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Box3l>() == DurablePrimitiveTypes.OfType<Box3l>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Box2f>() == DurablePrimitiveTypes.OfType<Box2f>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Box3f>() == DurablePrimitiveTypes.OfType<Box3f>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Box2d>() == DurablePrimitiveTypes.OfType<Box2d>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<Box3d>() == DurablePrimitiveTypes.OfType<Box3d>().ElementSizeInBytes);
//        }

//        [Test]
//        public void SizeOfVectors()
//        {
//            Assert.IsTrue(DurableCodec.SizeOf<V2i>() == DurablePrimitiveTypes.OfType<V2i>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V3i>() == DurablePrimitiveTypes.OfType<V3i>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V4i>() == DurablePrimitiveTypes.OfType<V4i>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V2l>() == DurablePrimitiveTypes.OfType<V2l>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V3l>() == DurablePrimitiveTypes.OfType<V3l>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V4l>() == DurablePrimitiveTypes.OfType<V4l>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V2f>() == DurablePrimitiveTypes.OfType<V2f>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V3f>() == DurablePrimitiveTypes.OfType<V3f>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V4f>() == DurablePrimitiveTypes.OfType<V4f>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V2d>() == DurablePrimitiveTypes.OfType<V2d>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V3d>() == DurablePrimitiveTypes.OfType<V3d>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<V4d>() == DurablePrimitiveTypes.OfType<V4d>().ElementSizeInBytes);
//        }

//        [Test]
//        public void SizeOfColors()
//        {
//            Assert.IsTrue(DurableCodec.SizeOf<C3b>() == DurablePrimitiveTypes.OfType<C3b>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<C4b>() == DurablePrimitiveTypes.OfType<C4b>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<C3f>() == DurablePrimitiveTypes.OfType<C3f>().ElementSizeInBytes);
//            Assert.IsTrue(DurableCodec.SizeOf<C4f>() == DurablePrimitiveTypes.OfType<C4f>().ElementSizeInBytes);
//        }
//    }
//}
