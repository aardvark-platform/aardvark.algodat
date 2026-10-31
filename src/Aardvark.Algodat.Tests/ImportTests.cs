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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Ply.Net;
using PlyImport = Aardvark.Data.Points.Import.Ply;
using PlyType = Ply.Net.PlyParser.DataType;

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

        private static readonly PlyType[] RescaledPlyIntensityTypes = { PlyType.UInt32, PlyType.Int64, PlyType.UInt64, PlyType.Float32, PlyType.Float64 };
        private static readonly PlyType[] NarrowPlyIntensityTypes = { PlyType.Int8, PlyType.UInt8, PlyType.Int16, PlyType.UInt16, PlyType.Int32 };

        [Test]
        public void EmptyPlyAsciiStreamsKeepRescaledIntensityAndOptionalAttributeShape()
        {
            foreach (var type in RescaledPlyIntensityTypes)
            foreach (var name in new[] { "intensity", "scalar_intensity" })
            foreach (var newline in new[] { "\n", "\r\n" })
            foreach (var attributes in new[] { false, true })
            foreach (var parts in new[] { false, true })
            {
                var data = PlyIntensityDataset(type, name, PlyIntensityValues(type, Array.Empty<double>()), attributes);
                CheckPlyIntensityConversion(data, Array.Empty<int>(), attributes, PlyIntensityConfig(parts), newline, $"empty ASCII type={type}, name={name}, CRLF={newline.Length == 2}, attrs={attributes}, parts={parts}");
            }
        }

        [Test]
        public void EmptyPlyDatasetsKeepRescaledIntensityBatchesAndPropertyPrecedence()
        {
            foreach (var type in RescaledPlyIntensityTypes)
            foreach (var name in new[] { "intensity", "scalar_intensity" })
            foreach (var attributes in new[] { false, true })
            foreach (var parts in new[] { false, true })
            {
                var empty = PlyIntensityDataset(type, name, PlyIntensityValues(type, Array.Empty<double>()), attributes);
                var config = PlyIntensityConfig(parts); var context = $"empty dataset type={type}, name={name}, attrs={attributes}, parts={parts}";
                CheckPlyIntensityConversion(empty, Array.Empty<int>(), attributes, config, null, context);
                CheckPlyIntensityChunk(PlyImport.Chunks(empty).Single(), Array.Empty<int>(), attributes, ParseConfig.Default, 0, context + " default forwarding");
                var full = PlyIntensityDataset(type, name, PlyIntensityValues(type, new double[] { 1024, 2048, 3072 }), attributes);
                var face = new PlyParser.ElementData(new PlyParser.Element(PlyParser.ElementType.Face, "face", 0, ImmutableList<PlyParser.Property>.Empty), ImmutableList<PlyParser.PropertyData>.Empty);
                var mixed = new PlyParser.Dataset(full.Header, new[] { face, empty.Data.Single(), full.Data.Single(), empty.Data.Single(), face });
                var chunks = PlyImport.Chunks(mixed, config).ToArray();
                Assert.That(chunks.Length, Is.EqualTo(3), context + " retain both empty batches and skip faces");
                CheckPlyIntensityChunk(chunks[0], Array.Empty<int>(), attributes, config, 0, context);
                CheckPlyIntensityChunk(chunks[1], new[] { 0, 127, 255 }, attributes, config, 3, context);
                CheckPlyIntensityChunk(chunks[2], Array.Empty<int>(), attributes, config, 0, context);
            }
            foreach (var type in RescaledPlyIntensityTypes)
            foreach (var reverse in new[] { false, true })
            {
                var data = PlyIntensityDataset(type, "scalar_intensity", PlyIntensityValues(type, Array.Empty<double>()), true, bothNames: true);
                if (reverse) data = data with { Data = data.Data.Select(d => d with { Data = d.Data.Reverse().ToImmutableList() }).ToArray() };
                CheckPlyIntensityConversion(data, Array.Empty<int>(), true, PlyIntensityConfig(true), null, $"empty scalar precedence type={type}, reverse={reverse}");
            }
        }

        [Test]
        public void EmptyPlyNarrowIntegerAndMissingIntensityControlsKeepExistingOutput()
        {
            foreach (var type in NarrowPlyIntensityTypes.Append(PlyType.Undefined))
            foreach (var name in type == PlyType.Undefined ? new string[] { null } : new[] { "intensity", "scalar_intensity" })
            foreach (var attributes in new[] { false, true })
            foreach (var parts in new[] { false, true })
            foreach (var newline in new string[] { null, "\n", "\r\n" })
            {
                var data = PlyIntensityDataset(type, name, type == PlyType.Undefined ? Array.Empty<int>() : PlyIntensityValues(type, Array.Empty<double>()), attributes);
                CheckPlyIntensityConversion(data, name == null ? null : Array.Empty<int>(), attributes, PlyIntensityConfig(parts), newline, $"empty control type={type}, name={name}, attrs={attributes}, parts={parts}, newline={newline?.Length}");
            }
        }

        [Test]
        public void NonemptyPlyIntensitiesKeepPassthroughConstantsRangeScalingAndScalarPrecedence()
        {
            foreach (var item in PlyIntensityControlCases())
            foreach (var name in new[] { "intensity", "scalar_intensity" })
            foreach (var parts in new[] { false, true })
            foreach (var newline in new string[] { null, "\n", "\r\n" })
            {
                var data = PlyIntensityDataset(item.Type, name, item.Values, true);
                CheckPlyIntensityConversion(data, item.Expected, true, PlyIntensityConfig(parts), newline, $"nonempty {item.Scenario}, type={item.Type}, name={name}, parts={parts}, newline={newline?.Length}");
            }
            foreach (var type in RescaledPlyIntensityTypes)
            foreach (var reverse in new[] { false, true })
            foreach (var newline in new string[] { null, "\n", "\r\n" })
            {
                var data = PlyIntensityDataset(type, "scalar_intensity", PlyIntensityValues(type, new double[] { 1024, 2048, 3072 }), true, bothNames: true);
                if (reverse) data = data with { Data = data.Data.Select(d => d with { Data = d.Data.Reverse().ToImmutableList() }).ToArray() };
                CheckPlyIntensityConversion(data, new[] { 0, 127, 255 }, true, PlyIntensityConfig(true), newline, $"nonempty scalar precedence type={type}, reverse={reverse}, newline={newline?.Length}");
            }
        }

        private static ParseConfig PlyIntensityConfig(bool parts) => ParseConfig.Default.WithEnabledPartIndices(parts).WithPartIndexOffset(31).WithMaxChunkPointCount(1);

        private static Array PlyIntensityValues(PlyType type, double[] values) => type switch {
            PlyType.Int8 => values.Select(v => (sbyte)v).ToArray(), PlyType.UInt8 => values.Select(v => (byte)v).ToArray(),
            PlyType.Int16 => values.Select(v => (short)v).ToArray(), PlyType.UInt16 => values.Select(v => (ushort)v).ToArray(),
            PlyType.Int32 => values.Select(v => (int)v).ToArray(), PlyType.UInt32 => values.Select(v => (uint)v).ToArray(),
            PlyType.Int64 => values.Select(v => (long)v).ToArray(), PlyType.UInt64 => values.Select(v => (ulong)v).ToArray(),
            PlyType.Float32 => values.Select(v => (float)v).ToArray(), PlyType.Float64 => values.ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        private static IEnumerable<(PlyType Type, Array Values, int[] Expected, string Scenario)> PlyIntensityControlCases()
        {
            foreach (var type in RescaledPlyIntensityTypes)
            {
                yield return (type, PlyIntensityValues(type, new double[] { 0, 127, 255 }), new[] { 0, 127, 255 }, "passthrough");
                yield return (type, PlyIntensityValues(type, new double[] { 42, 42, 42 }), new[] { 42, 42, 42 }, "constant-in-range");
                yield return (type, PlyIntensityValues(type, new double[] { 1024, 1024, 1024 }), new[] { 255, 255, 255 }, "constant-out-of-range");
                yield return (type, PlyIntensityValues(type, new double[] { 1024, 2048, 3072 }), new[] { 0, 127, 255 }, "range");
                if (type == PlyType.Float32 || type == PlyType.Float64)
                    yield return (type, PlyIntensityValues(type, new double[] { 0.25, 127.75, 254.5 }), new[] { 0, 127, 254 }, "fractional truncation");
                if (type == PlyType.Int64 || type == PlyType.Float32 || type == PlyType.Float64)
                {
                    yield return (type, PlyIntensityValues(type, new double[] { -1024, 0, 1024 }), new[] { 0, 127, 255 }, "signed range");
                    yield return (type, PlyIntensityValues(type, new double[] { -4, -4, -4 }), new[] { 255, 255, 255 }, "negative constant");
                }
            }
            yield return (PlyType.Int8, new sbyte[] { sbyte.MinValue, 0, sbyte.MaxValue }, new[] { -128, 0, 127 }, "narrow signed passthrough");
            yield return (PlyType.UInt8, new byte[] { 0, 127, 255 }, new[] { 0, 127, 255 }, "narrow byte passthrough");
            yield return (PlyType.Int16, new short[] { short.MinValue, 0, short.MaxValue }, new[] { -32768, 0, 32767 }, "narrow signed passthrough");
            yield return (PlyType.UInt16, new ushort[] { 0, 255, ushort.MaxValue }, new[] { 0, 255, 65535 }, "narrow unsigned passthrough");
            yield return (PlyType.Int32, new int[] { int.MinValue, -17, int.MaxValue }, new[] { int.MinValue, -17, int.MaxValue }, "narrow signed passthrough");
        }

        private static PlyParser.Dataset PlyIntensityDataset(PlyType type, string name, Array values, bool attributes, bool bothNames = false)
        {
            var count = values.Length; var indices = Enumerable.Range(0, count).ToArray(); var data = new List<PlyParser.PropertyData>();
            void Add(PlyType t, string n, Array a) => data.Add(new PlyParser.PropertyData(new PlyParser.Property(t, n, PlyType.Undefined), a));
            Add(PlyType.Float64, "x", indices.Select(i => i + 0.25).ToArray());
            Add(PlyType.Float64, "y", indices.Select(i => -i - 0.5).ToArray());
            Add(PlyType.Float64, "z", indices.Select(i => i * 0.125).ToArray());
            if (name != null) Add(type, name, values);
            if (bothNames) Add(PlyType.Int32, "intensity", indices.Select(i => 17 + i).ToArray());
            if (attributes)
            {
                Add(PlyType.Float32, "red", indices.Select(_ => 0.25f).ToArray());
                Add(PlyType.UInt8, "green", indices.Select(i => (byte)(17 + i)).ToArray());
                Add(PlyType.UInt8, "blue", indices.Select(i => (byte)(255 - i)).ToArray());
                Add(PlyType.Float32, "nx", indices.Select(_ => 1.0f).ToArray());
                Add(PlyType.Float32, "ny", indices.Select(_ => -2.0f).ToArray());
                Add(PlyType.Float32, "nz", indices.Select(_ => 0.5f).ToArray());
                Add(PlyType.UInt8, "classification", indices.Select(i => (byte)(90 + i)).ToArray());
                Add(PlyType.UInt8, "scalar_classification", indices.Select(i => (byte)(7 + i)).ToArray());
            }
            var element = new PlyParser.Element(PlyParser.ElementType.Vertex, "vertex", count, data.Select(d => d.Property).ToImmutableList());
            var header = new PlyParser.Header(PlyParser.Format.Ascii, ImmutableList.Create(element), ImmutableList<string>.Empty, 0);
            return new PlyParser.Dataset(header, new[] { new PlyParser.ElementData(element, data.ToImmutableList()) });
        }

        private static byte[] PlyIntensityAscii(PlyParser.Dataset data, string newline)
        {
            var element = data.Data.Single(); var text = new StringBuilder("ply" + newline + "format ascii 1.0" + newline + "element vertex " + element.Element.Count + newline);
            foreach (var property in element.Data) text.Append("property ").Append(property.Property.DataType.ToString().ToLowerInvariant()).Append(' ').Append(property.Property.Name).Append(newline);
            text.Append("end_header").Append(newline);
            for (var i = 0; i < element.Element.Count; i++)
                text.Append(string.Join(" ", element.Data.Select(d => Convert.ToString(d.Data.GetValue(i), CultureInfo.InvariantCulture)))).Append(newline);
            return Encoding.ASCII.GetBytes(text.ToString());
        }

        private static void CheckPlyIntensityConversion(PlyParser.Dataset data, int[] expected, bool attributes, ParseConfig config, string newline, string context)
        {
            var source = data.Data.Single().Data; var snapshots = source.Select(p => (Array)p.Data.Clone()).ToArray(); Chunk[] chunks;
            if (newline == null) chunks = PlyImport.Chunks(data, config).ToArray();
            else
            {
                using var stream = new MemoryStream(PlyIntensityAscii(data, newline));
                chunks = PlyImport.Chunks(stream, stream.Length, config).ToArray();
                Assert.That(stream.CanRead, Is.True, context + " caller retains stream ownership");
            }
            Assert.That(chunks.Length, Is.EqualTo(1), context + " retain a chunk even for zero vertices");
            CheckPlyIntensityChunk(chunks[0], expected, attributes, config, source[0].Data.Length, context);
            Assert.That(data.Data.Single().Data, Is.SameAs(source), context + " source property list");
            for (var i = 0; i < source.Count; i++) Assert.That(source[i].Data, Is.EqualTo(snapshots[i]), context + $" source property={source[i].Property.Name}");
        }

        private static void CheckPlyIntensityChunk(Chunk chunk, int[] expected, bool attributes, ParseConfig config, int count, string context)
        {
            Assert.That(chunk.Count, Is.EqualTo(count), context);
            var positions = Enumerable.Range(0, count).Select(i => new V3d(i + 0.25, -i - 0.5, i * 0.125)).ToArray();
            foreach (var p in positions) Assert.That(p.IsNaN || p.IsInfinity, Is.False, context + " finite expected position");
            Assert.That(chunk.Positions, Is.EqualTo(positions), context + " positions");
            Assert.That(chunk.BoundingBox, Is.EqualTo(count == 0 ? Box3d.Invalid : new Box3d(positions)), context + " bounds");
            Assert.That(chunk.HasIntensities, Is.EqualTo(expected != null), context + " intensity presence");
            if (expected == null) Assert.That(chunk.Intensities, Is.Null, context);
            else { Assert.That(chunk.Intensities, Is.TypeOf<int[]>(), context); Assert.That(chunk.Intensities, Is.EqualTo(expected), context + " intensity values"); }
            Assert.That(chunk.HasColors, Is.EqualTo(attributes), context); Assert.That(chunk.HasNormals, Is.EqualTo(attributes), context); Assert.That(chunk.HasClassifications, Is.EqualTo(attributes), context);
            if (attributes)
            {
                Assert.That(chunk.Colors, Is.EqualTo(Enumerable.Range(0, count).Select(i => new C4b((byte)63, (byte)(17 + i), (byte)(255 - i), (byte)255))), context + " colors/default alpha");
                Assert.That(chunk.Normals, Is.EqualTo(Enumerable.Range(0, count).Select(_ => new V3f(1, -2, 0.5f))), context + " normals");
                Assert.That(chunk.Classifications, Is.EqualTo(Enumerable.Range(0, count).Select(i => (byte)(7 + i))), context + " classification precedence");
            }
            else { Assert.That(chunk.Colors, Is.Null, context); Assert.That(chunk.Normals, Is.Null, context); Assert.That(chunk.Classifications, Is.Null, context); }
            Assert.That(chunk.HasPartIndices, Is.EqualTo(config.EnabledProperties.PartIndices), context);
            Assert.That(chunk.PartIndices, Is.EqualTo(config.EnabledProperties.PartIndices ? (object)config.PartIndexOffset : null), context + " part label");
            Assert.That(chunk.PartIndexRange, Is.EqualTo(config.EnabledProperties.PartIndices ? (Range1i?)new Range1i(config.PartIndexOffset, config.PartIndexOffset) : null), context + " part range");
        }

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
