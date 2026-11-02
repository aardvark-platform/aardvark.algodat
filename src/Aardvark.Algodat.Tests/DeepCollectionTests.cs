using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class DeepCollectionTests
    {
        [Test]
        public void MaximumDepthCollectsTheOrderedLeafFrontOfSmallCells()
        {
            foreach (var exponent in new[] { -2, -3, -6, -12 })
            foreach (var attributes in new[] { false, true })
            foreach (var reload in new[] { false, true })
            {
                using var fixture = new Fixture(exponent, attributes, reload);
                var root = fixture.Root;
                var context = $"exponent={exponent}, attributes={attributes}, reload={reload}";
                Assert.That(root.IsLeaf, Is.False, context);
                Assert.That(root.MaxTreeDepth, Is.EqualTo(3), context);
                Assert.That(root.PointCountCell, Is.EqualTo(1), context);
                Assert.That(root.PointCountTree, Is.EqualTo(30), context);
                var leaves = Front(root, 8).ToArray();
                Assert.That(leaves.Length, Is.EqualTo(15), context);
                CheckChunks(root.Collect(8), leaves, context + ", finite depth");
                CheckChunks(root.Collect(int.MaxValue), leaves, context + ", maximum depth");
            }
        }

        [Test]
        public void OrdinaryDepthsAndExponentControlsKeepEarlyLeavesAndPredicateSelection()
        {
            foreach (var exponent in new[] { -12, -6, -3, -2, -1, 0, 3 })
            foreach (var attributes in new[] { false, true })
            {
                using var fixture = new Fixture(exponent, attributes, false);
                var root = fixture.Root;
                foreach (var depth in new[] { -1, 0, 1, 2, 3, 8 })
                {
                    var context = $"exponent={exponent}, attributes={attributes}, depth={depth}";
                    CheckChunks(root.Collect(depth), Front(root, depth).ToArray(), context);
                    var leaf = root.Subnodes[0].Value;
                    CheckChunks(leaf.Collect(depth), new[] { leaf }, context + ", early leaf");
                }
                if (exponent >= -1)
                    CheckChunks(root.Collect(int.MaxValue), Front(root, 8).ToArray(), $"exponent={exponent}, maximum control");
                CheckChunks(root.Collect(n => n.IsLeaf), Front(root, 8).ToArray(), $"exponent={exponent}, leaf predicate");
                CheckChunks(root.Collect(n => true), new[] { root }, $"exponent={exponent}, root predicate");
                var leafRoot = root.Subnodes[0].Value;
                CheckChunks(leafRoot.Collect(n => throw new Exception("leaf predicate must not run")), new[] { leafRoot }, "leaf bypasses predicate");

                IPointCloudNode missing = null;
                Assert.Throws<NullReferenceException>(() => missing.Collect(0), "integer overload reads root immediately");
                Assert.That(missing.Collect(n => true), Is.Empty, "predicate overload tolerates null root");
                Func<IPointCloudNode, bool> absentPredicate = null;
                CheckChunks(leafRoot.Collect(absentPredicate), new[] { leafRoot }, "leaf ignores null predicate");
                var invalidPredicate = root.Collect(absentPredicate);
                Assert.Throws<NullReferenceException>(() => invalidPredicate.ToArray(), "nonleaf invokes predicate during enumeration");
                var negativeCellDepth = root.QueryCell(root.Cell).GetPoints(-1);
                var cellError = Assert.Throws<ArgumentException>(() => negativeCellDepth.ToArray());
                Assert.That(cellError.ParamName, Is.EqualTo("fromRelativeDepth"));
                var column = new Cell2d(root.Cell.X, root.Cell.Y, root.Cell.Exponent);
                var columnError = Assert.Throws<ArgumentException>(() => root.CollectColumnXY(column, -1));
                Assert.That(columnError.ParamName, Is.EqualTo("fromRelativeDepth"));
                var nullError = Assert.Throws<ArgumentNullException>(() => missing.CollectColumnXY(column, 0));
                Assert.That(nullError.ParamName, Is.EqualTo("root"));
                Assert.Throws<InvalidOperationException>(() => root.CollectColumnXY(new Cell2d(exponent), 0));
            }
        }

        [Test]
        public void CellAndContainingColumnQueriesKeepDeepOrderedAttributes()
        {
            foreach (var exponent in new[] { -12, -6, -3, -2, -1, 0, 3 })
            foreach (var attributes in new[] { false, true })
            {
                using var fixture = new Fixture(exponent, attributes, true);
                var root = fixture.Root;
                foreach (var node in new[] { root, root.Subnodes[3].Value })
                foreach (var depth in new[] { 0, 1, 8, int.MaxValue })
                {
                    var context = $"exponent={exponent}, attributes={attributes}, node={node.Cell}, depth={depth}";
                    var expected = Front(node, depth).ToArray();
                    CheckChunks(root.QueryCell(node.Cell).GetPoints(depth), expected, context + ", cell");
                    var column = new Cell2d(node.Cell.X, node.Cell.Y, node.Cell.Exponent);
                    CheckChunks(node.CollectColumnXY(column, depth), expected, context + ", exact column");
                    CheckChunks(node.CollectColumnXY(column.Parent, depth), expected, context + ", containing column");
                }
            }
        }

        [Test]
        public void DeepResultsOwnAttributesAcrossRepeatedEnumerations()
        {
            using var fixture = new Fixture(-3, true, false);
            var root = fixture.Root;
            var leaves = Front(root, 8).ToArray();
            var enumerable = root.Collect(int.MaxValue);
            var first = enumerable.ToArray();
            var second = enumerable.ToArray();
            CheckChunks(first, leaves, "first enumeration");
            CheckChunks(second, leaves, "second enumeration");
            var before = leaves[0].Positions.Value.ToArray();
            first[0].Positions[0] = new V3d(-9);
            first[0].Colors[0] = C4b.Black;
            first[0].Normals[0] = new V3f(-9);
            first[0].Intensities[0] = -999;
            first[0].Classifications[0] = 255;
            ((int[])first[0].PartIndices)[0] = -999;
            Assert.That(leaves[0].Positions.Value, Is.EqualTo(before), "local positions unchanged");
            CheckChunks(second, leaves, "independent second result");
            CheckChunks(root.Collect(8), leaves, "source after mutation");
            CheckChunks(root.Collect(int.MaxValue), leaves, "repeated maximum query");

        }

        // Structural depth oracle: no exponent subtraction and no collection/query helpers.
        private static IEnumerable<IPointCloudNode> Front(IPointCloudNode node, int remaining)
        {
            if (remaining <= 0 || node.IsLeaf) yield return node;
            else
                foreach (var child in node.Subnodes)
                    if (child != null)
                        foreach (var selected in Front(child.Value, remaining - 1)) yield return selected;
        }

        private static void CheckChunks(IEnumerable<Chunk> results, IPointCloudNode[] expected, string context)
        {
            var chunks = results.ToArray();
            Assert.That(chunks.Length, Is.EqualTo(expected.Length), context);
            for (var i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];
                var node = expected[i];
                var where = $"{context}, chunk={i}, cell={node.Cell}";
                Assert.That(chunk.Count, Is.EqualTo(node.PointCountCell), where);
                var positions = node.Positions.Value.Select(p => node.Center + (V3d)p).ToArray();
                foreach (var p in positions.Concat(chunk.Positions))
                    Assert.That(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z), Is.True, where);
                Assert.That(chunk.Positions, Is.EqualTo(positions), where);
                Assert.That(chunk.HasColors, Is.EqualTo(node.HasColors), where);
                Assert.That(chunk.HasNormals, Is.EqualTo(node.HasNormals), where);
                Assert.That(chunk.HasIntensities, Is.EqualTo(node.HasIntensities), where);
                Assert.That(chunk.HasClassifications, Is.EqualTo(node.HasClassifications), where);
                Assert.That(chunk.HasPartIndices, Is.EqualTo(node.HasPartIndices), where);
                if (node.HasColors) CheckOwned(chunk.Colors, node.Colors.Value, where + ", colors");
                if (node.HasNormals) CheckOwned(chunk.Normals, node.Normals.Value, where + ", normals");
                if (node.HasIntensities) CheckOwned(chunk.Intensities, node.Intensities.Value, where + ", intensities");
                if (node.HasClassifications) CheckOwned(chunk.Classifications, node.Classifications.Value, where + ", classifications");
                if (node.HasPartIndices)
                {
                    CheckOwned((int[])chunk.PartIndices, (int[])node.PartIndices, where + ", part indices");
                    Assert.That(chunk.PartIndexRange, Is.EqualTo(new Range1i((int[])node.PartIndices)), where);
                }
            }
        }

        private static void CheckOwned<T>(IList<T> actual, T[] expected, string context)
        {
            Assert.That(actual, Is.EqualTo(expected), context);
            Assert.That(actual, Is.Not.SameAs(expected), context);
        }

        // Stored sparse tree with genuine representative samples on inner nodes,
        // leaves at depths 1/2/3, and distinguishable ordered per-point attributes.
        private sealed class Fixture : IDisposable
        {
            public readonly Storage Storage = PointCloud.CreateInMemoryStore();
            public readonly IPointCloudNode Root;
            private int nextPoint;

            public Fixture(int exponent, bool attributes, bool reload)
            {
                Root = Build(new Cell(1, 2, 3, exponent), 3, attributes);
                if (reload)
                {
                    Storage.Cache.Clear();
                    Root = Storage.GetPointCloudNode(Root.Id);
                }
            }

            private IPointCloudNode Build(Cell cell, int remaining, bool attributes)
            {
                var children = new IPointCloudNode[8];
                if (remaining > 0)
                    foreach (var slot in new[] { 0, 3, 7 })
                        children[slot] = Build(cell.GetOctant(slot), slot == 0 ? 0 : remaining - 1, attributes);
                var size = cell.BoundingBox.Size.X;
                var absolute = remaining == 0
                    ? new[] { cell.GetCenter() + new V3d(-size / 8, size / 16, -size / 16), cell.GetCenter() + new V3d(size / 8, -size / 16, size / 16) }
                    : new[] { children[0].PositionsAbsolute[0] };
                var positions = absolute.Select(p => (V3f)(p - cell.GetCenter())).ToArray();
                var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(Durable.Octree.NodeId, Guid.NewGuid())
                    .Add(Durable.Octree.Cell, cell)
                    .Add(Durable.Octree.PositionsLocal3fReference, Store(positions));
                if (remaining > 0)
                {
                    var bounds = new Box3d(children.Where(c => c != null).Select(c => c.BoundingBoxExactGlobal));
                    data = data.Add(Durable.Octree.SubnodesGuids, children.Select(c => c == null ? Guid.Empty : c.Id).ToArray())
                        .Add(Durable.Octree.BoundingBoxExactGlobal, bounds)
                        .Add(Durable.Octree.BoundingBoxExactLocal, (Box3f)(bounds - cell.GetCenter()));
                }
                if (attributes)
                {
                    var ids = remaining == 0 ? new[] { ++nextPoint, ++nextPoint } : new[] { children[0].Intensities.Value[0] };
                    data = data
                        .Add(Durable.Octree.Colors4bReference, Store(ids.Select(i => new C4b((byte)i, (byte)(3 * i), (byte)(255 - i), (byte)201)).ToArray()))
                        .Add(Durable.Octree.Normals3fReference, Store(ids.Select(i => new V3f(i, -i, 2 * i)).ToArray()))
                        .Add(Durable.Octree.Intensities1iReference, Store(ids))
                        .Add(Durable.Octree.Classifications1bReference, Store(ids.Select(i => (byte)(i % 251)).ToArray()))
                        .Add(Durable.Octree.PerPointPartIndex1iReference, Store(ids.Select(i => i + 1000).ToArray()))
                        .Add(Durable.Octree.PartIndexRange, remaining == 0
                            ? new Range1i(ids.Min() + 1000, ids.Max() + 1000)
                            : new Range1i(children.Where(c => c != null).Min(c => c.PartIndexRange.Value.Min),
                                children.Where(c => c != null).Max(c => c.PartIndexRange.Value.Max)));
                }
                return new PointSetNode(data, Storage, writeToStore: true);
            }

            private Guid Store<T>(T[] array)
            {
                var id = Guid.NewGuid();
                Storage.Add(id, array);
                return id;
            }

            public void Dispose() => Storage.Dispose();
        }
    }
}
