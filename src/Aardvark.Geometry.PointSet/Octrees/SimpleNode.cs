using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Aardvark.Base;
using Aardvark.Data.Points;
using Newtonsoft.Json.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary></summary>
    public class SimpleNode : IPointCloudNode
    {
        private readonly Guid guid;

        /// <summary>
        /// Create node.
        /// </summary>
        public SimpleNode(
            Storage storage, Guid id, Cell cell, int pointCount,
            ImmutableDictionary<DurableDataDefinition, object> data,
            PersistentRef<IPointCloudNode>[] subNodes)
        {
            Storage = storage;
            guid = id;
            Cell = cell;
            Id = id.ToString();
            Data = data;
            Center = cell.BoundingBox.Center;
            PointCountTree = pointCount;
            SubNodes = subNodes;
            FilterState = FilterState.FullyInside;
            NodeType = "SimpleNode";
        }

        /// <summary>
        /// Copy.
        /// </summary>
        public SimpleNode(IPointCloudNode other)
        {
            Data = other.Data;
            Storage = other.Storage;
            if (!Guid.TryParse(other.Id, out guid)) guid = Guid.NewGuid();
            Id = other.Id;
            Cell = other.Cell;
            Center = other.Center;
            PointCountTree = other.PointCountTree;
            SubNodes = other.SubNodes;
            FilterState = other.FilterState;
            NodeType = other.NodeType;
        }

        /// <summary>
        /// Copy.
        /// </summary>
        private SimpleNode(SimpleNode other)
        {
            Data = other.Data;
            Storage = other.Storage;
            guid = other.guid;
            Id = other.Id;
            Cell = other.Cell;
            Center = other.Center;
            PointCountTree = other.PointCountTree;
            SubNodes = other.SubNodes;
            FilterState = other.FilterState;
            NodeType = other.NodeType;
        }

        ///// <summary>
        ///// </summary>
        //public PointSetNode Persist()
        //{
        //    Guid? TryParse(string str)
        //    {
        //        if (str == null) return null;
        //        else if (Guid.TryParse(str, out var g)) return g;
        //        else return null;
        //    }

        //    Guid? TryGetId(string att)
        //    {
        //        if (Attributes.TryGetValue(att, out var t) && Guid.TryParse(t.Item1, out var g)) return g;
        //        else return (Guid?)null;
        //    }
            
        //    var id = Guid.NewGuid();

        //    return new PointSetNode(
        //        id, Cell, PointCountTree, CellAttributes,
        //        TryGetId(PointCloudAttribute.Positions),
        //        TryGetId(PointCloudAttribute.Colors),
        //        TryGetId(PointCloudAttribute.KdTree),
        //        TryGetId(PointCloudAttribute.Normals),
        //        TryGetId(PointCloudAttribute.Intensities),
        //        TryGetId(PointCloudAttribute.Classifications),
        //        SubNodes?.Map(r => TryParse(r?.Id)),
        //        Storage,
        //        writeToStore: true
        //    );
        //}

        /// <summary></summary>
        public SimpleNode WithData(ImmutableDictionary<DurableDataDefinition, object> data)
            => new SimpleNode(this) { Data = data };

        /// <summary></summary>
        public SimpleNode AddData(DurableDataDefinition what, Guid key, object value)
            => new SimpleNode(this) { Data = Data.Add(what, (key, value)) };

        /// <summary></summary>
        public SimpleNode WithStorage(Storage s)
            => new SimpleNode(this) { Storage = s };

        /// <summary></summary>
        public SimpleNode WithId(string id)
            => new SimpleNode(this) { Id = id };

        /// <summary></summary>
        public SimpleNode WithPointCountTree(int count)
            => new SimpleNode(this) { PointCountTree = count };

        /// <summary></summary>
        public ImmutableDictionary<DurableDataDefinition, object> Data { get; private set; }

        /// <summary></summary>
        public Storage Storage { get; private set; }

        /// <summary></summary>
        public string Id { get; private set; }

        /// <summary></summary>
        public Cell Cell { get; private set; }

        /// <summary></summary>
        public V3d Center { get; private set; }

        /// <summary></summary>
        public Box3d BoundingBoxExact
        {
            get
            {
                if (Data.TryGetValue(OctreeAttributes.BoundingBoxExactLocal, out var value) && value is Box3f)
                {
                    var box = (Box3f)value;
                    return new Box3d(Center + (V3d)box.Min, Center + (V3d)box.Max);
                }
                else return Cell.BoundingBox;
            }
        }

        /// <summary></summary>
        public long PointCountTree { get; private set; }

        /// <summary></summary>
        public float PointDistanceAverage => (float)Data[OctreeAttributes.AveragePointDistance];

        /// <summary></summary>
        public float PointDistanceStandardDeviation => (float)Data[OctreeAttributes.AveragePointDistanceStdDev];

        /// <summary></summary>
        public PersistentRef<IPointCloudNode>[] SubNodes { get; private set; }

        /// <summary></summary>
        public FilterState FilterState { get; private set; }

        /// <summary></summary>
        public string NodeType { get; private set; }

        /// <summary></summary>
        public void Dispose() { }

        /// <summary></summary>
        public JObject ToJson() => throw new NotSupportedException();
    }
}
