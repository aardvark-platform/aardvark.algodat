using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class IntersectableTriangleRayTests
{
    private static readonly FastRay3d Up = new(new V3d(.25, .25, 0), V3d.OOI);
    private static readonly float[] Heights = { 2, 5, 8, 2 };
    private static readonly Func<IIntersectableObjectSet, int, int, RayHit3d, bool> AcceptHit = (_, _, _, _) => false;

    private static IntersectableTriangleSet Create(bool indexed, V3f[] positions)
    {
        positions = positions.ToArray();
        if (!indexed) return new IntersectableTriangleSet(positions);
        Array.Reverse(positions);
        return new IntersectableTriangleSet(Enumerable.Range(0, positions.Length).Reverse().ToArray(), positions);
    }
    private static IntersectableTriangleSet Create(bool indexed) => Create(indexed,
        Heights.SelectMany(z => new[] { new V3f(0, 0, z), new V3f(2, 0, z), new V3f(0, 2, z) }).ToArray());

    private static ObjectRayHit Incoming(IntersectableTriangleSet set, double t = 20) => new()
    {
        RayHit = new RayHit3d { T = t, Point = new V3d(-7, -8, -9), Coord = new V2d(.25, .75), Part = 17, BackSide = true },
        SetObject = new SetObject(set, 42), Tag = new object(),
        ObjectStack = new List<SetObject> { new(set, 19) }
    };
    private static void Unchanged(ObjectRayHit actual, ObjectRayHit before, string context)
    {
        Assert.That(actual.RayHit, Is.EqualTo(before.RayHit), context);
        Assert.That(actual.SetObject.Set, Is.SameAs(before.SetObject.Set), context);
        Assert.That(actual.SetObject.Index, Is.EqualTo(before.SetObject.Index), context);
        Assert.That(actual.Tag, Is.SameAs(before.Tag), context);
        Assert.That(actual.ObjectStack, Is.SameAs(before.ObjectStack), context);
        Assert.That(before.ObjectStack.Count, Is.EqualTo(1), context);
        Assert.That(before.ObjectStack[0].Index, Is.EqualTo(19), context);
    }
    private static void Payload(RayHit3d hit, V3d point, double t, string context)
    {
        Assert.That(double.IsFinite(hit.T) && double.IsFinite(hit.Point.X) &&
            double.IsFinite(hit.Point.Y) && double.IsFinite(hit.Point.Z), Is.True, context);
        Assert.That(hit.T, Is.EqualTo(t), context);
        Assert.That(hit.Point, Is.EqualTo(point), context);
        Assert.That(hit.Part, Is.Zero, context);
        Assert.That(hit.Coord, Is.EqualTo(V2d.Zero), context);
        Assert.That(hit.BackSide, Is.False, context);
    }
    private static void Accepted(ObjectRayHit hit, ObjectRayHit before, IntersectableTriangleSet set,
        int id, FastRay3d ray, double t, string context, bool tree = false)
    {
        Payload(hit.RayHit, ray.Ray.Origin + t * ray.Ray.Direction, t, context);
        Assert.That(hit.SetObject.Set, Is.SameAs(set), context);
        Assert.That(hit.SetObject.Index, Is.EqualTo(id), context);
        Assert.That(hit.Tag, Is.SameAs(before.Tag), context);
        if (tree) Assert.That(hit.ObjectStack, Is.Null, $"Existing flat-tree stack clearing: {context}");
        else Assert.That(hit.ObjectStack, Is.SameAs(before.ObjectStack), context);
        Assert.That(before.ObjectStack.Count, Is.EqualTo(1), context);
        Assert.That(before.ObjectStack[0].Index, Is.EqualTo(19), context);
    }

    [Test]
    public void HitFiltersReceiveCompleteCandidatesBeforeNearestAcceptedCommit() => Assert.Multiple(() =>
    {
        foreach (var indexed in new[] { false, true })
        foreach (var order in new[] { new[] { 0, 1, 2 }, new[] { 2, 1, 0 }, new[] { 1, 0, 2 }, new[] { 0, 2, 1 } })
        foreach (var mode in new[] { "null", "accept", "reject near", "reject all" })
        {
            var set = Create(indexed);
            var hit = Incoming(set);
            var before = hit;
            var seen = new List<int>();
            bool Reject(int id) => mode == "reject all" || (mode == "reject near" && Heights[id] == 2);
            var expectedSeen = new List<int>();
            var expected = -1;
            var limit = before.RayHit.T;
            foreach (var id in order)
            {
                // Independent intersections: these planes are at the stored Z heights,
                // and the ray's XY lies strictly within each right triangle.
                var t = Heights[id];
                if (t < 0 || t >= limit) continue;
                expectedSeen.Add(id);
                if (Reject(id)) continue;
                expected = id;
                limit = t;
            }
            var context = $"indexed={indexed}, order={string.Join(',', order)}, mode={mode}";
            bool Filter(IIntersectableObjectSet objects, int id, int part, RayHit3d candidate)
            {
                Assert.That(objects, Is.SameAs(set), context);
                Assert.That(part, Is.Zero, context);
                Payload(candidate, new V3d(.25, .25, Heights[id]), Heights[id], context);
                Unchanged(hit, before, $"No tentative publication: {context}");
                seen.Add(id);
                return Reject(id);
            }
            var found = set.ObjectsIntersectRay(order, 0, order.Length, Up, null,
                mode == "null" ? null : Filter, 0, 20, ref hit);
            Assert.That(found, Is.EqualTo(expected >= 0), context);
            Assert.That(seen, Is.EqualTo(mode == "null" ? Array.Empty<int>() : expectedSeen.ToArray()), context);
            if (found) Accepted(hit, before, set, expected, Up, limit, context);
            else Unchanged(hit, before, context);
        }
    });

    [Test]
    public void ObjectFiltersAndIndexSlicesRetainTheirInclusionConvention()
    {
        foreach (var indexed in new[] { false, true })
        foreach (var mode in new[] { "null", "all", "far only", "none" })
        {
            var set = Create(indexed);
            var hit = Incoming(set);
            var before = hit;
            var candidates = new[] { int.MaxValue, 1, 0, 2, -1 };
            var seen = new List<int>();
            var context = $"indexed={indexed}, object filter={mode}";
            bool Include(IIntersectableObjectSet objects, int id)
            {
                Assert.That(objects, Is.SameAs(set), context);
                seen.Add(id);
                return mode == "all" || (mode == "far only" && id == 1);
            }
            var found = set.ObjectsIntersectRay(candidates, 1, 3, Up,
                mode == "null" ? null : Include, null, 0, 20, ref hit);
            Assert.That(found, Is.EqualTo(mode != "none"), context);
            Assert.That(seen, Is.EqualTo(mode == "null" ? Array.Empty<int>() : new[] { 1, 0, 2 }), context);
            if (found) Accepted(hit, before, set, mode == "far only" ? 1 : 0, Up, mode == "far only" ? 5 : 2, context);
            else Unchanged(hit, before, context);
        }
    }

    [Test]
    public void IntervalsMissesAndTiesKeepExistingStrictUpperBounds()
    {
        foreach (var indexed in new[] { false, true })
        foreach (var acceptAll in new[] { false, true })
        foreach (var (min, max, bound) in new[] { (0.0, 20.0, 20.0), (2.0, 20.0, 20.0),
            (0.0, 2.0, 20.0), (0.0, 20.0, 2.0), (2.0, 2.0, 20.0), (3.0, 8.0, 20.0), (6.0, 5.0, 20.0) })
        {
            var set = Create(indexed);
            var order = new[] { 3, 0, 1, 2 }; // Equal nearest triangles retain the first eligible one.
            var hit = Incoming(set, bound);
            var before = hit;
            var limit = Math.Min(max, bound);
            var expected = -1;
            foreach (var id in order)
                if (Heights[id] >= min && Heights[id] < limit) { expected = id; limit = Heights[id]; }
            var context = $"indexed={indexed}, acceptAll={acceptAll}, interval=[{min},{max}), bound={bound}";
            var found = set.ObjectsIntersectRay(order, 0, order.Length, Up, null,
                acceptAll ? AcceptHit : null, min, max, ref hit);
            Assert.That(found, Is.EqualTo(expected >= 0), context);
            if (found) Accepted(hit, before, set, expected, Up, limit, context);
            else Unchanged(hit, before, context);
        }
        foreach (var indexed in new[] { false, true })
        {
            var set = Create(indexed);
            foreach (var ray in new[] { new FastRay3d(new V3d(.25, .25, 0), 2 * V3d.OOI),
                new FastRay3d(new V3d(.25, .25, 10), -2 * V3d.OOI) })
            {
                var order = new[] { 3, 0, 1, 2 };
                var hit = Incoming(set);
                var before = hit;
                var nearest = 20.0;
                var expected = -1;
                foreach (var id in order)
                {
                    var t = (Heights[id] - ray.Ray.Origin.Z) / ray.Ray.Direction.Z;
                    if (t >= 0 && t < nearest) { nearest = t; expected = id; }
                }
                Assert.That(set.ObjectsIntersectRay(order, 0, 4, ray, null, AcceptHit, 0, 20, ref hit), Is.True);
                Accepted(hit, before, set, expected, ray, nearest, $"Nonunit/reverse direction, indexed={indexed}");
            }
            foreach (var ray in new[] { new FastRay3d(new V3d(4, 4, 0), V3d.OOI),
                new FastRay3d(new V3d(.25, .25, 0), V3d.IOO) })
            {
                var hit = Incoming(set);
                var before = hit;
                Assert.That(set.ObjectsIntersectRay(new[] { 0, 1, 2 }, 0, 3, ray, null, AcceptHit, 0, 20, ref hit), Is.False);
                Unchanged(hit, before, $"Miss/parallel, indexed={indexed}");
            }
            var empty = Incoming(set);
            var original = empty;
            Assert.That(set.ObjectsIntersectRay(new[] { int.MaxValue }, 1, 0, Up, null, null, 0, 20, ref empty), Is.False);
            Unchanged(empty, original, $"Empty slice, indexed={indexed}");
        }
    }

    [Test]
    public void ThrowingFiltersDoNotPublishAnEarlierTentativeAcceptance()
    {
        foreach (var indexed in new[] { false, true })
        foreach (var objectThrows in new[] { false, true })
        {
            var set = Create(indexed);
            var hit = Incoming(set);
            var before = hit;
            var failure = new InvalidOperationException("filter failed");
            var context = $"indexed={indexed}, objectThrows={objectThrows}";
            var actual = Assert.Catch(() => set.ObjectsIntersectRay(new[] { 2, 1, 0 }, 0, 3, Up,
                (_, id) => { if (objectThrows && id == 1) throw failure; return true; },
                (_, id, _, _) => { if (!objectThrows && id == 1) throw failure; return false; }, 0, 20, ref hit), context);
            Assert.That(actual, Is.SameAs(failure), context);
            Unchanged(hit, before, context);
        }
    }

    [Test]
    public void MultiLeafTreesRetainNearestAcceptedHitsAndExistingStackClearing() => Assert.Multiple(() =>
    {
        var positions = Enumerable.Range(0, 64).SelectMany(id =>
        {
            var x = 4 * (id / 4 % 4); var y = 4 * (id / 16); var z = 2 + 3 * (id % 4);
            return new[] { new V3f(x, y, z), new V3f(x + 2, y, z), new V3f(x, y + 2, z) };
        }).ToArray();
        foreach (var indexed in new[] { false, true })
        {
            var set = Create(indexed, positions);
            var tree = new KdIntersectionTree(set, KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading);
            Assert.That(tree.Tree.Leafs().Count(), Is.GreaterThan(1), "Fixture must exercise multiple leaves.");
            for (var column = 0; column < 16; column++)
            foreach (var mode in new[] { "null", "accept", "reject near", "reject all", "object far" })
            {
                var ray = new FastRay3d(new V3d(4 * (column % 4) + .25, 4 * (column / 4) + .25, 0), V3d.OOI);
                var hit = Incoming(set, 30);
                var before = hit;
                var context = $"indexed={indexed}, column={column}, mode={mode}";
                var found = tree.Intersect(ray, mode == "object far" ? (_, id) => id % 4 == 1 : null,
                    mode == "null" ? null : (_, id, part, candidate) =>
                    {
                        Assert.That(part, Is.Zero, context);
                        var t = 2 + 3 * (id % 4);
                        Payload(candidate, new V3d(ray.Ray.Origin.X, ray.Ray.Origin.Y, t), t, context);
                        return mode == "reject all" || (mode == "reject near" && id % 4 == 0);
                    }, 0, 30, ref hit);
                Assert.That(found, Is.EqualTo(mode != "reject all"), context);
                if (found)
                {
                    var layer = mode == "reject near" || mode == "object far" ? 1 : 0;
                    Accepted(hit, before, set, 4 * column + layer, ray, 2 + 3 * layer, context, tree: true);
                }
                else Unchanged(hit, before, context);
            }
        }
    });

    [Test]
    [NonParallelizable]
    public void WarmedDirectScansDoNotAllocateOrReplaceCallerStacks() => Assert.Multiple(() =>
    {
        foreach (var indexed in new[] { false, true })
        foreach (var filter in new[] { null, AcceptHit })
        {
            var set = Create(indexed);
            var order = new[] { 2, 1, 0 };
            var original = Incoming(set);
            var hit = original;
            for (var i = 0; i < 4096; i++) { hit = original; set.ObjectsIntersectRay(order, 0, 3, Up, null, filter, 0, 20, ref hit); }
            var before = GC.GetAllocatedBytesForCurrentThread();
            var count = 0;
            for (var i = 0; i < 4096; i++)
            {
                hit = original;
                if (set.ObjectsIntersectRay(order, 0, 3, Up, null, filter, 0, 20, ref hit)) count++;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            var context = $"indexed={indexed}, hit filter={(filter == null ? "null" : "accept-all")}";
            Assert.That(count, Is.EqualTo(4096), context);
            Assert.That(allocated, Is.Zero, context);
            Accepted(hit, original, set, 0, Up, 2, context);
        }
    });
}
