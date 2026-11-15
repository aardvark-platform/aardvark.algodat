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
using System.IO;
using System.Linq;
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
            try
            {
                var store = PointCloud.OpenStore(storepath, cache: default);
                using (store)
                {
                    store.Add("key", new byte[] { 1, 2, 3 });
                    var xs = store.GetByteArray("key");
                    ClassicAssert.IsTrue(xs[0] == 1 && xs[1] == 2 && xs[2] == 3);
                    store.Flush();
                }

                using var reopened = PointCloud.OpenStore(storepath, cache: default);
                Assert.That(reopened, Is.Not.SameAs(store));
                Assert.That(reopened.IsDisposed, Is.False);
                Assert.That(reopened.GetByteArray("key"), Is.EqualTo(new byte[] { 1, 2, 3 }));
            }
            finally
            {
                if (Directory.Exists(storepath)) Directory.Delete(storepath, recursive: true);
            }
        }

        [Test]
        public void SuccessfulDisposalSkipsLaterCallsAliasesAndUsingCleanup()
        {
            Assert.Multiple(() =>
            {
                foreach (var derived in new[] { false, true })
                foreach (var strictBackend in new[] { false, true })
                foreach (var pattern in new[] { "repeated", "nested aliases", "explicit inside using" })
                {
                    var context = $"derived={derived}, strict backend={strictBackend}, pattern={pattern}";
                    var calls = 0;
                    var completed = false;
                    var events = new List<string>();
                    var store = CreateStorage(derived, () =>
                    {
                        calls++;
                        if (strictBackend && calls > 1) throw new InvalidOperationException("backend cleaned twice");
                        completed = true;
                    }, () => Assert.Fail("disposal must not flush"), events);
                    try
                    {
                        Assert.That(store.IsDisposed, Is.False, context + ", initial state");
                        Assert.DoesNotThrow(() =>
                        {
                            switch (pattern)
                            {
                                case "repeated":
                                    store.Dispose(); store.Dispose(); store.Dispose();
                                    break;
                                case "nested aliases":
                                    using (var first = store)
                                    using (var second = store)
                                        Assert.That(second, Is.SameAs(first), context);
                                    break;
                                case "explicit inside using":
                                    using (store) store.Dispose();
                                    break;
                            }
                        }, context);
                        Assert.That(store.IsDisposed, Is.True, context + ", completed state");
                        Assert.That(calls, Is.EqualTo(1), context + ", backend count");
                        if (derived)
                            Assert.That(events, Is.EqualTo(new[] { "hook:True:True", "return:True" }), context + ", hook dispatch");
                    }
                    finally { if (!completed) store.Dispose(); }
                }
            });
        }

        [Test]
        public void BackendCleanupFailuresPropagateAndRetryUntilSuccessful()
        {
            Assert.Multiple(() =>
            {
                foreach (var derived in new[] { false, true })
                foreach (var failures in new[] { 1, 2 })
                {
                    var context = $"derived={derived}, failures={failures}";
                    var fault = new IOException("backend cleanup failure");
                    var attempts = 0;
                    var completed = false;
                    var events = new List<string>();
                    Storage store = null;
                    store = CreateStorage(derived, () =>
                    {
                        attempts++;
                        Assert.That(store.IsDisposed, Is.True, context + ", flag visible to backend");
                        if (attempts <= failures) throw fault;
                        completed = true;
                    }, () => Assert.Fail("cleanup retry must not flush"), events);
                    try
                    {
                        for (var i = 1; i <= failures; i++)
                        {
                            Assert.That(Assert.Throws<IOException>(() => store.Dispose(), context), Is.SameAs(fault), context);
                            Assert.That(store.IsDisposed, Is.True, context + $", failed attempt={i}");
                            Assert.That(attempts, Is.EqualTo(i), context + ", retry was not skipped");
                        }
                        Assert.DoesNotThrow(() => store.Dispose(), context + ", successful retry");
                        var hookCalls = events.Count;
                        Assert.DoesNotThrow(() => { store.Dispose(); store.Dispose(); }, context + ", after success");
                        Assert.That(attempts, Is.EqualTo(failures + 1), context + ", backend completion count");
                        Assert.That(events.Count, Is.EqualTo(hookCalls), context + ", no later hook");
                        if (derived)
                        {
                            Assert.That(events.Count(x => x == "hook:True:True"), Is.EqualTo(failures + 1), context);
                            Assert.That(events.Count(x => x == "return:True"), Is.EqualTo(1), context + ", only successful hook returns");
                        }
                    }
                    finally { if (!completed) store.Dispose(); }
                }
            });
        }

        [Test]
        public void FirstDisposalAndProtectedFalseKeepOrderingAndExplicitFlushing()
        {
            foreach (var explicitFlush in new[] { false, true })
            {
                var events = new List<string>();
                var store = new HookStorage(() => events.Add("backend"), () => events.Add("flush"), events);
                try
                {
                    store.CallProtectedFalse();
                    Assert.That(store.IsDisposed, Is.False, "protected false must not mark public disposal");
                    Assert.That(events, Is.EqualTo(new[] { "hook:False:False", "return:False" }));
                    if (explicitFlush) store.Flush();
                    store.Dispose();
                    Assert.That(store.IsDisposed, Is.True);
                    var expected = new List<string> { "hook:False:False", "return:False" };
                    if (explicitFlush) expected.Add("flush");
                    expected.AddRange(new[] { "hook:True:True", "backend", "return:True" });
                    Assert.That(events, Is.EqualTo(expected), $"explicit flush={explicitFlush}");
                }
                finally { if (!store.IsDisposed) store.Dispose(); }
            }
        }

        private static Storage CreateStorage(bool derived, Action dispose, Action flush, List<string> events)
            => derived ? new HookStorage(dispose, flush, events) : new Storage(
                add: (_, _, _) => { }, get: _ => null, getSlice: (_, _, _) => null,
                remove: _ => { }, dispose: dispose, flush: flush, cache: null);

        private sealed class HookStorage : Storage
        {
            private readonly List<string> m_events;

            public HookStorage(Action dispose, Action flush, List<string> events)
                : base(add: (_, _, _) => { }, get: _ => null, getSlice: (_, _, _) => null,
                    remove: _ => { }, dispose: dispose, flush: flush, cache: null)
                => m_events = events;

            public void CallProtectedFalse() => Dispose(false);

            protected override void Dispose(bool disposing)
            {
                m_events.Add($"hook:{disposing}:{IsDisposed}");
                base.Dispose(disposing);
                m_events.Add($"return:{disposing}");
            }
        }
    }
}
