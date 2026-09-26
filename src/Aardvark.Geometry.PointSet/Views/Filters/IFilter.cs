/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Aardvark.Geometry.Points;

/// <summary>
/// Point selection with conservative node classification for filtered views.
/// </summary>
public interface IFilter : IEquatable<IFilter>
{
    /// <summary>
    /// Returns true when the node can be accepted in full. False does not imply
    /// fully outside. Boolean OR requires either operand fully inside; AND requires both.
    /// </summary>
    bool IsFullyInside(IPointCloudNode node);

    /// <summary>
    /// Returns true when the node can be rejected in full. False does not imply
    /// fully inside. Boolean OR requires both operands fully outside; AND requires either.
    /// </summary>
    bool IsFullyOutside(IPointCloudNode node);

    /// <summary>
    /// Computes the accepted subset of the supplied selection without modifying it.
    /// </summary>
    /// <param name="node">Node whose local point indices are filtered.</param>
    /// <param name="selected">Read-only input domain of valid local point indices.
    /// Null denotes all indices from zero to <see cref="IPointCloudNode.PointCountCell"/>
    /// (exclusive); an empty set denotes no points.</param>
    /// <returns>
    /// A non-null subset of the input domain. An unchanged selection may be returned
    /// by reference; otherwise the result must be independently owned by the caller,
    /// not reused or modified by the filter after returning. Callers must not mutate a result that
    /// aliases their supplied selection unless they own that selection exclusively.
    /// </returns>
    HashSet<int> FilterPoints(IPointCloudNode node, HashSet<int>? selected = null);

    /// <summary></summary>
    JsonNode Serialize();
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
