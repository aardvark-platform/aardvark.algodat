/*
    Copyright (C) 2006-2024. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class FileHashTests
{
    private const int MiB = 1024 * 1024;

    [Test]
    public void DigestsKeepGuidByteOrderAndTheOneMiBBoundary()
    {
        WithFile(path =>
        {
            foreach (var size in new[] { 0, 3, 17, 4096, MiB - 1, MiB, MiB + 1, 2 * MiB + 37 })
            {
                var bytes = Contents(size);
                File.WriteAllBytes(path, bytes);
                foreach (var fast in new[] { false, true })
                {
                    var context = $"size={size}, fast={fast}";
                    var expected = Expected(bytes, fast);
                    Assert.That(FileHelpers.ComputeMd5Hash(path, fast), Is.EqualTo(expected), context);
                    Assert.That(FileHelpers.ComputeMd5Hash(path, fast), Is.EqualTo(expected), context + ", repeated");
                    AssertReleased(path);
                    Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes), context + ", input unchanged");
                }
            }

            foreach (var vector in new[]
            {
                (Bytes: Array.Empty<byte>(), Digest: "d98c1dd4-008f-04b2-e980-0998ecf8427e"),
                (Bytes: new byte[] { 97, 98, 99 }, Digest: "98500190-d23c-b04f-d696-3f7d28e17f72")
            })
            {
                File.WriteAllBytes(path, vector.Bytes);
                foreach (var fast in new[] { false, true })
                    Assert.That(FileHelpers.ComputeMd5Hash(path, fast), Is.EqualTo(vector.Digest),
                        $"known vector length={vector.Bytes.Length}, fast={fast}");
            }
        });
    }

    [Test]
    public void PrefixChangesAffectBothModesButSuffixChangesOnlyAffectFullHashes()
    {
        WithFile(path =>
        {
            foreach (var size in new[] { 17, MiB - 1, MiB, MiB + 1, 2 * MiB + 37 })
            {
                var bytes = Contents(size);
                foreach (var index in new[] { 0, Math.Min(size, MiB) - 1, size - 1 }.Distinct())
                {
                    var changed = (byte[])bytes.Clone();
                    changed[index] ^= 0x5a;
                    foreach (var fast in new[] { false, true })
                    {
                        var context = $"size={size}, changed index={index}, fast={fast}";
                        File.WriteAllBytes(path, bytes);
                        var before = FileHelpers.ComputeMd5Hash(path, fast);
                        File.WriteAllBytes(path, changed);
                        var after = FileHelpers.ComputeMd5Hash(path, fast);
                        Assert.That(before, Is.EqualTo(Expected(bytes, fast)), context + ", before");
                        Assert.That(after, Is.EqualTo(Expected(changed, fast)), context + ", after");
                        Assert.That(after == before, Is.EqualTo(fast && index >= MiB), context);
                        AssertReleased(path);
                    }
                }
            }
        });
    }

    [Test]
    public void FileSharingErrorsAndHandleReleaseKeepTheirExistingContracts()
    {
        WithFile(path =>
        {
            foreach (var size in new[] { 0, 17, MiB, MiB + 1 })
            {
                var bytes = Contents(size);
                File.WriteAllBytes(path, bytes);
                foreach (var fast in new[] { false, true })
                {
                    var context = $"size={size}, fast={fast}";
                    using (var reader = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        // Only the <=1 MiB fast branch uses File.ReadAllBytes' shared reader.
                        if (fast && size <= MiB)
                            Assert.That(FileHelpers.ComputeMd5Hash(path, fast), Is.EqualTo(Expected(bytes, fast)), context);
                        else
                            Assert.Throws<IOException>(() => FileHelpers.ComputeMd5Hash(path, fast), context);
                        Assert.That(reader.CanRead, Is.True, context + ", caller reader remains open");
                    }
                    AssertReleased(path);
                    Assert.That(FileHelpers.ComputeMd5Hash(path, fast), Is.EqualTo(Expected(bytes, fast)), context + ", retry");
                    AssertReleased(path);
                }
            }
            File.Delete(path);
            foreach (var fast in new[] { false, true })
            {
                Assert.Throws<FileNotFoundException>(() => FileHelpers.ComputeMd5Hash(path, fast), $"missing, fast={fast}");
                Assert.Throws<ArgumentNullException>(() => FileHelpers.ComputeMd5Hash(null, fast), $"null, fast={fast}");
                Assert.Throws<ArgumentException>(() => FileHelpers.ComputeMd5Hash("", fast), $"empty, fast={fast}");
            }
            File.WriteAllBytes(path, Contents(17));
            AssertReleased(path);
        });
    }

    [Test, NonParallelizable]
    public void FullAndFastHashesScheduleNoTasksOnTheirIsolatedCallingThread()
    {
        WithFile(path =>
        {
            Exception failure = null;
            var caller = new Thread(() =>
            {
                try
                {
                    // Initialize the event source before measuring, then prove the listener works.
                    Task.Run(() => { }).GetAwaiter().GetResult();
                    using var listener = new CallingThreadTasks();
                    Assert.That(listener.Measure(() => Task.Run(() => { }).GetAwaiter().GetResult()),
                        Is.EqualTo(1), "positive control must observe exactly one caller-scheduled task");
                    foreach (var size in new[] { 0, 17, MiB, MiB + 1 })
                    {
                        File.WriteAllBytes(path, Contents(size));
                        foreach (var fast in new[] { false, true })
                        {
                            var scheduled = listener.Measure(() =>
                            {
                                for (var i = 0; i < 8; i++) FileHelpers.ComputeMd5Hash(path, fast);
                            });
                            Assert.That(scheduled, Is.Zero, $"size={size}, fast={fast}, eight calls");
                        }
                    }
                }
                catch (Exception e) { failure = e; }
            });
            caller.IsBackground = true;
            caller.Start();
            Assert.That(caller.Join(TimeSpan.FromSeconds(30)), Is.True, "isolated diagnostic did not finish");
            if (failure != null) throw failure;
            AssertReleased(path);
        });
    }

    private sealed class CallingThreadTasks : EventListener
    {
        private readonly int m_caller = Environment.CurrentManagedThreadId;
        private bool m_measuring;
        private int m_scheduled;

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "System.Threading.Tasks.TplEventSource")
                EnableEvents(source, EventLevel.Informational, EventKeywords.All);
        }

        protected override void OnEventWritten(EventWrittenEventArgs data)
        {
            if (m_measuring && Environment.CurrentManagedThreadId == m_caller && data.EventName == "TaskScheduled")
                m_scheduled++;
        }

        public int Measure(Action action)
        {
            var before = m_scheduled;
            m_measuring = true;
            try { action(); }
            finally { m_measuring = false; }
            return m_scheduled - before;
        }
    }

    private static byte[] Contents(int size)
        => Enumerable.Range(0, size).Select(i => (byte)((i * 73 + 19) ^ ((i >> 8) * 31))).ToArray();

    private static string Expected(byte[] bytes, bool fast)
    {
        var hex = Convert.ToHexString(MD5.HashData(bytes.AsSpan(0, fast ? Math.Min(bytes.Length, MiB) : bytes.Length)))
            .ToLowerInvariant();
        // MD5 hex vectors are network-order bytes; Guid reverses its first three fields.
        return $"{hex[6..8]}{hex[4..6]}{hex[2..4]}{hex[0..2]}-{hex[10..12]}{hex[8..10]}-" +
               $"{hex[14..16]}{hex[12..14]}-{hex[16..20]}-{hex[20..32]}";
    }

    private static void AssertReleased(string path)
    {
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.That(exclusive.CanWrite, Is.True, "file handle must be released");
    }

    private static void WithFile(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "algodat-file-hash-" + Guid.NewGuid());
        try { action(path); }
        finally { File.Delete(path); }
    }
}
