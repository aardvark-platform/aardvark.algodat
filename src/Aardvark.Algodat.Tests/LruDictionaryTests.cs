/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class LruDictionaryTests
    {
        #region Create

        [Test]
        public void Create()
        {
            var a = new LruDictionary<int, string>(10);
            ClassicAssert.IsTrue(a.MaxSize == 10);
            ClassicAssert.IsTrue(a.CurrentSize == 0);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        #endregion

        #region Clear

        [Test]
        public void Clear0()
        {
            var a = new LruDictionary<int, string>(10);
            
            ClassicAssert.IsTrue(a.CurrentSize == 0);
            ClassicAssert.IsTrue(a.Count == 0);

            a.Clear();
            ClassicAssert.IsTrue(a.CurrentSize == 0);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        [Test]
        public void Clear2()
        {
            var a = new LruDictionary<int, string>(10);

            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 7);
            ClassicAssert.IsTrue(a.Count == 2);

            a.Clear();
            ClassicAssert.IsTrue(a.CurrentSize == 0);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        [Test]
        public void Clear3()
        {
            var a = new LruDictionary<int, string>(20);

            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            a.Add(3, "three", 5, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 12);
            ClassicAssert.IsTrue(a.Count == 3);

            a.Clear();
            ClassicAssert.IsTrue(a.CurrentSize == 0);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        #endregion

        #region Add

        [Test]
        public void Add1()
        {
            var a = new LruDictionary<int, string>(10);
            a.Add(1, "one", 3, onRemove: default);

            ClassicAssert.IsTrue(a.MaxSize == 10);
            ClassicAssert.IsTrue(a.CurrentSize == 3);
            ClassicAssert.IsTrue(a.Count == 1);
        }

        [Test]
        public void Add2()
        {
            var a = new LruDictionary<int, string>(10);
            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);

            ClassicAssert.IsTrue(a.MaxSize == 10);
            ClassicAssert.IsTrue(a.CurrentSize == 7);
            ClassicAssert.IsTrue(a.Count == 2);
        }

        [Test]
        public void Add1_Replace()
        {
            var a = new LruDictionary<int, string>(10);

            a.Add(1, "one", 3, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 3);
            ClassicAssert.IsTrue(a.Count == 1);

            a.Add(1, "one", 5, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 5);
            ClassicAssert.IsTrue(a.Count == 1);
        }

        [Test]
        public void Add2_ReplaceFirst()
        {
            var a = new LruDictionary<int, string>(10);

            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 7);
            ClassicAssert.IsTrue(a.Count == 2);

            a.Add(1, "one", 5, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 9);
            ClassicAssert.IsTrue(a.Count == 2);
        }

        [Test]
        public void Add2_ReplaceLast()
        {
            var a = new LruDictionary<int, string>(10);

            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 7);
            ClassicAssert.IsTrue(a.Count == 2);

            a.Add(2, "two", 5, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 8);
            ClassicAssert.IsTrue(a.Count == 2);
        }

        [Test]
        public void Add3_Replace_First()
        {
            var a = new LruDictionary<int, string>(20);

            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            a.Add(3, "three", 5, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 12);
            ClassicAssert.IsTrue(a.Count == 3);

            a.Add(1, "one", 5, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 14);
            ClassicAssert.IsTrue(a.Count == 3);
        }

        [Test]
        public void Add3_Replace_Last()
        {
            var a = new LruDictionary<int, string>(20);

            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            a.Add(3, "three", 5, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 12);
            ClassicAssert.IsTrue(a.Count == 3);

            a.Add(3, "three", 6, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 13);
            ClassicAssert.IsTrue(a.Count == 3);
        }

        [Test]
        public void Add3_Replace_Middle()
        {
            var a = new LruDictionary<int, string>(20);

            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            a.Add(3, "three", 5, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 12);
            ClassicAssert.IsTrue(a.Count == 3);

            a.Add(2, "two", 6, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 14);
            ClassicAssert.IsTrue(a.Count == 3);
        }

        [Test]
        public void Add_SizeTooBig()
        {
            var a = new LruDictionary<int, string>(10);

            Assert.Catch(() =>
            {
                a.Add(1, "one", 11, onRemove: default);
            });
        }

        [Test]
        public void Add_Size0()
        {
            var a = new LruDictionary<int, string>(10);
            a.Add(1, "one", 0, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 0);
        }

        [Test]
        public void Add_NegativeSize()
        {
            var a = new LruDictionary<int, string>(10);

            Assert.Catch(() =>
            {
                a.Add(1, "one", -1, onRemove: default);
            });
        }
        
        [Test]
        public void Add3_Overshoot()
        {
            var a = new LruDictionary<int, string>(15);

            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            a.Add(3, "three", 5, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 12);
            ClassicAssert.IsTrue(a.Count == 3);

            a.Add(4, "four", 10, onRemove: default);
            ClassicAssert.IsTrue(a.CurrentSize == 15);
            ClassicAssert.IsTrue(a.Count == 2);
        }
        
        [Test]
        public void Add3_Overshoot_WithCallback()
        {
            var a = new LruDictionary<int, string>(15);
            var removed = new HashSet<int>();

            a.Add(1, "one", 3, onRemove: (k, v, s) => { removed.Add(1); ClassicAssert.IsTrue(k == 1 && v == "one" && s == 3); });
            a.Add(2, "two", 4, onRemove: (k, v, s) => { removed.Add(2); ClassicAssert.IsTrue(k == 2 && v == "two" && s == 4); });
            a.Add(3, "three", 5, onRemove: (k, v, s) => { removed.Add(3); ClassicAssert.IsTrue(k == 3 && v == "three" && s == 5); });
            a.Add(1, "one", 3, onRemove: (k, v, s) => { removed.Add(1); ClassicAssert.IsTrue(k == 1 && v == "one" && s == 3); });
            ClassicAssert.IsTrue(a.CurrentSize == 12);
            ClassicAssert.IsTrue(a.Count == 3);

            a.Add(4, "four", 10, onRemove: (k, v, s) => { removed.Add(4); ClassicAssert.IsTrue(k == 4 && v == "four" && s == 10); });
            ClassicAssert.IsTrue(a.CurrentSize == 13);
            ClassicAssert.IsTrue(a.Count == 2);

            ClassicAssert.IsTrue(!removed.Contains(1));
            ClassicAssert.IsTrue(removed.Contains(2));
            ClassicAssert.IsTrue(removed.Contains(3));
            ClassicAssert.IsTrue(!removed.Contains(4));
        }

        #endregion

        #region Remove
        
        [Test]
        public void Remove0()
        {
            var a = new LruDictionary<int, string>(15);
            ClassicAssert.IsTrue(a.Remove(1) == false);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        [Test]
        public void Remove1()
        {
            var a = new LruDictionary<int, string>(15);
            a.Add(1, "one", 3, onRemove: default);
            ClassicAssert.IsTrue(a.Remove(1) == true);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        [Test]
        public void Remove2_Least()
        {
            var a = new LruDictionary<int, string>(15);
            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);

            ClassicAssert.IsTrue(a.Remove(1) == true);
            ClassicAssert.IsTrue(a.Count == 1);

            ClassicAssert.IsTrue(a.Remove(2) == true);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        [Test]
        public void Remove2_Last()
        {
            var a = new LruDictionary<int, string>(15);
            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);

            ClassicAssert.IsTrue(a.Remove(2) == true);
            ClassicAssert.IsTrue(a.Count == 1);

            ClassicAssert.IsTrue(a.Remove(1) == true);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        [Test]
        public void Remove3_Least()
        {
            var a = new LruDictionary<int, string>(15);
            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            a.Add(3, "three", 5, onRemove: default);

            ClassicAssert.IsTrue(a.Remove(1) == true);
            ClassicAssert.IsTrue(a.Count == 2);

            ClassicAssert.IsTrue(a.Remove(2) == true);
            ClassicAssert.IsTrue(a.Count == 1);

            ClassicAssert.IsTrue(a.Remove(3) == true);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        [Test]
        public void Remove3_Last()
        {
            var a = new LruDictionary<int, string>(15);
            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            a.Add(3, "three", 5, onRemove: default);

            ClassicAssert.IsTrue(a.Remove(3) == true);
            ClassicAssert.IsTrue(a.Count == 2);

            ClassicAssert.IsTrue(a.Remove(2) == true);
            ClassicAssert.IsTrue(a.Count == 1);

            ClassicAssert.IsTrue(a.Remove(1) == true);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        [Test]
        public void Remove3_Middle()
        {
            var a = new LruDictionary<int, string>(15);
            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);
            a.Add(3, "three", 5, onRemove: default);

            ClassicAssert.IsTrue(a.Remove(2) == true);
            ClassicAssert.IsTrue(a.Count == 2);

            ClassicAssert.IsTrue(a.Remove(1) == true);
            ClassicAssert.IsTrue(a.Count == 1);

            ClassicAssert.IsTrue(a.Remove(3) == true);
            ClassicAssert.IsTrue(a.Count == 0);
        }

        [Test]
        public void Remove1_Callback_With()
        {
            var a = new LruDictionary<int, string>(15);
            var removed = new HashSet<int>();
            a.Add(1, "one", 3, onRemove: (k, v, s) => removed.Add(1));
            ClassicAssert.IsTrue(a.Remove(1, true) == true);
            ClassicAssert.IsTrue(a.Count == 0);
            ClassicAssert.IsTrue(removed.Contains(1));
        }

        [Test]
        public void Remove1_Callback_Without()
        {
            var a = new LruDictionary<int, string>(15);
            var removed = new HashSet<int>();
            a.Add(1, "one", 3, onRemove: (k, v, s) => removed.Add(1));
            ClassicAssert.IsTrue(a.Remove(1, false) == true);
            ClassicAssert.IsTrue(a.Count == 0);
            ClassicAssert.IsTrue(!removed.Contains(1));
        }

        #endregion

        #region ContainsKey
        
        [Test]
        public void ContainsKey()
        {
            var a = new LruDictionary<int, string>(15);
            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);

            ClassicAssert.IsTrue(a.ContainsKey(1));
            ClassicAssert.IsTrue(a.ContainsKey(2));
            ClassicAssert.IsTrue(!a.ContainsKey(3));

            a.Remove(1);
            ClassicAssert.IsTrue(!a.ContainsKey(1));
            
            a.Remove(2);
            ClassicAssert.IsTrue(!a.ContainsKey(2));
        }

        #endregion

        #region ContainsKey

        [Test]
        public void TryGetValue()
        {
            var a = new LruDictionary<int, string>(15);
            a.Add(1, "one", 3, onRemove: default);
            a.Add(2, "two", 4, onRemove: default);

            ClassicAssert.IsTrue(a.TryGetValue(1, out string s1) && s1 == "one");
            ClassicAssert.IsTrue(a.TryGetValue(2, out string s2) && s2 == "two");
            ClassicAssert.IsTrue(!a.TryGetValue(3, out _));

            a.Remove(1);
            ClassicAssert.IsTrue(!a.TryGetValue(1, out _));

            a.Remove(2);
            ClassicAssert.IsTrue(!a.TryGetValue(2, out _));
        }

        #endregion

        #region GetOrCreate

        [Test]
        public void GetOrCreate()
        {
            var a = new LruDictionary<int, string>(15);
            var removed = new HashSet<int>();

            var s1 = a.GetOrCreate(1, () => ("one", 3), (k, v, s) => removed.Add(1));
            ClassicAssert.IsTrue(s1 == "one");
            ClassicAssert.IsTrue(a.Count == 1);
            ClassicAssert.IsTrue(a.ContainsKey(1));

            var s1a = a.GetOrCreate(1, () => throw new Exception(), (k, v, s) => removed.Add(-1));
            ClassicAssert.IsTrue(s1 == "one");
            ClassicAssert.IsTrue(a.Count == 1);
            ClassicAssert.IsTrue(a.ContainsKey(1));

            ClassicAssert.IsTrue(a.Remove(1, true));
            ClassicAssert.IsTrue(removed.Contains(1));
            ClassicAssert.IsTrue(!removed.Contains(-1));
            ClassicAssert.IsTrue(a.Count == 0);
        }

        #endregion

        #region Stress

        [Test]
        public void RandomInserts_1M_SingleThreaded()
        {
            var a = new LruDictionary<int, string>(1024 * 1024 * 1024);

            var r = new Random();
            var sw = new Stopwatch(); sw.Start();
            for (var i = 0; i < 1_000_000; i++)
            {
                a.Add(r.Next(), "foo", r.Next(1, 10 * 1024 * 1024), onRemove: default);
            }
            sw.Stop();
            Console.WriteLine(sw.Elapsed);
        }

        [Test]
        public void RandomInserts_1M_MultiThreaded()
        {
            var a = new LruDictionary<int, string>(1024 * 1024 * 1024);

            var start = new ManualResetEventSlim(false);
            var ts = new Task[4];
            for (var i = 0; i < ts.Length; i++)
            {
                ts[i] = new Task(Do, TaskCreationOptions.LongRunning);
                ts[i].Start();
            }
            var sw = new Stopwatch(); sw.Start();
            start.Set();
            Task.WhenAll(ts).Wait();
            sw.Stop();
            Console.WriteLine(sw.Elapsed);

            void Do()
            {
                start.Wait();
                var r = new Random();
                for (var i = 0; i < 1_000_000; i++)
                {
                    a.Add(r.Next(), "foo", r.Next(1, 10 * 1024 * 1024), onRemove: default);
                }
            }
        }

        #endregion
    }
}
