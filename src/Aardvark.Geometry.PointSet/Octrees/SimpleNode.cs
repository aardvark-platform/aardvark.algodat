using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Aardvark.Base;
using Aardvark.Data.Points;
using Newtonsoft.Json.Linq;

namespace Aardvark.Geometry.Points
{
    public class SimpleNode : IPointCloudNode
    {
        private static string[] AttributeNames =
            new string[]
            {
                PointCloudAttribute.Positions,
                PointCloudAttribute.Colors,
                PointCloudAttribute.Normals,
                PointCloudAttribute.Intensities,
                PointCloudAttribute.Classifications,
                PointCloudAttribute.KdTree,
            };

        private static ImmutableDictionary<string, (string, object)> GetAttributes(IPointCloudNode node)
        {
            var result = ImmutableDictionary<string, (string, object)>.Empty;
            foreach(var key in AttributeNames)
            {
                if(node.TryGetPropertyKey(key, out var id))
                {
                    if(node.TryGetPropertyValue(key, out var v))
                    {
                        result = result.Add(key, (id, v));
                    }
                    else
                    {
                        result = result.Add(key, (id, null));
                    }
                }
                else if(node.TryGetPropertyValue(key, out var v))
                {
                    result = result.Add(key, (null, v));
                }
            }
            return result;
        }

        private Guid guid;

        public SimpleNode(
            Storage storage, Guid id, Cell cell, int pointCount,
            ImmutableDictionary<Guid, object> cellAttributes,
            PersistentRef<IPointCloudNode>[] subNodes,
            ImmutableDictionary<string, (string, object)> attributes)
        {
            CellAttributes = cellAttributes;
            Attributes = attributes;
            Storage = storage;
            guid = id;
            Id = id.ToString();
            Cell = cell;
            Center = cell.BoundingBox.Center;
            PointCountTree = pointCount;
            SubNodes = subNodes;
            FilterState = FilterState.FullyInside;
            NodeType = "SimpleNode";
        }

        public SimpleNode(IPointCloudNode other)
        {
            CellAttributes = other.CellAttributes;
            Attributes = GetAttributes(other);
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

        private SimpleNode(SimpleNode other)
        {
            CellAttributes = other.CellAttributes;
            Attributes = other.Attributes;
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


        public PointSetNode Persist()
        {
            Guid? TryParse(string str)
            {
                if (str == null) return null;
                else if (Guid.TryParse(str, out var g)) return g;
                else return null;
            }

            Guid? TryGetId(string att)
            {
                if (Attributes.TryGetValue(att, out var t) && Guid.TryParse(t.Item1, out var g)) return g;
                else return (Guid?)null;
            }
            
            var id = Guid.NewGuid();

            return new PointSetNode(
                id, Cell, PointCountTree, CellAttributes,
                TryGetId(PointCloudAttribute.Positions),
                TryGetId(PointCloudAttribute.Colors),
                TryGetId(PointCloudAttribute.KdTree),
                TryGetId(PointCloudAttribute.Normals),
                TryGetId(PointCloudAttribute.Intensities),
                TryGetId(PointCloudAttribute.Classifications),
                SubNodes?.Map(r => TryParse(r?.Id)),
                Storage,
                true
            );

        }


        public SimpleNode WithCellAttributes(ImmutableDictionary<Guid, object> att)
        {
            return new SimpleNode(this) { CellAttributes = att };
        }
        public SimpleNode WithAttributes(ImmutableDictionary<string, (string, object)> att)
        {
            return new SimpleNode(this) { Attributes = att };
        }
        public SimpleNode AddAttribute(string key, string id, object value)
        {
            return new SimpleNode(this) { Attributes = Attributes.Add(key, (id, value)) };
        }
        public SimpleNode WithStorage(Storage s)
        {
            return new SimpleNode(this) { Storage = s };
        }
        public SimpleNode WithId(string id)
        {
            return new SimpleNode(this) { Id = id };
        }

        public SimpleNode WithPointCountTree(int count)
        {
            return new SimpleNode(this) { PointCountTree = count };
        }

        public ImmutableDictionary<Guid, object> CellAttributes { get; private set; }

        public ImmutableDictionary<string, (string, object)> Attributes { get; private set; }

        public Storage Storage { get; private set; }

        public string Id { get; private set; }

        public Cell Cell { get; private set; }

        public V3d Center { get; private set; }

        public Box3d BoundingBoxExact
        {
            get
            {
                if (CellAttributes.TryGetValue(Geometry.Points.CellAttributes.BoundingBoxExactLocal.Id, out var value) && value is Box3f)
                {
                    var box = (Box3f)value;
                    return new Box3d(Center + (V3d)box.Min, Center + (V3d)box.Max);
                }
                else return Cell.BoundingBox;
            }
        }

        public long PointCountTree { get; private set; }

        public float PointDistanceAverage => this.GetCellAttribute(Aardvark.Geometry.Points.CellAttributes.AveragePointDistance);

        public float PointDistanceStandardDeviation => this.GetCellAttribute(Aardvark.Geometry.Points.CellAttributes.AveragePointDistanceStdDev);

        public PersistentRef<IPointCloudNode>[] SubNodes { get; private set; }

        public FilterState FilterState { get; private set; }

        public string NodeType { get; private set; }

        public void Dispose()
        {
        }

        public JObject ToJson()
        {
            throw new NotSupportedException();
        }

        public bool TryGetCellAttribute<T>(Guid id, out T value)
        {
            if (CellAttributes.TryGetValue(id, out var v) && v is T)
            {
                value = (T)v;
                return true;
            }
            else
            {
                value = default(T);
                return false;
            }
        }

        public bool TryGetPropertyKey(string property, out string key)
        {
            if (Attributes.TryGetValue(property, out var t))
            {
                key = t.Item1;
                return true;
            }
            else
            {
                key = null;
                return false;
            }
        }

        public bool TryGetPropertyValue(string property, out object value)
        {
            if (Attributes.TryGetValue(property, out var t))
            {
                value = t.Item2;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
    }
}
