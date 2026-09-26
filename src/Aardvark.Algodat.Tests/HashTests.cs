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
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class HashTests
    {
        private static readonly Guid EmptyDigest = new("d98c1dd4-008f-04b2-e980-0998ecf8427e");
        private static readonly Plane3d A = new(new V3d(1, -2, 3.5), -4.25);
        private static readonly Plane3d B = new(new V3d(0, 1, 0), 0);
        private static readonly Hull3d EmptyHull = new(Array.Empty<Plane3d>());

        // Independent encoding oracle: explicit bit patterns/byte offsets, not
        // BinaryWriter, Plane.Point, or another production hash overload.
        private static byte[] PlaneBytes(params Plane3d[] planes)
        {
            var bytes = new byte[32 * planes.Length];
            for (var i = 0; i < planes.Length; i++)
            {
                var p = planes[i];
                var fields = new[] { p.Normal.X, p.Normal.Y, p.Normal.Z, p.Distance };
                for (var j = 0; j < 4; j++)
                    BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(32 * i + 8 * j), BitConverter.DoubleToInt64Bits(fields[j]));
            }
            return bytes;
        }

        private static Guid ExpectedPlanes(params Plane3d[] planes) => new(MD5.HashData(PlaneBytes(planes)));

        private static Guid ExpectedHulls(params Hull3d[] hulls)
        {
            var bytes = new List<byte>();
            foreach (var hull in hulls)
            {
                var prefix = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(prefix, hull.PlaneArray.Length);
                bytes.AddRange(prefix);
                bytes.AddRange(PlaneBytes(hull.PlaneArray));
            }
            return new Guid(MD5.HashData(bytes.ToArray()));
        }

        private sealed class OneShot<T> : IEnumerable<T>
        {
            private readonly T[] values;
            public OneShot(T[] values) { this.values = values; }
            public int Enumerations;
            public int Yielded;
            public int Disposals;
            public IEnumerator<T> GetEnumerator()
            {
                if (++Enumerations != 1) throw new InvalidOperationException("Sequence enumerated twice.");
                return Iterate().GetEnumerator();
            }
            private IEnumerable<T> Iterate()
            {
                try { foreach (var value in values) { Yielded++; yield return value; } }
                finally { Disposals++; }
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Test]
        public void PerpendicularOriginPlanesHaveDistinctFingerprints()
        {
            var planes = new[] { Plane3d.XPlane, Plane3d.YPlane, Plane3d.ZPlane };
            Assert.That(planes.Select(x => x.ComputeMd5Hash()).Distinct().Count(), Is.EqualTo(3));
            Assert.That(planes.Select(x => x.ComputeMd5Hash()), Is.EqualTo(new[] {
                new Guid("0986904c-8ef4-66b8-27f7-56b53418f4f7"),
                new Guid("52d7f6da-10fd-8900-3737-7108c4bfd6d8"),
                new Guid("b796f78a-c24e-2a49-271d-9d3d1e1ebad1") }));
        }

        [Test]
        public void EveryStoredCoefficientChangesTheHash([Values(0, 1, 2, 3)] int field)
        {
            var original = new Plane3d(new V3d(1, 2, 3), 0);
            var changed = original;
            if (field == 3) changed.Distance = 7;
            else changed.Normal[field] = 7;
            Assert.That(changed.ComputeMd5Hash(), Is.Not.EqualTo(original.ComputeMd5Hash()));
            Assert.That(changed.ComputeMd5Hash(), Is.EqualTo(ExpectedPlanes(changed)));
        }

        [Test]
        public void StoredBitsAreNotNormalized([Values(0, 1, 2, 3)] int field,
            [Values(0L, long.MinValue, 1L, 0x7ff0000000000000L, 0x7ff8000000000001L, 0x7ff8000000000002L)] long bits)
        {
            var plane = A;
            if (field == 3) plane.Distance = BitConverter.Int64BitsToDouble(bits);
            else plane.Normal[field] = BitConverter.Int64BitsToDouble(bits);
            var before = PlaneBytes(plane);
            Assert.That(plane.ComputeMd5Hash(), Is.EqualTo(ExpectedPlanes(plane)));
            Assert.That(new[] { plane }.ComputeMd5Hash(), Is.EqualTo(plane.ComputeMd5Hash()));
            Assert.That(PlaneBytes(plane), Is.EqualTo(before));
        }

        [Test]
        public void ScaledCoefficientsAndSignedZerosRemainDistinct()
        {
            var unit = Plane3d.XPlane;
            var scaled = new Plane3d(2 * unit.Normal, 0);
            Assert.That(scaled.ComputeMd5Hash(), Is.Not.EqualTo(unit.ComputeMd5Hash()));
            var negativeZero = unit;
            negativeZero.Distance = BitConverter.Int64BitsToDouble(long.MinValue);
            Assert.That(negativeZero.ComputeMd5Hash(), Is.Not.EqualTo(unit.ComputeMd5Hash()));
            Assert.That(A.ComputeMd5Hash(), Is.EqualTo(new Guid("31ebe777-2dc3-cf4e-d64e-601b95d388c3")));
        }

        [Test]
        public void PlaneSequencesMatchTheirEncoding([Values(0, 1, 2, 7, 8, 9, 31, 32, 33, 127, 128, 129, 257)] int count)
        {
            var planes = Enumerable.Range(0, count).Select(i => new Plane3d(new V3d(i + 1, -i, i / 2.0), i - 8.25)).ToArray();
            var sequence = new OneShot<Plane3d>(planes);
            var expected = ExpectedPlanes(planes);
            Assert.That(planes.ComputeMd5Hash(), Is.EqualTo(expected));
            Assert.That(sequence.ComputeMd5Hash(), Is.EqualTo(expected));
            Assert.That(sequence.Enumerations, Is.EqualTo(1));
            Assert.That(sequence.Yielded, Is.EqualTo(count));
            Assert.That(sequence.Disposals, Is.EqualTo(1));
            if (count == 1) Assert.That(planes[0].ComputeMd5Hash(), Is.EqualTo(expected));
        }

        [Test]
        public void HullCountAndCoefficientEncodingIsShared([Values(0, 1, 2, 7, 8, 31, 32, 127, 128, 255, 256, 257)] int count)
        {
            var planes = Enumerable.Range(0, count).Select(i => new Plane3d(new V3d(i + 1, 2, -3), i)).ToArray();
            var hull = new Hull3d(planes);
            var before = PlaneBytes(planes);
            var sequence = new OneShot<Hull3d>(new[] { hull });
            var expected = ExpectedHulls(hull);
            Assert.That(hull.ComputeMd5Hash(), Is.EqualTo(expected));
            Assert.That(new[] { hull }.ComputeMd5Hash(), Is.EqualTo(expected));
            Assert.That(sequence.ComputeMd5Hash(), Is.EqualTo(expected));
            Assert.That(sequence.Enumerations, Is.EqualTo(1));
            Assert.That(sequence.Yielded, Is.EqualTo(1));
            Assert.That(sequence.Disposals, Is.EqualTo(1));
            Assert.That(hull.PlaneArray, Is.SameAs(planes));
            Assert.That(PlaneBytes(planes), Is.EqualTo(before));
        }

        [Test]
        public void HullGoldenFingerprintsIncludeFraming()
        {
            Assert.That(EmptyHull.ComputeMd5Hash(), Is.EqualTo(new Guid("84ffd3f1-2943-3277-862d-f21dc4e57262")));
            Assert.That(new Hull3d(new[] { A }).ComputeMd5Hash(), Is.EqualTo(new Guid("f4f0176d-374b-bdae-f575-7a9332915af5")));
            Assert.That(new Hull3d(new[] { Plane3d.XPlane, Plane3d.YPlane, Plane3d.ZPlane }).ComputeMd5Hash(), Is.EqualTo(new Guid("8466d5f9-3acc-814e-647d-1baa79033404")));
        }

        [Test]
        public void HullGroupingAffectsTheFingerprint()
        {
            var c = Plane3d.ZPlane;
            var left = new[] { new Hull3d(new[] { A }), new Hull3d(new[] { B, c }) };
            var right = new[] { new Hull3d(new[] { A, B }), new Hull3d(new[] { c }) };
            Assert.That(left.ComputeMd5Hash(), Is.Not.EqualTo(right.ComputeMd5Hash()));
            Assert.That(left.ComputeMd5Hash(), Is.EqualTo(ExpectedHulls(left)));
            Assert.That(right.ComputeMd5Hash(), Is.EqualTo(ExpectedHulls(right)));
        }

        [Test]
        public void InsertingEmptyHullsAffectsTheFingerprint([Values(0, 1, 2)] int index)
        {
            var original = new[] { new Hull3d(new[] { A }), new Hull3d(new[] { B }) };
            var inserted = original.Take(index).Concat(new[] { EmptyHull }).Concat(original.Skip(index)).ToArray();
            Assert.That(inserted.ComputeMd5Hash(), Is.Not.EqualTo(original.ComputeMd5Hash()));
            Assert.That(inserted.ComputeMd5Hash(), Is.EqualTo(ExpectedHulls(inserted)));
        }

        [Test]
        public void PlaneHullAndEmptyHullOrderIsSignificant()
        {
            Assert.That(new[] { A, B }.ComputeMd5Hash(), Is.Not.EqualTo(new[] { B, A }.ComputeMd5Hash()));
            Assert.That(new Hull3d(new[] { A, B }).ComputeMd5Hash(), Is.Not.EqualTo(new Hull3d(new[] { B, A }).ComputeMd5Hash()));
            var a = new Hull3d(new[] { A }); var b = new Hull3d(new[] { B });
            Assert.That(new[] { a, b }.ComputeMd5Hash(), Is.Not.EqualTo(new[] { b, a }.ComputeMd5Hash()));
            Assert.That(new[] { a, EmptyHull }.ComputeMd5Hash(), Is.Not.EqualTo(new[] { EmptyHull, a }.ComputeMd5Hash()));
        }

        [Test]
        public void HullSequencesAreConsumedOnce([Values(0, 1, 2, 31, 128, 257)] int count)
        {
            var hulls = Enumerable.Range(0, count).Select(i => new Hull3d(Enumerable.Repeat(A, i % 5).ToArray())).ToArray();
            var sequence = new OneShot<Hull3d>(hulls);
            Assert.That(hulls.ComputeMd5Hash(), Is.EqualTo(ExpectedHulls(hulls)));
            Assert.That(sequence.ComputeMd5Hash(), Is.EqualTo(ExpectedHulls(hulls)));
            Assert.That(sequence.Enumerations, Is.EqualTo(1));
            Assert.That(sequence.Yielded, Is.EqualTo(count));
            Assert.That(sequence.Disposals, Is.EqualTo(1));
        }

        [Test]
        public void ExistingNullAndInvalidContractsArePreserved()
        {
            Assert.Throws<NullReferenceException>(() => ((Plane3d[])null).ComputeMd5Hash());
            Assert.Throws<NullReferenceException>(() => ((IEnumerable<Plane3d>)null).ComputeMd5Hash());
            Assert.That(((Hull3d[])null).ComputeMd5Hash(), Is.EqualTo(Guid.Empty));
            Assert.That(((IEnumerable<Hull3d>)null).ComputeMd5Hash(), Is.EqualTo(Guid.Empty));
            Assert.That(Hull3d.Invalid.ComputeMd5Hash(), Is.EqualTo(Guid.Empty));
            Assert.That(default(Hull3d).ComputeMd5Hash(), Is.EqualTo(Guid.Empty));
            Assert.That(Plane3d.Invalid.ComputeMd5Hash(), Is.EqualTo(ExpectedPlanes(Plane3d.Invalid)));
            Assert.That(Plane3d.Invalid.ComputeMd5Hash(), Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public void InvalidHullElementsThrowAndDisposeEnumeration([Values(0, 1, 2)] int position)
        {
            var hulls = new[] { EmptyHull, EmptyHull, EmptyHull };
            hulls[position] = Hull3d.Invalid;
            Assert.Throws<NullReferenceException>(() => hulls.ComputeMd5Hash());
            var sequence = new OneShot<Hull3d>(hulls);
            Assert.Throws<NullReferenceException>(() => sequence.ComputeMd5Hash());
            Assert.That(sequence.Enumerations, Is.EqualTo(1));
            Assert.That(sequence.Yielded, Is.EqualTo(position + 1));
            Assert.That(sequence.Disposals, Is.EqualTo(1));
        }

        [Test]
        public void EmptySequencesDifferFromOneEmptyHull()
        {
            Assert.That(Array.Empty<Plane3d>().ComputeMd5Hash(), Is.EqualTo(EmptyDigest));
            Assert.That(Array.Empty<Hull3d>().ComputeMd5Hash(), Is.EqualTo(EmptyDigest));
            Assert.That(new OneShot<Hull3d>(Array.Empty<Hull3d>()).ComputeMd5Hash(), Is.EqualTo(EmptyDigest));
            Assert.That(new[] { EmptyHull }.ComputeMd5Hash(), Is.Not.EqualTo(EmptyDigest));
            Assert.That(new[] { EmptyHull }.ComputeMd5Hash(), Is.EqualTo(EmptyHull.ComputeMd5Hash()));
            Assert.That(new[] { EmptyHull, EmptyHull }.ComputeMd5Hash(), Is.Not.EqualTo(EmptyHull.ComputeMd5Hash()));
        }

        [Test]
        public void EnumerationDoesNotAllocatePerElement([Values(16, 4096)] int count, [Values(false, true)] bool hulls)
        {
            var planes = Enumerable.Repeat(A, count).ToArray();
            var hs = Enumerable.Repeat(new Hull3d(new[] { A }), count).ToArray();
            Func<Guid> array = hulls ? () => hs.ComputeMd5Hash() : () => planes.ComputeMd5Hash();
            Func<Guid> sequence = hulls ? () => ((IEnumerable<Hull3d>)hs).ComputeMd5Hash() : () => ((IEnumerable<Plane3d>)planes).ComputeMd5Hash();
            for (var i = 0; i < 8; i++) { array(); sequence(); }
            long Allocated(Func<Guid> f)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 8; i++) f();
                return (GC.GetAllocatedBytesForCurrentThread() - before) / 8;
            }
            Assert.That(Allocated(sequence) - Allocated(array), Is.InRange(0, 128));
        }

        [Test]
        public void GeometryHashingUsesBoundedWorkingMemory([Values(127, 128, 129, 1024, 65536)] int count, [Values(false, true)] bool hulls)
        {
            var planes = Enumerable.Repeat(A, count).ToArray();
            var hs = Enumerable.Repeat(new Hull3d(new[] { A }), count).ToArray();
            Func<Guid> hash = hulls ? () => hs.ComputeMd5Hash() : () => planes.ComputeMd5Hash();
            var expected = hulls ? ExpectedHulls(hs) : ExpectedPlanes(planes);
            for (var i = 0; i < 4; i++) Assert.That(hash(), Is.EqualTo(expected));
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 4; i++) hash();
            var perCall = (GC.GetAllocatedBytesForCurrentThread() - before) / 4;
            Assert.That(perCall, Is.LessThanOrEqualTo(1024), "do not buffer/materialize the whole input");
        }

        [Test]
        public void RecycledBuffersDoNotLeakEarlierDataOrFailedSequences([Values(false, true)] bool failAfterFlush)
        {
            IEnumerable<Plane3d> Input()
            {
                for (var i = 0; i < 257; i++) yield return A;
                if (failAfterFlush) throw new ApplicationException("sequence failure");
            }
            if (failAfterFlush) Assert.Throws<ApplicationException>(() => Input().ComputeMd5Hash());
            else Assert.That(Input().ComputeMd5Hash(), Is.EqualTo(ExpectedPlanes(Enumerable.Repeat(A, 257).ToArray())));
            Assert.That(B.ComputeMd5Hash(), Is.EqualTo(ExpectedPlanes(B)));
            Assert.That(EmptyHull.ComputeMd5Hash(), Is.EqualTo(ExpectedHulls(EmptyHull)));
            Assert.That(Array.Empty<Plane3d>().ComputeMd5Hash(), Is.EqualTo(EmptyDigest));
        }

        [Test]
        public void ReentrantEnumerationKeepsIndependentHashState()
        {
            IEnumerable<Plane3d> Input()
            {
                for (var i = 0; i < 257; i++)
                {
                    Assert.That(new Hull3d(new[] { B }).ComputeMd5Hash(), Is.EqualTo(ExpectedHulls(new Hull3d(new[] { B }))));
                    yield return A;
                }
            }
            Assert.That(Input().ComputeMd5Hash(), Is.EqualTo(ExpectedPlanes(Enumerable.Repeat(A, 257).ToArray())));
        }

        [Test]
        public void ConcurrentCallsKeepIndependentHashState()
        {
            Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
            {
                var planes = Enumerable.Range(0, 129 + i).Select(j => new Plane3d(new V3d(i + 1, j, -3), j - i)).ToArray();
                var expected = ExpectedPlanes(planes);
                for (var round = 0; round < 8; round++) Assert.That(planes.ComputeMd5Hash(), Is.EqualTo(expected));
            });
        }

        public static IEnumerable<TestCaseData> VectorColorGoldens()
        {
            yield return new(new V2f(1.25f, -2.5f), "460e5921-56b0-9e58-84cb-53ed76b09e58", "40c61419-6878-61e2-a5c4-f7447c13deac");
            yield return new(new V2d(1.25, -2.5), "ecb315e5-1400-2104-1eb4-21c1f7caa4f2", "852c6d46-b16c-9eff-21fa-5b51492ba70d");
            yield return new(new V2i(-123456789, 42), "61bbc2c9-5bac-24c6-07e8-42ca78c38c38", "8d343978-4950-1597-0886-38c724402f21");
            yield return new(new V2l(-12345678901234567L, 1152921504606846977L), "e8eb24c7-b3ab-0443-e459-331afbf85a7f", "4ee9a489-d89f-bc92-24f5-a02b0c48d760");
            yield return new(new V3f(1.25f, -2.5f, 3.75f), "272ed5ed-b25f-a604-c7b0-7fa2aad5a022", "20ced9cf-e0e4-2283-c595-bcf7391b7d11");
            yield return new(new V3d(1.25, -2.5, 3.75), "cd5b6dd7-62fa-b087-9e72-5a531e878a04", "35490e6d-6c79-132d-7c7b-69016042262a");
            yield return new(new V3i(-123456789, 42, -7), "319a677f-7abf-c688-0bb6-51d3c9cda3eb", "66488229-1e1a-4337-f3a6-ebe31e35dde0");
            yield return new(new V3l(-12345678901234567L, 1152921504606846977L, -7L), "664fbc3b-1251-2996-50dd-b14718706c1f", "f5acaa44-dd5d-7056-2526-7f75a7cb82c3");
            yield return new(new V4f(1.25f, -2.5f, 3.75f, -4.5f), "a0e812f2-e715-3c10-c69a-b0cf79033335", "b1170319-3990-6bfc-11a4-a3e04f92240b");
            yield return new(new V4d(1.25, -2.5, 3.75, -4.5), "90a5305e-d7b4-fe3b-b87a-eca56d9d99cc", "8bf8737f-2dda-9465-cba6-0e74ddc436d0");
            yield return new(new V4i(-123456789, 42, -7, 12345), "973cb8db-424c-4e69-2588-6e91d2206d27", "147705a2-7f98-3bcf-4d9b-26795432343a");
            yield return new(new V4l(-12345678901234567L, 1152921504606846977L, -7L, 12345L), "ffd0b05f-0cf4-b539-9013-2675709afb5f", "e86ee5ad-89ff-3fc4-dd1c-de402ae7328f");
            yield return new(new C3b(1, 2, 200), "d928c6f0-05bb-5d80-edbf-25ebde831087", "0c9ffca2-813c-b274-373d-9463c7ffc551");
            yield return new(new C3f(0.25f, 0.5f, 0.75f), "cda5c47e-d78f-75fd-3de0-4073902a89a5", "e082d876-edf5-0e0f-1973-7dc4064b4575");
            yield return new(new C4b(1, 2, 200, 255), "352fc21a-d3da-1752-b617-e6c6d3443473", "d687dcbb-51eb-207a-533e-7074b9dab9f0");
            yield return new(new C4f(0.25f, 0.5f, 0.75f, 1f), "2dbdc84a-7c53-0502-503b-e47d2b19d742", "ffb9a1b8-3e44-ec18-d6e1-d73de2e2dac1");
        }

        [TestCaseSource(nameof(VectorColorGoldens))]
        public void VectorAndColorFingerprintsRemainUnchanged(object value, string scalarGolden, string pairGolden)
        {
            var type = value.GetType();
            Guid Hash(Type inputType, object input) => (Guid)typeof(HashExtensions).GetMethod(nameof(HashExtensions.ComputeMd5Hash), new[] { inputType }).Invoke(null, new[] { input });
            var single = Array.CreateInstance(type, 1); single.SetValue(value, 0);
            var pair = Array.CreateInstance(type, 2); pair.SetValue(value, 0); pair.SetValue(value, 1);
            var enumerableType = typeof(IEnumerable<>).MakeGenericType(type);
            Assert.That(Hash(type, value), Is.EqualTo(new Guid(scalarGolden)));
            Assert.That(Hash(single.GetType(), single), Is.EqualTo(new Guid(scalarGolden)));
            Assert.That(Hash(enumerableType, single), Is.EqualTo(new Guid(scalarGolden)));
            Assert.That(Hash(pair.GetType(), pair), Is.EqualTo(new Guid(pairGolden)));
            Assert.That(Hash(enumerableType, pair), Is.EqualTo(new Guid(pairGolden)));
            Assert.That(Hash(single.GetType(), Array.CreateInstance(type, 0)), Is.EqualTo(EmptyDigest));
            Assert.That(Hash(single.GetType(), null), Is.EqualTo(Guid.Empty));
            Assert.That(Hash(enumerableType, null), Is.EqualTo(Guid.Empty));
        }

        [Test]
        public void HashOfHull3d_Invalid_Equals_Invalid()
        {
            var a = Hull3d.Invalid;
            var b = Hull3d.Invalid;
            ClassicAssert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }

        [Test]
        public void HashOfHull3d_Empty_Equals_Empty()
        {
            var a = new Hull3d(new Plane3d[0]);
            var b = new Hull3d(new Plane3d[0]);
            ClassicAssert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }

        [Test]
        public void HashOfHull3d_DefaultConstructorCreatesInvalidHull3d()
        {
            var a = new Hull3d();
            var b = Hull3d.Invalid;
            ClassicAssert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }

        [Test]
        public void HashOfHull3d_Invalid_NotEquals_Empty()
        {
            var a = Hull3d.Invalid;
            var b = new Hull3d(new Plane3d[0]);
            ClassicAssert.IsTrue(a.ComputeMd5Hash() != b.ComputeMd5Hash());
        }

        [Test]
        public void HashOfHull3d_FromBox_Equals_FromSameBox()
        {
            var a = new Hull3d(new Box3d(new V3d(1, 2, 3), new V3d(2, 3, 4.1)));
            var b = new Hull3d(new Box3d(new V3d(1, 2, 3), new V3d(2, 3, 4.1)));
            ClassicAssert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }

        [Test]
        public void HashOfHull3d_FromBox_NotEquals_FromDifferentBox()
        {
            var a = new Hull3d(new Box3d(new V3d(1, 2, 3), new V3d(2, 3, 4.1)));
            var b = new Hull3d(new Box3d(new V3d(1, 2, 3), new V3d(2, 3, 4.2)));
            ClassicAssert.IsTrue(a.ComputeMd5Hash() != b.ComputeMd5Hash());
        }



        [Test]
        public void HashOfV3fArray_Equals()
        {
            var a = new[] { new V3f(1, 2, 3) };
            var b = new[] { new V3f(1, 2, 3) };
            ClassicAssert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }
        [Test]
        public void HashOfV3fArray_NotEquals()
        {
            var a = new[] { new V3f(1, 2, 3) };
            var b = new[] { new V3f(1, 2, 3), new V3f(1, 2, 3) };
            ClassicAssert.IsTrue(a.ComputeMd5Hash() != b.ComputeMd5Hash());
        }



        [Test]
        public void HashOfC4bArray_Equals()
        {
            var a = new[] { new C4b(1, 2, 3) };
            var b = new[] { new C4b(1, 2, 3) };
            ClassicAssert.IsTrue(a.ComputeMd5Hash() == b.ComputeMd5Hash());
        }
        [Test]
        public void HashC4bArray_NotEquals()
        {
            var a = new[] { new C4b(1, 2, 3) };
            var b = new[] { new C4b(1, 2, 3), new C4b(1, 2, 3) };
            ClassicAssert.IsTrue(a.ComputeMd5Hash() != b.ComputeMd5Hash());
        }
    }
}
