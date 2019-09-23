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
using System;

namespace Aardvark.Geometry
{
    public static class BigEdgeRef
    {
        public static long Create(long ei, long es) => (ei << 1) | es;
        public static long Index(long er) => er >> 1;
        public static long Side(long er) => er & 1;
        public static long Reversed(long edgeRef) => edgeRef ^ 1;
    }

    public struct BigFaceRef
    {
        public long Index;
        public int Side;

        public static readonly BigFaceRef Invalid = new BigFaceRef(long.MinValue, int.MinValue);

        #region Constructor

        public BigFaceRef(long index, int side) { Index = index; Side = side; }

        #endregion

        #region Properties

        public bool IsValid => Index >= 0;

        public bool IsNonManifoldEdgeRef => Index != long.MinValue;

        public long NonManifoldEdgeRef(out bool flipped)
        {
            long er = -1 - Index;
            flipped = (er & 1) != 0;
            return er | 1;
        }

        #endregion

        #region Operators

        public static bool operator ==(BigFaceRef fr0, BigFaceRef fr1)
        {
            return fr0.Index == fr1.Index && fr0.Side == fr1.Side;
        }

        public static bool operator !=(BigFaceRef fr0, BigFaceRef fr1)
        {
            return fr0.Index != fr1.Index || fr0.Side != fr1.Side;
        }

        #endregion

        #region Overrides

        public override int GetHashCode() => HashCode.Combine(Index, Side);

        public override bool Equals(object obj)
        {
            return (obj is BigFaceRef) ? this == (BigFaceRef)obj : false;
        }

        #endregion
    }

    public class BigTopology
    {
        // input members
        private long m_faceCount;
        private long m_faceVertexCount;
        private long m_vertexCount;

        private long[] m_firstIndexArray;
        private long[] m_faceVertexIndexArray;

        #region Constructors

        public BigTopology(
            long faceCount, long faceVertexCount, long vertexCount, long eulerCharacteristic, bool strict,
            long[] firstIndices, long[] faceVertexIndexArray)
        {
            m_faceCount = faceCount;
            m_faceVertexCount = faceVertexCount;
            m_vertexCount = vertexCount;

            m_firstIndexArray = firstIndices;
            m_faceVertexIndexArray = faceVertexIndexArray;

            BuildTopology(eulerCharacteristic, strict);
        }

        #endregion

        #region Properties

        public long EulerCharacteristic { get; private set; }
        public long EdgeCount { get; private set; }
        public long BorderEdgeCount { get; private set; }
        public long NonManifoldEdgeCount { get; private set; }
        public long[] FaceEdgeRefArray { get; private set; }
        public BigFaceRef[] EdgeFaceRefArray { get; private set; }

        #endregion

        #region BuildTopology and Helpers

        private static long TopologyVertexRef(long vi, long vs) => (vi << 2) | vs;
        private static long TopologyVertexIndexOf(long vr) => vr >> 2;
        private static long TopologyVertexSideOf(long vr) => vr & 3;

        /// <summary>
        /// Check if the edge is in the edge list of vertex v0.
        /// </summary>
        private static long FindEdgeInList(
                BigFaceRef[] edgeFaceRefArray, long v0, long v1)
        {
            #if DEBUG
            long count = 0;
            #endif
            long otherVertexRef;
            long edgeRef = BigEdgeRef.Create(v0, 0);
            do
            {
                otherVertexRef = edgeFaceRefArray[edgeRef].Index;
                if (TopologyVertexIndexOf(otherVertexRef) == v1)  // found edge!
                {
                    // check if its a nonmanifold edge, use it if unused,
                    // search first unused, use it and mark it
                    if (TopologyVertexSideOf(otherVertexRef) < 3)
                    {
                        if (TopologyVertexSideOf(otherVertexRef) == 2)
                            edgeFaceRefArray[edgeRef].Index
                                = TopologyVertexRef(v1, 3);
                        return edgeRef;
                    }
                }
                edgeRef = edgeFaceRefArray[edgeRef | 1].Index;
                #if DEBUG
                ++count;
                #endif
            }
            while (edgeRef >= 0);
            return -1;
        }

