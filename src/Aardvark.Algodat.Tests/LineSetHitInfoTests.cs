using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class LineSetHitInfoTests
    {
        private static double sink;
        private static IEnumerable<(Cylinder3d Cylinder, V3d Point, V3d Radial, double Fraction, string Context)> Cases()
        {
            // Independent orthonormal frames, not a projection of the production hit offset.
            var frames = new[] {
                (Axis: V3d.ZAxis, U: V3d.XAxis, V: V3d.YAxis),
                (Axis: V3d.XAxis, U: V3d.YAxis, V: V3d.ZAxis),
                (Axis: new V3d(2.0/3,1.0/3,2.0/3), U: new V3d(2.0/3,-2.0/3,-1.0/3), V: new V3d(1.0/3,2.0/3,-2.0/3))
            };
            foreach (var frame in frames)
            foreach (var offset in new[] { V3d.Zero, new V3d(10,-20,30) })
            foreach (var size in new[] { (Length: 1.0, Radius: .25), (Length: 10.0, Radius: 1.0), (Length: 32.0, Radius: 3.0) })
            foreach (var reversed in new[] { false, true })
            foreach (var fraction in new[] { .1, .5, .9 })
            foreach (var direction in new[] { frame.U, -frame.U, .6 * frame.U + .8 * frame.V })
            {
                var radial = direction.Normalized;
                var p0 = offset; var p1 = offset + size.Length * frame.Axis;
                var cylinder = reversed ? new Cylinder3d(p1,p0,size.Radius) : new Cylinder3d(p0,p1,size.Radius);
                var point = offset + fraction * size.Length * frame.Axis + size.Radius * radial;
                yield return (cylinder, point, radial, fraction,
                    $"axis={frame.Axis}, offset={offset}, length={size.Length}, radius={size.Radius}, reversed={reversed}, fraction={fraction}, radial={radial}");
            }
        }
        private static LineSet Set(Cylinder3d cylinder, int index)
        {
            var far0 = new Cylinder3d(new V3d(400,500,600), new V3d(400,500,610), 1);
            var far1 = new Cylinder3d(new V3d(-400,-500,-600), new V3d(-400,-500,-590), 2);
            return new LineSet(index == 0 ? new[] { cylinder, far0, far1 } : new[] { far0, far1, cylinder });
        }
        private static ObjectRayHit Hit(LineSet set, int index, V3d point) => new ObjectRayHit(1000) {
            RayHit = new RayHit3d(7) { Point = point, Coord = new V2d(.125,.75), Part = 17, BackSide = true },
            SetObject = new SetObject(set,index), ObjectStack = new List<SetObject> { new SetObject(set,42) }, Tag = new object()
        };
        private static void Unchanged(ObjectRayHit actual, ObjectRayHit before, string context)
        {
            Assert.That(actual.RayHit, Is.EqualTo(before.RayHit), context + ": ray hit unchanged");
            Assert.That(actual.SetObject.Set, Is.SameAs(before.SetObject.Set), context + ": hit owner");
            Assert.That(actual.SetObject.Index, Is.EqualTo(before.SetObject.Index), context + ": hit index");
            Assert.That(actual.Tag, Is.SameAs(before.Tag), context + ": tag");
            Assert.That(actual.ObjectStack, Is.SameAs(before.ObjectStack), context + ": stack reference");
            if (actual.ObjectStack != null)
            {
                Assert.That(actual.ObjectStack.Count, Is.EqualTo(1), context + ": stack count");
                Assert.That(actual.ObjectStack[0].Index, Is.EqualTo(42), context + ": stack value");
                Assert.That(actual.ObjectStack[0].Set, Is.SameAs(before.SetObject.Set), context + ": stack owner");
            }
        }
        private static void Normal(V3d actual, V3d expected, Cylinder3d cylinder, string context)
        {
            Assert.That(double.IsFinite(actual.X) && double.IsFinite(actual.Y) && double.IsFinite(actual.Z), Is.True, context + ": finite normal");
            Assert.That(actual.ApproximateEquals(expected, 2e-11), Is.True, context + $": radial normal expected={expected}, actual={actual}");
            Assert.That(actual.LengthSquared, Is.EqualTo(1).Within(2e-12), context + ": unit length");
            var axis = cylinder.P1 - cylinder.P0;
            Assert.That(Math.Abs(actual.Dot(axis) / axis.Length), Is.LessThan(2e-11), context + ": orthogonal to axis");
            Assert.That(actual.Dot(expected), Is.GreaterThan(1-2e-12), context + ": outward orientation");
        }
        private static void Check(LineSet set, ObjectRayHit hit, V3d expected, string context)
        {
            var before = hit; var lines = set.Lines.ToArray();
            var points = new[] { new V3d(7,8,9) }; var edges = new[] { new V3d(-1,-2,-3) };
            var info = new ObjectHitInfo { Normal = new V3d(5,6,7), Points = points, Edges = edges };
            set.ObjectHitInfo(hit, ref info);
            Normal(info.Normal, expected, lines[hit.SetObject.Index], context + ": direct");
            Assert.That(info.Points, Is.SameAs(points), context); Assert.That(info.Edges, Is.SameAs(edges), context);
            Assert.That(points, Is.EqualTo(new[] { new V3d(7,8,9) }), context); Assert.That(edges, Is.EqualTo(new[] { new V3d(-1,-2,-3) }), context);
            var result = hit.GetIntersectionRayHitInfo();
            Normal(result.Normal, expected, lines[hit.SetObject.Index], context + ": dispatched");
            Assert.That(result.Points, Is.Null, context); Assert.That(result.Edges, Is.Null, context);
            Unchanged(hit, before, context); Assert.That(set.Lines, Is.EqualTo(lines), context + ": source cylinders");
        }

        [Test]
        public void OffCenterSideHitsReturnOutwardRadialUnitNormals()
        {
            Assert.Multiple(() => {
                foreach (var c in Cases().Where(c => c.Fraction != .5))
                foreach (var index in new[] { 0, 2 })
                {
                    var set = Set(c.Cylinder,index);
                    Check(set, Hit(set,index,c.Point), c.Radial, c.Context + $", index={index}");
                }
            });
        }

        [Test]
        public void MidplaneNormalsAndZeroLengthAxesRetainTheirContracts()
        {
            foreach (var c in Cases().Where(c => c.Fraction == .5))
            {
                var set = Set(c.Cylinder,2); Check(set, Hit(set,2,c.Point), c.Radial, c.Context);
            }
            foreach (var center in new[] { V3d.Zero, new V3d(10,-20,30) })
            foreach (var offset in new[] { V3d.Zero, new V3d(1,2,3), new V3d(-2,0,0), new V3d(.1,-.3,.5) })
            foreach (var radius in new[] { 0.0, 1.0, 3.0 })
            {
                var cylinder = new Cylinder3d(center,center,radius); var set = Set(cylinder,2); var hit = Hit(set,2,center+offset); var before = hit;
                var expected = (hit.RayHit.Point - cylinder.Center).Normalized;
                var points = new[] { V3d.XAxis }; var edges = new[] { V3d.YAxis };
                var info = new ObjectHitInfo { Points = points, Edges = edges };
                set.ObjectHitInfo(hit, ref info); var dispatched = hit.GetIntersectionRayHitInfo();
                // Zero offsets deliberately retain historical nonfinite normalization bits.
                foreach (var actual in new[] { info.Normal, dispatched.Normal })
                    Assert.That(new[] { BitConverter.DoubleToInt64Bits(actual.X), BitConverter.DoubleToInt64Bits(actual.Y), BitConverter.DoubleToInt64Bits(actual.Z) },
                        Is.EqualTo(new[] { BitConverter.DoubleToInt64Bits(expected.X), BitConverter.DoubleToInt64Bits(expected.Y), BitConverter.DoubleToInt64Bits(expected.Z) }), $"zero axis center={center}, offset={offset}, radius={radius}");
                Assert.That(info.Points, Is.SameAs(points)); Assert.That(info.Edges, Is.SameAs(edges)); Unchanged(hit,before,"zero axis");
            }
        }

        [Test]
        public void DirectAndFlatTreeIntersectionsKeepGeometryAndExposeRadialHitInfo()
        {
            Assert.Multiple(() => {
                foreach (var c in Cases())
                {
                    var set = Set(c.Cylinder,2); var beforeLines = set.Lines.ToArray();
                    var tree = new KdIntersectionTree(set, KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading);
                    tree.Flatten(); Assert.That(tree.Tree, Is.TypeOf<KdFlat>(), c.Context);
                    var distance = c.Cylinder.Radius + 2;
                    var ray = new FastRay3d(c.Point + distance * c.Radial, -c.Radial);
                    foreach (var accelerated in new[] { false, true })
                    {
                        var hit = Hit(set,2,new V3d(-7,-8,-9)); hit.RayHit.T = 1000;
                        var found = accelerated ? tree.Intersect(ray,0,1000,ref hit)
                            : set.ObjectsIntersectRay(new[] { int.MaxValue, 2, -1 },1,1,ray,null,null,0,1000,ref hit);
                        var context = c.Context + $", accelerated={accelerated}";
                        Assert.That(found, Is.True, context);
                        if (!found) continue;
                        Assert.That(double.IsFinite(hit.RayHit.T) && double.IsFinite(hit.RayHit.Point.X) && double.IsFinite(hit.RayHit.Point.Y) && double.IsFinite(hit.RayHit.Point.Z), Is.True, context);
                        Assert.That(hit.RayHit.T, Is.EqualTo(distance).Within(2e-10), context + ": ray parameter");
                        Assert.That(hit.RayHit.Point.ApproximateEquals(c.Point,2e-10), Is.True, context + ": surface point");
                        Assert.That(hit.SetObject.Set, Is.SameAs(set), context); Assert.That(hit.SetObject.Index, Is.EqualTo(2), context);
                        Check(set,hit,c.Radial,context);
                    }
                    Assert.That(set.Lines, Is.EqualTo(beforeLines), c.Context + ": query source unchanged");
                }
            });
        }

        [Test]
        public void RepeatedHitInfoCallsAllocateNoBytes()
        {
            foreach (var c in Cases().Where((_,i) => i%41==0))
            {
                var set = Set(c.Cylinder,2); var hit = Hit(set,2,c.Point); var info = new ObjectHitInfo(); double sum = 0;
                for (int i=0;i<128;i++) { set.ObjectHitInfo(hit,ref info); sum += hit.GetIntersectionRayHitInfo().Normal.X; }
                var start = GC.GetAllocatedBytesForCurrentThread();
                for (int i=0;i<4096;i++) { set.ObjectHitInfo(hit,ref info); sum += info.Normal.X + info.Normal.Y + hit.GetIntersectionRayHitInfo().Normal.Z; }
                var allocated = GC.GetAllocatedBytesForCurrentThread()-start; sink = sum;
                Assert.That(allocated, Is.Zero, c.Context + ": direct and dispatched allocation"); Assert.That(double.IsFinite(sink), Is.True, c.Context);
            }
        }
    }
}
