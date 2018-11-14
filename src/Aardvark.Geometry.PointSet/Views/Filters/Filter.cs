using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aardvark.Geometry.Points.Views
{
    /// <summary>
    /// </summary>
    public static class Filter
    {
        /// <summary>
        /// </summary>
        public static IFilter Deserialize(string s)
        {
            var json = JObject.Parse(s);
            var type = (string)json["Type"];

            switch (type)
            {
                case FilterInsideBox3d.Type: return FilterInsideBox3d.Deserialize(json);
                case FilterOutsideBox3d.Type: return FilterOutsideBox3d.Deserialize(json);
                default: throw new NotImplementedException($"Unknown filter type: '{type}'");
            }
        }
    }
}
