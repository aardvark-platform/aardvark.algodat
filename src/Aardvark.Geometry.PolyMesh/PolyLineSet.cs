using Aardvark.Base;
using Aardvark.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry
{
    /// <summary>
    /// A set of polylines sharing one vertex array, laid out like the faces of a PolyMesh:
    /// <see cref="FirstIndexArray"/> holds the start of each polyline in <see cref="VertexIndexArray"/> plus a final entry with the total index count.
    /// Gaps are separate polylines, vertex re-use is a repeated index.
    /// </summary>
    public class PolyLineSet : IBoundingBox3d
    {
        /// <summary>Positions of all vertices, referenced through <see cref="VertexIndexArray"/>.</summary>
        public V3d[] VertexArray;

        /// <summary>Vertex indices of all polylines packed together, without end markers.</summary>
        public int[] VertexIndexArray;

        /// <summary>PolyLineCount + 1 entries, the start of each polyline in <see cref="VertexIndexArray"/>; the last entry is the total index count.</summary>
        public int[] FirstIndexArray;

        /// <summary>Attributes with one entry per vertex, parallel to <see cref="VertexArray"/>.</summary>
        public SymbolDict<Array> VertexAttributes = new SymbolDict<Array>();

        /// <summary>Attributes with one entry per polyline.</summary>
        public SymbolDict<Array> LineAttributes = new SymbolDict<Array>();

        /// <summary>Attributes with one entry per polyline vertex, parallel to <see cref="VertexIndexArray"/>.</summary>
        public SymbolDict<Array> LineVertexAttributes = new SymbolDict<Array>();

        /// <summary>Attributes of the whole set.</summary>
        public SymbolDict<object> InstanceAttributes = new SymbolDict<object>();

        public static class Property
        {
            public static readonly Symbol Positions = DefaultSemantic.Positions;
            public static readonly Symbol Normals = DefaultSemantic.Normals;
            public static readonly Symbol Colors = DefaultSemantic.Colors;
        }

        #region Construction

        /// <summary>
        /// Builds a set with one run of fresh vertices per polyline, no vertex sharing.
        /// Polylines with fewer than two points are dropped.
        /// </summary>
        public static PolyLineSet FromPolyLines(IEnumerable<IReadOnlyList<V3d>> polyLines)
        {
            if (polyLines == null)
                throw new ArgumentNullException(nameof(polyLines));

            var vertices = new List<V3d>();
            var firstIndices = new List<int> { 0 };
            foreach (var polyLine in polyLines)
            {
                if (polyLine == null || polyLine.Count < 2)
                    continue;
                vertices.AddRange(polyLine);
                firstIndices.Add(vertices.Count);
            }

            if (firstIndices.Count < 2)
                return new PolyLineSet();

            return new PolyLineSet
            {
                VertexArray = vertices.ToArray(),
                VertexIndexArray = Enumerable.Range(0, vertices.Count).ToArray(),
                FirstIndexArray = firstIndices.ToArray(),
            };
        }

        /// <summary>
        /// Builds a set over a shared vertex array from index runs.
        /// Polylines with fewer than two indices are dropped, an index outside the vertex array throws.
        /// </summary>
        public static PolyLineSet FromIndexed(V3d[] vertices, IEnumerable<IReadOnlyList<int>> polyLineIndices)
        {
            if (vertices == null)
                throw new ArgumentNullException(nameof(vertices));
            if (polyLineIndices == null)
                throw new ArgumentNullException(nameof(polyLineIndices));

            var vertexIndices = new List<int>();
            var firstIndices = new List<int> { 0 };
            foreach (var indices in polyLineIndices)
            {
                if (indices == null || indices.Count < 2)
                    continue;
                foreach (var index in indices)
                {
                    if (index < 0 || index >= vertices.Length)
                        throw new ArgumentOutOfRangeException(nameof(polyLineIndices), $"vertex index {index} outside [0, {vertices.Length})");
                    vertexIndices.Add(index);
                }
                firstIndices.Add(vertexIndices.Count);
            }

            if (firstIndices.Count < 2)
                return new PolyLineSet();

            return new PolyLineSet
            {
                VertexArray = vertices,
                VertexIndexArray = vertexIndices.ToArray(),
                FirstIndexArray = firstIndices.ToArray(),
            };
        }

        #endregion

        #region Counts

        public int PolyLineCount => FirstIndexArray != null && FirstIndexArray.Length > 1 ? FirstIndexArray.Length - 1 : 0;

        public int VertexCount => VertexArray != null ? VertexArray.Length : 0;

        /// <summary>Number of polyline vertices over all polylines, the used length of <see cref="VertexIndexArray"/>.</summary>
        public int VertexIndexCount => PolyLineCount > 0 ? FirstIndexArray[PolyLineCount] : 0;

        /// <summary>Number of line segments over all polylines.</summary>
        public int SegmentCount
        {
            get
            {
                var count = 0;
                for (var pi = 0; pi < PolyLineCount; pi++)
                    count += GetSegmentCount(pi);
                return count;
            }
        }

        public int GetVertexCount(int polyLineIndex) => FirstIndexArray[polyLineIndex + 1] - FirstIndexArray[polyLineIndex];

        public int GetSegmentCount(int polyLineIndex) => Math.Max(0, GetVertexCount(polyLineIndex) - 1);

        #endregion

        #region Queries

        /// <summary>Positions of one polyline in order.</summary>
        public IEnumerable<V3d> GetPolyLine(int polyLineIndex)
        {
            var end = FirstIndexArray[polyLineIndex + 1];
            for (var i = FirstIndexArray[polyLineIndex]; i < end; i++)
                yield return VertexArray[VertexIndexArray[i]];
        }

        /// <summary>Segments of one polyline in order.</summary>
        public IEnumerable<Line3d> GetSegments(int polyLineIndex)
        {
            var end = FirstIndexArray[polyLineIndex + 1] - 1;
            for (var i = FirstIndexArray[polyLineIndex]; i < end; i++)
                yield return new Line3d(VertexArray[VertexIndexArray[i]], VertexArray[VertexIndexArray[i + 1]]);
        }

        /// <summary>Segments of all polylines, polyline by polyline.</summary>
        public IEnumerable<Line3d> Segments
        {
            get
            {
                for (var pi = 0; pi < PolyLineCount; pi++)
                    foreach (var segment in GetSegments(pi))
                        yield return segment;
            }
        }

        /// <summary>
        /// Two vertex indices per segment, the layout of IndexedGeometryMode.LineList, so all polylines render in one draw call.
        /// </summary>
        public int[] BuildLineListIndexArray()
        {
            var result = new int[SegmentCount * 2];
            var ri = 0;
            for (var pi = 0; pi < PolyLineCount; pi++)
            {
                var end = FirstIndexArray[pi + 1] - 1;
                for (var i = FirstIndexArray[pi]; i < end; i++)
                {
                    result[ri++] = VertexIndexArray[i];
                    result[ri++] = VertexIndexArray[i + 1];
                }
            }
            return result;
        }

        /// <summary>Bounding box of the vertices referenced by a polyline; unreferenced vertices are ignored.</summary>
        public Box3d BoundingBox3d
        {
            get
            {
                var box = Box3d.Invalid;
                var count = VertexIndexCount;
                for (var i = 0; i < count; i++)
                    box.ExtendBy(VertexArray[VertexIndexArray[i]]);
                return box;
            }
        }

        #endregion

        #region Transformation

        /// <summary>
        /// Transforms positions and the <see cref="Property.Normals"/> attribute of each attribute dictionary; index arrays and all other attributes are shared with this set.
        /// </summary>
        public PolyLineSet Transformed(Trafo3d trafo)
        {
            var forward = trafo.Forward;
            var backward = trafo.Backward;
            return new PolyLineSet
            {
                VertexArray = VertexArray?.Map(p => forward.TransformPos(p)),
                VertexIndexArray = VertexIndexArray,
                FirstIndexArray = FirstIndexArray,
                VertexAttributes = WithTransformedNormals(VertexAttributes, backward),
                LineAttributes = WithTransformedNormals(LineAttributes, backward),
                LineVertexAttributes = WithTransformedNormals(LineVertexAttributes, backward),
                InstanceAttributes = InstanceAttributes,
            };
        }

        private static SymbolDict<Array> WithTransformedNormals(SymbolDict<Array> attributes, M44d backward)
        {
            if (!attributes.TryGetValue(Property.Normals, out Array normals))
                return attributes;

            Array transformed;
            if (normals is V3d[] normalsD)
                transformed = normalsD.Map(n => backward.TransposedTransformDir(n).Normalized);
            else if (normals is V3f[] normalsF)
                transformed = normalsF.Map(n => backward.TransposedTransformDir(n.ToV3d()).Normalized.ToV3f());
            else
            {
                Report.Warn($"[PolyLineSet] Cannot transform normals of type {normals.GetType()}.");
                return attributes;
            }

            var result = new SymbolDict<Array>();
            foreach (var kvp in attributes)
                result[kvp.Key] = kvp.Value;
            result[Property.Normals] = transformed;
            return result;
        }

        #endregion
    }
}
