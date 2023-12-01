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
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static Aardvark.Base.MultimethodTest;
using static Aardvark.Data.Durable;

namespace Aardvark.Geometry.Points
{
    public class InMemoryPointSet
    {
        private readonly ImmutableDictionary<Def, object> m_data;
        private readonly int m_splitLimit;
        private readonly Node m_root;
        private readonly IList<V3d> m_ps;
        private readonly bool m_hasPartIndices = false;

        public static InMemoryPointSet Build(GenericChunk chunk, int octreeSplitLimit)
            => new(chunk.Data, new Cell(chunk.BoundingBox), octreeSplitLimit);

        public static InMemoryPointSet Build(Chunk chunk, int octreeSplitLimit)
            => Build(chunk.Positions, chunk.Colors, chunk.Normals, chunk.Intensities, chunk.Classifications, chunk.PartIndices, new Cell(chunk.BoundingBox), octreeSplitLimit);

        public static InMemoryPointSet Build(Chunk chunk, Cell rootBounds, int octreeSplitLimit)
            => Build(chunk.Positions, chunk.Colors, chunk.Normals, chunk.Intensities, chunk.Classifications, chunk.PartIndices, rootBounds, octreeSplitLimit);

        public static InMemoryPointSet Build(IList<V3d> ps, IList<C4b>? cs, IList<V3f>? ns, IList<int>? js, IList<byte>? ks, object? partIndices, Cell rootBounds, int octreeSplitLimit)
        {
            if (ps == null) throw new ArgumentNullException(nameof(ps));

            var data = ImmutableDictionary<Def, object>.Empty
                .Add(Octree.PositionsGlobal3d, ps.ToArray())
                ;
            if (cs != null) data = data.Add(Octree.Colors4b, cs.ToArray());
            if (ns != null) data = data.Add(Octree.Normals3f, ns.ToArray());
            if (js != null) data = data.Add(Octree.Intensities1i, js.ToArray());
            if (ks != null) data = data.Add(Octree.Classifications1b, ks.ToArray());

            switch (partIndices)
            {
                case null                 : break;
                case int                x : data = data.Add(Octree.PerCellPartIndex1i , x           ).Add(Octree.PartIndexRange, new Range1i(     x)); break;
                case uint               x : data = data.Add(Octree.PerCellPartIndex1i , (int)x      ).Add(Octree.PartIndexRange, new Range1i((int)x)); break;
                case byte[]             xs: data = data.Add(Octree.PerPointPartIndex1b, xs          ).Add(Octree.PartIndexRange, new Range1i(xs.Select(x => (int)x))); break;
                case IList<byte>        xs: data = data.Add(Octree.PerPointPartIndex1b, xs.ToArray()).Add(Octree.PartIndexRange, new Range1i(xs.Select(x => (int)x))); break;
                case IEnumerable<byte>  xs: data = data.Add(Octree.PerPointPartIndex1b, xs.ToArray()).Add(Octree.PartIndexRange, new Range1i(xs.Select(x => (int)x))); break;
                case short[]            xs: data = data.Add(Octree.PerPointPartIndex1s, xs          ).Add(Octree.PartIndexRange, new Range1i(xs.Select(x => (int)x))); break;
                case IList<short>       xs: data = data.Add(Octree.PerPointPartIndex1s, xs.ToArray()).Add(Octree.PartIndexRange, new Range1i(xs.Select(x => (int)x))); break;
                case IEnumerable<short> xs: data = data.Add(Octree.PerPointPartIndex1s, xs.ToArray()).Add(Octree.PartIndexRange, new Range1i(xs.Select(x => (int)x))); break;
                case int[]              xs: data = data.Add(Octree.PerPointPartIndex1i, xs          ).Add(Octree.PartIndexRange, new Range1i(xs)); break;
                case IList<int>         xs: data = data.Add(Octree.PerPointPartIndex1i, xs.ToArray()).Add(Octree.PartIndexRange, new Range1i(xs)); break;
                case IEnumerable<int>   xs: data = data.Add(Octree.PerPointPartIndex1i, xs.ToArray()).Add(Octree.PartIndexRange, new Range1i(xs)); break;

                default: throw new Exception(
                    $"Unknown part indices type {partIndices.GetType().FullName}. Error 9869c0df-de22-4385-b39f-2b55a8c64f13."
                    );
            }

            return new InMemoryPointSet(data, rootBounds, octreeSplitLimit);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        private InMemoryPointSet(ImmutableDictionary<Def, object> data, Cell cell, int octreeSplitLimit)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            
            foreach (var kv in data)
            {
                if (kv.Key == Octree.PerCellPartIndex1i || kv.Key == Octree.PerCellPartIndex1ui) continue;
                if (kv.Key == Octree.PartIndexRange) { m_hasPartIndices = true; break; }
                if (kv.Value is not Array) throw new ArgumentException($"Entry {kv.Key} must be array.");
            }

            void TryRename(Def from, Def to)
            {
                if (data.TryGetValue(from, out var obj))
                {
                    data = data.Remove(from).Add(to, obj);
                }
            }
            TryRename(GenericChunk.Defs.Positions3f,        Octree.PositionsGlobal3f);
            TryRename(GenericChunk.Defs.Positions3d,        Octree.PositionsGlobal3d);
            TryRename(GenericChunk.Defs.Classifications1b,  Octree.Classifications1b);
            TryRename(GenericChunk.Defs.Classifications1i,  Octree.Classifications1i);
            TryRename(GenericChunk.Defs.Classifications1s,  Octree.Classifications1s);
            TryRename(GenericChunk.Defs.Colors3b,           Octree.Colors3b         );
            TryRename(GenericChunk.Defs.Colors4b,           Octree.Colors4b         );
            TryRename(GenericChunk.Defs.Intensities1i,      Octree.Intensities1i    );
            TryRename(GenericChunk.Defs.Intensities1f,      Octree.Intensities1f    );
            TryRename(GenericChunk.Defs.Normals3f,          Octree.Normals3f        );

            // extract positions ...
            if (data.TryGetValue(Octree.PositionsGlobal3d, out var ps3d))
            {
                m_ps = (V3d[])ps3d;
                data = data.Remove(Octree.PositionsGlobal3d);
            }
            else if (data.TryGetValue(Octree.PositionsGlobal3f, out var ps3f))
            {
                m_ps = ((V3f[])ps3f).Map(p => (V3d)p);
                data = data.Remove(Octree.PositionsGlobal3f);
            }
            else
            {
                throw new Exception("Could not find positions. Please add one of the following entries to 'data': Octree.PositionsGlobal3[df], GenericChunk.Defs.Positions3[df].");
            }

            m_data = data;
            m_splitLimit = octreeSplitLimit;
            m_root = new Node(this, cell);
            for (var i = 0; i < m_ps.Count; i++) m_root.Insert(i);
        }


