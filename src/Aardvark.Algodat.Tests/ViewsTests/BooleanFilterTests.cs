using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class BooleanFilterTests
    {
        public enum SelectionKind { Null, Empty, Full, Sparse }

        private static readonly V3d[] RegressionPoints =
        {
            new V3d(0.25, 0.25, 0.25), new V3d(0.75, 0.25, 0.25), new V3d(1.25, 0.25, 0.25)
        };

        private Storage storage;
        private IPointCloudNode node;
        private IPointCloudNode oracleNode;
        private IPointCloudNode tree;

        [OneTimeSetUp]
        public void SetUp()
        {
            storage = PointCloud.CreateInMemoryStore(cache: default);
            node = CreateNode(new Cell(RegressionPoints), RegressionPoints);
            oracleNode = CreateNode(new Cell(0, 0, 0, 5),
                Enumerable.Range(0, 65).Select(i => new V3d(0.25 * i, 0.25, 0.25)).ToArray());
            tree = CreateTree(new Cell(0, 0, 0, 3), 2);
        }

        [OneTimeTearDown]
        public void TearDown() => storage.Dispose();

        private IPointCloudNode CreateNode(Cell cell, V3d[] positions, IPointCloudNode[] children = null)
        {
            var local = positions.Select(p => (V3f)(p - cell.GetCenter())).ToArray();
            var positionId = Guid.NewGuid();
            storage.Add(positionId, local);
            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.NodeId, Guid.NewGuid())
                .Add(Durable.Octree.Cell, cell)
                .Add(Durable.Octree.PositionsLocal3fReference, positionId)
                .Add(Durable.Octree.BoundingBoxExactLocal, new Box3f(local))
                .Add(Durable.Octree.BoundingBoxExactGlobal, children == null
                    ? new Box3d(positions) : new Box3d(children.Select(c => c.BoundingBoxExactGlobal)));
            if (children != null)
            {
                var ids = new Guid[8];
                ids[0] = children[0].Id;
                ids[1] = children[1].Id;
                data = data.Add(Durable.Octree.SubnodesGuids, ids);
            }
            return new PointSetNode(data, storage, writeToStore: true);
        }

        private IPointCloudNode CreateTree(Cell cell, int depth)
        {
            if (depth == 0)
                return CreateNode(cell, RegressionPoints.Select(p => p + cell.BoundingBox.Min).ToArray());
            var children = new[] { CreateTree(cell.GetOctant(0), depth - 1), CreateTree(cell.GetOctant(1), depth - 1) };
            return CreateNode(cell, children.Select(c => c.PositionsAbsolute[0]).ToArray(), children);
        }

        private static FilterInsideBox3d Box(double minX, double maxX)
            => new FilterInsideBox3d(new Box3d(new V3d(minX, 0, 0), new V3d(maxX, 1, 1)));

        private static IFilter Compose(bool and, IFilter left, IFilter right)
            => and ? new FilterAnd(left, right) : new FilterOr(left, right);

        private static IEnumerable<int> Domain(IPointCloudNode n, HashSet<int> selected)
            => selected ?? (IEnumerable<int>)Enumerable.Range(0, n.PointCountCell);

        private static HashSet<int> Selection(SelectionKind kind, int count)
            => kind switch
            {
                SelectionKind.Null => null,
                SelectionKind.Empty => new HashSet<int>(),
                SelectionKind.Full => new HashSet<int>(Enumerable.Range(0, count)),
                _ => new HashSet<int>(Enumerable.Range(0, count).Where(i => i % 2 == 0))
            };

        private sealed class ProbeFilter : IFilter
        {
            public bool Inside;
            public bool Outside;
            public int InsideCalls;
            public int OutsideCalls;
            public int PointCalls;
            public HashSet<int> LastSelection;
            public HashSet<int> LastResult;
            public Func<IPointCloudNode, HashSet<int>, HashSet<int>> Select;

            public bool IsFullyInside(IPointCloudNode n) { InsideCalls++; return Inside; }
            public bool IsFullyOutside(IPointCloudNode n) { OutsideCalls++; return Outside; }
            public HashSet<int> FilterPoints(IPointCloudNode n, HashSet<int> selected = null)
            {
                PointCalls++;
                LastSelection = selected;
                return LastResult = Select(n, selected);
            }
            public bool Equals(IFilter other) => ReferenceEquals(this, other);
            public JsonNode Serialize() => throw new NotSupportedException();
        }

        private static ProbeFilter SelectIndices(IEnumerable<int> accepted, bool aliasPassThrough = false)
        {
            var set = new HashSet<int>(accepted);
            return new ProbeFilter
            {
                Select = (n, selected) => aliasPassThrough && selected != null && selected.All(set.Contains)
                    ? selected : new HashSet<int>(Domain(n, selected).Where(set.Contains))
            };
        }

        private static void AssertEvaluation(IFilter filter, IPointCloudNode n, HashSet<int> selected, IEnumerable<int> expected)
        {
            var original = selected?.OrderBy(i => i).ToArray();
            var result = filter.FilterPoints(n, selected);
            Assert.That(result, Is.EquivalentTo(expected));
            if (selected != null) Assert.That(selected, Is.EquivalentTo(original), "The caller's selection was modified.");
        }

        [Test]
        public void OrDoesNotPrunePartiallyMatchingLeaf([Values] bool swapped)
        {
            var left = Box(0, 1);
            var right = Box(10, 11);
            var filter = swapped ? new FilterOr(right, left) : new FilterOr(left, right);
            Assert.That(filter.IsFullyInside(node), Is.False);
            Assert.That(filter.IsFullyOutside(node), Is.False);
            var view = FilteredNode.CreateTransient(node, filter);
            Assert.That(view.PointCountCell, Is.EqualTo(2));
            Assert.That(view.QueryAllPoints().SelectMany(c => c.Positions), Is.EquivalentTo(RegressionPoints.Take(2)));
        }

        [Test]
        public void NestedPassThroughDoesNotShrinkSiblingDomain()
        {
            var all = Box(0, 2);
            var filter = new FilterOr(new FilterAnd(new FilterAnd(all, all), Box(0, 0.5)), Box(1, 1.5));
            var selected = new HashSet<int> { 0, 1, 2 };
            for (var repeat = 0; repeat < 4; repeat++)
                AssertEvaluation(filter, node, selected, new[] { 0, 2 });
        }

        [Test, Combinatorial]
        public void ClassificationTruthTablesShortCircuit(
            [Values] bool and, [Values] bool inside, [Values] bool leftValue, [Values] bool rightValue)
        {
            var left = new ProbeFilter { Inside = leftValue, Outside = leftValue };
            var right = new ProbeFilter { Inside = rightValue, Outside = rightValue };
            var filter = Compose(and, left, right);
            var conjunction = inside ? and : !and;
            var expected = conjunction ? leftValue && rightValue : leftValue || rightValue;
            Assert.That(inside ? filter.IsFullyInside(node) : filter.IsFullyOutside(node), Is.EqualTo(expected));
            Assert.That(inside ? left.InsideCalls : left.OutsideCalls, Is.EqualTo(1));
            Assert.That(inside ? right.InsideCalls : right.OutsideCalls, Is.EqualTo(conjunction == leftValue ? 1 : 0));
            Assert.That(inside ? left.OutsideCalls + right.OutsideCalls : left.InsideCalls + right.InsideCalls, Is.Zero);
            Assert.That(left.PointCalls + right.PointCalls, Is.Zero);
        }

        [Test]
        public void PartialOperandsRemainConservativelyClassified()
        {
            var low = Box(0, 1);
            var high = Box(1, 2);
            var union = new FilterOr(low, high);
            var intersection = new FilterAnd(low, high);
            Assert.That(union.FilterPoints(node), Is.EquivalentTo(new[] { 0, 1, 2 }));
            Assert.That(union.IsFullyInside(node), Is.False, "Partial coverage does not prove either operand fully inside.");
            Assert.That(intersection.FilterPoints(node), Is.Empty);
            Assert.That(intersection.IsFullyOutside(node), Is.False, "Partial operands do not prove either operand fully outside.");
        }

        [Test, Combinatorial]
        public void SelectionDomainsSurviveNestedAndSwappedOperands(
            [Values] bool and, [Values] bool swapped, [Values] SelectionKind selection, [Values] bool aliasPassThrough)
        {
            var selected = Selection(selection, 3);
            var all = SelectIndices(new[] { 0, 1, 2 }, aliasPassThrough);
            var a = new FilterAnd(new FilterAnd(all, all), SelectIndices(new[] { 0, 1 }, aliasPassThrough));
            var b = new FilterOr(SelectIndices(new[] { 2 }, aliasPassThrough), SelectIndices(new[] { 0 }, aliasPassThrough));
            var filter = swapped ? Compose(and, b, a) : Compose(and, a, b);
            var expected = Domain(node, selected).Where(i => and ? i == 0 : true).ToArray();
            for (var repeat = 0; repeat < 3; repeat++) AssertEvaluation(filter, node, selected, expected);
        }

        [Test, Combinatorial]
        public void EmptySelectionSkipsBothOperands([Values] bool and)
        {
            var left = new ProbeFilter { Select = (_, _) => throw new AssertionException("Left was evaluated.") };
            var right = new ProbeFilter { Select = (_, _) => throw new AssertionException("Right was evaluated.") };
            var selected = new HashSet<int>();
            Assert.That(Compose(and, left, right).FilterPoints(node, selected), Is.SameAs(selected));
            Assert.That(left.PointCalls + right.PointCalls, Is.Zero);
        }

        [Test]
        public void AndSkipsRightAfterEmptyLeft([Values] SelectionKind selection)
        {
            var left = SelectIndices(Array.Empty<int>());
            var right = new ProbeFilter { Select = (_, _) => throw new AssertionException("Right was evaluated.") };
            var selected = Selection(selection, 3);
            AssertEvaluation(new FilterAnd(left, right), node, selected, Array.Empty<int>());
            Assert.That(right.PointCalls, Is.Zero);
            Assert.That(left.PointCalls, Is.EqualTo(selection == SelectionKind.Empty ? 0 : 1));
        }

        [Test, Combinatorial]
        public void AndNarrowsProvidedSelectionsAndKeepsNullScan([Values] SelectionKind selection, [Values] bool alias)
        {
            var left = SelectIndices(new[] { 0, 2 }, alias);
            var right = SelectIndices(new[] { 0, 2 }, true);
            var selected = Selection(selection, 3);
            var expected = Domain(node, selected).Where(i => i % 2 == 0).ToArray();
            AssertEvaluation(new FilterAnd(left, right), node, selected, expected);
            if (expected.Length > 0)
            {
                if (selected == null)
                    Assert.That(right.LastSelection, Is.Null, "A null domain retains the contiguous scan.");
                else
                {
                    var expectedDomain = left.LastResult.Count == selected.Count ? selected : left.LastResult;
                    Assert.That(right.LastSelection, Is.SameAs(expectedDomain));
                    Assert.That(right.LastResult, Is.SameAs(expectedDomain), "Pass-through aliases remain safe without copying.");
                }
                Assert.That(left.LastResult, Is.EquivalentTo(expected));
            }
        }

        [Test]
        public void AndNarrowsSparseLeftResultBeforeCallingRight()
        {
            var left = SelectIndices(new[] { 0 });
            var right = SelectIndices(new[] { 0, 2 }, true);
            AssertEvaluation(new FilterAnd(left, right), node, new HashSet<int> { 0, 1, 2 }, new[] { 0 });
            Assert.That(right.LastSelection, Is.SameAs(left.LastResult));
            Assert.That(right.LastResult, Is.SameAs(left.LastResult));
        }

        [Test]
        public void AndIntersectsOwnedNullDomainResultsWithinLeftDomain()
        {
            var left = SelectIndices(new[] { 0, 1 });
            var right = SelectIndices(new[] { 1, 2 });
            AssertEvaluation(new FilterAnd(left, right), node, null, new[] { 1 });
            Assert.That(right.LastSelection, Is.Null);
            Assert.That(right.LastResult, Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public void AndKeepsNullDomainWhenLeftAcceptsEveryPoint()
        {
            var left = SelectIndices(new[] { 0, 1, 2 });
            var right = SelectIndices(new[] { 0, 2 });
            AssertEvaluation(new FilterAnd(left, right), node, null, new[] { 0, 2 });
            Assert.That(right.PointCalls, Is.EqualTo(1));
            Assert.That(right.LastSelection, Is.Null, "Keep the primitive filter's all-points scan when no narrowing occurred.");
        }

        [Test]
        public void NullDomainOnEmptyNodeReturnsAnOwnedEmptySet([Values] bool and)
        {
            var left = SelectIndices(Array.Empty<int>());
            var right = SelectIndices(Array.Empty<int>());
            var filter = Compose(and, left, right);
            var result = filter.FilterPoints(PointSetNode.Empty);
            Assert.That(result, Is.Not.Null.And.Empty);
            result.Add(0);
            Assert.That(filter.FilterPoints(PointSetNode.Empty), Is.Empty);
            Assert.That(right.PointCalls, Is.Zero);
        }

        [Test]
        public void OwnedPartialResultsCanBeChangedWithoutAffectingLaterEvaluations([Values] bool and)
        {
            var selected = new HashSet<int> { 0, 1, 2 };
            var filter = and
                ? Compose(true, SelectIndices(selected, true), SelectIndices(new[] { 0, 2 }))
                : Compose(false, SelectIndices(new[] { 0 }), SelectIndices(new[] { 2 }));
            var result = filter.FilterPoints(node, selected);
            Assert.That(result, Is.Not.SameAs(selected));
            result.Clear();
            AssertEvaluation(filter, node, selected, new[] { 0, 2 });
            Assert.That(selected, Is.EquivalentTo(new[] { 0, 1, 2 }));
        }

        [Test, Combinatorial]
        public void OrSkipsRightAfterFullLeft([Values] SelectionKind selection, [Values] bool alias)
        {
            var left = SelectIndices(new[] { 0, 1, 2 }, alias);
            var right = new ProbeFilter { Select = (_, _) => throw new AssertionException("Right was evaluated.") };
            var selected = Selection(selection, 3);
            AssertEvaluation(new FilterOr(left, right), node, selected, Domain(node, selected).ToArray());
            Assert.That(right.PointCalls, Is.Zero);
        }

        [Test, Combinatorial]
        public void OrEvaluatesIndependentDomainsAndReusesSafeResults(
            [Values(0, 1, 2, 3, 4)] int shape, [Values] bool swapped, [Values] SelectionKind selection)
        {
            var masks = new[]
            {
                (new[] { 0 }, new[] { 2 }),
                (Array.Empty<int>(), new[] { 0 }),
                (new[] { 0 }, Array.Empty<int>()),
                (new[] { 0 }, new[] { 0, 1, 2 }),
                (new[] { 0 }, new[] { 0, 1 })
            };
            var (a, b) = masks[shape];
            if (swapped) (a, b) = (b, a);
            var left = SelectIndices(a, true);
            var right = SelectIndices(b, true);
            var selected = Selection(selection, 3);
            var expected = Domain(node, selected).Where(i => a.Contains(i) || b.Contains(i)).ToArray();
            var before = selected?.ToArray();
            var result = new FilterOr(left, right).FilterPoints(node, selected);
            Assert.That(result, Is.EquivalentTo(expected));
            if (selected != null) Assert.That(selected, Is.EquivalentTo(before));
            if (right.PointCalls > 0)
            {
                Assert.That(left.LastSelection, Is.SameAs(selected));
                Assert.That(right.LastSelection, Is.SameAs(selected));
                Assert.That(ReferenceEquals(result, left.LastResult) || ReferenceEquals(result, right.LastResult), Is.True);
            }
        }

        private sealed class ExpressionCase
        {
            public IFilter Filter;
            public Func<HashSet<int>, HashSet<int>> Evaluate;
        }

        private static ExpressionCase RandomExpression(Random random, int count, int depth)
        {
            if (depth == 0 || random.Next(4) == 0)
            {
                var mode = random.Next(5);
                var accepted = Enumerable.Range(0, count).Where(i => mode == 0 || (mode != 1 && random.Next(2) == 0)).ToArray();
                var filter = SelectIndices(accepted, random.Next(2) == 0);
                return new ExpressionCase
                {
                    Filter = filter,
                    Evaluate = domain => new HashSet<int>(domain.Where(accepted.Contains))
                };
            }
            var left = RandomExpression(random, count, depth - 1);
            var right = RandomExpression(random, count, depth - 1);
            var and = random.Next(2) == 0;
            return new ExpressionCase
            {
                Filter = Compose(and, left.Filter, right.Filter),
                Evaluate = domain =>
                {
                    // Independent oracle: evaluate BOTH operands on the original domain,
                    // then combine freshly owned sets (not the implementation's narrowed-AND path).
                    var a = left.Evaluate(domain);
                    var b = right.Evaluate(domain);
                    if (and) a.IntersectWith(b); else a.UnionWith(b);
                    return a;
                }
            };
        }

        [Test]
        public void SeededExpressionsMatchIndependentSetOracle([Values(7, 101, 2026)] int seed)
        {
            var random = new Random(seed);
            for (var i = 0; i < 200; i++)
            {
                var expression = RandomExpression(random, oracleNode.PointCountCell, 6);
                foreach (SelectionKind kind in Enum.GetValues(typeof(SelectionKind)))
                {
                    var selected = Selection(kind, oracleNode.PointCountCell);
                    var expected = expression.Evaluate(new HashSet<int>(Domain(oracleNode, selected)));
                    AssertEvaluation(expression.Filter, oracleNode, selected, expected);
                    AssertEvaluation(expression.Filter, oracleNode, selected, expected);
                }
            }
        }

        [Test, Combinatorial]
        public void SerializedFiltersPreserveCompositionAndSelection(
            [Values] bool and, [Values] bool swapped, [Values] SelectionKind selection)
        {
            IFilter left = new FilterOr(Box(0, 1), Box(10, 11));
            IFilter right = new FilterOr(Box(1, 1.5), Box(0, 0.5));
            var filter = swapped ? Compose(and, right, left) : Compose(and, left, right);
            var json = filter.Serialize().ToJsonString();
            var decoded = Filter.Deserialize(json);
            Assert.That(decoded.Equals(filter), Is.True);
            Assert.That(filter.Equals(decoded), Is.True);
            Assert.That(decoded.Serialize().ToJsonString(), Is.EqualTo(json));
            var selected = Selection(selection, 3);
            var expected = Domain(node, selected).Where(i => !and || i == 0).ToArray();
            AssertEvaluation(decoded, node, selected, expected);
            Assert.That(decoded.IsFullyInside(node), Is.EqualTo(filter.IsFullyInside(node)));
            Assert.That(decoded.IsFullyOutside(node), Is.EqualTo(filter.IsFullyOutside(node)));
        }

        [Test]
        public void EqualityRemainsOrderedAndTypeSensitive([Values] bool and)
        {
            var left = Box(0, 1);
            var right = Box(1, 2);
            var filter = Compose(and, left, right);
            Assert.That(filter.Equals(Compose(and, left, right)), Is.True);
            Assert.That(filter.Equals(Compose(and, right, left)), Is.False);
            Assert.That(filter.Equals(Compose(and, left, left)), Is.False);
            Assert.That(filter.Equals(Compose(!and, left, right)), Is.False);
            Assert.That(filter.Equals(null), Is.False);
        }

        [Test, Combinatorial]
        public void ConstructorNullOperandContractsRemainUnchanged([Values] bool and, [Values] bool leftNull)
        {
            var operand = Box(0, 1);
            var error = Assert.Throws<ArgumentNullException>(() => Compose(and, leftNull ? null : operand, leftNull ? operand : null));
            Assert.That(error.ParamName, Is.EqualTo(leftNull ? "left" : "right"));
        }

        [Test, Combinatorial]
        public void FilteredQueriesMatchPredicatesAtLeafAndMultipleLevels(
            [Values] bool multilevel, [Values] bool and, [Values] bool swapped, [Values] bool roundTrip)
        {
            var source = multilevel ? tree : node;
            Assert.That(source.IsLeaf, Is.EqualTo(!multilevel));
            if (multilevel) Assert.That(source.Subnodes[0].Value.IsLeaf, Is.False);
            var limit = multilevel ? 4.5 : 1.0;
            IFilter a = new FilterOr(Box(0, limit), Box(100, 101));
            IFilter b = new FilterOr(Box(0, 0.5), Box(1, 7));
            var filter = swapped ? Compose(and, b, a) : Compose(and, a, b);
            if (roundTrip) filter = Filter.Deserialize(filter.Serialize().ToJsonString());
            var original = source.QueryAllPoints().SelectMany(c => c.Positions).ToArray();
            var expected = original.Where(p => and
                ? p.X <= limit && (p.X <= 0.5 || (p.X >= 1 && p.X <= 7))
                : p.X <= limit || p.X <= 0.5 || (p.X >= 1 && p.X <= 7)).ToArray();
            var view = FilteredNode.CreateTransient(source, filter);
            Assert.That(view.QueryAllPoints().SelectMany(c => c.Positions), Is.EquivalentTo(expected));
            Assert.That(view.QueryAllPoints().SelectMany(c => c.Positions), Is.EquivalentTo(expected));
            Assert.That(source.QueryAllPoints().SelectMany(c => c.Positions), Is.EquivalentTo(original));
        }
    }
}
