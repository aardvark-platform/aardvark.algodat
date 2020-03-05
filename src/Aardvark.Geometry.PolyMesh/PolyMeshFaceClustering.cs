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
using Aardvark.Base.Coder;
using Aardvark.Base.Sorting;
using Aardvark.Geometry.Clustering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry
{
    [Flags]
    public enum FaceClusterOptions
    {
        ObjectClustering = 0x0001,
        SheetClustering = 0x0002,
        PlaneClustering = 0x0004,
        FaceSheets = 0x0100,
        MinBoxRotation = 0x0200,
        Default = ObjectClustering | SheetClustering | FaceSheets,
    }

    [RegisterTypeInfo(Version = 2)]
    public class FaceAggregation : IFieldCodeable, IAwakeable
    {
        public int FaceCount;
        public double Area;            
        public PlaneWithPoint3d Plane; // average normal and centroid
        public double MinimumRotation; // rotation from plane-space to minimum bounding box
        public M44d Global2Local;      
        public Box2d LocalBox;

        public FaceAggregation()
        {
            LocalBox = Box2d.Invalid;
        }

        public V3d Centroid { get { return Plane.Point; } set { Plane.Point = value; } }
        public V3d Normal { get { return Plane.Normal; } set { Plane.Normal = value; } }
        public V3d AreaNormal => Area * Plane.Normal;
        public V3d AreaCentroid => Area * Plane.Point;

        public V2d GetTextureCoordinate(V3d globalVertex)
        {
            V2d localVertex = M44d.TransformPos(Global2Local, globalVertex).XY;

            V2d normalizedPos = (localVertex - LocalBox.Min) / LocalBox.Size;

            if (normalizedPos.AnySmaller(-1e-8) || normalizedPos.AnyGreater(1 + 1e-8))
                Report.Warn("Problem with Texture Vertices");

            return normalizedPos;
        }

        public void Normalize()
        {
            if (Area == 0) return; // Normal and Centroid should be 0 if accumulated using area weight
            var scale = 1.0 / Area;
            Normal *= scale;
            Centroid *= scale;
        }

        public void InitFrame()
            => M44d.NormalFrame(Centroid, Normal, out M44d local2global, out Global2Local);

        /// <summary>
        /// Adds a hull vertex that is used to build the local box.
        /// </summary>
        public void AddVertex(V3d vertex) => LocalBox.ExtendBy(Global2Local.TransformPos(vertex).XY);

        #region IFieldCodeable Members

        virtual public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            yield return new FieldCoder(0, "FaceCount", (c, o) => c.CodeInt(ref ((FaceAggregation)o).FaceCount));
            yield return new FieldCoder(1, "Area", (c, o) => c.CodeDouble(ref ((FaceAggregation)o).Area));
            yield return new FieldCoder(2, "Plane", (c, o) => c.CodePlaneWithPoint3d(ref ((FaceAggregation)o).Plane));
            yield return new FieldCoder(3, "Global2Local", 0, 1, (c, o) => c.CodeM44d(ref ((FaceAggregation)o).Global2Local));
            yield return new FieldCoder(4, "LocalBox", (c, o) => c.CodeBox2d(ref ((FaceAggregation)o).LocalBox));
            yield return new FieldCoder(5, "NormalBox", 0, 1, (c, o) => { Box3d dummy = default(Box3d); c.CodeBox3d(ref dummy); });
            yield return new FieldCoder(6, "MinimumRotation", 2, int.MaxValue, (c, o) => c.CodeDouble(ref ((FaceAggregation)o).MinimumRotation));
        }

        #endregion

        #region IAwakeable Members

        public void Awake(int codedVersion)
        {
            if (codedVersion > 1)
            {
                InitFrame();
                Global2Local = M44d.RotationZ(MinimumRotation) * Global2Local;
            }
            else // compute minimum rotation
            {
                M44d.NormalFrame(Centroid, Normal, out M44d foo, out M44d referenceGlobal2Local);
                var refX = referenceGlobal2Local.R0.XYZ; 
                var minX = Global2Local.R0.XYZ;
                var refY = referenceGlobal2Local.R1.XYZ;
                MinimumRotation = -Fun.Acos(Fun.Clamp(refX.Dot(minX), -1, 1)) * Fun.Sign(minX.Dot(refY));
            }
        }

        #endregion
    }

    [RegisterTypeInfo]
    public class FacePlane : FaceAggregation
    {
        public Box3d GlobalBox;
        public V3d[] Points;
        public bool ContainsPartialSheets;

        public FacePlane(double area, V3d normal, V3d centroid, Box3d box)
        {
            Area = area;
            Plane = new PlaneWithPoint3d(normal, centroid);
            GlobalBox = box;
        }

        public FacePlane MergedPlane(FacePlane other,
                double distanceThreshold, double normalThreshold, double maxDistance)
        {
            if (!NearCoplanarPlanes(Plane, other.Plane, distanceThreshold, normalThreshold))
                return null;
            if (!GlobalBox.Intersects(other.GlobalBox, -maxDistance)) return null;
            double area = Area + other.Area;
            double scale = 1.0 / area;
            return new FacePlane(
                area,
                (AreaNormal + other.AreaNormal) * scale,
                (AreaCentroid + other.AreaCentroid) * scale,
                GlobalBox.ExtendedBy(other.GlobalBox));
        }

        private static bool NearCoplanarPlanes(PlaneWithPoint3d p0, PlaneWithPoint3d p1,
                                          double distanceThreshold, double normalThreshold)
        {
            if ((p0.Normal - p1.Normal).Length >= normalThreshold) return false;
            var dist = Fun.Min(Fun.Abs(p0.Height(p1.Point)), Fun.Abs(p1.Height(p0.Point)));
            return dist < distanceThreshold;
        }

    }

    [RegisterTypeInfo(Version=3)]
    public class FaceSheet : FaceAggregation
    {
        public int VertexCount; // number of unique vertices in the sheet (number of coordinates)
        public int[] FaceIndices; // now in combined array, but needed to restore data
        public int FirstFaceIndex;

        public FaceSheet()
            : base()
        { }

        public void AddFace(PolyMesh m, int fi,
                         double[] faa, V3d[] fna, V3d[] fca)
        {
            double area = faa[fi];
            var fn = fna[fi];
            ++FaceCount;
            Area += area;
            Normal += area * fn;
            Centroid += area * fca[fi];
        }

        #region IFieldCodeable Members

        override public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            foreach (var fc in base.GetFieldCoders(coderVersion))
                yield return fc;
            yield return new FieldCoder(6, "VertexCount", (c, o) => c.CodeInt(ref ((FaceSheet)o).VertexCount));
            yield return new FieldCoder(7, "FaceIndices", 0, 2, (c, o) => c.CodeIntArray(ref ((FaceSheet)o).FaceIndices));
            yield return new FieldCoder(8, "FirstFaceIndex", 3, int.MaxValue, (c, o) => c.CodeInt(ref ((FaceSheet)o).FirstFaceIndex));
        }

        #endregion
    }

    public class FaceAdjacencyClustering : Clustering.Clustering
    {
        public FaceAdjacencyClustering(PolyMesh m, Func<int, int, bool> sameFun)
        {
            if (!m.HasTopology)
                m.BuildTopology(0);
            int faceCount = m.FaceCount;
            Alloc(faceCount);
            var ca = m_indexArray;
            var sa = new int[faceCount].Set(1);
            for (int fi = 0; fi < faceCount; fi++)
            {
                var fvc = m.VertexCountOfFace(fi);
                int ci = ca[fi]; 
                if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[fi] = ci; }
                int si = sa[ci];
                for (int fs = 0; fs < fvc; fs++)
                {
                    var er = m.EdgeRef_Zero_OfFace(fi, fs);
                    var fr = m.FaceRef_One_OfEdgeRef(er);
                    if (!fr.IsValid) continue;
                    int fj = fr.Index;
                    if (fj < fi) continue;
                    int cj = ca[fj]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[fj] = cj; }
                    if (ci == cj || !sameFun(fi, fj)) continue;
                    int sj = sa[cj];
                    if (si < sj) { ca[ci] = cj; ca[fi] = cj; ci = cj; }
                    else { ca[cj] = ci; ca[fj] = ci; }
                    si += sj; sa[ci] = si;
                }
            }
            Init();
        }
    }

    public class FaceAdjacencyRawClustering : Clustering.Clustering
    {
        public FaceAdjacencyRawClustering(PolyMesh m, Func<int, int, bool> sameFun)
        {
            int faceCount = m.FaceCount;
            Alloc(faceCount);
            var ca = m_indexArray;
            var sa = new int[faceCount].Set(1);

            // var edgeTable = new DictSet<Line1i>();

            for (int fi = 0; fi < faceCount; fi++)
            {
                var fvc = m.VertexCountOfFace(fi);
                int ci = ca[fi]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[fi] = ci; }
                int si = sa[ci];
                for (int fs = 0; fs < fvc; fs++)
                {
                    var er = m.EdgeRef_Zero_OfFace(fi, fs);
                    var fr = m.FaceRef_One_OfEdgeRef(er);
                    if (!fr.IsValid) continue;
                    int fj = fr.Index;
                    if (fj < fi) continue;
                    int cj = ca[fj]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[fj] = cj; }
                    if (ci == cj || !sameFun(fi, fj)) continue;
                    int sj = sa[cj];
                    if (si < sj) { ca[ci] = cj; ca[fi] = cj; ci = cj; }
                    else { ca[cj] = ci; ca[fj] = ci; }
                    si += sj; sa[ci] = si;
                }
            }
            Init();
        }
    }

    public class FaceEqualClustering : Clustering.Clustering
    {
        public FaceEqualClustering(PolyMesh m, double epsilon = 1e-6)
        {
            int faceCount = m.FaceCount;
            Alloc(faceCount);
            var ca = m_indexArray;
            var sa = new int[faceCount].Set(1);
            var faceTable = new IntDict<int>(faceCount, stackDuplicateKeys:true);
            int[] fia = m.FirstIndexArray, via = m.VertexIndexArray;
            V3d[] pa = m.PositionArray;
            var pl = new List<V3d>();

            for (int fvi = fia[0], fi = 0; fi < faceCount; fi++)
            {
                pl.Clear();
                int fve = fia[fi + 1], fvc = fve - fvi;
                for (int i = fvi; i < fve; i++) pl.Add(pa[via[i++]]);
                pl.QuickSort(V3d.LexicalCompare);
                int hashCode = fvc;
                foreach (var p in pl) hashCode = HashCode.Combine(hashCode, p.HashCode1(epsilon));
                int ci = ca[fi]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[fi] = ci; }
                int si = sa[ci];
                foreach (int fj in faceTable.ValuesWithKey(hashCode))
                {
                    int cj = ca[fj]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[fj] = cj; }
                    if (ci == cj) continue;
                    int fvj = fia[fj];
                    if (fvc != fia[fj + 1] - fvj) continue; // different vertex count
                    int j = 0; var pi0 = pa[via[fvi]];
                    while (j < fvc && pi0 != pa[via[fvj + j]]) ++j;
                    if (j == fvc) continue; // not a single equal vertex
                    int i = 1;
                    while (i < fvc)
                    {
                        j = (j + 1) % fvc; if (pa[via[fvi + i]] != pa[via[fvj + j]]) break;
                        ++i;
                    }
                    if (i != fvc) continue; // at least one different vertex
                    int sj = sa[cj];
                    if (si < sj) { ca[ci] = cj; ca[fi] = cj; ci = cj; }
                    else { ca[cj] = ci; ca[fj] = ci; }
                    si += sj; sa[ci] = si;
                }
                faceTable[hashCode] = fi;
                fvi = fve;
            }
            Init();
        }
    }

    public class FacePlanarClustering : Clustering.Clustering
    {

        public FacePlanarClustering(PolyMesh m,
                    double normalRaster = 0.02, double distanceRaster = 0.01,
                    double normalThreshold = 0.01, double distanceThreshold = 0.005,
                    double maximalDistance = 0.05)
        {
            var normalCoder = new V3fCoder((uint)(1.0 / normalRaster));
            var distanceScale = 1.0 / distanceRaster;

            int faceCount = m.FaceCount;
            Alloc(faceCount);
            var ca = m_indexArray;
            var sa = new int[faceCount].Set(1);
            var normalArray = m.FaceAttributeArray<V3d>(PolyMesh.Property.Normals);
            var areaArray = m.FaceAttributeArray<double>(PolyMesh.Property.Areas);
            var centroidArray = m.FaceAttributeArray<V3d>(PolyMesh.Property.Centroids);

            var distanceCodeTable = new int[3];
            var normalCodeTable = new uint[9];
            var planeArray = new FacePlane[faceCount].SetByIndex(fi =>
                    new FacePlane(areaArray[fi], normalArray[fi], centroidArray[fi],
                                  m.BoundingBoxOfFace(fi)));

            var planeTable = new IntDict<int>(stackDuplicateKeys:true);
            for (int fi = 0; fi < faceCount; fi++)
            {
                V3d normal = normalArray[fi], centroid = centroidArray[fi];
                double n_dot_p = V3d.Dot(normal, centroid);

                uint normalCode = normalCoder.Encode((V3f)normal);
                int distanceCode = (int)(distanceScale * n_dot_p);
                distanceCodeTable[0] = distanceCode;
                distanceCodeTable[1] = distanceCode - 1;
                distanceCodeTable[2] = distanceCode + 1;
                var numNormalCodes = normalCoder.NeighbourCodes(
                                            normalCode, normalCodeTable);
                normalCodeTable[numNormalCodes++] = normalCode;
                int hi = distanceCode ^ (int)(normalCode << 16);
                
                int ci = ca[fi]; if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[fi] = ci; }
                int si = sa[ci];
                for (int dc = 0; dc < 3; dc++)
                    for (int nc = 0; nc < numNormalCodes; nc++)
                    {
                        int hj = distanceCodeTable[dc] ^ (int)(normalCodeTable[nc] << 16);
                        foreach (int fj in planeTable.ValuesWithKey(hj))
                        {
                            int cj = ca[fj]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[fj] = cj; }
                            if (ci == cj) continue;
                            FacePlane pi = planeArray[ci];
                            FacePlane pj = planeArray[cj];
                            var merged = pi.MergedPlane(pj, distanceThreshold, normalThreshold,
                                                        maximalDistance);
                            if (merged == null) continue;
                            int sj = sa[cj];
                            if (si < sj) { ca[ci] = cj; ca[fi] = cj; planeArray[ci] = null; ci = cj; }
                            else { ca[cj] = ci; ca[fj] = ci; planeArray[cj] = null;  }
                            si += sj; sa[ci] = si; planeArray[ci] = merged;
                        }
                        planeTable[hi] = fi;
                    }
            }
            Init();
        }
    }

    public class FaceCluster
    {
        public FaceAdjacencyClustering SheetClustering;
        public FaceAdjacencyClustering ObjectClustering;
        public FacePlanarClustering FaceClustering;

        public FaceCluster(FaceClusterOptions options, PolyMesh m, double normalDotThreshold,
                           Symbol coordsName, Symbol sheetName)
        {
            m.AddFaceNormalsAreasCentroids();
            var faceNormalArray = m.FaceAttributeArray<V3d>(PolyMesh.Property.Normals);
            
            if (!m.HasTopology) m.BuildTopology(0);

            // TODO: test with clustering if edge has shared normals
            //Func<int, int, bool> sameSmoothGroupFun = (i, j) =>
            //    faceNormalArray[i].Dot(faceNormalArray[j]) > normalDotThreshold;

            //SmoothCluster = new FaceAdjacencyClustering(m, sameSmoothGroupFun);
            
            // TODO: cut smooth cluster if adjacent faces do not meet the soothing criteria (clustered because of other path to face)

            var faceNormalCodeArray = faceNormalArray.Map(n => n.CubeFaceCode);
            //var sgia = SmoothCluster.IndexArray;
            //Func<int, int, bool> sameSheetFun = (i, j) =>
            //    sgia[i] == sgia[j] && faceNormalCodeArray[i] == faceNormalCodeArray[j];

            Func<int, int, bool> sameSheetFun = (i, j) =>
                   faceNormalArray[i].Dot(faceNormalArray[j]) > normalDotThreshold
                && faceNormalCodeArray[i] == faceNormalCodeArray[j];

            SheetClustering = new FaceAdjacencyClustering(m, sameSheetFun);

            ObjectClustering = new FaceAdjacencyClustering(m, (i, j) => true);

            if ((options & FaceClusterOptions.FaceSheets) != 0)
            {
                var faa = m.FaceAttributeArray<double>(PolyMesh.Property.Areas);
                var fca = m.FaceAttributeArray<V3d>(PolyMesh.Property.Centroids);

                var cia = SheetClustering.IndexArray;
                var cca = SheetClustering.CountArray;
                var sc = SheetClustering.Count;

                var faceCount = m.FaceCount;
                var sheetArray = new FaceSheet[sc].SetByIndex(i => new FaceSheet());
                var sheetFaces = new List<int>[sc].SetByIndex(i => new List<int>());

                for (int fi = 0; fi < faceCount; fi++)
                {
                    var sheetIndex = cia[fi];
                    sheetArray[sheetIndex].AddFace(m, fi, faa, faceNormalArray, fca);
                    sheetFaces[sheetIndex].Add(fi);
                }

                foreach (var sheet in sheetArray) sheet.Normalize();

                var visited = new bool[faceCount].Set(false);

                int reducedClusterCount = sc;
                
                if (reducedClusterCount < sc)
                {
                    var remappedIndex = new int[sc];
                    var reducedSheetArray = new FaceSheet[reducedClusterCount];
                    for (int si = 0, i = 0; si < sc; si++)
                    {
                        if (sheetArray[si] != null)
                        {
                            reducedSheetArray[i] = sheetArray[si];
                            remappedIndex[si] = i++;
                        }
                    }

                    for (int fi = 0; fi < faceCount; fi++)
                        cia[fi] = remappedIndex[cia[fi]];

                    sheetArray = reducedSheetArray;
                    //Report.Line("merged {0} to {1} clusters", sc, reducedClusterCount);
                    sc = reducedClusterCount;
                }

                // allow a maximum normal deviation of about 54.74 degrees, this is what
                // the cube face normals automatically have as a maximum
                double cosLimit = Fun.Cos(Fun.Atan(Fun.Sqrt(2.0))) - Constant<double>.PositiveTinyValue;

                // correct normals if face normal is outside range
                for (int fi = 0; fi < faceCount; fi++)
                {
                    var sheet = sheetArray[cia[fi]];
                    if (sheet != null && V3d.Dot(sheet.Normal, faceNormalArray[fi]) < cosLimit)
                        sheet.Normal = V3d.FromCubeFaceCode(faceNormalCodeArray[fi]);
                }

                foreach (var sheet in sheetArray) if (sheet != null) sheet.InitFrame();

                var pa = m.PositionArray;
                var via = m.VertexIndexArray;
                var fia = m.FirstIndexArray;


                if ((options & FaceClusterOptions.MinBoxRotation) != 0)
                {
                    var sheetVertexCountArray = new int[sc];

                    // count the vertices in each sheet
                    for (int fvi = fia[0], fi = 0; fi < faceCount; fi++)
                    {
                        int fve = fia[fi + 1];
                        sheetVertexCountArray[cia[fi]] += fve - fvi;
                        fvi = fve;
                    }

                    var sheetVertexIndexArrays = new int[sc][];
                    sheetVertexIndexArrays.SetByIndex(i => new int[sheetVertexCountArray[i]]);
                    sheetVertexCountArray.Set(0);
                    // add all vertices to sheets
                    for (int fvi = fia[0], fi = 0; fi < faceCount; fi++)
                    {
                        var si = cia[fi];
                        var svia = sheetVertexIndexArrays[si];
                        var svc = sheetVertexCountArray[si];
                        for (int fve = fia[fi + 1]; fvi < fve; fvi++)
                            svia[svc++] = via[fvi];
                        sheetVertexCountArray[si] = svc;
                    }

                    for (int si = 0; si < sc; si++)
                    {
                        var svia = sheetVertexIndexArrays[si];
                        var svc = sheetVertexCountArray[si];
                        var sheet = sheetArray[si];
                        var spa = new V2d[svc].SetByIndex(
                                        i => sheet.Global2Local.TransformPos(pa[svia[i]]).XY);
                        var convexHull = spa.ConvexHullIndexPolygon();
                        var minRot = convexHull.ToPolygon2d().ComputeMinAreaEnclosingBoxRotation();
                        sheet.MinimumRotation = -Fun.Acos(minRot[0]);
                        sheet.Global2Local = (M44d)minRot * sheet.Global2Local;
                        convexHull.ForEachIndex(i => sheet.AddVertex(pa[svia[i]]));
                    }
                }
                else
                {
                    // add all vertices to local 2d boxes
                    for (int fvi = fia[0], fi = 0; fi < faceCount; fi++)
                    {
                        var sheet = sheetArray[cia[fi]];
                        for (int fve = fia[fi + 1]; fvi < fve; fvi++)
                            sheet.AddVertex(pa[via[fvi]]);
                    }
                }

                // compute texture coordinates
                var vic = m.VertexIndexCount;
                var coordsArray = new V2d[vic];
                for (int fvi = fia[0], fi = 0; fi < faceCount; fi++)
                {
                    var sheet = sheetArray[cia[fi]];
                    for (int fve = fia[fi + 1]; fvi < fve; fvi++)
                        coordsArray[fvi] = sheet.GetTextureCoordinate(pa[via[fvi]]);
                }

                // store coordinates and sheets into mesh
                m.FaceVertexAttributes[coordsName] = coordsArray;
                m.FaceAttributes[sheetName] = cia;
                m.FaceAttributes[-sheetName] = sheetArray;
            }

            if ((options & FaceClusterOptions.PlaneClustering) != 0)
            {
                FaceClustering = new FacePlanarClustering(m);
            }
        }
    }

    public class FaceEdgeClustering : Clustering.Clustering
    {
        public List<int> SplitEdges;
        public IntSet[] SheetBorderEdges;

        public FaceEdgeClustering(PolyMesh m, IntSet borderEdges)
        {
            if (!m.HasTopology)
                m.BuildTopology(0);
            SplitEdges = new List<int>();
            int faceCount = m.FaceCount;
            Alloc(faceCount);
            var ca = m_indexArray;
            //var sa = new int[faceCount].Set(1);
            var clusterBorderEdges = new IntSet[faceCount].SetByIndex(i =>
                {
                    var be = m.GetFace(i).Edges.Select(e => e.Index).Where(ei => borderEdges.Contains(ei));
                    return be.IsEmpty() ? null : new IntSet(be);
                });
            for (int fi = 0; fi < faceCount; fi++)
            {
                var fvc = m.VertexCountOfFace(fi);
                int ci = ca[fi];
                if (ca[ci] != ci) { do { ci = ca[ci]; } while (ca[ci] != ci); ca[fi] = ci; clusterBorderEdges[fi] = clusterBorderEdges[ci]; }
                //int si = sa[ci];
                var eA = clusterBorderEdges[ci];
                for (int fs = 0; fs < fvc; fs++)
                {
                    var er = m.EdgeRef_Zero_OfFace(fi, fs);
                    var ei = EdgeRef.Index(er);
                    if (borderEdges.Contains(ei)) continue;
                    var fr = m.FaceRef_One_OfEdgeRef(er);
                    if (!fr.IsValid) continue;
                    int fj = fr.Index;
                    if (fj < fi) continue;
                    int cj = ca[fj]; if (ca[cj] != cj) { do { cj = ca[cj]; } while (ca[cj] != cj); ca[fj] = cj; clusterBorderEdges[fj] = clusterBorderEdges[cj]; }
                    if (ci == cj) continue;
                    var eB = clusterBorderEdges[cj];
                    if (eA != null && eB != null && (eB.Count < eA.Count ? eB.Any(i => eA.Contains(i)) : eA.Any(i => eB.Contains(i)))) { SplitEdges.Add(ei); continue; }
                    if (eA == null) { clusterBorderEdges[ci] = eB; eA = eB; }
                    else if (eB != null) eA.AddRange(eB);
                    //int sj = sa[cj];
                    clusterBorderEdges[cj] = eA; clusterBorderEdges[fj] = eA;
                    //if (si < sj) { ca[ci] = cj; ca[fi] = cj; ci = cj; } else 
                    { ca[cj] = ci; ca[fj] = ci;  }
                    //si += sj; sa[ci] = si;
                }
            }
            m_indexArray.ClusterConsolidate();
            var sbe = m_indexArray.Distinct().Select(i => (i, clusterBorderEdges[i])).ToArray();
            m_countArray = m_indexArray.CompactAndComputeCountArray();
            m_count = m_countArray.Length;

            SheetBorderEdges = new IntSet[m_count];
            sbe.ForEach(x => SheetBorderEdges[m_indexArray[x.Item1]] = x.Item2);
        }
    }

    public class ClusterUsingList
    {
        public List<int> BorderEdges;
        public List<int> Faces = new List<int>();
    }

    public class FaceClusterNew
    {
        HashSet<ClusterUsingList> m_clusters;
        ClusterUsingList[] m_clusterRefs;

        public IntSet ClusterEdges { get; private set; }
        public IntSet SheetEdges { get; private set; }

        public FaceClusterNew(PolyMesh m, IntDict<int> nonManifoldConnections, IntSet borderEdges, Symbol sheetAttributeName, Symbol sheetCoordsName, double maxClusterAngle = 40)
        {
            if (!m.FaceAttributes.Contains(PolyMesh.Property.Normals) ||
                !m.FaceAttributes.Contains(PolyMesh.Property.Areas) ||
                !m.FaceAttributes.Contains(PolyMesh.Property.Centroids))
            {
                m.AddFaceNormalsAreasCentroids();
            }

            const int verbosity = 3;

            var faceCount = m.FaceCount;

            Report.BeginTimed(verbosity, "initial clustering");
            InitialClustering(m, nonManifoldConnections, borderEdges);
            Report.End(verbosity);

            Report.Value(verbosity, "ClusterCount", m_clusters.Count);

            //Report.BeginTimed(verbosity, "optimize cluster borders");
            //OptimizeEdges(m, borderEdges, this.ClusterEdges);
            //Report.End(verbosity);

            // build cluster indices and add them to the mesh
            AddClusterIndices(m);

            //// do pathfinding optimization
            //Report.BeginTimed("optimize cluster outline");
            //foreach(var c in Clusters)
            //    OptimizeClusterOutline(m, c, borderEdges);
            //Report.End();

            Report.BeginTimed(verbosity, "optimize cluster borders");
            SheetClustering(m, borderEdges, nonManifoldConnections, maxClusterAngle);
            Report.End(verbosity);

            //Report.BeginTimed("optimize sheet borders");
            //OptimizeEdges(m, borderEdges, this.SheetEdges);
            //Report.End();

            Report.BeginTimed(verbosity, "create sheets");
            AddFaceSheets(m, sheetAttributeName, sheetCoordsName);
            Report.End(verbosity);
        }

        /// <summary>
        /// splits the initial clusters (smoothing groups) in sheets with similar normals.
        /// </summary>
        void SheetClustering(PolyMesh m, IntSet borderEdges, IntDict<int> nonManifoldConnections, double maxClusterAngle)
        {
            m.AddFaceNormalsAreasCentroids();
            var fna = m.FaceAttributeArray<V3d>(PolyMesh.Property.Normals);
            var faa = m.FaceAttributeArray<double>(PolyMesh.Property.Areas);
            var fca = m.FaceAttributeArray<V3d>(PolyMesh.Property.Centroids);

            //Report.BeginTimed("grow sheets");

            var clusters = m_clusters.ToArray();

            var sheetClusters = new HashSet<ClusterUsingList>();
            var sheetClusterRefs = new ClusterUsingList[m.FaceCount];

            double maxAngle = Fun.Cos(maxClusterAngle * Constant.RadiansPerDegree);

            //var sheetEdges = new IntSet(this.ClusterEdges);

            //var sheetList = new List<FaceSheet>();
            // grow areas with normal tolerance for each cluster
            foreach (var fc in m_clusters)
            {
                var clusterEdges = new IntSet();

                var workSet = new IntSet(fc.Faces.Count);
                workSet.AddRange(fc.Faces);

                do
                {
                    var ci = new ClusterUsingList
                    {
                        BorderEdges = new List<int>()
                    };

                    // take some random face and start growing a cluster
                    var clusterWorkSet = new IntSet(workSet.Count);
                    var first = workSet.First();
                    clusterWorkSet.Add(first);

                    var box = Box3d.Invalid;
                    //double coneAngleCos = 1;
                    var coneNormal = V3d.Zero; // what if this is nan
                    var coneValid = false;

                    do
                    {
                        var newClusterWorkSet = new IntSet();

                        foreach(var fi in clusterWorkSet)
                        {
                            var fn = fna[fi];
                            var normalValid = fn != V3d.Zero && !fn.AnyNaN; // merge face to cluster if degenerated
                            var angleCos = normalValid && coneValid ? fn.Dot(coneNormal) : 1;
                            //var angle = coneAngleCos;
                            //var d = fn == V3d.Zero || fn.AnyNaN ? 1 : fn.Dot(coneNormal);
                            if (angleCos >= maxAngle)
                            {
                                coneNormal = normalValid ? coneValid ? (coneNormal * ci.Faces.Count + fn).Normalized : fn : coneNormal; // new weighed-average normal
                                if (normalValid) coneValid = true; // cone is valid if one valid face has been found

                                workSet.Remove(fi);
                                newClusterWorkSet.TryRemove(fi);
                                sheetClusterRefs[fi] = ci;
                                ci.Faces.Add(fi);

                                // add potential neighbours to cluster work set
                                //foreach (var e in m.GetFace(fi).Edges)
                                //{
                                //    var ofi = e.OppositeFaceIndex;
                                //    if (ofi >= 0 && workSet.Contains(ofi))
                                //        newClusterWorkSet.Add(ofi);
                                //    var ei = e.Index;
                                //    if (borderEdges.Contains(ei))
                                //        ci.BorderEdges.Add(ei);
                                //}

                                var fvc = m.VertexCountOfFace(fi);
                                for (int fs = 0; fs < fvc; fs++)
                                {
                                    var er = m.EdgeRef_Zero_OfFace(fi, fs);
                                    var ei = EdgeRef.Index(er);
                                    if (borderEdges.Contains(ei))
                                    {
                                        ci.BorderEdges.Add(ei);
                                        continue;
                                    }

                                    var ofi = m.FaceRef_One_OfEdgeRef(er).Index;
                                    if (ofi < 0)
                                    {
                                        int e1;
                                        if (!nonManifoldConnections.TryGetValue(ei, out e1))
                                            continue; // should not happen
                                        ofi = m.FaceRef_Zero_OfEdgeRef(EdgeRef.Create(e1, 0)).Index;
                                    }
                                    if (workSet.Contains(ofi))
                                        newClusterWorkSet.Add(ofi);
                                }
                                   
                                //newClusterWorkSet.AddRange(face.Edges.Select(e => e.OppositeFaceIndex).Where(ofi => ofi >= 0 && workSet.Contains(ofi)));
                                //ci.BorderEdges.AddRange(face.Edges.Select(e => e.Index).Where(ei => borderEdges.Contains(ei)));
                                //sheetBorderEdges.AddRange(face.Edges.Select(e => e.Index).Where(ei => borderEdges.Contains(ei)));
                            }
                        }

                        clusterWorkSet = newClusterWorkSet;
                    }
                    while (clusterWorkSet.Count > 0);
                    
                    sheetClusters.Add(ci);
                }
                while (workSet.Count > 0);

                

                //sheetList.AddRange(sheetsOfCluster);

                //sheetEdges.AddRange(clusterEdges).Where(ei =>
                //{
                //    var e = m.GetEdge(ei);
                //     return sheetClusterRefs[e.Face.Index] != sheetClusterRefs[e.OppositeFace.Index];
                //}));
            }

            m_clusters = sheetClusters;
            m_clusterRefs = sheetClusterRefs;

            var sheetEdges = new IntSet();
            foreach (var e in m.Edges.Where(e => e.IsValid)) // filter invalid, in case a vertex is not referenced by any face there are invalid edges in the edge-set
            {
                var fi0 = e.FaceIndex;
                var fi1 = e.OppositeFaceIndex;
                //if (fi0 < 0 || fi1 < 0 || sheetClusterRefs[fi0] != sheetClusterRefs[fi1])
                //    sheetEdges.Add(e.Index);
                if (fi1 < 0)
                {
                    if (!nonManifoldConnections.TryGetValue(e.Index, out int e1))
                        sheetEdges.Add(e.Index);
                    fi1 = m.FaceRef_Zero_OfEdgeRef(EdgeRef.Create(e1, 0)).Index;
                }
                if (fi1 < 0 || sheetClusterRefs[fi0] != sheetClusterRefs[fi1])
                    sheetEdges.Add(e.Index);
            }

            this.SheetEdges = sheetEdges;
                //new IntSet(m.Edges.Where(e => e.IsAnyBorder
                //                                    || sheetClusterRefs[e.FaceIndex] != sheetClusterRefs[e.OppositeFaceIndex])
                //                                .Select(e => e.Index));

            //Report.End();
        }

        /// <summary>
        /// Clusters all faces into regions (smoothing groups) where faces dont share border edges.
        /// </summary>
        void InitialClustering(PolyMesh m, IntDict<int> nonManifoldConnections, IntSet borderEdges)
        {
            int faceCount = m.FaceCount;

            m_clusters = new HashSet<ClusterUsingList>();
            //ClusterRefs = new ClusterUsingList[faceCount];
            m_clusterRefs = new ClusterUsingList[faceCount].SetByIndex(i =>
                {
                    // initialize starting clusters for faces where any edge has an open connections
                    var be = m.GetFace(i).Edges.Select(e => e.Index).Where(ei => borderEdges.Contains(ei));
                    if (!be.IsEmpty())
                    {
                        var c = new ClusterUsingList();
                        c.BorderEdges = /*be.IsEmpty() ? null :*/ new List<int>(be);
                        c.Faces.Add(i);
                        m_clusters.Add(c);
                        return c;
                    }
                    return null;
                });

            var clusterEdges = new IntSet(borderEdges.Count);
            clusterEdges.AddRange(borderEdges);

            for (int fi = 0; fi < faceCount; fi++)
            {
                var fvc = m.VertexCountOfFace(fi);
                var ci = m_clusterRefs[fi];
                for (int fs = 0; fs < fvc; fs++)
                {
                    var er = m.EdgeRef_Zero_OfFace(fi, fs);
                    var ei = EdgeRef.Index(er);
                    if (borderEdges.Contains(ei)) continue;
                    var fr = m.FaceRef_One_OfEdgeRef(er);
                    if (!fr.IsValid)
                    {
                        // check non-manifold connections
                        if (!nonManifoldConnections.TryGetValue(ei, out int e1))
                            continue;
                        fr = m.FaceRef_Zero_OfEdgeRef(EdgeRef.Create(e1, 0));
                    }
                    int fj = fr.Index;
                    if (fj < fi) continue;
                    var cj = m_clusterRefs[fj];
                    if (ci == cj && ci != null) continue;
                    var eA = ci == null ? null : ci.BorderEdges;
                    var eB = cj == null ? null : cj.BorderEdges;
                    // check if merge not allowed

                    if (eA != null && eB != null && (eB.Count < eA.Count ? eB.Any(i => eA.Contains(i)) : eA.Any(i => eB.Contains(i)))) { clusterEdges.Add(ei); continue; }
                    
                    // merge clusters
                    if (cj != null)
                    {
                        if (ci == null)
                        {
                            cj.Faces.Add(fi);
                            m_clusterRefs[fi] = cj;
                            ci = cj;
                        }
                        else
                        {
                            if (ci.Faces.Count > cj.Faces.Count)
                            {
                                foreach (var x in cj.Faces)
                                {
                                    m_clusterRefs[x] = ci;
                                    ci.Faces.Add(x);
                                }
                                m_clusters.Remove(cj);
                            }
                            else
                            {
                                foreach (var x in ci.Faces)
                                {
                                    m_clusterRefs[x] = cj;
                                    cj.Faces.Add(x);
                                }
                                m_clusters.Remove(ci);
                                ci = cj;
                            }
                        }
                    }
                    else // cj == null
                    {
                        if (ci == null)
                        {
                            var c = new ClusterUsingList();
                            c.Faces.Add(fi);
                            c.Faces.Add(fj);
                            m_clusters.Add(c);
                            m_clusterRefs[fi] = c;
                            m_clusterRefs[fj] = c;
                            ci = c;
                        }
                        else
                        {
                            ci.Faces.Add(fj);
                            m_clusterRefs[fj] = ci;
                        }
                    }

                    if (eA == null) eA = eB; 
                    else if (eB != null) eA.AddRange(eB);

                    ci.BorderEdges = eA;
                }
                if (ci == null)
                {
                    var c = new ClusterUsingList();
                    c.Faces.Add(fi);
                    m_clusters.Add(c);
                    m_clusterRefs[fi] = c;
                }
            }

            this.ClusterEdges = clusterEdges;
        }

        //void OptimizeClusterOutline(PolyMesh m, ClusterUsingList c, IntSet meshBorderEdges)
        //{
        //    bool onBorder = false;
        //    PolyMesh.Vertex lastJunction = new PolyMesh.Vertex(m, -1); // either a crossing of three or more ClusterEdges or a connection to border edge

        //    // find some start point (edge)
        //    PolyMesh.Edge currentEdge;
        //    if (c.BorderEdges != null)
        //    {
        //        currentEdge = m.GetEdge(c.BorderEdges.First());
        //        onBorder = true;
        //    }
        //    else
        //        currentEdge = c.Faces.SelectMany(fi => m.GetFace(fi).Edges.Where(x => m_clusterEdges.Contains(x.Index) || meshBorderEdges.Contains(x.Index))).First();// x.OppositeIsBorder || ClusterRefs[x.OppositeFace.Index] != c)).First();

        //    // make sure to be on inside of cluster
        //    //if (ClusterRefs[currentEdge.Face.Index] != c)
        //    //{
        //    //    currentEdge = currentEdge.Opposite;
        //    //    if (ClusterRefs[currentEdge.Face.Index] != c) throw new InvalidOperationException();
        //    //}

        //    PolyMesh.Edge startEdge = currentEdge;
        //    PolyMesh.Vertex firstJunction = new PolyMesh.Vertex(m, -1);

        //    PolyMesh.Vertex currentVertex = currentEdge.FromVertex;
        //    PolyMesh.Face currentFace = m_clusterRefs[currentEdge.Face.Index] == c ? currentEdge.Face : currentEdge.OppositeFace;

        //    int outlineLength = 0;

        //    do
        //    {
        //        if (firstJunction.Index < 0 && lastJunction.Index >= 0)
        //            firstJunction = lastJunction;

        //        // only border and cluster edges (except the edge we come from)
        //        var nextVertex = currentEdge.ToVertex;// == currentVertex.Index ? currentEdge.FromVertex : currentEdge.ToVertex;
        //        var possibleEdges = nextVertex.Edges.Where(x => x.Index != currentEdge.Index && (m_clusterEdges.Contains(x.Index) || meshBorderEdges.Contains(x.Index)))
        //            .Select(e => e.FromVertexIndex == nextVertex.Index ? e : e.Opposite).ToArray();

        //        int nextIndex = 0;

        //        if (possibleEdges.Length == 1) // only one path to go (must be valid meaning one of its faces belong to the cluster)
        //        {
        //            //if (!onBorder && meshBorderEdges.Contains(currentEdge.Index))
        //            //{
        //            //    onBorder = true;
        //            //    // do path optimization [lastJunction <-> nextVertex]

        //            //    lastJunction = nextVertex;
        //            //}

        //            nextIndex = 0;

        //            //currentEdge = possiblePaths[0];
        //        }
        //        else
        //        {
        //            // junction here
        //            nextIndex = possibleEdges.FirstIndexOf(x => m_clusterRefs[x.Face.Index] == c);
        //        }

        //        if (nextIndex < 0)
        //        {
        //            Report.Line("dead end2");
        //            //debugList.Add(new Line3d(currentVertex.Position, -V3d.One));
        //            break;
        //        }

        //        var nextOnBorder = meshBorderEdges.Contains(possibleEdges[nextIndex].Index);
        //        if (possibleEdges.Length > 1 && onBorder != nextOnBorder // junction point but on mesh border edge
        //            || onBorder ^ !nextOnBorder) //if (!onBorder && nextOnBorder || onBorder && !nextOnBorder)
        //        {
        //            // do pathfinding
        //            if (lastJunction.Index >= 0)
        //            {
        //                OptimizeEdgePath(lastJunction, nextVertex);
        //            }

        //            lastJunction = nextVertex;
        //        }

        //        currentEdge = possibleEdges[nextIndex];
        //        currentVertex = nextVertex;

        //        outlineLength++;

        //        if (outlineLength > 10000)
        //        {
        //            Report.Line("endless loop");
        //            //debugList.Add(new Line3d(currentVertex.Position, V3d.One));
        //            break;
        //        }
        //    }
        //    while (!(firstJunction.Index < 0 && currentEdge == startEdge || firstJunction.Index >= 0 && firstJunction == currentVertex));

        //    //Report.Value("OutlineLength", outlineLength);
        //}

        void AddClusterIndices(PolyMesh m)
        {
            var clusterIndexArray = new uint[m.FaceCount].Set(UInt32.MaxValue);

            uint i = 0;
            foreach(var cluster in m_clusters.ToArray())
            {
                foreach (var faceIndex in cluster.Faces)
                    clusterIndexArray[faceIndex] = i;
                i++;
            }
            
            m.FaceAttributes[PolyMesh.Property.FaceClusterIndices] = clusterIndexArray;
            m[PolyMesh.Property.FaceClusterCount] = i;
        }

        /// <summary>
        /// Adds a FaceSheet[] and indices per face and coordinates of the normalized local box to the mesh.
        /// </summary>
        void AddFaceSheets(PolyMesh m, Symbol sheetName, Symbol coordsName)
        {
            var fc = m.FaceCount;

            var fna = m.FaceAttributeArray<V3d>(PolyMesh.Property.Normals);
            var faa = m.FaceAttributeArray<double>(PolyMesh.Property.Areas);
            var fca = m.FaceAttributeArray<V3d>(PolyMesh.Property.Centroids);

            var fia = m.FirstIndexArray;
            var via = m.VertexIndexArray;
            var pa = m.PositionArray;

            var clusters = m_clusters.ToArray();

            // build face vertex indexed sheet coordinates
            // each sheet can share all vertex coordinates
            var vic = m.VertexIndexCount;
            var sheetCoordIndexArray = new int[vic];
            var sheetCoordArray = new V2d[vic]; // allocate for worst case
            var vertexMap = new int[pa.Length].Set(-1);
            var vertexIndices = new List<int>();

            var sheetCoordIndex = 0;
            var sheetCoordPosIndex = 0;
            
            // build the face sheets
            var sheetIndexArray = new int[fc].Set(-1); // -1 to for invalid faces?
            var sheetFaceIndexArray = new int[fc];
            var sheetFaceIndex = 0;
            var sheetArray = new FaceSheet[clusters.Length].SetByIndex(i => 
            {
                vertexIndices.Clear();
                int vertexCount = 0;

                var c = clusters[i];
                var fs = new FaceSheet();
                foreach (var fi in c.Faces)
                {
                    fs.AddFace(m, fi, faa, fna, fca);
                    for (int fvi = fia[fi], fve = fia[fi + 1]; fvi < fve; fvi++)
                    {
                        var vi = via[fvi];
                        var coordIndex = vertexMap[vi];
                        if (coordIndex < 0)
                        {
                            // add index for this vertex
                            coordIndex = vertexMap[vi] = sheetCoordIndex++;
                            vertexIndices.Add(vi);
                            vertexCount++;
                        }

                        sheetCoordIndexArray[fvi] = coordIndex;
                    }

                    sheetIndexArray[fi] = i;
                }

                // check if only degenerated faces have been added; this can happen if mesh contains degenerated faces with normal variation
                // -> init Centroid and Normal by averaging face centroids and normals (without area weighting)
                if (fs.Area == 0)
                {
                    foreach (var fi in c.Faces)
                    {
                        fs.Normal += fna[fi];
                        fs.Centroid += fca[fi];
                    }
                    var scale = 1.0 / c.Faces.Count;
                    fs.Normal *= scale;
                    fs.Centroid *= scale;
                }
                else // normalize using the area to get weighted average
                {
                    fs.Normalize();
                }

                fs.InitFrame();

                vertexIndices.ForEach(vi => vertexMap[vi] = -1);
                
                var projectedVertices = vertexIndices.SelectToArray(vi => fs.Global2Local.TransformPos(pa[vi]).XY);
                var convexHull = projectedVertices.ConvexHullIndexPolygon();
                var hullPoly = convexHull.ToPolygon2d();
                var minRot = hullPoly.ComputeMinAreaEnclosingBoxRotation();
                fs.MinimumRotation = -Fun.Acos(minRot[0]);
                fs.Global2Local = (M44d)minRot * fs.Global2Local;
                projectedVertices.Apply(v => minRot * v);
                fs.LocalBox = new Box2d(projectedVertices);

                var lbMin = fs.LocalBox.Min;
                var lbSize = fs.LocalBox.Size.Reciprocal;

                projectedVertices.Apply(p => (p - lbMin) * lbSize);

                projectedVertices.CopyTo(projectedVertices.Length, sheetCoordArray, sheetCoordPosIndex);
                sheetCoordPosIndex += projectedVertices.Length;
                
                fs.VertexCount = vertexCount;
                //fs.FaceIndices = c.Faces.ToArray();
                fs.FirstFaceIndex = sheetFaceIndex;
                foreach (var fi in c.Faces)
                    sheetFaceIndexArray[sheetFaceIndex++] = fi;

                return fs;
            });

            // store coordinates and sheets into mesh
            m.FaceVertexAttributes[coordsName] = sheetCoordIndexArray;
            m.FaceVertexAttributes[-coordsName] = sheetCoordPosIndex < sheetCoordArray.Length ? sheetCoordArray.Copy(sheetCoordPosIndex) : sheetCoordArray; // copy range of actually used coordinates
            m.FaceAttributes[sheetName] = sheetIndexArray;
            m.FaceAttributes[-sheetName] = sheetArray;
            m.InstanceAttributes[PolyMesh.Property.SheetFaceIndices] = sheetFaceIndexArray;
        }

        /// <summary>
        /// Locally optimizes the outline of a cluster.
        /// </summary>
        void OptimizeEdges(PolyMesh m, IntSet borderEdges, IntSet edgeSet)
        {
            //Report.BeginTimed("optimize border faces");

            var edgesToProcess = edgeSet;

            int iterations = 0;

            do
            {
                var facesToProcess = edgesToProcess.Select(ei => m.GetEdge(ei)).SelectMany(e => e.Faces);                                        

                var newClusterEdges = new IntSet();

                var constrainedClusters = new HashSet<ClusterUsingList>();
                var potentialClusters = new HashSet<ClusterUsingList>();

                // for each face check all neighbour clusters and try to merge them
                foreach (var face in facesToProcess)
                {
                    var faceIndex = face.Index;
                    var faceCluster = m_clusterRefs[face.Index];
                    var faceEdges = face.Edges.ToArray();
                    constrainedClusters.Clear();
                    foreach (var e in faceEdges.Where(e => !e.OppositeIsBorder && borderEdges.Contains(e.Index))) // edges of faces which are borders
                        constrainedClusters.Add(m_clusterRefs[e.OppositeFaceIndex]);
                    // find neighbour faces/cluster with which a merge is valid
                    var potentialEdges = faceEdges.Where(e =>
                    {
                        if (!edgeSet.Contains(e.Index) || e.OppositeIsBorder)
                            return false; // invalid edge
                        if (constrainedClusters.Count == 0)
                            return true;
                        return !constrainedClusters.Contains(m_clusterRefs[e.OppositeFace.Index]);
                    }).ToArray();

                    if (potentialEdges.IsEmpty())
                        continue;

                    var currentPenalty = potentialEdges.Sum(e => (e.ToVertex.Position - e.FromVertex.Position).LengthSquared);
                    var currentBestCluster = faceCluster;

                    potentialClusters.Clear();
                    foreach (var e in potentialEdges)
                        potentialClusters.Add(m_clusterRefs[e.OppositeFaceIndex]);
                    //var potentialClusters = potentialEdges.Select(e => cia[e.OppositeFace.Index]).Distinct().ToArray();

                    foreach (var pc in potentialClusters)
                    {
                        var penalty = faceEdges.Where(e => !e.OppositeIsBorder && !borderEdges.Contains(e.Index) && m_clusterRefs[e.OppositeFaceIndex] != pc).Sum(e => (e.ToVertex.Position - e.FromVertex.Position).LengthSquared);
                        if (penalty < currentPenalty)
                        {
                            currentPenalty = penalty;
                            currentBestCluster = pc;
                        }
                    }

                    if (currentBestCluster != faceCluster)
                    {
                        //int otherFaceIndex = faceEdges.Select(e => e.OppositeFace.Index).Where(fi => cia[fi] == currentBestCluster).First();
                        //cia[otherFaceIndex] = currentBestCluster;
                        m_clusterRefs[faceIndex] = currentBestCluster;
                        faceCluster.Faces.Remove(faceIndex);
                        currentBestCluster.Faces.Add(faceIndex);
                        // remove all current cluster edges

                        //Report.Line("face({0}): {1}=>{2}", faceIndex, faceClusterIndex, currentBestCluster);

                        // 
                        var faceClusterBorderEdges = faceCluster.BorderEdges;
                        var bestClusterBorderEdges = currentBestCluster.BorderEdges;
                        foreach (var ei in faceEdges.Select(fe => fe.Index))
                        {
                            edgeSet.TryRemove(ei);
                            if (borderEdges.Contains(ei))
                            {
                                var a = !bestClusterBorderEdges.Contains(ei);
                                var b = faceClusterBorderEdges.Contains(ei);
                                if (!a || !b)
                                    Report.Warn("epic!");
                                faceClusterBorderEdges.Remove(ei);
                                bestClusterBorderEdges.Add(ei);
                            }
                        }
                        // add all new cluster edges
                        edgeSet.AddRange(faceEdges.Where(fe => fe.OppositeIsBorder 
                                                            || borderEdges.Contains(fe.Index)
                                                            || m_clusterRefs[fe.OppositeFaceIndex] != currentBestCluster)
                                                  .Select(fe => fe.Index));

                        // new edges to process
                        newClusterEdges.AddRange(faceEdges.Where(fe => !fe.OppositeIsBorder && !borderEdges.Contains(fe.Index) && m_clusterRefs[fe.OppositeFaceIndex] != currentBestCluster).Select(fe => fe.Index));
                        
                        if (faceCluster.Faces.IsEmptyOrNull())
                            m_clusters.Remove(faceCluster);
                        //Report.Value("pc", currentBestCluster);
                    }
                }

                edgesToProcess = newClusterEdges;
                iterations++;
            }
            while (!edgesToProcess.IsEmpty());

            //Report.End();

            //Report.Value("iterationCount", iterations);
        }
    }

    public partial class PolyMesh
    {
        /// <summary>
        /// Collapse vertices with a given clustering
        /// Note: Edges can degenerate (FromVertexIndex = ToVertexIndex) and
        ///       creates an invalid topology (may use WithoutDegeneratedEdges)
        /// </summary>
        public PolyMesh VertexClusteredCopy(
                    Clustering.Clustering vertexClustering
                    )
        {
            var via = m_vertexIndexArray;
            var nvia = via.Map(vi => vertexClustering.IndexArray[vi]);
            var pa = vertexClustering.CentroidArray(PositionArray);

            var vaDict = new SymbolDict<Array> { { Property.Positions, pa } };
            foreach (var a in VertexIAttributes)
            {
                if (a.Name == Property.Positions) continue;
                var ia = a.IndexArray;
                if (ia != null)
                {
                    vaDict[a.Name] = ia.ForwardMappedCopy(vertexClustering.IndexArray, vertexClustering.Count);
                    vaDict[-a.Name] = a.ValueArray;
                }
                else
                {
                    var nva = Array.CreateInstance(a.ValueType, vertexClustering.Count);
                    a.ForwardMappedCopyTo(vertexClustering.IndexArray, nva, 0);
                    vaDict[a.Name] = nva;
                }
            }

            var overrides = new SymbolDict<object>
            {
                { Property.VertexIndexArray, nvia },
                { Property.FaceAttributes, FaceAttributes.Copy() },
                { Property.VertexAttributes, vaDict },
                { Property.FaceVertexAttributes, FaceVertexAttributes.Copy() },
                { Property.InstanceAttributes,  InstanceAttributes.Copy() },
            };
            var m = new PolyMesh(this, overrides);
            return m;
        }

    }
}
