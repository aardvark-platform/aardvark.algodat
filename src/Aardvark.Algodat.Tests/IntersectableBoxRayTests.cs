using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class IntersectableBoxRayTests
{
    private static readonly FastRay3d Diagonal = new(V3d.Zero, V3d.III);
    private static readonly Box3d Near = new(new V3d(2), new V3d(3));
    private static readonly Box3d Far = new(new V3d(4), new V3d(5));
    private static readonly Box3d Miss = new(new V3d(1, 10, 1), new V3d(2, 11, 2));
    private static readonly Func<IIntersectableObjectSet, int, bool> AcceptAll = (_, _) => true;

    // Compute the physical slab interval independently using division, not
    // FastRay3d's reciprocal/flag implementation. The pinned helper excludes
    // tangencies and endpoint-only contacts, but permits a zero-width query
    // strictly inside the physical interval. Parallel slab boundaries are inclusive.
    private static double? Interval(Box3d box, FastRay3d ray, double min, double max)
    {
        var enter = double.NegativeInfinity;
        var exit = double.PositiveInfinity;
        for (var axis = 0; axis < 3; axis++)
        {
            var origin = ray.Ray.Origin[axis];
            var direction = ray.Ray.Direction[axis];
            if (direction == 0)
            {
                if (origin < box.Min[axis] || origin > box.Max[axis]) return null;
            }
            else
            {
                var a = (box.Min[axis] - origin) / direction;
                var b = (box.Max[axis] - origin) / direction;
                enter = Math.Max(enter, Math.Min(a, b));
                exit = Math.Min(exit, Math.Max(a, b));
            }
        }
        return enter < exit && exit > min && enter < max && min <= max ? Math.Max(enter, min) : null;
    }

    private static (int Index, double T) Scan(Box3d[] boxes, int[] indices, FastRay3d ray,
        double min, double max, double cutoff, Func<int, bool> allow = null, Func<int, bool> reject = null)
    {
        var index = -1;
        var t = Math.Min(max, cutoff);
        foreach (var id in indices)
        {
            if (allow != null && !allow(id)) continue;
            var candidate = Interval(boxes[id], ray, min, t);
            if (!candidate.HasValue || (reject != null && reject(id))) continue;
            index = id;
            t = candidate.Value;
        }
        return (index, t);
    }

    private static ObjectRayHit Incoming(double cutoff = double.PositiveInfinity) => new(cutoff)
    {
        RayHit = new RayHit3d(cutoff) { Point = new V3d(-7, -8, -9), Coord = new V2d(0.25, 0.75), Part = 17, BackSide = true },
        SetObject = new SetObject(new EmptyIntersectableObjectSet(), 42),
        Tag = new object(),
        ObjectStack = new List<SetObject> { new(new EmptyIntersectableObjectSet(), 19) }
    };

    private static void Unchanged(ObjectRayHit actual, ObjectRayHit before, string context)
    {
        Assert.That(actual.RayHit, Is.EqualTo(before.RayHit), context);
        Assert.That(actual.SetObject, Is.EqualTo(before.SetObject), context);
        Metadata(actual, before, context);
    }

    private static void Metadata(ObjectRayHit actual, ObjectRayHit before, string context)
    {
        Assert.That(actual.Tag, Is.SameAs(before.Tag), context);
        Assert.That(actual.ObjectStack, Is.SameAs(before.ObjectStack), context);
        if (actual.ObjectStack != null)
        {
            Assert.That(actual.ObjectStack.Count, Is.EqualTo(1), context);
            Assert.That(actual.ObjectStack[0].Index, Is.EqualTo(19), context);
            Assert.That(actual.ObjectStack[0].Set, Is.TypeOf<EmptyIntersectableObjectSet>(), context);
        }
    }

    private static void Payload(RayHit3d actual, FastRay3d ray, double t, string context)
    {
        Assert.That(actual.T, Is.EqualTo(t).Within(1e-12), context);
        Assert.That(actual.Point.ApproximateEquals(ray.Ray.Origin + t * ray.Ray.Direction, 1e-10), Is.True, context);
        Assert.That((actual.Part, actual.Coord, actual.BackSide), Is.EqualTo((0, V2d.Zero, false)), context);
    }

    private static void Result(bool found, ObjectRayHit hit, ObjectRayHit before, IntersectableBoxSet set,
        FastRay3d ray, (int Index, double T) expected, string context, bool tree = false)
    {
        Assert.That(found, Is.EqualTo(expected.Index >= 0), context);
        if (!found) { Unchanged(hit, before, context); return; }
        Assert.That(hit.SetObject.Set, Is.SameAs(set), context);
        Assert.That(hit.SetObject.Index, Is.EqualTo(expected.Index), context);
        Payload(hit.RayHit, ray, expected.T, context);
        Assert.That(hit.Tag, Is.SameAs(before.Tag), context);
        // Existing flat-set kd-tree queries clear the stack after a hit;
        // direct box queries must leave the caller's stack intact.
        if (tree) Assert.That(hit.ObjectStack, Is.Null, context);
        else Metadata(hit, before, context);
    }

    [Test]
    public void MissedBoxesCannotSuppressOrCorruptAnAcceptedHit()
    {
        var boxes = new[] { Far, Miss };
        var set = new IntersectableBoxSet(boxes);
        Assert.Multiple(() =>
        {
            foreach (var order in new[] { new[] { 1, 0 }, new[] { 0, 1 } })
            {
                var hit = ObjectRayHit.MaxRange;
                var found = set.ObjectsIntersectRay(order, 0, order.Length, Diagonal, AcceptAll, null, 0, 100, ref hit);
                var context = string.Join(",", order);
                Assert.That(found, Is.True, context);
                Assert.That(hit.SetObject.Index, Is.EqualTo(0), context);
                Payload(hit.RayHit, Diagonal, 4, context);
            }
        });
    }

    [Test]
    public void ObjectFiltersRespectTheIndexSliceAndNullAcceptsAll()
    {
        var boxes = new[] { Near, Far, Miss };
        var set = new IntersectableBoxSet(boxes);
        var indices = new[] { int.MaxValue, 2, 0, 1, -1 };
        foreach (var mode in new[] { "null", "all", "far-only", "none" })
        {
            var calls = new List<int>();
            Func<int, bool> allow = id => mode == "all" || mode == "null" || (mode == "far-only" && id == 1);
            Func<IIntersectableObjectSet, int, bool> filter = mode == "null" ? null : (s, id) =>
            {
                Assert.That(s, Is.SameAs(set), mode);
                calls.Add(id);
                return allow(id);
            };
            var before = Incoming();
            var hit = before;
            var found = set.ObjectsIntersectRay(indices, 1, 3, Diagonal, filter, null, 0, 100, ref hit);
            Result(found, hit, before, set, Diagonal, Scan(boxes, new[] { 2, 0, 1 }, Diagonal, 0, 100, before.RayHit.T, allow), mode);
            Assert.That(calls, Is.EqualTo(mode == "null" ? Array.Empty<int>() : new[] { 2, 0, 1 }), mode);
            Assert.That(indices, Is.EqualTo(new[] { int.MaxValue, 2, 0, 1, -1 }), mode);
            Assert.That(boxes, Is.EqualTo(new[] { Near, Far, Miss }), mode);
        }
        foreach (var array in new[] { null, Array.Empty<int>(), indices })
        foreach (var first in new[] { -1, 0, int.MaxValue })
        foreach (var count in new[] { -1, 0 })
        {
            var before = Incoming();
            var hit = before;
            Assert.That(set.ObjectsIntersectRay(array, first, count, Diagonal,
                (_, _) => throw new AssertionException("empty slice invoked object filter"),
                (_, _, _, _) => throw new AssertionException("empty slice invoked hit filter"), 0, 100, ref hit), Is.False);
            Unchanged(hit, before, $"empty slice: first={first}, count={count}");
        }
        var unchanged = Incoming();
        var invalidHit = unchanged;
        Assert.Throws<NullReferenceException>(() => set.ObjectsIntersectRay(null, 0, 1, Diagonal, AcceptAll, null, 0, 100, ref invalidHit));
        Unchanged(invalidHit, unchanged, "null index array with nonempty slice");

        // The read-only box view must remain an alias, not a snapshot taken
        // before an object filter changes the caller-owned array.
        var changedHit = Incoming();
        Assert.That(set.ObjectsIntersectRay(new[] { 0 }, 0, 1, Diagonal, (_, id) =>
        {
            boxes[id] = Far;
            return true;
        }, null, 0, 100, ref changedHit), Is.True);
        Payload(changedHit.RayHit, Diagonal, 4, "object filter runs before reading its box");
    }

    [Test]
    public void HitFiltersReceiveCompleteCandidatesBeforeRejectionOrCommit()
    {
        var boxes = new[] { Near, Far, Miss, Near };
        var set = new IntersectableBoxSet(boxes);
        foreach (var order in new[] { new[] { 0, 1, 2, 3 }, new[] { 1, 0, 3, 2 }, new[] { 2, 1, 3, 0 } })
        foreach (var mode in new[] { "reject-near", "reject-all", "accept-all", "reject-tie" })
        {
            var context = $"{mode}, order={string.Join(",", order)}";
            Func<int, bool> reject = id => mode == "reject-all" || (mode == "reject-near" && (id == 0 || id == 3)) || (mode == "reject-tie" && id == 3);
            var expectedCalls = new List<(int Index, double T)>();
            var cutoff = 100.0;
            foreach (var id in order)
            {
                var t = Interval(boxes[id], Diagonal, 0, cutoff);
                if (!t.HasValue) continue;
                expectedCalls.Add((id, t.Value));
                if (!reject(id)) cutoff = t.Value;
            }
            var before = Incoming();
            var hit = before;
            var calls = new List<(int Index, double T)>();
            var found = set.ObjectsIntersectRay(order, 0, order.Length, Diagonal, AcceptAll, (s, id, part, candidate) =>
            {
                Assert.That(s, Is.SameAs(set), context);
                Assert.That(part, Is.EqualTo(0), context);
                Payload(candidate, Diagonal, id == 1 ? 4 : 2, context);
                if (calls.Count == 0 || mode == "reject-all") Unchanged(hit, before, context + ", before acceptance");
                calls.Add((id, candidate.T));
                candidate.T = -123; // Callback receives a value, not mutable retained state.
                candidate.Point = V3d.NaN;
                return reject(id);
            }, 0, 100, ref hit);
            Assert.That(calls, Is.EqualTo(expectedCalls), context);
            Result(found, hit, before, set, Diagonal, Scan(boxes, order, Diagonal, 0, 100, before.RayHit.T, reject: reject), context);
        }
    }

    [Test]
    public void NoAcceptedCandidateOrThrowingFilterLeavesIncomingStateIntact()
    {
        var set = new IntersectableBoxSet(Near, Miss, Far);
        foreach (var mode in new[] { "empty", "miss", "cutoff", "object-reject", "hit-reject", "object-throws", "hit-throws", "late-object-throws", "late-hit-throws" })
        {
            var before = Incoming(mode == "cutoff" ? 1.5 : 100);
            var hit = before;
            var indices = mode == "miss" ? new[] { 1 } : mode.StartsWith("late") ? new[] { 2, 0 } : new[] { 0 };
            var objectCalls = 0;
            var hitCalls = 0;
            Func<IIntersectableObjectSet, int, bool> objectFilter = (_, _) =>
            {
                objectCalls++;
                if (mode == "object-throws" || (mode == "late-object-throws" && objectCalls == 2)) throw new InvalidOperationException("object");
                return mode != "object-reject";
            };
            Func<IIntersectableObjectSet, int, int, RayHit3d, bool> hitFilter = (_, _, _, _) =>
            {
                hitCalls++;
                if (mode == "hit-throws" || (mode == "late-hit-throws" && hitCalls == 2)) throw new InvalidOperationException("hit");
                return mode == "hit-reject";
            };
            bool Query() => set.ObjectsIntersectRay(indices, 0, mode == "empty" ? 0 : indices.Length, Diagonal, objectFilter, hitFilter, 0, 100, ref hit);
            if (mode.EndsWith("throws")) Assert.Throws<InvalidOperationException>(() => Query(), mode);
            else Assert.That(Query(), Is.False, mode);
            Unchanged(hit, before, mode);
        }
    }

    [Test]
    public void AcceptedHitsPreserveCallerTagAndStackWhileReplacingTheRayPayload()
    {
        var set = new IntersectableBoxSet(Far);
        foreach (var tagged in new[] { false, true })
        foreach (var stacked in new[] { false, true })
        foreach (var cutoff in new[] { 4.125, 100.0 })
        {
            var before = Incoming(cutoff);
            if (!tagged) before.Tag = null;
            if (!stacked) before.ObjectStack = null;
            var hit = before;
            var context = $"tagged={tagged}, stacked={stacked}, cutoff={cutoff}";
            var found = set.ObjectsIntersectRay(new[] { 0 }, 0, 1, Diagonal, AcceptAll, null, 0, 100, ref hit);
            Result(found, hit, before, set, Diagonal, (0, 4), context);
        }
    }

    [Test]
    public void ClippedIntervalsParallelRaysAndTiesKeepTheirExistingSemantics()
    {
        var boxes = new[] { new Box3d(new V3d(2), new V3d(4)), new Box3d(new V3d(-1), new V3d(1)), new Box3d(new V3d(2, -1, -1), new V3d(2, 1, 1)) };
        var rays = new[] { Diagonal, new FastRay3d(new V3d(3), -V3d.III), new FastRay3d(V3d.Zero, new V3d(2, 1, 0.5)),
            new FastRay3d(V3d.Zero, V3d.IOO), new FastRay3d(new V3d(2), V3d.OIO), new FastRay3d(new V3d(5), -V3d.OOI),
            new FastRay3d(new V3d(3), V3d.Zero), new FastRay3d(new V3d(0, 1, -1), V3d.IOO) };
        foreach (var box in boxes)
        foreach (var ray in rays)
        foreach (var range in new[] { (-5.0, 100.0), (0.0, 100.0), (2.5, 3.5), (2.0, 2.0), (4.0, 4.0), (4.125, 5.0), (3.0, 2.0) })
        foreach (var cutoff in new[] { 1.875, 2.0, 4.0, double.PositiveInfinity })
        {
            var set = new IntersectableBoxSet(box);
            var hit = ObjectRayHit.MaxRange;
            hit.RayHit.T = cutoff;
            var expected = Interval(box, ray, range.Item1, Math.Min(range.Item2, cutoff));
            var context = $"box={box}, ray={ray.Ray}, range={range}, cutoff={cutoff}";
            var found = set.ObjectsIntersectRay(new[] { 0 }, 0, 1, ray, AcceptAll, null, range.Item1, range.Item2, ref hit);
            Assert.That(found, Is.EqualTo(expected.HasValue), context);
            if (found) Payload(hit.RayHit, ray, expected.Value, context);
            else Assert.That(hit.RayHit.T, Is.EqualTo(cutoff), context);
        }
        var ties = new IntersectableBoxSet(Near, Near);
        foreach (var order in new[] { new[] { 0, 1 }, new[] { 1, 0 } })
        {
            foreach (var min in new[] { 0.0, 2.5 })
            {
                var hit = new ObjectRayHit(100);
                Assert.That(ties.ObjectsIntersectRay(order, 0, 2, Diagonal, AcceptAll, null, min, 100, ref hit), Is.True);
                Assert.That(hit.SetObject.Index, Is.EqualTo(order[min == 0 ? 0 : 1]), $"min={min}, order={string.Join(",", order)}");
                Payload(hit.RayHit, Diagonal, Math.Max(2, min), "surface ties keep first; clipped interior ties keep last");
            }
        }
    }

    [Test]
    public void DirectAndBuiltKdTreeQueriesMatchIndependentIntervals()
    {
        var grid = (from x in Enumerable.Range(0, 4) from y in Enumerable.Range(0, 4) from z in Enumerable.Range(0, 4)
                    let min = new V3d(4 * x + 1, 4 * y + 1, 4 * z + 1) select new Box3d(min, min + V3d.III)).ToArray();
        var random = new Random(17031);
        var rays = new List<FastRay3d> { Diagonal, new(new V3d(1.5), V3d.IOO), new(new V3d(16), -V3d.III) };
        for (var i = 0; i < 64; i++)
            rays.Add(new FastRay3d(new V3d(random.Next(-4, 20) + 0.25, random.Next(-4, 20) + 0.25, random.Next(-4, 20) + 0.25),
                new V3d(random.Next(-2, 3), random.Next(-2, 3), 1)));
        foreach (var boxes in new[] { new[] { Miss, Far }, new[] { Far, Miss }, grid })
        {
            var set = new IntersectableBoxSet(boxes);
            var tree = new KdIntersectionTree(set, KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading);
            if (boxes.Length > 7) Assert.That(tree.Tree, Is.Not.TypeOf<KdLeaf>(), "fixture must exercise actual subdivision");
            var indices = Enumerable.Range(0, boxes.Length).Reverse().ToArray();
            foreach (var flattened in new[] { false, true })
            {
                if (flattened) tree.Flatten();
                foreach (var ray in rays)
                foreach (var mode in new[] { "default", "all", "selective", "reject-near", "reject-all" })
                foreach (var cutoff in new[] { 3.0, 100.0 })
                {
                    Func<int, bool> allow = id => mode != "selective" || id % 3 != 2;
                    Func<int, bool> reject = id => mode == "reject-all" || (mode == "reject-near" && id % 5 == 0);
                    Func<IIntersectableObjectSet, int, bool> objectFilter = mode == "default" ? null : (_, id) => allow(id);
                    Func<IIntersectableObjectSet, int, int, RayHit3d, bool> hitFilter = mode.StartsWith("reject") ? (_, id, _, _) => reject(id) : null;
                    var expected = Scan(boxes, indices, ray, 0, 100, cutoff, allow, reject);
                    var before = Incoming(cutoff);
                    var direct = before;
                    var accelerated = before;
                    var context = $"count={boxes.Length}, flattened={flattened}, ray={ray.Ray}, mode={mode}, cutoff={cutoff}";
                    var found = set.ObjectsIntersectRay(indices, 0, indices.Length, ray, objectFilter, hitFilter, 0, 100, ref direct);
                    Result(found, direct, before, set, ray, expected, context + ", direct");
                    var treeFound = mode == "default" ? tree.Intersect(ray, 0, 100, ref accelerated)
                        : tree.Intersect(ray, objectFilter, hitFilter, 0, 100, ref accelerated);
                    Result(treeFound, accelerated, before, set, ray, expected, context + ", tree", tree: true);
                }
            }
        }
    }

    [Test]
    public void WarmedDirectScansDoNotAllocateReplacementStacks()
    {
        var set = new IntersectableBoxSet(Far, Miss);
        var indices = new[] { 0, 1 };
        var before = Incoming();
        foreach (var filter in new[] { AcceptAll, null })
        foreach (var hitFilter in new Func<IIntersectableObjectSet, int, int, RayHit3d, bool>[] { null, (_, _, _, _) => false, (_, _, _, _) => true })
        foreach (var cutoff in new[] { 0.5, 100.0 })
        {
            void Query()
            {
                var hit = before;
                set.ObjectsIntersectRay(indices, 0, 2, Diagonal, filter, hitFilter, 0, cutoff, ref hit);
            }
            for (var i = 0; i < 1024; i++) Query();
            var start = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1024; i++) Query();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            Assert.That(allocated, Is.Zero, $"object-filter={filter != null}, hit-filter={hitFilter != null}, cutoff={cutoff}");
        }
    }
}