        private static void AppendEdge(
                ref BigFaceRef[] efra, ref long freeEdgeRef,
                long firstEdgeRef, long lastEdgeRef, long vertexRef)
        {
            if (efra[firstEdgeRef].Index >= 0)
            {
                long count = efra.Length;
                if (freeEdgeRef >= count)
                {
                    efra = efra.Resized(Fun.Max(count + 2, (~1) & (long)(1.2 * count)));
                    efra.Set(freeEdgeRef, long.MaxValue, new BigFaceRef(long.MinValue, int.MinValue));
                }
                efra[lastEdgeRef | 1].Index = freeEdgeRef;
                efra[freeEdgeRef].Index = vertexRef;
                freeEdgeRef += 2;
            }
            else
            {
                efra[firstEdgeRef].Index = vertexRef;
            }
        }

        private static void InsertEdge(
                ref BigFaceRef[] efra, ref long freeEdgeRef,
                long firstEdgeRef, long vertexRef)
        {
            if (efra[firstEdgeRef].Index >= 0)
            {
                long count = efra.Length;
                if (freeEdgeRef >= count)
                {
                    efra = efra.Resized(Fun.Max(count + 2, (~1) & (long)(1.2 * count)));
                    efra.Set(freeEdgeRef, long.MaxValue, new BigFaceRef(long.MinValue, int.MinValue));
                }
                efra[freeEdgeRef].Index = efra[firstEdgeRef].Index;
                efra[freeEdgeRef | 1].Index = efra[firstEdgeRef | 1].Index;

                efra[firstEdgeRef].Index = vertexRef;
                efra[firstEdgeRef | 1].Index = freeEdgeRef;

                freeEdgeRef += 2;
            }
            else
            {
                efra[firstEdgeRef].Index = vertexRef;
            }
        }

