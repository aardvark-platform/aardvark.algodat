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
using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Aardvark.Geometry.Points;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ProgressTests
    {
        [Test]
        public void ProgressToken_Init()
        {
            var x = Progress.Token(50, 100);
            Assert.IsTrue(x.Value == 50);
            Assert.IsTrue(x.Max == 100);
            Assert.IsTrue(x.Ratio == 0.5);
        }

        [Test]
        public void ProgressToken_Init_Negative()
        {
            var x = Progress.Token(-1, 100);
            Assert.IsTrue(x.Value == 0);
            Assert.IsTrue(x.Max == 100);
        }

        [Test]
        public void ProgressToken_Init_Exceed()
        {
            var x = Progress.Token(101, 100);
            Assert.IsTrue(x.Value == 100);
            Assert.IsTrue(x.Max == 100);
        }

        [Test]
        public void ProgressToken_Multiply()
        {
            var x = Progress.Token(10, 100) * 2;
            Assert.IsTrue(x.Value == 20);
            Assert.IsTrue(x.Max == 200);
        }

        [Test]
        public void ProgressToken_Divide()
        {
            var x = Progress.Token(10, 100) / 10;
            Assert.IsTrue(x.Value == 1);
            Assert.IsTrue(x.Max == 10);
        }

        [Test]
        public void ProgressToken_Add()
        {
            var x = Progress.Token(10, 100);
            var y = Progress.Token(60, 100);
            var sum = x + y;
            Assert.IsTrue(sum.Value == 70);
            Assert.IsTrue(sum.Max == 200);
        }



        [Test]
        public void ProgressReporter_Init()
        {
            var x = Progress.Reporter();
            Assert.IsTrue(x != null);
        }

        [Test]
        public void ProgressReporter_Subscribe()
        {
            var itWorked = false;
            var x = Progress.Reporter();
            x.Subscribe(_ => itWorked = true);
            x.Report(Progress.Token(1, 10));
            Assert.IsTrue(itWorked);
        }
    }
}
