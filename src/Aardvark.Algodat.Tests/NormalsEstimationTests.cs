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
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class NormalsEstimationTests
    {
        [Test]
        public void CanEstimateNormals_FromZeroToThreePoints()
        {
            var r = new Random();
            for (var n = 0; n < 4; n++)
            {
                var ps = new V3f[n].SetByIndex(_ => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble()));

                var kd = ps.BuildKdTree();

                var ns = Normals.EstimateNormals(ps, 16, kd);
                Assert.IsTrue(ns.Length == n);
            }
        }

        [Test]
        public void CanEstimateNormalsAsync_FromZeroToThreePoints()
        {
            var r = new Random();
            for (var n = 0; n < 4; n++)
            {
                var ps = new V3f[n].SetByIndex(_ => new V3f(r.NextDouble(), r.NextDouble(), r.NextDouble()));

                var kd = ps.BuildKdTreeAsync().Result;

                var ns = Normals.EstimateNormalsAsync(ps, 16, kd).Result;
                Assert.IsTrue(ns.Length == n);
            }
        }

        [Test]
        public void CanEstimateNormals()
        {
            var ps = new[]
            {
                new V3f(0, 0, 0),
                new V3f(1, 0, 0),
                new V3f(1, 1, 0),
                new V3f(0, 1, 0),
            };

            var kd = ps.BuildKdTree();

            var ns = Normals.EstimateNormals(ps, 16, kd);
            Assert.IsTrue(ns.Length == 4);
            Assert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }

        [Test]
        public void CanEstimateNormalsAsync()
        {
            var ps = new[]
            {
                new V3f(0, 0, 0),
                new V3f(1, 0, 0),
                new V3f(1, 1, 0),
                new V3f(0, 1, 0),
            };

            var kd = ps.BuildKdTreeAsync().Result;

            var ns = Normals.EstimateNormalsAsync(ps, 16, kd).Result;
            Assert.IsTrue(ns.Length == 4);
            Assert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }
        [Test]
        public void CanEstimateNormalsWithoutKdTree()
        {
            var ps = new[]
            {
                new V3f(0, 0, 0),
                new V3f(1, 0, 0),
                new V3f(1, 1, 0),
                new V3f(0, 1, 0),
            };

            var ns = Normals.EstimateNormals(ps, 16);
            Assert.IsTrue(ns.Length == 4);
            Assert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }

        [Test]
        public void CanEstimateNormalsWithoutKdTreeAsync()
        {
            var ps = new[]
            {
                new V3f(0, 0, 0),
                new V3f(1, 0, 0),
                new V3f(1, 1, 0),
                new V3f(0, 1, 0),
            };

            var ns = Normals.EstimateNormalsAsync(ps, 16).Result;
            Assert.IsTrue(ns.Length == 4);
            Assert.IsTrue(ns.All(n => n == V3f.ZAxis));
        }
    }
}
