/*
    Copyright (C) 2006-2022. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
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
            var pcl = pointset.Root.Value;

            var inlineConfig = new InlineConfig(collapse: true, gzipped: true);
            var inlinedNodes = store.EnumerateOctreeInlined(key, inlineConfig);

            Assert.IsTrue(inlinedNodes.Nodes.Count() == 1);

            var n = inlinedNodes.Nodes.Single();

            Assert.IsTrue(n.PositionsLocal3f.Length == 8);
            Assert.IsTrue(n.Classifications1b.Length == 8);
            Assert.IsTrue(n.Intensities1b.Length == 8);

            var encoded = n.Encode(gzip: true);
            var m = new InlinedNode(encoded, gzipped: true);

            Assert.True(n.NodeId == m.NodeId);
            Assert.True(n.Cell == m.Cell);
            Assert.True(n.PointCountCell == m.PointCountCell);
            Assert.True(n.PointCountTreeLeafs == m.PointCountTreeLeafs);
            Assert.IsTrue(m.PositionsLocal3f.Length == 8);
            Assert.IsTrue(m.Classifications1b.Length == 8);
            Assert.IsTrue(m.Intensities1b.Length == 8);
        }

    }
}
