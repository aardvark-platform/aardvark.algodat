using Aardvark.Base;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Linq;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class PointsNearObjectTests
{
    private static PointsNearObject<object> Create(int mask, int count = 5)
    {
        var indices = Enumerable.Range(0, count).ToArray();
        return new PointsNearObject<object>(new object(), 8.0,
            indices.Select(i => new V3d(10 + i, -2 * i, .25 * i)).ToArray(),
            (mask & 1) == 0 ? null : indices.Select(i => new C4b((byte)(i + 1), (byte)(i + 2), (byte)(i + 3), (byte)(200 + i))).ToArray(),
            (mask & 2) == 0 ? null : indices.Select(i => new V3f(i + 1, -i, .5f)).ToArray(),
            (mask & 4) == 0 ? null : indices.Select(i => i * 10 - 20).ToArray(),
            (mask & 8) == 0 ? null : indices.Select(i => 100 + i * 7).ToArray(),
            (mask & 16) == 0 ? null : indices.Select(i => (byte)(i + 5)).ToArray(),
            new[] { .75, .125, 2.0, .5, 1.5 }.Take(count).ToArray());
    }

    private static void CheckPrefix<T>(T[] source, T[] actual, int count, string context)
    {
        if (source == null)
        {
            Assert.That(actual, Is.Null, context);
            return;
        }

        Assert.That(actual, Is.EqualTo(source.Take(count).ToArray()), context);
        if (count > 0 && actual != null)
        {
            Assert.That(actual, Is.Not.SameAs(source), context);
            var before = source.ToArray();
            actual[0] = default;
            Assert.That(source, Is.EqualTo(before), context + ", source after result mutation");
        }
    }

    private static void CheckSource(PointsNearObject<object> source, PointsNearObject<object> before, string context)
    {
        Assert.That(source.Count, Is.EqualTo(before.Count), context);
        Assert.That(source.MaxDistance, Is.EqualTo(before.MaxDistance), context);
        Assert.That(source.Positions, Is.EqualTo(before.Positions), context);
        Assert.That(source.Colors, Is.EqualTo(before.Colors), context);
        Assert.That(source.Normals, Is.EqualTo(before.Normals), context);
        Assert.That(source.Intensities, Is.EqualTo(before.Intensities), context);
        Assert.That(source.PartIndices, Is.EqualTo(before.PartIndices), context);
        Assert.That(source.Classifications, Is.EqualTo(before.Classifications), context);
        Assert.That(source.Distances, Is.EqualTo(before.Distances), context);
    }

    [Test]
    public void ZeroPrefixesRetainObjectAndOptionalAttributePresence()
    {
        Assert.Multiple(() =>
        {
            for (var mask = 0; mask < 32; mask++)
            {
                var source = Create(mask);
                var context = $"attributes={mask}, count=0";
                PointsNearObject<object> result = null;
                Assert.DoesNotThrow(() => result = source.Take(0), context);
                if (result == null) continue;

                Assert.That(result, Is.Not.SameAs(source), context);
                Assert.That(result.Object, Is.SameAs(source.Object), context);
                Assert.That(result.Count, Is.Zero, context);
                Assert.That(result.IsEmpty, Is.True, context);
                Assert.That(result.MaxDistance, Is.Zero, context);
                CheckPrefix(source.Positions, result.Positions, 0, context + ", positions");
                CheckPrefix(source.Colors, result.Colors, 0, context + ", colors");
                CheckPrefix(source.Normals, result.Normals, 0, context + ", normals");
                CheckPrefix(source.Intensities, result.Intensities, 0, context + ", intensities");
                CheckPrefix(source.PartIndices, result.PartIndices, 0, context + ", parts");
                CheckPrefix(source.Classifications, result.Classifications, 0, context + ", classifications");
                CheckPrefix(source.Distances, result.Distances, 0, context + ", distances");
                CheckSource(source, Create(mask), context);
            }
        });
    }

    [Test]
    public void PartialPrefixesKeepOrderedAlignedOwnedValuesAndMaximumDistance()
    {
        Assert.Multiple(() =>
        {
            for (var mask = 0; mask < 32; mask++)
            foreach (var count in new[] { 1, 2, 4 })
            {
                var source = Create(mask);
                var context = $"attributes={mask}, count={count}";
                var result = source.Take(count);
                var repeat = source.Take(count);
                Assert.That(result, Is.Not.SameAs(source), context);
                Assert.That(result, Is.Not.SameAs(repeat), context);
                Assert.That(result.Object, Is.SameAs(source.Object), context);
                Assert.That(result.Count, Is.EqualTo(count), context);
                Assert.That(result.IsEmpty, Is.False, context);
                var expectedMaximum = 0.0;
                for (var i = 0; i < count; i++) expectedMaximum = Math.Max(expectedMaximum, source.Distances[i]);
                Assert.That(result.MaxDistance, Is.EqualTo(expectedMaximum), context);
                Assert.That(result.Positions.All(p => double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z)), Is.True, context);
                CheckPrefix(source.Positions, result.Positions, count, context + ", positions");
                CheckPrefix(source.Colors, result.Colors, count, context + ", colors");
                CheckPrefix(source.Normals, result.Normals, count, context + ", normals");
                CheckPrefix(source.Intensities, result.Intensities, count, context + ", intensities");
                CheckPrefix(source.PartIndices, result.PartIndices, count, context + ", parts");
                CheckPrefix(source.Classifications, result.Classifications, count, context + ", classifications");
                CheckPrefix(source.Distances, result.Distances, count, context + ", distances");
                Assert.That(repeat.Positions, Is.EqualTo(source.Positions.Take(count).ToArray()), context);
                Assert.That(repeat.Distances, Is.EqualTo(source.Distances.Take(count).ToArray()), context);
                CheckSource(source, Create(mask), context);
            }
        });
    }

    [Test]
    public void NegativeCountsAndWholePrefixesKeepErrorsAndIdentityIncludingEmptySources()
    {
        Assert.Multiple(() =>
        {
            for (var mask = 0; mask < 32; mask++)
            foreach (var length in new[] { 0, 5 })
            {
                var source = Create(mask, length);
                foreach (var count in new[] { -1, int.MinValue })
                {
                    var context = $"attributes={mask}, length={length}, count={count}";
                    var error = Assert.Throws<ArgumentOutOfRangeException>(() => source.Take(count), context);
                    Assert.That(error?.ParamName, Is.EqualTo("count"), context);
                }
                foreach (var count in new[] { length, length + 1, int.MaxValue })
                    Assert.That(source.Take(count), Is.SameAs(source), $"attributes={mask}, length={length}, count={count}");
            }
            Assert.That(PointsNearObject<object>.Empty.Take(0), Is.SameAs(PointsNearObject<object>.Empty));
            Assert.That(PointsNearObject<object>.Empty.Take(int.MaxValue), Is.SameAs(PointsNearObject<object>.Empty));
            Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => PointsNearObject<object>.Empty.Take(-1))?.ParamName, Is.EqualTo("count"));
        });
    }

    [Test]
    public void MissingDistancesKeepPositivePrefixFailuresAndWholeResultIdentity()
    {
        Assert.Multiple(() =>
        {
            for (var mask = 0; mask < 32; mask++)
            foreach (var distances in new double[][] { null, Array.Empty<double>() })
            {
                var data = Create(mask);
                var source = new PointsNearObject<object>(data.Object, data.MaxDistance,
                    data.Positions, data.Colors, data.Normals, data.Intensities, data.PartIndices, data.Classifications, distances);
                var context = $"attributes={mask}, distances={(distances == null ? "null" : "empty")}";
                foreach (var count in new[] { 1, 4 })
                    Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => source.Take(count), context)?.ParamName, Is.EqualTo("maxDistance"), context);
                Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => source.Take(-1), context)?.ParamName, Is.EqualTo("count"), context);
                Assert.That(source.Take(source.Count), Is.SameAs(source), context);
                Assert.That(source.Take(int.MaxValue), Is.SameAs(source), context);
                Assert.That(source.Distances, Is.SameAs(distances), context);
            }
        });
    }
}
