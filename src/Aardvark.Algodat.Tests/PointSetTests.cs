﻿/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PointSetTests
    {
        internal static Storage CreateStorage()
        {
            var x = new SimpleMemoryStore();
            void add(string name, object value, Func<byte[]> create) => x.Add(name, create());
            return new Storage(add, x.Get, x.GetSlice, x.Remove, x.Dispose, x.Flush, cache: default);
        }

        internal static Storage CreateDiskStorage(string dbDiskLocation)
        {
            var x = new SimpleDiskStore(dbDiskLocation);
            void add(string name, object value, Func<byte[]> create) => x.Add(name, create());
            return new Storage(add, x.Get, x.GetSlice, x.Remove, x.Dispose, x.Flush, cache: default);
        }

        [Test]
        public void CanCreateEmptyPointSet()
        {
            var pointset = PointSet.Empty;
            ClassicAssert.IsTrue(pointset.Bounds.IsInvalid);
            ClassicAssert.IsTrue(pointset.BoundingBox.IsInvalid);
            ClassicAssert.IsTrue(pointset.Id == "PointSet.Empty");
            ClassicAssert.IsTrue(pointset.IsEmpty == true);
            ClassicAssert.IsTrue(pointset.PointCount == 0);
            ClassicAssert.IsTrue(pointset.SplitLimit == 0);
            pointset.ValidateTree();
        }

        [Test]
        public void CanCreatePointSetFromSinglePoint()
        {
            var store = CreateStorage();
            var ps = new List<V3d> { new(0.1, 0.2, 0.3) };
            var cs = new List<C4b> { C4b.White };
            var pointset = PointSet.Create(
                store, "id", ps, cs, null, null, null, null, 1000,
                generateLod: false, isTemporaryImportNode: true, default
                );
            ClassicAssert.IsTrue(pointset.PointCount == 1);
            ClassicAssert.IsTrue(pointset.Root.Value.IsLeaf());
            ClassicAssert.IsTrue(pointset.Root.Value.Positions.Value.Length == 1);
            ClassicAssert.IsTrue(pointset.Root.Value.PointCountTree == 1);
            pointset.ValidateTree();
        }

        [Test]
        public void CanCreateInMemoryPointSet()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            var qs = new List<byte> { 17 };
            _ = InMemoryPointSet.Build(ps, cs, ns, js, ks, qs, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithoutColors()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            var qs = new List<byte> { 17 };
            _ = InMemoryPointSet.Build(ps, null, ns, js, ks, qs, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithoutNormals()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            var qs = new List<byte> { 17 };
            _ = InMemoryPointSet.Build(ps, cs, null, js, ks, qs, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithoutIntensities()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var ks = new List<byte> { 42 };
            var qs = new List<byte> { 17 };
            _ = InMemoryPointSet.Build(ps, cs, ns, null, ks, qs, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithoutClassifications()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var qs = new List<byte> { 17 };
            _ = InMemoryPointSet.Build(ps, cs, ns, js, null, qs, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSetWithoutPartIndices()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            _ = InMemoryPointSet.Build(ps, cs, ns, js, ks, null, Cell.Unit, 1);
        }

        [Test]
        public void CanCreateInMemoryPointSet_Many()
        {
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var ps = new List<V3d>();
            var ns = new List<V3f>();
            for (var x = 0.125; x < 1.0; x += 0.25)
                for (var y = 0.125; y < 1.0; y += 0.25)
                    for (var z = 0.125; z < 1.0; z += 0.25)
                    {
                        ps.Add(new V3d(x, y, z));
                        ns.Add(V3f.ZAxis);
                    }

            ClassicAssert.IsTrue(ps.Count == 4 * 4 * 4);

            var imps = InMemoryPointSet.Build(ps, null, ns, null, null, null, new Cell(0, 0, 0, 0), 1);
            var root = imps.ToPointSetNode(storage, isTemporaryImportNode: false);
            ClassicAssert.IsTrue(root.PointCountTree == 4 * 4 * 4);
            var countNodes = root.CountLeafNodes(true);
            ClassicAssert.IsTrue(countNodes == 4 * 4 * 4);
            root.ValidateTree(1, false);
        }


        [Test]
        public void PointSetAttributes_EmptyPointSet()
        {
            var pointset = PointSet.Empty;
            ClassicAssert.IsTrue(pointset.HasColors == false);
            ClassicAssert.IsTrue(pointset.HasIntensities == false);
            ClassicAssert.IsTrue(pointset.HasKdTree == false);
            ClassicAssert.IsTrue(pointset.HasNormals == false);
            ClassicAssert.IsTrue(pointset.HasPositions == false);
            pointset.ValidateTree();
        }

        [Test]
        public void PointSetAttributes_All()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            var qs = new List<byte> { 17 };
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var pointset = PointSet.Create(storage, "test", ps, cs, ns, js, ks, qs, octreeSplitLimit: 1, generateLod: true, isTemporaryImportNode: false, default);
            ClassicAssert.IsTrue(pointset.HasColors == true);
            ClassicAssert.IsTrue(pointset.HasClassifications == true);
            ClassicAssert.IsTrue(pointset.HasIntensities == true);
            ClassicAssert.IsTrue(pointset.HasKdTree == true);
            ClassicAssert.IsTrue(pointset.HasNormals == true);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.HasPositions == true);
            pointset.ValidateTree();
        }

        [Test]
        public void PointSetAttributes_NoLod()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var ns = new List<V3f> { V3f.ZAxis };
            var js = new List<int> { 123 };
            var ks = new List<byte> { 42 };
            var qs = new List<byte> { 17 };
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var pointset = PointSet.Create(storage, "test", ps, cs, ns, js, ks, qs, octreeSplitLimit: 1, generateLod: false, isTemporaryImportNode: true, default);
            ClassicAssert.IsTrue(pointset.HasColors == true);
            ClassicAssert.IsTrue(pointset.HasClassifications == true);
            ClassicAssert.IsTrue(pointset.HasIntensities == true);
            //ClassicAssert.IsTrue(pointset.HasKdTree == true);
            ClassicAssert.IsTrue(pointset.HasNormals == true);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.HasPositions == true);
            pointset.ValidateTree();
        }

        [Test]
        public void PointSetAttributes_PositionsAndColors()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var cs = new List<C4b> { C4b.White };
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var pointset = PointSet.Create(storage, "test", ps, cs, null, null, null, null, 1, generateLod: true, isTemporaryImportNode: true, default);
            ClassicAssert.IsTrue(pointset.HasColors == true);
            ClassicAssert.IsTrue(pointset.HasIntensities == false);
            ClassicAssert.IsTrue(pointset.HasKdTree == true);
            ClassicAssert.IsTrue(pointset.HasNormals == true);
            ClassicAssert.IsTrue(pointset.HasPositions == true);
        }

        [Test]
        public void PointSet_PartIndexRange_No()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var storage = PointCloud.CreateInMemoryStore(cache: default);
            var pointset = PointSet.Create(storage, "test", ps, null, null, null, null, partIndices: null, 1, generateLod: false, isTemporaryImportNode: true, default);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.PartIndexRange == null);
        }

        //[Test]
        //public void PointSet_PartIndexRange()
        //{
        //    var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
        //    var storage = PointCloud.CreateInMemoryStore(cache: default);
        //    var pointset = PointSet.Create(
        //        storage, "test", ps, null, null, null, null, null, 1, generateLod: false, isTemporaryImportNode: true
        //        )
        //        .WithPartIndexRange(new(7, 11))
        //        ;
        //    ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(7, 11));
        //    ClassicAssert.IsTrue(pointset.HasPartIndexRange == true);
        //}

        //[Test]
        //public void PointSet_PartIndexRange_Serialization()
        //{
        //    var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
        //    var storage = PointCloud.CreateInMemoryStore(cache: default);

        //    var pointset = PointSet.Create(
        //        storage, "test", ps, null, null, null, null, null, 1, generateLod: false, isTemporaryImportNode: true
        //        )
        //        .WithPartIndexRange(new(7, 11))
        //        ;

        //    var json = pointset.ToJson();
        //    var reloaded = PointSet.Parse(json, storage);

        //    ClassicAssert.IsTrue(pointset.Id == reloaded.Id);
        //    ClassicAssert.IsTrue(pointset.SplitLimit == reloaded.SplitLimit);
        //    ClassicAssert.IsTrue(pointset.Root.Value.Id == reloaded.Root.Value.Id);
        //    ClassicAssert.IsTrue(pointset.PartIndexRange == reloaded.PartIndexRange);
        //}

        [Test]
        public void PointSet_PartIndexRange_Serialization_NoRange()
        {
            var ps = new List<V3d> { new(0.5, 0.5, 0.5) };
            var storage = PointCloud.CreateInMemoryStore(cache: default);

            var pointset = PointSet.Create(
                storage, "test", ps, null, null, null, null, null, 1, generateLod: false, isTemporaryImportNode: true
                );

            var json = pointset.ToJson();
            var reloaded = PointSet.Parse(json, storage);

            ClassicAssert.IsTrue(pointset.Id             == reloaded.Id            );
            ClassicAssert.IsTrue(pointset.SplitLimit     == reloaded.SplitLimit    );
            ClassicAssert.IsTrue(pointset.Root.Value.Id  == reloaded.Root.Value.Id );
            ClassicAssert.IsTrue(pointset.PartIndexRange == reloaded.PartIndexRange);
            ClassicAssert.IsTrue(reloaded.PartIndexRange == null);
        }
    }
}
