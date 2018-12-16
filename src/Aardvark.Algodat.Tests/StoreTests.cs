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
using System.IO;
using System.Linq;
using System.Threading;
using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class StoreTests
    {
        [Test]
        public void Store_CanReopenDisposedStore()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            var store = PointCloud.OpenStore(storepath);
            store.Add("key", new byte[] { 1, 2, 3 });
            var xs = store.GetByteArray("key");
            Assert.IsTrue(xs[0] == 1 && xs[1] == 2 && xs[2] == 3);

            store.Flush();
            store.Dispose();

            store = PointCloud.OpenStore(storepath);
            xs = store.GetByteArray("key");
            Assert.IsTrue(xs[0] == 1 && xs[1] == 2 && xs[2] == 3);
        }
    }
}
