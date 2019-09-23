/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry
{
    public static class PolyMeshIndexedGeometryExtensions
    {
        #region GetIndexedGeometry

        public static IndexedGeometry GetIndexedGeometry(
                this PolyMesh mesh,
                PolyMesh.GetGeometryOptions options = PolyMesh.GetGeometryOptions.Default,
                double absoluteEps = 10e-6)
        {
            return mesh.GetIndexedGeometry(mesh.FaceCount.Range(), options, absoluteEps);
        }

        public static IndexedGeometry GetIndexedGeometry(
                this PolyMesh mesh,
                IEnumerable<int> faceIndices,
                PolyMesh.GetGeometryOptions options = PolyMesh.GetGeometryOptions.Default,
                double absoluteEps = 10e-6
                )
        {
            var faceAttributes = mesh.FaceAttributes.Keys.Where(k => k.IsPositive).ToArray();
            var vertexAttributes = mesh.VertexAttributes.Keys.Where(k => k.IsPositive).ToArray();
            var faceVertexAttributes = mesh.FaceVertexAttributes.Keys.Where(k => k.IsPositive).ToArray();
            var instanceAttributes = mesh.InstanceAttributes.Keys.ToArray();

            return mesh.GetIndexedGeometry(options, absoluteEps,
                    faceIndices, faceAttributes, vertexAttributes, faceVertexAttributes,
                    instanceAttributes);
        }

        public static IndexedGeometry GetIndexedGeometry(
                this PolyMesh mesh,
                PolyMesh.GetGeometryOptions options,
                double absoluteEps,
                IEnumerable<int> faceIndices,
                IEnumerable<Symbol> faceAttributeNames,
                IEnumerable<Symbol> vertexAttributeNames,
                IEnumerable<Symbol> faceVertexAttributeNames,
                IEnumerable<Symbol> instanceAttributeNames)
        {
            var faceBackMap = faceIndices.ToArray();
            var faceAttributeArray
                    = (from a in faceAttributeNames
                       select PolyMesh.GetIAttribute(a, mesh.FaceAttributes)).WhereNotNull().ToArray();
            var vertexAttributeArray
                    = (from a in vertexAttributeNames
                       select PolyMesh.GetIAttribute(a, mesh.VertexAttributes)).WhereNotNull().ToArray();
            var faceVertexAttributeArray
                    = (from a in faceVertexAttributeNames
                       select PolyMesh.GetIAttribute(a, mesh.FaceVertexAttributes)).WhereNotNull().ToArray();

            var instanceAttributes = new SymbolDict<object>();
            foreach (var name in instanceAttributeNames)
                instanceAttributes[name] = mesh.InstanceAttributes[name];

            return mesh.GetIndexedGeometry(
                        options,
                        absoluteEps,
                        faceBackMap,
                        faceAttributeArray,
                        vertexAttributeArray,
                        faceVertexAttributeArray,
                        instanceAttributes
                        );
        }

        public static IndexedGeometry GetIndexedGeometry(
                this PolyMesh mesh,
                PolyMesh.GetGeometryOptions options,
                double absoluteEps,
                int[] faceBackMap,
                PolyMesh.IAttribute[] faceAttributes,
                PolyMesh.IAttribute[] vertexAttributes,
                PolyMesh.IAttribute[] faceVertexAttributes,
                SymbolDict<object> instanceAttributes)
        {
            mesh.ComputeVertexBackMaps(faceBackMap,
                    (options & PolyMesh.GetGeometryOptions.Compact) != 0,
                    faceAttributes, vertexAttributes, faceVertexAttributes,
                    out int[] firstIndexArray, out int[] vertexIndexArray,
                    out int[] vBackMap, out int[] vfBackMap, out int[] vfvBackMap);

            var indexedAttributes = new SymbolDict<Array>();

            var vc = vBackMap.Length;

            if ((options & PolyMesh.GetGeometryOptions.FloatVectorsAndByteColors) != 0)
            {
                var fv = (options & PolyMesh.GetGeometryOptions.FloatVectors) != 0;
                var bc = (options & PolyMesh.GetGeometryOptions.ByteColors) != 0;
                foreach (var a in faceAttributes)
                {
                    var array = a.BackMappedConvertedCopy(vfBackMap, vc, fv, bc);
                    if (array != null) indexedAttributes[a.Name] = array;
                }
                foreach (var a in vertexAttributes)
                {
                    var array = a.BackMappedConvertedCopy(vBackMap, vc, fv, bc);
                    if (array != null) indexedAttributes[a.Name] = array;
                }
                foreach (var a in faceVertexAttributes)
                {
                    var array = a.BackMappedConvertedCopy(vfvBackMap, vc, fv, bc);
                    if (array != null) indexedAttributes[a.Name] = array;
                }
            }
            else
            {
                foreach (var a in vertexAttributes)
                {
                    var array = a.BackMappedCopy(vBackMap, vc);
                    if (array != null) indexedAttributes[a.Name] = array;
                }
                foreach (var a in faceAttributes)
                {
                    var array = a.BackMappedCopy(vfBackMap, vc);
                    if (array != null) indexedAttributes[a.Name] = array;
                }
                foreach (var a in faceVertexAttributes)
                {
                    var array = a.BackMappedCopy(vfvBackMap, vc);
                    if (array != null) indexedAttributes[a.Name] = array;
                }
            }

            var triangleIndices =
                    PolyMesh.CreateSimpleTriangleVertexIndexArray(
                        firstIndexArray, vertexIndexArray, faceBackMap.Length,
                        vertexIndexArray.Length, mesh.FaceVertexCountRange);

            var indexArray =
                ((options & PolyMesh.GetGeometryOptions.IntIndices) != 0)
                    ? (Array)triangleIndices : (Array)triangleIndices.Map(i => (short)i);
                    
            return new IndexedGeometry(indexArray, indexedAttributes, instanceAttributes);
        }

        #endregion
    }
}
