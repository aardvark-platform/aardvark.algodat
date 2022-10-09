using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Points
{
    public class MultiNodeIndex
    {
        /// <summary>
        /// Blob key of this MultiNodeIndex.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Key of tree root node.
        /// </summary>
        public string RootNodeId => Id;

        /// <summary>
        /// Blob key containing tree nodes this index applies to. 
        /// </summary>
        public string TreeBlobId { get; }

        private readonly Dictionary<string, (long offset, int size)> _index;

        public (long offset, int size) GetOffsetAndSize(string key) => _index[key];
        public bool TryGetOffsetAndSize(string key, out (long offset, int size) result) => _index.TryGetValue(key, out result);

        public PossiblyGZippedBuffer GetNodeBlobGzipped(string key, Func<string, long, int, PossiblyGZippedBuffer> getBlobPartial)
        {
            var (offset, size) = _index[key];
            var buffer = getBlobPartial(TreeBlobId, offset, size);
            return buffer;
        }

        public IPointCloudNode GetNode(string key, Func<string, PossiblyGZippedBuffer> getBlob, Func<string, long, int, PossiblyGZippedBuffer> getBlobPartial)
        {
            //var buffer = GetNodeBlobGzipped(key, getBlobPartial);
            //var result = QuantizedOctreeNode.Decode(buffer, getBlob, getBlobPartial);
            //return result;
            throw new NotImplementedException();
        }

        private MultiNodeIndex(string id, string treeBlobId, Dictionary<string, (long offset, int size)> index)
        {
            Id = id;
            TreeBlobId = treeBlobId;
            _index = index;
        }

        public byte[] Encode()
        {
            var keys = _index.Select(kv => kv.Key).ToArray();
            var offsets = _index.Select(kv => kv.Value.offset).ToArray();
            var sizes = _index.Select(kv => kv.Value.size).ToArray();
            var map = ImmutableDictionary<string, (Durable.Def, object)>.Empty
                .Add("Id", (Durable.Primitives.StringUTF8, Id))
                .Add("NodeDataId", (Durable.Primitives.StringUTF8, TreeBlobId))
                .Add("Keys", (Durable.Primitives.StringUTF8Array, keys))
                .Add("Offsets", (Durable.Primitives.Int32Array, offsets))
                .Add("Sizes", (Durable.Primitives.Int32Array, sizes))
                ;

            var buffer = DurableCodec.Serialize(Durable.Octree.MultiNodeIndex, map);
            var bufferGz = buffer.Gzip();
            return bufferGz;
        }

        public static MultiNodeIndex Decode(object obj)
        {
            var map = (IDictionary<string, (Durable.Def key, object value)>)obj;

            var id = (string)map["Id"].value;
            var nodeDataId = (string)map["NodeDataId"].value;
            var keys = (string[])map["Keys"].value;
            var offsets = (int[])map["Offsets"].value;
            var sizes = (int[])map["Sizes"].value;

            var index = new Dictionary<string, (long offset, int size)>();
            for (var i = 0; i < keys.Length; i++)
                index[keys[i]] = (offsets[i], sizes[i]);

            return new MultiNodeIndex(id, nodeDataId, index);
        }

        public static MultiNodeIndex Decode(byte[] buffer)
        {
            var (_, obj) = DurableCodec.Deserialize(buffer);
            var map = (IDictionary<string, (Durable.Def key, object value)>)obj;
            return Decode(map);
        }
    }

    public class MultiNode : IPointCloudNode
    {
        public MultiNodeIndex Index { get; }
        public PointSetNode Node { get; }
        public PersistentRef<IPointCloudNode>[] _subnodes;

        private (bool ok, IPointCloudNode node) TryLoadNode(string id)
        {
            if (Index.TryGetOffsetAndSize(id, out var result))
            {
                try
                {
                    var bufferRootNode = StorageExtensions.UnGZip(Storage.f_getSlice(Index.TreeBlobId, result.offset, result.size));
                    var node = PointSetNode.Decode(Storage, bufferRootNode);
                    var indexnode = new MultiNode(Index, node);
                    return (true, indexnode);
                }
                catch (Exception e)
                {
                    Report.Warn($"{e}");
                    return (false, null);
                }
            }
            else
            {
                return (false, null);
            }
        }

        private IPointCloudNode LoadNode(string id) => TryLoadNode(id).node;

        public MultiNode(MultiNodeIndex index, PointSetNode node)
        {
            Index = index;
            Node = node;
            _subnodes = node.Subnodes?.Map(x => x != null ? new PersistentRef<IPointCloudNode>(x.Id, LoadNode, TryLoadNode) : null);
        }

        public PersistentRef<IPointCloudNode>[] Subnodes => _subnodes;

        public Storage Storage => Node.Storage;
        public bool IsMaterialized => Node.IsMaterialized;
        public Guid Id => Node.Id;
        public bool IsTemporaryImportNode => Node.IsTemporaryImportNode;
        public Cell Cell => Node.Cell;
        public V3d Center => Node.Center;
        public int PointCountCell => Node.PointCountCell;
        public long PointCountTree => Node.PointCountTree;
        public bool IsLeaf => Node.IsLeaf;
        public IReadOnlyDictionary<Durable.Def, object> Properties => Node.Properties;
        public bool HasPositions => Node.HasPositions;
        public PersistentRef<V3f[]> Positions => Node.Positions;
        public V3d[] PositionsAbsolute => Node.PositionsAbsolute;
        public bool HasBoundingBoxExactLocal => Node.HasBoundingBoxExactLocal;
        public Box3f BoundingBoxExactLocal => Node.BoundingBoxExactLocal;
        public bool HasBoundingBoxExactGlobal => Node.HasBoundingBoxExactGlobal;
        public Box3d BoundingBoxExactGlobal => Node.BoundingBoxExactGlobal;
        public Box3d BoundingBoxApproximate => Node.BoundingBoxApproximate;
        public bool HasKdTree => Node.HasKdTree;
        public PersistentRef<PointRkdTreeF<V3f[], V3f>> KdTree => Node.KdTree;
        public bool HasColors => Node.HasColors;
        public PersistentRef<C4b[]> Colors => Node.Colors;
        public bool HasNormals => Node.HasNormals;
        public PersistentRef<V3f[]> Normals => Node.Normals;
        public bool HasIntensities => Node.HasIntensities;
        public PersistentRef<int[]> Intensities => Node.Intensities;
        public bool HasClassifications => Node.HasClassifications;
        public PersistentRef<byte[]> Classifications => Node.Classifications;

        #region Velocities

        /// <summary>
        /// Deprecated. Always returns false. Use custom attributes instead.
        /// </summary>
        [Obsolete("Use custom attributes instead.")]
        public bool HasVelocities => Node.HasVelocities;

        /// <summary>
        /// Deprecated. Always returns null. Use custom attributes instead.
        /// </summary>
        [Obsolete("Use custom attributes instead.")]
        public PersistentRef<V3f[]> Velocities => Node.Velocities;

        #endregion

        public bool HasCentroidLocal => Node.HasCentroidLocal;
        public V3f CentroidLocal => Node.CentroidLocal;
        public bool HasCentroidLocalStdDev => Node.HasCentroidLocalStdDev;
        public float CentroidLocalStdDev => Node.CentroidLocalStdDev;
        public bool HasMinTreeDepth => Node.HasMinTreeDepth;
        public int MinTreeDepth => Node.MinTreeDepth;
        public bool HasMaxTreeDepth => Node.HasMaxTreeDepth;
        public int MaxTreeDepth => Node.MaxTreeDepth;
        public bool HasPointDistanceAverage => Node.HasPointDistanceAverage;
        public float PointDistanceAverage => Node.PointDistanceAverage;
        public bool HasPointDistanceStandardDeviation => Node.HasPointDistanceStandardDeviation;
        public float PointDistanceStandardDeviation => Node.PointDistanceStandardDeviation;      
        public bool Has(Durable.Def what) => Node.Has(what);
        public IPointCloudNode Materialize() => Node.Materialize();
        public bool TryGetValue(Durable.Def what, out object o) => Node.TryGetValue(what, out o);

        #region Not supported ...

        /// <summary>
        /// MultiNode does not support Encode.
        /// </summary>
        public byte[] Encode() => throw new NotImplementedException();

        /// <summary>
        /// MultiNode does not support WithSubNodes.
        /// </summary>
        public IPointCloudNode WithSubNodes(IPointCloudNode[] subnodes) => throw new NotImplementedException();

        /// <summary>
        /// MultiNode does not support WriteToStore.
        /// </summary>
        public IPointCloudNode WriteToStore() => throw new NotImplementedException();

        /// <summary>
        /// MultiNode does not support With.
        /// </summary>
        public IPointCloudNode With(IReadOnlyDictionary<Durable.Def, object> replacements) => throw new NotImplementedException();

        #endregion
    }

    public class PossiblyGZippedBuffer
    {
        private byte[] _buffer;
        private bool _isGZipped;

        public PossiblyGZippedBuffer(byte[] buffer, bool isGZipped)
        {
            _buffer = buffer;
            _isGZipped = isGZipped;
        }

        public byte[] GetUncompressed()
        {
            if (_isGZipped)
            {
                _buffer = StorageExtensions.UnGZip(_buffer);
                _isGZipped = false;
            }
            return _buffer;
        }
    }
}
