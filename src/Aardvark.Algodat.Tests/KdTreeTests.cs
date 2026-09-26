/*
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
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class KdTreeTests
    {
        [Test]
        public void CreatingKdTreeDoesNotChangeOrderOfPoints()
        {
            var r = new Random();
            var ps = new V3f[1000].SetByIndex(_ => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble()));
            var copy = ps.Copy();

            var kd = ps.BuildKdTree();
            ClassicAssert.IsTrue(kd != null);
            ClassicAssert.IsTrue(ps.Length == copy.Length);
            for (var i = 0; i < ps.Length; i++)
            {
                ClassicAssert.IsTrue(ps[i] == copy[i]);
            }
        }

        [Test]
        public async Task V3dKdTreeBuildersPreservePrecisionAtLargeOffsets()
        {
            var result = await AssertV3dKdTreesMatchBruteForce(new V3d(1_000_000_000.0));
            Assert.That(result.Index, Is.Zero);
            Assert.That(result.Distance, Is.EqualTo(Math.Sqrt(6144.0)).Within(1e-12));
        }

        [Test]
        public async Task V3dKdTreeBuildersMatchOrdinaryCoordinateResults()
        {
            var ordinary = await AssertV3dKdTreesMatchBruteForce(V3d.Zero);
            var shifted = await AssertV3dKdTreesMatchBruteForce(new V3d(1_000_000_000.0));

            Assert.That(shifted.Index, Is.EqualTo(ordinary.Index));
            Assert.That(shifted.Distance, Is.EqualTo(ordinary.Distance).Within(1e-12));
        }

        private static async Task<(long Index, double Distance)> AssertV3dKdTreesMatchBruteForce(V3d offset)
        {
            var points = new[]
            {
                offset + new V3d(96, 64, -96),
                offset + new V3d(96, -32, -96),
                offset + new V3d(32, 96, 128)
            };
            var query = offset + new V3d(128, 32, -32);
            var arrayCopy = (V3d[])points.Clone();
            IList<V3d> list = new List<V3d>(points);
            var listCopy = new List<V3d>(list);

            long expectedIndex = 0;
            var expectedDistance = Vec.Distance(query, points[0]);
            for (var i = 1; i < points.Length; i++)
            {
                var distance = Vec.Distance(query, points[i]);
                if (distance < expectedDistance)
                {
                    expectedIndex = i;
                    expectedDistance = distance;
                }
            }

            var validData = points.CreateRkdTreeDist2(1e-12).Data;
            var trees = new[]
            {
                points.BuildKdTree(),
                list.BuildKdTree(),
                await points.BuildKdTreeAsync(),
                await list.BuildKdTreeAsync(),
                points.ToKdTree(validData),
                list.ToKdTree(validData)
            };

            foreach (var tree in trees)
            {
                var nearest = tree.GetClosest(query);
                Assert.That(nearest.Index, Is.EqualTo(expectedIndex));
                Assert.That(nearest.Dist, Is.EqualTo(expectedDistance).Within(1e-12));
            }

            CollectionAssert.AreEqual(arrayCopy, points);
            CollectionAssert.AreEqual(listCopy, list);
            return (expectedIndex, expectedDistance);
        }
    }
}
