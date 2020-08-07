/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ProgressReportingTests
    {
        [Test]
        public void ProgressCallbackWorks()
        {
            var CHUNKSIZE = 10000;
            var CHUNKCOUNT = 10;

            var countProgressCallbacks = 0L;

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithProgressCallback(x => Interlocked.Increment(ref countProgressCallbacks));
                ;

            var pointcloud = PointCloud.Chunks(GenerateChunks(CHUNKSIZE).Take(CHUNKCOUNT), config);
            Assert.IsTrue(pointcloud.PointCount == CHUNKSIZE * CHUNKCOUNT);

            Assert.IsTrue(countProgressCallbacks > 1);


            IEnumerable<Chunk> GenerateChunks(int numberOfPointsPerChunk)
            {
                var r = new Random();
                while (true)
                {
                    var _ps = new V3d[numberOfPointsPerChunk];
                    for (var i = 0; i < numberOfPointsPerChunk; i++) _ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
                    yield return new Chunk(_ps);
                }
            }
        }
    }
}
