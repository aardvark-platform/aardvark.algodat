using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Aardvark.Data.Wavefront
{
    class StreamParser<T>
    {
        StreamReader m_reader;

        /// <summary>
        /// Returns the initial encoding the StreamReader has been created with.
        /// </summary>
        public Encoding Encoding { get; }

        public string BaseDir { get; }

        public Text Line { get; private set; }

        public bool EndOfText { get; private set; }

        /// <summary>
        /// Move to the next line of the Stream.
        /// Lines ending with '\' are joined.
        /// Returns false if the end of the stream has been reached.
        /// </summary>
        public bool NextLine()
        {
            if (m_reader.EndOfStream)
            {
                EndOfText = true;
                Line = Text.Empty;
                return false;
            }

            // lines are allowed to be joined when ended with line continuation character (\)
            var line = String.Empty;
            var str = String.Empty;
            do
            {
                str = m_reader.ReadLine().TrimEnd();
                line = line + str;
                //var test = str.Length > 0 && str[str.Length - 1] == '\\';
                //if (test) Report.Line("JOIN");
            }
            while (str.Length > 0 && str[str.Length - 1] == '\\');

            Line = new Text(line);
            return true;
        }

        /// <summary>
        /// NOTE: if byte order marks are present the specified encoding will be overruled
        /// </summary>
        public StreamParser(Stream stream, Encoding encoding, string baseDir = null)
        {
            Encoding = encoding;
            m_reader = new StreamReader(stream, encoding, true);
            EndOfText = m_reader.EndOfStream;
            Line = Text.Empty;
            BaseDir = baseDir;
        }
    }

    public static class ObjParser
    {
        internal class ParseState : StreamParser<ParseState>
        {
            public WavefrontObject Object = new WavefrontObject();

            int m_currentMaterial = -1;
            int m_currentGroup = -1;
            int m_currentSmoothGroup = -1;


            Dictionary<Text, WavefrontMaterial> m_mtlLib = new Dictionary<Text, WavefrontMaterial>();
            Dictionary<WavefrontMaterial, int> m_mtlIndices = new Dictionary<WavefrontMaterial, int>();

            Dictionary<Text, int> m_groups = new Dictionary<Text, int>();
            
            public ParseState(Stream stream, Encoding encoding, string baseDir = null)
                : base(stream, encoding, baseDir)
            {
            }

            public ParseState(Stream stream, Encoding encoding, string baseDir = null, bool useDoublePrecision = false)
                : base(stream, encoding, baseDir)
            {
                Object = new WavefrontObject(useDoublePrecision);
            }


            static bool multipleGroupsWarningShown = false;

            public void SetGroup(IEnumerable<Text> args)
            {
                var name = args.First();
                if (name.IsEmpty) throw new InvalidOperationException();

                if (!m_groups.TryGetValue(name, out m_currentGroup))
                {
                    var gl = Object.Groups;
                    m_currentGroup = gl.Count;
                    m_groups[name] = gl.Count;
                    gl.Add(name.ToString());
                }

                if ((args.Count() > 1) && (!multipleGroupsWarningShown))
                {
                    Report.Warn("multiple groups are ignored");
                    multipleGroupsWarningShown = true;
                }
            }

            public void SetMaterial(IEnumerable<Text> args)
            {
                var name = args.Single();
                if (name.IsEmpty) throw new InvalidOperationException();

                if (m_mtlLib.TryGetValue(name, out WavefrontMaterial mtl))
                {
                    if (!m_mtlIndices.TryGetValue(mtl, out m_currentMaterial))
                    {
                        var ml = Object.Materials;
                        m_currentMaterial = ml.Count;
                        m_mtlIndices[mtl] = ml.Count;
                        ml.Add(mtl);
                    }
                }
                else
                {
#if DEBUG
                    Report.Warn("could not find material: {0}", name);
#endif
                    m_currentMaterial = -1;
                }
            }

            public void SetSmoothingGroup(IEnumerable<Text> args)
            {
                var sg = args.Single();

                m_currentSmoothGroup = sg == "off" ? -1 : sg.ParseIntValue();
            }

            bool TryLoadMaterialLib(string fileName, Encoding encoding)
            {
                try
                {
                    if (!string.IsNullOrEmpty(BaseDir))
                        fileName = Path.Combine(BaseDir, fileName);
                    
                    var materials = MtlParser.Load(fileName, encoding);
                    m_mtlLib.AddRange(materials.Select(m => KeyValuePairs.Create(new Text(m.Name), m)));
                    return true;
                }
                catch (Exception e)
                {
                    Report.Line("error loading material library: " + e.Message);
                }
                return false;
            }

            internal void LoadMaterialLibs(IEnumerable<Text> args, Encoding encoding)
            {
                if (args.IsEmpty()) return;

                // white spaces within filename possible (although not explicitly in specification) -> try interpretation of all texts as one
                
                var first = args.First();
                var start = first.Start;
                var end = args.Last().End;
                var fileName = first.String.Substring(start, end - start);

                if (TryLoadMaterialLib(fileName, encoding)) // if successfully loaded mtl lib with merged filename -> return
                    return; 
                
                foreach (var file in args)
                    TryLoadMaterialLib(file.ToString(), encoding);
            }

            public void AddPointSet(IList<Text> indices)
            {
                var indexCount = indices.Count;
                if (indexCount < 1) throw new InvalidOperationException("Point vertex index missing");

                var ps = Object.PointsSets.LastOrDefault();
                if (ps == null || ps.GroupIndex != this.m_currentGroup)
                {
                    ps = new WavefrontObject.PointSet();
                    ps.GroupIndex = this.m_currentGroup;
                    Object.PointsSets.Add(ps);
                }

                var vertexCount = Object.Vertices.Count;

                foreach (var i in indices)
                {
                    var index = i.ParseIntValue();
                    if (index == 0) throw new InvalidOperationException("Invalid point vertex index");
                    ps.VertexIndices.Add(index > 0 ? index - 1 : vertexCount - index);
                }

                var mi = this.m_currentMaterial;
                ps.MaterialIndices.Add(mi);
                ps.FirstIndices.Add(ps.VertexIndices.Count);
            }

            public void AddLineStrip(IList<Text> indices)
            {
                var indexCount = indices.Count;
                if (indexCount < 2) throw new InvalidOperationException("LineStrip must have at least to indices");

                var ls = Object.LineSets.LastOrDefault();
                if (ls == null || ls.GroupIndex != this.m_currentGroup)
                {
                    ls = new WavefrontObject.LineSet();
                    ls.GroupIndex = this.m_currentGroup;
                    Object.LineSets.Add(ls);
                }

                var vertexCount = Object.Vertices.Count;
                var coordCount = Object.TextureCoordinates.Count;

                ls.VertexCountRange.EnlargeBy(indices.Count);

                int indexAttributeLayout = -1;
                foreach (var i in indices)
                {
                    // l vi vi vi ...
                    // l vi/ti vi/ti vi/ti ...

                    // indices can be supplied by absolute vertex index or relative using negative numbers
                    // absolute vertex indices start by 1, index value 0 indicates not specified attribute

                    var ai = i.Split('/').GetEnumerator();
                    var iaLayout = 0;

                    var vi = 0;
                    if (ai.MoveNext() && ai.Current.Count > 0)
                    {
                        iaLayout |= 0x01;
                        vi = ai.Current.ParseIntValue();
                    }
                    else
                        throw new InvalidOperationException("Missing vertex index element");

                    var ti = 0;
                    if (ai.MoveNext() && ai.Current.Count > 0)
                    {
                        ti = ai.Current.ParseIntValue();
                        iaLayout |= 0x02;
                    }
                 
                    if (ai.MoveNext())
                        throw new InvalidOperationException("Invalid index element count");

                    if (indexAttributeLayout == -1)
                        indexAttributeLayout = iaLayout;
                    else if (iaLayout != indexAttributeLayout)
                        throw new InvalidOperationException("Different index element counts");

                    // indices can be supplied by absolute vertex index or relative using negative numbers
                    ls.VertexIndices.Add(vi > 0 ? vi - 1 : vertexCount + vi);
                    ls.TexCoordIndices.Add(ti > 0 ? ti - 1 : ti < 0 ? coordCount + ti : -1);
                }

                var mi = this.m_currentMaterial;
                ls.MaterialIndices.Add(mi);
                ls.FirstIndices.Add(ls.VertexIndices.Count);
            }

            public void AddFace(IList<Text> indices)
            {
                var indexCount = indices.Count;
                if (indexCount < 3) throw new InvalidOperationException("Face must have at least 3 indices");

                var fs = Object.FaceSets.LastOrDefault();
                if (fs == null || fs.GroupIndex != this.m_currentGroup)
                {
                    fs = new WavefrontObject.FaceSet();
                    fs.GroupIndex = this.m_currentGroup;
                    Object.FaceSets.Add(fs);
                }

                var vertexCount = Object.Vertices.Count;
                var coordCount = Object.TextureCoordinates.Count;
                var normalCount = Object.Normals.Count;
                
                int indexAttributeLayout = -1; // make sure all vertex declarations are equal

                fs.FaceVertexCountRange.ExtendBy(indices.Count);

                foreach (var i in indices)
                {
                    // f vi vi vi ...
                    // f vi/ti vi/ti vi/ti ...
                    // f vi//ni vi//ni vi//ni ...
                    // f vi/ti/ni vi/ti/ni vi/ti/ni ...

                    // indices can be supplied by absolute vertex index or relative using negative numbers
                    // absolute vertex indices start by 1, index value 0 indicates not specified attribute

                    var ai = i.Split('/').GetEnumerator();
                    var iaLayout = 0;

                    var vi = 0;
                    if (ai.MoveNext() && ai.Current.Count > 0)
                    {
                        iaLayout |= 0x01;
                        vi = ai.Current.ParseIntValue();
                    }
                    else
                        throw new InvalidOperationException("Missing vertex index element");

                    var ti = 0;
                    if (ai.MoveNext() && ai.Current.Count > 0)
                    {
                        ti = ai.Current.ParseIntValue();
                        iaLayout |= 0x02;
                    }

                    var ni = 0;
                    if (ai.MoveNext() && ai.Current.Count > 0)
                    {
                        ni = ai.Current.ParseIntValue();
                        iaLayout |= 0x04;
                    }

                    if (ai.MoveNext())
                        throw new InvalidOperationException("Invalid index element count");

                    if (indexAttributeLayout == -1)
                        indexAttributeLayout = iaLayout;
                    else if (iaLayout != indexAttributeLayout) 
                        throw new InvalidOperationException("Different index element counts");

                    // handle absolute or relative indices
                    // TODO: keep track if all attributes have the same layout + keep track if any attribute is not used at all 
                    // -> more efficient genernation of PolyMesh/IndexGeometry output that can exclude non-existing attributes (at the moment this is only done globally)
                    //var attributeIndexMask = (ti > 0 ? 0x1 : 0) | (ni > 0 ? 0x2 : 0); 
                    vi = vi > 0 ? vi - 1 : vertexCount + vi;
                    ti = ti > 0 ? ti - 1 : ti < 0 ? coordCount + ti : -1;
                    ni = ni > 0 ? ni - 1 : ni < 0 ? normalCount + ni : -1;
                    fs.VertexIndices.Add(vi);
                    fs.TexCoordIndices.Add(ti);
                    fs.NormalIndices.Add(ni);
                }

                var mi = this.m_currentMaterial;
                fs.MaterialIndices.Add(mi);
                fs.FirstIndices.Add(fs.VertexIndices.Count);
            }

            internal void AddVertex(IList<Text> a)
            {
                // vertex (x, y, z, w) w == optional otherwise 1
                // extension for colors: (x, y, z, [w]) [(r, g, b)]

                if (a.Count < 3 || a.Count == 5 || a.Count > 7) throw new InvalidOperationException("Invalid number of index elements");

                var va = Object.Vertices;

                if (Object.DoublePrecisionVertices)
                {
                    ((List<V4d>)va).Add(new V4d(a[0].ParseDoubleValue(),
                                               a[1].ParseDoubleValue(),
                                               a[2].ParseDoubleValue(),
                                               a.Count == 4 || a.Count == 7 ? a[3].ParseDoubleValue() : 1));
                }
                else
                {
                    ((List<V4f>)va).Add(new V4f(a[0].ParseFloatValue(),
                                                a[1].ParseFloatValue(),
                                                a[2].ParseFloatValue(),
                                                a.Count == 4 || a.Count == 7 ? a[3].ParseFloatValue() : 1));
                }

                // get optional color data array (not initialized by default)
                var ca = Object.VertexColors;
                // fill up colors if the data array is create or there are colors
                if (ca != null || a.Count > 4)
                {
                    var c = C3f.White;
                    if (a.Count > 4)
                    {
                        var ci0 = a.Count == 7 ? 4 : 3;
                        c = new C3f(a[ci0].ParseFloatValue(),
                                    a[ci0 + 1].ParseFloatValue(),
                                    a[ci0 + 1].ParseFloatValue());
                    }
                    // initialized vertex color array for all already read vertices in case not all vertices have colors
                    if (ca == null)
                    {
                        ca = new List<C3f>(va.Count);
                        ca.AddRange(C3f.White, va.Count - 1);
                        Object.VertexColors = ca;
                    }
                    ca.Add(c);
                }
            }
        }

        /// <summary>
        /// Loads an obj from file.
        /// The encoding is assumed based on the platform.
        /// Throws an exception if the file cannot be found or read.
        /// A mtllib will tried to be loaded from the same directory.
        /// </summary>
        public static WavefrontObject Load(string fileName)
        {
            return Load(fileName, Encoding.Default);
        }

        /// <summary>
        /// Loads an obj from file.
        /// The encoding is assumed based on the platform.
        /// Can be set to double or default float precision
        /// Throws an exception if the file cannot be found or read.
        /// A mtllib will tried to be loaded from the same directory.
        /// </summary>
        public static WavefrontObject Load(string fileName, bool useDoublePrecision = true)
        {
            return Load(fileName, Encoding.Default, useDoublePrecision);
        }

        /// <summary>
        /// Loads an obj from file.
        /// Throws an exception if the file cannot be found or read.
        /// A mtllib will tried to be loaded from the same directory.
        /// </summary>
        public static WavefrontObject Load(string fileName, Encoding encoding, bool useDoublePrecision = true)
        {
            var parseState = new ParseState(File.OpenRead(fileName), encoding, Path.GetDirectoryName(fileName), useDoublePrecision);
            WavefrontParser.Parse(parseState, s_elementProcessors);
            return parseState.Object;
        }

        static Dictionary<Text, Action<ParseState, IList<Text>>> s_elementProcessors =
            new Dictionary<Text, Action<ParseState, IList<Text>>>()
            {
                // Vertex data
                { new Text(WavefrontObject.Property.Vertices.ToString()), (s, a) => s.AddVertex(a) },
                { new Text(WavefrontObject.Property.TextureCoordinates.ToString()), (s, a) => s.Object.TextureCoordinates.Add(Primitives.ParseTexCoord(a)) },  // tex coord (u, v, w) v, w == optinal
                { new Text(WavefrontObject.Property.Normals.ToString()), (s, a) => s.Object.Normals.Add(Primitives.ParseVector(a)) },      // normal
                { new Text(WavefrontObject.Property.ControlPoints.ToString()), (s, a) => s.Object.ControlPoints.Add(Primitives.ParseVector(a)) },    // point (u, v, w) 
                // cstype
                // deg
                // bmat
                // step

                // Elements
                { new Text(WavefrontObject.Property.PointSets.ToString()), (s, a) => s.AddPointSet(a) }, // p: point set
                { new Text(WavefrontObject.Property.LineSets.ToString()), (s, a) => s.AddLineStrip(a) }, // l: line strip
                { new Text(WavefrontObject.Property.FaceSets.ToString()), (s, a) => s.AddFace(a) },      // f: face
                // curv
                // curv2
                // surf

                // Free-form
                // parm
                // trim
                // hole
                // scrv
                // sp
                // end

                // Connectivity
                // con

                // Grouping
                { new Text(WavefrontObject.Property.Groups.ToString()), (s, a) => s.SetGroup(a) }, // group: name (name2, ...) NOTE: multi group not supported
                { new Text("s"), (s, a) => s.SetSmoothingGroup(a) }, // smoothing group: "off"/-1 or index
                // mg
                // o

                // Render attributes
                { new Text("mtllib"), (s, a) => s.LoadMaterialLibs(a, s.Encoding) }, // material library
                { new Text(WavefrontObject.Property.Materials.ToString()), (s, a) => s.SetMaterial(a) }, // material
                // bevel
                // c_interp
                // d_interp
                // lod
                // shadow_obj
                // ctech
                // stech

                //{ new Text("#"), (s, a) => { } }, // comment
            };
    }
}