        public PointSetNode ToPointSetNode(Storage storage, bool isTemporaryImportNode)
        {
            var result = m_root.ToPointSetNode(storage, isTemporaryImportNode);
            return result;
        }

        private class Node
        {
            private readonly InMemoryPointSet _octree;
            private readonly Cell _cell;
            private readonly double _centerX, _centerY, _centerZ;
            private Node[]? _subnodes;
            private List<int>? _ia;
            public Box3d BoundingBox => _cell.BoundingBox;

            public Node(InMemoryPointSet octree, Cell cell)
            {
                _octree = octree;
                _cell = cell;
                var c = cell.BoundingBox.Center;
                _centerX = c.X; _centerY = c.Y; _centerZ = c.Z;
            }


            /// <summary>
            /// The following arrays will be stored as separate entries in the store and referenced via id.
            /// </summary>
            private static readonly Dictionary<Def, Def> StoreAsReference = new()
            {
                { Octree.PositionsLocal3f   , Octree.PositionsLocal3fReference    },
                { Octree.Colors4b           , Octree.Colors4bReference            },
                { Octree.Normals3f          , Octree.Normals3fReference           },
                { Octree.Intensities1i      , Octree.Intensities1iReference       },
                { Octree.Classifications1b  , Octree.Classifications1bReference   },
                { Octree.PerPointPartIndex1b, Octree.PerPointPartIndex1bReference },
                { Octree.PerPointPartIndex1s, Octree.PerPointPartIndex1sReference },
                { Octree.PerPointPartIndex1i, Octree.PerPointPartIndex1iReference },
            };


