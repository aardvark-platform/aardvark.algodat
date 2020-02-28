/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public class InMemoryPointSet
    {
        private readonly long m_splitLimit;
        private readonly IList<V3d> m_ps;
        private readonly IList<C4b> m_cs;
        private readonly IList<V3f> m_ns;
        private readonly IList<int> m_is;
        private readonly IList<byte> m_ks;
        private readonly IList<V3f> m_vs;
        private readonly Node m_root;
        private readonly long m_insertedPointsCount = 0;
        private long m_duplicatePointsCount = 0;

        /// <summary>
        /// </summary>
        public static InMemoryPointSet Build(Chunk chunk, long octreeSplitLimit)
            => Build(chunk.Positions, chunk.Colors, chunk.Normals, chunk.Intensities, chunk.Classifications, chunk.Velocities, chunk.BoundingBox, octreeSplitLimit);

        /// <summary>
        /// </summary>
        public static InMemoryPointSet Build(IList<V3d> ps, IList<C4b> cs, IList<V3f> ns, IList<int> js, IList<byte> ks, IList<V3f> vs, Box3d bounds, long octreeSplitLimit)
            => new InMemoryPointSet(ps, cs, ns, js, ks, vs, bounds, octreeSplitLimit);

        /// <summary>
        /// </summary>
        public static InMemoryPointSet Build(Chunk chunk, Cell rootBounds, long octreeSplitLimit)
            => Build(chunk.Positions, chunk.Colors, chunk.Normals, chunk.Intensities, chunk.Classifications, chunk.Velocities, rootBounds, octreeSplitLimit);

        /// <summary>
        /// </summary>
        public static InMemoryPointSet Build(IList<V3d> ps, IList<C4b> cs, IList<V3f> ns, IList<int> js, IList<byte> ks, IList<V3f> vs, Cell bounds, long octreeSplitLimit)
            => new InMemoryPointSet(ps, cs, ns, js, ks, vs, bounds, octreeSplitLimit);

        private InMemoryPointSet(IList<V3d> ps, IList<C4b> cs, IList<V3f> ns, IList<int> js, IList<byte> ks, IList<V3f> vs, Box3d bounds, long octreeSplitLimit)
            : this(ps, cs, ns, js, ks, vs, new Cell(bounds), octreeSplitLimit)
            { }

        private InMemoryPointSet(IList<V3d> ps, IList<C4b> cs, IList<V3f> ns, IList<int> js, IList<byte> ks, IList<V3f> vs, Cell bounds, long octreeSplitLimit)
        {
#if DEBUG
            //if (new Box3d(ps).Size.Length < 0.001) Debugger.Break();
#endif
            m_ps = ps;
            m_cs = cs;
            m_ns = ns;
            m_is = js;
            m_ks = ks;
            m_vs = vs;
            m_splitLimit = octreeSplitLimit;

            m_root = new Node(this, bounds);
            m_insertedPointsCount = ps != null ? ps.Count : 0;
            for (var i = 0; i < ps.Count; i++) m_root.Insert(i);
        }

        /// <summary></summary>
        public PointSetNode ToPointSetNode(Storage storage, bool isTemporaryImportNode)
        {
            var result = m_root.ToPointSetCell(storage, isTemporaryImportNode);
#if DEBUG
            if (m_duplicatePointsCount > 0)
            {
                var percent = (m_duplicatePointsCount / (double)m_insertedPointsCount) * 100.0;
                Report.Warn($"[INFO] Removed {m_duplicatePointsCount}/{m_insertedPointsCount} duplicate points ({percent:0.00}%).");
            }
#endif
            return result;
        }

        private class Node
        {
            private readonly InMemoryPointSet _octree;
            private readonly Cell _cell;
            private readonly double _centerX, _centerY, _centerZ;
            private Node[] _subnodes;
            private List<int> _ia;
            public Box3d BoundingBox => _cell.BoundingBox;

            public Node(InMemoryPointSet octree, Cell cell)
            {
                _octree = octree;
                _cell = cell;
                var c = cell.BoundingBox.Center;
                _centerX = c.X; _centerY = c.Y; _centerZ = c.Z;
            }

            public PointSetNode ToPointSetCell(Storage storage, bool isTemporaryImportNode)
            {
                var center = new V3d(_centerX, _centerY, _centerZ);
                V3f[] ps = null;
                C4b[] cs = null;
                V3f[] ns = null;
                int[] js = null;
                byte[] ks = null;
                V3f[] vs = null;
                
                if (_ia != null)
                {
                    var allPs = _octree.m_ps;
                    var count = _ia.Count;

                    ps = new V3f[count];
                    for (var i = 0; i < count; i++) ps[i] = (V3f)(allPs[_ia[i]] - center);

                    if (_octree.m_cs != null)
                    {
                        var allCs = _octree.m_cs;
                        cs = new C4b[count];
                        for (var i = 0; i < count; i++) cs[i] = allCs[_ia[i]];
                    }

                    if (_octree.m_ns != null)
                    {
                        var allNs = _octree.m_ns;
                        ns = new V3f[count];
                        for (var i = 0; i < count; i++) ns[i] = allNs[_ia[i]];
                    }

                    if (_octree.m_is != null)
                    {
                        var allIs = _octree.m_is;
                        js = new int[count];
                        for (var i = 0; i < count; i++) js[i] = allIs[_ia[i]];
                    }

                    if (_octree.m_ks != null)
                    {
                        var allKs = _octree.m_ks;
                        ks = new byte[count];
                        for (var i = 0; i < count; i++) ks[i] = allKs[_ia[i]];
                    }

                    if (_octree.m_vs != null)
                    {
                        var allVs = _octree.m_vs;
                        vs = new V3f[count];
                        for (var i = 0; i < count; i++) vs[i] = allVs[_ia[i]];
                    }
                }

                Guid? psId = ps != null ? (Guid?)Guid.NewGuid() : null;
                Guid? csId = cs != null ? (Guid?)Guid.NewGuid() : null;
                Guid? nsId = ns != null ? (Guid?)Guid.NewGuid() : null;
                Guid? isId = js != null ? (Guid?)Guid.NewGuid() : null;
                Guid? ksId = ks != null ? (Guid?)Guid.NewGuid() : null;
                Guid? vsId = vs != null ? (Guid?)Guid.NewGuid() : null;

                var subcells = _subnodes?.Map(x => x?.ToPointSetCell(storage, isTemporaryImportNode));
                var subcellIds = subcells?.Map(x => x?.Id);
                var isLeaf = _subnodes == null;

#if DEBUG
                if (_subnodes != null)
                {
                    if (ps != null)
                    {
                        throw new InvalidOperationException("Invariant d98ea55b-760c-4564-8076-ce9cf7d293a0.");
                    }

                    for (var i = 0; i < 8; i++)
                    {
                        var sn = _subnodes[i]; if (sn == null) continue;
                        if (sn._cell.Exponent != this._cell.Exponent - 1)
                        {
                            throw new InvalidOperationException("Invariant 2c33afb4-683b-4f71-9e1f-36ec4a79fba1.");
                        }
                    }
                }
#endif
                var pointCountTreeLeafs = subcells != null
                    ? subcells.Sum(n => n != null ? n.PointCountTree : 0)
                    : ps.Length
                    ;

                var data = ImmutableDictionary<Durable.Def, object>.Empty
                    .Add(Durable.Octree.NodeId, Guid.NewGuid())
                    .Add(Durable.Octree.Cell, _cell)
                    .Add(Durable.Octree.PointCountTreeLeafs, pointCountTreeLeafs)
                    ;

                if (isTemporaryImportNode)
                {
                    data = data.Add(PointSetNode.TemporaryImportNode, 0);
                }

                if (psId != null)
                {
                    storage.Add(psId.ToString(), ps);
                    var bbExactLocal = new Box3f(ps);

                    data = data
                        .Add(Durable.Octree.PointCountCell, ps.Length)
                        .Add(Durable.Octree.PositionsLocal3fReference, psId.Value)
                        .Add(Durable.Octree.BoundingBoxExactLocal, bbExactLocal)
                        ;

                    if (isLeaf)
                    {
                        var bbExactGlobal = (Box3d)bbExactLocal + center;
                        data = data
                            .Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal)
                            ;
                    }
                }
                else
                {
                    data = data
                        .Add(Durable.Octree.PointCountCell, 0)
                        ;
                }

                if (csId != null) { storage.Add(csId.ToString(), cs); data = data.Add(Durable.Octree.Colors4bReference, csId.Value); }
                if (nsId != null) { storage.Add(nsId.ToString(), ns); data = data.Add(Durable.Octree.Normals3fReference, nsId.Value); }
                if (isId != null) { storage.Add(isId.ToString(), js); data = data.Add(Durable.Octree.Intensities1iReference, isId.Value); }
                if (ksId != null) { storage.Add(ksId.ToString(), ks); data = data.Add(Durable.Octree.Classifications1bReference, ksId.Value); }
                if (vsId != null) { storage.Add(vsId.ToString(), vs); data = data.Add(Durable.Octree.Velocities3fReference, vsId.Value); }

                if (isLeaf) // leaf
                {
                    var result = new PointSetNode(data, storage, writeToStore: true);
                    if (storage.GetPointCloudNode(result.Id) == null) throw new InvalidOperationException("Invariant d1022027-2dbf-4b11-9b40-4829436f5789.");
                    return result;
                }
                else
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var x = subcellIds[i];
                        if (x.HasValue)
                        {
                            var id = x.Value;
                            if (storage.GetPointCloudNode(id) == null) throw new InvalidOperationException("Invariant 01830b8b-3c0e-4a8b-a1bd-bfd1b1be1844.");
                        }
                    }
                    var bbExactGlobal = new Box3d(subcells.Where(x => x != null).Select(x => x.BoundingBoxExactGlobal));
                    data = data
                        .Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal)
                        .Add(Durable.Octree.SubnodesGuids, subcellIds.Map(x => x ?? Guid.Empty))
                        ;
                    var result = new PointSetNode(data, storage, writeToStore: true);
                    if (storage.GetPointCloudNode(result.Id) == null) throw new InvalidOperationException("Invariant 7b09eccb-b6a0-4b99-be7a-eeff53b6a98b.");
                    return result;
                }
            }
            
            public Node Insert(int index)
            {
                if (_subnodes != null)
                {
                    var p = _octree.m_ps[index];
                    var si = GetSubIndex(p);
                    if (_subnodes[si] == null) _subnodes[si] = new Node(_octree, _cell.GetOctant(si));
                    return _subnodes[si].Insert(index);
                }
                else
                {
                    if (_ia == null)
                    {
                        _ia = new List<int>();
                    }
                    else
                    {
                        if (_octree.m_ps[index] == _octree.m_ps[_ia[0]])
                        {
                            // duplicate -> do not add
                            _octree.m_duplicatePointsCount++;
                            return this;
                        }
                    }
                    
                    _ia.Add(index);
                    
                    if (_ia.Count > _octree.m_splitLimit)
                    {
                        Split();
                    }

                    return this;
                }
            }
            
            private Node Split()
            {
#if DEBUG
                var ps = _ia.Map(i => _octree.m_ps[i]).ToArray();
                foreach (var p in ps)
                {
                    if (!BoundingBox.Contains(p))
                    {
                        throw new InvalidDataException($"{p} is not contained in {BoundingBox}");
                    }
                }
#endif
                var imax = _ia.Count;
                if (imax <= _octree.m_splitLimit) throw new InvalidOperationException();
                if (_subnodes != null) throw new InvalidOperationException();

                _subnodes = new Node[8];

                for (var i = 0; i < imax; i++)
                {
                    var pointIndex = _ia[i];
                    var si = GetSubIndex(_octree.m_ps[pointIndex]);
                    if (_subnodes[si] == null) _subnodes[si] = new Node(_octree, _cell.GetOctant(si));
                    _subnodes[si].Insert(pointIndex);
                }

#if DEBUG
                var subnodeCount = _subnodes.Count(x => x != null);
                if (subnodeCount == 0) throw new InvalidOperationException();
#endif

                _ia = null;
                return this;
            }

            private int GetSubIndex(V3d p)
            {
                var i = 0;
                if (p.X >= _centerX) i = 1;
                if (p.Y >= _centerY) i |= 2;
                if (p.Z >= _centerZ) i |= 4;
                return i;
            }
        }
    }
}
