using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class KdTreeSetTests
    {
        private static readonly V3d Query = new V3d(.25, .25, 0);
        private const KdIntersectionTree.BuildFlags Flags = KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading;
        private static IntersectableTriangleSet Triangle(float z, float x = 0) => new IntersectableTriangleSet(new[] {
            new V3f(x, 0, z), new V3f(x + 2, 0, z), new V3f(x, 2, z) });
        private static ConcreteKdIntersectionTree Child(IIntersectableObjectSet set, Trafo3d trafo) => new ConcreteKdIntersectionTree(new KdIntersectionTree(set, Flags), trafo);
        private static Trafo3d Rigid => new Trafo3d(
            new M44d(0,-1,0,8, 0,0,1,-4, -1,0,0,2, 0,0,0,1),
            new M44d(0,0,-1,2, -1,0,0,8, 0,1,0,4, 0,0,0,1));

        private sealed class Scene
        {
            public readonly IntersectableTriangleSet[] Leaves;
            public readonly KdTreeSet[] Inner;
            public readonly KdTreeSet Root;
            public readonly KdIntersectionTree Accelerated;
            private readonly Trafo3d trafo;
            public Scene(bool nested, bool rigid = false)
            {
                trafo = rigid ? Rigid : Trafo3d.Identity;
                Leaves = Enumerable.Range(0, 16).Select(i => Triangle(i == 1 ? 5 : 1, i < 2 ? 0 : 16 * i)).ToArray();
                Inner = nested ? Leaves.Select(l => new KdTreeSet(Child(l, trafo))).ToArray() : Array.Empty<KdTreeSet>();
                Root = new KdTreeSet(Leaves.Select((l, i) => Child(nested ? (IIntersectableObjectSet)Inner[i] : l, trafo)));
                Accelerated = new KdIntersectionTree(Root, Flags);
                Assert.That(Accelerated.Tree.Leafs().Count(), Is.GreaterThan(1), "hierarchy acceleration fixture must subdivide");
            }
            public V3d World(V3d p) => trafo.Forward.TransformPos(Inner.Length > 0 ? trafo.Forward.TransformPos(p) : p);
            public bool Run(bool accelerated, int[] indices, Func<IIntersectableObjectSet, int, bool> filter,
                Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> pointFilter, ref ObjectClosestPoint closest) => accelerated
                    ? Accelerated.ClosestPoint(World(Query), filter, pointFilter, ref closest)
                    : Root.ClosestPoint(indices, 0, indices.Length, World(Query), filter, pointFilter, ref closest);
            public void Callback(IIntersectableObjectSet set, int index, string context)
            {
                Assert.That(ReferenceEquals(set, Root) || Inner.Any(s => ReferenceEquals(s, set)) || Leaves.Any(s => ReferenceEquals(s, set)), Is.True, context + ": callback set identity");
                Assert.That(index, Is.InRange(0, set.ObjectCount - 1), context + ": original local index");
            }
        }

        private static ObjectClosestPoint Incoming(IIntersectableObjectSet root, double bound) => new ObjectClosestPoint {
            Distance = bound, DistanceSquared = bound * bound, Point = new V3d(-7, -8, -9), Coord = new V2d(.25, .75),
            SetObject = new SetObject(root, 99), ObjectStack = new List<SetObject> { new SetObject(root, 42) }
        };
        private static void Finite(ObjectClosestPoint c, string context)
        {
            foreach (var v in new[] { c.Distance, c.DistanceSquared, c.Point.X, c.Point.Y, c.Point.Z })
                Assert.That(double.IsFinite(v), Is.True, context + ": finite result");
        }
        private static void Result(Scene s, ObjectClosestPoint c, ObjectClosestPoint before, bool found, int selected, string context)
        {
            Finite(c, context);
            if (!found) Assert.That(c, Is.EqualTo(before), context + ": unchanged miss/rejection");
            else
            {
                var distance = selected == 0 ? 1.0 : 5.0;
                Assert.That(c.Distance, Is.EqualTo(distance).Within(1e-12), context + ": distance");
                Assert.That(c.DistanceSquared, Is.EqualTo(distance * distance).Within(1e-12), context + ": squared distance");
                Assert.That(c.Point.ApproximateEquals(s.World(Query + new V3d(0, 0, distance)), 1e-11), Is.True, context + ": world point");
                Assert.That(c.SetObject.Set, Is.SameAs(s.Leaves[selected]), context + ": selected leaf");
                Assert.That(c.SetObject.Index, Is.Zero, context + ": original leaf index");
                Assert.That(c.Coord, Is.EqualTo(V2d.Zero), context + ": existing triangle coordinates");
                Assert.That(c.ObjectStack.Count, Is.EqualTo(s.Inner.Length > 0 ? 2 : 1), context + ": hierarchy depth");
                var last = c.ObjectStack.Last();
                Assert.That(last.Set, Is.SameAs(s.Root), context + ": root stack owner");
                Assert.That(last.Index, Is.EqualTo(selected), context + ": root stack index");
                if (s.Inner.Length > 0)
                {
                    Assert.That(c.ObjectStack[0].Set, Is.SameAs(s.Inner[selected]), context + ": inner stack owner");
                    Assert.That(c.ObjectStack[0].Index, Is.Zero, context + ": inner stack index");
                }
            }
            Assert.That(before.ObjectStack.Count, Is.EqualTo(1), context + ": caller stack unchanged");
            Assert.That(before.ObjectStack[0].Index, Is.EqualTo(42), context + ": caller stack value");
        }

        [Test]
        public void HierarchyAndLeafFiltersSelectDistanceFiveOrPreserveRejectedResults()
        {
            Assert.Multiple(() => {
                foreach (var nested in new[] { false, true })
                foreach (var accelerated in new[] { false, true })
                foreach (var rigid in new[] { false, true })
                {
                    var s = new Scene(nested, rigid);
                    foreach (var mode in Enumerable.Range(0, 6))
                    foreach (var bound in new[] { 0.0, 1.0, 5.0, 6.0, 1000.0 })
                    {
                        var context = $"nested={nested}, accelerated={accelerated}, rigid={rigid}, mode={mode}, bound={bound}";
                        var closest = Incoming(s.Root, bound); var before = closest; var calls = 0;
                        Func<IIntersectableObjectSet, int, bool> filter = (set, index) => {
                            calls++; s.Callback(set, index, context);
                            return mode switch {
                                0 => !ReferenceEquals(set, s.Root) || index != 0,
                                1 => !ReferenceEquals(set, s.Root) || index == 1,
                                2 => !ReferenceEquals(set, s.Root),
                                3 => set is KdTreeSet || ReferenceEquals(set, s.Leaves[1]),
                                4 => set is KdTreeSet,
                                _ => nested ? !ReferenceEquals(set, s.Inner[0]) : !ReferenceEquals(set, s.Root) || index != 0
                            };
                        };
                        var expected = mode != 2 && mode != 4 && bound > 5;
                        var found = s.Run(accelerated, new[] { 0, 1 }, filter,
                            (_, _, _, _) => throw new Exception("Triangle point-result filters remain unused"), ref closest);
                        Assert.That(found, Is.EqualTo(expected), context + ": found");
                        Result(s, closest, before, expected, 1, context);
                        if (bound == 1000) Assert.That(calls, Is.GreaterThan(0), context + ": predicate invoked");
                    }
                }
            });
        }

        [Test]
        public void RejectedChildrenAreSkippedBeforeAccessAndSlicesKeepOriginalIndices()
        {
            var probe = new ProbeSet(); var root = new KdTreeSet(new ConcreteKdIntersectionTree[] { null, Child(probe, Trafo3d.Identity) });
            var indices = new[] { int.MinValue, -1, 0, 1, int.MaxValue, int.MinValue }; var trace = new List<int>();
            var closest = Incoming(root, 6);
            Assert.That(root.ClosestPoint(indices, 1, 4, Query, (set, i) => {
                if (ReferenceEquals(set, root)) { trace.Add(i); return i == 1; } return true;
            }, null, ref closest), Is.True);
            Assert.That(trace, Is.EqualTo(new[] { -1, 0, 1, int.MaxValue }), "before-access original indices");
            Assert.That(indices, Is.EqualTo(new[] { int.MinValue, -1, 0, 1, int.MaxValue, int.MinValue }), "source indices unchanged");
            Assert.That(probe.Calls, Is.EqualTo(1)); Assert.That(closest.Distance, Is.EqualTo(5));
            Assert.That(closest.ObjectStack.Last().Index, Is.EqualTo(1));
            var s = new Scene(false);
            foreach (var bound in new[] { 0.0, 5.0, 6.0 })
            {
                closest = Incoming(s.Root, bound); var before = closest; var roots = new List<int>();
                var found = s.Root.ClosestPoint(new[] { int.MaxValue, 1, 0, -1 }, 1, 2, Query, (set, i) => {
                    s.Callback(set, i, "slice"); if (!ReferenceEquals(set, s.Root)) return true;
                    roots.Add(i); return i == 1;
                }, null, ref closest);
                Assert.That(roots, Is.EqualTo(new[] { 1, 0 }), $"slice callback order bound={bound}");
                Assert.That(found, Is.EqualTo(bound > 5)); Result(s, closest, before, bound > 5, 1, "slice");
            }
        }

        private sealed class ProbeSet : IntersectableTriangleSet, IIntersectableObjectSet
        {
            public int Calls;
            public Func<IIntersectableObjectSet, int, bool> ObjectPredicate;
            public Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> PointPredicate;
            public ProbeSet() : base(new[] { new V3f(0,0,5), new V3f(2,0,5), new V3f(0,2,5) }) { }
            public new bool ClosestPoint(int[] indices, int first, int count, V3d query,
                Func<IIntersectableObjectSet, int, bool> objectFilter,
                Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> pointFilter, ref ObjectClosestPoint closest)
            {
                Calls++; ObjectPredicate = objectFilter; PointPredicate = pointFilter;
                var candidate = closest;
                if (!base.ClosestPoint(indices, first, count, query, objectFilter, null, ref candidate)) return false;
                // This instrumented leaf explicitly supports true-as-inclusion point predicates.
                if (pointFilter != null && !pointFilter(this, candidate.SetObject.Index, 0, candidate)) return false;
                closest = candidate; return true;
            }
        }

        [Test]
        public void BothPredicatesReachInstrumentedLeavesThroughNestedTrees()
        {
            Assert.Multiple(() => {
                foreach (var nested in new[] { false, true })
                foreach (var accelerated in new[] { false, true })
                foreach (var accept in new[] { false, true })
                {
                    var probe = new ProbeSet(); var inner = new KdTreeSet(Child(probe, Trafo3d.Identity));
                    var root = nested ? new KdTreeSet(Child(inner, Trafo3d.Identity)) : inner;
                    var tree = new KdIntersectionTree(root, Flags); var context = $"nested={nested}, accelerated={accelerated}, pointAccept={accept}";
                    var objectCalls = new List<IIntersectableObjectSet>(); var pointCalls = 0;
                    Func<IIntersectableObjectSet, int, bool> objects = (set, index) => { objectCalls.Add(set); Assert.That(index, Is.Zero, context); return true; };
                    Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> points = (set, index, part, candidate) => {
                        pointCalls++; Assert.That(set, Is.SameAs(probe), context); Assert.That(index, Is.Zero, context); Assert.That(part, Is.Zero, context);
                        Finite(candidate, context); Assert.That(candidate.Distance, Is.EqualTo(5), context); return accept;
                    };
                    var closest = Incoming(root, 6); var before = closest;
                    var found = accelerated ? tree.ClosestPoint(Query, objects, points, ref closest)
                        : root.ClosestPoint(new[] { 0 }, 0, 1, Query, objects, points, ref closest);
                    Assert.That(probe.Calls, Is.EqualTo(1), context);
                    Assert.That(probe.ObjectPredicate, Is.SameAs(objects), context + ": object predicate forwarded unchanged");
                    Assert.That(probe.PointPredicate, Is.SameAs(points), context + ": point predicate forwarded unchanged");
                    Assert.That(pointCalls, Is.EqualTo(1), context + ": leaf point support");
                    Assert.That(objectCalls, Is.EqualTo(nested ? new IIntersectableObjectSet[] { root, inner, probe } : new IIntersectableObjectSet[] { root, probe }), context + ": callback owners/order");
                    Assert.That(found, Is.EqualTo(accept), context + ": leaf-dependent result");
                    if (!accept) Assert.That(closest, Is.EqualTo(before), context + ": rejected point leaves result unchanged");
                    Assert.That(before.ObjectStack.Count, Is.EqualTo(1), context + ": caller stack unchanged");
                    Assert.That(before.ObjectStack[0].Index, Is.EqualTo(42), context + ": caller stack value");
                }
            });
        }

        [Test]
        public void NullAndAcceptAllRetainCutoffsRigidTransformsAndStacks()
        {
            foreach (var nested in new[] { false, true })
            foreach (var rigid in new[] { false, true })
            {
                var s = new Scene(nested, rigid);
                foreach (var accelerated in new[] { false, true })
                foreach (var indices in new[] { new[] { 0, 1 }, new[] { 1, 0 }, Array.Empty<int>() })
                foreach (var bound in new[] { .5, 1.0, Math.BitIncrement(1.0), 5.0, 6.0 })
                foreach (var useFilter in new[] { false, true })
                {
                    if (accelerated && indices.Length == 0) continue;
                    var context = $"nested={nested}, rigid={rigid}, accelerated={accelerated}, slice=[{string.Join(",", indices)}], bound={bound:R}, all={useFilter}";
                    var closest = Incoming(s.Root, bound); var before = closest;
                    Func<IIntersectableObjectSet, int, bool> filter = useFilter ? (_, _) => true : null;
                    var found = s.Run(accelerated, indices, filter, (_, _, _, _) => throw new Exception("Unused triangle point filter"), ref closest);
                    var expected = indices.Length > 0 && bound > 1;
                    Assert.That(found, Is.EqualTo(expected), context); Result(s, closest, before, expected, 0, context);
                }
            }
        }
    }
}