            internal PointSetNode ToPointSetNode(Storage storage, bool isTemporaryImportNode)
            {
                // final result data (which we will use to create a PointSetNode)
                var resultData = ImmutableDictionary<Def, object>.Empty
                    .Add(Octree.NodeId, Guid.NewGuid())
                    .Add(Octree.Cell, _cell)
                    ;

                if (isTemporaryImportNode) resultData = resultData.Add(PointSetNode.TemporaryImportNode, 0);

                // keep track whether we really convert all existing data entries to a PointSetNode
                // (so we can fail if something is left over at the end, something we did not expect or handle correctly)
                var notHandled = new HashSet<Def>(_octree.m_data.Keys);

                var center = new V3d(_centerX, _centerY, _centerZ);

                if (_subnodes == null)
                {
                    // leaf node ...

                    #region positions

                    // transform global float64 positions to local float32 positions (relative to cell center) to save space
                    var psGlobal = _octree.m_ps;
                    V3f[] psLocal;
                    if (_ia != null)
                    {
                        psLocal = new V3f[_ia.Count];
                        for (var i = 0; i < _ia.Count; i++) psLocal[i] = (V3f)(psGlobal[_ia[i]] - center); // relative to center
                    }
                    else
                    {
                        psLocal = new V3f[psGlobal.Count];
                        for (var i = 0; i < psGlobal.Count; i++) psLocal[i] = (V3f)(psGlobal[i] - center); // relative to center
                    }

                    var bbExactLocal = new Box3f(psLocal);

                    resultData = resultData
                        .Add(Octree.PositionsLocal3f, psLocal)
                        .Add(Octree.PointCountCell, psLocal.Length)
                        .Add(Octree.PointCountTreeLeafs, (long)psLocal.Length)
                        .Add(Octree.BoundingBoxExactLocal, bbExactLocal)
                        .Add(Octree.BoundingBoxExactGlobal, (Box3d)bbExactLocal + center)
                        ;

                    #endregion

                    #region per-point attribute arrays

                    var handledPerPointAttributeArrays = new List<Def>();
                    foreach (var k in notHandled)
                    {
                        if (StoreAsReference.TryGetValue(k, out var refdef))
                        {
                            var xs = (Array)_octree.m_data[k];
                            if (_ia != null) xs = xs.Subset(_ia);

                            // store separately and reference by id ...
                            var id = Guid.NewGuid();
                            storage.Add(id, xs);
                            resultData = resultData.Add(refdef, id);

                            handledPerPointAttributeArrays.Add(k);
                        }
                    }
                    foreach (var k in handledPerPointAttributeArrays) notHandled.Remove(k);

                    #endregion

                    #region other well-known entries

                    void copy(Def def)
                    {
                        if (_octree.m_data.TryGetValue(def, out var o))
                        {
                            resultData = resultData.Add(def, o);
                            notHandled.Remove(def);
                        }
                    }

                    copy(Octree.PartIndexRange);
                    copy(Octree.PerCellPartIndex1i);
                    copy(Octree.PerCellPartIndex1ui);

                    #endregion

                    if (notHandled.Count > 0) throw new Exception(
                        $"Unhandled entries {string.Join(", ", notHandled.Select(x => x.ToString()))}. " +
                        $"Invariant 42656f92-f5ac-43ca-a1b7-c7ec95fe9cb3."
                        );

                    // create and store result
                    var result = new PointSetNode(resultData, storage, writeToStore: true);
                    if (storage.GetPointCloudNode(result.Id) == null) throw new InvalidOperationException("Invariant 9e863bc5-e9f4-4d39-bd53-3d81e12af6b1.");
                    return result;
                }
                else
                {
                    // inner node ...

                    var subcells = _subnodes?.Map(x => x?.ToPointSetNode(storage, isTemporaryImportNode));
                    var subcellIds = subcells?.Map(x => x?.Id);

                    var pointCountTreeLeafs =  subcells.Sum(n => n != null ? n.PointCountTree : 0);
                    var bbExactGlobal = new Box3d(subcells.Where(x => x != null).Select(x => x!.BoundingBoxExactGlobal));
                    var bbExactLocal = (Box3f)(bbExactGlobal - center);

                    resultData = resultData
                        .Add(Octree.PointCountTreeLeafs, pointCountTreeLeafs)
                        .Add(Octree.BoundingBoxExactLocal, bbExactLocal)
                        .Add(Octree.BoundingBoxExactGlobal, bbExactGlobal)
                        .Add(Octree.SubnodesGuids, subcellIds.Map(x => x ?? Guid.Empty))
                        ;

#if DEBUG
                    // check if subnodes exist in store
                    for (var i = 0; i < 8; i++)
                    {
                        var x = subcellIds![i];
                        if (x.HasValue)
                        {
                            var id = x.Value;
                            if (storage.GetPointCloudNode(id) == null) throw new InvalidOperationException("Invariant 01830b8b-3c0e-4a8b-a1bd-bfd1b1be1844.");
                        }
                    }
#endif

                    // collect part-index-ranges from subnodes
                    {
                        var subRanges = subcells.Select(x => PartIndexUtils.GetRange(x?.Properties));
                        var partIndexRange = PartIndexUtils.MergeRanges(subRanges);
                        if (partIndexRange.HasValue)
                        {
                            resultData = resultData.Add(Octree.PartIndexRange, partIndexRange.Value);
                        }
                    }

                    // create and store result
                    var result = new PointSetNode(resultData, storage, writeToStore: true);
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
                if (_ia == null) throw new Exception($"Expected index array. Error 809d08b1-e5d8-4ef2-9c46-67a4415173a3.");

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
