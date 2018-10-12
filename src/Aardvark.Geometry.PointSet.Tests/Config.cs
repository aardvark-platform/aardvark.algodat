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
using System;
using System.Globalization;
using System.IO;
using System.Threading;
using NUnit.Framework;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    internal static class Config
    {
        public static readonly string TEST_FILE_NAME_PTS = Path.Combine(Path.GetTempPath(), "test.pts");

        public static string TestDataDir
        {
            get
            {
                var d = TestContext.CurrentContext.TestDirectory;
                TestContext.WriteLine($"[TestDataDir] TestContext.CurrentContext.TestDirectory: {d}");
                while (!Directory.EnumerateDirectories(d).Select(x => Path.GetFileName(x)).Contains(".paket"))
                    d = Path.GetDirectoryName(d);
                var result = Path.GetFullPath(Path.Combine(d, "src/Aardvark.Geometry.PointSet.Tests/TestData"));
                TestContext.WriteLine($"[TestDataDir] result: {result}");
                return result;
            }
        }

        public static string TempDataDir
        {
            get
            {
                var d = TestContext.CurrentContext.TestDirectory;
                TestContext.WriteLine($"[TempDataDir] TestContext.CurrentContext.TestDirectory: {d}");
                while (!Directory.EnumerateDirectories(d).Select(x => Path.GetFileName(x)).Contains(".paket"))
                    d = Path.GetDirectoryName(d);
                var result = Path.GetFullPath(Path.Combine(d, "bin/tmp"));
                TestContext.WriteLine($"[TestDataDir] result: {result}");
                return result;
            }
        }

        static Config()
        {
            lock (TEST_FILE_NAME_PTS)
            {
                if (File.Exists(TEST_FILE_NAME_PTS)) return;

                var n = 10000;
                var bb = new Box3d(new V3d(-1, -1, -1), new V3d(+1, +1, +1));
                var bbSize = bb.Size;
                var r = new Random();
                Func<V3d> randomV3d = () => new V3d(
                    bb.Min.X + bbSize.X * r.NextDouble(),
                    bb.Min.Y + bbSize.Y * r.NextDouble(),
                    bb.Min.Z + bbSize.Z * r.NextDouble()
                    );

                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                using (var fs = File.Open(TEST_FILE_NAME_PTS, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs))
                {
                    sw.WriteLine($"{n}");
                    for (var i = 0; i < n; i++)
                    {
                        var p = randomV3d();
                        sw.WriteLine($"{p.X} {p.Y} {p.Z} {-2000 + r.Next(4000)} {r.Next(256)} {r.Next(256)} {r.Next(256)}");
                    }
                }
            }
        }
    }
}
