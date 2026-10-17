using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyMeshParametrizedCopyTests
    {
        private static readonly Symbol Extra = "parametrized-extra";
        private static readonly double[][] Parameters = { new[] { 10.0, 20, 30, 2, 3, .5 }, new[] { -4.0, 6, 2, 3, 2, -.25 } };
        private static V3d[] Original(int corners) => corners == 3
            ? new[] { V3d.Zero, new V3d(2,0,0), new V3d(0,2,0) }
            : new[] { V3d.Zero, new V3d(2,0,0), new V3d(2,2,0), new V3d(0,2,0) };
        private static V3d[] Expected(int corners, int parameter) => parameter == 0
            ? (corners == 3 ? new[] { new V3d(10,20,30), new V3d(14,20,31), new V3d(10,26,30) }
                            : new[] { new V3d(10,20,30), new V3d(14,20,31), new V3d(14,26,31), new V3d(10,26,30) })
            : (corners == 3 ? new[] { new V3d(-4,6,2), new V3d(2,6,1.5), new V3d(-4,10,2) }
                            : new[] { new V3d(-4,6,2), new V3d(2,6,1.5), new V3d(2,10,1.5), new V3d(-4,10,2) });

        private static PolyMesh Mesh(int corners, bool topology, bool spare)
        {
            var points = Original(corners);
            var mesh = new PolyMesh {
                PositionArray = spare ? Enumerable.Concat(points, new[] { new V3d(999), new V3d(-999), new V3d(123) }).ToArray() : points,
                FirstIndexArray = spare ? new[] { 0, corners, -77 } : new[] { 0, corners },
                VertexIndexArray = spare ? Enumerable.Concat(Enumerable.Range(0, corners), new[] { int.MaxValue, -1 }).ToArray() : Enumerable.Range(0, corners).ToArray()
            };
            mesh.NormalArray = Enumerable.Repeat(V3d.ZAxis, mesh.VertexCapacity).ToArray();
            mesh.ColorArray = Enumerable.Repeat(C4f.White, mesh.VertexCapacity).ToArray();
            mesh.VertexCount = corners; mesh.FaceCount = 1; mesh.VertexIndexCount = corners;
            mesh.VertexAttributes[Extra] = Enumerable.Range(0, mesh.VertexCapacity).Select(i => i % 2).ToArray();
            mesh.VertexAttributes[-Extra] = new[] { "left", "right" };
            mesh.FaceAttributes[Extra] = new[] { 17 };
            mesh.FaceVertexAttributes[Extra] = Enumerable.Range(0, mesh.VertexIndexCapacity).ToArray();
            mesh.InstanceAttributes[Extra] = new object();
            if (topology) mesh.BuildTopology();
            mesh.EdgeAttributes[Extra] = new[] { 42, 43 };
            return mesh;
        }
        private static Action Snapshot(PolyMesh mesh, string context)
        {
            var map = mesh.MapItems.ToArray();
            var dicts = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes, mesh.EdgeAttributes };
            var entries = dicts.Select(d => d.ToArray()).ToArray();
            var arrays = Enumerable.Concat(map.Select(k => k.Value).OfType<Array>(), entries.SelectMany(e => e.Select(k => k.Value))).Where(a => a != null).ToArray();
            var values = arrays.Select(a => (Array)a.Clone()).ToArray();
            return () => {
                Assert.That(mesh.MapItems.Select(k => k.Key), Is.EquivalentTo(map.Select(k => k.Key)), context + ": source keys");
                foreach (var kv in map)
                    if (kv.Value != null && !kv.Value.GetType().IsValueType) Assert.That(mesh[kv.Key], Is.SameAs(kv.Value), context + ": source reference " + kv.Key);
                    else Assert.That(mesh[kv.Key], Is.EqualTo(kv.Value), context + ": source value " + kv.Key);
                for (int d = 0; d < dicts.Length; d++)
                {
                    Assert.That(dicts[d].Keys, Is.EquivalentTo(entries[d].Select(k => k.Key)), context + ": source domain keys " + d);
                    foreach (var kv in entries[d]) Assert.That(dicts[d][kv.Key], Is.SameAs(kv.Value), context + ": source attribute " + kv.Key);
                }
                for (int i = 0; i < arrays.Length; i++) Assert.That(arrays[i], Is.EqualTo(values[i]), context + ": source array " + i);
            };
        }
        private static void Coordinates(PolyMesh mesh, V3d[] expected, string context)
        {
            Assert.That(mesh.PositionArray, Is.EqualTo(expected), context + ": positions");
            Assert.That(mesh.VertexAttributes[PolyMesh.Property.Positions], Is.SameAs(mesh.PositionArray), context + ": attribute/cache agree");
            Assert.That(mesh.Contains(PolyMesh.Property.Positions), Is.False, context + ": no top-level position override");
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(mesh.GetVertex(i).Position, Is.EqualTo(expected[i]), context + ": vertex " + i);
                foreach (var x in new[] { mesh.PositionArray[i].X, mesh.PositionArray[i].Y, mesh.PositionArray[i].Z })
                    Assert.That(double.IsFinite(x), Is.True, context + ": finite vertex " + i);
            }
        }
        private static void Geometry(PolyMesh mesh, int parameter, string context)
        {
            var min = parameter == 0 ? new V3d(10,20,30) : new V3d(-4,6,1.5);
            var max = parameter == 0 ? new V3d(14,26,31) : new V3d(2,10,2);
            var box = new Box3d(min, max);
            Assert.That(mesh.BoundingBox3d, Is.EqualTo(box), context + ": mesh bounds");
            Assert.That(mesh.ObjectBoundingBox(0), Is.EqualTo(box), context + ": face bounds");
            var point = parameter == 0 ? new V3d(10.5,21.5,30.125) : new V3d(-3.25,7,1.9375);
            var ray = new Ray3d(point - new V3d(0,0,2), V3d.ZAxis);
            var found = mesh.Intersects(ray, out var hit);
            Assert.That(found, Is.True, context + ": new geometry intersects");
            if (found)
            {
                Assert.That(double.IsFinite(hit.T) && double.IsFinite(hit.Point.X) && double.IsFinite(hit.Point.Y) && double.IsFinite(hit.Point.Z), Is.True, context + ": finite hit");
                Assert.That(hit.T, Is.EqualTo(2).Within(1e-12), context + ": hit distance");
                Assert.That(hit.Point.ApproximateEquals(point, 1e-12), Is.True, context + ": hit point");
            }
            Assert.That(mesh.Intersects(new Ray3d(new V3d(.25,.5,-1), V3d.ZAxis), out _), Is.False, context + ": old geometry absent");
        }

        [Test]
        public void GeneratedPositionsDriveVerticesBoundsAndRayGeometryAcrossRepeatedCopies()
        {
            Assert.Multiple(() => {
                foreach (var corners in new[] { 3, 4 })
                foreach (var topology in new[] { false, true })
                foreach (var spare in new[] { false, true })
                {
                    var context = $"corners={corners}, topology={topology}, spare={spare}";
                    var mesh = Mesh(corners, topology, spare); var original = Original(corners);
                    var calls = new List<int>(); var parameterRefs = new List<double[]>();
                    mesh.PositionFunArray = Enumerable.Range(0, mesh.VertexCapacity).Select<int, Func<double[], V3d>>(i => p => {
                        calls.Add(i); parameterRefs.Add(p);
                        if (i >= corners) throw new Exception("Unused capacity evaluated");
                        var v = original[i]; return new V3d(p[0] + p[3] * v.X, p[1] + p[4] * v.Y, p[2] + p[5] * v.X);
                    }).ToArray();
                    var source = Snapshot(mesh, context);
                    PolyMesh prior = null;
                    for (int parameter = 0; parameter < 2; parameter++)
                    {
                        var p = (double[])Parameters[parameter].Clone(); calls.Clear(); parameterRefs.Clear();
                        var copy = mesh.ParametrizedCopy(p); var label = context + $", parameter={parameter}";
                        Assert.That(calls, Is.EqualTo(Enumerable.Range(0, corners)), label + ": evaluation order/count");
                        foreach (var actual in parameterRefs) Assert.That(actual, Is.SameAs(p), label + ": parameter identity");
                        Assert.That(p, Is.EqualTo(Parameters[parameter]), label + ": parameters unchanged by method");
                        Coordinates(copy, Expected(corners, parameter), label); Geometry(copy, parameter, label); source();
                        if (prior != null) { Coordinates(prior, Expected(corners, 0), label + ": prior result"); Geometry(prior, 0, label + ": prior result"); }
                        prior = copy;
                    }
                    Assert.That(mesh.Intersects(new Ray3d(new V3d(.25,.5,-1), V3d.ZAxis), out var originalHit), Is.True, context + ": source ray");
                    Assert.That(originalHit.T, Is.EqualTo(1), context + ": source hit unchanged");
                }
            });
        }

        [Test]
        public void CopiesOwnPositionsAndVertexDictionariesButShareOtherStorageAndTopology()
        {
            foreach (var corners in new[] { 3, 4 })
            foreach (var topology in new[] { false, true })
            foreach (var spare in new[] { false, true })
            {
                var context = $"corners={corners}, topology={topology}, spare={spare}";
                var mesh = Mesh(corners, topology, spare);
                mesh.PositionFunArray = Original(corners).Select<V3d, Func<double[], V3d>>(v => p => v + new V3d(p[0])).ToArray();
                var source = Snapshot(mesh, context); var a = mesh.ParametrizedCopy(new[] { 10.0 }); var b = a.ParametrizedCopy(new[] { 20.0 });
                Assert.That(a.PositionArray, Is.Not.SameAs(mesh.PositionArray), context);
                Assert.That(b.PositionArray, Is.Not.SameAs(a.PositionArray), context);
                Assert.That(a.VertexAttributes, Is.Not.SameAs(mesh.VertexAttributes), context);
                foreach (var copy in new[] { a, b })
                {
                    Assert.That(copy.VertexCount, Is.EqualTo(corners), context); Assert.That(copy.VertexCapacity, Is.EqualTo(corners), context + ": generated capacity");
                    Assert.That(copy.FaceCount, Is.EqualTo(mesh.FaceCount), context); Assert.That(copy.VertexIndexCount, Is.EqualTo(mesh.VertexIndexCount), context);
                    Assert.That(copy.FirstIndexArray, Is.SameAs(mesh.FirstIndexArray), context); Assert.That(copy.VertexIndexArray, Is.SameAs(mesh.VertexIndexArray), context);
                    Assert.That(copy.PositionFunArray, Is.SameAs(mesh.PositionFunArray), context);
                    Assert.That(copy.FaceAttributes, Is.SameAs(mesh.FaceAttributes), context); Assert.That(copy.FaceVertexAttributes, Is.SameAs(mesh.FaceVertexAttributes), context);
                    Assert.That(copy.EdgeAttributes, Is.SameAs(mesh.EdgeAttributes), context); Assert.That(copy.InstanceAttributes, Is.SameAs(mesh.InstanceAttributes), context);
                    Assert.That(copy.VertexAttributes.Keys, Is.EquivalentTo(mesh.VertexAttributes.Keys), context);
                    foreach (var kv in mesh.VertexAttributes.Where(k => k.Key != PolyMesh.Property.Positions)) Assert.That(copy.VertexAttributes[kv.Key], Is.SameAs(kv.Value), context);
                    Assert.That(copy.NormalArray, Is.SameAs(mesh.NormalArray), context); Assert.That(copy.ColorArray, Is.SameAs(mesh.ColorArray), context);
                    Assert.That(copy.HasTopology, Is.EqualTo(topology), context);
                    Assert.That(new[] { copy.EulerCharacteristic, copy.EdgeCount, copy.BorderEdgeCount, copy.NonManifoldEdgeCount, copy.HalfEdgeCount },
                        Is.EqualTo(new[] { mesh.EulerCharacteristic, mesh.EdgeCount, mesh.BorderEdgeCount, mesh.NonManifoldEdgeCount, mesh.HalfEdgeCount }), context);
                    foreach (var field in new[] { "m_faceEdgeRefArray", "m_edgeFaceRefArray" })
                    {
                        var f = typeof(PolyMesh).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                        Assert.That(f.GetValue(copy), Is.SameAs(f.GetValue(mesh)), context + ": shared topology " + field);
                    }
                    if (topology)
                    {
                        Assert.That(copy.Edges.Select(e => (e.Ref, e.FromVertexIndex, e.ToVertexIndex, e.Face.Index, e.Face.Side, e.OppositeFace.Index)),
                            Is.EqualTo(mesh.Edges.Select(e => (e.Ref, e.FromVertexIndex, e.ToVertexIndex, e.Face.Index, e.Face.Side, e.OppositeFace.Index))), context);
                        foreach (var edge in copy.GetFace(0).Edges) Assert.That(edge.Mesh, Is.SameAs(copy), context + ": facade owner");
                    }
                }
                var bPositions = (V3d[])b.PositionArray.Clone(); a.PositionArray[0] = new V3d(-100);
                a.VertexAttributes.Remove(Extra); a.VertexAttributes[-Extra] = new[] { "replacement" };
                Assert.That(b.VertexAttributes, Is.Not.SameAs(a.VertexAttributes), context);
                Assert.That(b.PositionArray, Is.EqualTo(bPositions), context + ": independent result coordinates");
                Assert.That(b.VertexAttributes.Contains(Extra), Is.True, context + ": independent dictionary"); source();
            }
        }

        [Test]
        public void CallbackOrderingParameterIdentityAndExceptionPropagationRemainUnchanged()
        {
            foreach (var corners in new[] { 3, 4 })
            foreach (var topology in new[] { false, true })
            foreach (var spare in new[] { false, true })
            foreach (var failAt in Enumerable.Range(0, corners))
            {
                var context = $"corners={corners}, topology={topology}, spare={spare}, failAt={failAt}";
                var mesh = Mesh(corners, topology, spare); var p = new[] { 10.0 }; var calls = new List<int>();
                var error = new InvalidOperationException(context);
                mesh.PositionFunArray = Enumerable.Range(0, mesh.VertexCapacity).Select<int, Func<double[], V3d>>(i => actual => {
                    Assert.That(actual, Is.SameAs(p), context); calls.Add(i); if (i == failAt) throw error; return new V3d(i);
                }).ToArray();
                var source = Snapshot(mesh, context);
                Assert.That(Assert.Throws<InvalidOperationException>(() => mesh.ParametrizedCopy(p)), Is.SameAs(error), context);
                Assert.That(calls, Is.EqualTo(Enumerable.Range(0, failAt + 1)), context + ": stop at throwing vertex"); source();
            }
            var stateful = Mesh(3, false, true); var parameter = new[] { 10.0 }; var seen = new List<double>();
            stateful.PositionFunArray = Enumerable.Range(0, 3).Select<int, Func<double[], V3d>>(i => p => {
                Assert.That(p, Is.SameAs(parameter)); seen.Add(p[0]); return new V3d(p[0]++, i, 0);
            }).ToArray();
            stateful.ParametrizedCopy(parameter);
            Assert.That(seen, Is.EqualTo(new[] { 10.0, 11, 12 })); Assert.That(parameter[0], Is.EqualTo(13));
            stateful.PositionFunArray = Enumerable.Range(0, 3).Select<int, Func<double[], V3d>>(i => p => { Assert.That(p, Is.Null); return new V3d(i); }).ToArray();
            Assert.DoesNotThrow(() => stateful.ParametrizedCopy(null));
        }

        [Test]
        public void EmptyCopiesSkipFunctionsAndDoNotChangeSourcePositionsOrDictionaries()
        {
            Assert.Multiple(() => {
                foreach (var route in Enumerable.Range(0, 3))
                foreach (var topology in new[] { false, true })
                foreach (var functions in Enumerable.Range(0, 3))
                {
                    if (route == 0 && topology) continue;
                    var context = $"route={route}, topology={topology}, functions={functions}";
                    var mesh = route == 0 ? new PolyMesh() : new PolyMesh {
                        PositionArray = route == 1 ? Array.Empty<V3d>() : new[] { new V3d(99), new V3d(-99) },
                        FirstIndexArray = new[] { 0 }, VertexIndexArray = Array.Empty<int>()
                    };
                    if (route == 2) mesh.VertexCount = 0;
                    if (topology) mesh.BuildTopology();
                    mesh.VertexAttributes[Extra] = new[] { 7 }; var calls = 0;
                    mesh.PositionFunArray = functions == 0 ? null : functions == 1 ? Array.Empty<Func<double[], V3d>>() : new Func<double[], V3d>[] { _ => { calls++; throw new Exception("Empty mesh evaluated"); } };
                    var source = Snapshot(mesh, context); var copy = mesh.ParametrizedCopy(null);
                    Assert.That(calls, Is.Zero, context); Assert.That(copy, Is.Not.SameAs(mesh), context);
                    Assert.That(copy.VertexCount, Is.Zero, context); Assert.That(copy.FaceCount, Is.Zero, context); Assert.That(copy.VertexIndexCount, Is.Zero, context);
                    Assert.That(copy.PositionArray, Is.Not.Null.And.Empty, context);
                    Assert.That(copy.VertexAttributes[PolyMesh.Property.Positions], Is.SameAs(copy.PositionArray), context);
                    Assert.That(copy.VertexAttributes, Is.Not.SameAs(mesh.VertexAttributes), context);
                    Assert.That(copy.VertexAttributes[Extra], Is.SameAs(mesh.VertexAttributes[Extra]), context);
                    Assert.That(copy.FirstIndexArray, Is.SameAs(mesh.FirstIndexArray), context); Assert.That(copy.VertexIndexArray, Is.SameAs(mesh.VertexIndexArray), context);
                    Assert.That(copy.HasTopology, Is.EqualTo(topology), context); source();
                }
            });
        }
    }
}
