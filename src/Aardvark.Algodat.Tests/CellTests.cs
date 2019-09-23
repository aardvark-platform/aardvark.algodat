/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class CellTests
    {
        #region box inside/outside tests

        [Test]
        public void BoxInsideOutside_Equals()
        {
            var a = new Box3d(new V3d(1, 2, 3), new V3d(4, 5, 6));
            var b = new Box3d(new V3d(1, 2, 3), new V3d(4, 5, 6));
            Assert.IsTrue(a == b);
        }

        [Test]
        public void BoxInsideOutside_ContainsEqual()
        {
            var a = new Box3d(new V3d(1, 2, 3), new V3d(4, 5, 6));
            var b = new Box3d(new V3d(1, 2, 3), new V3d(4, 5, 6));
            Assert.IsTrue(a.Contains(b));
            Assert.IsTrue(b.Contains(a));
        }

        [Test]
        public void BoxInsideOutside_Contains_Inside()
        {
            var a = new Box3d(new V3d(0, 0, 0), new V3d(4, 4, 4));
            var b = new Box3d(new V3d(1, 1, 2), new V3d(2, 2, 3));
            Assert.IsTrue(a.Contains(b));
        }

        [Test]
        public void BoxInsideOutside_Contains_InsideTouching()
        {
            var a = new Box3d(new V3d(0, 0, 0), new V3d(4, 4, 4));
            var b = new Box3d(new V3d(0, 0, 0), new V3d(2, 2, 2));
            Assert.IsTrue(a.Contains(b));
        }

        [Test]
        public void BoxInsideOutside_Contains_Outside()
        {
            var a = new Box3d(new V3d(0, 0, 0), new V3d(4, 4, 4));
            var b = new Box3d(new V3d(6, 0, 0), new V3d(8, 2, 2));
            Assert.IsTrue(!a.Contains(b));
        }

        [Test]
        public void BoxInsideOutside_Contains_OutsideTouching()
        {
            var a = new Box3d(new V3d(0, 0, 0), new V3d(4, 4, 4));
            var b = new Box3d(new V3d(4, 0, 0), new V3d(8, 4, 4));
            Assert.IsTrue(!a.Contains(b));
        }

        [Test]
        public void BoxnsideOutside_Intersects_OutsideTouchingMin()
        {
            var a = new Box3d(new V3d(4, 0, 0), new V3d(8, 4, 4));
            var b = new Box3d(new V3d(0, 0, 0), new V3d(4, 4, 4));
            Assert.IsTrue(!a.Intersects(b));
        }

        [Test]
        public void BoxInsideOutside_Intersects_OutsideTouchingMax()
        {
            var a = new Box3d(new V3d(0, 0, 0), new V3d(4, 4, 4));
            var b = new Box3d(new V3d(4, 0, 0), new V3d(8, 4, 4));
            Assert.IsTrue(!a.Intersects(b));
        }

        #endregion

        #region json serialization

        [Test]
        public void UnitCell()
        {
            Assert.IsTrue(Cell.Unit.X == 0 && Cell.Unit.Y == 0 && Cell.Unit.Z == 0);
            Assert.IsTrue(Cell.Unit.Exponent == 0);
            Assert.IsTrue(Cell.Unit.BoundingBox == Box3d.Unit);
        }

        [Test]
        public void CanSerializeCellToJson()
        {
            var a = new Cell(1, 2, 3, -1);
            var json = JObject.FromObject(a);
        }

        [Test]
        public void CanDeserializeCellFromJson()
        {
            var a = new Cell(1, 2, 3, -1);
            var json = JObject.FromObject(a);

            var b = json.ToObject<Cell>();
            var c = JObject.Parse(json.ToString()).ToObject<Cell>();

            Assert.IsTrue(a == b);
            Assert.IsTrue(a == c);
        }
        
        [Test]
        public void CellDeserializationWorks()
        {
            var json = JObject.Parse("{\"X\": -8,\"Y\": 250,\"Z\": 0,\"E\": 10}");
            var a = new Cell((long)json["X"], (long)json["Y"], (long)json["Z"], (int)json["E"]);
            var bb = a.BoundingBox;

            Assert.IsTrue(!bb.IsEmpty);
        }

        #endregion

        //[Test]
        //public void Bug1()
        //{
        //    /*
        //        rootCell    {[[-7142.36701000002, 256995.808436, 706.360471999971], [-7142.36700999999, 256995.808436, 706.360472]]}
        //        a           {[[-7142.36701, 256995.808436, 706.360472], [-7142.36701, 256995.808436, 706.360472]]}

        //        X       -7142.36701000002       -7142.36700999999
        //                            -7142.36701
        //        Y       256995.808436           256995.808436
        //                            256995.808436
        //        Z       706.360471999971        706.360472
        //                            706.360472

        //        Octant0     {[[-7142.36701000002, 256995.808436, 706.360471999971], [-7142.36701, 256995.808436, 706.360471999986]]}
        //                -7142.36701000002   ####    -7142.36701     ####    -7142.36701
        //                256995.808436       ####    256995.808436   ####    256995.808436
        //                706.360471999971    ####    706.360472      ####  !!! 706.360471999986
        //        Octant4     {[[-7142.36701000002, 256995.808436, 706.360471999986], [-7142.36701, 256995.808436, 706.360472]]}
        //                -7142.36701000002   ####    -7142.36701     ####    -7142.36701
        //                256995.808436       ####    256995.808436   ####    256995.808436
        //                706.360471999986    ####    706.360472      ####    706.360472
        //    */
        //    var _1 = -7142.36701 >= -7142.36701000002;
        //    var _2 = -7142.36701 <= -7142.36701;
        //    var _3 = 256995.808436 >= 256995.808436;
        //    var _4 = 256995.808436 <= 256995.808436;
        //    var _5 = 706.360472 >= 706.360471999986;
        //    var _6 = 706.360472 <= 706.360472;

        //    var rootCell = new Cell(-245409861791835, 8830308739533606, 24270361011416, -35);
        //    var a = new Cell(-125649849237419232, 4521118074641206784, 12426424837845498, -44);

        //    Assert.IsTrue(rootCell != a);
        //    Assert.IsTrue(rootCell.Contains(a));

        //    var children = rootCell.Children;
        //    var contained = new bool[8];
        //    for (var i = 0; i < 8; i++)
        //    {
        //        var octant = rootCell.GetOctant(i);
        //        //var bb1 = octant.BoundingBox;
        //        //var bb2 = a.BoundingBox;
        //        if (octant.Contains(a)) contained[i] = true;
        //        //if (bb1.Contains(bb2)) contained[i] = true;
        //        //if (MyContains(bb1, bb2)) contained[i] = true;
        //    }

        //    Assert.IsTrue(contained.Where(x => x == true).Count() == 1);
        //}

        //public static bool MyContains(Box3d a, Box3d b)
        //{
        //    var _1 = b.Min.X >= a.Min.X;
        //    var _2 = b.Max.X <= a.Max.X;
        //    var _3 = b.Min.Y >= a.Min.Y;
        //    var _4 = b.Max.Y <= a.Max.Y;
        //    var _5 = b.Min.Z >= a.Min.Z;
        //    var _6 = b.Max.Z <= a.Max.Z;
        //    var _r =
        //        _1 && _2 &&
        //        _3 && _4 &&
        //        _5 && _6;
        //    return _r;
        //}
    }
}
