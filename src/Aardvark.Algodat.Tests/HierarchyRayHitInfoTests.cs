#nullable enable
using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class HierarchyRayHitInfoTests
    {
        private static readonly V3d[] Triangle = { V3d.Zero, new V3d(2, 0, 1), new V3d(0, 3, 1) };
        private static readonly Trafo3d RotateX = new(
            new M44d(1, 0, 0, 0, 0, 0, -1, 0, 0, 1, 0, 0, 0, 0, 0, 1),
            new M44d(1, 0, 0, 0, 0, 0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1));
        private static readonly Trafo3d RotateZ = new(
            new M44d(0, -1, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1),
            new M44d(0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1));
        private static readonly Trafo3d[] Frames = {
            Trafo3d.Identity, Trafo3d.Translation(10, 0, 0), RotateX, RotateZ,
            Trafo3d.Scale(2), Trafo3d.Scale(2, 0.5, 3), Trafo3d.Scale(-1, 2, 0.5),
            RotateX * Trafo3d.Scale(2, 0.5, 3) * Trafo3d.Translation(-4, 2, 5)
        };

        [Test]
        public void SphereNormalsUseLeafHitPointsAndReturnInTheCallerFrame()
        {
            foreach (var frame in Frames)
            foreach (var center in new[] { V3d.Zero, new V3d(0.25, -0.5, 0.75) })
            foreach (var radial in new[] { V3d.OOI, new V3d(0.6, 0, 0.8), new V3d(0, 0.6, -0.8) })
            foreach (var speed in new[] { 1.0, 2.0 })
            foreach (var flatten in new[] { false, true })
            {
                var context = $"sphere frame={frame}, center={center}, radial={radial}, speed={speed}, flatNodes={flatten}";
                var source = new SphereSet(new[] { new Sphere3d(center, 1) });
                var path = new[] { frame };
                var query = BuildHierarchy(source, path, flatten);
                var hit = Query(query.Tree, path, center + 3 * radial, -speed * radial, context);
                CheckStack(hit, query.Stack, context);
                var unchanged = Snapshot(hit, context);
                Equal(hit.RayHit.Point, Position(path, center + radial), context);
                Assert.That(hit.RayHit.T, Is.EqualTo(2 / speed).Within(1e-10), context);
                var info = hit.GetIntersectionRayHitInfo();
                Equal(info.Normal, Normal(path, radial), context);
                Assert.That(info.Points, Is.Null, context);
                Assert.That(info.Edges, Is.Null, context);
                Assert.That(source.Sphere3ds[0], Is.EqualTo(new Sphere3d(center, 1)), context);
                unchanged(hit);
            }
        }

        [Test]
        public void TriangleVerticesEdgesAndCovectorNormalsReturnInTheCallerFrame()
        {
            var normal = Unit(Cross(Triangle[1] - Triangle[0], Triangle[2] - Triangle[0]));
            var point = 0.25 * Triangle[1] + 0.25 * Triangle[2];
            foreach (var frame in Frames)
            foreach (var side in new[] { -1.0, 1.0 })
            foreach (var flatten in new[] { false, true })
            {
                var context = $"triangle frame={frame}, side={side}, flatNodes={flatten}";
                var source = new TriangleSet(new[] { new Triangle3d(Triangle[0], Triangle[1], Triangle[2]) });
                var path = new[] { frame };
                var query = BuildHierarchy(source, path, flatten);
                var hit = Query(query.Tree, path, point + side * 2 * normal, -side * normal, context);
                CheckStack(hit, query.Stack, context);
                var unchanged = Snapshot(hit, context);
                Equal(hit.RayHit.Point, Position(path, point), context);
                Assert.That(hit.RayHit.T, Is.EqualTo(2).Within(1e-10), context);
                CheckTriangle(hit.GetIntersectionRayHitInfo(), path, context);
                Assert.That(source.Position3dList, Is.EqualTo(Triangle), context);
                unchanged(hit);
            }
        }

        [Test]
        public void NestedNativeAndFlattenedQueriesRecordAndComposeLeafToRootTransforms()
        {
            var paths = new[] {
                new[] { Frames[1], RotateX },
                new[] { RotateZ, Frames[5], Trafo3d.Translation(-4, 2, 5) },
                new[] { Frames[5], Frames[6], RotateX, Frames[1] },
                new[] { Frames[1], Trafo3d.Translation(-10, 0, 0) },
                new[] { Trafo3d.Scale(-1, 1, 1), Trafo3d.Scale(-1, 1, 1) },
                new[] { Trafo3d.Identity, Trafo3d.Identity }
            };
            foreach (var path in paths)
            foreach (var sphere in new[] { false, true })
            foreach (var flatten in new[] { false, true })
            {
                var context = $"nested depth={path.Length}, sphere={sphere}, flatNodes={flatten}, path={string.Join(";", path.Select(t => t.ToString()))}";
                var normal = sphere ? new V3d(0.6, 0, 0.8) : Unit(Cross(Triangle[1], Triangle[2]));
                var point = sphere ? normal : 0.25 * Triangle[1] + 0.25 * Triangle[2];
                IIntersectableObjectSet source = sphere ? new SphereSet(new[] { new Sphere3d(V3d.Zero, 1) })
                    : new TriangleSet(new[] { new Triangle3d(Triangle[0], Triangle[1], Triangle[2]) });
                var query = BuildHierarchy(source, path, flatten);
                var hit = Query(query.Tree, path, point + 2 * normal, -normal, context);
                CheckStack(hit, query.Stack, context);
                var unchanged = Snapshot(hit, context);
                Equal(hit.RayHit.Point, Position(path, point), context);
                Assert.That(hit.RayHit.T, Is.EqualTo(2).Within(1e-10), context);
                var info = hit.GetIntersectionRayHitInfo();
                if (sphere) { Equal(info.Normal, Normal(path, normal), context); Assert.That(info.Points, Is.Null, context); }
                else CheckTriangle(info, path, context);
                unchanged(hit);
            }
        }

        [Test]
        public void FlatIdentityAndMissingResultsKeepTheirExistingValuesAndReferences()
        {
            var point = new V3d(0.5, 0.75, 0.5);
            foreach (var sphere in new[] { false, true })
            foreach (var emptyStack in new[] { false, true })
            {
                IIntersectableObjectSet source = sphere ? new SphereSet(new[] { new Sphere3d(V3d.Zero, 1) })
                    : new TriangleSet(new[] { new Triangle3d(Triangle[0], Triangle[1], Triangle[2]) });
                var hit = Seed(source, sphere ? V3d.OOI : point);
                if (emptyStack) hit.ObjectStack = new List<SetObject>();
                var unchanged = Snapshot(hit, "flat primitive");
                ObjectHitInfo direct = new(); source.ObjectHitInfo(hit, ref direct);
                var info = hit.GetIntersectionRayHitInfo();
                Assert.That(info.Normal, Is.EqualTo(direct.Normal));
                Assert.That(info.Points, Is.EqualTo(direct.Points));
                Assert.That(info.Edges, Is.EqualTo(direct.Edges));
                unchanged(hit);
            }
            foreach (var normal in new[] { V3d.Zero, new V3d(2, 3, 4) })
            foreach (var arrays in new[] { 0, 1, 2 })
            foreach (var path in new[] { Array.Empty<Trafo3d>(), new[] { Trafo3d.Identity }, new[] { Frames[1], Trafo3d.Translation(-10, 0, 0) } })
            {
                var source = new RecordingSet(Payload(arrays, normal));
                var hit = Seed(source, point); hit.ObjectStack = Stack(path);
                var unchanged = Snapshot(hit, "identity/missing");
                var info = hit.GetIntersectionRayHitInfo();
                Assert.That(info.Normal, Is.EqualTo(normal));
                Assert.That(info.Points, Is.SameAs(source.Payload.Points));
                Assert.That(info.Edges, Is.SameAs(source.Payload.Edges));
                Equal(source.Received.RayHit.Point, point, "identity leaf point");
                unchanged(hit);
            }
        }

        [Test]
        public void FrameConversionPreservesSharedArraysLeafCallbackFieldsAndCallerState()
        {
            var localPoint = new V3d(0.5, -0.25, 0.75);
            foreach (var path in new[] { new[] { Frames[1] }, new[] { Frames[6] }, new[] { RotateX, Frames[5], Frames[1] } })
            foreach (var arrays in new[] { 0, 1, 2, 3, 4, 5 })
            foreach (var normal in new[] { V3d.Zero, new V3d(2, 3, 4) })
            {
                var context = $"shared arrays={arrays}, normal={normal}, depth={path.Length}";
                var source = new RecordingSet(Payload(arrays, normal));
                var oldPoints = source.Payload.Points?.ToArray(); var oldEdges = source.Payload.Edges?.ToArray();
                var hit = Seed(source, Position(path, localPoint)); hit.ObjectStack = Stack(path);
                hit.ObjectStack.Insert(1, new SetObject(new EmptyIntersectableObjectSet(), -17));
                var unchanged = Snapshot(hit, context);
                var info = hit.GetIntersectionRayHitInfo();
                Equal(source.Received.RayHit.Point, localPoint, context + ", leaf point");
                Assert.That(source.Received.RayHit.T, Is.EqualTo(hit.RayHit.T), context);
                Assert.That(source.Received.RayHit.Part, Is.EqualTo(hit.RayHit.Part), context);
                Assert.That(source.Received.RayHit.Coord, Is.EqualTo(hit.RayHit.Coord), context);
                Assert.That(source.Received.RayHit.BackSide, Is.EqualTo(hit.RayHit.BackSide), context);
                Assert.That(source.Received.Tag, Is.SameAs(hit.Tag), context);
                Assert.That(source.Received.ObjectStack, Is.SameAs(hit.ObjectStack), context);
                Assert.That(source.Received.SetObject.Set, Is.SameAs(source), context);
                Assert.That(source.Received.SetObject.Index, Is.EqualTo(hit.SetObject.Index), context);
                if (normal == V3d.Zero) Assert.That(info.Normal, Is.EqualTo(V3d.Zero), context);
                else Equal(info.Normal, Normal(path, normal), context);
                CheckArray(info.Points, source.Payload.Points, p => Position(path, p), context);
                CheckArray(info.Edges, source.Payload.Edges, e => Direction(path, e), context);
                Assert.That(source.Payload.Points, Is.EqualTo(oldPoints), context);
                Assert.That(source.Payload.Edges, Is.EqualTo(oldEdges), context);
                var again = hit.GetIntersectionRayHitInfo();
                Assert.That(source.Calls, Is.EqualTo(2), context);
                Assert.That(info.Points, Is.EqualTo(again.Points), context);
                Assert.That(info.Edges, Is.EqualTo(again.Edges), context);
                unchanged(hit);
            }
            var failure = new InvalidOperationException("leaf hit-info failure");
            var throwing = new RecordingSet(default) { Failure = failure };
            var failedHit = Seed(throwing, Position(new[] { Frames[1] }, localPoint)); failedHit.ObjectStack = Stack(new[] { Frames[1] });
            var preserved = Snapshot(failedHit, "throwing leaf");
            Assert.That(Assert.Throws<InvalidOperationException>(() => failedHit.GetIntersectionRayHitInfo()), Is.SameAs(failure));
            preserved(failedHit);
        }

        private static ObjectHitInfo Payload(int kind, V3d normal)
        {
            var points = new[] { new V3d(1, 2, 3), new V3d(-2, 4, 1), new V3d(0.5, -1, 2) };
            var edges = new[] { new V3d(2, -1, 0.5), new V3d(-1, 3, 2) };
            return new ObjectHitInfo { Normal = normal, Points = kind == 0 || kind == 4 ? null : kind == 1 ? Array.Empty<V3d>() : points,
                Edges = kind == 0 || kind == 3 ? null : kind == 1 ? Array.Empty<V3d>() : kind == 5 ? points : edges };
        }
        private static void CheckArray(V3d[]? actual, V3d[]? source, Func<V3d, V3d> transform, string context)
        {
            if (source == null) { Assert.That(actual, Is.Null, context); return; }
            Assert.That(actual!.Length, Is.EqualTo(source.Length), context);
            if (source.Length == 0) Assert.That(actual, Is.SameAs(source), context);
            else Assert.That(actual, Is.Not.SameAs(source), context);
            for (var i = 0; i < source.Length; i++) Equal(actual[i], transform(source[i]), context + $", element={i}");
        }
        private static void CheckTriangle(ObjectHitInfo info, Trafo3d[] path, string context)
        {
            Assert.That(info.Points!.Length, Is.EqualTo(3), context);
            Assert.That(info.Edges!.Length, Is.EqualTo(2), context);
            for (var i = 0; i < 3; i++) Equal(info.Points[i], Position(path, Triangle[i]), context);
            Equal(info.Edges[0], Direction(path, Triangle[1] - Triangle[0]), context);
            Equal(info.Edges[1], Direction(path, Triangle[2] - Triangle[0]), context);
            Equal(info.Normal, Normal(path, Unit(Cross(Triangle[1], Triangle[2]))), context);
        }
        private static (KdIntersectionTree Tree, List<SetObject> Stack) BuildHierarchy(IIntersectableObjectSet leaf, Trafo3d[] path, bool flatten)
        {
            KdIntersectionTree Build(IIntersectableObjectSet set) {
                var tree = new KdIntersectionTree(set, KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading);
                if (flatten) tree.Flatten(); return tree;
            }
            var tree = Build(leaf); var stack = new List<SetObject>();
            foreach (var frame in path)
            {
                var far = Build(new SphereSet(new[] { new Sphere3d(new V3d(10000, 10000, 10000), 1) }));
                var set = new KdTreeSet(new ConcreteKdIntersectionTree(far, Trafo3d.Identity), new ConcreteKdIntersectionTree(tree, frame));
                stack.Add(new SetObject(set, 1)); tree = Build(set);
            }
            return (tree, stack);
        }
        private static List<SetObject> Stack(Trafo3d[] path) => path.Select(t => new SetObject(new KdTreeSet(
            new ConcreteKdIntersectionTree(new KdIntersectionTree(), Trafo3d.Translation(123, 456, 789)),
            new ConcreteKdIntersectionTree(new KdIntersectionTree(), t)), 1)).ToList();
        private static ObjectRayHit Query(KdIntersectionTree tree, Trafo3d[] path, V3d origin, V3d direction, string context)
        {
            var hit = ObjectRayHit.MaxRange;
            Assert.That(tree.Intersect(new FastRay3d(Position(path, origin), Direction(path, direction)), 0, 100, ref hit), Is.True, context);
            Assert.That(hit.SetObject.Index, Is.Zero, context);
            // Preserve arbitrary caller metadata during info lookup, not just default values.
            hit.Tag = new object(); hit.RayHit.Part = 31; hit.RayHit.Coord = new V2d(0.125, 0.75); hit.RayHit.BackSide = true;
            return hit;
        }
        private static ObjectRayHit Seed(IIntersectableObjectSet source, V3d point) => new ObjectRayHit(7) {
            SetObject = new SetObject(source, 0), Tag = new object(),
            RayHit = new RayHit3d { Point = point, T = 7, Part = 31, Coord = new V2d(0.125, 0.75), BackSide = true }
        };
        private static void CheckStack(ObjectRayHit hit, List<SetObject> expected, string context)
        {
            Assert.That(hit.ObjectStack!.Count, Is.EqualTo(expected.Count), context);
            for (var i = 0; i < expected.Count; i++) {
                Assert.That(hit.ObjectStack[i].Set, Is.SameAs(expected[i].Set), context);
                Assert.That(hit.ObjectStack[i].Index, Is.EqualTo(expected[i].Index), context);
            }
        }
        private static Action<ObjectRayHit> Snapshot(ObjectRayHit hit, string context)
        {
            var stack = hit.ObjectStack; var entries = stack?.ToArray();
            return current => {
                Assert.That(current.RayHit, Is.EqualTo(hit.RayHit), context);
                Assert.That(current.SetObject.Set, Is.SameAs(hit.SetObject.Set), context);
                Assert.That(current.SetObject.Index, Is.EqualTo(hit.SetObject.Index), context);
                Assert.That(current.Tag, Is.SameAs(hit.Tag), context);
                Assert.That(current.ObjectStack, Is.SameAs(stack), context);
                if (entries != null) CheckStack(current, entries.ToList(), context);
            };
        }
        private static V3d Position(Trafo3d[] path, V3d p) { foreach (var t in path) p = Apply(t.Forward, p, 1); return p; }
        private static V3d Direction(Trafo3d[] path, V3d p) { foreach (var t in path) p = Apply(t.Forward, p, 0); return p; }
        private static V3d Apply(M44d m, V3d p, double w) => new(
            m.M00 * p.X + m.M01 * p.Y + m.M02 * p.Z + w * m.M03,
            m.M10 * p.X + m.M11 * p.Y + m.M12 * p.Z + w * m.M13,
            m.M20 * p.X + m.M21 * p.Y + m.M22 * p.Z + w * m.M23);
        private static V3d Normal(Trafo3d[] path, V3d n)
        {
            foreach (var t in path)
            {
                var m = t.Forward;
                var c00 = m.M11 * m.M22 - m.M12 * m.M21; var c01 = m.M12 * m.M20 - m.M10 * m.M22; var c02 = m.M10 * m.M21 - m.M11 * m.M20;
                var det = m.M00 * c00 + m.M01 * c01 + m.M02 * c02;
                n = new V3d(c00 * n.X + c01 * n.Y + c02 * n.Z,
                    (m.M02 * m.M21 - m.M01 * m.M22) * n.X + (m.M00 * m.M22 - m.M02 * m.M20) * n.Y + (m.M01 * m.M20 - m.M00 * m.M21) * n.Z,
                    (m.M01 * m.M12 - m.M02 * m.M11) * n.X + (m.M02 * m.M10 - m.M00 * m.M12) * n.Y + (m.M00 * m.M11 - m.M01 * m.M10) * n.Z) / det;
            }
            return Unit(n);
        }
        private static V3d Cross(V3d a, V3d b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        private static V3d Unit(V3d p) => p / Math.Sqrt(p.X * p.X + p.Y * p.Y + p.Z * p.Z);
        private static void Equal(V3d actual, V3d expected, string context)
        {
            Assert.That(actual.IsNaN || actual.IsInfinity, Is.False, context);
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(2e-10), context);
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(2e-10), context);
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(2e-10), context);
        }
        private sealed class RecordingSet : IIntersectableObjectSet
        {
            public readonly ObjectHitInfo Payload; public ObjectRayHit Received; public int Calls; public Exception? Failure;
            public RecordingSet(ObjectHitInfo payload) => Payload = payload;
            public int ObjectCount => 1;
            public Box3d ObjectBoundingBox(int index = -1) => Box3d.Unit;
            public void ObjectHitInfo(ObjectRayHit hit, ref ObjectHitInfo info) { Received = hit; Calls++; if (Failure != null) throw Failure; info = Payload; }
            public bool ObjectsIntersectRay(int[] a, int f, int c, FastRay3d r, Func<IIntersectableObjectSet, int, bool> o, Func<IIntersectableObjectSet, int, int, RayHit3d, bool> h, double min, double max, ref ObjectRayHit hit) => throw new NotSupportedException();
            public bool ClosestPoint(int[] a, int f, int c, V3d q, Func<IIntersectableObjectSet, int, bool> o, Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> p, ref ObjectClosestPoint hit) => throw new NotSupportedException();
            public bool ObjectIntersectsBox(int i, Box3d b) => throw new NotSupportedException();
            public bool ObjectIsInsideBox(int i, Box3d b) => throw new NotSupportedException();
        }
    }
}
