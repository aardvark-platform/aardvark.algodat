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

namespace Aardvark.Geometry
{
    [RegisterTypeInfo]
    public partial class PolyLine
        : SymMap, IAwakeable, IBoundingBox2d
    {
        public static readonly Symbol Identifier = "PolyLine";

        // cached properties

        internal int m_vertexCount;
        internal bool m_hideLastVertex;
        internal V3d[] m_positionArray;

        internal SymbolDict<Array> m_vertexAttributes;
        internal SymbolDict<Array> m_lineAttributes;
        internal SymbolDict<object> m_instanceAttributes;

        #region Constructors

        public PolyLine()
            : this(Identifier)
        { }

        protected PolyLine(Symbol identifier)
            : base(identifier)
        {
            m_vertexCount = 0;
            m_hideLastVertex = false;
            m_positionArray = null;

            VertexAttributes = new SymbolDict<Array>();
            LineAttributes = new SymbolDict<Array>();
            InstanceAttributes = new SymbolDict<object>();
        }
        
        #endregion

        #region Properties

        public static class Property
        {
            public static readonly Symbol Vertices = Symbol.Create("Vertices");
            public static readonly Symbol Lines = Symbol.Create("Lines");
            public static readonly Symbol HideLastVertex = Symbol.Create("HideLastVertex");
            public static readonly Symbol Positions = Symbol.Create("Positions");
            public static readonly Symbol VertexCount = Symbol.Create("VertexCount");
            public static readonly Symbol VertexAttributes = Symbol.Create("VertexAttributes");
            public static readonly Symbol LineAttributes = Symbol.Create("LineAttributes");
            public static readonly Symbol InstanceAttributes = Symbol.Create("InstanceAttributes");
        }

        public bool HideLastVertex
        {
            get { return m_hideLastVertex; }
            set { this[Property.HideLastVertex] = m_hideLastVertex = value; }
        }

        public int VertexCount => m_vertexCount;

        public int VisibleVertexCount => m_hideLastVertex ? m_vertexCount - 1 : m_vertexCount;

        public int LineCount => m_vertexCount - 1;

        public V3d[] PositionArray
        {
            get { return m_positionArray; }
            set
            {
                m_positionArray = value;
                VertexAttributes[Property.Positions] = value;
                m_vertexCount = m_positionArray.Length;
                this[Property.VertexCount] = m_vertexCount;
            }
        }

        public SymbolDict<Array> VertexAttributes
        {
            get { return m_vertexAttributes; }
            set
            {
                this[Property.VertexAttributes] = m_vertexAttributes = value;
                m_positionArray = m_vertexAttributes.GetArray<V3d>(Property.Positions, null);
            }
        }

        public SymbolDict<Array> LineAttributes
        {
            get { return m_lineAttributes; }
            set { this[Property.LineAttributes] = m_lineAttributes = value; }
        }

        public SymbolDict<object> InstanceAttributes
        {
            get { return m_instanceAttributes; }
            set { this[Property.InstanceAttributes] = m_instanceAttributes = value; }
        }

        public T[] VertexAttributeArray<T>(Symbol name)
        {
            if (VertexAttributes.TryGetValue(name, out Array array)) return (T[])array;
            return null;
        }

        public T[] LineAttributeArray<T>(Symbol name)
        {
            if (LineAttributes.TryGetValue(name, out Array array)) return (T[])array;
            return null;
        }

        public IEnumerable<Vertex> Vertices
        {
            get
            {
                var vc = VertexCount;
                for (int vi = 0; vi < vc; vi++) yield return new Vertex(this, vi);
            }
        }

        public IEnumerable<Vertex> VisibleVertices
        {
            get
            {
                var vc = VisibleVertexCount;
                for (int vi = 0; vi < vc; vi++) yield return new Vertex(this, vi);
            }
        }

        public IEnumerable<Line> Lines
        {
            get
            {
                var lc = LineCount;
                for (int li = 0; li < lc; li++) yield return new Line(this, li);
            }
        }

        #endregion

        #region IBoundingBox2d Members

        public Box2d BoundingBox2d => throw new NotImplementedException();

        #endregion

        #region Vertex

        public struct Vertex : IEquatable<Vertex>
        {
            public readonly PolyLine PolyLine;
            public readonly int Index;

            #region Constructors

            public Vertex(PolyLine polyLine, int index)
            {
                PolyLine = polyLine;
                Index = index;
            }

            #endregion

            #region Operators

            public static bool operator ==(Vertex v0, Vertex v1)
            {
                return v0.PolyLine == v1.PolyLine && v0.Index == v1.Index;
            }

            public static bool operator !=(Vertex v0, Vertex v1)
            {
                return v0.PolyLine != v1.PolyLine || v0.Index != v1.Index;
            }

            #endregion

            #region Overrides

            public override int GetHashCode()
            {
                return HashCode.Combine(PolyLine.GetHashCode(), Index);
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

        #region Line

        public struct Line : IEquatable<Line>
        {
            public readonly PolyLine PolyLine;
            public readonly int Index;

            #region Constructors

            public Line(PolyLine polyLine, int index)
            {
                PolyLine = polyLine;
                Index = index;
            }

            #endregion

            #region Operators

            public static bool operator ==(Line l0, Line l1)
            {
                return l0.PolyLine == l1.PolyLine && l0.Index == l1.Index;
            }

            public static bool operator !=(Line l0, Line l1)
            {
                return l0.PolyLine != l1.PolyLine || l0.Index != l1.Index;
            }

            #endregion

            #region Overrides

            public override int GetHashCode()
            {
                return HashCode.Combine(PolyLine.GetHashCode(), Index);
            }

            public override bool Equals(object obj)
            {
                return obj is Vertex ? this == (Line)obj : false;
            }

            #endregion

            #region IEquatable<Line> Members

            public bool Equals(Line other)
            {
                return this == other;
            }

            #endregion
        }

        #endregion

        #region IAwakeable Members

        public void Awake(int codedVersion)
        {
            m_vertexCount = Get<int>(Property.VertexCount);
            m_hideLastVertex = Get<bool>(Property.HideLastVertex);
            m_vertexAttributes = Get<SymbolDict<Array>>(Property.VertexAttributes);
            m_lineAttributes = Get<SymbolDict<Array>>(Property.LineAttributes);
            m_instanceAttributes = Get<SymbolDict<object>>(Property.InstanceAttributes);

            // use proerty assignment to initialize dependent fields
            PositionArray = VertexAttributeArray<V3d>(Property.Positions);
        }

        #endregion
    }
}
