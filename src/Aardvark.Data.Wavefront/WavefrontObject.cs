using Aardvark.Base;
using Aardvark.Geometry;
using FSharp.Data.Adaptive;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Data.Wavefront
{
    public class WavefrontObject
    {
        public static class Property
        {
            public static readonly Symbol Vertices = "v";
            public static readonly Symbol Normals = "vn";
            public static readonly Symbol TextureCoordinates = "vt";
            public static readonly Symbol ControlPoints = "vp";

            public static readonly Symbol Materials = "usemtl";
            public static readonly Symbol Groups = "g";

            public static readonly Symbol PointSets = "p";
            public static readonly Symbol LineSets = "l";
            public static readonly Symbol FaceSets = "f";
        }

        public class ElementSet
        {
            public int GroupIndex;
            public List<int> VertexIndices = new List<int>();
            public List<int> FirstIndices = new List<int>(0.IntoIEnumerable());
            public List<int> MaterialIndices = new List<int>();

            public int ElementCount
            {
                get { return FirstIndices.Count - 1; }
            }
        }

        public class PointSet : ElementSet
        {
        }

        public class LineSet : ElementSet
        {
            /// <summary>
            /// Min and max number of vertices per line strip
            /// </summary>
            public Range1i VertexCountRange = Range1i.Invalid;
            public List<int> TexCoordIndices = new List<int>();
        }

        public class FaceSet : ElementSet
        {
            /// <summary>
            /// Min and max number of vertices per face
            /// </summary>
            public Range1i FaceVertexCountRange = Range1i.Invalid;
            public List<int> NormalIndices = new List<int>();
            public List<int> TexCoordIndices = new List<int>();
            public List<int> SmoothingGroupIndex = new List<int>(); // Unused at the moment
        }

        public List<PointSet> PointsSets { get; } = new List<PointSet>();

        public List<LineSet> LineSets { get; } = new List<LineSet>();

        public List<FaceSet> FaceSets { get; } = new List<FaceSet>();

        public List<string> Groups { get; } = new List<string>();

        public List<WavefrontMaterial> Materials { get; } = new List<WavefrontMaterial>();

        public bool DoublePrecisionVertices { get; }

        /// <summary>
        /// Vertices either in Single or Double precision (V4f or V4d)
        /// </summary>
        public IList Vertices { get; }

        public List<V3f> Normals { get; } = new List<V3f>();

        /// <summary>
        /// Optional attribute with same length as Vertices, is null of Obj file does not contain colors in form of unofficial extension
        /// </summary>
        public List<C3f> VertexColors { get; internal set; } // no colors by default

        public List<V3f> TextureCoordinates { get; } = new List<V3f>();

        public List<V3f> ControlPoints { get; } = new List<V3f>();

        /// <summary>
        /// Creates a WavefrontObject and initializes the Vertices depending on the parameter as floats or double (default)
        /// </summary>
        /// <param name="useDoublePositions"></param>
        public WavefrontObject(bool useDoublePositions = true)
        {
            if (useDoublePositions)
                Vertices = new List<V4d>();
            else
                Vertices = new List<V4f>();

            DoublePrecisionVertices = useDoublePositions;
        }
    }

    public static class WavefrontExtensions
    {
        /// <summary>
        /// Builds an enumeration of PolyMeshes as that represent the FaceSets in the Wavefront file.
        /// The Position attribute is converted to double vectors, as this is required for a PolyMesh. 
        /// Normals and TextureCoordinates can either remain float or be also converted to double.
        /// The default behavior is similar to PolyMeshFromVrml97: V3d Positions, V3f Normals, V2f TexCoords.
        /// All meshes point to the same data arrays with different First/VertexIndex arrays.
        /// </summary>
        public static IEnumerable<PolyMesh> GetFaceSetMeshes(this WavefrontObject obj, bool doubleAttributes = false)
        {
            var vertices = obj.DoublePrecisionVertices ?
                                ((List<V4d>)obj.Vertices).MapToArray(x => x.XYZ) :
                                ((List<V4f>)obj.Vertices).MapToArray(x => new V3d(x.XYZ));

            var vertexColors = obj.VertexColors?.ToArray();
            var normals = obj.Normals.Count > 0 ? doubleAttributes ?
                                obj.Normals.MapToArray(n => n.ToV3d()) : (Array)obj.Normals.ToArray() : null;
            var texCoords = obj.TextureCoordinates.Count > 0 ? doubleAttributes ?
                                obj.TextureCoordinates.MapToArray(v => v.XY.ToV2d()) : (Array)obj.TextureCoordinates.MapToArray(v => v.XY) : null;

            var materials = obj.Materials.Count > 0 ? obj.Materials.ToArray() : null;
            var groups = obj.Groups;

            foreach (var fs in obj.FaceSets)
            {
                var mesh = new PolyMesh();

                mesh.PositionArray = vertices;
                if (vertexColors != null)
                    mesh.VertexAttributes.Add(PolyMesh.Property.Colors, vertexColors);
                if (fs.GroupIndex >= 0)
                    mesh.InstanceAttributes.Add(PolyMesh.Property.Name, groups[fs.GroupIndex]);

                var fia = fs.FirstIndices.ToArray();
                var via = fs.VertexIndices.ToArray();

                var mIndices = fs.MaterialIndices;
                var nIndices = fs.NormalIndices;
                var tcIndices = fs.TexCoordIndices;

                mesh.FirstIndexArray = fia;
                mesh.VertexIndexArray = via;

                if (!materials.IsEmptyOrNull() && !mIndices.IsEmptyOrNull() && mIndices[0] >= 0) // NOTE: >= 0 check assumes that all faces have no normal indices, otherwise the mesh would need to be split 
                {
                    if (mIndices.Count != fs.ElementCount) throw new InvalidOperationException();
                    mesh.FaceAttributes.Add(PolyMesh.Property.Material, mIndices.ToArray());
                    mesh.FaceAttributes.Add(-PolyMesh.Property.Material, materials);
                }

                if (normals != null && normals.Length > 0 && !nIndices.IsEmptyOrNull() && nIndices[0] >= 0) // NOTE: >= 0 check assumes that all faces have no normal indices, otherwise the mesh would need to be split 
                {
                    if (nIndices.Count != via.Length) throw new InvalidOperationException();
                    mesh.FaceVertexAttributes.Add(PolyMesh.Property.Normals, nIndices.ToArray());
                    mesh.FaceVertexAttributes.Add(-PolyMesh.Property.Normals, normals);
                }

                if (texCoords != null && texCoords.Length > 0 && !tcIndices.IsEmptyOrNull() && tcIndices[0] >= 0) // NOTE: >= 0 check assumes that all faces have no normal indices, otherwise the mesh would need to be split 
                {
                    if (tcIndices.Count != via.Length) throw new InvalidOperationException();
                    mesh.FaceVertexAttributes.Add(PolyMesh.Property.DiffuseColorCoordinates, tcIndices.ToArray());
                    mesh.FaceVertexAttributes.Add(-PolyMesh.Property.DiffuseColorCoordinates, texCoords);
                }

                yield return mesh;
            }
        }

        //public static IEnumerable<IndexedGeometry> GetFaceSetGeometry(this WavefrontObject obj, bool simpleTriangulation = true)
        //{
        //    var vertices = obj.DoublePrecisionVertices ?
        //                        ((List<V4d>)obj.Vertices).MapToArray(x => new V3f(x.XYZ)) :
        //                        ((List<V4f>)obj.Vertices).MapToArray(x => x.XYZ);

        //    var vertexColors = obj.VertexColors?.ToArray();
        //    var normals = obj.Normals.Count > 0 ? (Array)obj.Normals.ToArray() : null;
        //    var texCoords = obj.TextureCoordinates.Count > 0 ? (Array)obj.TextureCoordinates.MapToArray(v => v.XY) : null;

        //    var materials = obj.Materials.Count > 0 ? obj.Materials.ToArray() : null;
        //    var groups = obj.Groups;

        //    foreach (var fs in obj.FaceSets)
        //    {
        //        var indexedAttributes = new SymbolDict<Array>();

        //        // TODO: attributes are PerFaceVertex -> needs to be convertex to PerVertex: create matching combination of vertex/normal/texCoord/color indices
        //        //  -> 1. create simple triangulation (seed PoylMesh.CreateSimpleTriangleVertexIndexArray) or non-concave polygon triangulation (only used if FaceVertexCountRange.Max > 4, see PolyMesh.TriangulatedCopy)
        //        //  -> 2. create PerVertex attributes (see PolyMesh.GetIndexGeometry)

        //        var fia = fs.FirstIndices.ToArray();
        //        var via = fs.VertexIndices.ToArray();

        //        var vertexIndexCount = via.Length; // faceBackMap.Length
        //        var indexArray = PolyMesh.CreateSimpleTriangleVertexIndexArray(fia, via, fs.ElementCount, vertexIndexCount, fs.FaceVertexCountRange);

        //        //var mIndices = fs.MaterialIndices;
        //        //var nIndices = fs.NormalIndices;
        //        //var tcIndices = fs.TexCoordIndices;

        //        //indexedAttributes.Add(DefaultSemantic.Positions, vertices);
        //        //if (vertexColors != null)
        //        //    indexAttributes.Add(DefaultSemantic.Colors, vertexColors);
        //        //if (normals != null)
        //        //    indexAttributes.Add(DefaultSemantic.Normals, normals);
        //        //if (texCoords != null)
        //        //    indexAttributes.Add(DefaultSemantic.DiffuseColorCoordinates, texCoords);

        //        // mateirals ??? TODO: materials -> need to create one return mutliple indexed geometries with different index offset/counts ?

        //        var singleAttributes = new SymbolDict<object>();
        //        if (fs.GroupIndex >= 0)
        //            singleAttributes.Add(DefaultSemantic.Name, groups[fs.GroupIndex]);

        //        yield return new IndexedGeometry(IndexedGeometryMode.TriangleList, indexArray, indexedAttributes, singleAttributes);
        //    }
        //}
    }
}
