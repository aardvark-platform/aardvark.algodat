/*
    Copyright (C) 2006-2026. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program. If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class HullExtrusionTests
{
    private static readonly Range1d[] Ranges =
    {
        new(2, 4), new(-4, -2), new(-2, 3), new(0, 3), new(2, 2), new(-2, -2), new(0, 0)
    };

    private static Polygon2d Footprint(bool concave, bool clockwise = false)
    {
        var points = concave
            ? new[] { new V2d(0, 0), new V2d(3, 0), new V2d(3, 1), new V2d(1, 1), new V2d(1, 3), new V2d(0, 3) }
            : new[] { new V2d(0, 0), new V2d(2, 0), new V2d(2, 1), new V2d(0, 1) };
        if (clockwise) Array.Reverse(points);
        return new Polygon2d(points);
    }

    // An independent rectangle/union-of-rectangles oracle: no hull, polygon
    // containment, decomposition, or production plane construction is used.
    private static bool Expected(bool concave, Range1d z, V3d p)
        => p.Z >= z.Min && p.Z <= z.Max && p.X >= 0 && p.Y >= 0 &&
           (concave ? (p.X <= 3 && p.Y <= 1) || (p.X <= 1 && p.Y <= 3) : p.X <= 2 && p.Y <= 1);

    private static V2d[] Samples(bool concave) => concave
        ? new[] { new V2d(0.25, 0.5), new V2d(2.25, 0.5), new V2d(0.5, 2.25), new V2d(2, 2), new V2d(-0.25, 0.5), new V2d(0.5, 3.25) }
        : new[] { new V2d(0.5, 0.5), new V2d(1.5, 0.5), new V2d(-0.25, 0.5), new V2d(2.25, 0.5), new V2d(0.5, -0.25), new V2d(0.5, 1.25) };

    private static (string Name, Trafo3d Trafo)[] ExactTransforms()
    {
        // An exact quarter-turn keeps cap-boundary assertions independent of
        // trigonometric roundoff. Concave XY samples avoid decomposition seams.
        var rotation = new M44d(0, 0, 1, 0, 0, 1, 0, 0, -1, 0, 0, 0, 0, 0, 0, 1);
        var turn = new Trafo3d(rotation, rotation.Transposed);
        var translation = Trafo3d.Translation(8, -4, 16);
        var scale = Trafo3d.Scale(2, 0.5, 4);
        return new[] { ("identity", Trafo3d.Identity), ("translation", translation), ("rotation", turn),
            ("nonuniform-scale", scale), ("combined", translation * turn * scale) };
    }

    private static void CheckMembership(Range1d z)
    {
        foreach (var concave in new[] { false, true })
        foreach (var clockwise in new[] { false, true })
        foreach (var (name, trafo) in ExactTransforms())
        {
            var filter = new FilterInsideConvexHulls3d(Footprint(concave, clockwise), z, trafo);
            var decoded = Filter.Deserialize(filter.Serialize().ToString());
            var context = $"concave={concave}, clockwise={clockwise}, z={z}, transform={name}";
            Assert.That(filter.Equals(decoded), Is.True, context);
            foreach (var xy in Samples(concave))
            foreach (var height in new[] { z.Min - 0.125, z.Min, z.Min + 0.125, (z.Min + z.Max) / 2, z.Max - 0.125, z.Max, z.Max + 0.125 })
            {
                var local = new V3d(xy, height);
                var world = trafo.Forward.TransformPos(local);
                var expected = Expected(concave, z, local);
                Assert.That(filter.Contains(world), Is.EqualTo(expected), $"{context}, local={local}");
                Assert.That(((ISpatialFilter)decoded).Contains(world), Is.EqualTo(expected), $"decoded: {context}, local={local}");
            }
        }
    }

    [Test]
    public void SignedExtrusionCapsAreInclusiveBeforeAndAfterSerialization()
    {
        foreach (var z in Ranges.Where(z => z.Min != 0)) CheckMembership(z);
    }

    [Test]
    public void ZeroBasedExtrusionRetainsItsMembership()
    {
        CheckMembership(new Range1d(0, 3));
        CheckMembership(new Range1d(0, 0));
    }

    [Test]
    public void ObliqueTransformsApplyAfterLocalExtrusion()
    {
        var rotation = Trafo3d.RotationEuler(0.23, -0.41, 0.67);
        foreach (var trafo in new[] { rotation, Trafo3d.Translation(7, -3, 11) * rotation * Trafo3d.Scale(2, 0.5, 4) })
        foreach (var concave in new[] { false, true })
        foreach (var z in Ranges.Where(z => z.Min < z.Max))
        {
            var filter = new FilterInsideConvexHulls3d(Footprint(concave), z, trafo);
            foreach (var xy in Samples(concave))
            foreach (var height in new[] { z.Min - 0.125, z.Min + 0.125, z.Max - 0.125, z.Max + 0.125 })
            {
                var local = new V3d(xy, height);
                Assert.That(filter.Contains(trafo.Forward.TransformPos(local)), Is.EqualTo(Expected(concave, z, local)),
                    $"concave={concave}, z={z}, local={local}, transform={trafo}");
            }
        }
    }

    private static C4b Color(int i) => new((byte)i, (byte)(i + 64), (byte)(255 - i), (byte)255);
    private static V3f Normal(int i) => i % 2 == 0 ? V3f.IOO : V3f.OIO;

    private static PointSet Cloud(Storage storage, V3d[] positions, int split)
    {
        var ids = Enumerable.Range(0, positions.Length).ToArray();
        var chunk = new Chunk(positions, ids.Select(Color).ToArray(), ids.Select(Normal).ToArray(),
            ids.Select(i => 1000 + i).ToArray(), ids.Select(i => (byte)i).ToArray(),
            ids.Select(i => 10000 + i).ToArray(), new Range1i(10000, 10000 + ids.Length - 1), null);
        return PointCloud.Chunks(chunk, ImportConfig.Default.WithStorage(storage).WithRandomKey()
            .WithOctreeSplitLimit(split).WithEnabledPartIndices(true));
    }

    private static void CheckView(IPointCloudNode view, V3d[] positions, int[] expected, string context)
    {
        var seen = new List<int>();
        foreach (var chunk in view.QueryAllPoints())
        {
            Assert.That((chunk.HasColors, chunk.HasNormals, chunk.HasIntensities, chunk.HasClassifications, chunk.HasPartIndices),
                Is.EqualTo((true, true, true, true, true)), context);
            Assert.That((chunk.Colors.Count, chunk.Normals.Count, chunk.Intensities.Count, chunk.Classifications.Count),
                Is.EqualTo((chunk.Count, chunk.Count, chunk.Count, chunk.Count)), context);
            for (var i = 0; i < chunk.Count; i++)
            {
                var id = chunk.Intensities[i] - 1000;
                seen.Add(id);
                Assert.That(chunk.Positions[i], Is.EqualTo(positions[id]), $"{context}, position {id}");
                Assert.That(chunk.Colors[i], Is.EqualTo(Color(id)), $"{context}, color {id}");
                Assert.That(chunk.Normals[i], Is.EqualTo(Normal(id)), $"{context}, normal {id}");
                Assert.That(chunk.Classifications[i], Is.EqualTo((byte)id), $"{context}, classification {id}");
                Assert.That(PartIndexUtils.Get(chunk.PartIndices, i), Is.EqualTo(10000 + id), $"{context}, part {id}");
            }
        }
        Assert.That(seen.Count, Is.EqualTo(expected.Length), $"{context}, selected count");
        Assert.That(seen.OrderBy(i => i), Is.EqualTo(expected), $"{context}, selected point ids");
    }

    private static void CheckReloads(PointSet cloud, V3d[] positions, FilterInsideConvexHulls3d filter, int[] expected, string context)
    {
        var view = (FilteredNode)FilteredNode.Create(cloud.Root.Value, filter);
        for (var cycle = 0; cycle < 3; cycle++)
        {
            CheckView(view, positions, expected, $"{context}, cycle={cycle}");
            var decoded = FilteredNode.Decode(cloud.Storage, view.Encode());
            Assert.That(decoded.Id, Is.EqualTo(view.Id), context);
            Assert.That(decoded.Node.Id, Is.EqualTo(view.Node.Id), context);
            Assert.That(decoded.Filter.Equals(view.Filter), Is.True, context);
            view = decoded;
        }
        CheckView(cloud.Storage.GetPointCloudNode(view.Id), positions, expected, $"{context}, store reload");
        CheckView(cloud.Root.Value, positions, Enumerable.Range(0, positions.Length).ToArray(), $"{context}, unchanged source");
    }

    [Test]
    public void ShiftedRangesRetainSixPointsInLeafAndSubdividedViews()
    {
        var positions = (from x in new[] { 0.5, 1.5 } from z in Enumerable.Range(-5, 11) select new V3d(x, 0.5, z)).ToArray();
        Assert.Multiple(() =>
        {
            foreach (var split in new[] { 64, 1 })
            using (var storage = PointCloud.CreateInMemoryStore(cache: null))
            {
                var cloud = Cloud(storage, positions, split);
                Assert.That(cloud.Root.Value.IsLeaf, Is.EqualTo(split == 64));
                foreach (var z in new[] { new Range1d(2, 4), new Range1d(-4, -2) })
                {
                    var expected = Enumerable.Range(0, positions.Length).Where(i => Expected(false, z, positions[i])).ToArray();
                    Assert.That(expected.Length, Is.EqualTo(6), z.ToString());
                    CheckReloads(cloud, positions, new FilterInsideConvexHulls3d(Footprint(false), z, Trafo3d.Identity), expected,
                        $"split={split}, z={z}");
                }
            }
        });
    }

    [Test]
    public void TransformedViewsPreserveSelectedPositionsAndAlignedAttributes()
    {
        foreach (var concave in new[] { false, true })
        foreach (var (name, trafo) in ExactTransforms())
        foreach (var split in new[] { 128, 2 })
        using (var storage = PointCloud.CreateInMemoryStore(cache: null))
        {
            var local = (from xy in Samples(concave) from z in Enumerable.Range(-5, 11) select new V3d(xy, z)).ToArray();
            var positions = local.Select(p => trafo.Forward.TransformPos(p)).ToArray();
            var cloud = Cloud(storage, positions, split);
            Assert.That(cloud.Root.Value.IsLeaf, Is.EqualTo(split == 128));
            foreach (var z in Ranges)
            {
                var expected = Enumerable.Range(0, local.Length).Where(i => Expected(concave, z, local[i])).ToArray();
                CheckReloads(cloud, positions, new FilterInsideConvexHulls3d(Footprint(concave), z, trafo), expected,
                    $"concave={concave}, transform={name}, split={split}, z={z}");
            }
        }
    }

    [Test]
    public void PolygonValidationAndOtherConstructorsRemainCompatible()
    {
        foreach (var invalid in new[]
        {
            new[] { V2d.Zero, V2d.IO },
            new[] { V2d.Zero, V2d.IO, V2d.IO, V2d.OI },
            new[] { V2d.Zero, new V2d(2, 2), new V2d(0, 2), new V2d(2, 0) }
        })
            Assert.Throws<ArgumentException>(() => new FilterInsideConvexHulls3d(new Polygon2d(invalid), new Range1d(2, 4), Trafo3d.Identity),
                string.Join("; ", invalid));

        var hulls = new[] { new Hull3d(new Box3d(new V3d(0, 0, 2), new V3d(2, 1, 4))) };
        var array = new FilterInsideConvexHulls3d(hulls);
        var enumerable = new FilterInsideConvexHulls3d((IEnumerable<Hull3d>)hulls);
        Assert.That(array.Hulls, Is.SameAs(hulls));
        Assert.That(enumerable.Hulls, Is.Not.SameAs(hulls));
        Assert.That(array.Equals(enumerable), Is.True);
        Assert.That(array.Equals(Filter.Deserialize(array.Serialize().ToString())), Is.True);
        foreach (var xy in Samples(false))
        foreach (var z in Enumerable.Range(-5, 11))
        {
            var point = new V3d(xy, z);
            Assert.That(array.Contains(point), Is.EqualTo(Expected(false, new Range1d(2, 4), point)), point.ToString());
            Assert.That(enumerable.Contains(point), Is.EqualTo(array.Contains(point)), point.ToString());
        }
    }
}
