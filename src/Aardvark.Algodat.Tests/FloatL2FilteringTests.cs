/*
   Aardvark Platform
   Copyright (C) 2006-2025 Aardvark Platform Team
   https://aardvark.graphics

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using PointUtils = Aardvark.Data.Points.Utils;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class FloatL2FilteringTests
{
    [Test]
    public void FloatPredicatesKeepExtremeTinyAndExactThresholdComparisonsStrict()
    {
        Assert.Multiple(() =>
        {
            foreach (var dimension in new[] { 2, 3 })
            foreach (var axis in Enumerable.Range(0, dimension))
            {
                var context = $"dimension={dimension}, axis={axis}";
                Predicate(dimension, V3f.Zero, Axis(1e20f, axis), 4e40, true, context + ", square overflow");
                Predicate(dimension, V3f.Zero, Axis(1e-30f, axis), 1e-62, false, context + ", square underflow");
                Predicate(dimension, Axis(-float.MaxValue, axis), Axis(float.MaxValue, axis), 1e80, true, context + ", subtraction overflow");
                var boundary = (double)0.3f * 0.3f;
                Predicate(dimension, V3f.Zero, Axis(0.3f, axis), boundary, false, context + ", equality");
                Predicate(dimension, V3f.Zero, Axis(0.3f, axis), Math.BitIncrement(boundary), true, context + ", immediately above equality");
                Predicate(dimension, V3f.Zero, Axis(0.3f, axis), Math.BitDecrement(boundary), false, context + ", immediately below equality");
            }
            foreach (var dimension in new[] { 2, 3 })
                Predicate(dimension, V3f.Zero, new V3f(1.5e19f), 1e40, true, $"dimension={dimension}, sum overflow");
        });
    }

    [Test]
    public void FloatFiltersKeepCorrectIdentitiesIncludingFirstExtremaAndHugeThresholds()
    {
        var cases = new[]
        {
            new FilterCase("overflow", new[] { V3f.Zero, Axis(1e20f, 0) }, 2e20, new[] { 0 }),
            new FilterCase("underflow", new[] { V3f.Zero, Axis(1e-30f, 0) }, 1e-31, new[] { 0, 1 }),
            new FilterCase("strict 0.3f", new[] { V3f.Zero, Axis(0.3f, 0), Axis(0.6f, 0) }, 0.3f, new[] { 0, 1, 2 }),
            new FilterCase("opposite extrema", new[] { Axis(-float.MaxValue, 0), Axis(float.MaxValue, 0) }, 1e39, new[] { 0 }),
            new FilterCase("minimum first", new[] { V3f.MinValue, V3f.Zero, V3f.IOO }, 1, new[] { 0, 1, 2 }),
            new FilterCase("huge ordinary", new[] { V3f.Zero, V3f.IOO, new V3f(2, 0, 0) }, 1e100, new[] { 0 }),
            new FilterCase("huge extrema", new[] { V3f.MinValue, V3f.Zero, V3f.MaxValue }, 1e100, new[] { 0 }),
            new FilterCase("infinite square", new[] { V3f.MinValue, V3f.Zero, V3f.MaxValue }, double.MaxValue, new[] { 0 })
        };
        Assert.Multiple(() =>
        {
            foreach (var dimension in new[] { 2, 3 })
            foreach (var attributes in new[] { false, true })
            foreach (var c in cases)
            {
                var source = Make(dimension, false, c.Points.Select(p => (V3d)p).ToArray(), attributes);
                var snapshot = Snapshot(source);
                var context = $"dimension={dimension}, attributes={attributes}, {c.Name}";
                CheckResult(source, source.ImmutableFilterSequentialMinDistL2(c.Distance), c.Indices, context);
                Unchanged(source, snapshot, context);
            }
        });
    }

    [Test]
    public void OrdinaryDuplicateAndDisabledFiltersPreserveTypesOrderingAndOwnership()
    {
        foreach (var dimension in new[] { 2, 3 })
        foreach (var doubles in new[] { false, true })
        foreach (var attributes in new[] { false, true })
        {
            var context = $"dimension={dimension}, doubles={doubles}, attributes={attributes}";
            foreach (var count in new[] { 0, 1, 6 })
            {
                var source = Make(dimension, doubles, Enumerable.Range(0, count).Select(i => new V3d(i, 0, 0)).ToArray(), attributes);
                foreach (var disabled in new[] { 0.0, -1.0, double.NegativeInfinity })
                    Assert.That(source.ImmutableFilterSequentialMinDistL2(disabled), Is.SameAs(source), context + $", disabled={disabled}");
                if (count <= 1)
                    foreach (var distance in new[] { 1.0, double.MaxValue, double.NaN })
                        Assert.That(source.ImmutableFilterSequentialMinDistL2(distance), Is.SameAs(source), context + $", count={count}");
            }
            foreach (var c in new[]
            {
                (Xs: new[] { 0.0, 0.25, 0.5, 1, 1.75, 2 }, Indices: new[] { 0, 3, 5 }),
                (Xs: new[] { 0.0, 0, 1, 1, 2 }, Indices: new[] { 0, 2, 4 })
            })
            {
                var source = Make(dimension, doubles, c.Xs.Select(x => new V3d(x, 0, 0)).ToArray(), attributes);
                var snapshot = Snapshot(source);
                var result = source.ImmutableFilterSequentialMinDistL2(1);
                CheckResult(source, result, c.Indices, context);
                foreach (var value in result.Data.Values) Array.Clear((Array)value, 0, ((Array)value).Length);
                Unchanged(source, snapshot, context + ", mutating result arrays");
                CheckResult(source, source.ImmutableFilterSequentialMinDistL2(1), c.Indices, context + ", repeated");
                CheckResult(source, source.ImmutableFilterSequentialMinDistL2(double.NaN), Enumerable.Range(0, source.Count).ToArray(), context + ", NaN threshold");
            }
        }
    }

    [Test]
    public void DoublePredicatesAndL1ComparisonsKeepOrdinaryAndIeeeBehavior()
    {
        foreach (var dimension in new[] { 2, 3 })
        foreach (var threshold in new[] { -1.0, 0, 0.5, 1, 2, double.PositiveInfinity, double.NaN })
        {
            var a2 = new V2d(0, 0); var b2 = new V2d(1, 0);
            var a3 = new V3d(0, 0, 0); var b3 = new V3d(1, 0, 0);
            var expected = 1.0 < threshold;
            Assert.That(dimension == 2 ? PointUtils.DistLessThanL2(ref a2, ref b2, threshold) : PointUtils.DistLessThanL2(ref a3, ref b3, threshold),
                Is.EqualTo(expected), $"double dimension={dimension}, squared threshold={threshold}");
            var fa2 = new V2f(0, 0); var fb2 = new V2f(1, 0);
            var fa3 = new V3f(0, 0, 0); var fb3 = new V3f(1, 0, 0);
            var l1Expected = !(1.0 >= threshold || 0.0 >= threshold);
            Assert.That(dimension == 2 ? PointUtils.DistLessThanL1(ref fa2, ref fb2, threshold) : PointUtils.DistLessThanL1(ref fa3, ref fb3, threshold),
                Is.EqualTo(l1Expected), $"float L1 dimension={dimension}, threshold={threshold}");
            Assert.That(dimension == 2 ? PointUtils.DistLessThanL1(ref a2, ref b2, threshold) : PointUtils.DistLessThanL1(ref a3, ref b3, threshold),
                Is.EqualTo(l1Expected), $"double L1 dimension={dimension}, threshold={threshold}");
        }
        foreach (var dimension in new[] { 2, 3 })
        {
            Predicate(dimension, V3f.Zero, V3f.Zero, 0, false, "duplicate at zero threshold");
            Predicate(dimension, V3f.Zero, V3f.Zero, 1, true, "duplicate at positive threshold");
            Predicate(dimension, V3f.Zero, V3f.IOO, double.NaN, false, "NaN threshold");
            Predicate(dimension, V3f.Zero, Axis(float.NaN, 0), 1, false, "NaN coordinate");
            Predicate(dimension, V3f.Zero, Axis(float.PositiveInfinity, 0), double.PositiveInfinity, false, "infinite distance and threshold");
        }
    }

    private static void Predicate(int dimension, V3f a, V3f b, double squared, bool expected, string context)
    {
        var beforeA = a; var beforeB = b;
        bool actual;
        if (dimension == 2)
        {
            var aa = a.XY; var bb = b.XY;
            actual = PointUtils.DistLessThanL2(ref aa, ref bb, squared);
            Assert.That(aa, Is.EqualTo(beforeA.XY), context + ", first ref unchanged");
            Assert.That(bb, Is.EqualTo(beforeB.XY), context + ", second ref unchanged");
        }
        else actual = PointUtils.DistLessThanL2(ref a, ref b, squared);
        Assert.That(actual, Is.EqualTo(expected), context);
        Assert.That(a, Is.EqualTo(beforeA), context + ", first unchanged");
        Assert.That(b, Is.EqualTo(beforeB), context + ", second unchanged");
    }

    private static V3f Axis(float value, int axis) => new(axis == 0 ? value : 0, axis == 1 ? value : 0, axis == 2 ? value : 0);
    private sealed record FilterCase(string Name, V3f[] Points, double Distance, int[] Indices);

    private static GenericChunk Make(int dimension, bool doubles, V3d[] positions, bool attributes)
    {
        var data = new Dictionary<Durable.Def, object>();
        if (doubles)
            data.Add(dimension == 2 ? GenericChunk.Defs.Positions2d : GenericChunk.Defs.Positions3d,
                dimension == 2 ? positions.Select(p => p.XY).ToArray() : (object)positions);
        else
            data.Add(dimension == 2 ? GenericChunk.Defs.Positions2f : GenericChunk.Defs.Positions3f,
                dimension == 2 ? positions.Select(p => (V2f)p.XY).ToArray() : (object)positions.Select(p => (V3f)p).ToArray());
        if (attributes)
        {
            var ids = Enumerable.Range(0, positions.Length).ToArray();
            data.Add(GenericChunk.Defs.Intensities1i, ids);
            data.Add(GenericChunk.Defs.Colors4b, ids.Select(i => new C4b((byte)(i + 11), (byte)(i + 31), (byte)(i + 51), (byte)201)).ToArray());
            data.Add(GenericChunk.Defs.Normals3f, ids.Select(i => new V3f(i + 1, 2, 3)).ToArray());
            data.Add(GenericChunk.Defs.Classifications1b, ids.Select(i => (byte)(i + 71)).ToArray());
            data.Add(Durable.Octree.PerPointPartIndex1i, ids.Select(i => i + 91).ToArray());
        }
        return new GenericChunk(data);
    }

    private static Dictionary<Durable.Def, Array> Snapshot(GenericChunk source)
        => source.Data.ToDictionary(kv => kv.Key, kv => (Array)((Array)kv.Value).Clone());

    private static void Unchanged(GenericChunk source, Dictionary<Durable.Def, Array> snapshot, string context)
    {
        foreach (var kv in snapshot) Assert.That(source.Data[kv.Key], Is.EqualTo(kv.Value), context + ", source " + kv.Key.Name);
    }

    private static void CheckResult(GenericChunk source, GenericChunk result, int[] indices, string context)
    {
        Assert.That(result, Is.Not.SameAs(source), context + ", result ownership");
        Assert.That(result.Count, Is.EqualTo(indices.Length), context + ", count");
        Assert.That(result.PositionsDef, Is.EqualTo(source.PositionsDef), context + ", position definition");
        Assert.That(result.Data.Keys, Is.EquivalentTo(source.Data.Keys), context + ", attribute presence");
        foreach (var kv in source.Data)
        {
            var input = (Array)kv.Value; var output = (Array)result.Data[kv.Key];
            Assert.That(output, Is.Not.SameAs(input), context + ", owned " + kv.Key.Name);
            Assert.That(output.GetType(), Is.EqualTo(input.GetType()), context + ", type " + kv.Key.Name);
            for (var i = 0; i < Math.Min(output.Length, indices.Length); i++)
                Assert.That(output.GetValue(i), Is.EqualTo(input.GetValue(indices[i])), context + $", {kv.Key.Name} row={i}");
        }
    }
}
