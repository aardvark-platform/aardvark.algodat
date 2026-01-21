using Aardvark.Base;
using Aardvark.Data.Points;
using Aardvark.Geometry.Points;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using static Aardvark.Data.Potree.PotreeOctree;

namespace Aardvark.Data.Potree
{
    public class PotreeStorage : IDisposable
    {
        private string mStorePath;

        private PotreeMetaData.PotreeData mMetaData;
        private FileStream mDataFileStream;

        public Storage Storage { get; }

        public PotreeStorage(string storePath, LruDictionary<string, object> cache)
        {
            mStorePath = storePath;

            Storage = new Storage((_, _, _) => { }, get, (_, _, _) => null, _ => { }, () => { }, () => { }, cache);

            if (!PotreeMetaData.TryDeserialize(mStorePath + "\\" + "metadata.json", out mMetaData))
                throw new InvalidOperationException("Cannot open store at given path. Maybe no potree store?");
        }

        private byte[] ReadByteRange(FileStream fs, long offset, long count)
        {
            if (offset >= fs.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset is outside the file bounds.");

            fs.Seek(offset, SeekOrigin.Begin);

            using (BinaryReader reader = new BinaryReader(fs,System.Text.Encoding.Default,true))
            {
                // ensure we don’t try to read past the end of the file
                var safeCount = Math.Min(count, fs.Length - offset);
                return reader.ReadBytes((int)safeCount);
            }
        }

        private void LoadHierarchyRecursive(PotreeNode root, ref byte[] data, long offset, long size)
        {
            int bytesPerNode = 22;
            int numNodes = (int)(size / bytesPerNode);

            var nodes = new List<PotreeNode>(numNodes) {  root };

            for (int i = 0; i < numNodes; i++)
            {
                var currentNode = nodes[i];

                var currentOffset = (int)(offset + i * bytesPerNode);

                Span<byte> nodeSpan = data.AsSpan(currentOffset, bytesPerNode);
                byte type = nodeSpan[0];
                byte childMask = nodeSpan[1];
                uint numPoints = BinaryPrimitives.ReadUInt32LittleEndian(nodeSpan.Slice(2, 4));
                long byteOffset = BinaryPrimitives.ReadInt64LittleEndian(nodeSpan.Slice(6, 8));
                long byteSize = BinaryPrimitives.ReadInt64LittleEndian(nodeSpan.Slice(14, 8));

                currentNode.NodeType = type;
                currentNode.NumPoints = numPoints;
                currentNode.ByteOffset = byteOffset;
                currentNode.ByteSize = byteSize;

                if (currentNode.NodeType == 2)
                {
                    LoadHierarchyRecursive(currentNode, ref data, byteOffset, byteSize);
                }
                else
                {
                    for (int childIndex = 0; childIndex < 8; childIndex++)
                    {
                        if ((childMask & (1 << childIndex)) == 0)
                            continue;

                        string childName = currentNode.Name + (char)('0' + childIndex);

                        var childAABB = CreateChildAABB(currentNode.BoundingBox, childIndex);
                        var child = new PotreeNode(childName, currentNode.OctreeRoot, childAABB);
                        currentNode.Children[childIndex] = child;
                        child.Parent = currentNode;

                        nodes.Add(child);
                    }
                }
            }
        }

        private Box3d CreateChildAABB(Box3d aabb, int index)
        {
            var min = aabb.Min;
            var max = aabb.Max;
            var halfSize = new V3d(
                aabb.Size.X * 0.5,
                aabb.Size.Y * 0.5,
                aabb.Size.Z * 0.5
            );

            if ((index & 0b0001) != 0)
                min.Z += halfSize.Z;
            else
                max.Z -= halfSize.Z;

            if ((index & 0b0010) != 0)
                min.Y += halfSize.Y;
            else
                max.Y -= halfSize.Y;

            if ((index & 0b0100) != 0)
                min.X += halfSize.X;
            else
                max.X -= halfSize.X;

            return new Box3d(min, max);
        }

        //read from potree files
        private byte[] get(string key)
        {
            if(Storage.Cache.TryGetValue(key, out var o))
            {
                if (o is not PotreePointCloudNode node) throw new InvalidOperationException(
                    $"Invariant 26EA902D-4168-447B-9102-88E0C63A49E7. " +
                    $"[get] Store key {key} is not PotreeNode."
                    );

                if (node.PotreeNodeData.NodeType == 2)
                {
                    var offset = node.PotreeNodeData.ByteOffset;
                    var count = node.PotreeNodeData.ByteSize;

                    var data = File.ReadAllBytes(mStorePath + "\\" + "hierarchy.bin");
                    LoadHierarchyRecursive(node.PotreeNodeData,ref data,offset,count);
                }

                if (mDataFileStream == null)
                    mDataFileStream = new FileStream(mStorePath + "\\" + "octree.bin", FileMode.Open, FileAccess.Read);

                return ReadByteRange(mDataFileStream, node.PotreeNodeData.ByteOffset, node.PotreeNodeData.ByteSize);
            }

            return [];
        }

        public IPointNode LoadRoot()
        {
            var octree = new PotreeOctree();
            octree.Url = mStorePath;
            octree.Scale = mMetaData.scale;
            octree.Offset = mMetaData.offset;
            octree.Attributes = new PointAttributes(mMetaData.attributes);

            var min = new V3d(mMetaData.boundingBox.min[0], mMetaData.boundingBox.min[1], mMetaData.boundingBox.min[2]);
            var max = new V3d(mMetaData.boundingBox.max[0], mMetaData.boundingBox.max[1], mMetaData.boundingBox.max[2]);
            var bbox = new Box3d(min, max);

            var root = new PotreeNode("r", octree, bbox);
            root.NodeType = 2;
            root.ByteOffset = 0;
            root.ByteSize = mMetaData.hierarchy.firstChunkSize;

            octree.Root = root;

            return GetPointCloudNode(root);
        }

        public IPointNode GetPointCloudNode(PotreeNode node, PotreePointCloudNode parent = null)
        {
            if (!Storage.HasCache)
                throw new InvalidOperationException("PotreeStorage without cache is not valid");

            var key = node.Id.ToString();

            if (Storage.Cache.TryGetValue(key, out object o))
            {
                if (o is not PotreePointCloudNode cn) throw new InvalidOperationException(
                    $"Invariant 56D238F3-40DC-40B8-9D94-BDBC8975B869" +
                    $"[GetPointCloudNode] Store key {key} is not PotreeNode."
                    );

                return cn;
            }
            //add points from parent
            var n = new PotreePointCloudNode(parent, node, this);
            Storage.Cache.Add(key, n, 1);

            var buffer = Storage.f_get(key) ?? throw new Exception(
                $"PointCloudNode not found (id={key})."
                );

            Storage.Cache.Remove(key);
            n.Fill(ref buffer);
            Storage.Cache.Add(key, n, buffer.Length); //fix cache footprint

            return n;
        }

        public bool TryGetPointNode(string key, out IPointNode? node)
        {
            if (!Storage.HasCache)
                throw new InvalidOperationException("PotreeStorage without cache is not valid");

            if (Storage.Cache.TryGetValue(key, out object o))
            {
                if (o is not PotreePointCloudNode n) throw new InvalidOperationException(
                    $"Invariant B09EF0AA-05B1-458B-9921-97BC0FDB5407." +
                    $"[GetPointCloudNode] Store key {key} is not PotreeNode."
                    );

                node = n;
                return true;
            }

            node = null;

            return false;
        }

        public void Dispose() => Dispose(true);

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                mDataFileStream?.Dispose();
        }
    }
}