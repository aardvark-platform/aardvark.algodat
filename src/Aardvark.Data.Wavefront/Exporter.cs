using Aardvark.Base;
using Aardvark.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Aardvark.Data.Wavefront
{
    public static class Exporter
    {
        public class ObjCoderState : IDisposable
        {
            string m_filename;
            string m_path;
            int m_vertexCount;
            int m_texCoordCount;
            int m_normalCount;
            int m_faceCount;
            StreamWriter m_geometryStream;
            StreamWriter m_materialStream;

            int m_geometryCount;

            public ObjCoderState(string fn)
            {
                m_filename = Path.GetFileNameWithoutExtension(fn);
                m_path = Path.GetDirectoryName(fn);

                var objFn = Path.Combine(m_path, m_filename + ".obj");
                var matFn = Path.Combine(m_path, m_filename + ".mtl");

                m_geometryStream = new StreamWriter(new FileStream(objFn, FileMode.Create));
                m_materialStream = new StreamWriter(new FileStream(matFn, FileMode.Create));

                m_geometryStream.WriteLine("mtllib {0}.mtl", m_filename);
            }

            static SymbolDict<string> s_materialEntries = new SymbolDict<string>()
            {
                { WavefrontMaterial.Property.AmbientColor, "Ka" },
                { WavefrontMaterial.Property.EmissiveColor, "Ke" },
                { WavefrontMaterial.Property.DiffuseColor, "Kd" },
                { WavefrontMaterial.Property.SpecularColor, "Ks" },
                { WavefrontMaterial.Property.SpecularExponent, "Ns" },
                { WavefrontMaterial.Property.TransmissionFilter, "Tf" },
                { WavefrontMaterial.Property.Opacity, "Tr" }, // or "d" with 1-Tr
                { WavefrontMaterial.Property.IlluminationModel, "illum" },
                { WavefrontMaterial.Property.Sharpness, "sharpness" },
                { WavefrontMaterial.Property.OpticalDensity, "Ni" },

                { WavefrontMaterial.Property.AmbientColorMap, "map_Ka" },
                { WavefrontMaterial.Property.DiffuseColorMap, "map_Kd" },
                { WavefrontMaterial.Property.SpecularColorMap, "map_Ks" },
                { WavefrontMaterial.Property.SpecularExponentMap, "map_Ns" },
                { WavefrontMaterial.Property.OpacityMap, "map_d" },
                { WavefrontMaterial.Property.DecalMap, "decal" },
                { WavefrontMaterial.Property.DisplacementMap, "disp" },
                { WavefrontMaterial.Property.BumpMap, "map_bump" }, // or "bump"
            };

            static Dictionary<Type, Func<object, string>> s_valueFormatters = new Dictionary<Type, Func<object, string>>()
            {
                { typeof(C3f), new Func<object, string>(v => String.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", ((C3f)v).R, ((C3f)v).G, ((C3f)v).B)) },
                { typeof(C3b), new Func<object, string>(v => String.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", ((C3b)v).R, ((C3b)v).G, ((C3b)v).B)) },
                { typeof(double), new Func<object, string>(v => String.Format(CultureInfo.InvariantCulture, "{0}", v)) },
                { typeof(float), new Func<object, string>(v => String.Format(CultureInfo.InvariantCulture, "{0}", v)) },
                { typeof(int), new Func<object, string>(v => String.Format(CultureInfo.InvariantCulture, "{0}", v)) },
                { typeof(string), new Func<object, string>(v => String.Format(CultureInfo.InvariantCulture, "{0}", v)) },
            };

            public void AppendMaterial(WavefrontMaterial material)
            {
                m_materialStream.WriteLine("newmtl {0}", material.Name);

                material.MapItems.ForEach(e =>
                {
                    string entryName;
                    if (s_materialEntries.TryGetValue(e.Key, out entryName))
                    {
                        string valueString = null;
                        Func<object, string> formatter;
                        if (s_valueFormatters.TryGetValue(e.Value.GetType(), out formatter))
                            valueString = formatter(e.Value);
                        else
                            Report.Warn("could not format entry {0} of type {1}", entryName, e.Value.GetType());
                        //valueString = e.ToString(); // might produce corrupted file

                        if (valueString != null)
                            m_materialStream.WriteLine("\t{0} {1}", entryName, valueString);
                    }
                });

                // append an empty line
                m_materialStream.WriteLine();
            }

            //public void AppendGeometry(VertexGeometry vg, string groupName = null, string materialName = null)
            //{
            //    vg = vg.ToIndexedVertexGeometry();
            //    var coords = vg.Positions as V3f[];
            //    var normals = vg.Normals as V3f[];
            //    var colors = vg.Colors as C4b[];
            //    var texCoord = vg.DiffuseColorCoordinates() as V2f[];
            //    var indices = vg.Indices as int[];
            //    //var tex = vg.DiffuseColorTexture();

            //    if (texCoord == null)
            //        texCoord = new V2f[coords.Length];
            //    if (normals == null)
            //        normals = new V3f[coords.Length];

            //    // write signatures of new geometry
            //    if (groupName != null)
            //        m_geometryStream.WriteLine("g {0}", groupName);
            //    if (materialName != null)
            //        m_geometryStream.WriteLine("usemtl {0}", materialName);

            //    // write geometry

            //    // (+1) for one base indices
            //    var baseVertexIndex = m_vertexCount + 1;
            //    var baseNormalIndex = m_normalCount + 1;
            //    var baseTexCoordIndex = m_texCoordCount + 1;

            //    coords.ForEach(v => m_geometryStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}", v.X, v.Y, v.Z)));
            //    m_vertexCount += coords.Length;

            //    if (normals != null)
            //    {
            //        normals.ForEach(vn => m_geometryStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "vn {0} {1} {2}", vn.X, vn.Y, vn.Z)));
            //        m_normalCount += normals.Length;
            //    }
            //    if (texCoord != null)
            //    {
            //        texCoord.ForEach(vt => m_geometryStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "vt {0} {1}", vt.X, 1 - vt.Y)));
            //        m_texCoordCount += texCoord.Length;
            //    }
            //    //if (colors != null) // not supported
            //    //    colors.Run(vc => m_geometryStream.WriteLine("vc {0} {1} {2} {3}", vc.R, vc.G, vc.B, vc.A));

            //    // base- vertex/tex/normal indices are the same
            //    for (int i = 0; i < vg.TriangleCount; i++)
            //    {
            //        var i0 = indices[i * 3 + 0];
            //        var i1 = indices[i * 3 + 1];
            //        var i2 = indices[i * 3 + 2];

            //        if (normals != null && texCoord != null)
            //        {
            //            m_geometryStream.WriteLine("f {0}/{1}/{2} {3}/{4}/{5} {6}/{7}/{8}",
            //                                        i0 + baseVertexIndex, i0 + baseNormalIndex, i0 + baseTexCoordIndex,
            //                                        i1 + baseVertexIndex, i1 + baseNormalIndex, i1 + baseTexCoordIndex,
            //                                        i2 + baseVertexIndex, i2 + baseNormalIndex, i2 + baseTexCoordIndex);
            //        }
            //        else
            //        {
            //            if (normals != null)
            //                m_geometryStream.WriteLine("f {0}/{1} {2}/{3} {4}/{5}",
            //                                        i0 + baseVertexIndex, i0 + baseNormalIndex,
            //                                        i1 + baseVertexIndex, i1 + baseNormalIndex,
            //                                        i2 + baseVertexIndex, i2 + baseNormalIndex);
            //            else if (texCoord != null)
            //                m_geometryStream.WriteLine("f {0}//{1} {2}//{3} {4}//{5}",
            //                                        i0 + baseVertexIndex, i0 + baseTexCoordIndex,
            //                                        i1 + baseVertexIndex, i1 + baseTexCoordIndex,
            //                                        i2 + baseVertexIndex, i2 + baseTexCoordIndex);
            //            else
            //                m_geometryStream.WriteLine("f {0} {1} {2}", i0 + baseVertexIndex, i1 + baseVertexIndex, i2 + baseVertexIndex);
            //        }
            //    }

            //    m_faceCount += vg.TriangleCount;

            //    m_geometryCount++;
            //}

            //public void AppendGeometry(ConcreteVertexGeometry cvg)
            //{
            //    var vg = cvg.TransformedVertexGeometry;
            //    var material = cvg.Surface as Instance;
            //    var materialName = string.Format("material{0}", m_geometryCount);
            //    AppendGeometry(vg, material, materialName);
            //}

            //public void AppendGeometry(VertexGeometry vg, Instance material, string groupName)
            //{
            //    vg = vg.ToIndexedVertexGeometry();
            //    var coords = vg.Positions as V3f[];
            //    var normals = vg.Normals as V3f[];
            //    var colors = vg.Colors as C4b[];
            //    var texCoord = vg.DiffuseColorCoordinates() as V2f[];
            //    var indices = vg.Indices as int[];
            //    //var tex = vg.DiffuseColorTexture();

            //    if (texCoord == null)
            //        texCoord = new V2f[coords.Length];
            //    if (normals == null)
            //        normals = new V3f[coords.Length];

            //    // write material
            //    m_materialStream.WriteLine("newmtl {0}", groupName);

            //    C3f diffuse = C3f.Gray80;
            //    C3f ambient = C3f.Gray20;
            //    C3f specular = C3f.Black;
            //    float opacity = 1;
            //    float shinyness = 0;


            //    if (material != null)
            //    {
            //        //object temp;
            //        //if (material.TryGetValue(SgOld.DefaultSurface.Property.DiffuseColor, out temp) && temp is C4f)
            //        //{
            //        //    diffuse = ((C4f)temp).ToC3f();
            //        //    opacity = ((C4f)temp).A;
            //        //}

            //        //if (material.TryGetValue(SgOld.DefaultSurface.Property.AmbientColor, out temp) && temp is C4f)
            //        //    ambient = ((C4f)temp).ToC3f();

            //        //if (material.TryGetValue(SgOld.DefaultSurface.Property.SpecularColor, out temp) && temp is C4f)
            //        //    specular = ((C4f)temp).ToC3f();

            //        //if (material.TryGetValue(SgOld.DefaultSurface.Property.Shininess, out temp) && temp is double)
            //        //    shinyness = (float)((double)temp);
            //    }

            //    if (colors != null)
            //    {
            //        var vertexColor = colors[0].ToC4f();
            //        diffuse = (C3f)(diffuse.ToV3f() * vertexColor.ToV3f());
            //        opacity *= vertexColor.A;
            //    }

            //    m_materialStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "Ka {0} {1} {2}", ambient.R, ambient.G, ambient.B));
            //    m_materialStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "Kd {0} {1} {2}", diffuse.R, diffuse.G, diffuse.B));

            //    //if (tex != null)
            //    //{
            //    //    var texFilename = string.Format("vgtexture{0}.png", m_geometryCount);
            //    //    m_materialStream.WriteLine("map_Kd {0}", texFilename);

            //    //    var convertible = PixImageSetConvertible.Create();
            //    //    tex.Convertible.ConvertInto(convertible);
            //    //    convertible.PixImagesFromConvertible().First().SaveAsImage(Path.Combine(m_path, texFilename));
            //    //    //tex.Convertible.ConvertInto(BitmapConvertible.CreateFile(Path.Combine(m_path, texFilename)));
            //    //}

            //    var illum = 1;

            //    if (specular != C3f.Black && shinyness != 0)
            //    {
            //        illum = 2;
            //        m_materialStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "Ks {0} {1} {2}", specular.R, specular.G, specular.B));
            //        m_materialStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "Ns {0}", shinyness));
            //    }
            //    else
            //    {
            //        m_materialStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "Ns {0}", 0));
            //    }

            //    if (opacity != 1)
            //    {
            //        illum = 9; // glass
            //        m_materialStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "Tf {0} {1} {2}", opacity, opacity, opacity));                    
            //    }

            //    m_materialStream.WriteLine("illum {0}", illum);

            //    // write geometry

            //    // (+1) for one base indices
            //    var baseVertexIndex = m_vertexCount + 1;
            //    var baseNormalIndex = m_normalCount + 1;
            //    var baseTexCoordIndex = m_texCoordCount + 1;

            //    coords.ForEach(v => m_geometryStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}", v.X, v.Y, v.Z)));
            //    m_vertexCount += coords.Length;

            //    if (normals != null)
            //    {
            //        normals.ForEach(vn => m_geometryStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "vn {0} {1} {2}", vn.X, vn.Y, vn.Z)));
            //        m_normalCount += normals.Length;
            //    }
            //    if (texCoord != null)
            //    {
            //        texCoord.ForEach(vt => m_geometryStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "vt {0} {1}", vt.X, 1 - vt.Y)));
            //        m_texCoordCount += texCoord.Length;
            //    }
            //    //if (colors != null) // not supported
            //    //    colors.Run(vc => m_geometryStream.WriteLine("vc {0} {1} {2} {3}", vc.R, vc.G, vc.B, vc.A));

            //    m_geometryStream.WriteLine("g {0}", groupName);
            //    m_geometryStream.WriteLine("usemtl {0}", groupName);                

            //    // base- vertex/tex/normal indices are the same
            //    for (int i = 0; i < vg.TriangleCount; i++)
            //    {
            //        var i0 = indices[i * 3 + 0];
            //        var i1 = indices[i * 3 + 1];
            //        var i2 = indices[i * 3 + 2];

            //        if (normals != null && texCoord != null)
            //        {
            //            m_geometryStream.WriteLine("f {0}/{1}/{2} {3}/{4}/{5} {6}/{7}/{8}",
            //                                        i0 + baseVertexIndex, i0 + baseNormalIndex, i0 + baseTexCoordIndex,
            //                                        i1 + baseVertexIndex, i1 + baseNormalIndex, i1 + baseTexCoordIndex,
            //                                        i2 + baseVertexIndex, i2 + baseNormalIndex, i2 + baseTexCoordIndex);
            //        }
            //        else
            //        {
            //            if (normals != null)
            //                m_geometryStream.WriteLine("f {0}/{1} {2}/{3} {4}/{5}",
            //                                        i0 + baseVertexIndex, i0 + baseNormalIndex,
            //                                        i1 + baseVertexIndex, i1 + baseNormalIndex,
            //                                        i2 + baseVertexIndex, i2 + baseNormalIndex);
            //            else if (texCoord != null)
            //                m_geometryStream.WriteLine("f {0}//{1} {2}//{3} {4}//{5}",
            //                                        i0 + baseVertexIndex, i0 + baseTexCoordIndex,
            //                                        i1 + baseVertexIndex, i1 + baseTexCoordIndex,
            //                                        i2 + baseVertexIndex, i2 + baseTexCoordIndex);
            //            else
            //                m_geometryStream.WriteLine("f {0} {1} {2}", i0 + baseVertexIndex, i1 + baseVertexIndex, i2 + baseVertexIndex);
            //        }
            //    }

            //    m_faceCount += vg.TriangleCount;

            //    m_geometryCount++;
            //}

            private static WavefrontMaterial s_defaultMaterial;
            private static WavefrontMaterial GetDefaultMaterial()
            {
                if (s_defaultMaterial == null)
                {
                    s_defaultMaterial = new WavefrontMaterial("default");

                    s_defaultMaterial[WavefrontMaterial.Property.DiffuseColor] = C3f.Gray80;
                    s_defaultMaterial[WavefrontMaterial.Property.AmbientColor] = C3f.Gray20;
                    s_defaultMaterial[WavefrontMaterial.Property.IlluminationModel] = 1;
                }

                return s_defaultMaterial;
            }

            /// <summary>
            /// Appends the supplied geometry to the Wavefront Obj.
            /// Normals/DiffuseColorCoordinates will be only exported when supplied as non-indexed VertexAttributes.
            /// </summary>
            /// <param name="mesh">Mesh</param>
            /// <param name="groupName">Optional group name (g)</param>
            /// <param name="materialName">Optional material name (usemtl) / otherwise a default material will be created/used</param>
            public void AppendGeometry(PolyMesh mesh, string groupName = null, string materialName = null)
            {
                var coords = mesh.Vertices.Select(p => (V3f)p.Position).ToArray();

                V3f[] normals = null;
                V2f[] texCoords = null;

                if (mesh.VertexAttributes.Contains(PolyMesh.Property.Normals))
                    normals = mesh.VertexAttributeArray<V3d>(PolyMesh.Property.Normals).Select(V3f.FromV3d).TakeToArray(coords.Length);
                if (mesh.VertexAttributes.Contains(PolyMesh.Property.DiffuseColorCoordinates))
                    texCoords = mesh.VertexAttributeArray<V2d>(PolyMesh.Property.DiffuseColorCoordinates).Select(V2f.FromV2d).TakeToArray(coords.Length);

                // write signatures of new geometry
                if (groupName != null)
                    m_geometryStream.WriteLine("g {0}", groupName);
                if (materialName == null)
                {
                    var mat = GetDefaultMaterial();
                    materialName = mat.Name;
                    AppendMaterial(mat);
                }
                m_geometryStream.WriteLine("usemtl {0}", materialName);

                // (+1) for one base indices
                var baseVertexIndex = m_vertexCount + 1;
                var baseNormalIndex = m_normalCount + 1;
                var baseTexCoordIndex = m_texCoordCount + 1;

                // write geometry
                coords.ForEach(v => m_geometryStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "v {0} {1} {2}", v.X, v.Y, v.Z)));
                m_vertexCount += coords.Length;

                if (normals != null)
                {
                    normals.ForEach(vn => m_geometryStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "vn {0} {1} {2}", vn.X, vn.Y, vn.Z)));
                    m_normalCount += normals.Length;
                }
                if (texCoords != null)
                {
                    texCoords.ForEach(vt => m_geometryStream.WriteLine(String.Format(CultureInfo.InvariantCulture, "vt {0} {1}", vt.X, 1 - vt.Y)));
                    m_texCoordCount += texCoords.Length;
                }

                // base- vertex/tex/normal indices are the same
                for (int i = 0; i < mesh.FaceCount; i++)
                {
                    var face = mesh.GetFace(i);
                    string faceString = "";
                    if (normals == null && texCoords == null)
                        faceString = "f " + face.VertexIndices.Select(idx =>
                            (baseVertexIndex + idx).ToString()).Join(" ");
                    else if (normals == null)
                        faceString = "f " + face.VertexIndices.Select(idx =>
                            (baseVertexIndex + idx).ToString() + "//" +
                            (baseTexCoordIndex + idx).ToString()).Join(" ");
                    else if (texCoords == null)
                        faceString = "f " + face.VertexIndices.Select(idx =>
                            (baseVertexIndex + idx).ToString() + "/" +
                            (baseNormalIndex + idx).ToString()).Join(" ");
                    else
                        faceString = "f " + face.VertexIndices.Select(idx =>
                            (baseVertexIndex + idx).ToString() + "/" +
                            (baseNormalIndex + idx).ToString() + "/" +
                            (baseTexCoordIndex + idx).ToString()).Join(" ");

                    m_geometryStream.WriteLine(faceString);
                }

                m_faceCount += mesh.FaceCount;

                m_geometryCount++;
            }


            #region IDisposable Members

            public void Dispose()
            {
                m_geometryStream.Close();
                m_materialStream.Close();
            }

            #endregion
        };

        //public static void SaveToFile(IEnumerable<ConcreteVertexGeometry> vgs, string fn)
        //{
        //    var state = new ObjCoderState(fn);

        //    vgs.ForEach(vg => state.AppendGeometry(vg));

        //    state.Dispose();
        //}

        /// <summary>
        /// Exports a sequence of PolyMeshes to the given file.
        /// A default material will be used for all geometries.
        /// Normals/DiffuseColorCoordinates will be only exported when supplied as non-indexed VertexAttributes.
        /// </summary>
        public static void SaveToFile(IEnumerable<PolyMesh> polyMeshes, string fn)
        {
            var state = new ObjCoderState(fn);

            polyMeshes.ForEach(pm => state.AppendGeometry(pm));

            state.Dispose();
        }
    }
}
