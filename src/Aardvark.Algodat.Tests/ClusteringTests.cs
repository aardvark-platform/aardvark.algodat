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
using Aardvark.Geometry.Clustering;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ClusteringTests
    {
        private sealed class FixedRandom : IRandomUniform
        {
            private readonly int m_value;

            public FixedRandom(int value)
            {
                m_value = value;
            }

            public int UniformIntCalls { get; private set; }
            public int RandomBits => 31;
            public bool GeneratesFullDoubles => false;
            public void ReSeed(int seed) { }
            public int UniformInt() { UniformIntCalls++; return m_value; }
            public uint UniformUInt() => (uint)m_value;
            public long UniformLong() => m_value;
            public ulong UniformULong() => (uint)m_value;
            public float UniformFloat() => 0.5f;
            public float UniformFloatClosed() => 0.5f;
            public float UniformFloatOpen() => 0.5f;
            public double UniformDouble() => 0.5;
            public double UniformDoubleClosed() => 0.5;
            public double UniformDoubleOpen() => 0.5;
        }

        private static readonly Plane3d[] s_interleavedPlaneGroups =
        {
            new(V3d.XAxis, 0.0),
            new(V3d.YAxis, 0.0),
            new(V3d.YAxis, 0.0),
            new(V3d.XAxis, 0.0)
        };

        [Test]
        public void GetClusterIndexResolvesRootAndFullyCompressesDeepPath()
        {
            var parents = new[] { 1, 2, 3, 4, 5, 5 };

            var root = parents.GetClusterIndex(0);

            ClassicAssert.AreEqual(5, root);
            CollectionAssert.AreEqual(new[] { 5, 5, 5, 5, 5, 5 }, parents);
        }

        [Test]
        public void ArrayAndListConsolidationFullyCompressEquivalentPaths()
        {
            var array = new[] { 1, 2, 2, 4, 5, 5 };
            var list = new List<int>(array);
            var expected = new[] { 2, 2, 2, 5, 5, 5 };

            array.ClusterConsolidate();
            list.ClusterConsolidate();

            CollectionAssert.AreEqual(expected, array);
            CollectionAssert.AreEqual(expected, list);
        }

        [Test]
        public void ArrayAndListCompactionProduceIdenticalDenseIndicesAndCounts()
        {
            var array = new[] { 2, 2, 2, 4, 4, 5 };
            var list = new List<int>(array);

            var arrayCounts = array.CompactAndComputeCountArray();
            var listCounts = list.CompactAndComputeCountArray();

            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 1, 2 }, array);
            CollectionAssert.AreEqual(array, list);
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, arrayCounts);
            CollectionAssert.AreEqual(arrayCounts, listCounts);
        }

        [Test]
        public void DynamicClusteringHandlesEmptyAndSingletonPartitions()
        {
            var empty = new DynamicClustering();
            empty.Init();
            ClassicAssert.AreEqual(0, empty.Count);
            CollectionAssert.IsEmpty(empty.IndexList);
            CollectionAssert.IsEmpty(empty.CountArray);

            var singleton = new DynamicClustering();
            singleton.AddItem();
            singleton.Init();
            ClassicAssert.AreEqual(1, singleton.Count);
            CollectionAssert.AreEqual(new[] { 0 }, singleton.IndexList);
            CollectionAssert.AreEqual(new[] { 1 }, singleton.CountArray);
        }

        [Test]
        public void DynamicClusteringMergeDirectionsPreserveMultiplePartitions()
        {
            var clustering = new DynamicClustering();
            for (var i = 0; i < 6; i++) clustering.AddItem();

            clustering.MergeLeft(0, 1);
            clustering.MergeRight(2, 3);
            clustering.MergeRight(0, 2);
            clustering.MergeLeft(4, 5);

            CollectionAssert.AreEqual(new[] { 3, 0, 3, 3, 4, 4 }, clustering.IndexList);

            clustering.Init();

            ClassicAssert.AreEqual(2, clustering.Count);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 1, 1 }, clustering.IndexList);
            CollectionAssert.AreEqual(new[] { 4, 2 }, clustering.CountArray);
        }

        [Test]
        public void NormalsClusteringPreservesInputAndAlignsSignedSumsWithDenseClusters()
        {
            var normals = new[] { V3d.XAxis, -V3d.XAxis, V3d.XAxis, V3d.YAxis, -V3d.YAxis };
            var original = (V3d[])normals.Clone();

            var clustering = new NormalsClustering(normals, delta: 1e-6);

            CollectionAssert.AreEqual(original, normals);
            ClassicAssert.AreEqual(2, clustering.Count);
            ClassicAssert.AreEqual(clustering.Count, clustering.SumArray.Length);

            var xCluster = clustering.IndexArray[0];
            var yCluster = clustering.IndexArray[3];
            ClassicAssert.AreNotEqual(xCluster, yCluster);
            ClassicAssert.AreEqual(xCluster, clustering.IndexArray[1]);
            ClassicAssert.AreEqual(xCluster, clustering.IndexArray[2]);
            ClassicAssert.AreEqual(yCluster, clustering.IndexArray[4]);
            ClassicAssert.AreEqual(3, clustering.CountArray[xCluster]);
            ClassicAssert.AreEqual(2, clustering.CountArray[yCluster]);
            ClassicAssert.IsTrue(clustering.SumArray[xCluster].ApproximateEquals(3.0 * V3d.XAxis, 1e-12));
            ClassicAssert.IsTrue(clustering.SumArray[yCluster].ApproximateEquals(2.0 * V3d.YAxis, 1e-12));
        }

        [Test]
        public void PlaneClusteringUsesRefreshedRandomBitsForRepresentativeDirection()
        {
            var low = new FixedRandom(0);
            var high = new FixedRandom(int.MaxValue);

            var lowClustering = new PlaneEpsilonClustering(
                s_interleavedPlaneGroups.Length, s_interleavedPlaneGroups,
                epsNormal: 0.01, epsDist: 0.01, rnd: low);
            var highClustering = new PlaneEpsilonClustering(
                s_interleavedPlaneGroups.Length, s_interleavedPlaneGroups,
                epsNormal: 0.01, epsDist: 0.01, rnd: high);

            AssertRandomRepresentativeResults(lowClustering, highClustering, low, high);
        }

        [Test]
        public void GenericPlaneClusteringUsesRefreshedRandomBitsForRepresentativeDirection()
        {
            var low = new FixedRandom(0);
            var high = new FixedRandom(int.MaxValue);

            var lowClustering = new PlaneEpsilonClustering<Plane3d[]>(
                s_interleavedPlaneGroups.Length, s_interleavedPlaneGroups,
                (planes, i) => planes[i].Normal,
                (planes, i) => planes[i].Distance,
                epsNormal: 0.01, epsDist: 0.01, rnd: low);
            var highClustering = new PlaneEpsilonClustering<Plane3d[]>(
                s_interleavedPlaneGroups.Length, s_interleavedPlaneGroups,
                (planes, i) => planes[i].Normal,
                (planes, i) => planes[i].Distance,
                epsNormal: 0.01, epsDist: 0.01, rnd: high);

            AssertRandomRepresentativeResults(lowClustering, highClustering, low, high);
        }

        private static void AssertRandomRepresentativeResults(
            Aardvark.Geometry.Clustering.Clustering lowClustering,
            Aardvark.Geometry.Clustering.Clustering highClustering,
            FixedRandom low, FixedRandom high)
        {
            ClassicAssert.AreEqual(2, lowClustering.Count);
            ClassicAssert.AreEqual(2, highClustering.Count);
            CollectionAssert.AreEqual(new[] { 2, 2 }, lowClustering.CountArray);
            CollectionAssert.AreEqual(new[] { 2, 2 }, highClustering.CountArray);
            CollectionAssert.AreEqual(new[] { 1, 0, 0, 1 }, lowClustering.IndexArray);
            CollectionAssert.AreEqual(new[] { 0, 1, 1, 0 }, highClustering.IndexArray);
            ClassicAssert.AreEqual(1, low.UniformIntCalls);
            ClassicAssert.AreEqual(1, high.UniformIntCalls);
        }
    }
}
