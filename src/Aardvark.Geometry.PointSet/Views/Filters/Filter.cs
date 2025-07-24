/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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

using System;
using System.Text.Json.Nodes;

#pragma warning disable IDE0130

namespace System.Runtime.CompilerServices
{
    internal class IsExternalInit { }
}

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class Filter
    {
        /// <summary></summary>
        public static IFilter Deserialize(string s) => Deserialize(JsonNode.Parse(s)!);
        
        /// <summary></summary>
        public static IFilter Deserialize(JsonNode json)
        {
            var type = (string?)json["Type"];

            return type switch
            {
                FilterAnd.Type                  => FilterAnd.Deserialize(json),
                FilterClassification.Type       => FilterClassification.Deserialize(json),
                FilterInsideBox3d.Type          => FilterInsideBox3d.Deserialize(json),
                FilterInsideConvexHull3d.Type   => FilterInsideConvexHull3d.Deserialize(json),
                FilterInsideConvexHulls3d.Type  => FilterInsideConvexHulls3d.Deserialize(json),
                FilterInsidePrismXY.Type        => FilterInsidePrismXY.Deserialize(json),
                FilterInsideSphere3d.Type       => FilterInsideSphere3d.Deserialize(json),
                FilterIntensity.Type            => FilterIntensity.Deserialize(json),
                FilterNormalDirection.Type      => FilterNormalDirection.Deserialize(json),
                FilterOr.Type                   => FilterOr.Deserialize(json),
                FilterOutsideBox3d.Type         => FilterOutsideBox3d.Deserialize(json),

                null => throw new Exception($"Failed to deserialize Json. Error 287535f3-1f56-49f8-856a-b3ab8abd3764.\n{json}"),
                _ => throw new Exception($"Unknown filter type \"{type}\". Error 306594ed-193b-4acb-88e8-7ba24770c884."),
            };
        }
    }
}
