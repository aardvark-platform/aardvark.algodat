using Aardvark.Base;
using Aardvark.Rendering;
using System;

namespace Aardvark.Geometry
{
    public static class PolyLineSetIndexedGeometryExtensions
    {
        /// <summary>
        /// Converts to an indexed IndexedGeometryMode.LineList over the shared vertex array.
        /// LineList rather than LineStrip because one set holds several disjoint polylines and the thick-line shaders of Aardvark.Rendering expect a line list.
        /// Vertex attributes are passed through with V3d arrays converted to V3f; line and line-vertex attributes have no per-vertex layout and are not included.
        /// Returns null for a set without polylines.
        /// </summary>
        public static IndexedGeometry GetIndexedGeometry(this PolyLineSet lineSet)
        {
            if (lineSet.PolyLineCount == 0)
                return null;

            var attributes = new SymbolDict<Array>();
            foreach (var kvp in lineSet.VertexAttributes)
                attributes[kvp.Key] = ToFloatVectors(kvp.Value);
            attributes[DefaultSemantic.Positions] = ToFloatVectors(lineSet.VertexArray);

            var instanceAttributes = new SymbolDict<object>();
            foreach (var kvp in lineSet.InstanceAttributes)
                instanceAttributes[kvp.Key] = kvp.Value;

            return new IndexedGeometry
            {
                Mode = IndexedGeometryMode.LineList,
                IndexArray = lineSet.BuildLineListIndexArray(),
                IndexedAttributes = attributes,
                SingleAttributes = instanceAttributes,
            };
        }

        /// <summary>
        /// As <see cref="GetIndexedGeometry(PolyLineSet)"/>; with resolveLineAttributes the line and line-vertex attributes are first moved to the vertices
        /// by <see cref="PolyLineSet.WithResolvedLineAttributes"/>, which duplicates only the vertices referenced with more than one value, so per-polyline colours render.
        /// </summary>
        public static IndexedGeometry GetIndexedGeometry(this PolyLineSet lineSet, bool resolveLineAttributes)
            => (resolveLineAttributes ? lineSet.WithResolvedLineAttributes() : lineSet).GetIndexedGeometry();

        private static Array ToFloatVectors(Array array) => array is V3d[] vectors ? vectors.Map(v => v.ToV3f()) : array;
    }
}