        /// <summary>
        /// The edgeFaceRefArray is misused to contain a list of edges for
        /// each vertex. For each vertex v, the list starts at the index v. As
        /// the edgeFaceRefArray can hold two values at each index, the
        /// first value (side 0) is used as the vertex index of the other
        /// vertex of the edge, and the second value (side 1) is used as a
        /// pointer to the next edge in the list. Each edge should be member
        /// of exactly one of the two edge lists of its vertices. When
        /// building up this data structure, it is necessary to look into both
        /// vertex lists if an edge has already been inserted.
        /// By adding external edges at the front of the vertex v0 list, and
        /// internal edges at the back, it is very likely, that border edges
        /// are the first edges in the list.
        /// Additionally we set the side filed of the side 0 references to
        /// indicate if a list contains (-1) or does not contain (0)
        /// non-manifold references.
        /// </summary>
        private static long AddEdge(
                ref BigFaceRef[] efra, ref long freeEdgeRef, long[] fera,
                long v0, long v1, bool strict, bool isExternalEdge)
        {
            long result = -1;
            long v1FirstEdgeRef = BigEdgeRef.Create(v1, 0);
            // check if the edge is in the edge list of vertex v1
            long edgeRef = v1FirstEdgeRef;
            long nonManifoldEdgeRef = -1;
            do
            {
                long otherVertexRef = efra[edgeRef].Index;
                if (TopologyVertexIndexOf(otherVertexRef) == v0) // found
                {
                    if (TopologyVertexSideOf(otherVertexRef) > 0) // in use
                    {
                        if (strict)
                        {
                            Report.End(3, " FAILED!");
                            throw new Exception("Edge already in use!");
                        }
                        else
                        {
                            nonManifoldEdgeRef = edgeRef;
                            break;
                        }
                    }
                    else // fill other side of edge
                    {
                        fera[v1] -= 1;
                        efra[edgeRef].Index = TopologyVertexRef(v0, 1);
                        result = edgeRef | 1;
                        break;
                    }
                }
                edgeRef = efra[edgeRef | 1].Index;
            }
            while (edgeRef >= 0);

            // check the edge list of vertex v0, if the edge is there

            long v0FirstEdgeRef = BigEdgeRef.Create(v0, 0);
            long v0LastEdgeRef = -1;

            if (nonManifoldEdgeRef < 0)
            {
                edgeRef = v0FirstEdgeRef;
                do
                {
                    long otherVertexRef = efra[edgeRef].Index;
                    if (TopologyVertexIndexOf(otherVertexRef) == v1)
                    {
                        if (strict)
                        {
                            Report.End(3, " FAILED!");
                            throw new Exception("Edge already in use!");
                        }
                        else
                        {
                            nonManifoldEdgeRef = edgeRef;
                            break;
                        }
                    }
                    v0LastEdgeRef = edgeRef;
                    edgeRef = efra[edgeRef | 1].Index;
                }
                while (edgeRef >= 0);
            }

            if (nonManifoldEdgeRef >= 0)
            {
                long nonManifoldEdgeCount = 1;
                long otherVertexRef = efra[nonManifoldEdgeRef].Index;
                if (TopologyVertexSideOf(otherVertexRef) == 1)
                {
                    // swap edge to the front, split it,  and add 3rd edge
                    fera[v0] += 1; fera[v1] += 1;
                    nonManifoldEdgeCount += 2;
                    if (TopologyVertexIndexOf(otherVertexRef) == v0)
                    {
                        if (nonManifoldEdgeRef != v1FirstEdgeRef)
                            efra[nonManifoldEdgeRef].Index = efra[v1FirstEdgeRef].Index;
                        efra[v1FirstEdgeRef].Index = TopologyVertexRef(v0, 2);
                        InsertEdge(ref efra, ref freeEdgeRef, v0FirstEdgeRef,
                                      TopologyVertexRef(v1, 2));
                    }
                    else
                    {
                        if (nonManifoldEdgeRef != v0FirstEdgeRef)
                            efra[nonManifoldEdgeRef].Index = efra[v0FirstEdgeRef].Index;
                        efra[v0FirstEdgeRef].Index = TopologyVertexRef(v1, 2);
                        InsertEdge(ref efra, ref freeEdgeRef, v1FirstEdgeRef,
                                      TopologyVertexRef(v0, 2));
                    }
                    efra[v1FirstEdgeRef].Side = 0;
                }
                else if (TopologyVertexSideOf(otherVertexRef) == 0)
                {
                    nonManifoldEdgeCount += 1;
                    efra[nonManifoldEdgeRef].Index = TopologyVertexRef(v1, 2);
                }

                fera[v0] += 1;
                InsertEdge(ref efra, ref freeEdgeRef, v0FirstEdgeRef,
                              TopologyVertexRef(v1, 2));
                efra[v0FirstEdgeRef].Side = 0;
                return nonManifoldEdgeCount;
            }

            if (result < 0) // we have to create a new (normal) edge
            {
                if (isExternalEdge)
                    InsertEdge(ref efra, ref freeEdgeRef, v0FirstEdgeRef,
                               TopologyVertexRef(v1, 0));
                else
                    AppendEdge(ref efra, ref freeEdgeRef, v0FirstEdgeRef,
                               v0LastEdgeRef, TopologyVertexRef(v1, 0));
                fera[v0] += 1;
            }
            return 0;
        }

        private static void SwapBorderEdgeToFront(BigFaceRef[] efra, long[] fera, long vi)
        {
            if (fera[vi] == 0) return;
            long firstEdgeRef = BigEdgeRef.Create(vi, 0);
            long otherVertexRef = efra[firstEdgeRef].Index;
            if (otherVertexRef < 0) return;
            if (TopologyVertexSideOf(otherVertexRef) != 1) return;
            long edgeRef = efra[firstEdgeRef | 1].Index;
            while (edgeRef >= 0)
            {
                otherVertexRef = efra[edgeRef].Index;
                if (TopologyVertexSideOf(otherVertexRef) != 1)
                {
                    Fun.Swap(ref efra[firstEdgeRef].Index, ref efra[edgeRef].Index);
                    return;
                }
                edgeRef = efra[edgeRef | 1].Index;
            }
        }

