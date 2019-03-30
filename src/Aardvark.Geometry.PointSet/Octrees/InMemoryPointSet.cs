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
using Aardvark.Base;
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
        private IList<V3d> m_ps;
        private IList<C4b> m_cs;
        private IList<V3f> m_ns;
        private IList<int> m_is;
        private IList<byte> m_ks;
        private Node m_root;
        private long m_insertedPointsCount = 0;
        private long m_duplicatePointsCount = 0;

        /// <summary>
        /// </summary>
        public static InMemoryPointSet Build(Chunk chunk, long octreeSplitLimit)
            => Build(chunk.Positions, chunk.Colors, chunk.Normals, chunk.Intensities, chunk.Classifications, chunk.BoundingBox, octreeSplitLimit);

        /// <summary>
        /// </summary>
        public static InMemoryPointSet Build(IList<V3d> ps, IList<C4b> cs, IList<V3f> ns, IList<int> js, IList<byte> ks, Box3d bounds, long octreeSplitLimit)
            => new InMemoryPointSet(ps, cs, ns, js, ks, bounds, octreeSplitLimit);

        /// <summary>
        /// </summary>
        public static InMemoryPointSet Build(IList<V3d> ps, IList<C4b> cs, IList<V3f> ns, IList<int> js, IList<byte> ks, Cell bounds, long octreeSplitLimit)
            => new InMemoryPointSet(ps, cs, ns, js, ks, bounds, octreeSplitLimit);

        private InMemoryPointSet(IList<V3d> ps, IList<C4b> cs, IList<V3f> ns, IList<int> js, IList<byte> ks, Box3d bounds, long octreeSplitLimit)
            : this(ps, cs, ns, js, ks, new Cell(bounds), octreeSplitLimit)
            { }

        private InMemoryPointSet(IList<V3d> ps, IList<C4b> cs, IList<V3f> ns, IList<int> js, IList<byte> ks, Cell bounds, long octreeSplitLimit)
        {
            m_ps = ps;
            m_cs = cs;
            m_ns = ns;
            m_is = js;
            m_ks = ks;
            m_splitLimit = octreeSplitLimit;

            m_root = new Node(this, bounds);
            m_insertedPointsCount = ps.Count;
            for (var i = 0; i < ps.Count; i++) m_root.Insert(i);
        }

        /// <summary></summary>
        [Obsolete("Use ToPointSetNode instead.")]
        public PointSetNode ToPointSetCell(Storage storage, double kdTreeEps = 1e-6, CancellationToken ct = default)
            => ToPointSetNode(storage, kdTreeEps, ct);

        /// <summary></summary>
        public PointSetNode ToPointSetNode(Storage storage, double kdTreeEps = 1e-6, CancellationToken ct = default)
        {
            var result = m_root.ToPointSetCell(storage, ct, kdTreeEps);
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

            public PointSetNode ToPointSetCell(Storage storage, CancellationToken ct, double kdTreeEps = 1e-6)
            {
                var center = new V3d(_centerX, _centerY, _centerZ);
                V3f[] ps = null;
                C4b[] cs = null;
                V3f[] ns = null;
                int[] js = null;
                byte[] ks = null;
                PointRkdTreeD<V3f[], V3f> kdTree = null;
                
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

                    kdTree = new PointRkdTreeD<V3f[], V3f>(
                        3, ps.Length, ps,
                        (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                        (a, b) => V3f.Distance(a, b), (i, a, b) => b - a,
                        (a, b, c) => VecFun.DistanceToLine(a, b, c), VecFun.Lerp, kdTreeEps
                        );
                    
                }

                Guid? psId = ps != null ? (Guid?)Guid.NewGuid() : null;
                Guid? csId = cs != null ? (Guid?)Guid.NewGuid() : null;
                Guid? nsId = ns != null ? (Guid?)Guid.NewGuid() : null;
                Guid? isId = js != null ? (Guid?)Guid.NewGuid() : null;
                Guid? ksId = ks != null ? (Guid?)Guid.NewGuid() : null;
                Guid? kdId = kdTree != null ? (Guid?)Guid.NewGuid() : null;
                
                var subcells = _subnodes?.Map(x => x?.ToPointSetCell(storage, ct, kdTreeEps));
                var subcellIds = subcells?.Map(x => x?.Id);

#if DEBUG
                if (ps != null && _subnodes != null) throw new InvalidOperationException();
#endif
                var pointCountTree = subcells != null
                    ? subcells.Sum(n => n != null ? n.PointCountTree : 0)
                    : ps.Length
                    ;

                if (psId != null) storage.Add(psId.ToString(), ps);
                if (csId != null) storage.Add(csId.ToString(), cs);
                if (nsId != null) storage.Add(nsId.ToString(), ns);
                if (isId != null) storage.Add(isId.ToString(), js);
                if (ksId != null) storage.Add(ksId.ToString(), ks);
                if (kdId != null) storage.Add(kdId.ToString(), kdTree.Data);



                if (subcellIds == null) // leaf
                {
                    throw new NotImplementedException();
                    //return new PointSetNode(_cell, pointCountTree, ImmutableDictionary<Guid, object>.Empty,
                    //    psId, csId, kdId, nsId, isId, ksId, storage);
                }
                else
                {
                    throw new NotImplementedException();
                    //return new PointSetNode(_cell, pointCountTree, ImmutableDictionary<Guid, object>.Empty, subcellIds, storage);
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
