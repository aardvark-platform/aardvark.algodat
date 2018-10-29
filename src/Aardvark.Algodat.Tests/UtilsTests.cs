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
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Aardvark.Base;
using Aardvark.Data.Points;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class UtilsTests
    {
        #region DistLessThanL1

        [Test]
        public void DistLessThanL1_1()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(1, 2, 3);
            Assert.IsTrue(Utils.DistLessThanL1(ref a, ref b, 0.1));
        }

        [Test]
        public void DistLessThanL1_2()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(1, 2, 3);
            Assert.IsTrue(!Utils.DistLessThanL1(ref a, ref b, 0.0));
        }

        [Test]
        public void DistLessThanL1_3()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            Assert.IsTrue(Utils.DistLessThanL1(ref a, ref b, 1.0001));
        }

        [Test]
        public void DistLessThanL1_4()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            Assert.IsTrue(!Utils.DistLessThanL1(ref a, ref b, 1.0));
        }

        [Test]
        public void DistLessThanL1_5()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            Assert.IsTrue(Utils.DistLessThanL1(ref b, ref a, 1.0001));
        }

        [Test]
        public void DistLessThanL1_6()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            Assert.IsTrue(!Utils.DistLessThanL1(ref b, ref a, 1.0));
        }

        #endregion

        #region DistLessThanL2

        [Test]
        public void DistLessThanL2_1()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(1, 2, 3);
            Assert.IsTrue(Utils.DistLessThanL2(ref a, ref b, 0.1));
        }

        [Test]
        public void DistLessThanL2_2()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(1, 2, 3);
            Assert.IsTrue(!Utils.DistLessThanL2(ref a, ref b, 0.0));
        }

        [Test]
        public void DistLessThanL2_3()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            Assert.IsTrue(!Utils.DistLessThanL2(ref a, ref b, 1.0001));
        }

        [Test]
        public void DistLessThanL2_4()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            Assert.IsTrue(!Utils.DistLessThanL2(ref a, ref b, 1.0));
        }

        [Test]
        public void DistLessThanL2_5()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            Assert.IsTrue(!Utils.DistLessThanL2(ref b, ref a, 1.0001));
        }

        [Test]
        public void DistLessThanL2_6()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            Assert.IsTrue(!Utils.DistLessThanL2(ref b, ref a, 1.0));
        }

        #endregion
    }
}
