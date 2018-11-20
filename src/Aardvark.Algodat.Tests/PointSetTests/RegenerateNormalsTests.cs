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
    public class RegenerateNormalsTests
    {
        [Test]
        public void CanRegenerateNormals()
        {
            // create original pointset
            var r = new Random();
            var storage = PointSetTests.CreateStorage();
            var ps = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var pointset0 = PointSet.Create(storage, "test", ps.ToList(), null, null, null, null, 5000, true, CancellationToken.None);
            Assert.IsTrue(!(pointset0.HasNormals || pointset0.HasLodNormals));

            // create new pointset with regenerated normals
            var pointset1 = pointset0.RegenerateNormals(xs => xs.Map(_ => V3f.XAxis), default, default);
            Assert.IsTrue(pointset1.HasNormals || pointset1.HasLodNormals);


            // compare original and regenerated
            Assert.IsTrue(pointset0.Octree.Value.CountPoints() == pointset1.Octree.Value.CountPoints());

#pragma warning disable CS0618 // Type or member is obsolete
            var nodes0 = pointset0.Root.Value.ForEachNode().ToArray();
            var nodes1 = pointset1.Root.Value.ForEachNode().ToArray();
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.IsTrue(nodes0.Length == nodes1.Length);

            for (var ni = 0; ni < nodes0.Length; ni++)
            {
                var node0 = nodes0[ni]; var node1 = nodes1[ni];

                Assert.IsTrue(node0.Center == node1.Center);

                var ps0 = node0.Positions?.Value; var ps1 = node1.Positions?.Value;
                for (var i = 0; i < ps0?.Length; i++) Assert.IsTrue(ps0[i] == ps1[i]);

                var lps0 = node0.LodPositions.Value; var lps1 = node1.LodPositions.Value;
                for (var i = 0; i < lps0.Length; i++) Assert.IsTrue(lps0[i] == lps1[i]);

                var ns0 = node0.Normals?.Value; var ns1 = node1.Normals?.Value;
                for (var i = 0; i < ns0?.Length; i++) Assert.IsTrue(ns0[i] + ns1[i] == V3f.ZAxis + V3f.XAxis);

                var lns0 = node0.LodNormals?.Value; var lns1 = node1.LodNormals?.Value;
                for (var i = 0; i < lns0?.Length; i++) Assert.IsTrue(lns0[i] + lns1[i] == V3f.ZAxis + V3f.XAxis);
            }
        }

        [Test]
        public void CanRegenerateNormals_Overwrite()
        {
            // create original pointset
            var r = new Random();
            var storage = PointSetTests.CreateStorage();
            var ps = new V3d[42000].SetByIndex(_ => new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var ns = ps.Map(_ => V3f.ZAxis);
            var pointset0 = PointSet.Create(storage, "test", ps.ToList(), null, ns.ToList(), null, null, 5000, true, CancellationToken.None);
            Assert.IsTrue(pointset0.HasNormals || pointset0.HasLodNormals);

            // create new pointset with regenerated normals
            var pointset1 = pointset0.RegenerateNormals(xs => xs.Map(_ => V3f.XAxis), default, default);
            Assert.IsTrue(pointset1.HasNormals || pointset1.HasLodNormals);


            // compare original and regenerated
            Assert.IsTrue(pointset0.Octree.Value.CountPoints() == pointset1.Octree.Value.CountPoints());

#pragma warning disable CS0618 // Type or member is obsolete
            var nodes0 = pointset0.Root.Value.ForEachNode().ToArray();
            var nodes1 = pointset1.Root.Value.ForEachNode().ToArray();
#pragma warning restore CS0618 // Type or member is obsolete
            Assert.IsTrue(nodes0.Length == nodes1.Length);

            for (var ni = 0; ni < nodes0.Length; ni++)
            {
                var node0 = nodes0[ni]; var node1 = nodes1[ni];

                Assert.IsTrue(node0.Center == node1.Center);

                var ps0 = node0.Positions?.Value; var ps1 = node1.Positions?.Value;
                for (var i = 0; i < ps0?.Length; i++) Assert.IsTrue(ps0[i] == ps1[i]);

                var lps0 = node0.LodPositions.Value; var lps1 = node1.LodPositions.Value;
                for (var i = 0; i < lps0.Length; i++) Assert.IsTrue(lps0[i] == lps1[i]);

                var ns0 = node0.Normals?.Value; var ns1 = node1.Normals?.Value;
                for (var i = 0; i < ns0?.Length; i++) Assert.IsTrue(ns0[i] + ns1[i] == V3f.ZAxis + V3f.XAxis);

                var lns0 = node0.LodNormals?.Value; var lns1 = node1.LodNormals?.Value;
                for (var i = 0; i < lns0?.Length; i++) Assert.IsTrue(lns0[i] + lns1[i] == V3f.ZAxis + V3f.XAxis);
            }
        }
    }
}