        /// <summary>
        /// Create rings of non-manifold edges. The references are stored
        /// in faceRef 0 of each edge, and need to be moved to the
        /// other faceRef before they can be used.
        /// </summary>
        private static void ConnectNonManifoldEdges(BigFaceRef[] efra, long v0, long v1)
        {
            long pEdgeRef = -1, pSide = -1;

            long v0FirstEdgeRef = BigEdgeRef.Create(v0, 0), v0count = 0;
            if (efra[v0FirstEdgeRef].Side == 0)
            {
                int hasNonManifolds = int.MinValue;
                long v0edgeRef = v0FirstEdgeRef;
                while (v0edgeRef >= 0)
                {
                    long v1VertexRef = efra[v0edgeRef].Index;
                    if (TopologyVertexSideOf(v1VertexRef) > 1)
                    {
                        if (TopologyVertexIndexOf(v1VertexRef) == v1)
                        {
                            pEdgeRef = v0edgeRef; pSide = 0; ++v0count;
                        }
                        hasNonManifolds = 0;
                    }
                    v0edgeRef = efra[v0edgeRef | 1].Index;
                }
                efra[v0FirstEdgeRef].Side = hasNonManifolds;
            }

            long v1FirstEdgeRef = BigEdgeRef.Create(v1, 0), v1count = 0;
            if (efra[v1FirstEdgeRef].Side == 0)
            {
                int hasNonManifolds = int.MinValue;
                long v1edgeRef = v1FirstEdgeRef;
                while (v1edgeRef >= 0)
                {
                    long v0VertexRef = efra[v1edgeRef].Index;
                    if (TopologyVertexSideOf(v0VertexRef) > 1)
                    {
                        if (TopologyVertexIndexOf(v0VertexRef) == v0)
                        {
                            pEdgeRef = v1edgeRef; pSide = 1; ++v1count;
                        }
                        hasNonManifolds = 0;
                    }
                    v1edgeRef = efra[v1edgeRef | 1].Index;
                }
                efra[v1FirstEdgeRef].Side = hasNonManifolds;
            }

            #if DEBUG
            long nonManifoldCount = v0count + v1count;
            if (nonManifoldCount == 1)
                Report.Warn("single non-manifold edge");
            #endif

            if (v0count > 0)
            {
                long v0edgeRef = v0FirstEdgeRef;
                long thisRef = -1;
                while (v0count > 0)
                {
                    long v1VertexRef = efra[v0edgeRef].Index;
                    long nextRef = v0edgeRef | 1;
                    if (TopologyVertexIndexOf(v1VertexRef) == v1
                        && TopologyVertexSideOf(v1VertexRef) > 1)
                    {
                        efra[v0edgeRef].Index = -pEdgeRef - (pSide == 0 ? 2 : 1);
                        pEdgeRef = v0edgeRef; pSide = 0; --v0count;
                        if (thisRef >= 0) efra[thisRef].Index = efra[nextRef].Index;
                    }
                    v0edgeRef = efra[nextRef].Index;
                    thisRef = nextRef;
                }
            }
            if (v1count > 0)
            {
                long v1edgeRef = v1FirstEdgeRef;
                long thisRef = -1;
                while (v1count > 0)
                {
                    long v0VertexRef = efra[v1edgeRef].Index;
                    long nextRef = v1edgeRef | 1;
                    if (TopologyVertexIndexOf(v0VertexRef) == v0
                        && TopologyVertexSideOf(v0VertexRef) > 1)
                    {
                        efra[v1edgeRef].Index = -pEdgeRef - (pSide == 1 ? 2 : 1);
                        pEdgeRef = v1edgeRef; pSide = 1; --v1count;
                        if (thisRef >= 0) efra[thisRef].Index = efra[nextRef].Index;
                    }
                    v1edgeRef = efra[nextRef].Index;
                    thisRef = nextRef;
                }
            }
        }

