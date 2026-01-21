using Aardvark.Base;
using Aardvark.Geometry;
using Aardvark.Geometry.Points;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Aardvark.Data.Potree
{
    public class PotreePointCloudNode : IPointNode
    {
        public PotreePointCloudNode Parent { get; }
        public PotreeNode PotreeNodeData { get; }
        public PotreeStorage PotreeStorage { get; }

        public Box3d CellBounds => PotreeNodeData.BoundingBox;

        public Box3d DataBounds => PotreeNodeData.BoundingBox;

        public V3d[] Positions => mPositions;

        public PointKdTree? KdTree => new PointKdTree(Positions.Map(p => (V3f)(p - Positions[0])).BuildKdTree(),Positions[0]);

        public IPointNode[] Children => PotreeNodeData.Children.SelectNotNull(kvp => PotreeStorage.GetPointCloudNode(kvp.Value,this)).ToArray();

        private V3d[] mPositions;
        private C4b[]? mColors;
        private int[]? mIntensities;
        private byte[]? mClassifications;

        /// <summary>
        /// Creates node.
        /// </summary>
        public PotreePointCloudNode(PotreePointCloudNode parent, PotreeNode node, PotreeStorage storage)
        {
            Parent = parent;
            PotreeNodeData = node;
            PotreeStorage = storage;
        }

        public void Fill(ref byte[] buffer)
        {
            var bufferSpan = buffer.AsSpan();

            var bytesPerPoint = PotreeNodeData.OctreeRoot.Attributes.ByteSize;
            var attributeOffset = 0;

            var scale = PotreeNodeData.OctreeRoot.Scale;
            double scaleX = scale[0];
            double scaleY = scale[1];
            double scaleZ = scale[2];

            var offset = PotreeNodeData.OctreeRoot.Offset;
            double offsetX = offset[0];
            double offsetY = offset[1];
            double offsetZ = offset[2];

            foreach (var pointAttribute in PotreeNodeData.OctreeRoot.Attributes.Attributes)
            {
                switch(pointAttribute.Name)
                {
                    case "position":
                        mPositions = new V3d[PotreeNodeData.NumPoints];

                        for (var j = 0; j < PotreeNodeData.NumPoints; j++)
                        {
                            var pointOffset = j * bytesPerPoint + attributeOffset;

                            var xRaw = BinaryPrimitives.ReadInt32LittleEndian(bufferSpan.Slice(pointOffset, 4));
                            var yRaw = BinaryPrimitives.ReadInt32LittleEndian(bufferSpan.Slice(pointOffset + 4, 4));
                            var zRaw = BinaryPrimitives.ReadInt32LittleEndian(bufferSpan.Slice(pointOffset + 8, 4));

                            var x = xRaw * scaleX + offsetX;
                            var y = yRaw * scaleY + offsetY;
                            var z = zRaw * scaleZ + offsetZ;

                            mPositions[j] = new V3d(x, y, z);
                        }

                        break;

                    case "rgba":
                        mColors = new C4b[PotreeNodeData.NumPoints];

                        for (var j = 0; j < PotreeNodeData.NumPoints; j++)
                        {
                            var pointOffset = j * bytesPerPoint + attributeOffset;

                            var rRaw = BinaryPrimitives.ReadUInt16LittleEndian(bufferSpan.Slice(pointOffset, 2));
                            var gRaw = BinaryPrimitives.ReadUInt16LittleEndian(bufferSpan.Slice(pointOffset + 2, 2));
                            var bRaw = BinaryPrimitives.ReadUInt16LittleEndian(bufferSpan.Slice(pointOffset + 4, 2));

                            var x = rRaw > 255 ? rRaw / 256 : rRaw;
                            var y = gRaw > 255 ? gRaw / 256 : gRaw;
                            var z = bRaw > 255 ? bRaw / 256 : bRaw;

                            mColors[j] = new C4b(x, y, z);
                        }

                        break;

                    case "intensity":
                        mIntensities = new int[PotreeNodeData.NumPoints];

                        for (var j = 0; j < PotreeNodeData.NumPoints; j++)
                        {
                            var pointOffset = j * bytesPerPoint + attributeOffset;
                            mIntensities[j] = BinaryPrimitives.ReadUInt16LittleEndian(bufferSpan.Slice(pointOffset, 2));
                        }

                        break;

                    case "classification":
                        mClassifications = new byte[PotreeNodeData.NumPoints];

                        for (var j = 0; j < PotreeNodeData.NumPoints; j++)
                        {
                            var pointOffset = j * bytesPerPoint + attributeOffset;
                            mClassifications[j] = bufferSpan[pointOffset];
                        }

                        break;
                }

                attributeOffset += pointAttribute.ByteSize;
            }

            //add parent infos to current node, because aardvark octree stores all points in leafs, while inner node contain subsets of their leaf-data 
            if (Parent != null)
            {
                var positionData = Parent.Positions;

                var parentData = GetPointsFromParentParallelSIMD(Parent.Positions, PotreeNodeData.BoundingBox);

                void mergeParentData<T>(
                    Symbol key,
                    T[] currentData,
                    Func<int, T> parentValueSelector
                )
                {
                    var merged = new T[currentData.Length + parentData.Length];
                    Array.Copy(currentData, merged, currentData.Length);

                    Parallel.For(0, parentData.Length, i => merged[currentData.Length + i] = parentValueSelector(i));

                    if (key == PointNodeAttributes.Positions && merged is V3d[] positions)
                        mPositions = positions;
                    if (key == PointNodeAttributes.Colors && merged is C4b[] colors)
                        mColors = colors;
                    if (key == PointNodeAttributes.Intensities && merged is int[] intensities)
                        mIntensities = intensities;
                    if (key == PointNodeAttributes.Classifications && merged is byte[] classifications)
                        mClassifications = classifications;
                }

                mergeParentData(PointNodeAttributes.Positions, Positions, i => parentData[i].Item1);

                if (Parent.TryGetAttribute(PointNodeAttributes.Colors, out var pCs) && pCs is C4b[] parentColors)
                    mergeParentData(PointNodeAttributes.Colors, mColors ?? [], i => parentColors[parentData[i].Item2]);

                if (Parent.TryGetAttribute(PointNodeAttributes.Intensities, out var pInt) && pCs is int[] parentIntensities)
                    mergeParentData(PointNodeAttributes.Colors, mIntensities ?? [], i => parentIntensities[parentData[i].Item2]);

                if (Parent.TryGetAttribute(PointNodeAttributes.Classifications, out var pCls) && pCls is byte[] parentClassifcations)
                    mergeParentData(PointNodeAttributes.Colors, mClassifications ?? [], i => parentClassifcations[parentData[i].Item2]);
            }
        }

        private (V3d,int)[] GetPointsFromParentParallelSIMD(V3d[] inputPoints, Box3d bounds)
        {
            var childIndex = PotreeNodeData.Name.Last() - '0';

            Func<System.Numerics.Vector<double>, System.Numerics.Vector<double>, System.Numerics.Vector<long>> vecCompX, vecCompY, vecCompZ;
            Func<double, double, bool> compX,compY,compZ;

            vecCompX = (childIndex is 4 or 5 or 6 or 7) ? System.Numerics.Vector.LessThanOrEqual : System.Numerics.Vector.LessThan;
            compX = (childIndex is 4 or 5 or 6 or 7) ? (a, b) => a <= b : (a, b) => a < b;

            vecCompY = (childIndex is 2 or 3 or 6 or 7) ? System.Numerics.Vector.LessThanOrEqual : System.Numerics.Vector.LessThan;
            compY = (childIndex is 2 or 3 or 6 or 7) ? (a, b) => a <= b : (a, b) => a < b;

            vecCompZ = (childIndex is 1 or 3 or 5 or 7) ? System.Numerics.Vector.LessThanOrEqual : System.Numerics.Vector.LessThan;
            compZ = (childIndex is 1 or 3 or 5 or 7) ? (a, b) => a <= b : (a, b) => a < b;

            int count = inputPoints.Length;

            double[] xs = new double[count];
            double[] ys = new double[count];
            double[] zs = new double[count];

            for (int i = 0; i < inputPoints.Length; i++)
            {
                xs[i] = inputPoints[i].X;
                ys[i] = inputPoints[i].Y;
                zs[i] = inputPoints[i].Z;
            }

            // use SIMD
            int vectorSize = System.Numerics.Vector<double>.Count;
            var minX = new System.Numerics.Vector<double>(bounds.Min.X);
            var minY = new System.Numerics.Vector<double>(bounds.Min.Y);
            var minZ = new System.Numerics.Vector<double>(bounds.Min.Z);
            var maxX = new System.Numerics.Vector<double>(bounds.Max.X);
            var maxY = new System.Numerics.Vector<double>(bounds.Max.Y);
            var maxZ = new System.Numerics.Vector<double>(bounds.Max.Z);

            // thread-safe collection of results
            var resultBag = new ConcurrentBag<List<(V3d,int)>>();

            // parallel over chunks of data
            int coreCount = Environment.ProcessorCount;
            int minChunkSize = System.Numerics.Vector<double>.Count * 4; // ensure SIMD-friendly minimum
            int maxChunkSize = 65536; // upper bound to prevent overly large chunks

            // target: ~2-4x more chunks than cores for parallel load balancing
            int estimatedChunkSize = Math.Max(minChunkSize, count / (coreCount * 4));

            // clamp to safe bounds
            int chunkSize = Math.Min(Math.Max(estimatedChunkSize, minChunkSize), maxChunkSize);

            int chunkCount = (count + chunkSize - 1) / chunkSize;

            Parallel.For(0, chunkCount, chunkIndex =>
            {
                int chunkStart = chunkIndex * chunkSize;
                int chunkEnd = Math.Min(chunkStart + chunkSize, count);

                var local = new List<(V3d,int)>(chunkSize / 2); // preallocate some capacity

                int simdEnd = chunkEnd - ((chunkEnd - chunkStart) % vectorSize);

                // SIMD section
                for (int i = chunkStart; i < simdEnd; i += vectorSize)
                {
                    var vx = new System.Numerics.Vector<double>(xs, i);
                    var vy = new System.Numerics.Vector<double>(ys, i);
                    var vz = new System.Numerics.Vector<double>(zs, i);

                    var mask = System.Numerics.Vector.GreaterThanOrEqual(vx, minX) & vecCompX(vx, maxX) &
                               System.Numerics.Vector.GreaterThanOrEqual(vy, minY) & vecCompY(vy, maxY) &
                               System.Numerics.Vector.GreaterThanOrEqual(vz, minZ) & vecCompZ(vz, maxZ);

                    for (int j = 0; j < vectorSize; j++)
                    {
                        if (mask[j] != 0)
                        {
                            var idx = i + j;
                            local.Add((new V3d(xs[idx], ys[idx], zs[idx]), idx));
                        }
                    }
                }

                // scalar remainder
                for (int i = simdEnd; i < chunkEnd; i++)
                {
                    var p = new V3d(xs[i], ys[i], zs[i]);
                    if (p.X >= bounds.Min.X && compX(p.X, bounds.Max.X) &&
                        p.Y >= bounds.Min.Y && compY(p.Y, bounds.Max.Y) &&
                        p.Z >= bounds.Min.Z && compZ(p.Z, bounds.Max.Z))
                    {
                        local.Add((p,i));
                    }
                }

                if (local.Count > 0)
                    resultBag.Add(local);
            });

            // merge all thread-local results into final array
            int totalCount = resultBag.Sum(l => l.Count);
            var result = new (V3d,int)[totalCount];
            int index = 0;

            foreach (var list in resultBag)
            {
                list.CopyTo(result, index);
                index += list.Count;
            }

            return result;
        }

        public bool TryGetAttribute(Symbol name, out Array data)
        {
            if(name == PointNodeAttributes.Positions)
            {
                data = Positions;
                return true;
            }

            if (name == PointNodeAttributes.Colors && mColors != null)
            {
                data = mColors;
                return true;
            }

            if (name == PointNodeAttributes.Intensities && mIntensities != null)
            {
                data = mIntensities;
                return true;
            }

            if (name == PointNodeAttributes.Classifications && mClassifications != null)
            {
                data = mClassifications;
                return true;
            }

            data = null;
            return false;
        }
    }
}