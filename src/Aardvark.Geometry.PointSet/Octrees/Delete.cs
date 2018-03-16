/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
            var root = Delete(node.Root.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, ct);
            var newId = Guid.NewGuid().ToString();
            var result = new PointSet(node.Storage, newId, root?.Id, node.SplitLimit);
            node.Storage.Add(newId, result, ct);
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
            
            if (node.IsLeaf)
            {
                Guid? newPsId = null;
                Guid? newCsId = null;
                Guid? newNsId = null;
                Guid? newKdId = null;

                if (!node.HasPositions) throw new InvalidOperationException();

                var ps = new List<V3f>();
                var cs = node.HasColors ? new List<C4b>() : null;
                var ns = node.HasNormals ? new List<V3f>() : null;
                var oldPsAbsolute = node.PositionsAbsolute;
                var oldPs = node.Positions.Value;
                var oldCs = node.Colors?.Value;
                var oldNs = node.Normals?.Value;
                for (var i = 0; i < oldPsAbsolute.Length; i++)
                {
                    if (!isPositionInside(oldPsAbsolute[i]))
                    {
                        ps.Add(oldPs[i]);
                        if (oldCs != null) cs.Add(oldCs[i]);
                        if (oldNs != null) ns.Add(oldNs[i]);
                    }
                }

                if (ps.Count > 0)
                {
                    newPsId = Guid.NewGuid();
                    var psa = ps.ToArray();
                    node.Storage.Add(newPsId.Value, psa, ct);

                    newKdId = Guid.NewGuid();
                    node.Storage.Add(newKdId.Value, psa.BuildKdTree().Data, ct);

                    if (node.HasColors)
                    {
                        newCsId = Guid.NewGuid();
                        node.Storage.Add(newCsId.Value, cs.ToArray(), ct);
                    }

                    if (node.HasNormals)
                    {
                        newNsId = Guid.NewGuid();
                        node.Storage.Add(newNsId.Value, ns.ToArray(), ct);
                    }

                    var result = new PointSetNode(node.Cell, ps.Count, newPsId, newCsId, newKdId, newNsId, node.Storage);
                    if (node.HasLodPositions) result = result.WithLod();
                    return result;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                Guid? newLodPsId = null;
                Guid? newLodCsId = null;
                Guid? newLodNsId = null;
                Guid? newLodKdId = null;

                if (node.HasLodPositions)
                {
                    var ps = node.HasLodPositions ? new List<V3f>() : null;
                    var cs = node.HasLodColors ? new List<C4b>() : null;
                    var ns = node.HasLodNormals ? new List<V3f>() : null;
                    var oldLodPsAbsolute = node.LodPositionsAbsolute;
                    var oldLodPs = node.LodPositions.Value;
                    var oldLodCs = node.LodColors?.Value;
                    var oldLodNs = node.LodNormals?.Value;
                    for (var i = 0; i < oldLodPsAbsolute.Length; i++)
                    {
                        if (!isPositionInside(oldLodPsAbsolute[i]))
                        {
                            ps.Add(oldLodPs[i]);
                            if (oldLodCs != null) cs.Add(oldLodCs[i]);
                            if (oldLodNs != null) ns.Add(oldLodNs[i]);
                        }
                    }

                    if (ps.Count > 0)
                    {
                        newLodPsId = Guid.NewGuid();
                        var psa = ps.ToArray();
                        node.Storage.Add(newLodPsId.Value, psa, ct);

                        newLodKdId = Guid.NewGuid();
                        node.Storage.Add(newLodKdId.Value, psa.BuildKdTree().Data, ct);

                        if (node.HasLodColors)
                        {
                            newLodCsId = Guid.NewGuid();
                            node.Storage.Add(newLodCsId.Value, cs.ToArray(), ct);
                        }

                        if (node.HasLodNormals)
                        {
                            newLodNsId = Guid.NewGuid();
                            node.Storage.Add(newLodNsId.Value, ns.ToArray(), ct);
                        }
                    }
                }

                var newSubnodes = node.Subnodes.Map(n => n?.Value.Delete(isNodeFullyInside, isNodeFullyOutside, isPositionInside, ct));
                if (newSubnodes.All(n => n == null)) return null;
                return node.WithLod(newLodPsId, newLodCsId, newLodNsId, newLodKdId, newSubnodes);
            }
        }
    }
}
