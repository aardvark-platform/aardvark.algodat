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
using System.Collections.Generic;
using System.IO;
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class StoreTests
    {
        private static readonly byte[] Seed = { 1, 2, 3 };
        private static readonly byte[] Written = { 9, 7, 5, 3 };

        private sealed class NativeStore : IDisposable
        {
            private readonly string m_directory = Path.Combine(Path.GetTempPath(), "algodat-store-mode-" + Guid.NewGuid());
            private readonly HashSet<Storage> m_handles = new();
            public string PathName { get; }

            public NativeStore(bool seed = true)
            {
                Directory.CreateDirectory(m_directory);
                PathName = Path.Combine(m_directory, "native.store");
                if (seed)
                {
                    using var store = new SimpleDiskStore(PathName, _ => { });
                    store.Add("seed", Seed);
                    store.Flush();
                }
            }

            public Storage Track(Storage store)
            {
                m_handles.Add(store);
                return store;
            }

            public Storage Open(bool readOnly, LruDictionary<string, object> cache = null, Action<string[]> logger = null)
                => Track(PointCloud.OpenStore(PathName, cache, logger, readOnly));

            public void Dispose()
            {
                try
                {
                    foreach (var store in m_handles) if (!store.IsDisposed) store.Dispose();
                }
                finally { Directory.Delete(m_directory, recursive: true); }
            }
        }

        [Test]
        public void MixedModesKeepDistinctHandlesAndBackendPermissionsInBothOpeningOrders()
        {
            Assert.Multiple(() =>
            {
                foreach (var readOnlyFirst in new[] { false, true })
                foreach (var logged in new[] { false, true })
                foreach (var explicitCache in new[] { false, true })
                {
                    using var disk = new NativeStore();
                    var context = $"readOnlyFirst={readOnlyFirst}, logged={logged}, explicitCache={explicitCache}";
                    Action<string[]> logger = logged ? _ => { } : null;
                    var first = disk.Open(readOnlyFirst, explicitCache ? new LruDictionary<string, object>(4096) : null, logger);
                    var second = disk.Open(!readOnlyFirst, explicitCache ? new LruDictionary<string, object>(8192) : null, logger);
                    var reader = readOnlyFirst ? first : second;
                    var writer = readOnlyFirst ? second : first;
                    Assert.That(reader, Is.Not.SameAs(writer), context);
                    Assert.That(disk.Open(true), Is.SameAs(reader), context + ", reader reuse");
                    Assert.That(disk.Open(false), Is.SameAs(writer), context + ", writer reuse");
                    Assert.That(reader.GetByteArray("seed"), Is.EqualTo(Seed), context);
                    Assert.That(writer.GetByteArray("seed"), Is.EqualTo(Seed), context);
                    Assert.Throws<InvalidOperationException>(() => reader.Add("forbidden", Written), context + ", read-only add");
                    Assert.Throws<InvalidOperationException>(() => reader.Add("seed", Written), context + ", read-only overwrite");
                    Assert.Throws<InvalidOperationException>(() => reader.f_remove("seed"), context + ", read-only remove");
                    Assert.DoesNotThrow(() => writer.Add("accepted", Written), context + ", writable add");
                    Assert.DoesNotThrow(() => writer.Add("temporary", Seed), context);
                    Assert.DoesNotThrow(() => writer.f_remove("temporary"), context + ", writable remove");
                    Assert.DoesNotThrow(() => writer.Flush(), context);
                    Assert.That(writer.GetByteArray("accepted"), Is.EqualTo(Written), context);
                    Assert.That(writer.GetByteArray("temporary"), Is.Null, context);
                    Assert.That(reader.GetByteArray("seed"), Is.EqualTo(Seed), context + ", rejected mutation");
                    if (!ReferenceEquals(reader, writer))
                        Assert.That(reader.GetByteArray("accepted"), Is.Null, context + ", native snapshot remains unchanged");
                    first.Dispose();
                    if (!second.IsDisposed) second.Dispose();
                    var persisted = disk.Open(true);
                    Assert.That(persisted.GetByteArray("seed"), Is.EqualTo(Seed), context + ", persisted seed");
                    Assert.That(persisted.GetByteArray("accepted"), Is.EqualTo(Written), context + ", persisted writable data");
                    Assert.That(persisted.GetByteArray("forbidden"), Is.Null, context + ", rejected write absent");
                }
            });
        }

        [Test]
        public void Store_CanReopenDisposedStore()
        {
            foreach (var readOnly in new[] { false, true })
            foreach (var logged in new[] { false, true })
            Assert.Multiple(() =>
            {
                using var disk = new NativeStore(seed: readOnly);
                var context = $"readOnly={readOnly}, logged={logged}";
                var cache = new LruDictionary<string, object>(4096);
                var first = disk.Open(readOnly, cache, logged ? _ => { } : null);
                Assert.That(File.Exists(disk.PathName), Is.True, context + ", native disk backend");
                if (!readOnly)
                {
                    first.Add("seed", Seed);
                    first.Flush();
                }
                var ignoredCache = new LruDictionary<string, object>(8192);
                var duplicatePath = new string(disk.PathName.ToCharArray());
                var same = disk.Track(PointCloud.OpenStore(duplicatePath, ignoredCache, _ => throw new InvalidOperationException("cache hit must not open a backend"), readOnly));
                Assert.That(same, Is.SameAs(first), context);
                Assert.That(same.Cache, Is.SameAs(cache), context);
                Assert.That(same.Cache, Is.Not.SameAs(ignoredCache), context);
                first.Dispose();
                Assert.That(first.IsDisposed, Is.True, context);
                var reopened = disk.Open(readOnly, ignoredCache, logged ? _ => { } : null);
                Assert.That(reopened, Is.Not.SameAs(first), context);
                Assert.That(reopened.IsDisposed, Is.False, context);
                Assert.That(reopened.Cache, Is.SameAs(ignoredCache), context);
                Assert.That(reopened.GetByteArray("seed"), Is.EqualTo(Seed), context);
                Assert.That(disk.Open(readOnly), Is.SameAs(reopened), context);
                if (readOnly) Assert.Throws<InvalidOperationException>(() => reopened.Add("reopened", Written), context);
                else
                {
                    reopened.Add("reopened", Written);
                    reopened.Flush();
                    reopened.Dispose();
                    Assert.That(disk.Open(false).GetByteArray("reopened"), Is.EqualTo(Written), context);
                }
            });
        }

        [Test]
        public void DisposingEitherModeKeepsTheOtherLiveAndReopeningIndependent()
        {
            Assert.Multiple(() =>
            {
                foreach (var readOnlyFirst in new[] { false, true })
                foreach (var disposeReadOnly in new[] { false, true })
                {
                    using var disk = new NativeStore();
                    var context = $"readOnlyFirst={readOnlyFirst}, disposeReadOnly={disposeReadOnly}";
                    var first = disk.Open(readOnlyFirst);
                    var second = disk.Open(!readOnlyFirst);
                    Assert.That(first, Is.Not.SameAs(second), context);
                    if (ReferenceEquals(first, second)) continue;
                    var disposed = readOnlyFirst == disposeReadOnly ? first : second;
                    var survivor = ReferenceEquals(disposed, first) ? second : first;
                    disposed.Dispose();
                    Assert.That(survivor.IsDisposed, Is.False, context);
                    Assert.That(survivor.GetByteArray("seed"), Is.EqualTo(Seed), context);
                    Assert.That(disk.Open(!disposeReadOnly), Is.SameAs(survivor), context);
                    if (disposeReadOnly)
                    {
                        survivor.Add("survivor", Written);
                        survivor.Flush();
                    }
                    var replacement = disk.Open(disposeReadOnly);
                    Assert.That(replacement, Is.Not.SameAs(disposed), context);
                    Assert.That(replacement, Is.Not.SameAs(survivor), context);
                    Assert.That(replacement.IsDisposed, Is.False, context);
                    if (!disposeReadOnly)
                    {
                        replacement.Add("replacement", Written);
                        replacement.Flush();
                        Assert.That(survivor.GetByteArray("replacement"), Is.Null, context + ", existing read-only snapshot");
                    }
                    else Assert.Throws<InvalidOperationException>(() => replacement.Add("forbidden", Written), context);
                    replacement.Dispose();
                    Assert.That(survivor.IsDisposed, Is.False, context);
                    survivor.Dispose();
                    var persisted = disk.Open(true);
                    Assert.That(persisted.GetByteArray("seed"), Is.EqualTo(Seed), context);
                    Assert.That(persisted.GetByteArray(disposeReadOnly ? "survivor" : "replacement"), Is.EqualTo(Written), context);
                }
            });
        }

        [Test]
        public void LiteralPathsNullErrorsAndFolderBackendKeepTheirExistingContracts()
        {
            using var disk = new NativeStore();
            var direct = disk.Open(true);
            var alias = Path.Combine(Path.GetDirectoryName(disk.PathName), ".", Path.GetFileName(disk.PathName));
            var separate = disk.Track(PointCloud.OpenStore(alias, readOnly: true));
            Assert.That(separate, Is.Not.SameAs(direct), alias);
            Assert.That(separate.GetByteArray("seed"), Is.EqualTo(Seed), alias);
            Assert.That(disk.Track(PointCloud.OpenStore(alias, readOnly: true)), Is.SameAs(separate), alias);
            Assert.That(Assert.Throws<ArgumentNullException>(() => PointCloud.OpenStore(null))?.ParamName, Is.EqualTo("key"));

            var folder = Path.Combine(Path.GetDirectoryName(disk.PathName), "folder");
            Directory.CreateDirectory(folder);
            var folderStore = disk.Track(PointCloud.OpenStore(folder, readOnly: true));
            Assert.DoesNotThrow(() => folderStore.Add("folder-key", Written), "folder-store readOnly enforcement is not part of this fix");
            Assert.That(folderStore.GetByteArray("folder-key"), Is.EqualTo(Written));
            Assert.That(disk.Track(PointCloud.OpenStore(folder, readOnly: true)), Is.SameAs(folderStore));
        }
    }
}
