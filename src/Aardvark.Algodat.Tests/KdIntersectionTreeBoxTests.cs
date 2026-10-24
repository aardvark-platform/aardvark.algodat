#nullable enable

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class KdIntersectionTreeBoxTests
    {
        private static readonly Box3d Cube = new Box3d(V3d.Zero, new V3d(10));
        private static readonly Box3d[] Geometry = { Cube, new Box3d(new V3d(-6, -4, -2), new V3d(8, 2, 16)) };
        private static KdIntersectionTree Tree(Box3d geometry, bool optimized, bool flat)
        {
            var flags = KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading;
            if (optimized) flags |= KdIntersectionTree.BuildFlags.EmptySpaceOptimization;
            var tree = new KdIntersectionTree(new IntersectableBoxSet(geometry), flags);
            var bounds = tree.BoundingBox3d;
            if (flat) tree.Flatten();
            Assert.That(tree.BoundingBox3d, Is.EqualTo(bounds));
            return tree;
        }
        private static bool ContainsBounds(Box3d outer, Box3d inner)
            => outer.Min.X <= inner.Min.X && outer.Min.Y <= inner.Min.Y && outer.Min.Z <= inner.Min.Z
            && outer.Max.X >= inner.Max.X && outer.Max.Y >= inner.Max.Y && outer.Max.Z >= inner.Max.Z;

        [Test]
        public void DirectContainmentUsesTheEnclosingQueryAndExistingPaddedTreeBounds()
        {
            foreach (var geometry in Geometry)
            foreach (var optimized in new[] { false, true })
            foreach (var flat in new[] { false, true })
            {
                var tree = Tree(geometry, optimized, flat);
                var bounds = tree.BoundingBox3d;
                var root = tree.Tree;
                var objects = tree.ObjectSet;
                var center = (bounds.Min + bounds.Max) * 0.5;
                var context = $"geometry={geometry}, optimized={optimized}, flat={flat}";
                Assert.That(bounds.Min.X, Is.LessThan(geometry.Min.X), context);
                Assert.That(bounds.Max.X, Is.GreaterThan(geometry.Max.X), context);
                var cases = new[]
                {
                    (Name: "enclosing", Query: new Box3d(geometry.Min - V3d.III, geometry.Max + V3d.III), Expected: true),
                    (Name: "equal cached bounds", Query: bounds, Expected: true),
                    (Name: "contained query", Query: new Box3d(center - bounds.Size * 0.1, center + bounds.Size * 0.1), Expected: false),
                    (Name: "partial overlap", Query: new Box3d(center, bounds.Max + V3d.III), Expected: false),
                    (Name: "disjoint", Query: new Box3d(bounds.Max + V3d.III, bounds.Max + 2 * V3d.III), Expected: false),
                    (Name: "geometry omits padding", Query: geometry, Expected: false),
                    (Name: "minimum boundary inclusive", Query: new Box3d(bounds.Min, bounds.Max + V3d.III), Expected: true),
                    (Name: "maximum boundary inclusive", Query: new Box3d(bounds.Min - V3d.III, bounds.Max), Expected: true)
                };
                foreach (var c in cases)
                {
                    Assert.That(ContainsBounds(c.Query, bounds), Is.EqualTo(c.Expected), context + ": oracle " + c.Name);
                    Assert.That(tree.IsInsideBox(c.Query), Is.EqualTo(c.Expected), context + ": " + c.Name);
                }
                for (var axis = 0; axis < 3; ++axis)
                {
                    var outside = bounds;
                    outside.Min[axis] = Math.BitDecrement(outside.Min[axis]);
                    Assert.That(tree.IsInsideBox(outside), Is.True, $"{context}, next outer minimum axis={axis}");
                    var inside = bounds;
                    inside.Min[axis] = Math.BitIncrement(inside.Min[axis]);
                    Assert.That(tree.IsInsideBox(inside), Is.False, $"{context}, next inner minimum axis={axis}");
                    outside = bounds;
                    outside.Max[axis] = Math.BitIncrement(outside.Max[axis]);
                    Assert.That(tree.IsInsideBox(outside), Is.True, $"{context}, next outer maximum axis={axis}");
                    inside = bounds;
                    inside.Max[axis] = Math.BitDecrement(inside.Max[axis]);
                    Assert.That(tree.IsInsideBox(inside), Is.False, $"{context}, next inner maximum axis={axis}");
                }
                Assert.That(tree.Tree, Is.SameAs(root), context);
                Assert.That(tree.ObjectSet, Is.SameAs(objects), context);
                Assert.That(tree.BoundingBox3d, Is.EqualTo(bounds), context);
            }
        }

        [Test]
        public void HierarchicalSelectionsContainChildrenAcrossNativeFlatAndTransformedTrees()
        {
            var frames = new[]
            {
                (Name: "identity", Trafo: Trafo3d.Identity),
                (Name: "translation", Trafo: Trafo3d.Translation(32, -16, 8)),
                (Name: "nonuniform scale", Trafo: Trafo3d.Scale(2, 0.5, 4)),
                (Name: "translated scale", Trafo: Trafo3d.Translation(32, -16, 8) * Trafo3d.Scale(2, 0.5, 4))
            };
            foreach (var optimized in new[] { false, true })
            foreach (var childFlat in new[] { false, true })
            foreach (var parentFlat in new[] { false, true })
            foreach (var frame in frames)
            foreach (var reversed in new[] { false, true })
            {
                var child = Tree(Cube, optimized, childFlat);
                var bounds = child.BoundingBox3d;
                var childRoot = child.Tree;
                // Any object scan would fail; hierarchy containment must use cached child bounds.
                child.ObjectSet = null!;
                var target = new ConcreteKdIntersectionTree(child, frame.Trafo);
                var sibling = new ConcreteKdIntersectionTree(child, frame.Trafo * Trafo3d.Translation(20, 0, 0));
                var entries = reversed ? new[] { sibling, target } : new[] { target, sibling };
                var set = new KdTreeSet(entries);
                var parent = new KdIntersectionTree(set, child.BuildOptions);
                if (parentFlat) parent.Flatten();
                var parentRoot = parent.Tree;
                var targetIndex = reversed ? 1 : 0;
                var context = $"optimized={optimized}, childFlat={childFlat}, parentFlat={parentFlat}, frame={frame.Name}, reversed={reversed}";
                var queries = new[]
                {
                    (Name: "enclosing [-1,11]", Box: new Box3d(new V3d(-1), new V3d(11)).Transformed(frame.Trafo), Expected: new[] { targetIndex }),
                    (Name: "contained [4,6]", Box: new Box3d(new V3d(4), new V3d(6)).Transformed(frame.Trafo), Expected: Array.Empty<int>()),
                    (Name: "partial overlap", Box: new Box3d(new V3d(8, -1, -1), new V3d(12, 11, 11)).Transformed(frame.Trafo), Expected: Array.Empty<int>()),
                    (Name: "disjoint", Box: new Box3d(new V3d(40), new V3d(42)).Transformed(frame.Trafo), Expected: Array.Empty<int>()),
                    (Name: "both children", Box: new Box3d(new V3d(-1), new V3d(31, 11, 11)).Transformed(frame.Trafo), Expected: new[] { 0, 1 }),
                    (Name: "unpadded geometry", Box: Cube.Transformed(frame.Trafo), Expected: Array.Empty<int>())
                };
                foreach (var q in queries)
                {
                    var expected = entries.Select((e, i) => (Entry: e, Index: i))
                        .Where(e => ContainsBounds(q.Box.Transformed(e.Entry.Trafo.Backward), bounds)).Select(e => e.Index).ToArray();
                    Assert.That(expected, Is.EquivalentTo(q.Expected), context + ": independent " + q.Name);
                    for (var i = 0; i < entries.Length; ++i)
                        Assert.That(set.ObjectIsInsideBox(i, q.Box), Is.EqualTo(expected.Contains(i)), $"{context}, {q.Name}, child={i}");
                    Assert.That(parent.ObjectsInsideBox(q.Box).Distinct().OrderBy(i => i), Is.EqualTo(expected.OrderBy(i => i)), context + ": selection " + q.Name);
                }
                // Use actual padded bounds. Keep existing inverse-transform rounding, rather than changing it for equality.
                var equalWorldBounds = set.ObjectBoundingBox(targetIndex);
                var expectedEqual = ContainsBounds(equalWorldBounds.Transformed(target.Trafo.Backward), bounds);
                Assert.That(set.ObjectIsInsideBox(targetIndex, equalWorldBounds), Is.EqualTo(expectedEqual), context + ": equality after existing inverse transform");
                var equalSelection = entries.Select((e, i) => (Entry: e, Index: i))
                    .Where(e => ContainsBounds(equalWorldBounds.Transformed(e.Entry.Trafo.Backward), bounds)).Select(e => e.Index).ToArray();
                Assert.That(parent.ObjectsInsideBox(equalWorldBounds).Distinct().OrderBy(i => i), Is.EqualTo(equalSelection), context + ": equal-bounds selection");
                if (frame.Name == "identity" || frame.Name == "nonuniform scale") Assert.That(expectedEqual, Is.True, context);
                Assert.That(child.Tree, Is.SameAs(childRoot), context);
                Assert.That(child.BoundingBox3d, Is.EqualTo(bounds), context);
                Assert.That(child.ObjectSet, Is.Null, context);
                Assert.That(parent.Tree, Is.SameAs(parentRoot), context);
                Assert.That(set.ConcreteKdTreeList, Is.EqualTo(entries), context);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Repeat(KdIntersectionTree tree, Box3d[] boxes, int count)
        {
            var included = 0;
            for (var i = 0; i < count; ++i) if (tree.IsInsideBox(boxes[i % boxes.Length])) ++included;
            return included;
        }

        [Test]
        public void CachedBoundsRetainEqualityOverlapAndDisjointResultsWithoutAllocating()
        {
            foreach (var geometry in Geometry)
            foreach (var optimized in new[] { false, true })
            foreach (var flat in new[] { false, true })
            {
                var tree = Tree(geometry, optimized, flat);
                var bounds = tree.BoundingBox3d;
                var root = tree.Tree;
                tree.ObjectSet = null!;
                var boxes = new[] { bounds, new Box3d((bounds.Min + bounds.Max) * 0.5, bounds.Max + V3d.III), new Box3d(bounds.Max + V3d.III, bounds.Max + 2 * V3d.III) };
                Assert.That(Repeat(tree, boxes, 96), Is.EqualTo(32));
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var included = Repeat(tree, boxes, 12288);
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                var context = $"geometry={geometry}, optimized={optimized}, flat={flat}";
                Assert.That(included, Is.EqualTo(4096), context);
                Assert.That(allocated, Is.Zero, context);
                Assert.That(tree.Tree, Is.SameAs(root), context);
                Assert.That(tree.BoundingBox3d, Is.EqualTo(bounds), context);
                Assert.That(tree.ObjectSet, Is.Null, context);
            }
        }
    }
}
