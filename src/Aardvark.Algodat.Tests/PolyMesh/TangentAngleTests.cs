using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class TangentAngleTests
    {
        private static readonly Symbol Coordinates = (Symbol)"TangentAngleCoordinates";
        private static readonly Symbol Custom = (Symbol)"TangentAngleSource";
        private static readonly Symbol U = PolyMesh.Property.DiffuseColorUTangents;
        private static readonly Symbol V = PolyMesh.Property.DiffuseColorVTangents;
        private static readonly V3d[] Thin = { V3d.Zero, new V3d(2, 3, 0), new V3d(4.00000001, 6, 0) };
        private enum Mode { Vertex, CornerDefault, CornerShared, CornerInferred, CornerIsolated }
        private static readonly Mode[] SharedModes = { Mode.Vertex, Mode.CornerDefault, Mode.CornerShared, Mode.CornerInferred };

        [Test]
        public void ThinTriangleRoundoffKeepsPositiveWeightTangentsFiniteAcrossGeneratorsAndOrders()
        {
            var a = Thin[1] - Thin[0]; var b = Thin[2] - Thin[0];
            Assert.That(Vec.Dot(a, b) / (a.Length * b.Length), Is.EqualTo(1.0000000000000002), "roundoff reproduction");
            Assert.That(a.X * b.Y - a.Y * b.X, Is.Not.Zero, "nondegenerate geometry and XY texture determinant");
            foreach (var exponent in new[] { -8, -2, 0, 2, 8 })
            foreach (var order in Orders(new[] { 0, 1, 2 }))
            foreach (var mode in SharedModes)
            {
                var scale = Math.Pow(2, exponent); var positions = Thin.Select(p => p * scale).ToArray();
                var fixture = Create(positions, new[] { order }, XY(positions), mode);
                var context = $"thin exponent={exponent}, order={string.Join(",", order)}, mode={mode}";
                RunAndCheck(fixture, U, V, V3d.IOO, V3d.OIO, context);
                var slot = fixture.Map[Array.IndexOf(fixture.Mesh.VertexIndexArray, 1)];
                Equal(Get(fixture, U)[slot], V3d.IOO, context + ", positive-angle U");
                Equal(Get(fixture, V)[slot], V3d.OIO, context + ", positive-angle V");
            }
        }

        [Test]
        public void ThinNeighborsDoNotPoisonOrdinarySharedVertexContributions()
        {
            var source = new[] { Thin[0], Thin[1], Thin[2], new V3d(0, 4, 0), new V3d(-4, 0, 0) };
            foreach (var exponent in new[] { -4, 0, 4 })
            foreach (var thinOrder in Orders(new[] { 0, 1, 2 }))
            foreach (var ordinaryOrder in new[] { new[] { 0, 3, 4 }, new[] { 4, 3, 0 } })
            foreach (var reverseFaces in new[] { false, true })
            foreach (var mode in SharedModes)
            {
                var positions = source.Select(p => p * Math.Pow(2, exponent)).ToArray();
                var faces = new[] { thinOrder, ordinaryOrder }; if (reverseFaces) Array.Reverse(faces);
                var fixture = Create(positions, faces, XY(positions), mode);
                var context = $"shared exponent={exponent}, thin={string.Join(",", thinOrder)}, neighbor={string.Join(",", ordinaryOrder)}, reverseFaces={reverseFaces}, mode={mode}";
                RunAndCheck(fixture, U, V, V3d.IOO, V3d.OIO, context);
                var slot = fixture.Map[Array.IndexOf(fixture.Mesh.VertexIndexArray, 0)];
                Equal(Get(fixture, U)[slot], V3d.IOO, context + ", ordinary shared U");
                Equal(Get(fixture, V)[slot], V3d.OIO, context + ", ordinary shared V");
            }
        }

        [Test]
        public void OrdinaryTriangleQuadAndOptionalOutputsKeepTheirExistingContracts()
        {
            foreach (var source in new[] {
                new[] { V3d.Zero, new V3d(2, 0, 0), new V3d(0, 3, 0) },
                new[] { V3d.Zero, new V3d(2, 0, 0), new V3d(2, 3, 0), new V3d(0, 3, 0) } })
            foreach (var order in Orders(Enumerable.Range(0, source.Length).ToArray()))
            foreach (var exponent in new[] { -2, 0, 2 })
            foreach (Mode mode in Enum.GetValues(typeof(Mode)))
            foreach (var rotatedUv in new[] { false, true })
            for (var outputs = 0; outputs < 4; outputs++)
            {
                var positions = source.Select(p => p * Math.Pow(2, exponent)).ToArray();
                var coordinates = positions.Select(p => rotatedUv ? new V2d(p.Y, -p.X) : new V2d(p.X, p.Y)).ToArray();
                var fixture = Create(positions, new[] { order }, coordinates, mode);
                var u = (outputs & 1) != 0 ? (Symbol)"CustomAngleU" : Symbol.Empty;
                var v = (outputs & 2) != 0 ? (Symbol)"CustomAngleV" : Symbol.Empty;
                RunAndCheck(fixture, u, v, rotatedUv ? V3d.OIO : V3d.IOO, rotatedUv ? -V3d.IOO : V3d.OIO,
                    $"ordinary n={source.Length}, order={string.Join(",", order)}, exponent={exponent}, mode={mode}, rotatedUv={rotatedUv}, outputs={outputs}");
            }
        }

        [Test]
        public void CornerSharingAndDegenerateInputsRetainAccumulationAndGuardBehavior()
        {
            var positions = new[] { V3d.Zero, new V3d(2, 0, 0), new V3d(0, 2, 0), new V3d(0, 4, 0), new V3d(-4, 0, 0) };
            var faces = new[] { new[] { 0, 1, 2 }, new[] { 0, 3, 4 } };
            var coordinates = new[] { V2d.OO, new V2d(2, 0), new V2d(0, 2), new V2d(4, 0), new V2d(0, 4) };
            foreach (Mode mode in Enum.GetValues(typeof(Mode)))
            {
                var fixture = Create(positions, faces, coordinates, mode);
                var unchanged = Snapshot(fixture, U, V, $"sharing mode={mode}");
                Generate(fixture, U, V);
                var u = Get(fixture, U); var v = Get(fixture, V);
                if (mode == Mode.CornerIsolated)
                {
                    Equal(u[0], V3d.IOO, "isolated first U"); Equal(v[0], V3d.OIO, "isolated first V");
                    Equal(u[3], V3d.OIO, "isolated second U"); Equal(v[3], -V3d.IOO, "isolated second V");
                }
                else
                {
                    var slot = fixture.Map[0]; var s = Math.Sqrt(0.5);
                    Equal(u[slot], new V3d(s, s, 0), $"shared U mode={mode}");
                    Equal(v[slot], new V3d(-s, s, 0), $"shared V mode={mode}");
                }
                CheckLayout(fixture, U, V, "sharing layout"); unchanged();
            }
            var tiny = Constant<double>.PositiveTinyValue * 0.5;
            var geometries = new[] {
                new[] { V3d.Zero, new V3d(2, 0, 0), new V3d(0, 3, 0) }, // collapsed UV determinant
                new[] { V3d.Zero, V3d.Zero, new V3d(1, 1, 0) }, // zero edge, nonzero UV determinant
                new[] { V3d.Zero, new V3d(tiny, 0, 0), new V3d(0, tiny, 0) }, // short-edge guard
                new[] { V3d.Zero, V3d.IOO, 2 * V3d.IOO } // collinear XY coordinates
            };
            for (var kind = 0; kind < geometries.Length; kind++)
            foreach (Mode mode in Enum.GetValues(typeof(Mode)))
            for (var outputs = 0; outputs < 4; outputs++)
            {
                var uv = kind == 0 ? new V2d[3] : kind == 3 ? XY(geometries[kind]) : new[] { V2d.OO, V2d.IO, V2d.OI };
                var fixture = Create(geometries[kind], new[] { new[] { 0, 1, 2 } }, uv, mode);
                var u = (outputs & 1) != 0 ? U : Symbol.Empty; var v = (outputs & 2) != 0 ? V : Symbol.Empty;
                var context = $"degenerate kind={kind}, mode={mode}, outputs={outputs}";
                var unchanged = Snapshot(fixture, u, v, context); Generate(fixture, u, v);
                foreach (var name in new[] { u, v }.Where(n => n.IsNotEmpty))
                    foreach (var tangent in Get(fixture, name)) Equal(tangent, V3d.Zero, context);
                CheckLayout(fixture, u, v, context); unchanged();
            }
        }

        private sealed record Fixture(PolyMesh Mesh, Mode Mode, int[] Map, int Count, int[] Sharing);
        private static Fixture Create(V3d[] positions, int[][] faces, V2d[] coordinates, Mode mode)
        {
            var indices = faces.SelectMany(f => f).ToArray();
            var first = new int[faces.Length + 1]; for (var i = 0; i < faces.Length; i++) first[i + 1] = first[i] + faces[i].Length;
            var mesh = new PolyMesh { PositionArray = positions.ToArray(), FirstIndexArray = first, VertexIndexArray = indices };
            mesh.VertexAttributes.Add(Coordinates, coordinates.ToArray());
            mesh.VertexAttributes.Add(Custom, new int[positions.Length]);
            mesh.FaceAttributes.Add(Custom, new int[faces.Length]);
            mesh.FaceVertexAttributes.Add(Custom, new int[indices.Length]);
            mesh.FaceVertexAttributes.Add(Coordinates, indices.ToArray());
            mesh.FaceVertexAttributes.Add(-Coordinates, coordinates.ToArray());
            var map = mode == Mode.CornerShared || mode == Mode.CornerInferred ? indices.Select(i => (i + 2) % positions.Length).ToArray()
                : mode == Mode.CornerIsolated ? Enumerable.Range(0, indices.Length).ToArray()
                : mode == Mode.CornerDefault ? (int[])mesh.FaceVertexAttributes[Coordinates] : indices;
            var count = mode == Mode.CornerShared ? positions.Length + 2 : mode == Mode.CornerIsolated ? indices.Length : positions.Length;
            return new Fixture(mesh, mode, map, count, mode == Mode.Vertex || mode == Mode.CornerDefault ? null : map);
        }
        private static void Generate(Fixture fixture, Symbol u, Symbol v)
        {
            if (fixture.Mode == Mode.Vertex) fixture.Mesh.AddPerVertexTangents(Coordinates, u, v);
            else fixture.Mesh.AddPerFaceVertexTangents(Coordinates, u, v, fixture.Sharing, fixture.Mode == Mode.CornerInferred ? 0 : fixture.Count);
        }
        private static V3d[] Get(Fixture f, Symbol name) => (V3d[])(f.Mode == Mode.Vertex ? f.Mesh.VertexAttributes[name] : f.Mesh.FaceVertexAttributes[-name]);
        private static void CheckLayout(Fixture fixture, Symbol u, Symbol v, string context)
        {
            foreach (var name in new[] { u, v }.Where(n => n.IsNotEmpty))
            {
                Assert.That(Get(fixture, name).Length, Is.EqualTo(fixture.Count), context);
                if (fixture.Mode != Mode.Vertex) Assert.That(fixture.Mesh.FaceVertexAttributes[name], Is.SameAs(fixture.Map), context + ", sharing map identity");
            }
            if (u.IsNotEmpty && v.IsNotEmpty) Assert.That(Get(fixture, u), Is.Not.SameAs(Get(fixture, v)), context);
        }
        private static void RunAndCheck(Fixture fixture, Symbol u, Symbol v, V3d expectedU, V3d expectedV, string context)
        {
            var unchanged = Snapshot(fixture, u, v, context); Generate(fixture, u, v); CheckLayout(fixture, u, v, context);
            var weights = Weights(fixture);
            foreach (var item in new[] { (Name: u, Direction: expectedU), (Name: v, Direction: expectedV) }.Where(x => x.Name.IsNotEmpty))
            {
                var tangents = Get(fixture, item.Name);
                for (var i = 0; i < tangents.Length; i++)
                    if (weights[i] > 0) Equal(tangents[i], item.Direction, context + $", slot={i}, weight={weights[i]:R}");
                    else Equal(tangents[i], V3d.Zero, context + $", zero-contribution slot={i}");
            }
            unchanged();
        }
        // Independent scalar angle arithmetic; zero contributions retain the existing normalization contract.
        private static double[] Weights(Fixture f)
        {
            var result = new double[f.Count]; var mesh = f.Mesh; var first = mesh.FirstIndexArray; var indices = mesh.VertexIndexArray;
            for (var face = 0; face < mesh.FaceCount; face++)
            {
                var a = first[face]; var b = a + 1;
                for (var c = b + 1; c < first[face + 1]; c++)
                {
                    var p0 = mesh.PositionArray[indices[a]]; var p1 = mesh.PositionArray[indices[b]]; var p2 = mesh.PositionArray[indices[c]];
                    result[f.Map[a]] += Angle(p1 - p0, p2 - p0);
                    result[f.Map[b]] += Angle(p0 - p1, p2 - p1);
                    result[f.Map[c]] += Angle(p0 - p2, p1 - p2);
                    b = c;
                }
            }
            return result;
        }
        private static double Angle(V3d a, V3d b)
        {
            var dot = a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            var la = Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z); var lb = Math.Sqrt(b.X * b.X + b.Y * b.Y + b.Z * b.Z);
            return Math.Acos(Math.Max(-1, Math.Min(1, dot / (la * lb))));
        }
        private static V2d[] XY(V3d[] positions) => positions.Select(p => new V2d(p.X, p.Y)).ToArray();
        private static IEnumerable<int[]> Orders(int[] ids)
        {
            foreach (var reverse in new[] { false, true })
            for (var start = 0; start < ids.Length; start++)
                yield return Enumerable.Range(0, ids.Length).Select(i => ids[(start + (reverse ? ids.Length - i : i)) % ids.Length]).ToArray();
        }
        private static Action Snapshot(Fixture f, Symbol u, Symbol v, string context)
        {
            var mesh = f.Mesh; var positions = mesh.PositionArray; var p = positions.ToArray(); var indices = mesh.VertexIndexArray; var ix = indices.ToArray();
            var first = mesh.FirstIndexArray; var fi = first.ToArray(); var topology = mesh.HasTopology;
            var vertexCount = mesh.VertexCount; var indexCount = mesh.VertexIndexCount; var faceCount = mesh.FaceCount; var euler = mesh.EulerCharacteristic;
            var dictionaries = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes };
            var entries = dictionaries.Select(d => d.ToArray()).ToArray(); var copies = entries.Select(es => es.Select(kv => kv.Value.Clone()).ToArray()).ToArray();
            return () => {
                Assert.That(mesh.PositionArray, Is.SameAs(positions), context); Assert.That(mesh.PositionArray, Is.EqualTo(p), context);
                Assert.That(mesh.VertexIndexArray, Is.SameAs(indices), context); Assert.That(mesh.VertexIndexArray, Is.EqualTo(ix), context);
                Assert.That(mesh.FirstIndexArray, Is.SameAs(first), context); Assert.That(mesh.FirstIndexArray, Is.EqualTo(fi), context);
                Assert.That(mesh.HasTopology, Is.EqualTo(topology), context); Assert.That(mesh.EulerCharacteristic, Is.EqualTo(euler), context);
                Assert.That(mesh.VertexCount, Is.EqualTo(vertexCount), context); Assert.That(mesh.VertexIndexCount, Is.EqualTo(indexCount), context);
                Assert.That(mesh.FaceCount, Is.EqualTo(faceCount), context);
                var current = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes };
                for (var domain = 0; domain < current.Length; domain++)
                {
                    Assert.That(current[domain], Is.SameAs(dictionaries[domain]), context);
                    var extra = new[] { u, v }.Where(n => n.IsNotEmpty).SelectMany(n => domain == 2 && f.Mode != Mode.Vertex ? new[] { n, -n } : domain == 0 && f.Mode == Mode.Vertex ? new[] { n } : Array.Empty<Symbol>());
                    Assert.That(current[domain].Keys, Is.EquivalentTo(Enumerable.Concat(entries[domain].Select(kv => kv.Key), extra)), context);
                    for (var i = 0; i < entries[domain].Length; i++) {
                        Assert.That(current[domain][entries[domain][i].Key], Is.SameAs(entries[domain][i].Value), context);
                        Assert.That(current[domain][entries[domain][i].Key], Is.EqualTo(copies[domain][i]), context);
                    }
                }
            };
        }
        private static void Equal(V3d actual, V3d expected, string context)
        {
            Assert.That(actual.IsNaN || actual.IsInfinity, Is.False, context);
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-12), context);
            Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-12), context);
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(1e-12), context);
        }
    }
}
