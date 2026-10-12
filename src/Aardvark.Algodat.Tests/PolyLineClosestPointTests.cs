using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyLineClosestPointTests
    {
        private static PolyLine Fixture(V3d offset) => new PolyLine
        {
            PositionArray = new[]
            {
                new V3d(1, -1, 0) + offset, new V3d(1, 1, 0) + offset,
                new V3d(5, 1, 0) + offset, new V3d(5, -1, 0) + offset
            }
        };

        private static readonly (int[] Indices, int First, int Count)[] Domains =
        {
            (new[] { 0, 1, 2 }, 0, 3),
            (new[] { 2, 1, 0 }, 0, 3),
            (new[] { int.MaxValue, 2, 0, 1, -1 }, 1, 3),
            (new[] { int.MaxValue, 2, 0, -1 }, 1, 2),
            (new[] { int.MaxValue, 2, -1 }, 1, 1),
            (new[] { int.MaxValue, -1 }, 1, 0)
        };

        private static ObjectClosestPoint Incoming(PolyLine line, double distance) => new ObjectClosestPoint
        {
            Distance = distance, DistanceSquared = distance * distance,
            Point = new V3d(-7, -8, -9), Coord = new V2d(.125, .75),
            SetObject = new SetObject(line, 99),
            ObjectStack = new List<SetObject> { new SetObject(line, 42) }
        };

        // Independent clamped scalar projection, not the library's closest-point helper.
        private static (V3d Point, double Squared) Segment(V3d a, V3d b, V3d query)
        {
            var x = b.X - a.X; var y = b.Y - a.Y; var z = b.Z - a.Z;
            var t = ((query.X - a.X) * x + (query.Y - a.Y) * y + (query.Z - a.Z) * z)
                / (x * x + y * y + z * z);
            t = Math.Max(0, Math.Min(1, t));
            var point = new V3d(a.X + t * x, a.Y + t * y, a.Z + t * z);
            x = query.X - point.X; y = query.Y - point.Y; z = query.Z - point.Z;
            return (point, x * x + y * y + z * z);
        }

        private static (int Index, V3d Point, double Squared) Oracle(
            PolyLine line, IEnumerable<int> indices, V3d query, double cutoff, Func<int, bool> includes)
        {
            var result = (Index: -1, Point: V3d.Zero, Squared: cutoff);
            foreach (var i in indices)
            {
                if (!includes(i)) continue;
                var segment = Segment(line.PositionArray[i], line.PositionArray[i + 1], query);
                if (segment.Squared < result.Squared) result = (i, segment.Point, segment.Squared);
            }
            return result;
        }

        private static void Check(
            bool found, ObjectClosestPoint actual, ObjectClosestPoint before, PolyLine line,
            (int Index, V3d Point, double Squared) expected, string context)
        {
            Assert.That(found, Is.EqualTo(expected.Index >= 0), context + ": found");
            if (expected.Index < 0)
            {
                Assert.That(actual, Is.EqualTo(before), context + ": rejected result");
            }
            else
            {
                Assert.That(double.IsFinite(actual.Distance) && double.IsFinite(actual.DistanceSquared)
                    && double.IsFinite(actual.Point.X) && double.IsFinite(actual.Point.Y)
                    && double.IsFinite(actual.Point.Z), Is.True, context + ": finite result");
                Assert.That(actual.SetObject.Set, Is.SameAs(line), context + ": set");
                Assert.That(actual.SetObject.Index, Is.EqualTo(expected.Index), context + ": original index");
                Assert.That(actual.Point.ApproximateEquals(expected.Point, 1e-12), Is.True, context + ": point");
                Assert.That(actual.DistanceSquared, Is.EqualTo(expected.Squared).Within(1e-10), context + ": squared distance");
                Assert.That(actual.Distance, Is.EqualTo(Math.Sqrt(expected.Squared)).Within(1e-12), context + ": distance");
            }
            Assert.That(actual.Coord, Is.EqualTo(before.Coord), context + ": coordinates");
            Assert.That(actual.ObjectStack, Is.SameAs(before.ObjectStack), context + ": stack identity");
            Assert.That(actual.ObjectStack.Select(s => s.Index), Is.EqualTo(new[] { 42 }), context + ": stack contents");
        }

        [Test]
        public void DirectInclusionFiltersUseOriginalIndicesAndPreserveRejectedResults()
        {
            Assert.Multiple(() =>
            {
                foreach (var offset in new[] { V3d.Zero, new V3d(8, -4, 2) })
                foreach (var domain in Domains)
                foreach (var mask in new[] { 4, 0, 2, 1, 5 })
                foreach (var cutoff in new[] { .5, 1, Math.BitIncrement(1), 5, Math.BitIncrement(5), 10 })
                {
                    var line = Fixture(offset); var before = Incoming(line, cutoff); var closest = before;
                    var indices = domain.Indices.Skip(domain.First).Take(domain.Count).ToArray();
                    var visits = new List<int>();
                    Func<int, bool> includes = i => (mask & (1 << i)) != 0;
                    var expected = Oracle(line, indices, offset, before.DistanceSquared, includes);
                    var context = $"offset={offset}, indices=[{string.Join(",", indices)}], mask={mask}, cutoff={cutoff:R}";
                    var found = line.ClosestPoint(domain.Indices, domain.First, domain.Count, offset,
                        (objects, index) =>
                        {
                            Assert.That(objects, Is.SameAs(line), context + ": callback set");
                            visits.Add(index); return includes(index);
                        }, null, ref closest);
                    Check(found, closest, before, line, expected, context);
                    Assert.That(visits, Is.EqualTo(indices), context + ": callback indices/order");
                    if (mask == 4 && cutoff == 10 && indices.Contains(2))
                    {
                        Assert.That(found, Is.True, context + ": distance-five reproduction");
                        Assert.That(closest.Distance, Is.EqualTo(5), context + ": distance-five reproduction");
                        Assert.That(closest.Point, Is.EqualTo(offset + new V3d(5, 0, 0)), context + ": literal point");
                    }
                }
            });
        }

        [Test]
        public void RejectedSegmentsAreSkippedBeforeReadingGeometry()
        {
            var line = Fixture(V3d.Zero); var before = Incoming(line, 10); var closest = before;
            var indices = new[] { -1, 2, int.MaxValue }; var visits = new List<int>();
            var found = line.ClosestPoint(indices, 0, indices.Length, V3d.Zero,
                (objects, index) =>
                {
                    Assert.That(objects, Is.SameAs(line), $"callback index={index}");
                    visits.Add(index); return index == 2;
                }, null, ref closest);
            Assert.Multiple(() =>
            {
                Check(found, closest, before, line, (2, new V3d(5, 0, 0), 25), "rejected geometry sentinels");
                Assert.That(visits, Is.EqualTo(indices), "callback must run before vertex lookup");
            });
        }

        [Test]
        public void NullAndAcceptAllKeepStrictCutoffsAndIgnorePointResultFilters()
        {
            var line = Fixture(V3d.Zero);
            Assert.Multiple(() =>
            {
                foreach (var query in new[] { V3d.Zero, new V3d(3, 0, 0), new V3d(1, 3, .25) })
                foreach (var domain in Domains)
                foreach (var cutoff in new[] { 0, 1, Math.BitIncrement(1), 5, 10 })
                foreach (var withFilter in new[] { false, true })
                {
                    var before = Incoming(line, cutoff); var closest = before; var pointCalls = 0;
                    var indices = domain.Indices.Skip(domain.First).Take(domain.Count).ToArray();
                    var expected = Oracle(line, indices, query, before.DistanceSquared, _ => true);
                    var context = $"query={query}, indices=[{string.Join(",", indices)}], cutoff={cutoff:R}, acceptAll={withFilter}";
                    Func<IIntersectableObjectSet, int, bool> filter = withFilter ? (_, _) => true : null;
                    var found = line.ClosestPoint(domain.Indices, domain.First, domain.Count, query, filter,
                        (_, _, _, _) => { pointCalls++; throw new InvalidOperationException("Point-result filter remains ignored"); },
                        ref closest);
                    Check(found, closest, before, line, expected, context);
                    Assert.That(pointCalls, Is.Zero, context + ": point-result filter");
                }
            });
        }

        [Test]
        public void SubdividedTreesMatchIndependentFilteredSegmentDistances()
        {
            var line = new PolyLine
            {
                PositionArray = Enumerable.Range(0, 64).SelectMany(i => new[]
                {
                    new V3d(4 * i, -1, 0), new V3d(4 * i, 1, 0)
                }).ToArray()
            };
            var tree = new KdIntersectionTree(line,
                KdIntersectionTree.BuildFlags.Default | KdIntersectionTree.BuildFlags.NoMultithreading);
            Assert.That(tree.Tree.Leafs().Count(), Is.GreaterThan(1), "genuinely subdivided fixture");
            Assert.Multiple(() =>
            {
                foreach (var column in new[] { 0, 7, 31, 63 })
                foreach (var mode in new[] { "last", "none", "even", "last-four", "null", "all" })
                foreach (var cutoff in new[] { 0, .5, 2, 6, 1000 })
                {
                    var query = new V3d(4 * column - .25, .125, .5);
                    Func<int, bool> includes = i => mode == "last" ? i == line.ObjectCount - 1
                        : mode == "none" ? false : mode == "even" ? i % 2 == 0
                        : mode == "last-four" ? i >= line.ObjectCount - 4 : true;
                    var before = Incoming(line, cutoff); var closest = before; var visits = 0;
                    var expected = Oracle(line, Enumerable.Range(0, line.ObjectCount), query, before.DistanceSquared, includes);
                    var context = $"column={column}, mode={mode}, cutoff={cutoff:R}";
                    Func<IIntersectableObjectSet, int, bool> filter = mode == "null" ? null : (objects, index) =>
                    {
                        Assert.That(objects, Is.SameAs(line), context + ": callback set");
                        Assert.That(index, Is.InRange(0, line.ObjectCount - 1), context + ": original callback index");
                        visits++; return includes(index);
                    };
                    var found = tree.ClosestPoint(query, filter,
                        (_, _, _, _) => throw new InvalidOperationException("Point-result filter remains ignored"), ref closest);
                    Check(found, closest, before, line, expected, context);
                    if (cutoff == 1000 && mode != "null") Assert.That(visits, Is.GreaterThan(0), context + ": callback invoked");
                }
            });
        }
    }
}
