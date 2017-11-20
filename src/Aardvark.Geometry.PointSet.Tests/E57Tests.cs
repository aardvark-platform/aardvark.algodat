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
using System.IO;
using System.Linq;
using System.Threading;
using Aardvark.Base;
using Aardvark.Data.E57;
using Aardvark.Geometry.Points;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class E57Tests
    {
        [Test]
        public void E57_Addresses_PhysicalPlusPhysical()
        {
            Assert.IsTrue((new PhysicalAddress(100) + new PhysicalAddress(234)).Value == 334);
            Assert.IsTrue((new PhysicalAddress(600) + new PhysicalAddress(1000)).Value == 1600);
        }
        [Test]
        public void E57_Addresses_PhysicalPlusLogical()
        {
            PhysicalAddress x = new PhysicalAddress(1000) + new LogicalAddress(100);
            Assert.IsTrue(x.Value == 1104);
        }
        [Test]
        public void E57_Addresses_PhysicalToLogical_1()
        {
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(0)).Value == 0);
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(1019)).Value == 1019);
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(1024)).Value == 1020);
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(2048)).Value == 2040);
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(3072)).Value == 3060);

            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(1500)).Value == 1496);
        }
        [Test]
        public void E57_Addresses_PhysicalToLogical_2()
        {
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(1019)).Value == 1019);
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(1020)).Value == 1020);
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(1021)).Value == 1020);
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(1022)).Value == 1020);
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(1023)).Value == 1020);
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(1024)).Value == 1020);
            Assert.IsTrue(((LogicalAddress)new PhysicalAddress(1025)).Value == 1021);
        }

        [Test]
        public void E57_Addresses_LogicalPlusLogical()
        {
            Assert.IsTrue((new LogicalAddress(100) + new LogicalAddress(234)).Value == 334);
            Assert.IsTrue((new LogicalAddress(600) + new LogicalAddress(1000)).Value == 1600);
        }
        public void E57_Addresses_LogicalToPhysical()
        {
            Assert.IsTrue(((PhysicalAddress)new LogicalAddress(0)).Value == 0);
            Assert.IsTrue(((PhysicalAddress)new LogicalAddress(1019)).Value == 1019);
            Assert.IsTrue(((PhysicalAddress)new LogicalAddress(1020)).Value == 1024);
            Assert.IsTrue(((PhysicalAddress)new LogicalAddress(2039)).Value == 2043);
            Assert.IsTrue(((PhysicalAddress)new LogicalAddress(2040)).Value == 2048);
            Assert.IsTrue(((PhysicalAddress)new LogicalAddress(3059)).Value == 3067);
            Assert.IsTrue(((PhysicalAddress)new LogicalAddress(3060)).Value == 3072);
        }
    }
}
