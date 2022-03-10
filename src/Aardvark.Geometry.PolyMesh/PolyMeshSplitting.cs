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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry
{
    public class PolygonSplitter
    {
        /// <summary>
        /// New vertices on the split plane, belonging to both sides.
        /// </summary>
        private readonly List<SplitPoint> m_spl;

        private readonly List<Vertex> m_nvl;
        private readonly List<Vertex> m_pvl;

        private readonly List<Face> m_nfl;
        private readonly List<Face> m_pfl;

        private readonly int[] m_ofia;

        /// <summary>
        /// If this is null, the result does not contain a negative part.
        /// </summary>
        private readonly int[] m_nfia, m_nvia;

        /// <summary>
        /// If this is null, the result does not contain a positive part.
        /// </summary>
        private readonly int[] m_pfia, m_pvia;

        /// <summary>
        /// If this is null, when <see cref="m_nvia"/> is not null,
        /// the whole input is on the negative side.
        /// </summary>
        private readonly int[] m_nvbm, m_nfbm;

        /// <summary>
        /// If this is null, when <see cref="m_pvia"/> is not null,
        /// the whole input is on the positive side.
        /// </summary>
        private readonly int[] m_pvbm, m_pfbm;

        #region Help Structures

        /// <summary>
        /// Represents a value at an interpolated point between two indexed
        /// values of an indexable set of values. This is implemented as a
        /// class in order to avoid duplicating interpolated points in some
        /// algorithms.
        /// </summary>
        public struct SplitPoint
        {
            public readonly double T;
            public readonly int OldIndex0;
            public readonly int OldIndex1;

            #region Constructor

            public SplitPoint(double t, int oldIndex0, int oldIndex1)
            {
                T = t; OldIndex0 = oldIndex0; OldIndex1 = oldIndex1;
            }

            #endregion
        }

        public struct Vertex
        {
            public readonly int Index;
            public readonly int SplitIndex;
            public readonly double T;
            public readonly int OldSide;

            #region Constructor

            public Vertex(int index, int splitIndex, double t, int oldSide)
            {
                Index = index; SplitIndex = splitIndex; T = t; OldSide = oldSide;
            }

            #endregion
        }

        public struct FaceVertex
        {
            public readonly int VertexIndex;
            public readonly int OldSide;

            #region Constructor

            public FaceVertex(int vertexIndex, int faceSide)
            {
                VertexIndex = vertexIndex; OldSide = faceSide;
            }

            #endregion
        }

        public struct Face
        {
            public readonly int OldIndex;
            public readonly List<FaceVertex> Vertices;

            #region Constructor

            public Face(int oldIndex, List<FaceVertex> vertices)
            {
                OldIndex = oldIndex; Vertices = vertices;
            }

            #endregion
        }

        #endregion

        #region Constructor (Main Algorithm)

        /// <summary>
        /// A splitter can split a vertex-indexed face set, based on the
        /// supplied array of vertexHeights, which indicate for each vertex
        /// vi if it belongs to the negative split side (vertexHeights[vi]
        /// &lt; 0.0) or positive split side (vertexHeights[vi] &gt;= 0.0).
        /// </summary>
        public PolygonSplitter(
                int[] fia, int faceCount,
                int[] via, int maxFaceVertexCount,
                double[] vertexHeights, double eps,
                SplitterOptions options)
        {
            int vc = vertexHeights.Length;
            int vertexIndexCount = fia[faceCount];

            bool doNeg = (options & SplitterOptions.Negative) != 0;
            bool doPos = (options & SplitterOptions.Positive) != 0;

            if (doPos && vertexHeights.All(h => h >= -eps))
            {
                m_pfia = fia; m_pvia = via; m_ofia = null; return;
            }

            if (doNeg && vertexHeights.All(h => h <= eps))
            {
                m_nfia = fia; m_nvia = via; m_ofia = null; return;
            }

            m_ofia = fia;

            var lineMap = new Dict<Line1i, (int, int, int)>();

            m_spl = doNeg ? new List<SplitPoint>() : null;
            m_nvl = doPos ? new List<Vertex>() : null;
            m_pvl = doPos ? new List<Vertex>() : null;
            m_nfl = doNeg ? new List<Face>() : null;
            m_pfl = doPos ? new List<Face>() : null;

            int[] nffm = doNeg ? new int[faceCount].Set(-1) : null;
            int[] pffm = doPos ? new int[faceCount].Set(-1) : null;

            int[] nvfm = doNeg ? new int[vc].Set(-1) : null;
            int[] pvfm = doPos ? new int[vc].Set(-1) : null;

            var ha = new double[maxFaceVertexCount];
            int nfc = 0, pfc = 0, nvic = 0, pvic = 0, nvc = 0, pvc = 0;

            for (int fvi = fia[0], fi = 0; fi < faceCount; fi++)
            {
                int nc = 0, pc = 0, zc = 0, fve = fia[fi+1], fvc = fve - fvi;
                for (int fs = 0; fs < fvc; fs++)
                {
                    double height = ha[fs] = vertexHeights[via[fvi + fs]];
                    if (height < -eps) { ++nc; continue; }
                    if (height > +eps) { ++pc; continue; }
                    ++zc;
                }
                if (nc == 0)
                {
                    if (doPos)
                    {
                        pffm[fi] = pfc++; pvic += fvc;
                        for (int fs = 0; fs < fvc; fs++)
                            pvfm.ForwardMapAdd(via[fvi + fs], ref pvc);
                    }
                }
                else if (pc == 0)
                {
                    if (doNeg)
                    {
                        nffm[fi] = nfc++; nvic += fvc;
                        for (int fs = 0; fs < fvc; fs++)
                            nvfm.ForwardMapAdd(via[fvi + fs], ref nvc);
                    }
                }
                else
                {
                    if (zc > 2) Report.Warn("non-convex polygon encountered");
                    var nfvl = new List<FaceVertex>(nc + 2);
                    var pfvl = new List<FaceVertex>(pc + 2);

                    int sb = ha[0] > eps ? 1 : (ha[0] < -eps ? -1 : 0), vib = via[fvi];
                    int i0 = 0, s0 = sb, vi0 = vib;
                    if (doNeg && sb <= 0) { nfvl.Add(new FaceVertex(nvfm.ForwardMapAdd(vib, ref nvc), 0)); }
                    if (doPos && sb >= 0) { pfvl.Add(new FaceVertex(pvfm.ForwardMapAdd(vib, ref pvc), 0)); }
                    for (int i1 = 1; i1 < fvc; i0 = i1++)
                    {
                        int s1 = ha[i1] > eps ? 1 : (ha[i1] < -eps ? -1 : 0), vi1 = via[fvi + i1];
                        if (s0 != 0 && s1 != 0 && s0 != s1)
                        {
                            double t = ha[i0] / (ha[i0] - ha[i1]);
                            var key = Line1i.CreateSorted(vi0, vi1);
                            if (!lineMap.TryGetValue(key, out (int, int, int) v))
                            {
                                v = lineMap[key] = (nvc++, pvc++, m_spl.Count);
                                m_spl.Add(new SplitPoint(t, vi0, vi1));
                            }
                            if (doNeg)
                            {
                                m_nvl.Add(new Vertex(v.Item1, v.Item3, t, i0));
                                nfvl.Add(new FaceVertex(v.Item1, -m_nvl.Count));
                            }
                            if (doPos)
                            {
                                m_pvl.Add(new Vertex(v.Item2, v.Item3, t, i0));
                                pfvl.Add(new FaceVertex(v.Item2, -m_pvl.Count));
                            }
                        }
                        if (doNeg && s1 <= 0) { nfvl.Add(new FaceVertex(nvfm.ForwardMapAdd(vi1, ref nvc), i1)); }
                        if (doPos && s1 >= 0) { pfvl.Add(new FaceVertex(pvfm.ForwardMapAdd(vi1, ref pvc), i1)); }
                        vi0 = vi1; s0 = s1;
                    }
                    if (s0 != 0 && sb != 0 && s0 != sb)
                    {
                        double t = ha[i0] / (ha[i0] - ha[0]);
                        var key = Line1i.CreateSorted(vi0, vib);
                        if (!lineMap.TryGetValue(key, out (int, int, int) v))
                        {
                            v = lineMap[key] = (nvc++, pvc++, m_spl.Count);
                            m_spl.Add(new SplitPoint(t, vi0, vib));
                        }
                        if (doNeg)
                        {
                            m_nvl.Add(new Vertex(v.Item1, v.Item3, t, i0));
                            nfvl.Add(new FaceVertex(v.Item1, -m_nvl.Count));
                        }
                        if (doPos)
                        {
                            m_pvl.Add(new Vertex(v.Item2, v.Item3, t, i0));
                            pfvl.Add(new FaceVertex(v.Item2, -m_pvl.Count));
                        }
                    }
                    if (doNeg && nfvl.Count > 2) { m_nfl.Add(new Face(fi, nfvl)); nvic += nfvl.Count; }
                    if (doPos && pfvl.Count > 2) { m_pfl.Add(new Face(fi, pfvl)); pvic += pfvl.Count; }
                }
                fvi = fve;
            }

            if (doNeg && (nfc > 0 || m_nfl.Count > 0))
                CalculateIndices(fia, faceCount, via, nfc, nvic, nvc, nffm, m_nfl, nvfm,
                                 out m_nfia, out m_nvia, out m_nfbm, out m_nvbm);
            if (doPos && (pfc > 0 || m_pfl.Count > 0))
                CalculateIndices(fia, faceCount, via, pfc, pvic, pvc, pffm, m_pfl, pvfm,
                                 out m_pfia, out m_pvia, out m_pfbm, out m_pvbm);
        }

        /// <summary>
        /// Calculate new array of indices based on the results of the split
        /// algorithm.
        /// </summary>
        private void CalculateIndices(
                int[] ofia, int oldFaceCount, int[] ovia,
                int faceCount, int vertexIndexCount, int vertexCount,
                int[] faceForwardMap, List<Face> newFaceList, int[] vertexForwardMap,
                out int[] firstIndexArray, out int[] vertexIndexArray,
                out int[] faceBackMap, out int[] vertexBackMap)
        {
            var newFaceCount = faceCount + newFaceList.Count;
            var fia = new int[newFaceCount + 1];
            var via = new int[vertexIndexCount];

            int fvi = 0;
            for (int ofi = 0; ofi < oldFaceCount; ofi++)
            {
                int fi = faceForwardMap[ofi]; if (fi < 0) continue;
                fia[fi] = fvi;
                int ofvi = ofia[ofi], ofve = ofia[ofi+1];
                while (ofvi < ofve) via[fvi++] = vertexForwardMap[ovia[ofvi++]];
            }

            var fbm = faceForwardMap.CreateBackMap(faceCount);

            foreach (var f in newFaceList)
            {
                fia[faceCount++] = fvi;
                foreach (var v in f.Vertices) via[fvi++] = v.VertexIndex;
            }
            fia[faceCount] = fvi;

            firstIndexArray = fia;
            vertexIndexArray = via;
            faceBackMap = fbm;
            vertexBackMap = vertexForwardMap.CreateBackMap(vertexCount);
        }

        #endregion

        #region Result Query Functions

        public int[] FirstIndexArray(int side) { return side == 0 ? m_nfia : m_pfia; }
        public int[] VertexIndexArray(int side) { return side == 0 ? m_nvia : m_pvia; }
        public bool IsEqualToInput(int side) { return side == 0 ? (m_nvbm == null) : (m_pvbm == null); }

        public PolyMesh.IAttribute FaceAttribute(int side, PolyMesh.IAttribute attribute)
        {
            return side == 0
                    ? attribute.SplitFaceAttribute(m_nfbm, m_nfl)
                    : attribute.SplitFaceAttribute(m_pfbm, m_pfl);
        }

        public PolyMesh.IAttribute VertexAttribute(int side, PolyMesh.IAttribute attribute)
        {
            return side == 0
                    ? attribute.SplitVertexAttribute(m_nvbm, m_nvl, m_spl)
                    : attribute.SplitVertexAttribute(m_pvbm, m_pvl, m_spl);
        }

        public PolyMesh.IAttribute FaceVertexAttribute(int side, PolyMesh.IAttribute attribute)
        {
            return side == 0
                    ? attribute.SplitFaceVertexAttribute(
                            m_ofia, m_nfia, m_nvia, m_nfbm, m_nfl, m_nvl, m_spl)
                    : attribute.SplitFaceVertexAttribute(
                            m_ofia, m_pfia, m_pvia, m_pfbm, m_pfl, m_pvl, m_spl);
        }

        #endregion

    }

    public partial class PolyMesh
    {
        public partial interface IAttribute
        {
            IAttribute SplitFaceAttribute(
                    int[] faceBackMap, List<PolygonSplitter.Face> newFaceList);

            IAttribute SplitVertexAttribute(
                    int[] vertexBackMap,
                    List<PolygonSplitter.Vertex> newVertexList,
                    List<PolygonSplitter.SplitPoint> splitPointList);

            IAttribute SplitFaceVertexAttribute(
                    int[] ofia, int[] fia, int[] via, int[] faceBackMap,
                    List<PolygonSplitter.Face> newFaceList,
                    List<PolygonSplitter.Vertex> newVertexList,
                    List<PolygonSplitter.SplitPoint> splitPointList);

        }

        public partial struct Attribute<T>
        {
            public IAttribute SplitFaceAttribute(
                    int[] faceBackMap, List<PolygonSplitter.Face> newFaceList)
            {
                var faceBackCount = faceBackMap.Length;
                var newFaceCount = faceBackCount + newFaceList.Count;
                var ia = IndexArray;
                var va = TypedValueArray;
                if (ia == null)
                {
                    var nva = va.BackMappedCopy(faceBackMap, newFaceCount);
                    for (int i = 0; i < newFaceList.Count; i++)
                        nva[faceBackCount + i] = va[newFaceList[i].OldIndex];

                    return new PolyMesh.Attribute<T>(
                                Name, null, nva,
                                Interpolator);
                }
                else
                {
                    var forwardMap = new int[va.Length].Set(-1);
                    int nac = 0;
                    for (int fi = 0; fi < faceBackCount; fi++)
                        forwardMap.ForwardMapAdd(ia[faceBackMap[fi]], ref nac);

                    var nia = faceBackMap.Map(newFaceCount, i => forwardMap[ia[i]]);
                    for (int i = 0; i < newFaceCount; i++)
                        nia[faceBackCount + i] = forwardMap[newFaceList[i].OldIndex];

                    return new PolyMesh.Attribute<T>(
                            Name, nia,
                            va.ForwardMappedCopy(forwardMap, nac),
                            Interpolator);
                }
            }

            public IAttribute SplitVertexAttribute(
                    int[] vertexBackMap,
                    List<PolygonSplitter.Vertex> newVertexList,
                    List<PolygonSplitter.SplitPoint> splitPointList)
            {
                var splitVertexCount = splitPointList.Count;
                var name = Name;
                var indexArray = IndexArray;
                var valueArray = TypedValueArray;
                var interpolator = Interpolator;
                if (indexArray == null)
                {
                    var newValueArray = valueArray.BackMappedCopySafe(vertexBackMap, default);
                    foreach (var v in newVertexList)
                    {
                        var sp = splitPointList[v.SplitIndex];
                        newValueArray[v.Index]
                                = interpolator(sp.T, valueArray[sp.OldIndex0],
                                                     valueArray[sp.OldIndex1]);
                    }
                    return new PolyMesh.Attribute<T>(
                            name, null, newValueArray, interpolator);
                }
                else
                {
                    var forwardMap = new int[valueArray.Length].Set(-1);
                    int attributeCount = 0;
                    for (int nvi = 0, nvc = vertexBackMap.Length; nvi < nvc; nvi++)
                    {
                        var ovi = vertexBackMap[nvi];
                        if (ovi < 0) continue;
                        forwardMap.ForwardMapAdd(indexArray[ovi], ref attributeCount);
                    }

                    var splitAttributeBase = attributeCount;
                    attributeCount += splitVertexCount;

                    var newValueArray = valueArray.ForwardMappedCopy(forwardMap, attributeCount);

                    for (int si = 0; si < splitVertexCount; si++)
                    {
                        var sp = splitPointList[si];
                        newValueArray[splitAttributeBase + si]
                                = interpolator(sp.T, valueArray[indexArray[sp.OldIndex0]],
                                                     valueArray[indexArray[sp.OldIndex1]]);
                    }

                    var splitMap = new int[vertexBackMap.Length];

                    foreach (var v in newVertexList)
                        splitMap[v.Index] = splitAttributeBase + v.SplitIndex;

                    var newIndexArray = new int[vertexBackMap.Length].SetByIndex(
                        nvi =>
                        {
                            var vi = vertexBackMap[nvi];
                            return vi < 0 ? splitMap[nvi] : forwardMap[indexArray[vi]];
                        });

                    return new PolyMesh.Attribute<T>(
                            name, newIndexArray, newValueArray, interpolator);
                }
            }

            public IAttribute SplitFaceVertexAttribute(
                    int[] ofia, int[] fia, int[] via, int[] faceBackMap,
                    List<PolygonSplitter.Face> newFaceList,
                    List<PolygonSplitter.Vertex> newVertexList,
                    List<PolygonSplitter.SplitPoint> splitPointList)
            {
                var name = Name;
                var indexArray = IndexArray;
                var valueArray = TypedValueArray;
                var interpolator = Interpolator;
                var faceCount = fia.Length - 1;
                var faceVertexCount = fia[faceCount];
                if (indexArray == null)
                {
                    var newValueArray = new T[faceVertexCount];
                    valueArray.BackMappedGroupCopyTo(faceBackMap, faceCount, ofia, newValueArray, 0);

                    var newFaceVertexIndex = fia[faceBackMap.Length];
                    foreach (var f in newFaceList)
                    {
                        var ofvi = ofia[f.OldIndex];
                        foreach (var v in f.Vertices)
                        {
                            if (v.OldSide >= 0)
                                newValueArray[newFaceVertexIndex] = valueArray[ofvi + v.OldSide];
                            else
                            {
                                var vertex = newVertexList[-1 - v.OldSide];
                                var sp = splitPointList[vertex.SplitIndex];
                                int ofve = ofia[f.OldIndex + 1];
                                int ofvi0 = ofvi + vertex.OldSide, ofvi1 = ofvi0 + 1;
                                if (ofvi1 == ofve) ofvi1 = ofvi;
                                newValueArray[newFaceVertexIndex] =
                                        interpolator(vertex.T, valueArray[ofvi0], valueArray[ofvi1]);
                            }
                            ++newFaceVertexIndex;
                        }
                    }

                    return new PolyMesh.Attribute<T>(
                            name, null, newValueArray, interpolator);
                }
                else
                {
                    var forwardMap = new int[valueArray.Length].Set(-1);
                    int attributeCount = 0;
                    for (int fi = 0; fi < faceBackMap.Length; fi++)
                    {
                        var ofi = faceBackMap[fi];
                        for (int ofvi = ofia[ofi], ofve = ofia[ofi + 1]; ofvi < ofve; ofvi++)
                            forwardMap.ForwardMapAdd(indexArray[ofvi], ref attributeCount);
                    }

                    foreach (var f in newFaceList)
                    {
                        var ofvi = ofia[f.OldIndex];
                        foreach (var v in f.Vertices)
                            if (v.OldSide >= 0)
                                forwardMap.ForwardMapAdd(indexArray[ofvi + v.OldSide],
                                                         ref attributeCount);
                    }

                    var newIndexArray = new int[faceVertexCount];
                    indexArray.BackMappedGroupCopyTo(faceBackMap, faceBackMap.Length, ofia,
                                                     forwardMap, newIndexArray, 0);
                    var newValueArray = new T[attributeCount + newVertexList.Count];
                    valueArray.ForwardMappedCopyTo(newValueArray, forwardMap, 0);

                    var newFaceVertexIndex = fia[faceBackMap.Length];
                    foreach (var f in newFaceList)
                    {
                        foreach (var v in f.Vertices)
                        {
                            var ofvi = ofia[f.OldIndex];
                            if (v.OldSide >= 0)
                                newIndexArray[newFaceVertexIndex] = forwardMap[indexArray[ofvi + v.OldSide]];
                            else
                            {
                                var vertex = newVertexList[-1 - v.OldSide];
                                var sp = splitPointList[vertex.SplitIndex];
                                int ofve = ofia[f.OldIndex + 1];
                                int ofvi0 = ofvi + vertex.OldSide, ofvi1 = ofvi0 + 1;
                                if (ofvi1 == ofve) ofvi1 = ofvi;
                                newIndexArray[newFaceVertexIndex] = attributeCount;
                                newValueArray[attributeCount++] =
                                        interpolator(vertex.T, valueArray[indexArray[ofvi0]],
                                                               valueArray[indexArray[ofvi1]]);
                            }
                            ++newFaceVertexIndex;
                        }
                    }

                    return new PolyMesh.Attribute<T>(
                            name, newIndexArray, newValueArray, interpolator);
                }
            }

        }

        /// <summary>
        /// Splits the mesh on the specified plane in a pair containing the negative
        /// (element 0) and the positive side (element 1) of the plane.
        /// Note that the normal vector of the plane need not be normalized.
        /// </summary>
        public (PolyMesh, PolyMesh) SplitOnPlane(
                Plane3d plane, double epsilon, SplitterOptions options)
        {
            var heightArray = m_positionArray.Map(
                                m_vertexCount, p => plane.Height(p));
            var splitter = new PolygonSplitter(
                            m_firstIndexArray, m_faceCount,
                            m_vertexIndexArray, m_faceVertexCountRange.Max,
                            heightArray, epsilon, options);

            var result = (default(PolyMesh), default(PolyMesh));

            for (int side = 0; side < 2; side++)
            {
                var fia = splitter.FirstIndexArray(side);
                if (fia != null)
                {
                    if (splitter.IsEqualToInput(side))
                    {
                        //result[side] = this;
                        switch (side)
                        {
                            case 0:
                                result = (this, result.Item2);
                                break;
                            case 1:
                                result = (result.Item1, this);
                                break;
                            default:
                                throw new IndexOutOfRangeException();
                        }
                    }
                    else
                    {
                        var pm = new PolyMesh()
                        {
                            FirstIndexArray = fia,
                            VertexIndexArray = splitter.VertexIndexArray(side),
                            InstanceAttributes = InstanceAttributes,
                            FaceAttributes = FaceIAttributes.Select(
                                    a => splitter.FaceAttribute(side, a)).ToSymbolDict(),
                            VertexAttributes = VertexIAttributes.Select(
                                    a => splitter.VertexAttribute(side, a)).ToSymbolDict(),
                            FaceVertexAttributes = FaceVertexIAttributes.Select(
                                    a => splitter.FaceVertexAttribute(side, a)).ToSymbolDict(),
                        };

                        //result[side] = pm;
                        switch (side)
                        {
                            case 0:
                                result = (pm, result.Item2);
                                break;
                            case 1:
                                result = (result.Item1, pm);
                                break;
                            default:
                                throw new IndexOutOfRangeException();
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Splits the mesh on the specified plane in a pair containing the negative
        /// (element 0) and the positive side (element 1) of the plane.
        /// Note that the normal vector of the plane need not be normalized.
        /// </summary>
        public (PolyMesh, PolyMesh) SplitOnPlane(
                Plane3d plane, double epsilon)
        {
            return SplitOnPlane(plane, epsilon, SplitterOptions.NegativeAndPositive);
        }

        /// <summary>
        /// Clips the mesh with the given plane. 
        /// The part on the positive hals will remain.
        /// Will return null when everything is clipped.
        /// Cap-faces will be built on everything that is cut open by the plane (non-convex cap faces are not handled properly).
        /// Only works for meshes without Face-/FaceVertexAttributes -> attributes will be invalid for generated faces.
        /// </summary>
        public PolyMesh ClipByPlane(Plane3d plane, double epsilon = 1e-7)
        {
            var clippedMesh = SplitOnPlane(plane, epsilon).Item2;

            // in case everything is clipped away
            if (clippedMesh == null)
                return null;

            // 1. go trough all edges
            // 2. if edge on plane -> test if there is an open face along the plane (border edges)

            var vertexOnPlane = clippedMesh.PositionArray.Map(clippedMesh.VertexCount, p => plane.Height(p).Abs() <= epsilon);

            clippedMesh.BuildTopology();

            // use indices so an edge can be removed no matter what "ref"
            var edges = new IntSet(clippedMesh.EdgeCount);
            foreach (var e in clippedMesh.Edges)
                if (e.IsValid)
                    edges.Add(e.Index);

            var capFaceEdges = new List<PolyMesh.Edge>();
            var capFaceEdgeSet = new IntSet();

            while (edges.Count > 0)
            {
                var e = clippedMesh.GetEdge(edges.First());
                edges.Remove(e.Index);

                if (e.IsAnyBorder
                    && vertexOnPlane[e.FromVertexIndex]
                    && vertexOnPlane[e.ToVertexIndex])
                {
                    // try to find other edges along the plane
                    // keep set of all edges so in case there the loop runs into a degenerated case an exit is possible (will generate degenerated face)
                    capFaceEdges.Clear();
                    capFaceEdgeSet.Clear();

                    var currEdge = e;
                    do
                    {
                        if (currEdge.IsValid)
                        {
                            capFaceEdges.Add(currEdge);
                            capFaceEdgeSet.Add(currEdge.Index);
                        }
                        
                        // find next edge at start-vertex along plane 
                        // the new cap face should have winding order start<-to becaues it is on the opposite of the current face-edge
                        currEdge = currEdge.FromVertex.Edges
                            .Select(x => x.FromVertexIndex == currEdge.FromVertexIndex ? x.Opposite : x)
                            .FirstOrDefault(x => x.IsAnyBorder && x.Index != currEdge.Index && vertexOnPlane[x.FromVertexIndex], PolyMesh.Edge.Invalid);
                    } while (currEdge.IsValid 
                          && currEdge.Index != e.Index 
                          && !capFaceEdgeSet.Contains(currEdge.Index));

                    if (capFaceEdges.Count > 2)
                    {
                        // add cap-face
                        foreach (var fe in capFaceEdges.Skip(1))
                            edges.Remove(fe.Index);

                        clippedMesh.AddFace(capFaceEdges.Select(fe => fe.ToVertexIndex).ToArray());
                    }
                }
            }

            // clear topology (is invalid if face has been added)
            clippedMesh.ClearTopology();

            return clippedMesh;
        }
    }

    public static partial class PolyMeshExtensions
    {
        private static readonly Func<PolyMesh, Plane3d, double, SplitterOptions, (PolyMesh, PolyMesh)>
            s_polyMeshSplitOnPlane = (pm, pl, eps, opt) => pm.SplitOnPlane(pl, eps, opt);

        /// <summary>
        /// Splits the supplied PolyMeshes on the specified plane and
        /// returns the desired results (specified by the options parameter)
        /// in a pair containing lists of PolyMeshes for the negative
        /// (element 0) and the positive side (element 1) of the plane.
        /// Note that the normal vector of the plane need not be normalized.
        /// </summary>
        public static (List<PolyMesh>, List<PolyMesh>) SplitOnPlane(
                this IEnumerable<PolyMesh> polyMeshes,
                Plane3d plane, double epsilon, SplitterOptions options)
        {
            return polyMeshes.SplitOnPlane(plane, epsilon, options, s_polyMeshSplitOnPlane);
        }

        /// <summary>
        /// Splits the supplied PolyMeshes on the specified plane and
        /// returns the result in a pair containing lists of PolyMeshes
        /// for the negative (element 0) and the positive side (element 1) of
        /// the plane. Note that the normal vector of the plane need not be
        /// normalized.
        /// </summary>
        public static (List<PolyMesh>, List<PolyMesh>) SplitOnPlane(
                this IEnumerable<PolyMesh> polyMeshes,
                Plane3d plane, double epsilon)
        {
            return polyMeshes.SplitOnPlane(plane, epsilon,
                            SplitterOptions.NegativeAndPositive,
                            s_polyMeshSplitOnPlane);
        }

        public static List<PolyMesh>[] SplitOnParallelPlanes(
                this IEnumerable<PolyMesh> polyMeshes,
                V3d point, V3d normal, V3d delta, int count, double epsilon)
        {
            return polyMeshes.SplitOnParallelPlanes(point, normal, delta,
                                         count, epsilon, s_polyMeshSplitOnPlane);
        }

        /// <summary>
        /// Split the supplied PolyMeshes into a grid (stored as a
        /// volume of lists of PolyMeshes) of cells specified by
        /// the grid size and the size of the cells. Note, that the first
        /// split plane in each direction is offset by one cell size with
        /// respect to the origin.
        /// </summary>
        public static Volume<List<PolyMesh>> SplitOnGrid(
                this IEnumerable<PolyMesh> polyMeshes,
                V3d origin, V3d cellSize, V3i gridSize, double epsilon)
        {
            return polyMeshes.SplitOnGrid(origin, cellSize, gridSize, epsilon, s_polyMeshSplitOnPlane);
        }
    }
}
