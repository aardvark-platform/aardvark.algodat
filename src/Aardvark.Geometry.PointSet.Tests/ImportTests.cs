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
using Aardvark.Geometry.Points;
using NUnit.Framework;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ImportTests
    {
        #region File Formats

        [Test]
        public void CanRegisterFileFormat()
        {
            PointCloudFormat.Register(new PointCloudFormat("Test Description 1", new[] { ".test1" }, null, null));
        }

        [Test]
        public void CanRetrieveFileFormat()
        {
            PointCloudFormat.Register(new PointCloudFormat("Test Description 2", new[] { ".test2" }, null, null));

            var format = PointCloudFormat.FromFileName(@"C:\Data\pointcloud.test2");
            Assert.IsTrue(format != null);
            Assert.IsTrue(format.Description == "Test Description 2");
        }

        [Test]
        public void CanRetrieveFileFormat2()
        {
            PointCloudFormat.Register(new PointCloudFormat("Test Description 3", new[] { ".test3", ".tst3" }, null, null));

            var format1 = PointCloudFormat.FromFileName(@"C:\Data\pointcloud.test3");
            var format2 = PointCloudFormat.FromFileName(@"C:\Data\pointcloud.tst3");
            Assert.IsTrue(format1 != null && format1.Description == "Test Description 3");
            Assert.IsTrue(format2 != null && format2.Description == "Test Description 3");
        }

        [Test]
        public void UnknownFileFormatGivesUnknown()
        {
            var format = PointCloudFormat.FromFileName(@"C:\Data\pointcloud.foo");
            Assert.IsTrue(format == PointCloudFormat.Unknown);
        }

        #endregion

        #region Chunks

        [Test]
        public void CanImportChunkWithoutColor()
        {
            int n = 100;
            var r = new Random();
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());

            PointCloud.Chunks(new Chunk(ps, null), new ImportConfig
            {
                Storage = PointCloud.CreateInMemoryStore(),
                Key = "test",
                OctreeSplitLimit = 10
            });
        }

        #endregion

        #region General

        [Test]
        public void CanCreateInMemoryStore()
        {
            var store = PointCloud.CreateInMemoryStore();
            Assert.IsTrue(store != null);
        }

        [Test]
        public void CanCreateOutOfCoreStore()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            var store = PointCloud.OpenStore(storepath);
            Assert.IsTrue(store != null);
        }

        [Test]
        public void CanCreateOutOfCoreStore_FailsIfPathIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var storepath = (string)null;
                var store = PointCloud.OpenStore(storepath);
                Assert.IsTrue(store != null);
            });
        }

        [Test]
        public void CanCreateOutOfCoreStore_FailsIfInvalidPath()
        {
            Assert.That(() =>
            {
                var storepath = @"some invalid path C:\";
                var store = PointCloud.OpenStore(storepath);
                Assert.IsTrue(store != null);
            },
            Throws.Exception
            );
        }

        [Test]
        public void CanImportFile_WithoutConfig_InMemory()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            TestContext.WriteLine($"testfile is '{filename}'");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            var a = PointCloud.Import(filename);
            Assert.IsTrue(a != null);
            Assert.IsTrue(a.PointCount == 3);
        }

        [Test]
        public void CanImportFile_WithoutConfig_OutOfCore()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            TestContext.WriteLine($"storepath is '{storepath}'");
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var a = PointCloud.Import(filename, storepath);
            Assert.IsTrue(a != null);
            Assert.IsTrue(a.PointCount == 3);
        }

        [Test]
        public void CanImportFileAndLoadFromStore()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            TestContext.WriteLine($"storepath is '{storepath}'");
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var a = PointCloud.Import(filename, storepath);
            var key = a.Id;

            var b = PointCloud.Load(key, storepath);
            Assert.IsTrue(b != null);
            Assert.IsTrue(b.PointCount == 3);
        }

        #endregion

        #region Pts

        [Test]
        public void CanParsePtsFileInfo()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var info = PointCloud.ParseFileInfo(filename, ImportConfig.Default);

            Assert.IsTrue(info.PointCount == 3);
            Assert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParsePtsFile()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            var ps = PointCloud.Parse(filename, ImportConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(ps.Length == 3);
            Assert.IsTrue(ps[0].ApproxEqual(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        public void CanImportPtsFile()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = new ImportConfig
            {
                Storage = PointCloud.CreateInMemoryStore(),
                Key = "test"
            };
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'"); 
            var config = new ImportConfig
            {
                Storage = PointCloud.CreateInMemoryStore(),
                Key = "test"
            };
            var pointset = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test", CancellationToken.None);
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
        }

        #endregion

        #region E57

        [Test]
        public void CanParseE57FileInfo()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var info = PointCloud.ParseFileInfo(filename, ImportConfig.Default);

            Assert.IsTrue(info.PointCount == 3);
            Assert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        [Ignore("waiting for E57 implementation")]
        public void CanParseE57File()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var ps = PointCloud.Parse(filename, ImportConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(ps.Length == 3);
            Assert.IsTrue(ps[0].ApproxEqual(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        [Ignore("waiting for E57 implementation")]
        public void CanImportE57File()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = new ImportConfig
            {
                Storage = PointCloud.CreateInMemoryStore(),
                Key = "test"
            };
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
        }

        [Test]
        [Ignore("waiting for E57 implementation")]
        public void CanImportE57FileAndLoadFromStore()
        {
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = new ImportConfig
            {
                Storage = PointCloud.CreateInMemoryStore(),
                Key = "test"
            };
            var pointset = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test", CancellationToken.None);
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
        }

        #endregion
    }
}
