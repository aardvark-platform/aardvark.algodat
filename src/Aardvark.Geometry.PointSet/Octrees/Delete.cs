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
using Aardvark.Data;
using Aardvark.Data.Points;

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
            Func<IPointCloudNode, bool> isNodeFullyInside,
            Func<IPointCloudNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            Storage storage, CancellationToken ct
            )
        {
            var root = Delete(node.Root.Value, isNodeFullyInside, isNodeFullyOutside, isPositionInside, storage, ct);
            var newId = Guid.NewGuid().ToString();
            var result = new PointSet(node.Storage, newId, root?.Id, node.SplitLimit);
            node.Storage.Add(newId, result);
            return result;
        }

        /// <summary>
        /// Returns new tree with all points deleted which are inside.
        /// </summary>
        public static IPointCloudNode Delete(this IPointCloudNode root,
            Func<IPointCloudNode, bool> isNodeFullyInside,
            Func<IPointCloudNode, bool> isNodeFullyOutside,
            Func<V3d, bool> isPositionInside,
            Storage storage, CancellationToken ct
            )
        {
            //Report.Error($"[Delete] {root.GetType().Name}.Delete({root.Id})");
            if (root == null) return null;
            if (isNodeFullyInside(root)) return null;
            if (isNodeFullyOutside(root))
            {
                if (!root.IsMaterialized)
                {
                    root = root.Materialize();
                }
                return root;
            }


            var newId = Guid.NewGuid();
            //Report.Error($"[Delete] create {newId}");
            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.NodeId, newId)
                .Add(Durable.Octree.Cell, root.Cell)
                ;

            Guid? newPsId = null;
            Guid? newCsId = null;
            Guid? newNsId = null;
            Guid? newIsId = null;
            Guid? newKsId = null;
            Guid? newKdId = null;

            var ps = root.HasPositions ? new List<V3f>() : null;
            var cs = root.HasColors ? new List<C4b>() : null;
            var ns = root.HasNormals ? new List<V3f>() : null;
            var js = root.HasIntensities ? new List<int>() : null;
            var ks = root.HasClassifications ? new List<byte>() : null;


            var oldPs = root.Positions?.Value;

            if (oldPs == null) return null;

            var oldCs = root.Colors?.Value;
            var oldNs = root.Normals?.Value;
            var oldIs = root.Intensities?.Value;
            var oldKs = root.Classifications?.Value;
            for (var i = 0; i < oldPs.Length; i++)
            {
                if (!isPositionInside(new V3d(oldPs[i]) + root.Center))
                {
                    if (oldPs != null) ps.Add(oldPs[i]);
                    if (oldCs != null) cs.Add(oldCs[i]);
                    if (oldNs != null) ns.Add(oldNs[i]);
                    if (oldIs != null) js.Add(oldIs[i]);
                    if (oldKs != null) ks.Add(oldKs[i]);
                }
            }

            if (ps.Count == 0) return null;



            if (root.Subnodes != null)
            {
                /// inner node
                var newChildren = root.Subnodes.Map(n => n?.Value.Delete(isNodeFullyInside, isNodeFullyOutside, isPositionInside, storage, ct));
                var gotEmpty = newChildren.All(n => n == null);
                if (gotEmpty) return null;
                
                var totalPointCountChildren = newChildren.Where(x => x != null).Sum(s => s.PointCountTree);
                if (totalPointCountChildren < 8192)
                {
                    // make me a leaf.
                    var ps_ = new List<V3d>();
                    var cs_ = new List<C4b>();
                    var ns_ = new List<V3f>();
                    var js_ = new List<int>();
                    var ks_ = new List<byte>();
                    var leaves = MergeExtensions.CollectEverything(root, ps_, cs_, ns_, js_, ks_);

                    var bbExactGlobal_ = new Box3d(ps_);

                    if (ps_.Count == 0) ps_ = null;
                    if (cs_.Count == 0) cs_ = null;
                    if (ns_.Count == 0) ns_ = null;
                    if (js_.Count == 0) js_ = null;
                    if (ks_.Count == 0) ks_ = null;

                    var node = InMemoryPointSet.Build(ps_, cs_, ns_, js_, ks_, bbExactGlobal_, 8192);
                    var psnode = node.ToPointSetNode(root.Storage, isTemporaryImportNode: true);
                    if (root.Cell != psnode.Cell)
                    {
                        return MergeExtensions.JoinTreeToRootCell(root.Cell, psnode, ImportConfig.Default.WithOctreeSplitLimit(8192), collapse: false);
                    }
                    else
                    {
                        return psnode;
                    }
                }
                else
                {
                    // stay inner
                    var newSubnodesIds = newChildren.Map(x => x?.Id ?? Guid.Empty);
                    data = data.Add(Durable.Octree.SubnodesGuids, newSubnodesIds);
                    data = data.Add(Durable.Octree.PointCountTreeLeafs, (long)totalPointCountChildren);

                    var bbglobal = newChildren.Where(x => x != null).Select(x => x.BoundingBoxExactGlobal);
                    var bbExactGlobal_ = new Box3d(bbglobal);
                    data = data.Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal_);

                    //var newSelf = new PointSetNode(data, storage, writeToStore: true);



                    if (root.HasPositions)
                    {
                        newPsId = Guid.NewGuid();
                        var psa = ps.ToArray();
                        storage.Add(newPsId.Value, psa);

                        newKdId = Guid.NewGuid();
                        storage.Add(newKdId.Value, psa.Length != 0 ? psa.BuildKdTree().Data : new PointRkdTreeFData());

                        data = data
                            .Add(Durable.Octree.PositionsLocal3fReference, newPsId)
                            .Add(Durable.Octree.PointRkdTreeFDataReference, newKdId)
                            ;
                    }

                    if (root.HasColors)
                    {
                        newCsId = Guid.NewGuid();
                        storage.Add(newCsId.Value, cs.ToArray());

                        data = data.Add(Durable.Octree.Colors4bReference, newCsId);
                    }

                    if (root.HasNormals)
                    {
                        newNsId = Guid.NewGuid();
                        storage.Add(newNsId.Value, ns.ToArray());

                        data = data.Add(Durable.Octree.Normals3fReference, newNsId);
                    }

                    if (root.HasIntensities)
                    {
                        newIsId = Guid.NewGuid();
                        storage.Add(newIsId.Value, js.ToArray());

                        data = data.Add(Durable.Octree.Intensities1iReference, newIsId);
                    }

                    if (root.HasClassifications)
                    {
                        newKsId = Guid.NewGuid();
                        storage.Add(newKsId.Value, ks.ToArray());

                        data = data.Add(Durable.Octree.Classifications1bReference, newKsId);
                    }


                    //data = data.Add(Durable.Octree.PointCountTreeLeafs, (long)ps.Count);

                    //var bbExactGlobal = new Box3d(ps.Map(p => (V3d)p + root.Center));
                    //data = data.Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal);


                    var result_ = new PointSetNode(data, storage, writeToStore: true);

                    return result_;

                    //return Aardvark.Geometry.Points.LodExtensions.GenerateLod(newSelf, 8192, foo, CancellationToken.None).Result;
                }
            }

            // leaf.


            if (root.HasPositions)
            {
                newPsId = Guid.NewGuid();
                var psa = ps.ToArray();
                storage.Add(newPsId.Value, psa);

                newKdId = Guid.NewGuid();
                storage.Add(newKdId.Value, psa.Length != 0 ? psa.BuildKdTree().Data : new PointRkdTreeFData());

                data = data
                    .Add(Durable.Octree.PositionsLocal3fReference, newPsId)
                    .Add(Durable.Octree.PointRkdTreeFDataReference, newKdId)
                    ;
            }

            if (root.HasColors)
            {
                newCsId = Guid.NewGuid();
                storage.Add(newCsId.Value, cs.ToArray());

                data = data.Add(Durable.Octree.Colors4bReference, newCsId);
            }

            if (root.HasNormals)
            {
                newNsId = Guid.NewGuid();
                storage.Add(newNsId.Value, ns.ToArray());

                data = data.Add(Durable.Octree.Normals3fReference, newNsId);
            }

            if (root.HasIntensities)
            {
                newIsId = Guid.NewGuid();
                storage.Add(newIsId.Value, js.ToArray());

                data = data.Add(Durable.Octree.Intensities1iReference, newIsId);
            }

            if (root.HasClassifications)
            {
                newKsId = Guid.NewGuid();
                storage.Add(newKsId.Value, ks.ToArray());

                data = data.Add(Durable.Octree.Classifications1bReference, newKsId);
            }


            data = data.Add(Durable.Octree.PointCountTreeLeafs, (long) ps.Count);

            var bbExactGlobal = new Box3d(ps.Map(p => (V3d)p + root.Center));
            data = data.Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal);


            var result = new PointSetNode(data, storage, writeToStore: true);
#if DEBUG
            if (result.Id != newId) throw new InvalidOperationException("Invariant 0c351a17-c4bb-40fc-94ba-04fc6a26ca7e.");
            if (storage.GetPointCloudNode(newId) == null) throw new InvalidOperationException("Invariant a5ae64fa-4b60-40a5-88a7-15adc038d6bb.");
#endif
            return result;
        }
    }
}
