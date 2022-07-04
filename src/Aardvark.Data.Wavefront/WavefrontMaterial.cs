using Aardvark.Base;

namespace Aardvark.Data.Wavefront
{
    public class WavefrontMaterial : SymMapBase
    {
        public static class Property
        {
            public static readonly Symbol AmbientColor = "AmbientColor";
            public static readonly Symbol EmissiveColor = "EmissiveColor";
            public static readonly Symbol DiffuseColor = "DiffuseColor";
            public static readonly Symbol SpecularColor = "SpecularColor";
            public static readonly Symbol SpecularExponent = "SpecularExponent";
            public static readonly Symbol Opacity = "Opacity";
            public static readonly Symbol TransmissionFilter = "TransmissionFilter";
            public static readonly Symbol IlluminationModel = "IlluminationModel";
            public static readonly Symbol Sharpness = "Sharpness";
            public static readonly Symbol OpticalDensity = "OpticalDensity";

            public static readonly Symbol AmbientColorMap = "AmbientColorMap";
            public static readonly Symbol DiffuseColorMap = "DiffuseColorMap";
            public static readonly Symbol SpecularColorMap = "SpecularColorMap";
            public static readonly Symbol SpecularExponentMap = "SpecularExponentMap";
            public static readonly Symbol OpacityMap = "OpacityMap";
            public static readonly Symbol DecalMap = "DecalMap";
            public static readonly Symbol DisplacementMap = "DisplacementMap";
            public static readonly Symbol BumpMap = "BumpMap";

            public static readonly Symbol ReflectionMap = "ReflectionMap";

            public static readonly Symbol Path = "Path";
        }

        public string Name;

        public WavefrontMaterial(string name)
        {
            Name = name;
        }
    }
}
