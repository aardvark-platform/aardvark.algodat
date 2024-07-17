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
using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.IO;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ImportTests
    {
        #region File Formats

        [Test]
        public void CanRegisterFileFormat()
        {
            PointCloudFileFormat.Register(new PointCloudFileFormat("Test Description 1", new[] { ".test1" }, null, null));
        }

        [Test]
        public void CanRetrieveFileFormat()
        {
            PointCloudFileFormat.Register(new PointCloudFileFormat("Test Description 2", new[] { ".test2" }, null, null));

            var format = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.test2");
            ClassicAssert.IsTrue(format != null);
            ClassicAssert.IsTrue(format.Description == "Test Description 2");
        }

        [Test]
        public void CanRetrieveFileFormat2()
        {
            PointCloudFileFormat.Register(new PointCloudFileFormat("Test Description 3", new[] { ".test3", ".tst3" }, null, null));

            var format1 = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.test3");
            var format2 = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.tst3");
            ClassicAssert.IsTrue(format1 != null && format1.Description == "Test Description 3");
            ClassicAssert.IsTrue(format2 != null && format2.Description == "Test Description 3");
        }

        [Test]
        public void UnknownFileFormatGivesUnknown()
        {
            var format = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.foo");
            ClassicAssert.IsTrue(format == PointCloudFileFormat.Unknown);
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
            var chunk = new Chunk(ps, null, null, null, null, null, null, null);

            ClassicAssert.IsTrue(chunk.Count == 100);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            ClassicAssert.IsTrue(pointcloud.PointCount == 100);
        }

        [Test]
        public void CanImportChunk_MinDist()
        {
            int n = 100;
            var r = new Random();
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
            var chunk = new Chunk(ps, null, null, null, null, null, null, null);

            ClassicAssert.IsTrue(chunk.Count == 100);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.5)
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            ClassicAssert.IsTrue(pointcloud.PointCount < 100);
        }

        [Test]
        public void CanImportChunk_Reproject()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);
            var bb = new Box3d(ps);

            var chunk = new Chunk(ps, null, null, null, null, null, null, null);
            ClassicAssert.IsTrue(chunk.Count == 10);
            ClassicAssert.IsTrue(chunk.BoundingBox == bb);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithReproject(xs => xs.Select(x => x += V3d.OIO).ToArray())
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            ClassicAssert.IsTrue(pointcloud.BoundingBox == bb + V3d.OIO);
        }

        [Test]
        public void CanImport_WithKey()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps, null, null, null, null, null, null, null);
            ClassicAssert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.0)
                .WithReproject(null)
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            ClassicAssert.IsTrue(pointcloud.Id == "test");
        }


        [Test]
        public void CanImportWithKeyAndThenLoadAgainFromStore()
        {
            var store = PointCloud.CreateInMemoryStore(cache: default);
            var key = "test";

            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps, null, null, null, null, null, null, null);
            ClassicAssert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(store)
                .WithKey(key)
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.0)
                .WithReproject(null)
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            ClassicAssert.IsTrue(pointcloud.Id == key);


            var reloaded = store.GetPointSet(key);
            ClassicAssert.IsTrue(reloaded.Id == key);
        }

        [Test]
        public void CanImport_WithoutKey()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps, null, null, null, null, null, null, null);
            ClassicAssert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey(null)
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.0)
                .WithReproject(null)
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            ClassicAssert.IsTrue(pointcloud.Id != null);
        }

        [Test]
        public void CanImport_DuplicateKey()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps, null, null, null, null, null, null, null);
            ClassicAssert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.0)
                .WithReproject(null)
                ;

            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);

            var pointcloud = PointCloud.Chunks(Array.Empty<Chunk>(), config);
            ClassicAssert.IsTrue(pointcloud.Id != null);
            ClassicAssert.IsTrue(pointcloud.PointCount == 0);


            var pointcloud2 = PointCloud.Chunks(chunk, config);
            ClassicAssert.IsTrue(pointcloud2.Id != null);
            ClassicAssert.IsTrue(pointcloud2.PointCount == 10);


            var reloaded = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(reloaded.PointCount == 10);
        }

        [Test]
        public void CanImport_Empty()
        {
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.0)
                .WithReproject(null)
                ;


            var pointcloud = PointCloud.Chunks(Array.Empty<Chunk>(), config);
            ClassicAssert.IsTrue(pointcloud.Root != null);
            ClassicAssert.IsTrue(pointcloud.Root.Id == Guid.Empty.ToString());
            ClassicAssert.IsTrue(pointcloud.Root.Value.IsEmpty);
            ClassicAssert.IsTrue(pointcloud.Id == "test");
            ClassicAssert.IsTrue(pointcloud.PointCount == 0);
            
            var reloaded = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(reloaded.Id == "test");
            ClassicAssert.IsTrue(reloaded.PointCount == 0);
        }

        #endregion

        #region General

        [Test]
        public void CanCreateInMemoryStore()
        {
            using var store = PointCloud.CreateInMemoryStore(cache: default);
            ClassicAssert.IsTrue(store != null);
        }

        [Test]
        public void CanCreateOutOfCoreStore()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            try
            {
                using var store = PointCloud.OpenStore(storepath, cache: default);
                ClassicAssert.IsTrue(store != null);
            }
            finally
            {
                File.Delete(storepath);
            }
        }

        [Test]
        public void CanCreateOutOfCoreFolderStore()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            if (!Directory.Exists(storepath)) { Directory.CreateDirectory(storepath); }
            var store = PointCloud.OpenStore(storepath, cache: default);
            ClassicAssert.IsTrue(store != null);
        }

        [Test]
        public void CanCreateOutOfCoreStore_FailsIfPathIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var storepath = (string)null;
                var store = PointCloud.OpenStore(storepath, cache: default);
                ClassicAssert.IsTrue(store != null);
            });
        }

        [Test]
        public void CanCreateOutOfCoreStore_FailsIfInvalidPath()
        {
            Assert.That(() =>
            {
                var storepath = @"some invalid path C:\";
                var store = PointCloud.OpenStore(storepath, cache: default);
                ClassicAssert.IsTrue(store != null);
            },
            Throws.Exception
            );
        }

        [Test]
        public void CanImportFile_WithoutConfig_InMemory()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            TestContext.WriteLine($"testfile is '{filename}'");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            var a = PointCloud.Import(filename);
            ClassicAssert.IsTrue(a != null);
            ClassicAssert.IsTrue(a.PointCount == 3);
        }

        [Test]
        public void CanImportFile_WithoutConfig_OutOfCore()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            TestContext.WriteLine($"storepath is '{storepath}'");
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var a = PointCloud.Import(filename, storepath, cache: default);
            ClassicAssert.IsTrue(a != null);
            ClassicAssert.IsTrue(a.PointCount == 3);
        }

        [Test]
        public void CanImportFileAndLoadFromStore()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            TestContext.WriteLine($"storepath is '{storepath}'");
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var a = PointCloud.Import(filename, storepath, cache: default);
            var key = a.Id;

            var b = PointCloud.Load(key, storepath, cache: default);
            ClassicAssert.IsTrue(b != null);
            ClassicAssert.IsTrue(b.PointCount == 3);
        }

        #endregion

        #region Pts

        [Test]
        public void CanParsePtsFileInfo()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var info = PointCloud.ParseFileInfo(filename, ImportConfig.Default.ParseConfig);

            ClassicAssert.IsTrue(info.PointCount == 3);
            ClassicAssert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParsePtsFile()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            var ps = PointCloud.Parse(filename, ImportConfig.Default.ParseConfig)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            ClassicAssert.IsTrue(ps.Length == 3);
            ClassicAssert.IsTrue(ps[0].ApproximateEquals(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        public void CanImportPtsFile()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledProperties(EnabledProperties.All.WithPartIndices(false))
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsFalse(pointset.HasPartIndices);
            ClassicAssert.IsFalse(pointset.HasPartIndexRange);
            ClassicAssert.IsNull(pointset.PartIndexRange);
        }

        [Test]
        public void CanImportPtsFile_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithEnabledPartIndices(true)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndices == true);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndices == true);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndices is int x && x == 42);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportPtsFile_PartIndex_None()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.PartIndexRange == null);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndices == null);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndexRange == null);
        }

        [Test]
        public void CanImportPtsFile_MinDist()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                .WithMinDist(10.0);
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.PointCount < 3);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportPtsFile_MinDist_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithMinDist(10.0);
            ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.PointCount < 3);
            ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore_CheckKey()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.Id == "test");
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore_CheckKey_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.Id == "test");
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanParsePtsChunksThenImportThenLoadFromStore()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var ptsChunks = Data.Points.Import.Pts.Chunks(filename, config.ParseConfig);
            var pointset = PointCloud.Chunks(ptsChunks, config);
            ClassicAssert.IsTrue(pointset.Id == "test");
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.HasPartIndexRange == false);
        }

        [Test]
        public void CanParsePtsChunksThenImportThenLoadFromStore_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            var ptsChunks = Data.Points.Import.Pts.Chunks(filename, config.ParseConfig);
            var pointset = PointCloud.Chunks(ptsChunks, config);
            ClassicAssert.IsTrue(pointset.Id == "test");
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
        }

        #endregion

        #region E57

        [Test]
        public void CanParseE57FileInfo()
        {
            ClassicAssert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var info = PointCloud.ParseFileInfo(filename, ParseConfig.Default);

            ClassicAssert.IsTrue(info.PointCount == 3);
            ClassicAssert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParseE57File()
        {
            ClassicAssert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var ps = PointCloud.Parse(filename, ParseConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            ClassicAssert.IsTrue(ps.Length == 3);
            ClassicAssert.IsTrue(ps[0].ApproximateEquals(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        public void CanImportE57File()
        {
            ClassicAssert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportE57File_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithEnabledPartIndices(true)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndices == true);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndices == true);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndices is int x && x == 42);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportE57File_PartIndex_None()
        {
            ClassicAssert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.PartIndexRange == null);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndices == null);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndexRange == null);
        }

        [Test]
        public void CanImportE57File_MinDist()
        {
            ClassicAssert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.PointCount < 3);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportE57File_MinDist_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.PointCount < 3);
            ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportE57FileAndLoadFromStore()
        {
            ClassicAssert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        [Test]
        public void CanImportE57FileAndLoadFromStore_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
            ClassicAssert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        #endregion

        #region LAS

        [Test]
        public void CanParseLasFileInfo()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var info = PointCloud.ParseFileInfo(filename, ParseConfig.Default);

            ClassicAssert.IsTrue(info.PointCount == 3);
            ClassicAssert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParseLasFile()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var ps = PointCloud.Parse(filename, ParseConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            ClassicAssert.IsTrue(ps.Length == 3);
            ClassicAssert.IsTrue(ps[0].ApproximateEquals(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        public void CanImportLasFile()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportLasFile_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithEnabledPartIndices(true)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndices == true);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndices == true);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndices is int x && x == 42);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportLasFile_PartIndex_None()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.PartIndexRange == null);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndices == null);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndexRange == null);
        }

        [Test]
        public void CanImportLasFile_MinDist()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.PointCount < 3);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportLasFile_MinDist_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.PointCount < 3);
            ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportLasFileAndLoadFromStore()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        [Test]
        public void CanImportLasFileAndLoadFromStore_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
            ClassicAssert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        #endregion

        #region LAZ

        [Test]
        public void CanParseLazFileInfo()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var info = PointCloud.ParseFileInfo(filename, ParseConfig.Default);

            ClassicAssert.IsTrue(info.PointCount == 3);
            ClassicAssert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParseLazFile()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var ps = PointCloud.Parse(filename, ParseConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            ClassicAssert.IsTrue(ps.Length == 3);
            ClassicAssert.IsTrue(ps[0].ApproximateEquals(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        public void CanImportLazFile()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportLazFile_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithEnabledPartIndices(true)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndices == true);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndices == true);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndices is int x && x == 42);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportLazFile_PartIndex_None()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 3);
            ClassicAssert.IsTrue(pointset.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.PartIndexRange == null);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndices == null);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndexRange == null);
        }

        [Test]
        public void CanImportLazFile_MinDist()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.PointCount < 3);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportLazFile_MinDist_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.PointCount < 3);
            ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportLazFileAndLoadFromStore()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        [Test]
        public void CanImportLazFileAndLoadFromStore_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 3);
            ClassicAssert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
            ClassicAssert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        #endregion

        #region PLY

        [Test]
        public void CanParsePlyFileInfo()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var info = PointCloud.ParseFileInfo(filename, ParseConfig.Default);

            ClassicAssert.IsTrue(info.PointCount == 8);
            ClassicAssert.IsTrue(info.Bounds == Box3d.Invalid);
        }

        [Test]
        public void CanParsePlyFile()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var ps = PointCloud.Parse(filename, ParseConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            ClassicAssert.IsTrue(ps.Length == 8);
            ClassicAssert.IsTrue(ps[0] == V3d.OOO);
        }

        [Test]
        public void CanImportPlyFile()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 8);
            ClassicAssert.IsTrue(pointset.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportPlyFile_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithEnabledPartIndices(true)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 8);
            ClassicAssert.IsTrue(pointset.HasPartIndices == true);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndices == true);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndices is int x && x == 42);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndexRange == true);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportPlyFile_PartIndex_None()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset != null);
            ClassicAssert.IsTrue(pointset.PointCount == 8);
            ClassicAssert.IsTrue(pointset.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.PartIndexRange == null);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndices == false);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndices == null);
            ClassicAssert.IsTrue(pointset.Root.Value.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset.Root.Value.PartIndexRange == null);
        }

        [Test]
        public void CanImportPlyFile_MinDist()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.PointCount < 8);
            ClassicAssert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportPlyFile_MinDist_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            ClassicAssert.IsTrue(pointset.PointCount < 8);
            ClassicAssert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportPlyFileAndLoadFromStore()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 8);
            ClassicAssert.IsTrue(pointset2.HasPartIndexRange == false);
            ClassicAssert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        [Test]
        public void CanImportPlyFileAndLoadFromStore_PartIndex()
        {
            ClassicAssert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            ClassicAssert.IsTrue(pointset2 != null);
            ClassicAssert.IsTrue(pointset2.PointCount == 8);
            ClassicAssert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
            ClassicAssert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        #endregion
    }
}
