using Newtonsoft.Json.Linq;
using System;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class Filter
    {
        /// <summary></summary>
        public static IFilter Deserialize(string s) => Deserialize(JObject.Parse(s));
        
        /// <summary></summary>
        public static IFilter Deserialize(JToken jtoken) => Deserialize((JObject)jtoken);

        /// <summary></summary>
        public static IFilter Deserialize(JObject json)
        {
            var type = (string)json["Type"];

            switch (type)
            {
                case FilterInsideBox3d      .Type : return FilterInsideBox3d    .Deserialize(json);
                case FilterOutsideBox3d     .Type : return FilterOutsideBox3d   .Deserialize(json);
                case FilterOr               .Type : return FilterOr             .Deserialize(json);
                case FilterAnd              .Type : return FilterAnd            .Deserialize(json);
                case FilterIntensity        .Type : return FilterIntensity      .Deserialize(json);
                case FilterNormalDirection  .Type : return FilterNormalDirection.Deserialize(json);
                default: throw new NotImplementedException($"Unknown filter type: '{type}'");
            }
        }
    }
}
