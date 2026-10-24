/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class InlinedNodeTests
    {
        [Test]
        public void CanInlineNode()
        {
            var ply = @"ply
format ascii 1.0          
element vertex 8          
property float32 x         
property float32 y         
property float32 z
property uint8 scalar_Classification
property uint8 scalar_Intensity
end_header
0 0 0 0   255
0 0 1 1   254
0 1 1 2   200
0 1 0 42  128
1 0 0 128  42
1 0 1 200   2
1 1 1 254   1
1 1 0 255   0
";

            var ms = new MemoryStream(Encoding.UTF8.GetBytes(ply));
            var store = PointCloud.CreateInMemoryStore();
            var key = "test";
            var config = ImportConfig.Default
                .WithStorage(store)
                .WithKey(key)
                ;

            var pointset = PointCloud.Import(Data.Points.Import.Ply.Chunks(ms, 0, config.ParseConfig), config);
            var _ /*pcl*/ = pointset.Root.Value;

            var inlineConfig = new InlineConfig(collapse: true, gzipped: true);
            var inlinedNodes = store.EnumerateOctreeInlined(key, inlineConfig);

            ClassicAssert.IsTrue(inlinedNodes.Nodes.Count() == 1);

            // reentrant?
            ClassicAssert.IsTrue(inlinedNodes.Nodes.Count() == 1);

            var n = inlinedNodes.Nodes.Single();

            ClassicAssert.IsTrue(n.PositionsLocal3f.Length == 8);
            ClassicAssert.IsTrue(n.Classifications1b.Length == 8);
            ClassicAssert.IsTrue(n.Intensities1b.Length == 8);

            var encoded = n.Encode(gzip: true);
            var m = new InlinedNode(encoded, gzipped: true);

            ClassicAssert.True(n.NodeId == m.NodeId);
            ClassicAssert.True(n.Cell == m.Cell);
            ClassicAssert.True(n.PointCountCell == m.PointCountCell);
            ClassicAssert.True(n.PointCountTreeLeafs == m.PointCountTreeLeafs);
            ClassicAssert.IsTrue(m.PositionsLocal3f.Length == 8);
            ClassicAssert.IsTrue(m.Classifications1b.Length == 8);
            ClassicAssert.IsTrue(m.Intensities1b.Length == 8);
        }

        private static readonly int[][] WideRanges =
        {
            new[] { int.MinValue, 0, int.MaxValue },
            new[] { int.MinValue, int.MinValue + 1, -1073741824, -1, 0, 1, 1073741823, int.MaxValue - 1, int.MaxValue },
            new[] { int.MinValue, -1073741824, -1, 0 },
            new[] { -1, 0, 1073741823, int.MaxValue },
            new[] { int.MinValue + 17, -1, 17, 18, 19 },
            new[] { int.MinValue + 1, -42, 0, 42, int.MaxValue }
        };

        [Test]
        public void DirectInliningRescalesWideSignedIntensitiesWithoutOverflow()
        {
            Assert.That(RescaleOracle(WideRanges[0]), Is.EqualTo(new byte[] { 0, 127, 255 }));
            foreach (var values in WideRanges)
            foreach (var ordered in Orders(values))
            foreach (var collapse in new[] { false, true })
            {
                var leaf = CreateNode(new Cell(0, 0, 0, 4), ordered, ordered.Length, 0);
                CheckInlining(leaf, new[] { leaf }, collapse, $"direct [{string.Join(",", ordered)}], collapse={collapse}");
            }
        }

        [Test]
        public void CollapsedInliningRescalesCombinedChildRangesAndKeepsAttributesAligned()
        {
            // Each child has a different range; collapse must scale their combined values,
            // rather than separately scaling children or reusing the parent's LOD intensities.
            var ranges = WideRanges.Concat(new[] { new[] { 0, 32768, 65535, 100000, 100010, 100020 } });
            foreach (var values in ranges)
            foreach (var ordered in Orders(values))
            foreach (var reverseChildren in new[] { false, true })
            foreach (var collapse in new[] { false, true })
            {
                var parts = new[] { ordered.Take(ordered.Length / 2).ToArray(), ordered.Skip(ordered.Length / 2).ToArray() };
                if (reverseChildren) Array.Reverse(parts);
                var nodes = CreateTree(parts[0], parts[1]);
                CheckInlining(nodes[0], nodes, collapse,
                    $"children [{string.Join(",", ordered)}], reverse={reverseChildren}, collapse={collapse}");
            }
        }

        [Test]
        public void OrdinaryConstantEmptyAndMissingIntensitiesRetainInliningContracts()
        {
            var ranges = new[]
            {
                new[] { 0, 1, 42, 127, 128, 254, 255 },
                new[] { 0, 32767, 32768, 65535 },
                new[] { -65536, -1, 0, 65535 },
                new[] { int.MinValue, -1073741824, -1 },
                new[] { 0, 1073741823, int.MaxValue },
                new[] { 17 },
                new[] { 0, 0, 0 }, new[] { 255, 255, 255 },
                new[] { -1, -1, -1 }, new[] { 256, 256, 256 },
                new[] { int.MinValue, int.MinValue, int.MinValue },
                new[] { int.MaxValue, int.MaxValue, int.MaxValue },
                Array.Empty<int>(), null
            };
            foreach (var values in ranges)
            foreach (var collapse in new[] { false, true })
            {
                var context = $"ordinary [{(values == null ? "missing" : string.Join(",", values))}], collapse={collapse}";
                var leaf = CreateNode(new Cell(0, 0, 0, 4), values, values?.Length ?? 3, 0);
                CheckInlining(leaf, new[] { leaf }, collapse, context);
                var nodes = CreateTree(values, values);
                CheckInlining(nodes[0], nodes, collapse, context);
            }
        }

        private static IEnumerable<int[]> Orders(int[] values)
        {
            yield return values;
            yield return values.Reverse().ToArray();
            yield return values.Skip(1).Concat(values.Take(1)).ToArray();
        }

        private static byte[] RescaleOracle(int[] values)
        {
            if (values.Length == 0) return Array.Empty<byte>();
            long lo = values.Min(), hi = values.Max();
            return values.Select(v => lo >= 0 && hi <= 255 ? (byte)v : lo == hi ? (byte)255
                : checked((byte)decimal.Truncate(255.999m * ((long)v - lo) / (hi - lo)))).ToArray();
        }

        private static PointSetNode[] CreateTree(int[] left, int[] right)
        {
            var cell = new Cell(0, 0, 0, 4);
            var a = CreateNode(cell.GetOctant(0), left, left?.Length ?? 3, 0);
            var b = CreateNode(cell.GetOctant(7), right, right?.Length ?? 3, 32);
            var count = Math.Min(3, a.PointCountCell + b.PointCountCell);
            var lod = left == null ? null : Enumerable.Range(1000, count).ToArray();
            var parent = CreateNode(cell, lod, count, 100, new[] { a, b });
            return new[] { parent, a, b };
        }

        private static PointSetNode CreateNode(Cell cell, int[] values, int count, int tag, PointSetNode[] children = null)
        {
            var positions = Enumerable.Range(0, count).Select(i => new V3f(i * 0.125f, 0.25f, -0.5f)).ToArray();
            var props = new List<(Durable.Def, object)>
            {
                (Durable.Octree.NodeId, Guid.NewGuid()), (Durable.Octree.Cell, cell),
                (Durable.Octree.PositionsLocal3f, positions),
                (Durable.Octree.Colors4b, Enumerable.Range(tag, count).Select(i => new C4b((byte)i, (byte)(2 * i), (byte)(255 - i), (byte)255)).ToArray()),
                (Durable.Octree.Classifications1b, Enumerable.Range(tag, count).Select(i => (byte)i).ToArray())
            };
            if (values != null) props.Add((Durable.Octree.Intensities1i, values));
            if (children != null)
            {
                var ids = new Guid[8];
                ids[0] = children[0].Id;
                ids[7] = children[1].Id;
                props.Add((Durable.Octree.SubnodesGuids, ids));
                props.Add((Durable.Octree.BoundingBoxExactGlobal, new Box3d(children.Select(n => n.BoundingBoxExactGlobal))));
                props.Add((Durable.Octree.PointCountTreeLeafs, children.Sum(n => n.PointCountTree)));
                props.Add((Durable.Octree.MinTreeDepth, 1));
                props.Add((Durable.Octree.MaxTreeDepth, 1));
            }
            var result = new PointSetNode(Storage.None, writeToStore: false, props.ToArray());
            if (children != null)
            {
                result.Subnodes[0] = new PersistentRef<IPointCloudNode>(children[0].Id, children[0]);
                result.Subnodes[7] = new PersistentRef<IPointCloudNode>(children[1].Id, children[1]);
            }
            return result;
        }

        private static void CheckInlining(PointSetNode root, PointSetNode[] sources, bool collapse, string context)
        {
            var data = sources.Select(n => n.Properties).ToArray();
            var snapshots = data.Select(d => d.ToDictionary(kv => kv.Key,
                kv => kv.Value is Array a ? a.Clone() : kv.Value)).ToArray();
            var childRefs = sources.Select(n => n.Subnodes?.ToArray()).ToArray();
            var inlined = root.EnumerateOctreeInlined(new InlineConfig(collapse, false));
            var nodes = inlined.Nodes.ToArray();
            Assert.That(nodes.Length, Is.EqualTo(collapse ? 1 : sources.Length), context);
            CheckNode(inlined.Root, root);
            for (var i = 0; i < nodes.Length; i++) CheckNode(nodes[i], sources[i]);
            for (var i = 0; i < sources.Length; i++)
            {
                Assert.That(sources[i].Properties, Is.SameAs(data[i]), context);
                foreach (var kv in snapshots[i]) Assert.That(sources[i].Properties[kv.Key], Is.EqualTo(kv.Value), $"{context}, source {i}, {kv.Key}");
                Assert.That(sources[i].Subnodes, Is.EqualTo(childRefs[i]), context);
            }

            void CheckNode(InlinedNode node, PointSetNode source)
            {
                var inputs = collapse && !source.IsLeaf ? sources.Skip(1).ToArray() : new[] { source };
                var expectedPositions = inputs.SelectMany(n => n.Positions.Value.Select(p => (V3f)((V3d)p + n.Center - source.Center))).ToArray();
                var expectedColors = inputs.SelectMany(n => n.Colors.Value).Select(c => new C3b(c)).ToArray();
                var expectedClasses = inputs.SelectMany(n => n.Classifications.Value).ToArray();
                var expectedIntensities = source.HasIntensities ? RescaleOracle(inputs.SelectMany(n => n.Intensities.Value).ToArray()) : null;
                var expectedChildren = source.IsLeaf || collapse ? null : source.SubnodeIds.Select(id => id ?? Guid.Empty).ToArray();
                CheckPayload(node);
                foreach (var gzip in new[] { false, true }) CheckPayload(new InlinedNode(node.Encode(gzip), gzip));

                void CheckPayload(InlinedNode actual)
                {
                    Assert.That(actual.NodeId, Is.EqualTo(source.Id), context);
                    Assert.That(actual.Cell, Is.EqualTo(source.Cell), context);
                    Assert.That(actual.BoundingBoxExactGlobal, Is.EqualTo(source.BoundingBoxExactGlobal), context);
                    Assert.That(actual.PointCountCell, Is.EqualTo(expectedPositions.Length), context);
                    Assert.That(actual.PointCountTreeLeafs, Is.EqualTo(source.PointCountTree), context);
                    Assert.That(actual.SubnodesGuids, Is.EqualTo(expectedChildren), context);
                    Assert.That(actual.PositionsLocal3f, Is.EqualTo(expectedPositions), context);
                    Assert.That(actual.Colors3b, Is.EqualTo(expectedColors), context);
                    Assert.That(actual.Classifications1b, Is.EqualTo(expectedClasses), context);
                    Assert.That(actual.Intensities1b, Is.EqualTo(expectedIntensities), context);
                    foreach (var p in actual.PositionsLocal3f)
                        Assert.That(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z), Is.True, context);
                }
            }
        }
    }
}