        private void BuildTopology(long eulerCharacteristic, bool strict)
        {
            Report.BeginTimed(12, "building topology for {0} {1}",
                                 m_faceCount,
                                 "face".Plural(m_faceCount));

            // use Eulers Formula to compute number of edges, but there
            // must be at least one edge per vertex, otherwise the mesh
            // cannot be topologically correct
            long edgeCount
                    = Fun.Max(m_vertexCount,
                              m_vertexCount + m_faceCount - eulerCharacteristic);
            long nonManifoldEdgeCount = 0;
            BigFaceRef[] edgeFaceRefArray
                    = new BigFaceRef[2 * edgeCount].Set(new BigFaceRef(long.MinValue, int.MinValue));

            // make this array large enough to hold one counter (for the
            // borderedges) per vertex, even if there are unused vertices
            long[] faceEdgeRefArray
                    = new long[Fun.Max(m_faceVertexCount,
                                      m_vertexCount)].Set(0);
            long freeEdgeRef = BigEdgeRef.Create(m_vertexCount, 0);

            long[] fvia = m_faceVertexIndexArray;

            // create list of edges for each vertex stored in edgeFaceRefArray
            for (long fi = 0; fi < m_faceCount; fi++)
            {
                long ffi = m_firstIndexArray[fi];
                long fvc = m_firstIndexArray[fi + 1] - ffi;
                for (long fs0 = fvc-1, fs1 = 0; fs1 < fvc; fs0 = fs1, fs1++)
                    nonManifoldEdgeCount +=
                        AddEdge(ref edgeFaceRefArray, ref freeEdgeRef,
                            faceEdgeRefArray,
                            fvia[ffi + fs0], fvia[ffi + fs1], strict, true);
            }

            long borderEdgeCount = 0;
            for (long vi = 0; vi < m_vertexCount; vi++)
                borderEdgeCount += faceEdgeRefArray[vi];

            if (borderEdgeCount > 0)
                for (long vi = 0; vi < m_vertexCount; vi++)
                    SwapBorderEdgeToFront(edgeFaceRefArray,
                                          faceEdgeRefArray, vi);

            // fill faceEdgeRefArray by searching edge in both vertex lists

            for (long fi = 0; fi < m_faceCount; fi++)
            {
                long ffi = m_firstIndexArray[fi];
                long fvc = m_firstIndexArray[fi + 1] - ffi;
                for (long fs0 = fvc - 1, fs1 = 0; fs1 < fvc; fs0 = fs1, fs1++)
                {
                    long v0 = fvia[ffi + fs0], v1 = fvia[ffi + fs1];
                    long edgeRef = FindEdgeInList(edgeFaceRefArray, v0, v1);
                    if (edgeRef < 0)
                        edgeRef = BigEdgeRef.Reversed(
                            FindEdgeInList(edgeFaceRefArray, v1, v0));
                    faceEdgeRefArray[ffi + fs0] = edgeRef;
                }
            }

            if (nonManifoldEdgeCount > 0)
            {
                for (long fi = 0; fi < m_faceCount; fi++)
                {
                    long ffi = m_firstIndexArray[fi];
                    long fvc = m_firstIndexArray[fi + 1] - ffi;
                    for (long fs0 = fvc-1, fs1 = 0; fs1 < fvc; fs0 = fs1, fs1++)
                        ConnectNonManifoldEdges(edgeFaceRefArray,
                                fvia[ffi + fs0], fvia[ffi + fs1]);
                }
                for (long edgeRef = 0; edgeRef < freeEdgeRef; edgeRef += 2)
                {
                    if (edgeFaceRefArray[edgeRef].Index < 0)
                        edgeFaceRefArray[edgeRef | 1].Index =
                            edgeFaceRefArray[edgeRef].Index;
                    else
                        edgeFaceRefArray[edgeRef | 1].Index = long.MinValue;
                    edgeFaceRefArray[edgeRef].Index = long.MinValue;
                    edgeFaceRefArray[edgeRef].Side = int.MinValue;
                }
            }
            else
                edgeFaceRefArray.Set(new BigFaceRef(long.MinValue, int.MinValue));

            // go through faces, store backpointers from edges to faces

            for (long fi = 0; fi < m_faceCount; fi++)
            {
                long ffi = m_firstIndexArray[fi];
                long fvc = m_firstIndexArray[fi + 1] - ffi;
                for (int fs = 0; fs < fvc; fs++)
                {
                    long edgeRef = faceEdgeRefArray[ffi + fs];
                    edgeFaceRefArray[edgeRef].Index = fi;
                    edgeFaceRefArray[edgeRef].Side = fs;
                }
            }

            EdgeCount = BigEdgeRef.Index(freeEdgeRef);
            BorderEdgeCount = borderEdgeCount;
            NonManifoldEdgeCount = nonManifoldEdgeCount;
            FaceEdgeRefArray = faceEdgeRefArray;
            EdgeFaceRefArray = edgeFaceRefArray;
            EulerCharacteristic = m_vertexCount + m_faceCount - EdgeCount;

            if (nonManifoldEdgeCount > 0)
                Report.End(12, ": non-manifold");
            else
                Report.End(12, ": euler characteristic {0}", EulerCharacteristic);
        }

        #endregion
    }
}
