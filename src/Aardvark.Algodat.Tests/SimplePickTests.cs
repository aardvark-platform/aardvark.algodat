#nullable enable
using Aardvark.Base;
using Aardvark.Rendering;
using Aardvark.Rendering.PointSet;
using FSharp.Data.Adaptive;
using Microsoft.FSharp.Collections;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using PickHit = Aardvark.Base.Geometry.RayHit<Aardvark.Rendering.PointSet.SimplePickExtensions.SimplePickPoint>;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class SimplePickTests
    {
        private static readonly Symbol Id = (Symbol)"EllipsePickId";
        private static readonly Symbol Missing = (Symbol)"MissingEllipsePickAttribute";
        // F# optional extension members have dotted CLR names, so bind their public overloads once.
        private static readonly Func<SimplePickTree, Trafo3d, Ellipse2d, IEnumerable<PickHit>> EllipsePick = Bind<Ellipse2d>();
        private static readonly Func<SimplePickTree, Trafo3d, Circle2d, IEnumerable<PickHit>> CirclePick = Bind<Circle2d>();
        private static readonly Func<SimplePickTree, Trafo3d, Box2d, IEnumerable<PickHit>> RectanglePick = Bind<Box2d>();

        [Test]
        public void RotatedUnitCircleRetainsExtremalPicksAndOriginalAttributeIndices()
        {
            var points = new[] {
                new V3d(0.9, 0, 0), new V3d(0.85, 0.85, 0), new V3d(0, 0.9, 0),
                new V3d(0, 0, 0), new V3d(-0.9, 0, 0), new V3d(0, -0.9, 0), new V3d(1.1, 0, 0)
            };
            var fixture = Create(points, Frames[0], false);
            var s = Math.Sqrt(0.5);
            var ellipse = new Ellipse2d(V2d.Zero, new V2d(s, s), new V2d(-s, s));
            var actual = Check(fixture, ellipse, "rotated unit-circle reproduction");
            var circle = CirclePick(fixture.Tree, fixture.Frame.View, new Circle2d(V2d.Zero, 1)).ToArray();
            Assert.That(Indices(actual), Is.EqualTo(Indices(circle)), "ellipse and equivalent circle must pick the same five points in the same order");
            Assert.That(Indices(actual).OrderBy(x => x), Is.EqualTo(new[] { 0, 2, 3, 4, 5 }));
        }

        [Test]
        public void EllipseSelectionsMatchScalarContainmentAcrossRadiiRotationsAndCenters()
        {
            foreach (var radii in new[] { (1.0, 1.0), (1.0, 0.5), (0.5, 1.0), (2.0, 0.25) })
            foreach (var angle in new[] { 0.0, Math.PI / 12, Math.PI / 6, Math.PI / 4, Math.PI / 3, Math.PI / 2, 5 * Math.PI / 6, 5 * Math.PI / 4 })
            foreach (var center in new[] { V2d.Zero, new V2d(0.25, -0.375), new V2d(-1.5, 2.25) })
            foreach (var conjugate in radii.Item1 == radii.Item2 ? new[] { false } : new[] { false, true })
            {
                var ellipse = MakeEllipse(center, radii.Item1, radii.Item2, angle, conjugate);
                var fixture = Create(Grid(center, Math.Max(radii.Item1, radii.Item2)), Frames[0], false);
                Check(fixture, ellipse, $"radii={radii}, angle={angle:R}, center={center}, conjugate={conjugate}");
            }
        }

        [Test]
        public void TransformedLeafAndHierarchicalPicksKeepPositionsAttributesAndRectangleOrdering()
        {
            foreach (var frame in Frames)
            foreach (var hierarchy in new[] { false, true })
            foreach (var conjugate in new[] { false, true })
            {
                var ellipse = MakeEllipse(new V2d(0.25, -0.375), 1, 0.5, Math.PI / 4, conjugate);
                var fixture = Create(Grid(ellipse.Center, 1), frame, hierarchy);
                Check(fixture, ellipse, $"transformed frame={frame}, hierarchy={hierarchy}, conjugate={conjugate}");
                if (hierarchy) Assert.That(fixture.Tree.bvh.Root, Is.Not.Null, "must traverse actual child bounds");
            }
        }

        [Test]
        public void AxisAlignedEllipsesAndOtherPickingOverloadsRetainTheirContracts()
        {
            foreach (var frame in Frames)
            foreach (var hierarchy in new[] { false, true })
            foreach (var center in new[] { V2d.Zero, new V2d(-1.5, 2.25) })
            {
                var context = $"controls frame={frame}, hierarchy={hierarchy}, center={center}";
                var fixture = Create(Grid(center, 1), frame, hierarchy);
                foreach (var axes in new[] {
                    (new V2d(1, 0), new V2d(0, 0.5)), (new V2d(0, 1), new V2d(-0.5, 0)),
                    (new V2d(-1, 0), new V2d(0, -0.5)) })
                    Check(fixture, new Ellipse2d(center, axes.Item1, axes.Item2), context);
                var circle = new Circle2d(center, 0.75);
                CheckHits(fixture, CirclePick(fixture.Tree, frame.View, circle).ToArray(),
                    p => (p.X - center.X) * (p.X - center.X) + (p.Y - center.Y) * (p.Y - center.Y) <= 0.75 * 0.75, context);
                var box = new Box2d(center - new V2d(0.6, 0.7), center + new V2d(0.6, 0.7));
                CheckHits(fixture, RectanglePick(fixture.Tree, frame.View, box).ToArray(),
                    p => p.X >= box.Min.X && p.X <= box.Max.X && p.Y >= box.Min.Y && p.Y <= box.Max.Y, context);
            }
            // Exactly representable contacts keep the existing inclusive ellipse predicate.
            var boundary = Create(new[] { new V3d(1, 0, 0), new V3d(-1, 0, 0), new V3d(0, 0.5, 0), new V3d(0, -0.5, 0) }, Frames[0], false);
            Assert.That(Check(boundary, new Ellipse2d(V2d.Zero, V2d.IO, new V2d(0, 0.5)), "inclusive axis contacts").Length, Is.EqualTo(4));
        }

        private static PickHit[] Check(Fixture fixture, Ellipse2d ellipse, string context)
        {
            var hits = EllipsePick(fixture.Tree, fixture.Frame.View, ellipse).ToArray();
            CheckHits(fixture, hits, p => Contains(ellipse, p), context);
            // A deliberately oversized rectangle is an independent broad-phase reference.
            var rectangle = RectanglePick(fixture.Tree, fixture.Frame.View, fixture.QueryBounds).ToArray();
            var expected = rectangle.Where(h => Contains(ellipse, h.Value.Ndc)).ToArray();
            Assert.That(Indices(hits), Is.EqualTo(Indices(expected)), context + ", rectangle order");
            Assert.That(hits.Select(h => h.T), Is.EqualTo(expected.Select(h => h.T)), context + ", rectangle distances");
            return hits;
        }

        // Scalar inverse of the original diameter basis: no Ellipse.Contains, M22d or bounds helper.
        private static bool Contains(Ellipse2d ellipse, V3d p)
        {
            var x = p.X - ellipse.Center.X; var y = p.Y - ellipse.Center.Y;
            var a = ellipse.Axis0; var b = ellipse.Axis1;
            var det = a.X * b.Y - a.Y * b.X;
            var u = (x * b.Y - y * b.X) / det;
            var v = (a.X * y - a.Y * x) / det;
            return u * u + v * v <= 1;
        }

        private static void CheckHits(Fixture fixture, PickHit[] hits, Func<V3d, bool> contains, string context)
        {
            var expected = fixture.Rows.Where(r => contains(r.Ndc)).Select(r => r.GlobalIndex).OrderBy(i => i).ToArray();
            Assert.That(Indices(hits).OrderBy(i => i), Is.EqualTo(expected), context + ", selected source indices");
            foreach (var hit in hits)
            {
                var attribute = hit.Value.TryGetAttribute.Invoke(Id);
                Assert.That(attribute, Is.Not.Null, context);
                var globalIndex = ((int[])attribute.Value.Item1)[attribute.Value.Item2];
                var row = fixture.Rows[globalIndex];
                Assert.That(attribute.Value.Item1, Is.SameAs(row.Attributes), context);
                Assert.That(attribute.Value.Item2, Is.EqualTo(row.LocalIndex), context);
                Assert.That(hit.Value.TryGetAttribute.Invoke(Missing), Is.Null, context);
                Equal(hit.Value.DataPosition, row.Data, context);
                Equal(hit.Value.WorldPosition, row.World, context);
                Equal(hit.Value.Ndc, row.Ndc, context);
                Assert.That(double.IsFinite(hit.T) && hit.T >= 0, Is.True, context);
            }
            foreach (var leaf in fixture.Leaves)
            {
                Assert.That(leaf.Tree.positions, Is.EqualTo(leaf.Positions), context + ", source positions");
                Assert.That(leaf.Attributes, Is.EqualTo(leaf.Ids), context + ", source attributes");
            }
        }

        private static int[] Indices(IEnumerable<PickHit> hits) => hits.Select(h => {
            var a = h.Value.TryGetAttribute.Invoke(Id).Value;
            return ((int[])a.Item1)[a.Item2];
        }).ToArray();

        private static Ellipse2d MakeEllipse(V2d center, double rx, double ry, double angle, bool conjugate)
        {
            var c = Math.Cos(angle); var s = Math.Sin(angle);
            var a = new V2d(rx * c, rx * s); var b = new V2d(-ry * s, ry * c);
            if (conjugate)
            {
                var u = Math.Cos(0.37); var v = Math.Sin(0.37);
                return new Ellipse2d(center, a * u + b * v, b * u - a * v);
            }
            return new Ellipse2d(center, a, b);
        }

        private static V3d[] Grid(V2d center, double radius) =>
            (from y in Enumerable.Range(-9, 19)
             from x in Enumerable.Range(-9, 19)
             select new V3d(center.X + x * radius * 0.13, center.Y + y * radius * 0.13, ((x + y + 18) % 3 - 1) * 0.25)).ToArray();

        private readonly record struct Frame(Trafo3d Data, Trafo3d Model, Trafo3d View);
        private static readonly Frame[] Frames = {
            new(Trafo3d.Identity, Trafo3d.Identity, Trafo3d.Identity),
            new(Trafo3d.Translation(4, -2, 1), Trafo3d.Translation(0.25, -0.125, 0.25), Trafo3d.Scale(0.5, 2, 1)),
            new(Trafo3d.Scale(2, 0.5, 1) * Trafo3d.Translation(-2, 4, 0.5), Trafo3d.Scale(0.5, 2, 1), Trafo3d.Translation(-0.25, 0.125, -0.25)),
            new(Trafo3d.Identity, Trafo3d.Identity, new Trafo3d(
                new M44d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0.25, 1),
                new M44d(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, -0.25, 1)))
        };
        private sealed record Row(int GlobalIndex, int LocalIndex, int[] Attributes, V3d Data, V3d World, V3d Ndc);
        private sealed record Leaf(SimplePickTree Tree, V3f[] Positions, int[] Attributes, int[] Ids);
        private sealed record Fixture(SimplePickTree Tree, Frame Frame, Row[] Rows, Leaf[] Leaves, Box2d QueryBounds);

        private static Fixture Create(V3d[] desiredNdc, Frame frame, bool hierarchy)
        {
            var rows = new List<Row>(); var leaves = new List<Leaf>();
            var inverse = (frame.Model * frame.View).Backward;
            var groups = hierarchy ? new[] { desiredNdc.Length / 2, desiredNdc.Length - desiredNdc.Length / 2 } : new[] { desiredNdc.Length };
            var offset = 0;
            foreach (var count in groups)
            {
                var positions = desiredNdc.Skip(offset).Take(count).Select(p => (V3f)inverse.TransformPosProj(p)).ToArray();
                var ids = Enumerable.Range(offset, count).ToArray();
                var attributes = MapExt<Symbol, Array>.Empty.Add(Id, ids);
                var bounds = new Box3d(positions.Select(p => Project(frame.Data.Forward, (V3d)p)));
                var tree = NewTree(bounds, positions, frame, attributes, Array.Empty<SimplePickTree>());
                leaves.Add(new Leaf(tree, positions.ToArray(), ids, ids.ToArray()));
                for (var i = 0; i < count; i++)
                {
                    var p = (V3d)positions[i]; var world = Project(frame.Model.Forward, p);
                    rows.Add(new Row(offset + i, i, ids, Project(frame.Data.Forward, p), world, Project(frame.View.Forward, world)));
                }
                offset += count;
            }
            var root = hierarchy ? NewTree(new Box3d(rows.Select(r => r.Data)), Array.Empty<V3f>(), frame,
                MapExt<Symbol, Array>.Empty, leaves.Select(l => l.Tree).ToArray()) : leaves[0].Tree;
            var xy = rows.Select(r => new V2d(r.Ndc.X, r.Ndc.Y)).ToArray();
            var outer = new Box2d(xy); outer.Min -= V2d.II; outer.Max += V2d.II;
            return new Fixture(root, frame, rows.ToArray(), leaves.ToArray(), outer);
        }

        private static SimplePickTree NewTree(Box3d bounds, V3f[] positions, Frame frame, MapExt<Symbol, Array> attributes, SimplePickTree[] children) =>
            new SimplePickTree(null!, bounds, positions, AValModule.constant(frame.Model), frame.Data, attributes,
                MapExt<string, Array>.Empty, new Lazy<FSharpList<SimplePickTree>>(() => ListModule.OfSeq(children)));

        private static V3d Project(M44d m, V3d p)
        {
            var w = m.M30 * p.X + m.M31 * p.Y + m.M32 * p.Z + m.M33;
            return new V3d((m.M00 * p.X + m.M01 * p.Y + m.M02 * p.Z + m.M03) / w,
                (m.M10 * p.X + m.M11 * p.Y + m.M12 * p.Z + m.M13) / w,
                (m.M20 * p.X + m.M21 * p.Y + m.M22 * p.Z + m.M23) / w);
        }
        private static void Equal(V3d actual, V3d expected, string context)
        {
            Assert.That(actual.IsNaN || actual.IsInfinity, Is.False, context);
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-12), context);
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-12), context);
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(1e-12), context);
        }
        private static Func<SimplePickTree, Trafo3d, T, IEnumerable<PickHit>> Bind<T>() =>
            typeof(SimplePickExtensions).GetMethod("SimplePickTree.FindPoints", new[] { typeof(SimplePickTree), typeof(Trafo3d), typeof(T) })!
                .CreateDelegate<Func<SimplePickTree, Trafo3d, T, IEnumerable<PickHit>>>();
    }
}
