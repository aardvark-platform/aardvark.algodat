using Aardvark.Base;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture, NonParallelizable]
    public class ShortFaceNormalsTests
    {
        private static readonly Symbol Marker = (Symbol)"ShortFaceNormalMarker";
        private static readonly Symbol UV = PolyMesh.Property.DiffuseColorCoordinates;
        private static readonly Symbol N = PolyMesh.Property.Normals;
        private static readonly V3d Normal = new V3d(-3.0 / 7, -2.0 / 7, 6.0 / 7);

        [Test]
        public void ShortIntervalsBeforeBetweenAndAfterValidFacesKeepIndependentNormals()
        {
            var layouts = new List<int[]>();
            for (var shortCount = 0; shortCount < 3; shortCount++)
            for (var slot = 0; slot < 3; slot++) { var sizes = new List<int> { 3, 4 }; sizes.Insert(slot, shortCount); layouts.Add(sizes.ToArray()); }
            layouts.Add(new[] { 0, 3, 1, 4, 2, 3, 0 }); layouts.Add(new[] { 1, 0, 2, 3, 2, 1, 0, 4, 1 });
            foreach (var sizes in layouts)
            foreach (var reverse in new[] { false, true })
            foreach (var poison in new[] { false, true })
            foreach (var extra in new[] { 0, 13 })
            foreach (var copy in new[] { false, true })
            {
                var faces = Faces(sizes, reverse, poison); var mesh = Create(faces, extra);
                var expected = sizes.Select(n => n < 3 ? V3d.Zero : reverse ? -Normal : Normal).ToArray();
                Run(mesh, copy, expected, sizes.Count(n => n < 3), 0, $"sizes={string.Join(",", sizes)}, reverse={reverse}, poison={poison}, extra={extra}, copy={copy}");
            }
        }

        [Test]
        public void AllShortAndEmptyMeshesReturnOneZeroPerFaceWithoutReadingCornerIndices()
        {
            foreach (var sizes in new[] { Array.Empty<int>(), new[] { 0 }, new[] { 1 }, new[] { 2 }, new[] { 0, 1, 2 }, new[] { 2, 0, 1, 0, 2 }, new[] { 0, 0, 0 } })
            foreach (var poison in new[] { false, true })
            foreach (var extra in new[] { 0, 13 })
            foreach (var copy in new[] { false, true })
                Run(Create(Faces(sizes, false, poison), extra), copy, new V3d[sizes.Length], sizes.Length, 0,
                    $"all-short sizes={string.Join(",", sizes)}, poison={poison}, extra={extra}, copy={copy}");
        }

        [Test]
        public void OrdinaryFanArithmeticWindingZeroNanAndExistingNormalPoliciesRemainUnchanged()
        {
            var layouts = new[] {
                new[] { new[] { 0, 1, 3 } }, new[] { new[] { 0, 1, 2, 3 } },
                new[] { new[] { 0, 4, 1, 2, 3 } },
                new[] { new[] { 0, 1, 3 }, new[] { 0, 1, 2, 3 }, new[] { 3, 1, 0 }, new[] { 0, 0, 0 }, new[] { 0, 4, 1 }, new[] { 0, 1, 5 } }
            };
            foreach (var sourceFaces in layouts)
            foreach (var reverse in new[] { false, true })
            foreach (var extra in new[] { 0, 13 })
            foreach (var copy in new[] { false, true })
            {
                var faces = sourceFaces.Select(f => reverse ? f.Reverse().ToArray() : f.ToArray()).ToArray();
                var mesh = Create(faces, extra); if (faces.Any(f => f.Contains(5))) mesh.PositionArray[5] = new V3d(double.NaN, 0, 0);
                var expected = LegacyNormals(mesh, out var zeros, out var nans);
                var result = Run(mesh, copy, expected, zeros, nans, $"ordinary faces={faces.Length}, reverse={reverse}, extra={extra}, copy={copy}");
                var actual = (V3d[])result.FaceAttributes[N];
                for (var i = 0; i < actual.Length; i++) BitsEqual(actual[i], expected[i], $"legacy bits face={i}, reverse={reverse}, copy={copy}");
            }
            foreach (var extra in new[] { 0, 13 })
            {
                var mesh = Create(Faces(new[] { 3, 4 }, false, false), extra); var cached = new[] { new V3d(9, 8, 7), new V3d(6, 5, 4) }; mesh.FaceAttributes[N] = cached;
                var unchanged = Snapshot(mesh, false, "cached source");
                using (var warnings = new Warnings())
                {
                    var result = mesh.WithPerFaceNormals(); Assert.That(result, Is.Not.SameAs(mesh)); Assert.That(result.FaceAttributes[N], Is.SameAs(cached));
                    Assert.That(warnings.Messages, Is.Empty, "existing normals are not recomputed");
                }
                unchanged();
                Run(mesh, false, new[] { Normal, Normal }, 0, 0, "Add replaces cached normals");
            }
        }

        [Test]
        public void DegenerateFaceCleanupRetainsValidFacesFollowingSingletonsWithoutMutatingTheSource()
        {
            foreach (var sizes in new[] { new[] { 1, 3 }, new[] { 0, 1, 2, 3 }, new[] { 3, 1, 4, 2, 0 } })
            foreach (var reverse in new[] { false, true })
            foreach (var poison in new[] { false, true })
            foreach (var extra in new[] { 0, 13 })
            {
                var faces = Faces(sizes, reverse, poison); var mesh = Create(faces, extra);
                var context = $"cleanup sizes={string.Join(",", sizes)}, reverse={reverse}, poison={poison}, extra={extra}";
                var selected = Enumerable.Range(0, sizes.Length).Where(i => sizes[i] >= 3).ToArray(); var unchanged = Snapshot(mesh, false, context);
                using (var warnings = new Warnings())
                {
                    Assert.That(mesh.NonDegenerateFaceIndices().ToArray(), Is.EqualTo(selected), context);
                    var result = mesh.WithoutDegeneratedFaces(); Assert.That(result.FaceCount, Is.EqualTo(selected.Length), context);
                    Assert.That(result.FaceAttributes[Marker], Is.EqualTo(selected.Select(i => 100 + i)), context);
                    var outputCorner = 0;
                    for (var f = 0; f < selected.Length; f++)
                    {
                        Assert.That(result.FirstIndexArray[f], Is.EqualTo(outputCorner), context);
                        foreach (var sourceIndex in faces[selected[f]])
                        {
                            var actual = result.PositionArray[result.VertexIndexArray[outputCorner++]];
                            Equal(actual, mesh.PositionArray[sourceIndex], context + ", retained position");
                        }
                    }
                    Assert.That(result.VertexIndexCount, Is.EqualTo(outputCorner), context); Assert.That(result.FirstIndexArray[selected.Length], Is.EqualTo(outputCorner), context);
                    Assert.That(warnings.Messages, Is.Empty, context + ", cleanup suppresses normal warnings");
                }
                unchanged();
            }
            // Natural AddFace capacity and a singleton must not truncate the following triangle.
            var incremental = new PolyMesh { PositionArray = Positions() }; incremental.AddFace(0); incremental.AddFace(0, 1, 3); Seed(incremental);
            Run(incremental, true, new[] { V3d.Zero, Normal }, 1, 0, "incremental singleton then triangle");
            Assert.That(incremental.NonDegenerateFaceIndices().ToArray(), Is.EqualTo(new[] { 1 }));
        }

        private static int[][] Faces(int[] sizes, bool reverse, bool poison) => sizes.Select(n => {
            var face = n == 0 ? Array.Empty<int>() : n == 1 ? new[] { poison ? -1 : 0 } : n == 2 ? new[] { poison ? int.MinValue : 0, poison ? int.MaxValue : 1 } : n == 3 ? new[] { 0, 1, 3 } : new[] { 0, 1, 2, 3 };
            return reverse ? face.Reverse().ToArray() : face;
        }).ToArray();
        private static V3d[] Positions() => new[] { V3d.Zero, new V3d(2, 0, 1), new V3d(2, 3, 2), new V3d(0, 3, 1), new V3d(1, 0, 0.5), new V3d(9, 8, 7) };
        private static PolyMesh Create(int[][] faces, int extra)
        {
            var active = faces.SelectMany(f => f).ToArray(); var indices = new int[active.Length + extra]; active.CopyTo(indices, 0);
            for (var i = active.Length; i < indices.Length; i++) indices[i] = i % 2 == 0 ? int.MinValue : int.MaxValue;
            var first = new int[faces.Length + 1]; for (var i = 0; i < faces.Length; i++) first[i + 1] = first[i] + faces[i].Length;
            var mesh = new PolyMesh { PositionArray = Positions(), FirstIndexArray = first, VertexIndexArray = indices };
            if (extra != 0) mesh.VertexIndexCount = active.Length;
            Seed(mesh); return mesh;
        }
        private static void Seed(PolyMesh mesh)
        {
            mesh.VertexAttributes[Marker] = Enumerable.Range(0, mesh.VertexCount).Select(i => (float)i).ToArray();
            mesh.FaceAttributes[Marker] = Enumerable.Range(0, mesh.FaceCount).Select(i => 100 + i).ToArray();
            mesh.FaceVertexAttributes[UV] = Enumerable.Range(0, mesh.VertexIndexCount).Select(i => i % 3).ToArray();
            mesh.FaceVertexAttributes[-UV] = new[] { V2d.OO, V2d.IO, V2d.OI }; mesh.InstanceAttributes[Marker] = "unchanged";
        }
        private static PolyMesh Run(PolyMesh mesh, bool copy, V3d[] expected, int zeros, int nans, string context)
        {
            var unchanged = Snapshot(mesh, !copy, context); PolyMesh result;
            using (var warnings = new Warnings())
            {
                if (copy) result = mesh.WithPerFaceNormals(); else { mesh.AddPerFaceNormals(); result = mesh; }
                var actual = (V3d[])result.FaceAttributes[N]; Assert.That(actual.Length, Is.EqualTo(mesh.FaceCount), context);
                for (var i = 0; i < actual.Length; i++) Equal(actual[i], expected[i], context + $", face={i}");
                var messages = new List<string>();
                if (zeros > 0) messages.Add($"encountered {zeros} zero normal vectors"); if (nans > 0) messages.Add($"encountered {nans} nan normal vectors");
                Assert.That(warnings.Messages.Count, Is.EqualTo(messages.Count), context);
                for (var i = 0; i < messages.Count; i++) Assert.That(warnings.Messages[i], Does.Contain(messages[i]), context);
            }
            if (copy)
            {
                Assert.That(result, Is.Not.SameAs(mesh), context); Assert.That(result.FaceAttributes, Is.Not.SameAs(mesh.FaceAttributes), context);
                Assert.That(result.PositionArray, Is.SameAs(mesh.PositionArray), context); Assert.That(result.VertexIndexArray, Is.SameAs(mesh.VertexIndexArray), context); Assert.That(result.FirstIndexArray, Is.SameAs(mesh.FirstIndexArray), context);
                Assert.That(result.FaceAttributes[Marker], Is.SameAs(mesh.FaceAttributes[Marker]), context);
                Assert.That(result.VertexAttributes[Marker], Is.SameAs(mesh.VertexAttributes[Marker]), context); Assert.That(result.FaceVertexAttributes[UV], Is.SameAs(mesh.FaceVertexAttributes[UV]), context);
                Assert.That(result.FaceVertexAttributes[-UV], Is.SameAs(mesh.FaceVertexAttributes[-UV]), context); Assert.That(result.InstanceAttributes[Marker], Is.EqualTo("unchanged"), context);
            }
            unchanged(); return result;
        }
        // Preserve the old arithmetic for ordinary faces; this reference is used only with >=3 corners.
        private static V3d[] LegacyNormals(PolyMesh mesh, out int zeros, out int nans)
        {
            zeros = nans = 0; var normals = new V3d[mesh.FaceCount];
            for (var face = 0; face < mesh.FaceCount; face++)
            {
                var start = mesh.FirstIndexArray[face]; var end = mesh.FirstIndexArray[face + 1]; var p0 = mesh.PositionArray[mesh.VertexIndexArray[start]];
                var e0 = mesh.PositionArray[mesh.VertexIndexArray[start + 1]] - p0; var normal = V3d.Zero;
                for (var i = start + 2; i < end; i++) { var e1 = mesh.PositionArray[mesh.VertexIndexArray[i]] - p0; normal += Vec.Cross(e0, e1); e0 = e1; }
                var len2 = normal.LengthSquared;
                if (len2 == 0) zeros++; else if (normal.IsNaN) nans++; else normals[face] = normal * (1.0 / Fun.Sqrt(len2));
            }
            return normals;
        }
        private sealed class Warnings : ILogTarget
        {
            public readonly List<string> Messages = new List<string>();
            public Warnings() { Report.Targets.Add(this); }
            public void Log(int threadIndex, LogMsg msg) { if (msg.Type == LogType.Warn) Messages.Add(msg.LeftText + " " + msg.RightText); }
            public void NewThreadIndex(int threadIndex) { }
            public void Dispose() { Report.Targets.Remove(this); }
        }
        private static void Equal(V3d actual, V3d expected, string context)
        {
            Assert.That(actual.IsNaN || actual.IsInfinity, Is.False, context);
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(1e-12), context); Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(1e-12), context); Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(1e-12), context);
        }
        private static void BitsEqual(V3d actual, V3d expected, string context)
        {
            Assert.That(BitConverter.DoubleToInt64Bits(actual.X), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.X)), context);
            Assert.That(BitConverter.DoubleToInt64Bits(actual.Y), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.Y)), context);
            Assert.That(BitConverter.DoubleToInt64Bits(actual.Z), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.Z)), context);
        }
        private static Action Snapshot(PolyMesh mesh, bool allowNormals, string context)
        {
            var p = mesh.PositionArray; var pc = p.ToArray(); var ix = mesh.VertexIndexArray; var ic = ix.ToArray(); var first = mesh.FirstIndexArray; var fc = first.ToArray();
            var counts = new[] { mesh.VertexCount, mesh.VertexIndexCount, mesh.FaceCount, mesh.VertexCapacity, mesh.VertexIndexCapacity, mesh.FaceCapacity, mesh.EulerCharacteristic }; var range = mesh.FaceVertexCountRange; var topology = mesh.HasTopology;
            var dictionaries = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes }; var entries = dictionaries.Select((d, domain) => d.Where(kv => !allowNormals || domain != 1 || kv.Key != N).ToArray()).ToArray(); var copies = entries.Select(es => es.Select(kv => kv.Value.Clone()).ToArray()).ToArray();
            var instance = mesh.InstanceAttributes; var instanceValues = instance.ToArray();
            return () => {
                Assert.That(mesh.PositionArray, Is.SameAs(p), context); Assert.That(p, Is.EqualTo(pc), context); Assert.That(mesh.VertexIndexArray, Is.SameAs(ix), context); Assert.That(ix, Is.EqualTo(ic), context);
                Assert.That(mesh.FirstIndexArray, Is.SameAs(first), context); Assert.That(first, Is.EqualTo(fc), context);
                Assert.That(new[] { mesh.VertexCount, mesh.VertexIndexCount, mesh.FaceCount, mesh.VertexCapacity, mesh.VertexIndexCapacity, mesh.FaceCapacity, mesh.EulerCharacteristic }, Is.EqualTo(counts), context);
                Assert.That(mesh.FaceVertexCountRange, Is.EqualTo(range), context); Assert.That(mesh.HasTopology, Is.EqualTo(topology), context);
                var current = new[] { mesh.VertexAttributes, mesh.FaceAttributes, mesh.FaceVertexAttributes };
                for (var d = 0; d < current.Length; d++)
                {
                    Assert.That(current[d], Is.SameAs(dictionaries[d]), context);
                    Assert.That(current[d].Keys.Where(k => !allowNormals || d != 1 || k != N), Is.EquivalentTo(entries[d].Select(kv => kv.Key)), context);
                    for (var i = 0; i < entries[d].Length; i++) { Assert.That(current[d][entries[d][i].Key], Is.SameAs(entries[d][i].Value), context); Assert.That(current[d][entries[d][i].Key], Is.EqualTo(copies[d][i]), context); }
                }
                Assert.That(mesh.InstanceAttributes, Is.SameAs(instance), context); Assert.That(instance.ToArray(), Is.EquivalentTo(instanceValues), context);
            };
        }
    }
}
