using Aardvark.Base;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Data.Potree
{
    public class PotreeOctree
    {
        public string Url { get; set; }
        public double[] Scale { get; set; }
        public double[] Offset { get; set; }
        public PotreeNode Root { get; set; }
        public PointAttributes Attributes { get; set; }

        public class PointAttributes
        {
            public class PointAttribute
            {
                public string Name { get; private set; }
                public int NumElements { get; private set; }
                public int ByteSize { get; private set; }
                public object[] Range { get; set; }
                public object[] InitialRange { get; set; }

                public PointAttribute(string name, (string name, int size) type, int numElements)
                {
                    Name = name;
                    NumElements = numElements;
                    ByteSize = numElements * type.size;
                }
            }

            public class PointAttributeVector
            {
                public string Name { get; private set; }
                public string[] AttributeSourceNames { get; private set; }

                public PointAttributeVector(string name, string[] sourceNames)
                {
                    Name = name;
                    AttributeSourceNames = sourceNames;
                }
            }

            public static class PointAttributeNames
            {
                public const string POSITION_CARTESIAN = "POSITION_CARTESIAN";
                public const string COLOR_PACKED = "COLOR_PACKED";
                public const string NORMAL_FLOATS = "NORMAL_FLOATS";
                public const string INTENSITY = "INTENSITY";
                public const string CLASSIFICATION = "CLASSIFICATION";
                public const string NORMAL_SPHEREMAPPED = "NORMAL_SPHEREMAPPED";
                public const string NORMAL_OCT16 = "NORMAL_OCT16";
                public const string NORMAL = "NORMAL";
                public const string RETURN_NUMBER = "RETURN_NUMBER";
                public const string NUMBER_OF_RETURNS = "NUMBER_OF_RETURNS";
                public const string SOURCE_ID = "SOURCE_ID";
                public const string INDICES = "INDICES";
                public const string SPACING = "SPACING";
                public const string GPS_TIME = "GPS_TIME";
            }

            public static class PointAttributeTypes
            {
                public static (string, int) DATA_TYPE_DOUBLE = ("double", 8);
                public static (string, int) DATA_TYPE_FLOAT = ("float", 4);
                public static (string, int) DATA_TYPE_INT8 = ("int8", 1);
                public static (string, int) DATA_TYPE_UINT8 = ("uint8", 1);
                public static (string, int) DATA_TYPE_INT16 = ("int16", 2);
                public static (string, int) DATA_TYPE_UINT16 = ("uint16", 2);
                public static (string, int) DATA_TYPE_INT32 = ("int32", 4);
                public static (string, int) DATA_TYPE_UINT32 = ("uint32", 4);
                public static (string, int) DATA_TYPE_INT64 = ("int64", 8);
                public static (string, int) DATA_TYPE_UINT64 = ("uint64", 8);
            }

            public PointAttribute POSITION_CARTESIAN = new PointAttribute(PointAttributeNames.POSITION_CARTESIAN, PointAttributeTypes.DATA_TYPE_FLOAT, 3);
            public PointAttribute RGBA_PACKED = new PointAttribute(PointAttributeNames.COLOR_PACKED, PointAttributeTypes.DATA_TYPE_INT8, 4);
            public PointAttribute COLOR_PACKED => RGBA_PACKED;
            public PointAttribute RGB_PACKED = new PointAttribute(PointAttributeNames.COLOR_PACKED, PointAttributeTypes.DATA_TYPE_INT8, 3);
            public PointAttribute NORMAL_FLOATS = new PointAttribute(PointAttributeNames.NORMAL_FLOATS, PointAttributeTypes.DATA_TYPE_FLOAT, 3);
            public PointAttribute INTENSITY = new PointAttribute(PointAttributeNames.INTENSITY, PointAttributeTypes.DATA_TYPE_UINT16, 1);
            public PointAttribute CLASSIFICATION = new PointAttribute(PointAttributeNames.CLASSIFICATION, PointAttributeTypes.DATA_TYPE_UINT8, 1);
            public PointAttribute NORMAL_SPHEREMAPPED = new PointAttribute(PointAttributeNames.NORMAL_SPHEREMAPPED, PointAttributeTypes.DATA_TYPE_UINT8, 2);
            public PointAttribute NORMAL_OCT16 = new PointAttribute(PointAttributeNames.NORMAL_OCT16, PointAttributeTypes.DATA_TYPE_UINT8, 2);
            public PointAttribute NORMAL = new PointAttribute(PointAttributeNames.NORMAL, PointAttributeTypes.DATA_TYPE_FLOAT, 3);
            public PointAttribute RETURN_NUMBER = new PointAttribute(PointAttributeNames.RETURN_NUMBER, PointAttributeTypes.DATA_TYPE_UINT8, 1);
            public PointAttribute NUMBER_OF_RETURNS = new PointAttribute(PointAttributeNames.NUMBER_OF_RETURNS, PointAttributeTypes.DATA_TYPE_UINT8, 1);
            public PointAttribute SOURCE_ID = new PointAttribute(PointAttributeNames.SOURCE_ID, PointAttributeTypes.DATA_TYPE_UINT16, 1);
            public PointAttribute INDICES = new PointAttribute(PointAttributeNames.INDICES, PointAttributeTypes.DATA_TYPE_UINT32, 1);
            public PointAttribute SPACING = new PointAttribute(PointAttributeNames.SPACING, PointAttributeTypes.DATA_TYPE_FLOAT, 1);
            public PointAttribute GPS_TIME = new PointAttribute(PointAttributeNames.GPS_TIME, PointAttributeTypes.DATA_TYPE_DOUBLE, 1);

            public List<PointAttribute> Attributes { get; private set; }
            public List<PointAttributeVector> AttributeVectors { get;private set; }

            public int ByteSize { get; private set; }
            public int Size => Attributes.Count;

            public PointAttributes(PotreeMetaData.PotreeAttribute[] jsonAttributes)
            {
                var typenameTypeattributeMap = new Dictionary<string, (string, int)>
                    {
                        { "double", PointAttributeTypes.DATA_TYPE_DOUBLE },
                        { "float", PointAttributeTypes.DATA_TYPE_FLOAT },
                        { "int8", PointAttributeTypes.DATA_TYPE_INT8 },
                        { "uint8", PointAttributeTypes.DATA_TYPE_UINT8 },
                        { "int16", PointAttributeTypes.DATA_TYPE_INT16 },
                        { "uint16", PointAttributeTypes.DATA_TYPE_UINT16 },
                        { "int32", PointAttributeTypes.DATA_TYPE_INT32 },
                        { "uint32", PointAttributeTypes.DATA_TYPE_UINT32 },
                        { "int64", PointAttributeTypes.DATA_TYPE_INT64 },
                        { "uint64", PointAttributeTypes.DATA_TYPE_UINT64 }
                    };

                Attributes = [];
                AttributeVectors = [];
                ByteSize = 0;

                var replacements = new Dictionary<string, string>
                    {
                        { "rgb", "rgba" },
                    };

                foreach (var jsonAttribute in jsonAttributes)
                {
                    var name = jsonAttribute.name;
                    var description = jsonAttribute.description;
                    var size = jsonAttribute.size;
                    var numElements = jsonAttribute.numElements;
                    var elementSize = jsonAttribute.elementSize;
                    var min = jsonAttribute.min;
                    var max = jsonAttribute.max;

                    var type = typenameTypeattributeMap[jsonAttribute.type];

                    var potreeAttributeName = replacements.ContainsKey(name) ? replacements[name] : name;

                    var attribute = new PointAttribute(potreeAttributeName, type, numElements);

                    if (numElements == 1)
                        attribute.Range = [PotreeMetaData.JsonElementToObject(min[0]), PotreeMetaData.JsonElementToObject(max[0])];
                    else
                        attribute.Range = [min.Select(PotreeMetaData.JsonElementToObject).ToArray(), max.Select(PotreeMetaData.JsonElementToObject).ToArray()];

                    if (name == "gps-time")
                    {
                        if (attribute.Range[0] == attribute.Range[1])
                            attribute.Range[1] = (int)attribute.Range[1] + 1;
                    }

                    attribute.InitialRange = attribute.Range;

                    Add(attribute);
                }

                // Check for normals
                bool hasNormals = Attributes.Any(a => a.Name == "NormalX") &&
                                  Attributes.Any(a => a.Name == "NormalY") &&
                                  Attributes.Any(a => a.Name == "NormalZ");

                if (hasNormals)
                    AddVector(new PointAttributeVector("NORMAL", ["NormalX", "NormalY", "NormalZ"]));
            }

            public void Add(PointAttribute attribute)
            {
                Attributes.Add(attribute);
                ByteSize += attribute.ByteSize;
            }

            public void AddVector(PointAttributeVector vector)
            {
                AttributeVectors.Add(vector);
            }

            public bool HasColors()
            {
                foreach (var attr in Attributes)
                {
                    if (attr.Name == PointAttributeNames.COLOR_PACKED)
                        return true;
                }
                return false;
            }

            public bool HasNormals()
            {
                foreach (var attr in Attributes)
                {
                    if (attr.Name == PointAttributeNames.NORMAL ||
                        attr.Name == PointAttributeNames.NORMAL_FLOATS ||
                        attr.Name == PointAttributeNames.NORMAL_OCT16 ||
                        attr.Name == PointAttributeNames.NORMAL_SPHEREMAPPED)
                        return true;
                }
                return false;
            }
        }
    }
}