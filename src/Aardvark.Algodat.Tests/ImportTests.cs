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
            Assert.IsTrue(format != null);
            Assert.IsTrue(format.Description == "Test Description 2");
        }

        [Test]
        public void CanRetrieveFileFormat2()
        {
            PointCloudFileFormat.Register(new PointCloudFileFormat("Test Description 3", new[] { ".test3", ".tst3" }, null, null));

            var format1 = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.test3");
            var format2 = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.tst3");
            Assert.IsTrue(format1 != null && format1.Description == "Test Description 3");
            Assert.IsTrue(format2 != null && format2.Description == "Test Description 3");
        }

        [Test]
        public void UnknownFileFormatGivesUnknown()
        {
            var format = PointCloudFileFormat.FromFileName(@"C:\Data\pointcloud.foo");
            Assert.IsTrue(format == PointCloudFileFormat.Unknown);
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

            Assert.IsTrue(chunk.Count == 100);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud.PointCount == 100);
        }

        [Test]
        public void CanImportChunk_MinDist()
        {
            int n = 100;
            var r = new Random();
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
            var chunk = new Chunk(ps, null, null, null, null, null, null, null);

            Assert.IsTrue(chunk.Count == 100);

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
            Assert.IsTrue(pointcloud.PointCount < 100);
        }

        [Test]
        public void CanImportChunk_Reproject()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);
            var bb = new Box3d(ps);

            var chunk = new Chunk(ps, null, null, null, null, null, null, null);
            Assert.IsTrue(chunk.Count == 10);
            Assert.IsTrue(chunk.BoundingBox == bb);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithReproject(xs => xs.Select(x => x += V3d.OIO).ToArray())
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud.BoundingBox == bb + V3d.OIO);
        }

        [Test]
        public void CanImport_WithKey()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps, null, null, null, null, null, null, null);
            Assert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.0)
                .WithReproject(null)
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud.Id == "test");
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
            Assert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(store)
                .WithKey(key)
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.0)
                .WithReproject(null)
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud.Id == key);


            var reloaded = store.GetPointSet(key);
            Assert.IsTrue(reloaded.Id == key);
        }

        [Test]
        public void CanImport_WithoutKey()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps, null, null, null, null, null, null, null);
            Assert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey(null)
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.0)
                .WithReproject(null)
                ;
            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);
            var pointcloud = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud.Id != null);
        }

        [Test]
        public void CanImport_DuplicateKey()
        {
            int n = 10;
            var ps = new V3d[n];
            for (var i = 0; i < n; i++) ps[i] = new V3d(i, 0, 0);

            var chunk = new Chunk(ps, null, null, null, null, null, null, null);
            Assert.IsTrue(chunk.Count == 10);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithOctreeSplitLimit(10)
                .WithMinDist(0.0)
                .WithReproject(null)
                ;

            if (config.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);

            var pointcloud = PointCloud.Chunks(Array.Empty<Chunk>(), config);
            Assert.IsTrue(pointcloud.Id != null);
            Assert.IsTrue(pointcloud.PointCount == 0);


            var pointcloud2 = PointCloud.Chunks(chunk, config);
            Assert.IsTrue(pointcloud2.Id != null);
            Assert.IsTrue(pointcloud2.PointCount == 10);


            var reloaded = config.Storage.GetPointSet("test");
            Assert.IsTrue(reloaded.PointCount == 10);
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
            Assert.IsTrue(pointcloud.Root != null);
            Assert.IsTrue(pointcloud.Root.Id == Guid.Empty.ToString());
            Assert.IsTrue(pointcloud.Root.Value.IsEmpty);
            Assert.IsTrue(pointcloud.Id == "test");
            Assert.IsTrue(pointcloud.PointCount == 0);
            
            var reloaded = config.Storage.GetPointSet("test");
            Assert.IsTrue(reloaded.Id == "test");
            Assert.IsTrue(reloaded.PointCount == 0);
            Assert.IsFalse(reloaded.HasPartIndices);
            Assert.IsFalse(reloaded.HasPartIndexRange);
            Assert.IsNull(reloaded.PartIndexRange);
        }

        #endregion

        #region General

        [Test]
        public void CanCreateInMemoryStore()
        {
            using var store = PointCloud.CreateInMemoryStore(cache: default);
            Assert.IsTrue(store != null);
        }

        [Test]
        public void CanCreateOutOfCoreStore()
        {
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            try
            {
                using var store = PointCloud.OpenStore(storepath, cache: default);
                Assert.IsTrue(store != null);
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
            Assert.IsTrue(store != null);
        }

        [Test]
        public void CanCreateOutOfCoreStore_FailsIfPathIsNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var storepath = (string)null;
                var store = PointCloud.OpenStore(storepath, cache: default);
                Assert.IsTrue(store != null);
            });
        }

        [Test]
        public void CanCreateOutOfCoreStore_FailsIfInvalidPath()
        {
            Assert.That(() =>
            {
                var storepath = @"some invalid path C:\";
                var store = PointCloud.OpenStore(storepath, cache: default);
                Assert.IsTrue(store != null);
            },
            Throws.Exception
            );
        }

        [Test]
        public void CanImportFile_WithoutConfig_InMemory()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
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
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            TestContext.WriteLine($"storepath is '{storepath}'");
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var a = PointCloud.Import(filename, storepath, cache: default);
            Assert.IsTrue(a != null);
            Assert.IsTrue(a.PointCount == 3);
        }

        [Test]
        public void CanImportFileAndLoadFromStore()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var storepath = Path.Combine(Config.TempDataDir, Guid.NewGuid().ToString());
            TestContext.WriteLine($"storepath is '{storepath}'");
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var a = PointCloud.Import(filename, storepath, cache: default);
            var key = a.Id;

            var b = PointCloud.Load(key, storepath, cache: default);
            Assert.IsTrue(b != null);
            Assert.IsTrue(b.PointCount == 3);
        }

        #endregion

        #region Pts

        [Test]
        public void CanParsePtsFileInfo()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var info = PointCloud.ParseFileInfo(filename, ImportConfig.Default.ParseConfig);

            Assert.IsTrue(info.PointCount == 3);
            Assert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParsePtsFile()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            var ps = PointCloud.Parse(filename, ImportConfig.Default.ParseConfig)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(ps.Length == 3);
            Assert.IsTrue(ps[0].ApproximateEquals(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        public void CanImportPtsFile()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledProperties(EnabledProperties.All.WithPartIndices(false))
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
            Assert.IsFalse(pointset.HasPartIndices);
            Assert.IsFalse(pointset.HasPartIndexRange);
            Assert.IsNull(pointset.PartIndexRange);
        }

        [Test]
        public void CanImportPtsFile_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
            Assert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportPtsFile_MinDist()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
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
            Assert.IsTrue(pointset.PointCount < 3);
            Assert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportPtsFile_MinDist_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
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
            Assert.IsTrue(pointset.PointCount < 3);
            Assert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
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
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
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
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore_CheckKey()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.Id == "test");
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportPtsFileAndLoadFromStore_CheckKey_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.pts");
            if (!File.Exists(filename)) Assert.Ignore($"File not found: {filename}");
            TestContext.WriteLine($"testfile is '{filename}'");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.Id == "test");
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanParsePtsChunksThenImportThenLoadFromStore()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
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
            Assert.IsTrue(pointset.Id == "test");
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.HasPartIndexRange == false);
        }

        [Test]
        public void CanParsePtsChunksThenImportThenLoadFromStore_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Pts.PtsFormat != null);
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
            Assert.IsTrue(pointset.Id == "test");
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
        }

        #endregion

        #region E57

        [Test]
        public void CanParseE57FileInfo()
        {
            Assert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var info = PointCloud.ParseFileInfo(filename, ParseConfig.Default);

            Assert.IsTrue(info.PointCount == 3);
            Assert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParseE57File()
        {
            Assert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var ps = PointCloud.Parse(filename, ParseConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(ps.Length == 3);
            Assert.IsTrue(ps[0].ApproximateEquals(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        public void CanImportE57File()
        {
            Assert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
            Assert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportE57File_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
            Assert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportE57File_MinDist()
        {
            Assert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.PointCount < 3);
            Assert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportE57File_MinDist_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.PointCount < 3);
            Assert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportE57FileAndLoadFromStore()
        {
            Assert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.HasPartIndexRange == false);
            Assert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        [Test]
        public void CanImportE57FileAndLoadFromStore_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.E57.E57Format != null);
            var filename = Path.Combine(Config.TestDataDir, "test.e57");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
            Assert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        #endregion

        #region LAS

        [Test]
        public void CanParseLasFileInfo()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var info = PointCloud.ParseFileInfo(filename, ParseConfig.Default);

            Assert.IsTrue(info.PointCount == 3);
            Assert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParseLasFile()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var ps = PointCloud.Parse(filename, ParseConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(ps.Length == 3);
            Assert.IsTrue(ps[0].ApproximateEquals(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        public void CanImportLasFile()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
            Assert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportLasFile_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
            Assert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportLasFile_MinDist()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.PointCount < 3);
            Assert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportLasFile_MinDist_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.PointCount < 3);
            Assert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportLasFileAndLoadFromStore()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.HasPartIndexRange == false);
            Assert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        [Test]
        public void CanImportLasFileAndLoadFromStore_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.las");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
            Assert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        #endregion

        #region LAZ

        [Test]
        public void CanParseLazFileInfo()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var info = PointCloud.ParseFileInfo(filename, ParseConfig.Default);

            Assert.IsTrue(info.PointCount == 3);
            Assert.IsTrue(info.Bounds == new Box3d(new V3d(1), new V3d(9)));
        }

        [Test]
        public void CanParseLazFile()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var ps = PointCloud.Parse(filename, ParseConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(ps.Length == 3);
            Assert.IsTrue(ps[0].ApproximateEquals(new V3d(1, 2, 9), 1e-10));
        }

        [Test]
        public void CanImportLazFile()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
            Assert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportLazFile_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 3);
            Assert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportLazFile_MinDist()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.PointCount < 3);
            Assert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportLazFile_MinDist_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.PointCount < 3);
            Assert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportLazFileAndLoadFromStore()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.HasPartIndexRange == false);
            Assert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        [Test]
        public void CanImportLazFileAndLoadFromStore_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Laszip.LaszipFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "test.laz");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 3);
            Assert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
            Assert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        #endregion

        #region PLY

        [Test]
        public void CanParsePlyFileInfo()
        {
            Assert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var info = PointCloud.ParseFileInfo(filename, ParseConfig.Default);

            Assert.IsTrue(info.PointCount == 8);
            Assert.IsTrue(info.Bounds == Box3d.Invalid);
        }

        [Test]
        public void CanParsePlyFile()
        {
            Assert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var ps = PointCloud.Parse(filename, ParseConfig.Default)
                .SelectMany(x => x.Positions)
                .ToArray()
                ;
            Assert.IsTrue(ps.Length == 8);
            Assert.IsTrue(ps[0] == V3d.OOO);
        }

        [Test]
        public void CanImportPlyFile()
        {
            Assert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 8);
            Assert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportPlyFile_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset != null);
            Assert.IsTrue(pointset.PointCount == 8);
            Assert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportPlyFile_MinDist()
        {
            Assert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.PointCount < 8);
            Assert.IsTrue(pointset.HasPartIndexRange == false);
        }

        [Test]
        public void CanImportPlyFile_MinDist_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                .WithMinDist(10)
                ;
            var pointset = PointCloud.Import(filename, config);
            Assert.IsTrue(pointset.PointCount < 8);
            Assert.IsTrue(pointset.PartIndexRange == new Range1i(42, 42));
        }

        [Test]
        public void CanImportPlyFileAndLoadFromStore()
        {
            Assert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithEnabledPartIndices(false)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 8);
            Assert.IsTrue(pointset2.HasPartIndexRange == false);
            Assert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        [Test]
        public void CanImportPlyFileAndLoadFromStore_PartIndex()
        {
            Assert.IsTrue(Data.Points.Import.Ply.PlyFormat != null);
            var filename = Path.Combine(Config.TestDataDir, "cube.ply");
            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: default))
                .WithKey("test")
                .WithPartIndexOffset(42)
                ;
            _ = PointCloud.Import(filename, config);
            var pointset2 = config.Storage.GetPointSet("test");
            Assert.IsTrue(pointset2 != null);
            Assert.IsTrue(pointset2.PointCount == 8);
            Assert.IsTrue(pointset2.PartIndexRange == new Range1i(42, 42));
            Assert.IsTrue(pointset2.Root.Value.KdTree.Value != null);
        }

        #endregion
    }
}
