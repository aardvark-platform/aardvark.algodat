using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using System.IO;
using System.Text;

namespace Aardvark.Data.Wavefront
{
    public static class MtlParser
    {
        class ParseState : StreamParser<ParseState>
        {
            public List<WavefrontMaterial> MaterialList = new List<WavefrontMaterial>();

            WavefrontMaterial m_currentMaterial;

            public ParseState(Stream stream, string baseDir = null)
                : this(stream, Encoding.Default, baseDir)
            {
            }

            public ParseState(Stream stream, Encoding encoding, string baseDir = null)
                : base(stream, encoding, baseDir)
            {
            }

            public void NewMaterial(IList<Text> args)
            {
                if (args.Count < 1) throw new ArgumentException("Invalid number of arguments");
                var name = args[0].ToString();
                m_currentMaterial = new WavefrontMaterial(name);
                m_currentMaterial[WavefrontMaterial.Property.Path] = BaseDir;
                MaterialList.Add(m_currentMaterial);
            }

            public void SetAttribute(Symbol name, object value)
            {
                m_currentMaterial[name] = value;
            }
        }

        /// <summary>
        /// Parses a mtl file into wavefront materials.
        /// The encoding is assumed based on the platform.
        /// Throws an exception if the file is not found.
        /// </summary>
        public static List<WavefrontMaterial> Load(string fileName)
        {
            return Load(fileName, Encoding.Default);
        }

        /// <summary>
        /// Parses a mtl file into wavefront materials.
        /// Throws an exception if the file is not found.
        /// </summary>
        public static List<WavefrontMaterial> Load(string fileName, Encoding encoding)
        {
            if (!File.Exists(fileName))
                throw new FileNotFoundException("Material library file \"{0}\" not found", fileName);

            var stream = File.OpenRead(fileName);
            return Load(stream, Encoding.Default, Path.GetDirectoryName(fileName));
        }

        /// <summary>
        /// Parses a mtl file contained in a stream into wavefront materials.
        /// The encoding is assumed based on the platform.
        /// The baseDir argument is optional and can be used to store texture file names using absolute paths.
        /// </summary>
        public static List<WavefrontMaterial> Load(Stream stream, string baseDir = null)
        {
            return Load(stream, Encoding.Default, baseDir);
        }

        /// <summary>
        /// Parses a mtl file contained in a stream into wavefront materials.
        /// The baseDir argument is optional and can be used to store texture file names using absolute paths.
        /// </summary>
        public static List<WavefrontMaterial> Load(Stream stream, Encoding encoding, string baseDir = null)
        {
            var parseState = new ParseState(stream, encoding, baseDir);
            WavefrontParser.Parse(parseState, s_attributeParsers);
            return parseState.MaterialList;
        }

        static Dictionary<Text, Action<ParseState, IList<Text>>> s_attributeParsers =
            new Dictionary<Text, Action<ParseState, IList<Text>>>
            {
                { new Text("Ka"),   (s, a) => s.SetAttribute(WavefrontMaterial.Property.AmbientColor, Primitives.ParseColor(a)) },
                { new Text("Kd"),   (s, a) => s.SetAttribute(WavefrontMaterial.Property.DiffuseColor, Primitives.ParseColor(a))},
                { new Text("Ke"),   (s, a) => s.SetAttribute(WavefrontMaterial.Property.EmissiveColor, Primitives.ParseColor(a)) },
                { new Text("Ks"),   (s, a) => s.SetAttribute(WavefrontMaterial.Property.SpecularColor, Primitives.ParseColor(a)) },
                { new Text("Ns"),   (s, a) => s.SetAttribute(WavefrontMaterial.Property.SpecularExponent, Primitives.ParseFloat(a)) },
                { new Text("Tf"),   (s, a) => s.SetAttribute(WavefrontMaterial.Property.TransmissionFilter, Primitives.ParseColor(a)) },
                { new Text("Tr"),   (s, a) => s.SetAttribute(WavefrontMaterial.Property.Opacity, 1 - Primitives.ParseFloat(a)) },
                { new Text("d"),    (s, a) => s.SetAttribute(WavefrontMaterial.Property.Opacity, Primitives.ParseFloat(a)) },
                { new Text("illum"),(s, a) => s.SetAttribute(WavefrontMaterial.Property.IlluminationModel, Primitives.ParseInt(a)) },
                { new Text("sharpness"),(s, a) => s.SetAttribute(WavefrontMaterial.Property.Sharpness, Primitives.ParseInt(a)) },
                { new Text("Ni"),   (s, a) => s.SetAttribute(WavefrontMaterial.Property.OpticalDensity, Primitives.ParseFloat(a)) },
                
                { new Text("map_Ka"), (s, a) => s.SetAttribute(WavefrontMaterial.Property.AmbientColorMap, Primitives.ParseMap(a, s.BaseDir)) },
                { new Text("map_Kd"), (s, a) => s.SetAttribute(WavefrontMaterial.Property.DiffuseColorMap, Primitives.ParseMap(a, s.BaseDir)) },
                { new Text("map_Ks"), (s, a) => s.SetAttribute(WavefrontMaterial.Property.SpecularColorMap, Primitives.ParseMap(a, s.BaseDir)) },
                { new Text("map_Kn"), (s, a) => s.SetAttribute(WavefrontMaterial.Property.NormalMap, Primitives.ParseMap(a, s.BaseDir)) },
                { new Text("map_Ns"), (s, a) => s.SetAttribute(WavefrontMaterial.Property.SpecularExponentMap, Primitives.ParseMap(a, s.BaseDir)) },
                { new Text("map_d"),  (s, a) => s.SetAttribute(WavefrontMaterial.Property.OpacityMap, Primitives.ParseMap(a, s.BaseDir)) },
                { new Text("decal"),  (s, a) => s.SetAttribute(WavefrontMaterial.Property.DecalMap, Primitives.ParseMap(a, s.BaseDir)) },
                { new Text("disp"),   (s, a) => s.SetAttribute(WavefrontMaterial.Property.DisplacementMap, Primitives.ParseMap(a, s.BaseDir)) },
                { new Text("map_bump"), (s, a) => s.SetAttribute(WavefrontMaterial.Property.BumpMap, Primitives.ParseMap(a, s.BaseDir)) },
                { new Text("bump"),   (s, a) => s.SetAttribute(WavefrontMaterial.Property.BumpMap, Primitives.ParseMap(a, s.BaseDir)) },

                // refl ... todo -type

                { new Text("newmtl"), (s, a) => s.NewMaterial(a) }
            };
    }
}
