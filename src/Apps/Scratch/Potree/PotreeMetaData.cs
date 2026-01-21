using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Aardvark.Data.Potree
{
    public static class PotreeMetaData
    {
        public static bool TryDeserialize(string filePath, out PotreeData data)
        {
            string json = File.ReadAllText(filePath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            try
            {
                data = JsonSerializer.Deserialize<PotreeData>(json, options);
                return true;
            }
            catch (Exception ex)
            {
                //Serilog.Log.Error(ex);
                data = null;
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        public class PotreeData
        {
            public string version { get; set; }
            public string name { get; set; }
            public string description { get; set; }
            public double points { get; set; }
            public string projection { get; set; }
            public PotreeHierarchy hierarchy { get; set; }
            public double[] offset { get; set; }
            public double[] scale { get; set; }
            public double spacing { get; set; }
            public PotreeBoundingBox boundingBox { get; set; }
            public string encoding { get; set; }
            public PotreeAttribute[] attributes { get; set; }
        }

        public class PotreeHierarchy
        {
            public int firstChunkSize { get; set; }
            public int stepSize { get; set; }
            public int depth { get; set; }
        }

        public class PotreeBoundingBox
        {
            public double[] min { get; set; }
            public double[] max { get; set; }
        }

        public class PotreeAttribute
        {
            public string name { get; set; }
            public string description { get; set; }
            public int size { get; set; }
            public int numElements { get; set; }
            public int elementSize { get; set; }
            public string type { get; set; }
            public JsonElement[] min { get; set; }
            public JsonElement[] max { get; set; }
            public JsonElement[] scale { get; set; }
            public JsonElement[] offset { get; set; }
            public long[] histogram { get; set; }
        }

        public static object JsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object>();
                    foreach (var property in element.EnumerateObject())
                    {
                        obj[property.Name] = JsonElementToObject(property.Value);
                    }
                    return obj;

                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(JsonElementToObject(item));
                    }
                    return list;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int l))
                        return l;
                    else if (element.TryGetDouble(out double d))
                        return d;
                    else
                        return element.GetRawText();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                case JsonValueKind.Null:
                    return null;

                case JsonValueKind.Undefined:
                    return null;
            }

            return null;
        }
    }
}

//  "version": "2.0",
//	"name": "dense_pointcloud_100mio",
//	"description": "",
//	"points": 108692819,
//	"projection": "",
//	"hierarchy": {
//		"firstChunkSize": 4290, 
//		"stepSize": 4, 
//		"depth": 9
//	},
//	"offset": [71677.816000000006, 266019.81199999998, 669.71500000000003],
//	"scale": [0.001, 0.001, 0.001],
//	"spacing": 9.7567734375002146,
//	"boundingBox": {
//		"min": [71677.816000000006, 266019.81199999998, 669.71500000000003], 
//		"max": [72926.683000000034, 267268.679, 1918.5820000000276]
//    },
//	"encoding": "BROTLI",
//	"attributes": [
//		{
//			"name": "position",
//			"description": "",
//			"size": 12,
//			"numElements": 3,
//			"elementSize": 4,
//			"type": "int32",
//			"min": [71677.816000000006, 266019.81199999998, 669.71500000000003],
//			"max": [72926.683000000005, 266554.69199999998, 825.78300000000002],
//			"scale": [1, 1, 1],
//			"offset": [0, 0, 0]
//},{
//    "name": "intensity",
//			"description": "",
//			"size": 2,
//			"numElements": 1,
//			"elementSize": 2,
//			"type": "uint16",
//			"min": [0],
//			"max": [65535],
//			"scale": [1],
//			"offset": [0]

//        },{
//    "name": "return number",
//			"description": "",
//			"size": 1,
//			"numElements": 1,
//			"elementSize": 1,
//			"type": "uint8",
//			"min": [1],
//			"max": [1],
//			"scale": [1],
//			"offset": [0]

//        },{
//    "name": "number of returns",
//			"description": "",
//			"size": 1,
//			"numElements": 1,
//			"elementSize": 1,
//			"type": "uint8",
//			"min": [1],
//			"max": [1],
//			"scale": [1],
//			"offset": [0]

//        },{
//    "name": "classification",
//			"description": "",
//			"size": 1,
//			"numElements": 1,
//			"elementSize": 1,
//			"type": "uint8",
//			"histogram": [108692819, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], 
//			"min": [0],
//			"max": [0],
//			"scale": [1],
//			"offset": [0]

//        },{
//    "name": "scan angle rank",
//			"description": "",
//			"size": 1,
//			"numElements": 1,
//			"elementSize": 1,
//			"type": "uint8",
//			"min": [0],
//			"max": [0],
//			"scale": [1],
//			"offset": [0]

//        },{
//    "name": "user data",
//			"description": "",
//			"size": 1,
//			"numElements": 1,
//			"elementSize": 1,
//			"type": "uint8",
//			"min": [0],
//			"max": [0],
//			"scale": [1],
//			"offset": [0]

//        },{
//    "name": "point source id",
//			"description": "",
//			"size": 2,
//			"numElements": 1,
//			"elementSize": 2,
//			"type": "uint16",
//			"min": [1],
//			"max": [1],
//			"scale": [1],
//			"offset": [0]

//        },{
//    "name": "rgb",
//			"description": "",
//			"size": 6,
//			"numElements": 3,
//			"elementSize": 2,
//			"type": "uint16",
//			"min": [0, 0, 0],
//			"max": [65535, 65535, 65535],
//			"scale": [1, 1, 1],
//			"offset": [0, 0, 0]

//        }
//	]
