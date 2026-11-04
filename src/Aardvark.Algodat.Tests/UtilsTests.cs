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
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Subsets = Aardvark.Data.Points.ArrayExtensions;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class UtilsTests
    {
        private static readonly int[][] Selections = { new int[0], new[] { 2 }, new[] { 3, 0, 2 }, new[] { 1, 1, 3, 1 } };

        [Test]
        public void IListOnlySelectionsKeepValueAndReferenceOrderDuplicatesAndEmptyResults()
        {
            Assert.Multiple(() =>
            {
                CheckIListOnly(new[] { 12, -7, 41, 99 });
                CheckIListOnly(new[] { V3d.Zero, V3d.IOO, new V3d(-2, 3, 4), V3d.OOI });
                CheckIListOnly(new[] { new string('a', 1), null, new string('c', 2), new string('d', 3) });
            });
        }

        private static void CheckIListOnly<T>(T[] source)
        {
            foreach (var indices in Selections)
            {
                var list = new IListOnlyIndices(indices);
                var context = $"type={typeof(T).Name}, indices=[{string.Join(",", indices)}], IList-only";
                Assert.That(list, Is.Not.InstanceOf<IReadOnlyList<int>>(), context);
                Assert.DoesNotThrow(() =>
                {
                    CheckSubset(source, indices, () => Subsets.Subset(source, (IList<int>)list), context);
                    Assert.That(list.CountReads, Is.EqualTo(1), context + ", one count read");
                    Assert.That(list.IndexReads, Is.EqualTo(Enumerable.Range(0, indices.Length)), context + ", indexed access only");
                }, context);
            }
        }

        [Test]
        public void ArrayListAndInterfaceOverloadsKeepTheirResultsAndArrayOwnership()
        {
            CheckOverloads(new[] { 12, -7, 41, 99 });
            CheckOverloads(new[] { V3d.Zero, V3d.IOO, new V3d(-2, 3, 4), V3d.OOI });
            CheckOverloads(new[] { new string('a', 1), null, new string('c', 2), new string('d', 3) });
        }

        private static void CheckOverloads<T>(T[] source)
        {
            foreach (var indices in Selections)
            {
                var list = new List<int>(indices);
                var calls = new (string Name, Func<T[]> Query)[]
                {
                    ("array indices", () => Subsets.Subset(source, indices)),
                    ("List indices", () => Subsets.Subset(source, list)),
                    ("array through IList", () => Subsets.Subset(source, (IList<int>)indices)),
                    ("List through IList", () => Subsets.Subset(source, (IList<int>)list)),
                    ("IReadOnlyList indices", () => Subsets.Subset(source, (IReadOnlyList<int>)indices)),
                    ("IList source / array", () => Subsets.Subset((IList<T>)source, indices)),
                    ("IList source / List", () => Subsets.Subset((IList<T>)source, list)),
                    ("IReadOnlyList source / array", () => Subsets.Subset((IReadOnlyList<T>)source, indices)),
                    ("IReadOnlyList source / IReadOnlyList", () => Subsets.Subset((IReadOnlyList<T>)source, (IReadOnlyList<int>)indices)),
                    ("object source", () => (T[])Subsets.Subset((object)source, (IReadOnlyList<int>)indices))
                };
                foreach (var call in calls)
                    CheckSubset(source, indices, call.Query, $"type={typeof(T).Name}, {call.Name}, indices=[{string.Join(",", indices)}]");
                Assert.That(list, Is.EqualTo(indices), "List selection unchanged");
            }
        }

        private static void CheckSubset<T>(T[] source, int[] indices, Func<T[]> query, string context)
        {
            var sourceBefore = source.ToArray();
            var indicesBefore = indices.ToArray();
            var expected = indices.Select(i => source[i]).ToArray();
            var actual = query();
            Assert.That(actual, Is.EqualTo(expected), context);
            Assert.That(actual, Is.Not.SameAs(source), context + ", new result array");
            if (!typeof(T).IsValueType)
                for (var i = 0; i < actual.Length; i++)
                    Assert.That(actual[i], Is.SameAs(source[indices[i]]), context + $", reference element={i}");
            foreach (var item in actual)
                if (item is V3d p)
                    Assert.That(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z), Is.True, context + ", finite vector");
            if (actual.Length > 0) actual[0] = default;
            Assert.That(source, Is.EqualTo(sourceBefore), context + ", source unchanged after result mutation");
            Assert.That(indices, Is.EqualTo(indicesBefore), context + ", indices unchanged");
        }

        [Test]
        public void IListSubsetKeepsNullBoundsAndEmptySourceBehavior()
        {
            var source = new[] { 12, -7, 41, 99 };
            var snapshot = source.ToArray();
            IList<int> missing = null;
            Assert.Throws<NullReferenceException>(() => Subsets.Subset(source, missing), "null indices");
            foreach (var list in new[] { false, true })
            {
                IList<int> Wrap(int[] indices) => list ? new List<int>(indices) : indices;
                var empty = Wrap(Array.Empty<int>());
                int[] absentSource = null;
                Assert.That(Subsets.Subset(absentSource, empty), Is.Empty, $"list={list}, null source with no reads");
                var emptySource = Array.Empty<int>();
                var first = Subsets.Subset(emptySource, empty);
                var second = Subsets.Subset(emptySource, empty);
                Assert.That(first, Is.Empty, $"list={list}, empty source");
                Assert.That(first, Is.Not.SameAs(emptySource), "empty result is not the source");
                Assert.That(second, Is.Not.SameAs(first), "empty results retain distinct arrays");
                Assert.Throws<NullReferenceException>(() => Subsets.Subset(absentSource, Wrap(new[] { 0 })), $"list={list}, nonempty null source");
                foreach (var indices in new[] { new[] { -1 }, new[] { 4 }, new[] { 2, int.MinValue }, new[] { 2, int.MaxValue } })
                {
                    var selection = Wrap(indices);
                    Assert.Throws<IndexOutOfRangeException>(() => Subsets.Subset(source, selection), $"list={list}, indices=[{string.Join(",", indices)}]");
                    Assert.That(source, Is.EqualTo(snapshot), "invalid selection leaves source unchanged");
                    Assert.That(selection, Is.EqualTo(indices), "invalid selection unchanged");
                }
            }
        }

        private sealed class IListOnlyIndices : IList<int>
        {
            private readonly int[] values;
            public int CountReads { get; private set; }
            public List<int> IndexReads { get; } = new();
            public IListOnlyIndices(int[] values) => this.values = values;
            public int Count { get { CountReads++; return values.Length; } }
            public bool IsReadOnly => true;
            public int this[int index]
            {
                get { IndexReads.Add(index); return values[index]; }
                set => throw new NotSupportedException();
            }
            public int IndexOf(int item) => Array.IndexOf(values, item);
            public bool Contains(int item) => IndexOf(item) >= 0;
            public IEnumerator<int> GetEnumerator() => throw new NotSupportedException("Use the IList indexer.");
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
            public void CopyTo(int[] array, int index) => throw new NotSupportedException("Do not materialize the indices.");
            public void Add(int item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public void Insert(int index, int item) => throw new NotSupportedException();
            public bool Remove(int item) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
        }

        #region DistLessThanL1

        [Test]
        public void DistLessThanL1_1()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(1, 2, 3);
            ClassicAssert.IsTrue(Utils.DistLessThanL1(ref a, ref b, 0.1));
        }

        [Test]
        public void DistLessThanL1_2()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(1, 2, 3);
            ClassicAssert.IsTrue(!Utils.DistLessThanL1(ref a, ref b, 0.0));
        }

        [Test]
        public void DistLessThanL1_3()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            ClassicAssert.IsTrue(Utils.DistLessThanL1(ref a, ref b, 1.0001));
        }

        [Test]
        public void DistLessThanL1_4()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            ClassicAssert.IsTrue(!Utils.DistLessThanL1(ref a, ref b, 1.0));
        }

        [Test]
        public void DistLessThanL1_5()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            ClassicAssert.IsTrue(Utils.DistLessThanL1(ref b, ref a, 1.0001));
        }

        [Test]
        public void DistLessThanL1_6()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            ClassicAssert.IsTrue(!Utils.DistLessThanL1(ref b, ref a, 1.0));
        }

        #endregion

        #region DistLessThanL2

        [Test]
        public void DistLessThanL2_1()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(1, 2, 3);
            ClassicAssert.IsTrue(Utils.DistLessThanL2(ref a, ref b, 0.1));
        }

        [Test]
        public void DistLessThanL2_2()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(1, 2, 3);
            ClassicAssert.IsTrue(!Utils.DistLessThanL2(ref a, ref b, 0.0));
        }

        [Test]
        public void DistLessThanL2_3()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            ClassicAssert.IsTrue(!Utils.DistLessThanL2(ref a, ref b, 1.0001));
        }

        [Test]
        public void DistLessThanL2_4()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            ClassicAssert.IsTrue(!Utils.DistLessThanL2(ref a, ref b, 1.0));
        }

        [Test]
        public void DistLessThanL2_5()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            ClassicAssert.IsTrue(!Utils.DistLessThanL2(ref b, ref a, 1.0001));
        }

        [Test]
        public void DistLessThanL2_6()
        {
            var a = new V3d(1, 2, 3);
            var b = new V3d(2, 3, 4);
            ClassicAssert.IsTrue(!Utils.DistLessThanL2(ref b, ref a, 1.0));
        }

        #endregion

        #region Normals

        [Test]
        public void NormalsFromEmptyArray()
        {
            var ps = Array.Empty<V3f>();
            var kd = ps.BuildKdTree();
            var ns = ps.EstimateNormalsAsync(16, kd).Result;
            ClassicAssert.IsTrue(ns.Length == 0);
        }

        #endregion
    }
}
