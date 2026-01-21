using Aardvark.Base;
using System;
using System.Collections.Generic;

namespace Aardvark.Data.Potree
{
    public class PotreeNode
    {
        public Guid Id { get; private set; }
        public string Name { get; set; }
        public int NodeType { get; set; }
        public bool HasChildren => !Children.IsEmpty();
        public long ByteOffset { get; set; }
        public long ByteSize { get; set; }

        public PotreeOctree OctreeRoot { get; private set; }
        public Box3d BoundingBox { get; private set; }
        public Dictionary<int, PotreeNode> Children { get; private set; }
        public long NumPoints { get; set; }
        public PotreeNode Parent { get; set; }

        public PotreeNode(string name, PotreeOctree rootGeometry, Box3d bbox)
        {
            Id = Guid.NewGuid();
            Name = name;
            OctreeRoot = rootGeometry;
            BoundingBox = bbox;
            Children = new Dictionary<int, PotreeNode>();
            NumPoints = 0;
        }
    }
}
