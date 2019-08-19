/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Base.Coder;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry
{
    [RegisterTypeInfo]
    public partial class PolyMesh
        : SymMap, IAwakeable, IBoundingBox3d
    {
        public static readonly Symbol Identifier = "PolyMesh";

        [RegisterTypeInfo]
        public enum WindingOrder
        {
            CounterClockwise,
            Clockwise,
            Undefined
        }

        // fields that are caches of the Map:

        internal int m_faceCount;
        internal int m_vertexIndexCount;
        internal int m_vertexCount;
        internal Range1i m_faceVertexCountRange;     // (0, 0) ... not set yet

        internal int[] m_firstIndexArray;   // .Length == FaceCapacity + 1
        internal int[] m_vertexIndexArray;  // .Length == VertexIndexCapacity
        internal V3d[] m_positionArray;     // .Length == VertexCapacity

        #region Optional Fields

        internal V3d[] m_normalArray;       // ... (only if normals are VertexAttributes)
        internal C4f[] m_colorArray;        // ... (only if colors are VertexAttributes)

        #endregion

        internal int m_eulerCharacteristic; // =! int.MinValue iff HasTopology

        internal SymbolDict<Array> m_vertexAttributes;
        internal SymbolDict<Array> m_faceAttributes;
        internal SymbolDict<Array> m_faceVertexAttributes;
        internal SymbolDict<Array> m_edgeAttributes;
        internal SymbolDict<object> m_instanceAttributes;

        // fields that are not in the Map and valid if HasTopology == true:

        internal int m_edgeCount;
        internal int m_borderEdgeCount;
        internal int m_nonManifoldEdgeCount;

        internal int[] m_faceEdgeRefArray;  // .Length == VertexIndexCapacity
        internal FaceRef[] m_edgeFaceRefArray; // .Length == EdgeCapacity * 2

        #region Constructors

        public PolyMesh(
                int initialFaceCapacity,
                int initialVertexIndexCapacity,
                int initialVertexCapacity)
            : this(Identifier)
        {
            if (initialFaceCapacity > 0)
            {
                m_firstIndexArray = new int[initialFaceCapacity + 1];
                this[Property.FirstIndexArray] = m_firstIndexArray;
            }
            if (initialVertexIndexCapacity > 0)
            {
                m_vertexIndexArray = new int[initialVertexIndexCapacity];
                this[Property.VertexIndexArray] = m_vertexIndexArray;
            }
            if (initialVertexCapacity > 0)
            {
                m_positionArray = new V3d[initialVertexCapacity];
                VertexAttributes[Property.Positions] = m_positionArray;
            }
        }

        public PolyMesh()
            : this(Identifier)
        { }

        /// <summary>
        /// Create a PolyMesh as a shallow copy of a supplied other PolyMesh.
        /// </summary>
        public PolyMesh(PolyMesh other)
            : this(other, null)
        { }

        /// <summary>
        /// Create a PolyMesh as a shallow copy of a supplied other PolyMesh
        /// with specified overrides.
        /// </summary>
        public PolyMesh(PolyMesh other, SymbolDict<object> overrides)
            : base(other, overrides)
        {
            Awake(false);
            object ec;
            if (overrides != null &&
                overrides.TryGetValue(Property.EulerCharacteristic, out ec))
            {
                if (ec is int && (int)ec == int.MinValue) return;
            }
            CopyTopologyFrom(other);
        }

        protected PolyMesh(Symbol identifier)
            : base(identifier)
        {
            m_faceCount = 0;
            m_vertexCount = 0;
            m_vertexIndexCount = 0;
            m_faceVertexCountRange = new Range1i(0, 0);

            m_edgeCount = 0;
            m_borderEdgeCount = 0;
            m_nonManifoldEdgeCount = 0;
            m_faceEdgeRefArray = null;
            m_edgeFaceRefArray = null;

            m_eulerCharacteristic = int.MinValue;
            this[Property.EulerCharacteristic] = m_eulerCharacteristic;

            // use properties to set the arrays in order to initialize caches
            VertexAttributes = new SymbolDict<Array>();
            FaceAttributes = new SymbolDict<Array>();
            FaceVertexAttributes = new SymbolDict<Array>();
            EdgeAttributes = new SymbolDict<Array>();
            InstanceAttributes = new SymbolDict<object>();
        }

        #endregion

        #region Constants

        /// <summary>
        /// Use this as a parameter to Copy in order to get a copy without
        /// any topology information.
        /// </summary>
        public static readonly SymbolDict<object> OverridesNoTopology =
            new SymbolDict<object>
            {
                { Property.EulerCharacteristic, int.MinValue }
            };

        #endregion

        #region Properties

        public static class Property
        {
            // cached properties
            public static readonly Symbol FirstIndexArray = "FirstIndexArray";
            public static readonly Symbol VertexIndexArray = "VertexIndexArray";
            public static readonly Symbol Positions = DefaultSemantic.Positions;
            public static readonly Symbol Normals = DefaultSemantic.Normals;
            public static readonly Symbol Colors = DefaultSemantic.Colors;
            public static readonly Symbol Tangents = "Tangents";
            public static readonly Symbol Bitangents = "Bitangents";

            public static readonly Symbol FaceCount = "FaceCount";
            public static readonly Symbol VertexIndexCount = "VertexIndexCount";
            public static readonly Symbol VertexCount = "VertexCount";
            public static readonly Symbol FaceVertexCountRange = "FaceVertexCountRange";

            public static readonly Symbol EulerCharacteristic = "EulerCharacteristic";

            public static readonly Symbol VertexAttributes = "VertexAttributes";
            public static readonly Symbol FaceAttributes = "FaceAttributes";
            public static readonly Symbol FaceVertexAttributes = "FaceVertexAttributes";
            public static readonly Symbol EdgeAttributes = "EdgeAttributes";
            public static readonly Symbol InstanceAttributes = "InstanceAttributes";

            // uncached properties
            public static readonly Symbol PositionFunArray = "PositionFunArray";
            public static readonly Symbol Areas = "Areas";
            public static readonly Symbol Centroids = "Centroids";

            // Instance Attributes (VertexGeometry)
            public static readonly Symbol DiffuseColorTexture = DefaultSemantic.DiffuseColorTexture;
            public static readonly Symbol LightMapTexture = DefaultSemantic.LightMapTexture;
            public static readonly Symbol NormalMapTexture = DefaultSemantic.NormalMapTexture;
            public static readonly Symbol Trafo3d = "Trafo3d";
            public static readonly Symbol DiffuseColorTrafo2d = "DiffuseColorTrafo2d";
            public static readonly Symbol Name = "Name";
            public static readonly Symbol Material = "Material";
            public static readonly Symbol CreaseAngle = "CreaseAngle";
            public static readonly Symbol WindingOrder = "WindingOrder";
            public static readonly Symbol AreaSum = "AreaSum";

            public static readonly Symbol SheetFaceIndices = "SheetFaceIndices";

            // Vertex Attributes (VertexGeometry)
            public static readonly Symbol DiffuseColorCoordinates = DefaultSemantic.DiffuseColorCoordinates;
            public static readonly Symbol LightMapCoordinates = DefaultSemantic.LightMapCoordinates;
            public static readonly Symbol NormalMapCoordinates = DefaultSemantic.NormalMapCoordinates;

            public static readonly Symbol DiffuseColorUTangents = DefaultSemantic.DiffuseColorUTangents;
            public static readonly Symbol DiffuseColorVTangents = DefaultSemantic.DiffuseColorVTangents;

            public static readonly Symbol SheetCoordinates = "SheetCoordinates";

            // Face Attributes
            public static readonly Symbol FaceSheets = "FaceSheets";

            public static readonly Symbol FaceClusterIndices = "FaceClusters";
            public static readonly Symbol FaceClusterCount = "FaceClusterCount";

            public static readonly Symbol FaceComponentIndices = "FaceComponentIndices";
            public static readonly Symbol FaceComponentCount = "FaceComponentCount";
        }

        public int FaceCapacity
        {
            get { return m_firstIndexArray.Length - 1; }
            private set
            {
                Array.Resize(ref m_firstIndexArray, value + 1);
                this[Property.FirstIndexArray] = m_firstIndexArray;
                var changes = from a in m_faceAttributes
                              where a.Key.IsPositive
                              select (a.Key, a.Value.Resized(value));
                foreach (var change in changes.ToList()) m_faceAttributes[change.Item1] = change.Item2;
            }
        }
        public int VertexIndexCapacity
        {
            get { return m_vertexIndexArray.Length; }
            private set
            {
                Array.Resize(ref m_vertexIndexArray, value);
                this[Property.VertexIndexArray] = m_vertexIndexArray;
                var changes = from a in m_faceVertexAttributes
                              where a.Key.IsPositive
                              select (a.Key, a.Value.Resized(value));
                foreach (var change in changes.ToList()) m_faceVertexAttributes[change.Item1] = change.Item2;
            }
        }

        public int VertexCapacity
        {
            get { return m_positionArray.Length; }
            private set
            {
                var changes = from a in m_vertexAttributes
                              where a.Key.IsPositive
                              select (a.Key, a.Value.Resized(value));
                foreach (var change in changes.ToList()) m_vertexAttributes[change.Item1] = change.Item2;

                m_positionArray =  m_vertexAttributes.GetOrDefault(Property.Positions) as V3d[];
                m_normalArray = m_vertexAttributes.GetOrDefault(Property.Normals) as V3d[];
                m_colorArray = m_vertexAttributes.GetOrDefault(Property.Colors) as C4f[];
            }
        }

        public int EdgeCapacity
        {
            get { return m_edgeFaceRefArray.Length >> 1; }
            private set
            {
                int oldSize = m_edgeFaceRefArray.Length;
                Array.Resize(ref m_edgeFaceRefArray, value << 1);
                var changes = from a in m_edgeAttributes
                              where a.Key.IsPositive
                              select (a.Key, a.Value.Resized(value));
                foreach (var change in changes.ToList()) m_edgeAttributes[change.Item1] = change.Item2;
            }
        }

        public int FaceCount
        {
            get { return m_faceCount; }
            set
            {
                if (m_faceCount == 0)
                    FirstIndexArray = new int[value + 1];
                else
                {
                    if (value > FaceCapacity)
                    {
                        Array.Resize(ref m_firstIndexArray, value + 1);
                        this[Property.FirstIndexArray] = m_firstIndexArray;
                    }
                    m_faceCount = value;
                    this[Property.FaceCount] = m_faceCount;
                }
            }
        }

        /// <summary>
        /// Gets the length of a FaceVertexAttributeArray or its IndexArray.
        /// </summary>
        public int VertexIndexCount
        {
            get { return m_vertexIndexCount; }
            set
            {
                if (m_vertexIndexCount == 0)
                    VertexIndexArray = new int[value];
                else
                {
                    if (value > VertexIndexCapacity)
                    {
                        Array.Resize(ref m_vertexIndexArray, value);
                        this[Property.VertexIndexArray] = m_vertexIndexArray;
                    }
                    m_vertexIndexCount = value;
                    this[Property.VertexIndexCount] = m_vertexIndexCount;
                }
            }
        }

        public int VertexCount
        {
            get { return m_vertexCount; }
            set
            {
                if (m_vertexCount == 0)
                    PositionArray = new V3d[value];
                else
                {
                    if (value > VertexCapacity) VertexCapacity = value;
                    m_vertexCount = value;
                    this[Property.VertexCount] = value;
                }
            }
        }

        public Range1i FaceVertexCountRange
        {
            get { return m_faceVertexCountRange; }
        }

        /// <summary>
        /// For each face it holds the index of the first vertex.
        /// The array has a length of FaceCount + 1.
        /// The number of vertices of a face can be calculated with: FirstIndexArray[FaceIndex + 1] - FirstIndexArray[FaceIndex].
        /// </summary>
        public int[] FirstIndexArray
        {
            get { return m_firstIndexArray; }
            set
            {
                m_firstIndexArray = value;
                this[Property.FirstIndexArray] = value;
                m_faceCount = value != null ? value.Length - 1 : 0;
                this[Property.FaceCount] = m_faceCount;

                var faceVertexCountRange = Range1i.Invalid;
                if (m_faceCount > 0)
                {
                    for (int fvi = value[0], i = 1; i < m_faceCount + 1; i++)
                    {
                        int fve = value[i];
                        faceVertexCountRange.ExtendBy(fve - fvi);
                        fvi = fve;
                    }
                }
                this[Property.FaceVertexCountRange]
                        = m_faceVertexCountRange = faceVertexCountRange;
            }
        }

        /// <summary>
        /// Mapping from face vertex index to actual vertex index.
        /// Has a length of FirstIndexArray[FaceCount] - 1.
        /// </summary>
        public int[] VertexIndexArray
        {
            get { return m_vertexIndexArray; }
            set
            {
                m_vertexIndexArray = value;
                this[Property.VertexIndexArray] = value;
                m_vertexIndexCount = value != null ? m_vertexIndexArray.Length : 0;
                this[Property.VertexIndexCount] = m_vertexIndexCount;
            }
        }

        public V3d[] PositionArray
        {
            get { return m_positionArray; }
            set
            {
                m_positionArray = value;
                VertexAttributes[Property.Positions] = value;
                m_vertexCount = value != null ? m_positionArray.Length : 0;
                this[Property.VertexCount] = m_vertexCount;
            }
        }

        public V3d[] NormalArray
        {
            get { return m_normalArray; }
            set
            {
                if (value.Length != m_vertexCount) throw new InvalidOperationException();
                m_normalArray = value;
                VertexAttributes[Property.Normals] = value;
            }
        }

        public C4f[] ColorArray
        {
            get { return m_colorArray; }
            set
            {
                if (value.Length != m_vertexCount) throw new InvalidOperationException();
                m_colorArray = value;
                VertexAttributes[Property.Colors] = value;
            }
        }

        public SymbolDict<Array> VertexAttributes
        {
            get { return m_vertexAttributes; }
            set
            {
                this[Property.VertexAttributes] = m_vertexAttributes = value;

                // use property to initialize dependent fields
                //PositionArray = m_vertexAttributes.Get(Property.Positions) as V3d[];

                // don't use property to initialize m_vertexCount in case the positions are not V3d (V2d, V3f, ...)
                var pa = m_vertexAttributes.GetOrDefault(Property.Positions);
                m_positionArray = pa as V3d[];
                m_vertexCount = pa != null ? pa.Length : 0;
                m_normalArray = m_vertexAttributes.GetOrDefault(Property.Normals) as V3d[];
                m_colorArray = m_vertexAttributes.GetOrDefault(Property.Colors) as C4f[];

                this[Property.VertexCount] = m_vertexCount;
            }
        }

        public SymbolDict<Array> FaceAttributes
        {
            get { return m_faceAttributes; }
            set { this[Property.FaceAttributes] = m_faceAttributes = value; }
        }

        public SymbolDict<Array> FaceVertexAttributes
        {
            get { return m_faceVertexAttributes; }
            set { this[Property.FaceVertexAttributes] = m_faceVertexAttributes = value; }
        }

        public SymbolDict<Array> EdgeAttributes
        {
            get { return m_edgeAttributes; }
            set { this[Property.EdgeAttributes] = m_edgeAttributes = value; }
        }

        public SymbolDict<object> InstanceAttributes
        {
            get { return m_instanceAttributes; }
            set { this[Property.InstanceAttributes] = m_instanceAttributes = value; }
        }

        public IEnumerable<Symbol> VertexAttributeNames
        {
            get { return VertexAttributes.Keys.Where(k => k.IsPositive); }
        }

        public IEnumerable<Symbol> FaceAttributeNames
        {
            get { return FaceAttributes.Keys.Where(k => k.IsPositive); }
        }

        public IEnumerable<Symbol> FaceVertexAttributeNames
        {
            get { return FaceVertexAttributes.Keys.Where(k => k.IsPositive); }
        }

        public T[] FaceAttributeArray<T>(Symbol name)
        {
            if (m_faceAttributes.TryGetValue(name, out Array array)) return (T[])array;
            return null;
        }

        public T[] VertexAttributeArray<T>(Symbol name)
        {
            if (m_vertexAttributes.TryGetValue(name, out Array array)) return (T[])array;
            return null;
        }

        public T[] FaceVertexAttributeArray<T>(Symbol name)
        {
            if (m_faceVertexAttributes.TryGetValue(name, out Array array)) return (T[])array;
            return null;
        }

        public T[] EdgeAttributeArray<T>(Symbol name)
        {
            if (m_edgeAttributes.TryGetValue(name, out Array array)) return (T[])array;
            return null;
        }

        public IEnumerable<IAttribute> FaceIAttributes
        {
            get
            {
                return FaceAttributeNames.Select(n => GetIAttribute(n, m_faceAttributes));
            }
        }

        public IEnumerable<IAttribute> VertexIAttributes
        {
            get
            {
                return VertexAttributeNames.Select(n => GetIAttribute(n, m_vertexAttributes));
            }
        }

        public IEnumerable<IAttribute> FaceVertexIAttributes
        {
            get
            {
                return FaceVertexAttributeNames.Select(n => GetIAttribute(n, m_faceVertexAttributes));
            }
        }
        
        public IEnumerable<Polygon> Polygons
        {
            get
            {
                var fc = m_faceCount;
                for (int fi = 0; fi < fc; fi++)
                    yield return new Polygon(this, fi);
            }
        }

        public bool HasColors { get { return HasAttribute(Property.Colors); } }
        public bool HasNormals { get { return HasAttribute(Property.Normals); } }
        public bool HasPositions { get { return HasAttribute(Property.Positions); } }

        public bool HasAttribute(Symbol attributeName)
        {
            if (VertexAttributes.Contains(attributeName)) return true;
            if (FaceAttributes.Contains(attributeName)) return true;
            if (FaceVertexAttributes.Contains(attributeName)) return true;
            if (InstanceAttributes.Contains(attributeName)) return true;
            return false;
        }

        public bool HasTopology { get { return m_eulerCharacteristic != int.MinValue; } }
        public int EdgeCount { get { return m_edgeCount; } }
        public int BorderEdgeCount { get { return m_borderEdgeCount; } }
        public int NonManifoldEdgeCount { get { return m_nonManifoldEdgeCount; } }
        public int HalfEdgeCount { get { return m_vertexIndexCount; } }

        public bool IsNonManifold
        {
            get
            {
                if (!HasTopology) throw new InvalidOperationException();
                return m_nonManifoldEdgeCount > 0;
            }
        }

        public bool IsManifold
        {
            get
            {
                if (!HasTopology) throw new InvalidOperationException();
                return m_nonManifoldEdgeCount == 0;
            }
        }

        /// <summary>
        /// The Euler characteristic iff the mesh is manifold.
        /// </summary>
        public int EulerCharacteristic { get { return m_eulerCharacteristic; } }

        public IEnumerable<Vertex> Vertices
        {
            get 
            {
                var vc = m_vertexCount;
                for (int vi = 0; vi < vc; vi++)
                    yield return new Vertex(this, vi);
            }
        }

        public IEnumerable<Face> Faces
        {
            get
            {
                var fc = m_faceCount;
                for (int fi = 0; fi < fc; fi++)
                    yield return new Face(this, fi, 0);
            }
        }

        public IEnumerable<Edge> Edges
        {
            get
            {
                var ec = m_edgeCount;
                for (int ei = 0; ei < ec; ei++)
                    yield return new Edge(this, ei, 0);
            }
        }

        public IEnumerable<Edge> HalfEdges
        {
            get
            {
                var vic = m_vertexIndexCount;
                for (int i = 0; i < vic; i++)
                    yield return new Edge(this, m_faceEdgeRefArray[i]);
            }
        }

        public Func<double[], V3d>[] PositionFunArray
        {
            get { return Get<Func<double[], V3d>[]>(Property.PositionFunArray); }
            set { this[Property.PositionFunArray] = value; }
        }

        #endregion

        #region Building Meshes

        /// <summary>
        /// Adds a vertex and returns its index.
        /// </summary>
        public int AddVertex(V3d position)
        {
            if (m_positionArray == null)
            {
                m_positionArray = new V3d[16];
                VertexAttributes[Property.Positions] = m_positionArray;
            }
            else if (m_vertexCount + 1 > VertexCapacity)
                VertexCapacity = VertexCapacity * 2;
            m_positionArray[m_vertexCount] = position;
            this[Property.VertexCount] = ++m_vertexCount;
            return m_vertexCount - 1;
        }

        /// <summary>
        /// Adds a vertex and returns its index.
        /// </summary>
        public int AddVertex(V3d position, V3d normal)
        {
            if (m_vertexCount == 0)
            {
                m_positionArray = new V3d[16];
                VertexAttributes[Property.Positions] = m_positionArray;
                m_normalArray = new V3d[16];
                VertexAttributes[Property.Normals] = m_normalArray;
            }
            else if (m_vertexCount + 1 > VertexCapacity)
                VertexCapacity = VertexCapacity * 2;
            m_positionArray[m_vertexCount] = position;
            m_normalArray[m_vertexCount] = normal;
            this[Property.VertexCount] = ++m_vertexCount;
            return m_vertexCount - 1;
        }

        /// <summary>
        /// Adds a vertex and returns its index.
        /// </summary>
        public int AddVertex(V3d position, V3d normal, C4f color)
        {
            if (m_vertexCount == 0)
            {
                m_positionArray = new V3d[16];
                VertexAttributes[Property.Positions] = m_positionArray;
                m_normalArray = new V3d[16];
                VertexAttributes[Property.Normals] = m_normalArray;
                m_colorArray = new C4f[16];
                VertexAttributes[Property.Colors] = m_colorArray;
            }
            else if (m_vertexCount + 1 > VertexCapacity)
                VertexCapacity = VertexCapacity * 2;
            m_positionArray[m_vertexCount] = position;
            m_normalArray[m_vertexCount] = normal;
            m_colorArray[m_vertexCount] = color;
            this[Property.VertexCount] = ++m_vertexCount;
            return m_vertexCount - 1;
        }

        private void ResizeForFace(int faceVertexCount)
        {
            if (m_faceCount == 0)
            {
                m_firstIndexArray = new int[5];
                this[Property.FirstIndexArray] = m_firstIndexArray;
            }
            else if (m_faceCount + 1 > FaceCapacity)
            {
                FaceCapacity = FaceCapacity * 2;
            }
            var requiredCapacity = m_vertexIndexCount + faceVertexCount;
            if (m_vertexIndexCount == 0)
            {
                var capacity = 16;
                while (requiredCapacity > capacity) capacity *= 2;
                m_vertexIndexArray = new int[capacity];
                this[Property.VertexIndexArray] = m_vertexIndexArray;
            }
            else
            {
                var capacity = VertexIndexCapacity;
                if (requiredCapacity > capacity)
                {
                    capacity *= 2;
                    while (requiredCapacity > capacity) capacity *= 2;
                    VertexIndexCapacity = capacity;
                }
            }
        }

        /// <summary>
        /// Adds a face and returns its index.
        /// </summary>
        public int AddFace(params int[] vertexIndices)
        {
            var len = vertexIndices.Length;
            ResizeForFace(len);
            var fvi = m_firstIndexArray[m_faceCount];
            m_firstIndexArray[++m_faceCount] = fvi + len;
            for (int i = 0; i < len; i++) m_vertexIndexArray[fvi + i] = vertexIndices[i];
            m_vertexIndexCount += len;
            if (m_faceVertexCountRange.Max > 0)
            {
                if (!m_faceVertexCountRange.Contains(len))
                {
                    m_faceVertexCountRange.ExtendBy(len);
                    this[Property.FaceVertexCountRange] = m_faceVertexCountRange;
                }
            }
            else
            {
                m_faceVertexCountRange = new Range1i(len, len);
                this[Property.FaceVertexCountRange] = m_faceVertexCountRange;
            }
            this[Property.FaceCount] = m_faceCount;
            this[Property.VertexIndexCount] = m_vertexIndexCount;
            return m_faceCount - 1;
        }

        /// <summary>
        /// Adds a face and returns its index.
        /// </summary>
        public int AddFaceWithTopology( params int[] vertexIndices)
        {
            var fvc = vertexIndices.Length;
            if (m_faceEdgeRefArray.Length + fvc > m_vertexIndexCount)
                Array.Resize(ref m_faceEdgeRefArray, (int)(m_vertexIndexCount * 1.5 + fvc));

            var newEdgeIndex = m_edgeCount;
            var fi = m_faceCount;

            var vertexEdges = new Edge[fvc];
            //int validCount = 0;
            for (int i = 0; i < fvc; i++)
            {
                var vi0 = vertexIndices[i];
                var vi1 = vertexIndices[(i + 1) % fvc];
                vertexEdges[i] = GetEdge(vi0, vi1);
                //if (vertexEdges[i].IsValid)
                //    validCount++;
            }
                        
            var fvi = m_firstIndexArray[fi];

            for (int side = 0; side < fvc; side++)
            {
                var edge = vertexEdges[side];
                
                if (edge.IsInvalid)
                {
                    // add new edge
                    var er = newEdgeIndex << 1;
                    if (newEdgeIndex + 1 > EdgeCapacity)
                        EdgeCapacity = (int)(newEdgeIndex * 1.5 + 1);
                    m_edgeFaceRefArray[er] = new FaceRef(fi, side);
                    m_edgeFaceRefArray[er + 1] = FaceRef.Invalid;

                    edge = new Edge(this, er);

                    newEdgeIndex++;
                }
                else
                {
                    // duplicate face
                    if (!edge.IsAnyBorder)
                    {
                        vertexEdges.Take(side).ForEach(e => 
                        {
                            if (e.IsValid)
                            {
                                if (m_edgeFaceRefArray[e.Ref].Index == fi)
                                    m_edgeFaceRefArray[e.Ref] = FaceRef.Invalid;
                                else
                                    m_edgeFaceRefArray[e.Ref ^ 1] = FaceRef.Invalid;
                            }
                        });
                        return -1;
                    }

                    // link face to existing edge
                    edge = edge.IsBorder ? edge : edge.Opposite;
                    m_edgeFaceRefArray[edge.Ref] = new FaceRef(fi, side);

                }

                m_faceEdgeRefArray[fvi + side] = edge.Ref;
            }

            m_edgeCount = newEdgeIndex;

            fi = AddFace(vertexIndices);

            //for (int i = 0; i < fvc; i++)
            //{
            //    var vi0 = vertexIndices[i];
            //    var vi1 = vertexIndices[(i + 1) % fvc];
            //    if (!GetEdge(vi0, vi1).IsValid)
            //        validCount++;
            //}           

            return fi;
        }

        public void AddFaceAttributeArray<T>(Symbol name)
        {
            m_faceAttributes[name] = new T[FaceCapacity + 1];
        }

        public void AddVertexAttributeArray<T>(Symbol name)
        {
            m_vertexAttributes[name] = new T[VertexCapacity];
        }

        public void AddFaceVertexAttributeArray<T>(Symbol name)
        {
            m_faceVertexAttributes[name] = new T[VertexIndexCapacity];
        }

        public void AddEdgeAttributeArray<T>(Symbol name)
        {
            m_edgeAttributes[name] = new T[EdgeCapacity];
        }

        /// <summary>
        /// Builds face to edge and edge to face reference arrays given
        /// the Euler characteristic of the mesh. For closed meshes, the
        /// Euler characteristic of the mesh is 2 - 2 * genus of the mesh.
        /// A planar mesh with a single border has an Euler characteristic
        /// of 1. If the Euler characteristic is unknown, 0 can be specified
        /// and it will be computed while building the topology (this may be
        /// slightly more inefficient than specifying it in advance due to
        /// reallocation of arrays). This method works for non-manifold and
        /// multi-component meshes as well.
        /// </summary>
        public void BuildTopology(int eulerCharacteristic = 0)
        {
            var topology = new Topology(m_faceCount, m_vertexIndexCount, m_vertexCount,
                                        eulerCharacteristic, false,
                                        m_firstIndexArray, m_vertexIndexArray);
            m_edgeCount = topology.EdgeCount;
            m_borderEdgeCount = topology.BorderEdgeCount;
            m_nonManifoldEdgeCount = topology.NonManifoldEdgeCount;
            m_faceEdgeRefArray = topology.FaceEdgeRefArray;
            m_edgeFaceRefArray = topology.EdgeFaceRefArray;
            m_eulerCharacteristic = topology.EulerCharacteristic;
            this[Property.EulerCharacteristic] = m_eulerCharacteristic;
        }

        /// <summary>
        /// Returns the mesh without topology, i.e. if the mesh has topology
        /// a copy without topology is returned, if the mesh does not have
        /// topology, it is directly returned.
        /// </summary>
        public PolyMesh WithoutTopology()
        {
            return HasTopology ? Copy(OverridesNoTopology) : this;
        }

        /// <summary>
        /// Returns a copy of the mesh with newly created topology.
        /// </summary>
        public PolyMesh WithNewTopology(int eulerCharacteristic = 0)
        {
            var m = Copy(OverridesNoTopology);
            m.BuildTopology(eulerCharacteristic);
            return m;
        }

        /// <summary>
        /// Returns the mesh with topology, i.e. if it already has topology,
        /// it is directly returned, if it does not have topology a new copy
        /// with topology is returned.
        /// </summary>
        public PolyMesh WithTopology(int eulerCharacteristic = 0)
        {
            if (HasTopology) return this;
            var m = Copy();
            m.BuildTopology(eulerCharacteristic);
            return m;
        }

        private void CopyTopologyFrom(PolyMesh other)
        {
            m_edgeCount = other.m_edgeCount;
            m_borderEdgeCount = other.m_borderEdgeCount;
            m_nonManifoldEdgeCount = other.m_nonManifoldEdgeCount;
            m_faceEdgeRefArray = other.m_faceEdgeRefArray;
            m_edgeFaceRefArray = other.m_edgeFaceRefArray;
            m_eulerCharacteristic = other.m_eulerCharacteristic;
            this[Property.EulerCharacteristic] = m_eulerCharacteristic;
        }

        #endregion

        #region Accessing Components

        public Polygon GetPolygon(int polygonIndex) => new Polygon(this, polygonIndex);

        public Vertex GetVertex(int vertexIndex) => new Vertex(this, vertexIndex);

        public Face GetFace(int faceIndex) => new Face(this, faceIndex, 0);

        public Edge GetEdge(int edgeIndex) => new Edge(this, edgeIndex, 0);

        /// <summary>
        /// Returns the existing edge between those vertices or an invalid edge.
        /// Note: Topology required, can fail to get desired edge (failure by design)
        /// </summary>
        public Edge GetEdge(int vertexIndex0, int vertexIndex1)
        {
            var edge = default(Edge);

            edge = GetVertex(vertexIndex0).Edges.Where(e => e.FromVertexIndex == vertexIndex1).FirstOrDefault();

            // in cases where v0 is the only connection between two faces (however, this can still fail)
            if (edge.IsInvalid)
                edge = GetVertex(vertexIndex1).Edges.Where(e => e.FromVertexIndex == vertexIndex0).FirstOrDefault();;

            //if (edge.IsValid && !edge.IsBetween(vertexIndex0, vertexIndex1))
            //    Report.Line("damn");

            return edge;
        }


        /// <summary>
        /// Returns all vertices around the face in counter-clockwise order,
        /// starting at a specified side.
        /// </summary>
        public IEnumerable<int> VertexIndicesOfFace(int fi, int fs)
        {
            int[] fia = FirstIndexArray, via = VertexIndexArray;
            int fvi1 = fia[fi], fve1 = fvi1 + fs;
            int fvi0 = fve1, fve0 = fia[fi + 1];
            while (fvi0 < fve0) yield return via[fvi0++];
            while (fvi1 < fve1) yield return via[fvi1++];
        }

        /// <summary>
        /// Returns all vertices around the face in counter-clockwise order.
        /// </summary>
        public IEnumerable<int> VertexIndicesOfFace(int fi)
        {
            int[] fia = FirstIndexArray, via = VertexIndexArray;
            int fvi = fia[fi], fve = fia[fi + 1];
            while (fvi < fve) yield return via[fvi++];
        }


        public int VertexCountOfFace(int faceIndex)
        {
            var fia = m_firstIndexArray;
            return fia[faceIndex + 1] - fia[faceIndex];
        }

        public int EdgeCountOfFace(int faceIndex)
        {
            var fia = m_firstIndexArray;
            return fia[faceIndex + 1] - fia[faceIndex];
        }

        #endregion

        #region Copy Methods

        /// <summary>
        /// Returns a shallow copy of the mesh. This of course also copies
        /// the topology.
        /// </summary>
        public PolyMesh Copy() => Copy(null);

        /// <summary>
        /// Returns a shallow copy of the PolyMesh with specified overrides.
        /// This of course also copies the topology, except if your override
        /// prevents topology from being copied.
        /// </summary>
        public PolyMesh Copy(SymbolDict<object> overrides) => new PolyMesh(this, overrides);

        /// <summary>
        /// Returns a shallow copy of the mesh, with the vertex positions
        /// modified according to the supplied parameter array. The vertex
        /// positions are computed using the <see cref="PositionFunArray"/>.
        /// </summary>
        public PolyMesh ParametrizedCopy(double[] parameters)
        {
            var vfa = PositionFunArray;
            var pa = new V3d[m_vertexCount].SetByIndex(i => vfa[i](parameters));
            return Copy(new SymbolDict<object> { { Property.Positions, pa } });
        }

        /// <summary>
        /// Reverses the point order of the faces of the mesh according to the
        /// supplied reverse function of the face index. Clears any built topology.
        /// </summary>
        public void ReverseFaces(Func<int, bool> faceIndex_reverseFun)
        {
            m_vertexIndexArray.ReverseGroups(m_firstIndexArray, m_faceCount, faceIndex_reverseFun);
            foreach (var kvp in FaceVertexAttributes)
                if (kvp.Key.IsPositive)
                    kvp.Value.ReverseGroups(m_firstIndexArray, m_faceCount, faceIndex_reverseFun);
            ClearTopology();
        }

        /// <summary>
        /// Returns a copied version of the mesh, whith faces flipped
        /// according to the supplied flip function of the index. Note,
        /// that topology information is not copied or built, since the
        /// actual euler characteristic of the mesh is not known.
        /// </summary>
        public PolyMesh FaceReversedCopy(Func<int, bool> faceIndex_reverseFun)
        {
            var m = Copy(OverridesNoTopology);
            int[] fia = m.FirstIndexArray;
            int fc = m_faceCount;
            m.VertexIndexArray = VertexIndexArray.GroupReversedCopy(
                                    fia, fc, faceIndex_reverseFun);
            m.FaceVertexAttributes = FaceVertexAttributes.Copy(
                (n, a) => n.IsNegative ? a
                            : a.GroupReversedCopy(
                                    fia, fc, faceIndex_reverseFun));
            return m;
        }

        /// <summary>
        /// Returns a copied version of the mesh, whith faces flipped
        /// according to the supplied face flip array. Note, that topology
        /// information is not copied or built, since the actual euler
        /// characteristic of the mesh is not known. If the supplied
        /// array is null (the default parameter) all faces are flipped.
        /// </summary>
        public PolyMesh FaceReversedCopy(bool[] faceReverseArray = null)
        {
            if (faceReverseArray == null)
                return FaceReversedCopy(fi => true);
            if (faceReverseArray.Length != m_faceCount) throw new InvalidOperationException();
            return FaceReversedCopy(fi => faceReverseArray[fi]);
        }

        /// <summary>
        /// Returns a (hopeflully) manifold copy of the mesh, by using non-
        /// manifold edge connections to combine faces with inconsistent
        /// vertex order. If the vertex order is the only problem preventing
        /// the mesh from being manifold, the returned mesh is indeed
        /// manifold. If you need more control consider 'Analyze'
        /// and 'FaceReversedCopy'. Note, that topology
        /// information is not copied or built, since the actual euler
        /// characteristic of the mesh is not known.
        /// </summary>
        public PolyMesh ManifoldCopy()
        {
            var cc = Analyze(out uint[] faceComponentIndexArray, out bool[] faceReverseArray);
            return FaceReversedCopy(faceReverseArray);
        }

        public PolyMesh Copy(
                SymbolDict<Func<Array, Array>> arrayAttributesFunMap,
                Func<Array, Array> arrayAttributesDefaultFun)
        {
            return Copy(
                new SymbolDict<object>
                {
                    { Property.FaceAttributes, FaceAttributes.Copy(
                            arrayAttributesFunMap, arrayAttributesDefaultFun) }, 
                    { Property.VertexAttributes, VertexAttributes.Copy(
                            arrayAttributesFunMap, arrayAttributesDefaultFun) }, 
                    { Property.FaceVertexAttributes, FaceVertexAttributes.Copy(
                            arrayAttributesFunMap, arrayAttributesDefaultFun) }, 
                });
        }


        public PolyMesh TriangulatedCopy(double absoluteEpsilon = 1e-6)
        {
            var normalArray = ComputeFaceNormalArray(warn: false);

            var polyBackMap = new List<int>();
            var polyList = m_positionArray.ComputeNonConcaveSubPolygons(
                                m_faceCount, normalArray, m_firstIndexArray,
                                m_vertexIndexArray, polyBackMap, absoluteEpsilon);

            int fc = polyList.Sum(p => p.Length - 2);
            var fia = new int[fc + 1].SetByIndex(i => i * 3);
            var tia = new int[3 * fc];
            var fibm = new int[fc];
            int ti = 0;
            int tvi = 0;
            var vibm = new int[3 * fc];
            var pc = polyList.Count;
            for (int pi = 0; pi < pc; pi++)
            {
                var pvibm = polyList[pi];
                var pvia = pvibm.Map(fvi => m_vertexIndexArray[fvi]);
                var ptc = m_positionArray.ComputeTriangulationOfNonConcavePolygon(
                                        pvia, absoluteEpsilon, tvi, tia, pvibm, vibm);
                tvi += 3 * ptc;
                var ofi = polyBackMap[pi];
                while (ptc-- > 0) fibm[ti++] = ofi;
            }
            var m = Copy(OverridesNoTopology);

            m.FirstIndexArray = fia;
            m.VertexIndexArray = tia;

            var fa = FaceAttributes.Copy();
            var fChanges = from a in m_faceAttributes
                           where a.Key.IsPositive
                           select KeyValuePairs.Create(a.Key, a.Value.BackMappedCopy(fibm));
            foreach (var c in fChanges) fa[c.Key] = c.Value;
            m.FaceAttributes = fa;

            var fva = FaceVertexAttributes.Copy();
            var fvChanges = from a in m_faceVertexAttributes
                            where a.Key.IsPositive
                            select KeyValuePairs.Create(a.Key, a.Value.BackMappedCopy(vibm));
            foreach (var c in fvChanges) fva[c.Key] = c.Value;
            m.FaceVertexAttributes = fva;

            return m;
        }

        #endregion

        #region Various Methods

        public IEnumerable<int> NonDegenerateFaceIndices(
                bool compareVertices = true, bool checkNormals = true)
        {
            int fc = FaceCount;
            int[] fia = FirstIndexArray, via = VertexIndexArray;

            // check if mesh is valid
            if (fc == 0 || fia == null || via == null) yield break;

            V3d[] na = FaceAttributes.GetOrDefault(Property.Normals) as V3d[];
            if (checkNormals && na == null) na = ComputeFaceNormalArray(false);
            var pa = PositionArray;
            for (int fvi = fia[0], fi = 0; fi < fc; fi++)
            {
                if (checkNormals && na[fi].LengthSquared == 0)
                {
                    fvi = fia[fi + 1]; continue;
                }
                int vi = via[fvi++], vi0 = vi;
                bool good = true;
                for (int fve = fia[fi + 1]; fvi < fve; fvi++)
                {
                    int vi1 = via[fvi];
                    if (vi0 == vi1 || (compareVertices && pa[vi0] == pa[vi1]))
                        { good = false; fvi = fve; break; }
                    vi0 = vi1;
                }
                if (good && vi != vi0 && (!compareVertices || pa[vi] != pa[vi0]))
                    yield return fi;
            }
        }

        public Box3d BoundingBoxOfFace(int faceIndex)
        {
            int[] fia = FirstIndexArray, via = VertexIndexArray;
            int fvi = fia[faceIndex], fve = fia[faceIndex + 1];
            var pa = PositionArray;
            var box = new Box3d(pa[via[fvi++]]);
            while (fvi < fve) box.ExtendBy(pa[via[fvi++]]);
            return box;
        }

        /// <summary>
        /// Removes all topology information from the mesh.
        /// consider using: WithNewTopology / WithoutTopology
        /// </summary>
        public void ClearTopology()
        {
            m_edgeCount = 0;
            m_borderEdgeCount = 0;
            m_nonManifoldEdgeCount = 0;
            m_faceEdgeRefArray = null;
            m_edgeFaceRefArray = null;
            m_eulerCharacteristic = int.MinValue;
            this[Property.EulerCharacteristic] = int.MinValue;
        }

        /// <summary>
        /// Analyze the mesh with respect to components, without taking non-
        /// manifold edges into account. Returns the number of components,
        /// and the component index index of each face.
        /// </summary>
        public uint Analyze(out uint[] faceComponentIndexArray)
        {
            var cia = new uint[m_faceCount].Set(uint.MaxValue);
            var cc = Analyze(cia, null);
            faceComponentIndexArray = cia;
            return cc;
        }

        /// <summary>
        /// Analyze the mesh with respect to components, taking non-manifold
        /// edges into account. Returns the number of components, the
        /// component index index of each face, and a bool for each face
        /// if it is flipped with respect to the first face of the component
        /// (first face being the one with the lowest face index).
        /// NOTE: Mesh requires valid topology.
        /// </summary>
        public uint Analyze(out uint[] faceComponentIndexArray,
                out bool[] faceReversedArray)
        {
            if (!HasTopology) throw new Exception("Analyze requires topology");
            var fcia = new uint[m_faceCount].Set(uint.MaxValue);
            var ffa = new bool[m_faceCount];
            var cc = Analyze(fcia, ffa);
            faceComponentIndexArray = fcia;
            faceReversedArray = ffa;
            return cc;
        }

        private struct EdgeRefReversion
        {
            public int EdgeRef;
            public bool Reversion;

            public EdgeRefReversion(int edgeRef, bool reversion)
            {
                EdgeRef = edgeRef; Reversion = reversion;
            }
        }

        private uint Analyze(uint[] fcia, bool[] ffa)
        {
            uint cc = 0;
            var queue = new Queue<EdgeRefReversion>();
            for (int fi = 0; fi < m_faceCount; fi++)
            {
                if (fcia[fi] < uint.MaxValue) continue;
                if (ffa != null) ffa[fi] = false;
                fcia[fi] = cc;
                var fec = EdgeCountOfFace(0);
                for (int fs = 0; fs < fec; fs++)
                    queue.Enqueue(new EdgeRefReversion(EdgeRefOfFace(fi, fs), false));
                while (queue.Count > 0)
                {
                    var erf = queue.Dequeue();
                    var fr = FaceRef_One_OfEdgeRef(erf.EdgeRef);
                    if (fr.Index < 0)
                    {
                        if (ffa == null || !fr.IsNonManifoldEdgeRef) continue;
                        bool flip;
                        var er = fr.NonManifoldEdgeRef(out flip);
                        if (!flip) continue;
                        erf.Reversion = !erf.Reversion;
                        fr = FaceRef_One_OfEdgeRef(er);
                    }
                    if (fcia[fr.Index] < uint.MaxValue) continue;
                    if (ffa != null) ffa[fr.Index] = erf.Reversion;
                    fcia[fr.Index] = cc;
                    var nfec = EdgeCountOfFaceRef(fr);
                    for (int fs = 1; fs < nfec; fs++)
                        queue.Enqueue(new EdgeRefReversion(EdgeRefOfFaceRef(fr, fs), erf.Reversion));
                }
                ++cc;
            }
            return cc;
        }

        public int[] TriangleIndicesFromNonConcavePolygons(double absoluteEps)
        {
            return TriangleIndicesFromNonConcavePolygons(
                    FirstIndexArray, VertexIndexArray, PositionArray,
                    FaceCount, VertexIndexCount, absoluteEps);
        }

        public static int[] TriangleIndicesFromNonConcavePolygons(
                int[] firstIndexArray, int[] vertexIndexArray, V3d[] positionArray,
                int faceCount, int vertexIndexCount, double absoluteEps)
        {
            var tc = vertexIndexCount - 2 * faceCount;
            var tia = new int[3 * tc];
            var ftvi = 0;
            for (int fi = 0; fi < faceCount; fi++)
            {
                var fvi = firstIndexArray[fi];
                var vc = firstIndexArray[fi + 1] - fvi;
                var indices = new int[vc].SetByIndex(i => vertexIndexArray[fvi + i]);
                var ptc = positionArray.ComputeTriangulationOfNonConcavePolygon(
                                            indices, absoluteEps, ftvi, tia);
                ftvi += 3 * ptc;
            }

            return tia;
        }

        /// <summary>
        /// Create a vertex index array for a simple triangulation based
        /// on the first vertex of each polygon. This obviously only works
        /// for convex polygons.
        /// </summary>
        public int[] CreateSimpleTriangleVertexIndexArray()
        {
            return CreateSimpleTriangleVertexIndexArray(
                        m_firstIndexArray, m_vertexIndexArray,
                        m_faceCount, m_vertexIndexCount, m_faceVertexCountRange);
        }

        public static int[] CreateSimpleTriangleVertexIndexArray(
                int[] firstIndexArray, int[] vertexIndexArray,
                int faceCount, int vertexIndexCount, Range1i faceVertexCountRange)
        {
            var via = vertexIndexArray;
            if (faceVertexCountRange.Min == 3
                && faceVertexCountRange.Max == 3) return via;
            int tc = vertexIndexCount - 2 * faceCount;
            var tvia = new int[3 * tc];
            var fia = firstIndexArray;
            int ti3 = 0;
            int fvi0 = fia[0];
            for (int fi1 = 1; fi1 <= faceCount; fi1++)
            {
                var fvi1 = fia[fi1];
                var fvc = fvi1 - fvi0;
                var vi0 = via[fvi0];

                for (int i = 1; i < fvc - 1; i++) // build triangle fan
                {
                    tvia[ti3++] = vi0;
                    tvia[ti3++] = via[fvi0 + i];
                    tvia[ti3++] = via[fvi0 + i + 1];
                }
                fvi0 = fvi1;
            }

            return tvia;
        }

        public PolyMesh WithCompactedVertices => SubSetOfFaces(FaceCount.Range());

        public PolyMesh SubSetOfFaces(
                IEnumerable<int> faceIndices,
                bool compactVertices = true)
        {
            var fBackMap = faceIndices.ToArray();
            var fc = fBackMap.Length;
            var ovc = VertexCount;
            int[] vBackMap = null;
            Func<int, int> vMapFun;

            var fia = new int[fc + 1];
            var vic = 0;
            if (compactVertices)
            {
                int[] vForwardMap = new int[ovc].Set(-1);
                var vc = 0;
                for (int fi = 0; fi < fc; fi++)
                {
                    var ofi = fBackMap[fi];
                    fia[fi] = vic;
                    var fvc = VertexCountOfFace(ofi);
                    vic += fvc;
                    for (int fs = 0; fs < fvc; fs++)
                        vForwardMap.ForwardMapAdd(
                                VertexIndexOfFace(ofi, fs), ref vc);
                }
                vBackMap = vForwardMap.CreateBackMap(vc);
                vMapFun = i => vForwardMap[i];
            }
            else
            {
                for (int fi = 0; fi < fc; fi++)
                {
                    fia[fi] = vic;
                    vic += VertexCountOfFace(fBackMap[fi]);
                }
                vMapFun = i => i;
            }
            fia[fc] = vic;

            var ofia = FirstIndexArray;
            var ovia = VertexIndexArray;
            var via = new int[vic];
            var fvBackMap = new int[vic];
            for (int fvi = fia[0], fi = 0; fi < fc; fi++)
            {
                int ofvi = ofia[fBackMap[fi]];
                for (int fve = fia[fi + 1]; fvi < fve; fvi++)
                {
                    via[fvi] = vMapFun(ovia[ofvi]);
                    fvBackMap[fvi] = ofvi++;
                }
            }

            var m = new PolyMesh()
            {
                FirstIndexArray = fia,
                VertexIndexArray = via,
                VertexAttributes = compactVertices
                        ? VertexAttributes.Copy(
                                (n, a) => n.IsNegative ? a : a.BackMappedCopy(vBackMap))
                        : VertexAttributes.Copy(),
                FaceAttributes = FaceAttributes.Copy(
                        (n, a) => n.IsNegative ? a : a.BackMappedCopy(fBackMap)),
                FaceVertexAttributes = FaceVertexAttributes.Copy(
                        (n, a) => n.IsNegative ? a : a.BackMappedCopy(fvBackMap)),
                               
                InstanceAttributes = InstanceAttributes.Copy(),
            };
            return m;
        }

        #endregion

        #region IAttributes

        public partial interface IAttribute
        {
            Symbol Name { get; }
            int[] IndexArray { get; }
            Array ValueArray { get; }
            Type ValueType { get; }

            bool EqualValues(int i0, int i1);
            Array BackMappedCopy(int[] backMap, int count);
            Array BackMappedConvertedCopy(int[] backMap, int count, bool floatVecs, bool byteCols);
            void BackMappedCopyTo(int[] backMap, Array target, int offset);
            void ForwardMappedCopyTo(int[] forwardMap, Array target, int offset);
            void BackMappedGroupCopyTo(int[] groupBackMap, int groupCount,
                    int[] fia, Array target, int offset);    
        }

        public partial struct Attribute<T> : IAttribute
        {
            public readonly T[] TypedValueArray;
            public readonly Func<double, T, T, T> Interpolator;

            public Attribute(Symbol name, int[] indexArray,
                    T[] typedValueArray, Func<double, T, T, T> interpolator)
            {
                Name = name;
                IndexArray = indexArray;
                TypedValueArray = typedValueArray;
                Interpolator = interpolator;
            }

            #region IAttribute Members

            public Symbol Name { get; }
            public int[] IndexArray { get; }
            public Array ValueArray { get { return TypedValueArray; } }
            public Type ValueType { get { return typeof(T); } }

            public bool EqualValues(int i0, int i1)
            {
                if (IndexArray != null)
                {
                    i0 = IndexArray[i0];
                    i1 = IndexArray[i1];
                    if (i0 == i1) return true;
                }
                return TypedValueArray[i0].Equals(TypedValueArray[i1]);
            }

            public Array BackMappedCopy(int[] backMap, int count)
            {
                return TypedValueArray.BackMappedCopy(IndexArray, backMap, count);
            }

            public Array BackMappedConvertedCopy(int[] backMap, int count, bool floatVecs, bool byteCols)
            {
                Func<Array, int[], int[], int, bool, bool, Array> fun;
                if (s_backMappedConvertedCopyFunMap.TryGetValue(ValueArray.GetType(), out fun))
                    return fun(ValueArray, IndexArray, backMap, count, floatVecs, byteCols);
                return null;
            }

            public void BackMappedCopyTo(int[] backMap, Array target, int offset)
            {
                TypedValueArray.BackMappedCopyTo((T[])target, backMap, offset);
            }

            public void ForwardMappedCopyTo(int[] forwardMap, Array target, int offset)
            {
                TypedValueArray.ForwardMappedCopyTo((T[])target, forwardMap, offset);
            }

            public void BackMappedGroupCopyTo(int[] groupBackMap, int groupCount,
                    int[] fia, Array target, int offset)
            {
                TypedValueArray.BackMappedGroupCopyTo(groupBackMap, groupCount, fia,
                            (T[])target, offset);
            }

            #endregion
        }

        public static Dictionary<Type, Func<Symbol, int[], Array, IAttribute>> s_iAttributeMap =
            new Dictionary<Type, Func<Symbol, int[], Array, IAttribute>>
            {
                { typeof(double[]),
                    (n, i, a) => new Attribute<double>(n, i, (double[])a, Fun.Lerp) },
                { typeof(float[]),
                    (n, i, a) => new Attribute<float>(n, i, (float[])a, Fun.Lerp) },
                { typeof(C3b[]),
                    (n, i, a) => new Attribute<C3b>(n, i, (C3b[])a, ColFun.Lerp) },
                { typeof(C3f[]),
                    (n, i, a) => new Attribute<C3f>(n, i, (C3f[])a, ColFun.Lerp) },
                { typeof(C4b[]),
                    (n, i, a) => new Attribute<C4b>(n, i, (C4b[])a, ColFun.Lerp) },
                { typeof(C4f[]),
                    (n, i, a) => new Attribute<C4f>(n, i, (C4f[])a, ColFun.Lerp) },
                { typeof(V2d[]),
                    (n, i, a) => new Attribute<V2d>(n, i, (V2d[])a, VecFun.Lerp) },
                { typeof(V2f[]),
                    (n, i, a) => new Attribute<V2f>(n, i, (V2f[])a, VecFun.Lerp) },
                { typeof(V3d[]),
                    (n, i, a) => n != Property.Normals
                        ? new Attribute<V3d>(n, i, (V3d[])a, VecFun.Lerp)
                        : new Attribute<V3d>(n, i, (V3d[])a, (t, v0, v1) => t.Lerp(v0, v1).Normalized) },
                { typeof(V3f[]),
                    (n, i, a) => n != Property.Normals
                        ? new Attribute<V3f>(n, i, (V3f[])a, VecFun.Lerp)
                        : new Attribute<V3f>(n, i, (V3f[])a, (t, v0, v1) => t.Lerp(v0, v1).Normalized) },
                { typeof(V4d[]),
                    (n, i, a) => new Attribute<V4d>(n, i, (V4d[])a, VecFun.Lerp) },
                { typeof(V4f[]),
                    (n, i, a) => new Attribute<V4f>(n, i, (V4f[])a, VecFun.Lerp) },
                { typeof(byte[]),
                    (n, i, a) => new Attribute<byte>(n, i, (byte[])a, Fun.Step) },
                { typeof(sbyte[]),
                    (n, i, a) => new Attribute<sbyte>(n, i, (sbyte[])a, Fun.Step) },
                { typeof(short[]),
                    (n, i, a) => new Attribute<short>(n, i, (short[])a, Fun.Step) },
                { typeof(ushort[]),
                    (n, i, a) => new Attribute<ushort>(n, i, (ushort[])a, Fun.Step) },
                { typeof(int[]),
                    (n, i, a) => new Attribute<int>(n, i, (int[])a, Fun.Step) },
                { typeof(uint[]),
                    (n, i, a) => new Attribute<uint>(n, i, (uint[])a, Fun.Step) },
                { typeof(char[]),
                    (n, i, a) => new Attribute<char>(n, i, (char[])a, Fun.Step) },
                { typeof(string[]),
                    (n, i, a) => new Attribute<string>(n, i, (string[])a, Fun.Step) },
                { typeof(WindingOrder[]),
                    (n, i, a) => new Attribute<WindingOrder>(n, i, (WindingOrder[])a, Fun.Step)},
            };

        private static object s_iAttributeMapLock = new object();

        private static Func<Symbol, int[], Array, IAttribute> GetIAttributeCreator(Type arrayType)
        {
            if (s_iAttributeMap.TryGetValue(arrayType, out Func<Symbol, int[], Array, IAttribute> creator)) return creator;
            return null;
        }

        public static void RegisterAttributeCreators(IEnumerable<KeyValuePair<Type, Func<Symbol, int[], Array, IAttribute>>> creators)
        {
            s_iAttributeMap.AddRange(creators);
        }

        public static IAttribute GetIAttribute(Symbol name, SymbolDict<Array> attributes)
        {
            if (!name.IsPositive) throw new InvalidOperationException();
            if (attributes.TryGetValue(name, out Array array))
            {
                if (attributes.TryGetValue(-name, out Array items))
                {
                    var creator = GetIAttributeCreator(items.GetType());
                    if (creator != null) return creator(name, (int[])array, items);
                }
                else
                {
                    var creator = GetIAttributeCreator(array.GetType());
                    if (creator != null) return creator(name, null, array);
                }
            }
            return default(IAttribute);
        }

        private static Dictionary<Type, Func<Array, int[], int[], int, bool, bool, Array>>
                s_backMappedConvertedCopyFunMap =
            new Dictionary<Type, Func<Array, int[], int[], int, bool, bool, Array>>
            {
                { typeof(byte[]), (a,i,m,c,f,b) => ((byte[])a).BackMappedCopy(i, m, c) },
                { typeof(sbyte[]), (a,i,m,c,f,b) => ((sbyte[])a).BackMappedCopy(i, m, c) },
                { typeof(short[]), (a,i,m,c,f,b) => ((short[])a).BackMappedCopy(i, m, c) },
                { typeof(ushort[]), (a,i,m,c,f,b) => ((ushort[])a).BackMappedCopy(i, m, c) },
                { typeof(int[]), (a,i,m,c,f,b) => ((int[])a).BackMappedCopy(i, m, c) },
                { typeof(uint[]), (a,i,m,c,f,b) => ((uint[])a).BackMappedCopy(i, m, c) },
                { typeof(float[]), (a,i,m,c,f,b) => ((float[])a).BackMappedCopy(i, m, c) },
                { typeof(double[]), (a,i,m,c,f,b) => f
                                        ? (Array)((double[])a).BackMappedCopy(i, m, d => (float)d, c)
                                        : (Array)((double[])a).BackMappedCopy(i, m, c) },

                { typeof(V2f[]), (a,i,m,c,f,b) => ((V2f[])a).BackMappedCopy(i, m, c) },
                { typeof(V3f[]), (a,i,m,c,f,b) => ((V3f[])a).BackMappedCopy(i, m, c) },
                { typeof(V4f[]), (a,i,m,c,f,b) => ((V4f[])a).BackMappedCopy(i, m, c) },

                { typeof(V2d[]), (a,i,m,c,f,b) => f
                                        ? (Array)((V2d[])a).BackMappedCopy(i, m, V2f.FromV2d, c)
                                        : (Array)((V2d[])a).BackMappedCopy(i, m, c) },
                { typeof(V3d[]), (a,i,m,c,f,b) => f
                                        ? (Array)((V3d[])a).BackMappedCopy(i, m, V3f.FromV3d, c)
                                        : (Array)((V3d[])a).BackMappedCopy(i, m, c) },
                { typeof(V4d[]), (a,i,m,c,f,b) => f
                                        ? (Array)((V4d[])a).BackMappedCopy(i, m, V4f.FromV4d, c)
                                        : (Array)((V4d[])a).BackMappedCopy(i, m, c) },

                { typeof(C3b[]), (a,i,m,c,f,b) => ((C3b[])a).BackMappedCopy(i, m, c) },
                { typeof(C4b[]), (a,i,m,c,f,b) => ((C4b[])a).BackMappedCopy(i, m, c) },
                { typeof(C3us[]), (a,i,m,c,f,b) => b
                                        ? (Array)((C3us[])a).BackMappedCopy(i, m, C3b.FromC3us, c)
                                        : (Array)((C3us[])a).BackMappedCopy(i, m, c) },
                { typeof(C4us[]), (a,i,m,c,f,b) => b
                                        ? (Array)((C4us[])a).BackMappedCopy(i, m, C4b.FromC4us, c)
                                        : (Array)((C4us[])a).BackMappedCopy(i, m, c) },
                { typeof(C3f[]), (a,i,m,c,f,b) => b
                                        ? (Array)((C3f[])a).BackMappedCopy(i, m, C3b.FromC3f, c)
                                        : (Array)((C3f[])a).BackMappedCopy(i, m, c) },
                { typeof(C4f[]), (a,i,m,c,f,b) => b
                                        ? (Array)((C4f[])a).BackMappedCopy(i, m, C4b.FromC4f, c)
                                        : (Array)((C4f[])a).BackMappedCopy(i, m, c) },
            };

        private bool EqualAttributes(
                int faceIndex0, int faceSide0,
                int faceIndex1, int faceSide1,
                IAttribute[] faceAttributes,
                IAttribute[] faceVertexAttributes)
        {
            foreach (var fa in faceAttributes)
                if (!fa.EqualValues(faceIndex0, faceIndex1)) return false;
            var fia = m_firstIndexArray;
            foreach (var fva in faceVertexAttributes)
            {
                int fvi0 = fia[faceIndex0] + faceSide0;
                int fvi1 = fia[faceIndex1] + faceSide1;
                if (!fva.EqualValues(fvi0, fvi1)) return false;
            }
            return true;
        }

        #endregion

        #region GetGeometry

        [Flags]
        public enum GetGeometryOptions
        {
            None = 0x00000000,
            FloatVectors = 0x00000001,  // alternative: double vectors
            ByteColors = 0x00000002,    // alternative: float colors
            IntIndices = 0x00000004,    // alternative: short indices
            FloatVectorsAndByteColors = FloatVectors | ByteColors | IntIndices,

            Compact = 0x00000008,

            Default = FloatVectorsAndByteColors | Compact
            // StoreDefName = 0x00100000,
        }

        public PolyMesh PerVertexAttributedCopy(bool compact = true)
        {
            var faceIndices = FaceCount.Range();
            var faceAttributes = FaceAttributes.Keys;
            var vertexAttributes = VertexAttributes.Keys;
            var faceVertexAttributes = FaceVertexAttributes.Keys;

            return PerVertexAttributedCopy(
                    faceIndices, compact, faceAttributes, vertexAttributes, faceVertexAttributes);
        }

        public PolyMesh PerVertexAttributedCopy(
                    IEnumerable<int> faceIndices,
                    bool compact,
                    IEnumerable<Symbol> faceAttributes,
                    IEnumerable<Symbol> vertexAttributes,
                    IEnumerable<Symbol> faceVertexAttributes)
        {
            var faceBackMap = faceIndices.ToArray();
            var faceAttributeArray
                    = (from a in faceAttributes
                       select GetIAttribute(a, m_faceAttributes)).ToArray();
            var vertexAttributeArray
                    = (from a in vertexAttributes
                       select GetIAttribute(a, m_vertexAttributes)).ToArray();
            var faceVertexAttributeArray
                    = (from a in faceVertexAttributes
                       select GetIAttribute(a, m_faceVertexAttributes)).ToArray();

            return PerVertexAttributedCopy(
                        faceBackMap,
                        compact,
                        faceAttributeArray,
                        vertexAttributeArray,
                        faceVertexAttributeArray);
        }

        public PolyMesh PerVertexAttributedCopy(
                    int[] faceBackMap,
                    bool compact,
                    IAttribute[] faceAttributes,
                    IAttribute[] vertexAttributes,
                    IAttribute[] faceVertexAttributes)
        {
            ComputeVertexBackMaps(faceBackMap, compact,
                    faceAttributes, vertexAttributes, faceVertexAttributes,
                    out int[] firstIndexArray, out int[] vertexIndexArray,
                    out int[] vBackMap, out int[] vfBackMap, out int[] vfvBackMap);

            var vaDict = new SymbolDict<Array>();

            var vc = vBackMap.Length;

            foreach (var a in vertexAttributes)
                vaDict[a.Name] = a.BackMappedCopy(vBackMap, vc);

            foreach (var a in faceAttributes)
                vaDict[a.Name] = a.BackMappedCopy(vfBackMap, vc);

            foreach (var a in faceVertexAttributes)
                vaDict[a.Name] = a.BackMappedCopy(vfvBackMap, vc);

            var m = new PolyMesh();
            m.FirstIndexArray = firstIndexArray;
            m.VertexIndexArray = vertexIndexArray;
            m.VertexAttributes = vaDict;
            return m;
        }

        public void ComputeVertexBackMaps(
                    int[] faceBackMap,
                    bool compact,
                    IAttribute[] faceAttributes,
                    IAttribute[] vertexAttributes,
                    IAttribute[] faceVertexAttributes,
                    out int[] firstIndexArray,
                    out int[] vertexIndexArray,
                    out int[] vertexBackMap,
                    out int[] vertexFaceBackMap,
                    out int[] vertexFaceVertexBackMap
                    )
        {
            var newFaceCount = faceBackMap.Length;
            var newVertexIndexCount = 0;
            var newVertexCount = 0;
            var vertexForwardMap = new int[VertexCount].Set(-1);
            foreach (var fi in faceBackMap)
            {
                var fvc = VertexCountOfFace(fi);
                newVertexIndexCount += fvc;
                for (int fs = 0; fs < fvc; fs++)
                    vertexForwardMap.ForwardMapAdd(VertexIndexOfFace(fi, fs), ref newVertexCount);
            }

            var fia = new int[newFaceCount + 1];
            var via = new int[newVertexIndexCount];
            fia[0] = 0;

            var vfBackMap = new int[newVertexIndexCount].Set(-1);
            var vfvBackMap = new int[newVertexIndexCount].Set(-1);
            var nextVertexIndex = new int[newVertexIndexCount].Set(-1);
            var attributeCount = newVertexCount;
            var newVertexIndexIndex = 0;
            var newFaceIndex = 0;

            foreach (var fi in faceBackMap)
            {
                var fvc = VertexCountOfFace(fi);
                for (int fs = 0; fs < fvc; fs++)
                {
                    var vi = VertexIndexOfFace(fi, fs);
                    var nvi = vertexForwardMap[vi];
                    var pvi = -1;
                    if (nvi < 0 || nvi >= newVertexIndexCount)
                        Report.Warn("out of range"); // breakpoint for debugging

                    while (vfBackMap[nvi] >= 0)
                    {
                        if (compact
                            && EqualAttributes(fi, fs, vfBackMap[nvi], vfvBackMap[nvi],
                                    faceAttributes, faceVertexAttributes))
                            break;
                        pvi = nvi; nvi = nextVertexIndex[nvi];
                        if (nvi < 0) nvi = attributeCount++;
                        if (nvi < 0 || nvi >= vfBackMap.Length)
                            Report.Warn("out of range"); // breakpoint for debugging
                    }
                    if (vfBackMap[nvi] < 0)
                    {
                        if (pvi >= 0) nextVertexIndex[pvi] = nvi;
                        vfBackMap[nvi] = fi;
                        vfvBackMap[nvi] = fs;
                    }
                    via[newVertexIndexIndex++] = nvi;
                }
                fia[++newFaceIndex] = newVertexIndexIndex;
            }

            var vBackMap = vertexForwardMap.CreateBackMap(attributeCount);

            for (int vi = 0; vi < newVertexCount; vi++)
            {
                var nvi = nextVertexIndex[vi];
                if (nvi >= 0)
                {
                    var ovi = vBackMap[vi];
                    do
                    {
                        vBackMap[nvi] = ovi;
                        nvi = nextVertexIndex[nvi];
                    }
                    while (nvi >= 0);
                }
            }

            for (int vi = 0; vi < attributeCount; vi++)
                vfvBackMap[vi] += m_firstIndexArray[vfBackMap[vi]];

            firstIndexArray = fia;
            vertexIndexArray = via;
            vertexBackMap = vBackMap;
            vertexFaceBackMap = vfBackMap;
            vertexFaceVertexBackMap = vfvBackMap;
        }

        #endregion

        #region IAwakeable Members

        private void Awake(bool buildTopology)
        {
            var vertexCount = Get<int>(Property.VertexCount); // 

            m_firstIndexArray = Get<int[]>(Property.FirstIndexArray);
            m_vertexIndexArray = Get<int[]>(Property.VertexIndexArray);
            m_faceCount = Get<int>(Property.FaceCount);
            m_vertexIndexCount = Get<int>(Property.VertexIndexCount);
            m_faceVertexCountRange = Get<Range1i>(Property.FaceVertexCountRange);

            m_vertexAttributes = Get<SymbolDict<Array>>(Property.VertexAttributes);
            m_faceAttributes = Get<SymbolDict<Array>>(Property.FaceAttributes);
            m_faceVertexAttributes = Get<SymbolDict<Array>>(Property.FaceVertexAttributes);
            m_edgeAttributes = Get<SymbolDict<Array>>(Property.EdgeAttributes);
            m_instanceAttributes = Get<SymbolDict<object>>(Property.InstanceAttributes);

            // use property assignment to initialize dependent fields
            PositionArray = m_vertexAttributes.GetOrDefault(Property.Positions) as V3d[];
            m_normalArray = m_vertexAttributes.GetOrDefault(Property.Normals) as V3d[];
            m_colorArray = m_vertexAttributes.GetOrDefault(Property.Colors) as C4f[];

            // set vertex count after position array, to correctly handle
            // meshes with VertexCapacity != VertexCount
            VertexCount = vertexCount;

            m_eulerCharacteristic = Get<int>(Property.EulerCharacteristic);

            // we do not store the topology, we rebuild it,
            // since building is faster than reading from disk!
            if (buildTopology && m_eulerCharacteristic != int.MinValue)
                BuildTopology(m_eulerCharacteristic);
        }

        public void Awake(int codedVersion) => Awake(true);

        #endregion

        #region IBoundingBox3d Members

        public Box3d BoundingBox3d => VertexIndexArray.GetBoundingBox3d(VertexIndexCount, PositionArray);

        #endregion

        #region Internal Topology Methods

        internal int EdgeCountOfFaceRef(FaceRef faceRef)
        {
            var fia = m_firstIndexArray;
            return fia[faceRef.Index + 1] - fia[faceRef.Index];
        }

        internal int VertexIndexOfFace(int faceIndex, int faceSide)
        {
            return m_vertexIndexArray[m_firstIndexArray[faceIndex] + faceSide];
        }

        internal int VertexIndex_MinusOne_OfFaceRef(FaceRef faceRef)
        {
            int fs = faceRef.Side - 1;
            if (fs < 0)
                return m_vertexIndexArray[m_firstIndexArray[faceRef.Index + 1] + fs];
            else
                return m_vertexIndexArray[m_firstIndexArray[faceRef.Index] + fs];
        }

        internal int VertexIndex_Zero_OfFaceRef(FaceRef faceRef)
        {
            return m_vertexIndexArray[
                        m_firstIndexArray[faceRef.Index]
                        + faceRef.Side];
        }

        internal int VertexIndex_PlusOne_OfFaceRef(FaceRef faceRef)
        {
            var fia = m_firstIndexArray;
            int fvi = fia[faceRef.Index], fs = faceRef.Side + 1;
            if (fs == fia[faceRef.Index + 1] - fvi) fs = 0;
            return m_vertexIndexArray[fvi + fs];
        }

        internal int VertexIndexOfFaceRef(FaceRef faceRef, int side)
        {
            var fia = m_firstIndexArray;
            int fvi = fia[faceRef.Index], fvc = fia[faceRef.Index + 1] - fvi;
            if (side < 0) side = side % fvc + fvc;
            return m_vertexIndexArray[fvi + (faceRef.Side + side) % fvc];
        }

        internal int EdgeRef_MinusOne_OfFaceRef(FaceRef faceRef)
        {
            var fs = faceRef.Side - 1;
            if (fs < 0)
                return m_faceEdgeRefArray[m_firstIndexArray[faceRef.Index + 1] + fs];
            else
                return m_faceEdgeRefArray[m_firstIndexArray[faceRef.Index] + fs];
        }

        internal int EdgeRef_Zero_OfFaceRef(FaceRef faceRef)
        {
            return m_faceEdgeRefArray[
                        m_firstIndexArray[faceRef.Index]
                        + faceRef.Side];
        }

        internal int EdgeRef_PlusOne_OfFaceRef(FaceRef faceRef)
        {
            var fia = m_firstIndexArray;
            int fvi = fia[faceRef.Index], fs = faceRef.Side + 1;
            if (fs == fia[faceRef.Index + 1] - fvi) fs = 0;
            return m_faceEdgeRefArray[fvi + fs];
        }

        internal int EdgeRef_MinusOne_OfFace(int faceIndex, int faceSide)
        {
            var fs = faceSide - 1;
            if (fs < 0) faceIndex += 1;
            return m_faceEdgeRefArray[m_firstIndexArray[faceIndex] + fs];
        }

        internal int EdgeRef_Zero_OfFace(int faceIndex, int faceSide)
        {
            return m_faceEdgeRefArray[m_firstIndexArray[faceIndex]
                                      + faceSide];
        }

        internal int EdgeRef_PlusOne_OfFace(int faceIndex, int faceSide)
        {
            var fia = m_firstIndexArray;
            int fvi = fia[faceIndex], fs = faceSide + 1;
            if (fs == fia[faceIndex + 1] - fvi) fs = 0;
            return m_faceEdgeRefArray[fvi + fs];
        }

        internal int EdgeRefOfFaceRef(FaceRef faceRef, int side)
        {
            var fia = m_firstIndexArray;
            int fvi = fia[faceRef.Index], fvc = fia[faceRef.Index + 1] - fvi;
            if (side < 0) side = side % fvc + fvc;
            return m_faceEdgeRefArray[fvi + (faceRef.Side + side) % fvc];
        }

        internal int EdgeRefOfFace(int faceIndex, int faceSide)
        {
            var fia = m_firstIndexArray;
            int fvi = fia[faceIndex], fvc = fia[faceIndex + 1] - fvi;
            if (faceSide < 0) faceSide = faceSide % fvc + fvc;
            return m_faceEdgeRefArray[fvi + (faceSide + faceSide) % fvc];
        }

        internal FaceRef TurnedFaceRef(FaceRef faceRef, int turnCount)
        {
            var fia = m_firstIndexArray;
            int fvi = fia[faceRef.Index], fvc = fia[faceRef.Index + 1] - fvi;
            if (turnCount < 0) turnCount = turnCount % fvc + fvc;
            return new FaceRef(faceRef.Index, (faceRef.Side + turnCount) % fvc);
        }

        internal FaceRef FaceRef_Zero_OfEdgeRef(int edgeRef)
        {
            return m_edgeFaceRefArray[edgeRef];
        }

        internal FaceRef FaceRef_One_OfEdgeRef(int edgeRef)
        {
            return m_edgeFaceRefArray[edgeRef ^ 1];
        }

        internal FaceRef FaceRef_Zero_OfEdge(int edgeIndex, int edgeSide)
        {
            return m_edgeFaceRefArray[2 * edgeIndex + edgeSide];
        }

        internal FaceRef FaceRef_One_OfEdge(int edgeIndex, int edgeSide)
        {
            return m_edgeFaceRefArray[2 * edgeIndex + edgeSide ^ 1];
        }

        internal int EdgeRef_Zero_OfVertex(int vertexIndex)
        {
            return EdgeRef.Create(vertexIndex, 1);
        }

        /// <summary>
        /// Given a non-manifold edge ref, return the next accessible non-
        /// manifold edge ref. Sets the out parameter flipped to true, if the
        /// returned non-manifold edge ref is flipped with respect to the
        /// supplied non-manifold edge ref.
        /// </summary>
        internal int NextNonManifoldEdgeRef(int edgeRef, out bool flipped)
        {
            int er = -1 - m_edgeFaceRefArray[edgeRef | 1].Index;
            flipped = (er & 1) != 0;
            return (int)(er | 1);
        }

        #endregion

        #region Polygon

        /// <summary>
        /// A non-oriented facade structure for a single polygon.
        /// </summary>
        public struct Polygon : IBoundingBox3d, IEquatable<Polygon>
        {
            public readonly PolyMesh Mesh;
            public readonly int Index;

            #region Constructor

            public Polygon(PolyMesh mesh, int index)
            {
                Mesh = mesh;
                Index = index;
            }

            #endregion

            #region Properties

            public int VertexCount
            {
                get
                {
                    var fia = Mesh.FirstIndexArray;
                    return fia[Index + 1] - fia[Index];
                }
            }

            public int VertexIndex0
            {
                get
                {
                    return Mesh.VertexIndexArray[Mesh.FirstIndexArray[Index]];
                }
            }

            public IEnumerable<int> VertexIndices
            {
                get
                {
                    int[] fia = Mesh.FirstIndexArray, via = Mesh.VertexIndexArray;
                    int fvi = fia[Index], fve = fia[Index + 1];
                    while (fvi < fve) yield return via[fvi++];
                }
            }

            public IEnumerable<Line3d> Lines
            {
                get
                {
                    int[] fia = Mesh.FirstIndexArray, via = Mesh.VertexIndexArray;
                    int fvi = fia[Index], fve = fia[Index + 1];
                    var pa = Mesh.PositionArray;
                    var p = pa[via[fvi++]];
                    var p0 = p;
                    while (fvi < fve)
                    {
                        var p1 = pa[via[fvi++]];
                        yield return new Line3d(p0, p1);
                        p0 = p1;
                    }
                    yield return new Line3d(p0, p);
                }
            }

            public Face Face { get { return new Face(Mesh, Index, 0); } }

            public Polygon3d Polygon3d
            {
                get
                {
                    int[] fia = Mesh.FirstIndexArray, via = Mesh.VertexIndexArray;
                    int fvi = fia[Index], fvc = fia[Index + 1] - fvi;
                    var va = new V3d[fvc];
                    var pa = Mesh.PositionArray;
                    for (int i = 0; i < fvc; i++) va[i] = pa[via[fvi + i]];
                    return new Polygon3d(va);
                }
            }

            #endregion

            #region Operators

            public static bool operator ==(Polygon p0, Polygon p1)
            {
                return p0.Mesh == p1.Mesh && p0.Index == p1.Index;
            }

            public static bool operator !=(Polygon p0, Polygon p1)
            {
                return p0.Mesh != p1.Mesh || p0.Index != p1.Index;
            }

            #endregion

            #region Overrides

            public override int GetHashCode()
            {
                return HashCode.Combine(Mesh.GetHashCode(), Index);
            }

            public override bool Equals(object obj)
            {
                return obj is Polygon ? this == (Polygon)obj : false;
            }

            #endregion

            #region IBoundingBox3d Members

            public Box3d BoundingBox3d
            {
                get { return Mesh.BoundingBoxOfFace(Index); }
            }

            #endregion

            #region IEquatable<Polygon> Members

            public bool Equals(Polygon other)
            {
                return this == other;
            }

            #endregion
        }

        #endregion

        #region Vertex

        public struct Vertex : IEquatable<Vertex>
        {
            public readonly PolyMesh Mesh;
            public readonly int Index;

            #region Constructors

            public Vertex(PolyMesh mesh, int index)
            {
                Mesh = mesh;
                Index = index;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns an edge incident to the vertex. If the vertex is on a
            /// border, the returned edge is a border edge that allows
            /// circulating the vertex in counter-clockwise fashion.
            /// </summary>
            public Edge Edge0
            {
                get
                {
                    var m = Mesh;
                    return new Edge(m, m.EdgeRef_Zero_OfVertex(Index));
                }
            }

            /// <summary>
            /// Returns a face incident to the vertex. The following relation
            /// with respect to <see cref="Face0"/> holds: v.Face0 ==
            /// v.Edge0.OppositeFace. Also: v.Face0.Vertex(0) == v. 
            /// </summary>
            public Face Face0
            {
                get
                {
                    var m = Mesh;
                    return new Face(m, m.FaceRef_One_OfEdgeRef(
                                            m.EdgeRef_Zero_OfVertex(Index)));
                }
            }

            /// <summary>
            /// Returns all edges incident to the vertex in counter clock-
            /// wise order. If the vertex is on a border, e.IsBorder will
            /// be true for the first returned edge, and e.OppositeIsBorder
            /// will be true for the last returned edge.
            /// Note: This will only return edges conntected to faces and 
            ///       faces connected to them of the vertex edge (Edge0).
            ///       In the case of a single vertex connects two faces it
            ///       will only return edges of the face which has the
            ///       vertex edge.
            /// </summary>
            public IEnumerable<Edge> Edges
            {
                get
                {
                    var m = Mesh;
                    var erStart = m.EdgeRef_Zero_OfVertex(Index); // edge pointing to vertex
                    if (erStart < 0)
                        yield break;

                    var er = erStart;

                    // run clock wise until end or start
                    var frStart = m.FaceRef_One_OfEdgeRef(er);
                    var fr = frStart;
                    
                    // while there is a face connected to this edge on side 0
                    while (fr.Index >= 0)
                    {
                        yield return new Edge(m, er);

                        // test if edge in +1 on face is connected to this.Index
                        er = m.EdgeRef_MinusOne_OfFaceRef(fr);
                        if (!new Edge(m, er).IsConnectedToVertex(this.Index))
                        {
                            // incoherent winding order
                            er = m.EdgeRef_PlusOne_OfFaceRef(fr);
                            if (!new Edge(m, er).IsConnectedToVertex(this.Index))
                            {
                                Report.Line("fail+");
                                yield break;
                            }
                        }

                        // jump to other face over the edge
                        fr = m.FaceRef_One_OfEdgeRef(er);

                        if (fr == frStart) // ran a whole loop
                            yield break;
                    }

                    yield return new Edge(m, er); // last edge in this direction (border)

                    // ran into dead end
                    
                    er = erStart; // reset edge ref

                    // start with other face
                    frStart = m.FaceRef_Zero_OfEdgeRef(er);
                    fr = frStart;

                    while (fr.Index >= 0)
                    {
                        // jump to other face edge connected to this vertex
                        // test if edge in +1 on face is connected to this.Index
                        er = m.EdgeRef_PlusOne_OfFaceRef(fr);
                        if (!new Edge(m, er).IsConnectedToVertex(this.Index))
                        {
                            // incoherent winding order
                            er = m.EdgeRef_MinusOne_OfFaceRef(fr);
                            if (!new Edge(m, er).IsConnectedToVertex(this.Index))
                            {
                                Report.Line("fail-");
                                yield break;
                            }
                        }

                        yield return new Edge(m, er);

                        // jump to other face
                        fr = m.FaceRef_One_OfEdgeRef(er);

                        // should run into dead end like in the other direction
                    }
                }
            }

            /// <summary>
            /// Return all faces incident on a vertex in counter-clockwise
            /// order.
            /// </summary>
            public IEnumerable<Face> Faces
            {
                get
                {
                    var m = Mesh;
                    var erStart = m.EdgeRef_Zero_OfVertex(Index);
                    if (erStart < 0)
                        yield break;

                    var er = erStart;

                    // run clock wise until end or start
                    var frStart = m.FaceRef_One_OfEdgeRef(er);
                    var fr = frStart;       

                    // while there is a face connected to this edge on side 0
                    while (fr.Index >= 0)
                    {
                        yield return new Face(m, fr);

                        // test if edge in +1 on face is connected to this.Index
                        er = m.EdgeRef_MinusOne_OfFaceRef(fr);
                        if (!new Edge(m, er).IsConnectedToVertex(this.Index))
                        {
                            // incoherent winding order
                            er = m.EdgeRef_PlusOne_OfFaceRef(fr);
                            if (!new Edge(m, er).IsConnectedToVertex(this.Index))
                            {
                                Report.Line("fail+");
                                yield break;
                            }
                        }

                        // jump to other face
                        fr = m.FaceRef_One_OfEdgeRef(er);

                        if (fr == frStart)
                            yield break;
                    }

                    // ran into dead end

                    er = erStart;
                    
                    frStart = m.FaceRef_Zero_OfEdgeRef(er);
                    fr = frStart;

                    while (fr.Index >= 0)
                    {
                        yield return new Face(m, fr);

                        // test if edge in +1 on face is connected to this.Index
                        er = m.EdgeRef_PlusOne_OfFaceRef(fr);
                        if (!new Edge(m, er).IsConnectedToVertex(this.Index))
                        {
                            // incoherent winding order
                            er = m.EdgeRef_MinusOne_OfFaceRef(fr);
                            if (!new Edge(m, er).IsConnectedToVertex(this.Index))
                            {
                                Report.Line("fail-");
                                yield break;
                            }
                        }

                        // jump to other face
                        fr = m.FaceRef_One_OfEdgeRef(er);

                        // should run into dead end like in the other direction
                    }
                }
            }

            /// <summary>
            /// Return all neigbour vertices of the vertex in counter-
            /// clockwise order.
            /// </summary>
            public IEnumerable<Vertex> VerticesBuggy
            {
                get
                {
                    var m = Mesh;
                    var er = m.EdgeRef_Zero_OfVertex(Index);
                    if (er < 0)
                        yield break;
                    var fr = m.FaceRef_One_OfEdgeRef(er);
                    if (m.FaceRef_Zero_OfEdgeRef(er).Index < 0)
                    {
                        FaceRef ofr;
                        do
                        {
                            yield return new Vertex(m, m.VertexIndex_PlusOne_OfFaceRef(fr));
                            er = m.EdgeRef_MinusOne_OfFaceRef(fr);
                            ofr = fr; fr = m.FaceRef_One_OfEdgeRef(er);
                        }
                        while (fr.Index >= 0);
                        yield return new Vertex(m, m.VertexIndex_MinusOne_OfFaceRef(ofr));
                    }
                    else // first edge (and vertex) is inside 
                    {
                        var fr1 = fr;
                        do
                        {
                            yield return new Vertex(m, m.VertexIndex_PlusOne_OfFaceRef(fr));
                            er = m.EdgeRef_PlusOne_OfFaceRef(fr);
                            fr = m.FaceRef_One_OfEdgeRef(er);
                        }
                        while (fr != fr1);
                    }
                }
            }

            public V3d Position
            {
                get { return Mesh.PositionArray[Index]; }
                set { Mesh.PositionArray[Index] = value; }
            }

            public V3d Normal
            {
                get { return Mesh.NormalArray[Index]; }
                set { Mesh.NormalArray[Index] = value; }
            }

            public C4f Color
            {
                get { return Mesh.ColorArray[Index]; }
                set { Mesh.ColorArray[Index] = value; }
            }

            #endregion

            #region Attributes

            public T GetAttribute<T>(Symbol name)
            {
                var aa = Mesh.VertexAttributeArray<T>(name);
                return aa[Index];
            }

            public void SetAttribute<T>(Symbol name, T value)
            {
                var aa = Mesh.VertexAttributeArray<T>(name);
                aa[Index] = value;
            }

            public T GetIndexedAttribute<T>(Symbol name)
            {
                var ia = Mesh.VertexAttributeArray<int>(name);
                var aa = Mesh.VertexAttributeArray<T>(-name);
                return aa[ia[Index]];
            }

            public void SetIndexedAttribute<T>(Symbol name, T value)
            {
                var ia = Mesh.VertexAttributeArray<int>(name);
                var aa = Mesh.VertexAttributeArray<T>(-name);
                aa[ia[Index]] = value;
            }

            public int GetAttributeIndex(Symbol name)
            {
                var ia = Mesh.VertexAttributeArray<int>(name);
                return ia[Index];
            }

            public void SetAttributeIndex(Symbol name, int index)
            {
                var ia = Mesh.VertexAttributeArray<int>(name);
                ia[Index] = index;
            }

            #endregion

            #region Operators

            public static bool operator ==(Vertex v0, Vertex v1)
            {
                return v0.Mesh == v1.Mesh && v0.Index == v1.Index;
            }

            public static bool operator !=(Vertex v0, Vertex v1)
            {
                return v0.Mesh != v1.Mesh || v0.Index != v1.Index;
            }

            #endregion

            #region Overrides

            public override int GetHashCode()
            {
                return HashCode.Combine(Mesh.GetHashCode(), Index);
            }

            public override bool Equals(object obj)
            {
                return obj is Vertex ? this == (Vertex)obj : false;
            }

            #endregion

            #region IEquatable<Vertex> Members

            public bool Equals(Vertex other)
            {
                return this == other;
            }

            #endregion
        }

        #endregion

        #region Face

        /// <summary>
        /// An oriented facade structure for a single face.
        /// </summary>
        public struct Face : IBoundingBox3d, IEquatable<Face>
        {
            public readonly PolyMesh Mesh;
            public readonly FaceRef Ref;

            #region Constructors

            public Face(PolyMesh mesh, FaceRef faceRef)
            {
                Mesh = mesh;
                Ref = faceRef;
            }

            public Face(PolyMesh mesh, int index, int side)
            {
                Mesh = mesh;
                Ref = new FaceRef(index, side);
            }

            #endregion

            #region Properties

            public int Index => Ref.Index;
            public int Side => Ref.Side;

            public int VertexCount => Mesh.VertexCountOfFace(Ref.Index);

            public int EdgeCount => Mesh.VertexCountOfFace(Ref.Index);

            public bool IsValid => Mesh != null && Index >= 0;

            public bool HasTopology
            {
                get
                {
                    var fviNext = Mesh.FirstIndexArray[Ref.Index + 1];
                    return Mesh.m_faceEdgeRefArray.Length >= fviNext;
                }
            }

            /// <summary>
            /// Returns all turned variants of the face. The first "turned" face
            /// is the face itself.
            /// </summary>
            public IEnumerable<Face> TurnedFaces
            {
                get
                {
                    var m = Mesh;
                    var fi = Ref.Index;
                    var fia = m.FirstIndexArray;
                    int fvi = fia[fi], fvc = fia[fi + 1] - fvi;
                    var fs0 = Ref.Side;
                    var fs = fs0;
                    do
                    {
                        yield return new Face(m, new FaceRef(fi, fs));
                        if (++fs == fvc) fs = 0;
                    }
                    while (fs != fs0);
                }
            }

            /// <summary>
            /// Returns all edges around the face in counter-clockwise order.
            /// </summary>
            public IEnumerable<Edge> Edges
            {
                get
                {
                    int fi = Ref.Index;
                    var fia = Mesh.FirstIndexArray;
                    int fvi = fia[fi], fvc = fia[fi + 1] - fvi;
                    var fera = Mesh.m_faceEdgeRefArray;
                    int fs0 = Ref.Side;
                    int fs = fs0;
                    do
                    {
                        yield return new Edge(Mesh, fera[fvi + fs]);
                        if (++fs == fvc) fs = 0;
                    }
                    while (fs != fs0);
                }
            }

            /// <summary>
            /// Returns all vertices around the face in counter-clockwise order.
            /// </summary>
            public IEnumerable<Vertex> Vertices
            {
                get
                {
                    var fi = Ref.Index;
                    int[] fia = Mesh.FirstIndexArray, via = Mesh.VertexIndexArray;
                    int fvi = fia[fi], fvc = fia[fi + 1] - fvi;
                    var fs0 = Ref.Side;
                    var fs = fs0;
                    do
                    {
                        yield return new Vertex(Mesh, via[fvi + fs]);
                        if (++fs == fvc) fs = 0;
                    }
                    while (fs != fs0);
                }
            }

            /// <summary>
            /// Returns all vertices around the face in counter-clockwise order.
            /// </summary>
            public IEnumerable<int> VertexIndices
            {
                get
                {
                    var fi = Ref.Index;
                    int[] fia = Mesh.FirstIndexArray, via = Mesh.VertexIndexArray;
                    int fvi = fia[fi], fvc = fia[fi + 1] - fvi;
                    var fs0 = Ref.Side;
                    var fs = fs0;
                    do
                    {
                        yield return via[fvi + fs];
                        if (++fs == fvc) fs = 0;
                    }
                    while (fs != fs0);
                }
            }

            /// <summary>
            /// Returns the polygon of the face (i.e. an un-oriented version
            /// of the face).
            /// </summary>
            public Polygon Polygon => new Polygon(Mesh, Ref.Index);

            public Polygon3d Polygon3d
            {
                get
                {
                    var m = Mesh;
                    var fi = Ref.Index;
                    int[] fia = m.FirstIndexArray, via = m.VertexIndexArray;
                    int fvi = fia[fi], fvc = fia[fi + 1] - fvi;
                    var va = new V3d[fvc];
                    var pa = m.PositionArray;
                    var fs = Ref.Side;
                    for (int i = 0; i < fvc; i++)
                    {
                        va[i] = pa[via[fvi + fs]];
                        if (++fs == fvc) fs = 0;
                    }
                    return new Polygon3d(va);
                }
            }

            public int StartVertexIndex => Mesh.FirstIndexArray[Ref.Index];

            #endregion

            #region Attributes

            /// <summary>
            /// Gets the face vertex index.
            /// </summary>
            public int GetFaceVertexIndex(int side)
            {
                var fia = Mesh.FirstIndexArray;
                int fvi = fia[Index], fvc = fia[Index + 1] - fvi;
                if (side < 0) side = side % fvc + fvc;
                return fvi + (Side + side) % fvc;
            }

            public T GetAttribute<T>(Symbol name)
            {
                var aa = Mesh.FaceAttributeArray<T>(name);
                return aa[Index];
            }

            public void SetAttribute<T>(Symbol name, T value)
            {
                var aa = Mesh.FaceAttributeArray<T>(name);
                aa[Index] = value;
            }

            public T GetIndexedAttribute<T>(Symbol name)
            {
                var ia = Mesh.FaceAttributeArray<int>(name);
                var aa = Mesh.FaceAttributeArray<T>(-name);
                return aa[ia[Index]];
            }

            public void SetIndexedAttribute<T>(Symbol name, T value)
            {
                var ia = Mesh.FaceAttributeArray<int>(name);
                var aa = Mesh.FaceAttributeArray<T>(-name);
                aa[ia[Index]] = value;
            }

            public int GetAttributeIndex(Symbol name)
            {
                var ia = Mesh.FaceAttributeArray<int>(name);
                return ia[Index];
            }

            public void SetAttributeIndex(Symbol name, int index)
            {
                var ia = Mesh.FaceAttributeArray<int>(name);
                ia[Index] = index;
            }

            /// <summary>
            /// Gets a face vertex attribute.
            /// </summary>
            public T GetVertexAttribute<T>(Symbol name, int side)
            {
                var fvi = GetFaceVertexIndex(side);
                var aa = Mesh.FaceVertexAttributeArray<T>(name);
                return aa[fvi];
            }

            /// <summary>
            /// Sets a face vertex attribute.
            /// </summary>
            public void SetVertexAttribute<T>(Symbol name, int side, T value)
            {
                var fvi = GetFaceVertexIndex(side);
                var aa = Mesh.FaceVertexAttributeArray<T>(name);
                aa[fvi] = value;
            }

            /// <summary>
            /// Gets a face vertex indexed attribute.
            /// </summary>
            public T GetVertexIndexedAttribute<T>(Symbol name, int side)
            {
                var fvi = GetFaceVertexIndex(side);
                var ia = Mesh.FaceVertexAttributeArray<int>(name);
                var aa = Mesh.FaceVertexAttributeArray<T>(-name);
                return aa[ia[fvi]];
            }

            /// <summary>
            /// Sets a face vertex indexed attribute.
            /// </summary>
            public void SetVertexIndexedAttribute<T>(Symbol name, int side, T value)
            {
                var fvi = GetFaceVertexIndex(side);
                var ia = Mesh.FaceVertexAttributeArray<int>(name);
                var aa = Mesh.FaceVertexAttributeArray<T>(-name);
                aa[ia[fvi]] = value;
            }

            /// <summary>
            /// Gets a face vertex attribute index.
            /// </summary>
            public int GetVertexAttributeIndex(Symbol name, int side)
            {
                var fvi = GetFaceVertexIndex(side);
                var ia = Mesh.FaceVertexAttributeArray<int>(name);
                return ia[fvi];
            }

            /// <summary>
            /// Sets a face vertex attribute index.
            /// </summary>
            public void SetVertexAttributeIndex(Symbol name, int side, int index)
            {
                var fvi = GetFaceVertexIndex(side);
                var ia = Mesh.FaceVertexAttributeArray<int>(name);
                ia[fvi] = index;
            }
            
            #endregion

            #region Methods

            /// <summary>
            /// Return a turned variant of the face, turned by turnCount steps
            /// in counter-clockwise fashion.
            /// </summary>
            public Face Turned(int turnCount)
            {
                return new Face(Mesh, Mesh.TurnedFaceRef(Ref, turnCount));
            }

            public Edge Edge(int faceEdgeIndex)
            {
                return new Edge(Mesh,
                                Mesh.EdgeRefOfFaceRef(
                                        Ref, faceEdgeIndex));
            }

            public Vertex Vertex(int faceVertexIndex)
            {
                return new Vertex(Mesh,
                                  Mesh.VertexIndexOfFaceRef(
                                        Ref, faceVertexIndex));
            }

            public int VertexIndex(int faceVertexIndex)
            {
                return Mesh.VertexIndexOfFaceRef(
                            Ref, faceVertexIndex);
            }

            public bool IsIndexEqualTo(Face otherFace)
            {
                return Ref.Index == otherFace.Ref.Index;
            }

            #endregion

            #region Operators

            public static bool operator ==(Face f0, Face f1)
            {
                return f0.Mesh == f1.Mesh && f0.Ref == f1.Ref;
            }

            public static bool operator !=(Face f0, Face f1)
            {
                return f0.Mesh != f1.Mesh || f0.Ref != f1.Ref;
            }

            #endregion

            #region Overrides

            public override int GetHashCode()
            {
                return HashCode.Combine(Mesh.GetHashCode(), Ref.GetHashCode());
            }

            public override bool Equals(object obj)
            {
                return obj is Face ? this == (Face)obj : false;
            }

            #endregion

            #region IBoundingBox3d Members

            public Box3d BoundingBox3d
            {
                get { return Mesh.BoundingBoxOfFace(Ref.Index); }
            }

            #endregion

            #region IEquatable<Face> Members

            public bool Equals(Face other)
            {
                return this == other;
            }

            #endregion
        }

        #endregion

        #region Edge

        /// <summary>
        /// An oriented facade structure for a single edge. This is
        /// equivalent to a half edge.
        /// </summary>
        public struct Edge : IEquatable<Edge>
        {
            public readonly PolyMesh Mesh;
            public readonly int Ref;

            public static readonly Edge Invalid = default(Edge);

            #region Constructors

            public Edge(PolyMesh mesh, int edgeRef)
            {
                Mesh = mesh;
                Ref = edgeRef;
            }

            public Edge(PolyMesh mesh, int index, int side)
            {
                Mesh = mesh;
                Ref = EdgeRef.Create(index, side);
            }

            #endregion

            #region Properties

            public int Index { get { return EdgeRef.Index(Ref); } }
            public int Side { get { return EdgeRef.Side(Ref); } }

            public bool IsBorder
            {
                get { return Mesh.FaceRef_Zero_OfEdgeRef(Ref).Index < 0; }
            }

            /// <summary>
            /// This is true for border edges and for non-manifold edges.
            /// </summary>
            public bool OppositeIsBorder
            {
                get { return Mesh.FaceRef_One_OfEdgeRef(Ref).Index < 0; }
            }

            public bool IsNonManifold
            {
                get { return Mesh.FaceRef_Zero_OfEdgeRef(Ref).IsNonManifoldEdgeRef; }
            }

            /// <summary>
            /// A valid edge is attached to at least one face.
            /// </summary>
            public bool IsValid
            {
                get
                {
                    return Mesh != null
                          && (Mesh.FaceRef_Zero_OfEdgeRef(Ref).Index >= 0
                           || Mesh.FaceRef_One_OfEdgeRef(Ref).Index >= 0);
                }
            }

            /// <summary>
            /// An invalid edge is not attached to any face.
            /// </summary>
            public bool IsInvalid
            {
                get
                {
                    return Mesh == null 
                          || Mesh.FaceRef_Zero_OfEdgeRef(Ref).Index < 0
                          && Mesh.FaceRef_One_OfEdgeRef(Ref).Index < 0;
                }
            }

            /// <summary>
            /// Returns face if both edge faces are valid.
            /// </summary>
            public bool IsAnyBorder
            {
                get
                {
                    return Mesh.FaceRef_Zero_OfEdgeRef(Ref).Index < 0
                       || Mesh.FaceRef_One_OfEdgeRef(Ref).Index < 0;
                }
            }

            public bool OppositeIsNonManifold
            {
                get { return Mesh.FaceRef_One_OfEdgeRef(Ref).IsNonManifoldEdgeRef; }
            }

            public Face Face
            {
                get { var m = Mesh; return new Face(m, m.FaceRef_Zero_OfEdgeRef(Ref)); }
            }

            /// <summary>
            /// Returns either the zero face or if it's invaild the opposite face.
            /// The face will be invalid if no faces are connected to the edge (for whatever reason)
            /// </summary>
            public Face AnyFace
            {
                get 
                {
                    var m = Mesh;
                    var fr = m.FaceRef_Zero_OfEdgeRef(Ref);
                    if (fr.Index >= 0) return new Face(m, fr);
                    return new Face(m, m.FaceRef_One_OfEdgeRef(Ref));
                }
            }

            public int FaceIndex => Mesh.FaceRef_Zero_OfEdgeRef(Ref).Index;

            public Face OppositeFace
            {
                get { var m = Mesh; return new Face(m, m.FaceRef_One_OfEdgeRef(Ref)); }
            }

            public int OppositeFaceIndex => Mesh.FaceRef_One_OfEdgeRef(Ref).Index;

            /// <summary>
            /// The same edge with opposite orientation. This is equivalent
            /// to the other half-edge.
            /// </summary>
            public Edge Opposite => new Edge(Mesh, EdgeRef.Reversed(Ref));

            public int FromVertexIndex
            {
                get
                {
                    var m = Mesh;
                    var fr = m.FaceRef_Zero_OfEdgeRef(Ref);
                    return fr.Index >= 0 ? m.VertexIndex_Zero_OfFaceRef(fr) :
                                           m.VertexIndex_PlusOne_OfFaceRef(m.FaceRef_One_OfEdgeRef(Ref));
                }
            }

            public int ToVertexIndex
            {
                get
                {
                    var m = Mesh;
                    var fr = m.FaceRef_Zero_OfEdgeRef(Ref);
                    return fr.Index >= 0 ? m.VertexIndex_PlusOne_OfFaceRef(fr) :
                                           m.VertexIndex_Zero_OfFaceRef(m.FaceRef_One_OfEdgeRef(Ref));
                }
            }

            public Vertex FromVertex => new Vertex(Mesh, FromVertexIndex);

            public Vertex ToVertex => new Vertex(Mesh, ToVertexIndex);

            public Edge NextEdge
            {
                get
                {
                    var m = Mesh;
                    return new Edge(m, m.EdgeRefOfFaceRef(
                                            m.FaceRef_Zero_OfEdgeRef(Ref), 1));
                }
            }

            public Edge PrevEdge
            {
                get
                {
                    var m = Mesh;
                    return new Edge(m, m.EdgeRefOfFaceRef(
                                            m.FaceRef_Zero_OfEdgeRef(Ref), -1));
                }
            }

            public IEnumerable<Face> Faces
            {
                get
                {
                    if (!this.IsBorder)  yield return this.Face;
                    if (!this.OppositeIsBorder) yield return this.OppositeFace;
                    yield break;
                }
            }

            public IEnumerable<Vertex> Vertices
            {
                get 
                {
                    GetVertexIndices(out int fvi, out int tvi);
                    yield return new Vertex(Mesh, fvi);
                    yield return new Vertex(Mesh, tvi);
                }
            }

            /// <summary>
            /// Gets all other edges connected to this vertex.
            /// </summary>
            public IEnumerable<Edge> ToVertexEdges
            {
                get 
                {
                    var thisEdgeIndex = this.Index;
                    return this.ToVertex.Edges.Where(edge => edge.Index != thisEdgeIndex);
                }
            }

            /// <summary>
            /// Gets all other edges connected to this vertex.
            /// </summary>
            public IEnumerable<Edge> FromVertexEdges
            {
                get
                {
                    var thisEdgeIndex = this.Index; 
                    return this.FromVertex.Edges.Where(edge => edge.Index != thisEdgeIndex);
                }
            }

            /// <summary>
            /// Gets any vertex not part of the edge from the face.
            /// </summary>
            public Vertex AnyOtherFaceVertex
            {
                get 
                {
                    var m = Mesh;
                    var fr = m.FaceRef_Zero_OfEdgeRef(Ref);
                    return new Vertex(m, m.VertexIndex_MinusOne_OfFaceRef(fr));
                }
            }

            /// <summary>
            /// Gets any vertex not part of the edge from the face on the opposite side.
            /// </summary>
            public Vertex AnyOtherOppositeFaceVertex
            {
                get 
                {
                    var m = Mesh;
                    var fr = m.FaceRef_One_OfEdgeRef(Ref);
                    return new Vertex(m, m.VertexIndex_PlusOne_OfFaceRef(fr));
                }
            }

            public Line3d EdgeLine => new Line3d(this.FromVertex.Position, this.ToVertex.Position);

            public Ray3d EdgeRay
            {
                get 
                { 
                    var from = this.FromVertex.Position;
                    var to = this.ToVertex.Position;
                    return new Ray3d(from, to - from);
                }
            }

            public double Length
            {
                get 
                {
                    var fi = this.FromVertexIndex;
                    var ti = this.ToVertexIndex;
                    if (fi < 0 || ti < 0)
                        return -1;
                    var pa = Mesh.PositionArray;
                    return (pa[fi] - pa[ti]).Length;
                }
            }

            #endregion

            #region Attributes

            public T GetAttribute<T>(Symbol name)
            {
                var aa = Mesh.EdgeAttributeArray<T>(name);
                return aa[Index];
            }

            public void SetAttribute<T>(Symbol name, T value)
            {
                var aa = Mesh.EdgeAttributeArray<T>(name);
                aa[Index] = value;
            }

            public T GetIndexedAttribute<T>(Symbol name)
            {
                var ia = Mesh.EdgeAttributeArray<int>(name);
                var aa = Mesh.EdgeAttributeArray<T>(-name);
                return aa[ia[Index]];
            }

            public void SetIndexedAttribute<T>(Symbol name, T value)
            {
                var ia = Mesh.EdgeAttributeArray<int>(name);
                var aa = Mesh.EdgeAttributeArray<T>(-name);
                aa[ia[Index]] = value;
            }

            public int GetAttributeIndex(Symbol name)
            {
                var ia = Mesh.EdgeAttributeArray<int>(name);
                return ia[Index];
            }

            public void SetAttributeIndex(Symbol name, int index)
            {
                var ia = Mesh.EdgeAttributeArray<int>(name);
                ia[Index] = index;
            }

            #endregion

            #region Operations

            /// <summary>
            /// For a non-manifold edge, return the next accessible non-
            /// manifold edge. Sets the out parameter flipped to true, iff
            /// the returned non-manifold edge is flipped with respect to
            /// this non-manifold edge..
            /// </summary>
            public Edge NextNonManifoldEdge(out bool flipped)
            {
                var m = Mesh;
                return new Edge(m, m.NextNonManifoldEdgeRef(Ref, out flipped));
            }

            /// <summary>
            /// Checks if the edge is between the specified vertices.
            /// </summary>
            public bool IsBetween(int vi0, int vi1)
            {
                GetVertexIndices(out int fvi, out int tvi);

                if (fvi == vi0)
                    return tvi == vi1;
                else
                    return fvi == vi1 && tvi == vi0;
            }

            /// <summary>
            /// Checks if the edge is between the specified vertices.
            /// </summary>
            public bool IsBetween(Vertex vertex0, Vertex vertex1)
                => IsBetween(vertex0.Index, vertex1.Index);

            /// <summary>
            /// Checks if the face touches the edge on any side.
            /// </summary>
            public bool IsBorderOf(Face face) => IsBorderOf(face.Index);

            /// <summary>
            /// Checks if the face with the supplied index touches the edge on any side.
            /// </summary>
            public bool IsBorderOf(int faceIndex)
            {
                GetFaceIndices(out int fi0, out int fi1);
                return fi0 == faceIndex || fi1 == faceIndex;
            }

            /// <summary>
            /// Compares to edges without considering the edge side.
            /// </summary>
            public bool IsIndexEqualTo(Edge edge) => ((Ref ^ edge.Ref) >> 1) == 0;

            /// <summary>
            /// Compares to edges without considering the edge side.
            /// </summary>
            public bool IsIndexEqualTo(int edgeIndex) => EdgeRef.Index(Ref) == edgeIndex;

            /// <summary>
            /// Checks if the edges any the spiefied vertex as start or end point.
            /// </summary>
            public bool IsConnectedToVertex(int vertexIndex)
            {
                GetVertexIndices(out int vi0, out int vi1);
                return vi0 == vertexIndex || vi1 == vertexIndex;
            }

            /// <summary>
            /// Checks if the edges any the spiefied vertex as start or end point.
            /// </summary>
            public bool IsConnectedToVertex(Vertex vertex) => IsConnectedToVertex(vertex.Index);

            public bool IsConnectedToEdge(int edgeIndex)
                => FromVertexEdges.Any(e => e.Index == edgeIndex) || ToVertexEdges.Any(e => e.Index == edgeIndex);
            
            public bool IsConnectedToFace(int faceIndex)
                => FaceIndex == faceIndex || OppositeFaceIndex == faceIndex;

            /// <summary>
            /// Returns all connected edges of this vertex.
            /// Note: The vertex must be connected to this edge.
            /// </summary>
            public IEnumerable<Edge> GetConnectedEdgesAt(int vertexIndex)
                => GetConnectedEdgesAt(new Vertex(Mesh, vertexIndex));

            /// <summary>
            /// Returns all connected edges of this vertex.
            /// </summary>
            public IEnumerable<Edge> GetConnectedEdgesAt(Vertex vertex)
            {
                if (IsConnectedToVertex(vertex))
                    return Enumerable.Empty<Edge>();
                var ei = Index;
                return vertex.Edges.Where(e => e.Index != ei);
            }

            /// <summary>
            /// Note: The vertex must be connected to this edge.
            /// </summary>
            public int GetOppositeVertexIndex(int vertexIndex)
            {
                GetVertexIndices(out int fvi, out int tvi);
                if (fvi == vertexIndex) return tvi;
                if (tvi == vertexIndex) return fvi;
                return -1;
            }

            /// <summary>
            /// Note: The vertex must be connected to this edge.
            /// </summary>
            public Vertex GetOppositeVertex(Vertex vertex)
                => new Vertex(Mesh, GetOppositeVertexIndex(vertex.Index));

            /// <summary>
            /// Note: The face must be connected to this edge.
            /// </summary>
            public int GetOppositeFaceIndex(int faceIndex)
            {
                GetFaceIndices(out int fi0, out int fi1);
                if (fi0 == faceIndex) return fi1;
                if (fi1 == faceIndex) return fi0;
                throw new InvalidOperationException();
            }

            /// <summary>
            /// Note: The face must be connected to this edge.
            /// </summary>
            public Face GetOppositFace(Face face)
            {
                var f0 = this.Face;
                var f1 = this.OppositeFace;
                if (f0.IsIndexEqualTo(face)) return f1;
                if (f1.IsIndexEqualTo(face)) return f0;
                throw new InvalidOperationException();
            }

            public void GetVertexIndices(out int fromVertexIndex, out int toVertexIndex)
            {
                var m = Mesh;
                var fr = m.FaceRef_Zero_OfEdgeRef(Ref);

                if (fr.Index >= 0)
                {
                    fromVertexIndex = m.VertexIndex_Zero_OfFaceRef(fr);
                    toVertexIndex = m.VertexIndex_PlusOne_OfFaceRef(fr);
                }
                else
                {
                    fr = m.FaceRef_One_OfEdgeRef(Ref);
                    if (fr.Index >= 0)
                    {
                        fromVertexIndex = m.VertexIndex_PlusOne_OfFaceRef(fr);
                        toVertexIndex = m.VertexIndex_Zero_OfFaceRef(fr);
                    }
                    else
                    {
                        fromVertexIndex = -1;
                        toVertexIndex = -1;
                    }
                }
            }

            public void GetFaceIndices(out int faceIndex, out int oppositeFaceIndex)
            {
                var m = Mesh;
                var fia = m.m_edgeFaceRefArray[Ref].Index;
                var fib = m.m_edgeFaceRefArray[Ref ^ 1].Index;
                if ((Ref & 0x1) == 0)
                {
                    faceIndex = fia;
                    oppositeFaceIndex = fib;
                }
                else
                {
                    faceIndex = fib;
                    oppositeFaceIndex = fia;
                }
            }

            public void EdgeWalkCounterClockwise(Func<Edge, bool> action)
            {
                var m = Mesh;
                var er = Ref;

                // run counter-clockwise until end or start
                var frStart = m.FaceRef_Zero_OfEdgeRef(er);
                var fr = frStart;

                if (!action(new Edge(m, er)))
                    return;

                // while there is a face connected to this edge on side 0
                while (fr.Index >= 0)
                {
                    // test if edge in +1 on face is connected to this.Index
                    er = m.EdgeRef_PlusOne_OfFaceRef(fr);
                    if (m.VertexIndex_Zero_OfFaceRef(fr) != this.Index)
                    {
                        // incoherent winding order
                        er = m.EdgeRef_MinusOne_OfFaceRef(fr);
                        if (m.VertexIndex_Zero_OfFaceRef(fr) != this.Index)
                        {
                            Report.Line("fail+");
                            return;
                        }
                    }

                    // jump to other face over the edge
                    fr = m.FaceRef_One_OfEdgeRef(er);

                    if (fr == frStart) // ran a whole loop
                        return;

                    if (!action(new Edge(m, er)))
                        return;
                }
            }

            public void EdgeWalkClockwise(Func<Edge, bool> action)
            {
                var m = Mesh;
                var er = Ref;

                // run clockwise until end or start
                var frStart = m.FaceRef_One_OfEdgeRef(er);
                var fr = frStart;

                if (!action(new Edge(m, er)))
                    return;

                while (fr.Index >= 0)
                {
                    // jump to other face edge connected to this vertex
                    // test if edge in +1 on face is connected to this.Index
                    er = m.EdgeRef_MinusOne_OfFaceRef(fr);
                    if (m.VertexIndex_Zero_OfFaceRef(fr) != this.Index)
                    {
                        // incoherent winding order
                        er = m.EdgeRef_PlusOne_OfFaceRef(fr);
                        if (m.VertexIndex_Zero_OfFaceRef(fr) != this.Index)
                        {
                            Report.Line("fail-");
                            return;
                        }
                    }

                    // jump to other face
                    fr = m.FaceRef_One_OfEdgeRef(er);

                    if (fr == frStart) // ran a whole loop
                        return;

                    if (!action(new Edge(m, er)))
                        return;
                }
            }

            #endregion

            #region Operators

            public static bool operator ==(Edge e0, Edge e1)
            {
                return e0.Mesh == e1.Mesh && e0.Ref == e1.Ref;
            }

            public static bool operator !=(Edge e0, Edge e1)
            {
                return e0.Mesh != e1.Mesh || e0.Ref != e1.Ref;
            }

            #endregion

            #region Overrides

            public override int GetHashCode()
            {
                return HashCode.Combine(Mesh.GetHashCode(), Ref);
            }

            public override bool Equals(object obj)
            {
                return obj is Edge ? this == (Edge)obj : false;
            }

            #endregion

            #region IEquatable<Edge> Members

            public bool Equals(Edge other)
            {
                return this == other;
            }

            #endregion
        }

        #endregion

        #region Transformations

        public static readonly M44d Vrml97ReadTrafo =
                new M44d(1, 0, 0, 0,
                         0, 0, -1, 0,
                         0, 1, 0, 0,
                         0, 0, 0, 1);

        public static readonly M44d Vrml97ReadTrafoInverse =
                new M44d(1, 0, 0, 0,
                         0, 0, 1, 0,
                         0, -1, 0, 0,
                         0, 0, 0, 1);

        public PolyMesh Transformed(Trafo3d trafo)
        {
            return Transformed(trafo.Forward, trafo.Backward);
        }

        public PolyMesh Transformed(M44d matrix, M44d inverse)
        {
            return Copy(new SymbolDict<Func<Array, Array>>
            {
                { Property.Positions,
                    a => a.Copy<V3d>(p => matrix.TransformPos(p)) },
                { Property.Normals,
                    a => a.Copy<V3d>(nd => M44d.TransposedTransformDir(inverse, nd).Normalized,
                    b => b.Copy<V3f>(nf => M44d.TransposedTransformDir(inverse, nf.ToV3d()).Normalized.ToV3f(),
                                        indexArray => indexArray)) },
                { -Property.Normals,
                    a => a.Copy<V3d>(nd => M44d.TransposedTransformDir(inverse, nd).Normalized,
                    b => b.Copy<V3f>(nf => M44d.TransposedTransformDir(inverse, nf.ToV3d()).Normalized.ToV3f()))},
                                    
            },
            a => a);
        }

        public PolyMesh Vrml97AfterReadingTransformed()
        {
            return Transformed(Vrml97ReadTrafo, Vrml97ReadTrafoInverse);
        }

        #endregion

        #region Normals

        /// <summary>
        /// Generates Normals and Centroids for faces. Note, that for
        /// degenerate faces, the returned normals are Zero, and in this
        /// case the centroids are also set to Zero. If you need centroids
        /// for degenerate faces, you should scan the array for zero normals
        /// and recalculate the centroid.
        /// </summary>
        /// <param name="warn"></param>
        public void AddFaceNormalsAreasCentroids(bool warn = true)
        {
            var pa = m_positionArray;
            var faceCount = FaceCount;
            int[] fia = FirstIndexArray, via = VertexIndexArray;

            var normalArray = new V3d[faceCount];
            var areaArray = new double[faceCount];
            var centroidArray = new V3d[faceCount];

            var zeroCount = 0;
            var nanCount = 0;
            for (int fvi = fia[0], fi = 0; fi < faceCount; fi++)
            {
                V3d p0 = pa[via[fvi++]], p1 = pa[via[fvi++]], p2 = pa[via[fvi++]];
                V3d e0 = p1 - p0, e1 = p2 - p0;
                var n0 = V3d.Cross(e0, e1);
                var normal = n0;
                var a0 = n0.Length;
                var area = a0;
                var centroid = (p0 + p1 + p2) * a0;
                for (int fve = fia[fi + 1]; fvi < fve; fvi++)
                {
                    p1 = p2; p2 = pa[via[fvi]];
                    e0 = e1; e1 = p2 - p0;
                    var n = V3d.Cross(e0, e1);
                    normal += n;
                    var a = V3d.Dot(n0, n) > 0 ? n.Length : -n.Length;
                    area += a;
                    centroid += (p0 + p1 + p2) * a;
                }
                var len2 = normal.LengthSquared;
                if (len2 == 0)
                    ++zeroCount;
                else if (normal.IsNaN)
                    ++nanCount;
                else
                {
                    centroidArray[fi] = centroid / (3.0 * area);
                    area = Fun.Sqrt(len2);
                    normalArray[fi] = normal / area;
                    areaArray[fi] = area * 0.5;
                }
            }
            if (warn)
            {
                if (zeroCount > 0) Report.Warn("encountered {0} zero normal vectors", zeroCount);
                if (nanCount > 0) Report.Warn("encountered {0} nan normal vectors", nanCount);
            }
            FaceAttributes[Property.Normals] = normalArray;
            FaceAttributes[Property.Areas] = areaArray;
            FaceAttributes[Property.Centroids] = centroidArray;
        }

        /// <summary>
        /// Adds double precision normals for all triangles of the mesh.
        /// </summary>
        internal V3d[] ComputeFaceNormalArray(bool warn = true)
        {
            var pa = m_positionArray;
            var faceCount = FaceCount;
            int[] fia = FirstIndexArray, via = VertexIndexArray;

            var normalArray = new V3d[faceCount];

            if (fia == null || via == null)
            {
                Report.Warn("ComputeFaceNormalArray: mesh does not have any faces");
            }
            else
            { 
                var zeroCount = 0;
                var nanCount = 0;
                for (int fvi = fia[0], fi = 0; fi < faceCount; fi++)
                {
                    V3d p0 = pa[via[fvi++]], e0 = pa[via[fvi++]] - p0;
                    var normal = V3d.Zero;
                    for (int fve = fia[fi + 1]; fvi < fve; fvi++)
                    {
                        var e1 = pa[via[fvi]] - p0;
                        normal += V3d.Cross(e0, e1);
                        e0 = e1;
                    }
                    var len2 = normal.LengthSquared;
                    if (len2 == 0)
                        ++zeroCount;
                    else if (normal.IsNaN)
                        ++nanCount;
                    else
                        normalArray[fi] = normal * (1.0 / Fun.Sqrt(len2));
                }
                if (warn)
                {
                    if (zeroCount > 0)
                        Report.Warn("encountered {0} zero normal vectors", zeroCount);
                    if (nanCount > 0)
                        Report.Warn("encountered {0} nan normal vectors", nanCount);
                }
            }
            return normalArray;
        }


        /// <summary>
        /// Compute per vertex indexed normals that interpolate over edges
        /// where the normals of the adjacent faces deviate by less than the
        /// supplied crease angle.
        /// </summary>
        /// <returns>this</returns>
        internal PolyMesh AddPerVertexIndexedNormals(double creaseAngle)
            => AddPerVertexIndexedNormals(creaseAngle, Int32.MaxValue);

        /// <summary>
        /// Returns a new PolyMesh with normals per face.
        /// </summary>
        /// <returns>Mesh with FaceNormals</returns>
        public PolyMesh WithPerFaceNormals()
        {
            var m = Copy();
            if (this.FaceAttributes.Contains(Property.Normals)) return m;
            m.FaceAttributes = new SymbolDict<Array>(m.FaceAttributes) { { Property.Normals, ComputeFaceNormalArray() } };
            return m;
        }

        /// <summary>
        /// Computes face normals and adds the attribute array to the mesh.
        /// </summary>
        public void AddPerFaceNormals()
        {
            FaceAttributes[Property.Normals] = ComputeFaceNormalArray();
        }

        /// <summary>
        /// Return a PolyMesh with per vertex indexed normals that interpolate over edges
        /// where the normals of the adjacent faces deviate by less than the
        /// supplied crease angle. Only interpolate for the supplied count
        /// of faces in both directions around a vertex.
        /// </summary>
        /// <param name="creaseAngle">crease angle in radians</param>
        /// <param name="maxNeighbourFaceCount">maximum number of faces to interpolate</param>
        /// <returns>mesh with added per-vertex indexed normals</returns>
        public PolyMesh WithPerVertexIndexedNormals(
                double creaseAngle,
                int maxNeighbourFaceCount = Int32.MaxValue)
        {
            var m = Copy();
            m.AddPerVertexIndexedNormals(creaseAngle, maxNeighbourFaceCount);
            return m;
        }

        /// <summary>
        /// Compute per vertex indexed normals that interpolate over edges
        /// where the normals of the adjacent faces deviate by less than the
        /// supplied crease angle. Only interpolate for the supplied count
        /// of faces in both directions around a vertex.
        /// </summary>
        /// <param name="creaseAngle">crease angle in radians</param>
        /// <param name="maxNeighbourFaceCount">Maximum number of of faces considered per vertex</param>
        /// <returns>this</returns>
        internal PolyMesh AddPerVertexIndexedNormals(double creaseAngle,
                                               int maxNeighbourFaceCount)
        {
            var creaseDotProduct = Fun.Cos(creaseAngle);
            if (!HasTopology) BuildTopology(0);
            if (!FaceAttributes.ContainsKey(Property.Normals)) AddPerFaceNormals(); // try to use normals attribute if already there
            var faceNormalsAttribute = FaceAttributes[Property.Normals];
            // face normal attribute is supposed to be non-indexed and of V3d -> compute otherwise (do not store if computed as original attributes should not be touched)
            var faceNormals = (faceNormalsAttribute is V3d[]) ? (V3d[])faceNormalsAttribute : ComputeFaceNormalArray();
            var fia = m_firstIndexArray;
            var nia = new int[VertexIndexCount].Set(-1); // a per face-verted index array to track shared and processed normals
            int attributeCount = 0;
            // loop over all faces
            for (int fvi = fia[0], fi = 0; fi < FaceCount; fi++)
            {
                int fve = fia[fi + 1], fvc = fve - fvi;
                // loop over all egdge paris (referenced by fs0, fs1) connected to current face vertix index (fvi + fsi1)
                for (int fs0 = fvc - 1, fs1 = 0; fs1 < fvc; fs0 = fs1, fs1++)
                {
                    int ai = nia[fvi + fs1]; // check if normal of this unique face-vertex attribute has aready been processed
                    if (ai < 0)
                    {
                        ai = attributeCount++;
                        nia[fvi + fs1] = ai;
                        V3d n0 = faceNormals[fi];
                        V3d n = n0;
                        int er = EdgeRef_Zero_OfFace(fi, fs0);
                        var fr = FaceRef_One_OfEdgeRef(er);
                        int nc = 0;

                        var nmei0 = -1;

                        while ((fr.Index >= 0 || fr.IsNonManifoldEdgeRef) && nc < maxNeighbourFaceCount && (nmei0 != EdgeRef.Index(er)))
                        {
                            // loop to next face on the opposite of the edge
                            while (fr.Index >= 0 && nc < maxNeighbourFaceCount)
                            {
                                int nfvi = fia[fr.Index] + fr.Side; // unique face-vertex attribute index of this face
                                int nai = nia[nfvi];
                                // check processed
                                if (nai >= 0) break;
                                // check crease angle
                                V3d nn = faceNormals[fr.Index];
                                if (V3d.Dot(n, nn) < creaseDotProduct) break;
                                // record crease normal
                                nia[nfvi] = ai;
                                n = nn;
                                // jump to next edges conncted to the vertex
                                er = EdgeRef_MinusOne_OfFaceRef(fr); // next edge opposite winding order
                                fr = FaceRef_One_OfEdgeRef(er); // opposite face
                                nmei0 = -1;
                                nc++;
                            }

                            if (!fr.IsNonManifoldEdgeRef) break;

                            if (nmei0 == -1) nmei0 = EdgeRef.Index(er); // store index of first non-manifold edge to terminate loop
                            er = NextNonManifoldEdgeRef(er, out bool flipped);
                            if (er < 0) break;
                            fr = FaceRef_One_OfEdgeRef(er); // opposite face
                        }
                        n = n0;
                        er = EdgeRef_Zero_OfFace(fi, fs1);
                        fr = FaceRef_One_OfEdgeRef(er);
                        nmei0 = -1;
                        nc = 0;
                        while ((fr.Index >= 0 || fr.IsNonManifoldEdgeRef) && nc < maxNeighbourFaceCount && (nmei0 != EdgeRef.Index(er)))
                        {
                            while (fr.Index >= 0 && nc < maxNeighbourFaceCount)
                            {
                                int nfvi = fia[fr.Index] + fr.Side + 1;
                                if (nfvi == fia[fr.Index + 1]) nfvi = fia[fr.Index];
                                int nai = nia[nfvi];
                                if (nai >= 0) break;
                                V3d nn = faceNormals[fr.Index];
                                if (V3d.Dot(n, nn) < creaseDotProduct) break;
                                nia[nfvi] = ai;
                                n = nn;
                                er = EdgeRef_PlusOne_OfFaceRef(fr); // next edge in winding order
                                fr = FaceRef_One_OfEdgeRef(er); // opposite face
                                nmei0 = -1;
                                nc++;
                            }

                            if (!fr.IsNonManifoldEdgeRef) break;

                            if (nmei0 == -1) nmei0 = EdgeRef.Index(er); // store index of first non-manifold edge to terminate loop
                            er = NextNonManifoldEdgeRef(er, out bool flipped);
                            if (er < 0) break;
                            fr = FaceRef_One_OfEdgeRef(er); // opposite face
                        }

                    }
                }
                fvi = fve;
            }

            var na = new V3d[attributeCount];
            var pa = m_positionArray;
            int[] via = VertexIndexArray;
            for (int fvi = fia[0], fi = 0; fi < FaceCount; fi++)
            {
                int fve = fia[fi + 1], fvc = fve - fvi;
                var p = pa[via[fvi + fvc - 1]];
                var ep = (pa[via[fvi + fvc - 2]] - p).Normalized;
                var n = faceNormals[fi];
                for (int fs = fvc - 1, fsn = 0; fsn < fvc; fs = fsn, fsn++)
                {
                    V3d pn = pa[via[fvi + fsn]]; V3d en = (pn - p).Normalized;
                    na[nia[fvi + fs]] += n * Fun.Acos(ep.Dot(en).Clamp(-1.0, 1.0));
                    p = pn; ep = -en;
                }
                fvi = fve;
            }

            na.Apply(n => n.Normalized);

            FaceVertexAttributes[Property.Normals] = nia;
            FaceVertexAttributes[-Property.Normals] = na;

            return this;
        }

        /// <summary>
        /// Add tangents in u- and v direction <see href="http://www.3dkingdoms.com/weekly/weekly.php?a=37"/>.
        /// </summary>
        public void AddPerVertexTangents(
                Symbol coordinateName,
                Symbol uTangentsName,
                Symbol vTangentsName)
        {
            int fc = m_faceCount;
            var fia = m_firstIndexArray;
            var via = m_vertexIndexArray;
            var pa = m_positionArray;
            var ca = VertexAttributeArray<V2d>(coordinateName);

            var uta = new V3d[VertexCapacity];
            var vta = new V3d[VertexCapacity];

            for (int fvi = fia[0], fi = 0; fi < fc; fi++)
            {
                var vi0 = via[fvi++];  var p0 = pa[vi0]; var c0 = ca[vi0];
                var vi1 = via[fvi++];  var p1 = pa[vi1]; var c1 = ca[vi1];
                var v10 = p1 - p0; var c10 = c1 - c0; var v10len = v10.Length;

                for (int fve = fia[fi + 1]; fvi < fve; fvi++)
                {
                    var vi2 = via[fvi];  var p2 = pa[vi2]; var c2 = ca[vi2];

                    var v20 = p2 - p0; var c20 = c2 - c0; var v20len = v20.Length;

                    var t0 = V3d.Zero; var t1 = V3d.Zero;
                    double alpha0 = 0.0, alpha1 = 0.0, alpha2 = 0.0;

                    double d = c10.X * c20.Y - c20.X * c10.Y;
                    if (d != 0.0)
                    {
                        d = 1.0 / d;
                        t0 = ((v10 * c20.Y - v20 * c10.Y) * d).Normalized;
                        t1 = ((v20 * c10.X - v10 * c20.X) * d).Normalized;
                        var v21 = p2 - p1; var v21len = v21.Length;
                        if (v10len > Constant<double>.PositiveTinyValue
                            && v20len > Constant<double>.PositiveTinyValue
                            && v21len > Constant<double>.PositiveTinyValue)
                        {
                            alpha0 = Fun.Acos(V3d.Dot(v10, v20) / (v10len * v20len));
                            alpha1 = Fun.Acos(-V3d.Dot(v10, v21) / (v10len * v21len));
                            alpha2 = Fun.Acos(V3d.Dot(v20, v21) / (v20len * v21len));
                        }
                    }

                    uta[vi0] += t0 * alpha0; vta[vi0] += t1 * alpha0;
                    uta[vi1] += t0 * alpha1; vta[vi1] += t1 * alpha1;
                    uta[vi2] += t0 * alpha2; vta[vi2] += t1 * alpha2;

                    vi1 = vi2;  p1 = p2; c1 = c2;
                    v10 = v20; c10 = c20; v10len = v20len;
                }
            }

            uta.Apply(t => t.Normalized);
            vta.Apply(b => b.Normalized);

            if (uTangentsName.IsNotEmpty)
            {
                VertexAttributes[uTangentsName] = uta;
            }
            if (vTangentsName.IsNotEmpty)
            {
                VertexAttributes[vTangentsName] = vta;
            }
        }

        /// <summary>
        /// Add tangents in u- and v direction <see href="http://www.3dkingdoms.com/weekly/weekly.php?a=37"/>.
        /// </summary>
        public void AddPerFaceVertexTangents(
                Symbol coordinateName,
                Symbol uTangentsName,
                Symbol vTangentsName,
                int[] tangentIndexArray = null,
                int tangentCount = 0)
        {
            int fc = m_faceCount;
            var fia = m_firstIndexArray;
            var via = m_vertexIndexArray;
            var pa = m_positionArray;
            var cia = FaceVertexAttributeArray<int>(coordinateName);
            var ca = FaceVertexAttributeArray<V2d>(-coordinateName);

            var tia = tangentIndexArray;

            if (tia == null)
            {
                tia = cia;
                tangentCount = ca.Length;
            }

            if (tangentCount == 0)
                tangentCount = tia.Max() + 1;

            var uta = new V3d[tangentCount];
            var vta = new V3d[tangentCount];

            for (int fvi = fia[0], fi = 0; fi < fc; fi++)
            {
                var p0 = pa[via[fvi]]; var ci0 = cia[fvi]; var c0 = ca[ci0]; var ti0 = tia[fvi++];
                var p1 = pa[via[fvi]]; var ci1 = cia[fvi]; var c1 = ca[ci1]; var ti1 = tia[fvi++];
                var v10 = p1 - p0; var c10 = c1 - c0; var v10len = v10.Length;

                for (int fve = fia[fi + 1]; fvi < fve; fvi++)
                {
                    var p2 = pa[via[fvi]]; var ci2 = cia[fvi]; var c2 = ca[ci2]; var ti2 = tia[fvi];

                    var v20 = p2 - p0; var c20 = c2 - c0; var v20len = v20.Length;

                    var t0 = V3d.Zero; var t1 = V3d.Zero;
                    double alpha0 = 0.0, alpha1 = 0.0, alpha2 = 0.0;

                    double d = c10.X * c20.Y - c20.X * c10.Y;
                    if (d != 0.0)
                    {
                        d = 1.0 / d;
                        t0 = ((v10 * c20.Y - v20 * c10.Y) * d).Normalized;
                        t1 = ((v20 * c10.X - v10 * c20.X) * d).Normalized;
                        var v21 = p2 - p1; var v21len = v21.Length;
                        if (v10len > Constant<double>.PositiveTinyValue
                            && v20len > Constant<double>.PositiveTinyValue
                            && v21len > Constant<double>.PositiveTinyValue)
                        {
                            alpha0 = Fun.Acos(V3d.Dot(v10, v20) / (v10len * v20len));
                            alpha1 = Fun.Acos(-V3d.Dot(v10, v21) / (v10len * v21len));
                            alpha2 = Fun.Acos(V3d.Dot(v20, v21) / (v20len * v21len));
                        }
                    }

                    uta[ti0] += t0 * alpha0; vta[ti0] += t1 * alpha0;
                    uta[ti1] += t0 * alpha1; vta[ti1] += t1 * alpha1;
                    uta[ti2] += t0 * alpha2; vta[ti2] += t1 * alpha2;

                    p1 = p2; ci1 = ci2; c1 = c2; ti1 = ti2;
                    v10 = v20; c10 = c20; v10len = v20len;
                }
            }

            uta.Apply(t => t.Normalized);
            vta.Apply(b => b.Normalized);

            if (uTangentsName.IsNotEmpty)
            {
                FaceVertexAttributes[uTangentsName] = tia;
                FaceVertexAttributes[-uTangentsName] = uta;
            }
            if (vTangentsName.IsNotEmpty)
            {
                FaceVertexAttributes[vTangentsName] = tia;
                FaceVertexAttributes[-vTangentsName] = vta;
            }
        }


        #endregion
    }

    #region PolyMesh Extensions

    public static partial class PolyMeshExtensions
    {
        #region Collections

        public static Box3d GetBoundingBox3d(this IEnumerable<PolyMesh> polyMeshes)
            => new Box3d(polyMeshes.Select(m => m.BoundingBox3d));

        public static PolyMesh[] Transformed(
                this PolyMesh[] polyMeshArray, Trafo3d trafo)
            => polyMeshArray.Map(m => m.Transformed(trafo));

        public static List<PolyMesh> Transformed(
                this List<PolyMesh> polyMeshList, Trafo3d trafo)
            => polyMeshList.Map(m => m.Transformed(trafo));

        public static SymbolDict<Array> ToSymbolDict(
                this IEnumerable<PolyMesh.IAttribute> attributes)
        {
            var dict = new SymbolDict<Array>();

            foreach (var a in attributes)
            {
                if (a.Name == default(Symbol)) continue;
                if (a == null) continue;
                if (a.IndexArray == null)
                {
                    dict[a.Name] = a.ValueArray;
                }
                else
                {
                    dict[a.Name] = a.IndexArray;
                    dict[-a.Name] = a.ValueArray;
                }
            }

            return dict;
        }

        #endregion

        #region Outlines

        public static IEnumerable<Line3d> GetLines(this PolyMesh polyMesh)
            => polyMesh.WithNewTopology().Edges.Select(e => new Line3d(e.FromVertex.Position, e.ToVertex.Position)).ToArray();
        
        public static IEnumerable<Line3d> GetOutlines(this PolyMesh polyMesh)
            => polyMesh.Polygons.GetOutlines();

        public static IEnumerable<Line3d> GetOutlines(
                this IEnumerable<PolyMesh.Polygon> polygons)
        {
            var lines = polygons.SelectMany(p => p.Lines);
            return RemoveDuplicatedLines(lines);
        }

        private static IEnumerable<Line3d> RemoveDuplicatedLines(IEnumerable<Line3d> lines)
        {
            var linePairs = lines.Pairs(true, true);

            var result = new HashSet<Line3d>();
            lines.ForEach(x => result.Add(x));

            var clippedLines = new List<Line3d>();

            foreach (var x in linePairs)
            {
                var line1 = x.Item1;
                var line2 = x.Item2;

                if ((line1.P0 == line2.P0 && line1.P1 == line2.P1) ||
                    (line1.P0 == line2.P1 && line1.P1 == line2.P0))
                {
                    result.Remove(line1);
                    result.Remove(line2);
                }
            }

            return result.AsEnumerable();
        }

        public static IEnumerable<Line3d> ComputeOutline(this PolyMesh pSet)
            => pSet.Polygons.ComputeOutline();

        public static IEnumerable<Line3d> ComputeOutline(
                this IEnumerable<PolyMesh.Polygon> polygons)
        {
            var lines = polygons.SelectMany(p => p.Lines);
            return ComputeOutline(lines);
        }

        public static IEnumerable<Line3d> ComputeOutline(
                this IEnumerable<Line3d> lines)
        {
            var array = lines.ToArray();
            var removed = new HashSet<Line3d>();

            for (int i = 0; i < array.Length; i++)
            {
                for (int j = 0; j < array.Length; j++)
                {
                    if (i == j)
                        continue;

                    var a = array[i];
                    var b = array[j];

                    if (removed.Contains(a) || removed.Contains(b))
                        continue;
                    
                    var overlapped = RemoveOverlappingSegments(a, b, out Line3d[] segments);

                    if (overlapped)
                    {
                        // completely overlapped
                        if (segments.Length == 0)
                        {
                            removed.Add(a);
                            removed.Add(b);
                        }
                        else
                        {
                            // is it a point? if yes, remove; if not, replace the line with the segment
                            if (segments[0].P0 == segments[0].P1)
                                removed.Add(a);
                            else
                                array[i] = segments[0];

                            if (segments[1].P0 == segments[1].P1)
                                removed.Add(b);
                            else
                                array[j] = segments[1];
                        }
                    }
                }
            }

            var result = array.Where(x => removed.Contains(x) == false).ToArray();
            return result;
        }

        /// <summary>
        /// Returns whether new line segments have been created plus the resulting lines (or the 
        /// unchanged lines).
        /// </summary>
        private static bool RemoveOverlappingSegments(Line3d a, Line3d b, out Line3d[] lines)
        {
            var epsilon = 0.000000001d;
            var aDirNorm = a.Direction.Normalized;
            var bDirNorm = b.Direction.Normalized;

            if (V3d.ApproxEqual(aDirNorm, bDirNorm.Negated, epsilon))
            {
                b = new Line3d(b.P1, b.P0);
                bDirNorm = bDirNorm * -1;
            }

            // different directions
            if ((aDirNorm - bDirNorm).Length > epsilon)
            {
                lines = new[] { a, b };
                return false;
            }

            // completely overlapping
            if (V3d.ApproxEqual(a.P0, b.P0, epsilon) && V3d.ApproxEqual(a.P1, b.P1, epsilon))
            {
                lines = new Line3d[] { };
                return true;
            }

            // end points touch
            if (V3d.ApproxEqual(a.P1, b.P0, epsilon) || V3d.ApproxEqual(a.P0, b.P1, epsilon))
            {
                lines = new[] { a, b };
                return false;
            }

            var a0_on_b = a.P0.GetClosestPointOn(b);
            var a1_on_b = a.P1.GetClosestPointOn(b);
            var b0_on_a = b.P0.GetClosestPointOn(a);
            var b1_on_a = b.P1.GetClosestPointOn(a);

            var a0_on_b_dist = (a0_on_b - a.P0).Length;
            var a1_on_b_dist = (a1_on_b - a.P1).Length;
            var b0_on_a_dist = (b0_on_a - b.P0).Length;
            var b1_on_a_dist = (b1_on_a - b.P1).Length;

            // a lies in b
            if ((a0_on_b_dist.Abs() < epsilon) &&
                (a1_on_b_dist.Abs() < epsilon))
            {
                lines = new[]
                {
                    new Line3d(b.P0, a0_on_b),
                    new Line3d(a1_on_b, b.P1),
                };
                return true;
            }

            // b lies in a
            if ((b0_on_a_dist.Abs() < epsilon) &&
                (b1_on_a_dist.Abs() < epsilon))
            {
                lines = new[]
                {
                    new Line3d(a.P0, b0_on_a),
                    new Line3d(b1_on_a, a.P1),
                };
                return true;
            }

            // a starts in b, but is longer
            if ((a0_on_b_dist.Abs() < epsilon))
            {
                lines = new[]
                {
                    new Line3d(b.P0, a0_on_b),
                    new Line3d(b.P1, a.P1),
                };
                return true;
            }

            // b starts in a, but is longer
            if ((b0_on_a_dist.Abs() < epsilon))
            {
                lines = new[]
                {
                    new Line3d(a.P0, b0_on_a),
                    new Line3d(a.P1, b.P1),
                };
                return true;
            }

            lines = new[] { a, b };
            return false;
        }

        #endregion

        #region Coordinates

        public static void NormalizeUVs(
                this PolyMesh m, Symbol property = default(Symbol)
            )
        {
            if (property == default(Symbol))
                property = PolyMesh.Property.DiffuseColorCoordinates;
            var coords = m.VertexAttributeArray<V2d>(property);
            var box = new Box2d(m.VertexIndexArray.Select(vi => coords[vi]));
            var scale = 1.0 / (box.Max - box.Min);
            for (int i = 0; i < coords.Length; i++)
                coords[i] = (coords[i] - box.Min) * scale;
        }

        #endregion

        /// <summary>
        /// Remove the named attribute (including its optional index array) from the mesh. 
        /// Checking for Instance-, Vertex-, Face- and FaceVertexAttributes.
        /// NOTE: The returned mesh is a modified copy if an attribute has been removed, else the original.
        /// </summary>
        public static PolyMesh WithoutAttribute(this PolyMesh mesh, Symbol attributeName)
        {
            PolyMesh copy = null;

            if (mesh.InstanceAttributes.Contains(attributeName))
            {
                if (copy == null) copy = new PolyMesh(mesh);
                copy.InstanceAttributes = mesh.InstanceAttributes.Copy();
                copy.InstanceAttributes.Remove(attributeName);
                copy.InstanceAttributes.Remove(-attributeName);
            }

            if (mesh.VertexAttributes.Contains(attributeName))
            {
                if (copy == null) copy = new PolyMesh(mesh);
                copy.VertexAttributes = mesh.VertexAttributes.Copy();
                copy.VertexAttributes.Remove(attributeName);
                copy.VertexAttributes.Remove(-attributeName);
            }

            if (mesh.FaceAttributes.Contains(attributeName))
            {
                if (copy == null) copy = new PolyMesh(mesh);
                copy.FaceAttributes = mesh.FaceAttributes.Copy();
                copy.FaceAttributes.Remove(attributeName);
                copy.FaceAttributes.Remove(-attributeName);
            }

            if (mesh.FaceVertexAttributes.Contains(attributeName))
            { 
                if (copy == null) copy = new PolyMesh(mesh);
                copy.FaceVertexAttributes = mesh.FaceVertexAttributes.Copy();
                copy.FaceVertexAttributes.Remove(attributeName);
                copy.FaceVertexAttributes.Remove(-attributeName);
            }

            return copy;
        }
    }

    #endregion

    #region PolyMeshSet

    [RegisterTypeInfo]
    public class PolyMeshSet : SymMap
    {
        public static readonly Symbol Identifier = "PolyMeshSet";

        public static class Property
        {
            public static readonly Symbol MeshArray = "MeshArray";
        }

        public PolyMeshSet()
            : base(Identifier)
        { }

        public PolyMesh[] MeshArray
        {
            get { return m_ht.GetAs<PolyMesh[]>(Property.MeshArray); }
            set { this[Property.MeshArray] = value; }
        }
    }
    
    #endregion
}
