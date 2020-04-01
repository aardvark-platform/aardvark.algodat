/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Base;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public interface IFilter
    {
        /// <summary></summary>
        bool IsFullyInside(IPointCloudNode node);

        /// <summary></summary>
        bool IsFullyOutside(IPointCloudNode node);

        /// <summary>
        /// Computes indices of selected/visible points, starting from already selected points.
        /// If 'selected' is null, then ALL points are selected to begin with.
        /// </summary>
        HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int> selected = default);

        /// <summary></summary>
        JObject Serialize();

    }

    public interface ISpatialFilter : IFilter
    {
        /// <summary></summary>
        bool IsFullyInside(Box3d box);

        /// <summary></summary>
        bool IsFullyOutside(Box3d box);

        bool Contains(V3d pt);

        Box3d Clip(Box3d box);
    }
}
