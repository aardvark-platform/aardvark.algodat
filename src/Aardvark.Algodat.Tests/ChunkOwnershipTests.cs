/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    /// <summary>
    /// Chunks handed out by queries must own their arrays:
    /// writing into a chunk must never change data cached in the node/store.
    /// </summary>
    [TestFixture]
    public class ChunkOwnershipTests
    {
        private static PointSet CreatePointSet()
        {
            var r = new Random(7);
            const int n = 5000;
            var ps = new V3d[n];
            var cs = new C4b[n];
            var ns = new V3f[n];
            var js = new int[n];
            var ks = new byte[n];
            for (var i = 0; i < n; i++)
            {
                ps[i] = new V3d(r.NextDouble(), r.NextDouble(), r.NextDouble());
                cs[i] = new C4b(r.Next(256), r.Next(256), r.Next(256));
                ns[i] = V3f.OOI;
                js[i] = r.Next(1000);
                ks[i] = (byte)r.Next(10);
            }
            var chunk = new Chunk(ps, cs, ns, js, ks, null, null, null);
            if (ImportConfig.Default.ParseConfig.EnabledProperties.PartIndices) chunk = chunk.WithPartIndices(42u, null);

            var config = ImportConfig.Default
                .WithStorage(PointCloud.CreateInMemoryStore(cache: new LruDictionary<string, object>(1L << 24)))
                .WithKey("test")
                .WithOctreeSplitLimit(512)
                ;
            return PointCloud.Chunks(chunk, config);
        }

        private static IPointCloudNode FirstLeaf(IPointCloudNode n)
        {
            while (!n.IsLeaf) n = n.Subnodes!.First(x => x != null)!.Value;
            return n;
        }

        private static void AssertChunkIsOwned(IPointCloudNode node, Func<IPointCloudNode, Chunk> query)
        {
            var chunk = query(node);
            ClassicAssert.IsTrue(chunk.Count > 0);
            ClassicAssert.IsTrue(chunk.HasColors && chunk.HasNormals && chunk.HasIntensities && chunk.HasClassifications);

            var p0 = node.PositionsAbsolute[0];
            var c0 = node.Colors!.Value[0];
            var n0 = node.Normals!.Value[0];
            var j0 = node.Intensities!.Value[0];
            var k0 = node.Classifications!.Value[0];

            // scribble over the chunk ...
            chunk.Positions[0] = new V3d(-1, -2, -3);
            chunk.Colors![0] = new C4b(c0.R ^ 0xFF, c0.G, c0.B);
            chunk.Normals![0] = -n0;
            chunk.Intensities![0] = j0 + 1;
            chunk.Classifications![0] = (byte)(k0 + 1);

            // ... node data must be unchanged
            ClassicAssert.AreEqual(p0, node.PositionsAbsolute[0]);
            ClassicAssert.AreEqual(c0, node.Colors.Value[0]);
            ClassicAssert.AreEqual(n0, node.Normals.Value[0]);
            ClassicAssert.AreEqual(j0, node.Intensities.Value[0]);
            ClassicAssert.AreEqual(k0, node.Classifications.Value[0]);

            // ... and a second query must not see the scribbles
            var again = query(node);
            ClassicAssert.AreEqual(p0, again.Positions[0]);
            ClassicAssert.AreEqual(c0, again.Colors![0]);
            ClassicAssert.AreEqual(n0, again.Normals![0]);
            ClassicAssert.AreEqual(j0, again.Intensities![0]);
            ClassicAssert.AreEqual(k0, again.Classifications![0]);
        }

        private static IEnumerable<(string name, Func<IPointCloudNode, Chunk> query)> LeafQueries()
        {
            yield return ("ToChunk", n => n.ToChunk());
            yield return ("ToChunk(0)", n => n.ToChunk(0).Single());
            yield return ("QueryPointsInOctreeLevel", n => n.QueryPointsInOctreeLevel(0).Single());
            yield return ("QueryPointsInOctreeLevel(bounds)", n => n.QueryPointsInOctreeLevel(0, Box3d.Infinite).Single());
            yield return ("QueryPointsInsideBox", n => Chunk.ImmutableMerge(n.QueryPointsInsideBox(Box3d.Infinite)));
            yield return ("Collect(0)", n => n.Collect(0).Single());
        }

        [Test]
        public void LeafChunks_DoNotAliasNodeArrays()
        {
            var leaf = FirstLeaf(CreatePointSet().Root.Value);
            foreach (var (name, query) in LeafQueries())
            {
                TestContext.Out.WriteLine(name);
                AssertChunkIsOwned(leaf, query);
            }
        }

        [Test]
        public void FilteredLeafChunks_DoNotAliasNodeArrays()
        {
            var root = CreatePointSet().Root.Value;
            var leaf = FirstLeaf(root);

            // partial filter: some points of the leaf are excluded, so the filtered node has its own subset arrays
            var bb = leaf.BoundingBoxExactGlobal;
            var half = new Box3d(bb.Min, new V3d(bb.Max.X, bb.Max.Y, bb.Center.Z));
            var filtered = FilteredNode.CreateTransient(leaf, new FilterInsideBox3d(half));
            ClassicAssert.IsTrue(filtered.PointCountCell > 0 && filtered.PointCountCell < leaf.PointCountCell);

            foreach (var (name, query) in LeafQueries())
            {
                TestContext.Out.WriteLine(name);
                AssertChunkIsOwned(filtered, query);
            }
        }

        [Test]
        public void CellQueryChunks_DoNotAliasNodeArrays()
        {
            var root = CreatePointSet().Root.Value;
            var cell = Queries.EnumerateCells(root, -1, V3i.III).First();
            var chunk = cell.GetPoints(0).First();
            var c0 = chunk.Colors![0];
            chunk.Colors[0] = new C4b(c0.R ^ 0xFF, c0.G, c0.B);

            var again = Queries.EnumerateCells(root, -1, V3i.III).First().GetPoints(0).First();
            ClassicAssert.AreEqual(c0, again.Colors![0]);
        }
    }
}
