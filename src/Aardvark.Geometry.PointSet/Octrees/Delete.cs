/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class DeleteExtensions
    {
        /// <summary>
        /// Returns new pointset with all points deleted which are inside.
        /// </summary>
        public static PointSet Delete(this PointSet node,
            Func<PointSetNode, bool> isNodeFullyInside,
            Func<PointSetNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            CancellationToken ct
            )
        {
            var root = Delete((PointSetNode)node.Octree.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, ct);
            var newId = Guid.NewGuid().ToString();
            var result = new PointSet(node.Storage, newId, root?.Id, node.SplitLimit);
            node.Storage.Add(newId, result);
            return result;
        }

        /// <summary>
        /// </summary>
        public static PointSetNode Delete(this PointSetNode node,
            Func<PointSetNode, bool> isNodeFullyInside,
            Func<PointSetNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            CancellationToken ct
            )
        {
            if (node == null) return null;
            if (isNodeFullyInside(node)) return null;
            if (isNodeFullyOutside(node)) return node;

            Guid? newPsId = null;
            Guid? newCsId = null;
            Guid? newNsId = null;
            Guid? newIsId = null;
            Guid? newKsId = null;
            Guid? newKdId = null;
            
            var ps = node.HasPositions ? new List<V3f>() : null;
            var cs = node.HasColors ? new List<C4b>() : null;
            var ns = node.HasNormals ? new List<V3f>() : null;
            var js = node.HasIntensities ? new List<int>() : null;
            var ks = node.HasClassifications ? new List<byte>() : null;
            var oldPsAbsolute = node.PositionsAbsolute;
            var oldPs = node.Positions.Value;
            var oldCs = node.Colors?.Value;
            var oldNs = node.Normals?.Value;
            var oldIs = node.Intensities?.Value;
            var oldKs = node.Classifications?.Value;
            for (var i = 0; i < oldPsAbsolute.Length; i++)
            {
                if (!isPositionInside(oldPsAbsolute[i]))
                {
                    ps.Add(oldPs[i]);
                    if (oldCs != null) cs.Add(oldCs[i]);
                    if (oldNs != null) ns.Add(oldNs[i]);
                    if (oldIs != null) js.Add(oldIs[i]);
                    if (oldKs != null) ks.Add(oldKs[i]);
                }
            }

            if (ps.Count > 0)
            {
                newPsId = Guid.NewGuid();
                var psa = ps.ToArray();
                node.Storage.Add(newPsId.Value, psa);

                newKdId = Guid.NewGuid();
                node.Storage.Add(newKdId.Value, psa.BuildKdTree().Data);

                if (node.HasColors)
                {
                    newCsId = Guid.NewGuid();
                    node.Storage.Add(newCsId.Value, cs.ToArray());
                }

                if (node.HasNormals)
                {
                    newNsId = Guid.NewGuid();
                    node.Storage.Add(newNsId.Value, ns.ToArray());
                }

                if (node.HasIntensities)
                {
                    newIsId = Guid.NewGuid();
                    node.Storage.Add(newIsId.Value, js.ToArray());
                }

                if (node.HasClassifications)
                {
                    newKsId = Guid.NewGuid();
                    node.Storage.Add(newKsId.Value, ks.ToArray());
                }
            }

            var newSubnodes = node.Subnodes?.Map(n => n?.Value.Delete(isNodeFullyInside, isNodeFullyOutside, isPositionInside, ct));
            if (newSubnodes?.All(n => n == null) == true) return null;
            return node.WithData(ImmutableDictionary<Guid, object>.Empty, ps.Count, newPsId, newCsId, newNsId, newIsId, newKdId, newKsId, newSubnodes);
        }
    }
}
