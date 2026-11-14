/*
    Copyright (C) 2006-2024. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class ImportReprojectionTests
{
    [Test]
    public void DensityUsesProjectedDistancesForExpansionAndCompression()
    {
        Assert.Multiple(() =>
        {
            foreach (var single in new[] { false, true })
            foreach (var global in new[] { false, true })
            foreach (var expand in new[] { false, true })
            {
                var scale = expand ? 100.0 : 0.1;
                var source = Make(single, new[] { 0.1, expand ? 0.2 : 2.1 }, 0, true);
                var context = $"V3{(single ? 'f' : 'd')}, global={global}, scale={scale}";
                var actual = Import(new[] { source }, global, 1, Scale(scale));
                var expected = Import(new[] { Project(source, Scale(scale)) }, global, 1, null);
                Compare(actual, expected, context);
                Assert.That(actual.Select(r => r.Intensity),
                    Is.EqualTo(expand ? new int?[] { 1000, 1001 } : new int?[] { 1000 }), context + ", retained identities");
                CheckAttributes(actual, context);
                Assert.That(source.PositionsAsV3d.Select(p => p.X),
                    Is.EqualTo(single ? new[] { (double)(float)0.1, (double)(float)(expand ? 0.2 : 2.1) } : new[] { 0.1, expand ? 0.2 : 2.1 }),
                    context + ", original positions unchanged");
            }
        });
    }

    [Test]
    public void DeduplicationUsesProjectedPositionsAndKeepsAlignedAttributes()
    {
        Assert.Multiple(() =>
        {
            foreach (var single in new[] { false, true })
            foreach (var global in new[] { false, true })
            {
                var input = Make(single, new[] { 0.1, 0.2, 2.1 }, 0, true);
                Func<IList<V3d>, IList<V3d>> collapse = ps => ps.Select(_ => new V3d(0.125, 0, 0)).ToArray();
                var actual = Import(new[] { input }, global, 0, collapse);
                var expected = Import(new[] { Project(input, collapse) }, global, 0, null);
                var context = $"V3{(single ? 'f' : 'd')}, global={global}, many-to-one, MinDist=0";
                Compare(actual, expected, context);
                Assert.That(actual.Select(r => r.Intensity), Is.EqualTo(new int?[] { 1000 }), context);
                CheckAttributes(actual, context);

                // A bulk callback can separate initially coincident samples by their input index.
                var duplicateInput = Make(single, new[] { 0.1, 0.1, 2.1 }, 0, true);
                Func<IList<V3d>, IList<V3d>> separate = ps => ps.Select((_, i) => new V3d(0.125 + i * 2, 0, 0)).ToArray();
                var separated = Import(new[] { duplicateInput }, global, 0, separate);
                var separatedReference = Import(new[] { Project(duplicateInput, separate) }, global, 0, null);
                Compare(separated, separatedReference, context + ", separated input duplicates");
                Assert.That(separated.Select(r => r.Intensity), Is.EqualTo(new int?[] { 1000, 1001, 1002 }), context + ", all input samples projected");
                CheckAttributes(separated, context);
            }
        });
    }

    [Test]
    public void IdentityDisabledAndAbsentProjectionKeepImportResults()
    {
        foreach (var single in new[] { false, true })
        foreach (var global in new[] { false, true })
        foreach (var attributes in new[] { false, true })
        foreach (var scale in new[] { 1.0, 0.1, 100.0 })
        {
            var source = Make(single, new[] { 0.1, 0.2, 2.1, 4.1 }, 0, attributes);
            var minDist = scale == 1.0 ? 1.0 : 0.0;
            var context = $"V3{(single ? 'f' : 'd')}, global={global}, attributes={attributes}, scale={scale}, MinDist={minDist}";
            var actual = Import(new[] { source }, global, minDist, Scale(scale));
            var expected = Import(new[] { Project(source, Scale(scale)) }, global, minDist, null);
            Compare(actual, expected, context);
            if (attributes) CheckAttributes(actual, context);
            else foreach (var row in actual)
            {
                Assert.That(row.Color, Is.Null, context);
                Assert.That(row.Intensity, Is.Null, context);
                Assert.That(row.Classification, Is.Null, context);
            }
            if (scale == 1.0)
                Compare(actual, Import(new[] { source }, global, minDist, null), context + ", no callback");
        }
    }

    [Test]
    public void ParallelChunkProjectionPreservesAttributesCancellationAndCallbackErrors()
    {
        foreach (var single in new[] { false, true })
        foreach (var parallelism in new[] { 1, 2 })
        {
            var source = Enumerable.Range(0, 4).Select(i => Make(single, new[] { i * 10.0 + 0.1, i * 10.0 + 0.2 }, 2 * i, true)).ToArray();
            var calls = 0;
            var points = 0;
            Func<IList<V3d>, IList<V3d>> reproject = ps =>
            {
                Interlocked.Increment(ref calls);
                Interlocked.Add(ref points, ps.Count);
                return Scale(100)(ps);
            };
            var context = $"V3{(single ? 'f' : 'd')}, parallelism={parallelism}";
            var actual = Import(source, true, 1, reproject, parallelism, 4);
            var expected = Import(source.Select(x => Project(x, Scale(100))).ToArray(), true, 1, null, parallelism, 4);
            Compare(actual, expected, context);
            Assert.That(calls, Is.EqualTo(4), context + ", original chunk batches");
            Assert.That(points, Is.EqualTo(8), context + ", all positions reach projection");
            Assert.That(actual.Length, Is.EqualTo(8), context);
            CheckAttributes(actual, context);

            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            using var store = PointCloud.CreateInMemoryStore();
            var config = Config(store, true, 1, parallelism, 4).WithCancellationToken(canceled.Token)
                .WithReproject(ps => { Assert.Fail("pre-canceled import invoked callback"); return ps; });
            Assert.Throws<OperationCanceledException>(() => PointCloud.Chunks(source, config), context);

            using var failedStore = PointCloud.CreateInMemoryStore();
            var failure = Assert.Throws<AggregateException>(() => PointCloud.Chunks(new[] { source[0] },
                Config(failedStore, true, 1, parallelism, 4).WithReproject(_ => throw new InvalidOperationException("projection failure"))), context);
            Assert.That(failure.Flatten().InnerExceptions.Any(e => e is InvalidOperationException && e.Message == "projection failure"),
                Is.True, context + ", callback error propagated");
        }
    }

    private static Func<IList<V3d>, IList<V3d>> Scale(double scale) => ps => ps.Select(p => p * scale).ToArray();

    private static GenericChunk Make(bool single, double[] xs, int offset, bool attributes)
    {
        var data = new Dictionary<Durable.Def, object>();
        var positions = xs.Select(x => new V3d(x, 0, 0)).ToArray();
        data.Add(single ? GenericChunk.Defs.Positions3f : GenericChunk.Defs.Positions3d,
            single ? positions.Select(p => (V3f)p).ToArray() : (object)positions);
        if (attributes)
        {
            var ids = Enumerable.Range(offset, xs.Length).ToArray();
            data.Add(GenericChunk.Defs.Colors4b, ids.Select(i => new C4b((byte)(11 + i), (byte)(31 + i), (byte)(51 + i), (byte)201)).ToArray());
            data.Add(GenericChunk.Defs.Normals3f, ids.Select(i => new V3f(i + 1, 2, 3)).ToArray());
            data.Add(GenericChunk.Defs.Intensities1i, ids.Select(i => 1000 + i).ToArray());
            data.Add(GenericChunk.Defs.Classifications1b, ids.Select(i => (byte)(71 + i)).ToArray());
        }
        return new GenericChunk(data);
    }

    private static GenericChunk Project(GenericChunk input, Func<IList<V3d>, IList<V3d>> projection)
    {
        var mapped = projection(input.PositionsAsV3d);
        return input.WithPositions(input.Positions is V3f[] ? mapped.Select(p => (V3f)p).ToArray() : (object)mapped.ToArray());
    }

    private static ImportConfig Config(Storage store, bool global, double minDist, int parallelism, int chunkLimit)
        => ImportConfig.Default.WithStorage(store).WithKey("reprojection-test").WithVerbose(false)
            .WithMinDist(minDist).WithNormalizePointDensityGlobal(global).WithMaxDegreeOfParallelism(parallelism)
            .WithMaxChunkPointCount(chunkLimit).WithOctreeSplitLimit(4);

    private static Row[] Import(GenericChunk[] source, bool global, double minDist,
        Func<IList<V3d>, IList<V3d>> projection, int parallelism = 1, int chunkLimit = 16)
    {
        using var store = PointCloud.CreateInMemoryStore();
        var config = Config(store, global, minDist, parallelism, chunkLimit).WithReproject(projection);
        var cloud = PointCloud.Chunks((IEnumerable<GenericChunk>)source, config);
        var rows = Read(cloud);
        Assert.That(cloud.PointCount, Is.EqualTo(rows.Length), "stored point count matches full-resolution enumeration");
        store.Cache.Clear();
        Compare(Read(store.GetPointSet("reprojection-test")), rows, "reloaded import");
        return rows;
    }

    private readonly record struct Row(V3d Position, C4b? Color, V3f? Normal, int? Intensity, byte? Classification);

    private static Row[] Read(PointSet cloud)
        => cloud.QueryAllPoints().SelectMany(c => Enumerable.Range(0, c.Count).Select(i =>
            new Row(c.Positions[i], c.Colors?[i], c.Normals?[i], c.Intensities?[i], c.Classifications?[i])))
            .OrderBy(r => r.Intensity).ThenBy(r => r.Position.X).ToArray();

    private static void Compare(Row[] actual, Row[] expected, string context)
    {
        Assert.That(actual.Length, Is.EqualTo(expected.Length), context + ", count");
        for (var i = 0; i < Math.Min(actual.Length, expected.Length); i++)
        {
            var a = actual[i]; var e = expected[i];
            Assert.That(double.IsFinite(e.Position.X) && double.IsFinite(e.Position.Y) && double.IsFinite(e.Position.Z), Is.True, context + ", finite reference");
            Assert.That(double.IsFinite(a.Position.X) && double.IsFinite(a.Position.Y) && double.IsFinite(a.Position.Z), Is.True, context + ", finite result");
            Assert.That((a.Position - e.Position).Length, Is.LessThanOrEqualTo(1e-5), context + $", position row={i}");
            Assert.That(a with { Position = e.Position }, Is.EqualTo(e), context + $", attributes row={i}");
        }
    }

    private static void CheckAttributes(Row[] rows, string context)
    {
        foreach (var row in rows)
        {
            var id = row.Intensity.Value - 1000;
            Assert.That(row.Color, Is.EqualTo(new C4b((byte)(11 + id), (byte)(31 + id), (byte)(51 + id), (byte)201)), context + $", color id={id}");
            Assert.That(row.Normal, Is.EqualTo(new V3f(id + 1, 2, 3)), context + $", unchanged normal id={id}");
            Assert.That(row.Classification, Is.EqualTo((byte)(71 + id)), context + $", classification id={id}");
        }
    }
}
