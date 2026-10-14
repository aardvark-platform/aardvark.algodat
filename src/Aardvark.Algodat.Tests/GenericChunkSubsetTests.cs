/*
    Copyright (C) 2026. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program. If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class GenericChunkSubsetTests
    {
        private static readonly Durable.Def Tokens = new Durable.Def(
            new Guid("41c49d98-46c6-4053-b235-59a1999242bf"), "Tests.Subset.Tokens", "Point tokens",
            Durable.Primitives.StringUTF8Array.Id, true);
        private static readonly Durable.Def[] PositionDefs = {
            GenericChunk.Defs.Positions2f, GenericChunk.Defs.Positions2d,
            GenericChunk.Defs.Positions3f, GenericChunk.Defs.Positions3d
        };

        private static V3d[] Points(int count) => Enumerable.Range(0, count)
            .Select(i => new V3d(i * .125 - 7, i % 7 - 3.25, 11 - i * .25)).ToArray();

        private static GenericChunk Create(int kind, V3d[] points, Box3d? bounds = null)
        {
            Array positions = kind switch {
                0 => points.Select(p => new V2f((float)p.X, (float)p.Y)).ToArray(),
                1 => points.Select(p => new V2d(p.X, p.Y)).ToArray(),
                2 => points.Select(p => new V3f((float)p.X, (float)p.Y, (float)p.Z)).ToArray(),
                _ => points.ToArray()
            };
            var n = points.Length;
            return new GenericChunk(new Dictionary<Durable.Def, object> {
                [PositionDefs[kind]] = positions,
                [GenericChunk.Defs.Colors4b] = Enumerable.Range(0, n).Select(i => new C4b(i % 256, (i * 7) % 256, (i * 13) % 256, 255)).ToArray(),
                [GenericChunk.Defs.Normals3f] = Enumerable.Range(0, n).Select(i => new V3f(i, -i, 1)).ToArray(),
                [GenericChunk.Defs.Intensities1i] = Enumerable.Range(0, n).ToArray(),
                [GenericChunk.Defs.Classifications1b] = Enumerable.Range(0, n).Select(i => (byte)(i % 31)).ToArray(),
                [Tokens] = Enumerable.Range(0, n).Select(i => "point-" + i).ToArray()
            }, bounds);
        }

        // The original Subset oracle converts to doubles before the IEnumerable box constructor.
        // Keep this separate from the selected-array accumulation used by the implementation.
        private static V3d DoublePoint(Array points, int i) => points switch {
            V2f[] ps => new V3d((double)ps[i].X, (double)ps[i].Y, 0.0),
            V2d[] ps => new V3d(ps[i].X, ps[i].Y, 0.0),
            V3f[] ps => new V3d((double)ps[i].X, (double)ps[i].Y, (double)ps[i].Z),
            V3d[] ps => ps[i],
            _ => throw new ArgumentException(nameof(points))
        };

        private static void AssertVectorBits(V3d actual, V3d expected, string context)
        {
            Assert.That(BitConverter.DoubleToInt64Bits(actual.X), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.X)), context + ": X bits");
            Assert.That(BitConverter.DoubleToInt64Bits(actual.Y), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.Y)), context + ": Y bits");
            Assert.That(BitConverter.DoubleToInt64Bits(actual.Z), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.Z)), context + ": Z bits");
        }

        private static void AssertBoxBits(Box3d actual, Box3d expected, string context)
        {
            AssertVectorBits(actual.Min, expected.Min, context + " minimum");
            AssertVectorBits(actual.Max, expected.Max, context + " maximum");
        }

        private static void AssertSubset(GenericChunk source, List<int> selection, GenericChunk result, string context)
        {
            var positions = (Array)source.Positions;
            var expected = source.Count == 0 ? source.BoundingBox
                : new Box3d(selection.Select(i => DoublePoint(positions, i)));
            Assert.That(result.Count, Is.EqualTo(selection.Count), context + ": count");
            Assert.That(result.PositionsDef, Is.EqualTo(source.PositionsDef), context + ": position definition");
            Assert.That(result.Data.Keys, Is.EquivalentTo(source.Data.Keys), context + ": attribute keys");
            AssertBoxBits(result.BoundingBox, expected, context);
            foreach (var kv in source.Data)
            {
                var original = (Array)kv.Value;
                var selected = (Array)result.Data[kv.Key];
                Assert.That(selected.GetType(), Is.EqualTo(original.GetType()), context + ": " + kv.Key.Name);
                Assert.That(selected, Is.Not.SameAs(original), context + ": owned " + kv.Key.Name);
                for (var i = 0; i < selection.Count; i++)
                    if (kv.Key == source.PositionsDef)
                        AssertVectorBits(DoublePoint(selected, i), DoublePoint(original, selection[i]), context + $": point {i}, source {selection[i]}");
                    else
                        Assert.That(selected.GetValue(i), Is.EqualTo(original.GetValue(selection[i])), context + $": {kv.Key.Name}, index {i}");
            }
        }

        [Test]
        public void SubsetsPreserveRepresentationsAttributesOrderAndOwnership()
        {
            for (var kind = 0; kind < 4; kind++)
            {
                var source = Create(kind, Points(9), new Box3d(new V3d(-100), new V3d(200)));
                var snapshots = source.Data.ToDictionary(kv => kv.Key, kv => (Array)((Array)kv.Value).Clone());
                var sourceBounds = source.BoundingBox;
                foreach (var indices in new[] { new int[0], new[] { 7 }, new[] { 8, 1, 8, 0, 4 }, Enumerable.Range(0, 9).Reverse().ToArray() })
                {
                    var selection = indices.ToList();
                    var context = $"{PositionDefs[kind].Name}, [{string.Join(",", indices)}]";
                    var result = source.Subset(selection);
                    AssertSubset(source, selection, result, context);
                    if (indices.Length > 0)
                        foreach (var kv in result.Data)
                        {
                            var array = (Array)kv.Value;
                            array.SetValue(Array.CreateInstance(array.GetType().GetElementType(), 1).GetValue(0), 0);
                        }
                    foreach (var kv in source.Data)
                        Assert.That((Array)kv.Value, Is.EqualTo(snapshots[kv.Key]), context + ": source immutability");
                    Assert.That(selection, Is.EqualTo(indices), context + ": selection immutability");
                    AssertBoxBits(source.BoundingBox, sourceBounds, context + ": source bounds");
                }
                foreach (var indices in new[] { new[] { -1 }, new[] { source.Count }, new[] { 0, int.MaxValue }, new[] { 8, 0, -1 } })
                    Assert.Throws<IndexOutOfRangeException>(() => source.Subset(indices.ToList()), $"kind={kind}, invalid=[{string.Join(",", indices)}]");
            }
        }

        [Test]
        public void BoundsRetainDoublePrecisionIeeeAndEmptySourceSemantics()
        {
            var nan = BitConverter.Int64BitsToDouble(0x7ff8000000000123);
            var points = new[] {
                new V3d(-0.0, +0.0, -0.0), new V3d(+0.0, -0.0, +0.0),
                new V3d(float.MaxValue, float.MinValue, float.MaxValue),
                new V3d(float.MinValue, float.MaxValue, float.MinValue),
                new V3d(nan, nan, nan), new V3d(double.PositiveInfinity), new V3d(double.NegativeInfinity),
                new V3d(double.MaxValue, double.MinValue, double.MaxValue),
                new V3d(nan, -2, 3), new V3d(1, nan, -3), new V3d(-1, 2, nan)
            };
            for (var kind = 0; kind < 4; kind++)
            {
                var source = Create(kind, points);
                var selections = Enumerable.Range(0, points.Length).Select(i => new List<int> { i }).ToList();
                selections.Add(new List<int>());
                selections.Add(Enumerable.Range(0, points.Length).ToList());
                selections.Add(Enumerable.Range(0, points.Length).Reverse().ToList());
                selections.Add(new List<int> { 0, 1, 0 });
                selections.Add(new List<int> { 1, 0, 1 });
                foreach (var selection in selections)
                    AssertSubset(source, selection, source.Subset(selection), $"kind={kind}, IEEE=[{string.Join(",", selection)}]");
                foreach (var bounds in new[] { Box3d.Invalid, new Box3d(new V3d(-9, -8, -7), new V3d(6, 5, 4)),
                    new Box3d(new V3d(-0.0, nan, double.NegativeInfinity), new V3d(+0.0, nan, double.PositiveInfinity)) })
                {
                    var empty = Create(kind, Array.Empty<V3d>(), bounds);
                    AssertSubset(empty, new List<int>(), empty.Subset(new List<int>()), $"kind={kind}, empty source, explicit bounds={bounds}");
                    Assert.Throws<IndexOutOfRangeException>(() => empty.Subset(new List<int> { 0 }), $"kind={kind}, invalid empty-source index");
                }
            }
        }

        [Test]
        public void GroupByKeepsPartitionsAndAllAttributeAlignment()
        {
            for (var kind = 0; kind < 4; kind++)
            {
                var source = Create(kind, Points(97));
                var calls = new List<int>();
                var groups = source.GroupBy((chunk, index) => {
                    Assert.That(chunk, Is.SameAs(source), $"kind={kind}, selector source");
                    calls.Add(index);
                    return (((int[])chunk.Data[GenericChunk.Defs.Intensities1i])[index] * 17 + 3) % 7;
                });
                Assert.That(calls, Is.EqualTo(Enumerable.Range(0, source.Count)), $"kind={kind}, selector order");
                Assert.That(groups.Keys, Is.EquivalentTo(Enumerable.Range(0, 7)), $"kind={kind}, keys");
                foreach (var group in groups)
                {
                    var selection = Enumerable.Range(0, source.Count).Where(i => (i * 17 + 3) % 7 == group.Key).ToList();
                    AssertSubset(source, selection, group.Value, $"kind={kind}, group={group.Key}");
                }
                Assert.That(groups.Values.SelectMany(c => (int[])c.Data[GenericChunk.Defs.Intensities1i]).OrderBy(i => i),
                    Is.EqualTo(Enumerable.Range(0, source.Count)), $"kind={kind}, disjoint complete partition");
                Assert.That(Create(kind, Array.Empty<V3d>()).GroupBy<int>((_, _) => throw new Exception("Empty selector called")), Is.Empty, $"kind={kind}, empty grouping");
            }
        }

        private static long Allocated(GenericChunk source, List<int> selection)
        {
            GenericChunk result = null;
            for (var i = 0; i < 16; i++) result = source.Subset(selection);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 64; i++) result = source.Subset(selection);
            var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(result);
            return bytes;
        }

        [Test]
        public void FixedSelectionsAllocateIndependentlyOfSourceSize()
        {
            Assert.Multiple(() => {
                for (var kind = 0; kind < 4; kind++)
                {
                    var small = Create(kind, Points(16));
                    var large = Create(kind, Points(65536));
                    foreach (var selection in new[] { new List<int>(), new List<int> { 7 }, new List<int> { 0, 7, 7, 3 } })
                        Assert.That(Allocated(large, selection), Is.EqualTo(Allocated(small, selection)),
                            $"kind={kind}, selected=[{string.Join(",", selection)}]: allocation must depend on selection, not source size (V3d is the direct-source control)");
                }
            });
        }
    }
}
