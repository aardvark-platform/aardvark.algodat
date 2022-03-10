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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TypeInfo = Aardvark.Base.Coder.TypeInfo;

namespace Aardvark.Geometry
{
    /// <summary>
    /// A KdIntersectionTree makes it possible to quickly calculate
    /// intersections between a ray and an
    /// <see cref="IIntersectableObjectSet"/>.
    /// </summary>
    [RegisterTypeInfo(Version = 1)]
    public class KdIntersectionTree : IFieldCodeable, IBoundingBox3d
    {
        public KdNode Tree;
        private Box3d m_box;
        private IIntersectableObjectSet m_objectSet;
        private BuildFlags m_buildFlags;
        private bool m_leafSet = true;

        #region Constructors

        public KdIntersectionTree()
            : this(null, BuildFlags.Default)
        { }

        public KdIntersectionTree(
            IIntersectableObjectSet objectSet,
            BuildFlags buildFlags = BuildFlags.Default,
            double relativeMinCellSize = 0.0,
            double splitPlaneEpsilon = 0.0)
        {
            if (relativeMinCellSize == 0.0) relativeMinCellSize = c_minCellSize;
            if (splitPlaneEpsilon == 0.0) splitPlaneEpsilon = c_eps;

            m_buildFlags = buildFlags;
            Build(objectSet, relativeMinCellSize, splitPlaneEpsilon);
        }

        #endregion

        #region Properties

        public BuildFlags BuildOptions
        {
            get { return m_buildFlags; }
            set { m_buildFlags = value; }
        }

        public IIntersectableObjectSet ObjectSet
        {
            get { return m_objectSet; }
            set { m_objectSet = value; }
        }

        public int Count => ObjectSet.ObjectCount;

        #endregion

        #region BuildFlags

        [Flags]
        public enum BuildFlags
        {
            None = 0,
            FastIntersection = 7,       // split if more then 7 objs/node
            MediumIntersection = 36,    // split if more then 36 objs/node
            SlowIntersection = 108,     // split if more then 108 objs/node
            SplitThresholdMask = 0x00ff,
            FastBuild = 0x0100,         // 1 split try in each dimension
            MediumBuild = 0x0300,       // 3 split tries in each dimension
            SlowBuild = 0x0500,         // 5 split tries in each dimension
            VerySlowBuild = 0x0700,     // 7 split tries in each dimension
            BuildMask = 0x0f00,
            Picking = SlowIntersection | FastBuild,
            Default = MediumIntersection | MediumBuild,
            Raytracing = FastIntersection | SlowBuild,
            OptimalRaytracing = FastIntersection | VerySlowBuild,
            EmptySpaceOptimization = 0x1000,
            Hierarchical = 0x10000,
            NoMultithreading = 0x20000,
            OnlyInnerNodes = 0x40000,
        };

        #endregion

        #region OutputParameters

        internal class OutputParameters
        {
            public int SplitCount;
            public int SingleCount;
            public int LeafCount;
            public int ObjectCount;
            public double LeafVolume;
            public int BigLeafCount;
            public int BigObjectCount;
            public double BigVolume;
            public int SmallLeafCount;
            public int SmallObjectCount;
            public double SmallVolume;
            public int MaxLeafCount;
            public int MaxObjectCount;
            public double MaxVolume;

            public int NodeCount => SplitCount + SingleCount + LeafCount;

            public void Add(OutputParameters other)
            {
                SplitCount += other.SplitCount;
                SingleCount += other.SingleCount;
                LeafCount += other.LeafCount;
                ObjectCount += other.ObjectCount;
                LeafVolume += other.LeafVolume;
                BigLeafCount += other.BigLeafCount;
                BigObjectCount += other.BigObjectCount;
                BigVolume += other.BigVolume;
                SmallLeafCount += other.SmallLeafCount;
                SmallObjectCount += other.SmallObjectCount;
                SmallVolume += other.SmallVolume;
                if (MaxObjectCount <= other.MaxObjectCount)
                {
                    if (MaxObjectCount < other.MaxObjectCount)
                    {
                        MaxLeafCount = other.MaxLeafCount;
                        MaxObjectCount = other.MaxObjectCount;
                        MaxVolume = other.MaxVolume;
                    }
                    else
                    {
                        MaxLeafCount += other.MaxLeafCount;
                        MaxVolume += other.MaxVolume;
                    }
                }
            }
        }

        #endregion

        #region InputParameters

        internal class InputParameters
        {
            public Box3d[] BoxArray;

            [Flags]
            public enum ReportLevel
            {
                Nothing = 0x00000,
                Progress = 0x00001,
                Nodes = 0x00002,
            };

            // input params
            public bool OnlyInnerNodes;
            public int SplitThreshold;
            public int MaxDepth;
            public double MinCellSize;
            public double Eps;
            public int BuildTrials;
            public ReportLevel Report;
            public int ParallelSplitThreshold;
            public V3d CostFactor;

            public bool ReportProgress => (Report & ReportLevel.Progress) != 0;
            public bool ReportNodes => (Report & ReportLevel.Nodes) != ReportLevel.Nothing;
        }

        #endregion

        #region Intersection Methods

        /// <summary>
        /// Intersect the kD-Tree with the supplied ray within a supplied
        /// t parameter interval.
        /// </summary>
        /// <param name="fastRay">The fast ray with which to intersect.</param>
        /// <param name="tmin">The lower limit of the t parameter interval.</param>
        /// <param name="tmax">The upper limit of the t parameter interval.</param>
        /// <param name="hit">Contains the previously found nearest hit,
        /// and will be filled with a closer hit, if one is found.</param>
        /// <returns>True if a closer hit is found, false otherwise.</returns>
        public bool Intersect(
                FastRay3d fastRay,
                double tmin, double tmax,
                ref ObjectRayHit hit
                )
        {
            return Intersect(fastRay, null, null, tmin, tmax, ref hit);
        }

        public bool Intersect(
                FastRay3d fastRay,
                Func<IIntersectableObjectSet, int, bool> objectFilter,
                Func<IIntersectableObjectSet, int, int, RayHit3d, bool> hitFilter,
                double tmin, double tmax,
                ref ObjectRayHit hit
                )
        {
            KdIntersectionRay kdRay = new()
            {
                FastRay = fastRay,
                ObjectSet = m_objectSet,
                Hit = hit,
                ObjectFilter = objectFilter,
                HitFilter = hitFilter
            };

            if (Tree == null)
            {
                Report.Warn("Kdtree.Intersect - kd-tree not built");
                return false;
            }

            if (fastRay.Intersects(m_box, ref tmin, ref tmax))
            {
                TP3d tpmin, tpmax;
                tpmin.T = tmin; tpmax.T = tmax;
                tpmin.P = kdRay.FastRay.Ray.GetPointOnRay(tmin);
                tpmax.P = kdRay.FastRay.Ray.GetPointOnRay(tmax);
                if (Tree.Intersect(ref kdRay, ref tpmin, ref tpmax))
                {
                    hit = kdRay.Hit;
                    if (m_leafSet)
                        hit.ObjectStack = null;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the kd-tree intersects the supplied box. Note
        /// that this is only an approximate test, i.e. it only tests the
        /// bounding box of the kd-tree against the supplied box, not the
        /// individual objects contained in the tree.
        /// </summary>
        public bool IntersectsBox(Box3d box)
        {
            return m_box.Intersects(box);
        }

        /// <summary>
        /// Returns true if the kd-tree is completeley contained inside the
        /// supplied box.
        /// </summary>
        public bool IsInsideBox(Box3d box)
        {
            return m_box.Contains(box);
        }

        /// <summary>
        /// Return the closest point of all objects in the kd-tree to the
        /// supplied query point.
        /// </summary>
        public bool ClosestPoint(
                V3d query,
                ref ObjectClosestPoint closest
                )
        {
            return ClosestPoint(query, null, null, ref closest);
        }

        /// <summary>
        /// Return the closest point of all objects in the kd-tree to the
        /// supplied query point. Note that the supplied filter functions
        /// are ignored (the functionality has not been implemented yet).
        /// </summary>
        public bool ClosestPoint(
            V3d query,
            Func<IIntersectableObjectSet, int, bool> ios_index_objectFilter,
            Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool>
                    ios_index_part_ocp_pointFilter,
            ref ObjectClosestPoint closest
            )
        {
            var qp = new KdQueryPoint
            {
                Point = query,
                ObjectSet = m_objectSet,
                ClosestPoint = closest,
                ObjectFilter = ios_index_objectFilter,
                PointFilter = ios_index_part_ocp_pointFilter,
            };
            var min = m_box.Min;
            var max = m_box.Max;

            if (Tree.ClosestPoint(ref qp, ref min, ref max))
            {
                closest = qp.ClosestPoint;
                return true;
            }
            return false;
        }

        public KdNode NodesIntersectingBox(Box3d box)
        {
            if (!box.Intersects(m_box)) return null;
            return Tree.Intersect(ref box, m_box.OutsideFlags(box));
        }

        /// <summary>
        /// Intersect the kd-tree with the supplied box, and return the indices
        /// of all objects that intersect the box. Note, that due to the way the
        /// result is computed (as fast as possible), indices may be returned
        /// twice in the result.
        /// </summary>
        public IEnumerable<int> ObjectsIntersectingBox(Box3d box)
        {
            if (!box.Intersects(m_box)) return Enumerable.Empty<int>();
            KdNode pruned = Tree.Intersect(ref box, m_box.OutsideFlags(box));
            if (pruned == null) return Enumerable.Empty<int>();
            return pruned.Indices(
                (interiorArray, f, c) => interiorArray.Elements(f, c),
                (borderArray, f, c) => borderArray.ElementsWhere(f, c,
                    i => m_objectSet.ObjectIntersectsBox(i, box))
                );
        }

        /// <summary>
        /// Intersect the kd-tree with the supplied box, and return the indices
        /// of all objects that are completeley contained in the box. Note, that
        /// due to the way the result is computed (as fast as possible), indices
        /// may be returned twice in the result.
        /// </summary>
        public IEnumerable<int> ObjectsInsideBox(Box3d box)
        {
            if (!box.Intersects(m_box)) return Enumerable.Empty<int>();
            KdNode pruned = Tree.Intersect(ref box, m_box.OutsideFlags(box));
            if (pruned == null) return Enumerable.Empty<int>();
            IEnumerable<int> inside(int[] leafArray, int f, int c) 
                => leafArray.ElementsWhere(f, c, i => m_objectSet.ObjectIsInsideBox(i, box));
            return pruned.Indices(inside, inside);
        }

        #endregion

        #region Building Constants

        ///// <summary>
        ///// For debugging it can be useful to only create KdInner nodes,
        ///// no optimized x/y/z, and left or right nodes.
        ///// </summary>
        //private const bool c_onlyKdInnerNodes = false;

        /// <summary>
        /// Smaller leafs are considered to have linear cost.
        /// </summary>
        private const int c_costThreshold = 63;

        /// <summary>
        /// Do not split cells smaller than this value.
        /// </summary>
        private const double c_minCellSize = 1e-6;

        /// <summary>
        /// Enlarge enclosing bounding box by this value (relative).
        /// </summary>
        private const double c_enlargeEps = 1e-5;

        /// <summary>
        /// Epsilon for split plane calculations.
        /// This epsilon is chosen to be larger than the single-precision
        /// floating point epsilon in order to prevent single-precision
        /// objects contained in the bounding box to create intersections
        /// outside the bounding box.
        /// </summary>
        private const double c_eps = 1e-7; // must be > double.Epsilon

        /// <summary>
        /// Penalty factor for objects that are split and appear
        /// in both child nodes.
        /// </summary>
        private const double c_splitPenalty = 2.0;

        /// <summary>
        /// Preference factor for splitting the longest axis of a cell.
        /// </summary>
        private const double c_majorAxisPref = 1.01;

        /// <summary>
        /// Preference factor for splitting a cell in the middle.
        /// </summary>
        private const double c_midSplitPref = 1.001;

        /// <summary>
        /// Maximum tree depth.
        /// </summary>
        private const int c_maxDepth = 32;

        public static int ReportVerbosity = 3;
        public static int DetailReportVerbosity = 5;

        #endregion

        #region Building

        /// <summary>
        /// Recursive node creation.
        /// </summary>
        private KdNode CreateNode(
            InputParameters inParams,
            OutputParameters outParams,
            int depth,
            int[] objectIndexArray,
            Box3d treeBox,
            out bool split
            )
        {
            int numObjects = objectIndexArray.Length;
            if (numObjects == 0)
            {
                //if (inParams.ReportProgress)
                //{
                //    parameters.m_progress += parameters.m_progressScale;
                //    Report.Progress(3, parameters.m_progress);
                //}
                split = false;
                return null;
            }

            //double childrenCost = 0.0;
            //if (inParams.ReportProgress)
            //{
            //    childrenCost = System.Math.Log((double)numObjects);
            //    // as compared to this level cost
            //    parameters.m_progressScale *= 1.0 / (childrenCost + 1.0);
            //    // split cost between this level and children
            //}

            int majorDim = treeBox.MajorDim;
            V3d boxSize = treeBox.Size;

            if (numObjects > inParams.SplitThreshold
                && depth < inParams.MaxDepth
                && boxSize[majorDim] > inParams.MinCellSize) // subdivide further
            {
                KdNode leftNode = null;
                KdNode rightNode = null;
                byte dim = 0;
                
                dim = CalculateSplitPlane(inParams, objectIndexArray, treeBox,
                        out double value, out int numLeftObjects, out int numRightObjects,
                        out bool[] leftFlags, out bool[] rightFlags);

                //if (inParams.ReportProgress)
                //{
                //        params.m_reporter->integer("depth", depth);
                //        params.m_reporter->integer("objects", numObjects);
                //        params.m_reporter->integer("dim", dim);
                //        params.m_reporter->floating("split", value);
                //        params.m_reporter->integer("left", numLeftObjects);
                //        params.m_reporter->integer("right", numRightObjects);
                //}

                // check if split is useless -> create leaf immediately
                if (numLeftObjects == numObjects && numRightObjects == numObjects)
                {
                    split = false;
                    return new KdLeaf(objectIndexArray);
                }

                Box3d leftBox = treeBox;
                Box3d rightBox = treeBox;

                leftBox.Max[dim] = value + inParams.Eps;
                rightBox.Min[dim] = value - inParams.Eps;

                int[] leftObjectIndexArray = new int[numLeftObjects];
                int[] rightObjectIndexArray = new int[numRightObjects];

                int li = 0;
                int ri = 0;

                //double progressPass = 0.1 * parameters.m_progressScale;
                //if (parameters.m_buildTrials == 1) progressPass *= 2.5;
                //double progressStep = 256.0 * progressPass
                //                      / (double)numObjects;
                //double progressStart = parameters.m_progress;

                int i;
                for (i = 0; i < numObjects; i++)
                {
                    int oi = objectIndexArray[i];
                    if (leftFlags[i]) leftObjectIndexArray[li++] = oi;
                    if (rightFlags[i]) rightObjectIndexArray[ri++] = oi;

                    //if (inParams.ReportProgress && ((i & 0xff) == 0))
                    //{
                    //    parameters.m_progress += progressStep;
                    //    Report.Progress(3, parameters.m_progress);
                    //}
                }
                //if (inParams.ReportProgress && ((i & 0xff) != 0))
                //{
                //    parameters.m_progress = progressStart + progressPass;
                //    Report.Progress(3, parameters.m_progress);
                //}

                objectIndexArray = null;
                bool leftSplit = false;
                bool rightSplit = false;

                //double leftCost;
                //double rightCost = 0.0;
                //double progressScale = 0.0;
                //if (inParams.ReportProgress)
                //{
                //    leftCost = numLeftObjects + 1;
                //    rightCost = numRightObjects + 1;
                //    if (leftCost > 3.0) leftCost *= System.Math.Log(leftCost);
                //    if (rightCost > 3.0) rightCost *= System.Math.Log(rightCost);
                //    progressScale = parameters.m_progressScale * childrenCost
                //                    / (leftCost + rightCost);

                //    // for left subtree
                //    parameters.m_progressScale = progressScale * leftCost;
                //    // for right subtree
                //    parameters.m_progressScale = progressScale * rightCost;
                //}

                if (numObjects > inParams.ParallelSplitThreshold)
                {
                    var outParams2 = new OutputParameters();
                    Parallel.Invoke(
                        new Action[] {
                            delegate
                            {
                                leftNode = CreateNode(inParams, outParams,
                                                      depth + 1,
                                                      leftObjectIndexArray,
                                                      leftBox, out leftSplit);
                            },
                            delegate
                            {
                                rightNode = CreateNode(inParams, outParams2,
                                                       depth + 1,
                                                       rightObjectIndexArray,
                                                       rightBox, out rightSplit);
                            }
                        }
                        //, Kernel.TaskManager.CpuBoundLowPriority, TaskCreationOptions.None
                    );
                    outParams.Add(outParams2);
                }
                else
                {
                    leftNode = CreateNode(inParams, outParams, depth + 1,
                                          leftObjectIndexArray, leftBox,
                                          out leftSplit);
                    rightNode = CreateNode(inParams, outParams, depth + 1,
                                           rightObjectIndexArray, rightBox,
                                           out rightSplit);
                }

                if (leftSplit || rightSplit
                    || numLeftObjects != numObjects
                    || numRightObjects != numObjects)
                {
                    if (leftNode != null)
                    {
                        if (rightNode != null)
                        {
                            outParams.SplitCount++;
                            split = true;
                            if (inParams.OnlyInnerNodes)
                                return new KdInner(dim, value, leftNode, rightNode);
                            switch (dim)
                            {
                                case 0:
                                    return new KdInnerBothX(
                                            value, leftNode, rightNode);
                                case 1:
                                    return new KdInnerBothY(
                                            value, leftNode, rightNode);
                                case 2:
                                    return new KdInnerBothZ(
                                            value, leftNode, rightNode);
                            }
                        }
                        else
                        {
                            outParams.SingleCount++;
                            split = true;
                            if (inParams.OnlyInnerNodes)
                                return new KdInner(dim, value, leftNode, null);  
                            return new KdInnerLeft(dim, value, leftNode);
                        }
                    }
                    else
                    {
                        if (rightNode != null)
                        {
                            outParams.SingleCount++;
                            split = true;
                            if (inParams.OnlyInnerNodes)
                                return new KdInner(dim, value, null, rightNode);
                            return new KdInnerRight(dim, value, rightNode);
                        }
                        else
                        {
                            split = false;
                            return null;
                        }
                    }
                }
                // Left and right have the same object count, and have not
                // been split, so we can join the two leaves again.
                outParams.LeafCount--;
                if (numObjects > inParams.SplitThreshold)
                {
                    outParams.BigLeafCount--;
                    outParams.BigObjectCount -= numObjects;
                }
                else
                {
                    outParams.SmallLeafCount--;
                    outParams.SmallObjectCount -= numObjects;
                }
                if (numObjects == outParams.MaxObjectCount)
                    outParams.MaxLeafCount--;
                outParams.ObjectCount -= numObjects;
                split = false;
                rightNode = null;
                return leftNode;
            }
            else
            {
                var volume = boxSize.X * boxSize.Y * boxSize.Z;
                var leaf = new KdLeaf(objectIndexArray);
                //if (inParams.ReportProgress)
                //{
                //    parameters.m_progress += parameters.m_progressScale;
                //    Report.Progress(3, parameters.m_progress);
                //}
                outParams.LeafCount++;
                outParams.LeafVolume += volume;
                if (numObjects > inParams.SplitThreshold)
                {
                    outParams.BigLeafCount++;
                    outParams.BigObjectCount += numObjects;
                    outParams.BigVolume += volume;
                }
                else
                {
                    outParams.SmallLeafCount++;
                    outParams.SmallObjectCount += numObjects;
                    outParams.SmallVolume += volume;
                }
                if (numObjects >= outParams.MaxObjectCount)
                {
                    if (numObjects > outParams.MaxObjectCount)
                    {
                        outParams.MaxLeafCount = 1;
                        outParams.MaxObjectCount = numObjects;
                        outParams.MaxVolume = volume;
                    }
                    else
                    {
                        outParams.MaxLeafCount++;
                        outParams.MaxVolume += volume;
                    }
                }
                outParams.ObjectCount += numObjects;
                split = false;
                return leaf;
            }
        }

        public void Build(
                IIntersectableObjectSet objectSet,
                double relativeMinCellSize = c_minCellSize,
                double splitPlaneEpsilon = c_eps)
        {
            m_objectSet = objectSet;
            Build(m_buildFlags, relativeMinCellSize, splitPlaneEpsilon);
        }

        private void Build(
                BuildFlags options, double relativeMinCellSize = c_minCellSize, double splitPlaneEpsilon = c_eps
            )
        {
            if (m_objectSet == null) return;
            m_leafSet = m_objectSet is not KdTreeSet;
            if (Tree != null) { Tree = null; }
            var inParams = new InputParameters();
            var outParams = new OutputParameters();

            if (relativeMinCellSize == 0) relativeMinCellSize = c_minCellSize;

            inParams.OnlyInnerNodes = (options & BuildFlags.OnlyInnerNodes) != 0; // was: c_onlyKdInnerNodes;
            inParams.MaxDepth = c_maxDepth;
            inParams.Report = InputParameters.ReportLevel.Nodes;
            inParams.ParallelSplitThreshold =
                (options & BuildFlags.NoMultithreading) != 0 ? int.MaxValue : 8192;

            int splitThreshold = (int)(options & BuildFlags.SplitThresholdMask);

            if (splitThreshold != 0)
                inParams.SplitThreshold = splitThreshold;
            else
                inParams.SplitThreshold = (int)BuildFlags.MediumIntersection;

            int buildTrials = ((int)(options & BuildFlags.BuildMask)) >> 8;

            if (buildTrials != 0)
                inParams.BuildTrials = buildTrials;
            else
                inParams.BuildTrials = 3;

            int numObjects = m_objectSet.ObjectCount;

            Report.BeginTimed(ReportVerbosity, "building kd-tree [{0}]",
                             numObjects);

            if ((options & BuildFlags.EmptySpaceOptimization) != 0)
            {
                var boxArray = new Box3d[numObjects];
                m_box = Box3d.Invalid;
                for (int oi = 0; oi < numObjects; oi++)
                {
                    var box = m_objectSet.ObjectBoundingBox(oi);
                    boxArray[oi] = box;
                    m_box.ExtendBy(box);
                }
                inParams.BoxArray = boxArray;
            }
            else
                m_box = m_objectSet.ObjectBoundingBox();

            V3d size = m_box.Size;
            double majorSize = size[m_box.MajorDim];


            // enlarge boundingbox by a factor
            double enlargeEps = c_enlargeEps * majorSize;
            m_box.EnlargeBy(enlargeEps);
            inParams.Eps = splitPlaneEpsilon * majorSize;

            inParams.CostFactor = new V3d(1, 1, 1);
            for (int i = 0; i < 3; i++)
                if (size[i] < c_minCellSize)
                    inParams.CostFactor[i] = 1e8;

            inParams.MinCellSize = majorSize * relativeMinCellSize;            

            double volume = size.X * size.Y * size.Z;
            if (Report.Verbosity > 2)
            {

                Report.Begin(DetailReportVerbosity, "parameters");
                Report.Value(DetailReportVerbosity, "number of objects", numObjects);
                Report.Value(DetailReportVerbosity, "volume", volume);
                if (volume > 0.0)
                    Report.Value(DetailReportVerbosity, "bounding box", m_box);
                Report.End(DetailReportVerbosity); // parameters
            }

            Report.BeginTimed(DetailReportVerbosity, "creating nodes");

            int[] objectIndexArray = new int[numObjects];
            for (int oi = 0; oi < numObjects; oi++) objectIndexArray[oi] = oi;
            Tree = CreateNode(inParams, outParams, 0, objectIndexArray, m_box, out _);

            Report.End(DetailReportVerbosity);

            if (Report.Verbosity > 2)
            {
                Report.Begin(DetailReportVerbosity, "statistics");
                Report.Value(DetailReportVerbosity, "volume", volume);
                Report.Value(DetailReportVerbosity, "number of splits", outParams.SplitCount);
                Report.Value(DetailReportVerbosity, "number of singles", outParams.SingleCount);
                Report.Value(DetailReportVerbosity, "number of leafs", outParams.LeafCount);
                if (outParams.LeafCount > 0)
                {
                    Report.Begin(DetailReportVerbosity);
                    Report.Value(DetailReportVerbosity, "objects in leafs",
                                    outParams.SmallObjectCount);
                    Report.Value(DetailReportVerbosity, "volume of leafs",
                                    outParams.SmallVolume);
                    Report.Value(DetailReportVerbosity, "objects per leaf",
                        outParams.ObjectCount / (double)outParams.LeafCount);
                    Report.Value(DetailReportVerbosity, "volume per leaf",
                        outParams.LeafVolume / outParams.LeafCount);
                    Report.End(DetailReportVerbosity);
                }
                Report.Value(DetailReportVerbosity, "number of small leafs",
                                outParams.SmallLeafCount);
                if (outParams.SmallLeafCount > 0)
                {
                    Report.Begin(DetailReportVerbosity);
                    Report.Value(DetailReportVerbosity, "objects in small leafs",
                        outParams.SmallObjectCount);
                    Report.Value(DetailReportVerbosity, "volume of small leafs",
                        outParams.SmallVolume);
                    Report.Value(DetailReportVerbosity, "objects per small leaf",
                        outParams.SmallObjectCount
                            / (double)outParams.SmallLeafCount);
                    Report.Value(DetailReportVerbosity, "volume per small leaf",
                        outParams.SmallVolume / outParams.SmallLeafCount);
                    Report.End(DetailReportVerbosity);
                }
                Report.Value(DetailReportVerbosity, "number of big leafs",
                        outParams.BigLeafCount);
                if (outParams.BigLeafCount > 0)
                {
                    Report.Begin(DetailReportVerbosity);
                    Report.Value(DetailReportVerbosity, "objects in big leafs",
                        outParams.BigObjectCount);
                    Report.Value(DetailReportVerbosity, "volume of big leafs",
                        outParams.BigVolume);
                    Report.Value(DetailReportVerbosity, "objects per big leaf",
                        outParams.BigObjectCount
                            / (double)outParams.BigLeafCount);
                    Report.Value(DetailReportVerbosity, "volume per big leaf",
                        outParams.BigVolume / outParams.BigLeafCount);
                    Report.End(DetailReportVerbosity);
                }
                Report.Value(DetailReportVerbosity, "number of max leafs",
                                outParams.MaxLeafCount);
                if (outParams.BigLeafCount > 0)
                {
                    Report.Begin(DetailReportVerbosity);
                    Report.Value(DetailReportVerbosity, "objects in max leafs",
                        outParams.MaxObjectCount);
                    Report.Value(DetailReportVerbosity, "volume of max leafs",
                        outParams.MaxVolume);
                    Report.Value(DetailReportVerbosity, "objects per max leaf",
                        outParams.MaxObjectCount
                            / (double)outParams.MaxLeafCount);
                    Report.Value(DetailReportVerbosity, "volume per max leaf",
                        outParams.MaxVolume / outParams.MaxLeafCount);
                    Report.End(DetailReportVerbosity);
                }
                Report.End(DetailReportVerbosity); // statistics
            }

            Report.End(ReportVerbosity, ": {0} + {1} * {2:0.0}",
                        outParams.SplitCount + outParams.SingleCount,
                        outParams.LeafCount,
                        outParams.LeafCount > 0 ? outParams.ObjectCount / (double)outParams.LeafCount : 0
                ); // building kd-tree

            if (Tree == null) Tree = new KdLeaf(new int[0]);
        }

        public void Flatten()
        {
            if (Tree is not KdFlat) Tree = Tree.Flatten();
        }

        // This table *must* contain the value 0.5 as first entry!
        // If you change the number of values in this table,
        // you need to update the progressStep accordingly.
        private static readonly double[] s_splitTryTable =
        {
            0.5f, 0.25f, 0.75f, 0.375f, 0.625f, 0.125f, 0.875f
        };

        private byte CalculateSplitPlane(
            InputParameters inParams,
            int[] objectIndexArray,
            Box3d box,
            out double bestSplit,
            out int bestLeft,
            out int bestRight,
            out bool[] leftFlags,
            out bool[] rightFlags
            )
        {
            bestSplit = 0.0f;
            bestLeft = 0;
            bestRight = 0;
            leftFlags = null;
            rightFlags = null;

            byte bestDim = 0;
            double bestCost = double.MaxValue;

            V3d size = box.Size;
            byte largestDim = (byte)size.MajorDim;
            double invBoxArea =
                1.0 / (size.X * size.Y + size.X * size.Z + size.Y * size.Z);

            //double progressPass = 0.1 * parameters.m_progressScale;
            //if (parameters.m_buildTrials == 1) progressPass *= 2.5;
            //double progressStep = 256.0 * progressPass / (double)objectIndexArray.Length;

            bool[] lfa = null;
            bool[] rfa = null;

            for (byte dim = 0; dim < 3; dim++)
            {
                var smin = double.MaxValue; // empty space
                var smax = double.MinValue; // in this dim
                var a = size[dim];
                var b = size[(dim + 1) % 3];
                var c = size[(dim + 2) % 3];

                for (int j = 0; j < inParams.BuildTrials; j++)
                {
                    double f = s_splitTryTable[j];
                    double delta = box.Min[dim];
                    double split = delta + f * size[dim];

                    if (split > smin || split < smax) continue; // already tried
                    int numLeft = 0, numRight = 0, numBoth = 0;

                    Box3d lBox = box; lBox.Max[dim] = split + inParams.Eps;
                    Box3d rBox = box; rBox.Min[dim] = split - inParams.Eps;

                    // double progressStart = parameters.m_progress;

                    var lObjBox = Box3d.Invalid;
                    var rObjBox = Box3d.Invalid;

                    int count = objectIndexArray.Length;
                    if (lfa == null)
                    {
                        lfa = new bool[count];
                        rfa = new bool[count];
                    }

                    for (int k = 0; k < count; k++)
                    {
                        int oi = objectIndexArray[k];
                        bool l = lfa[k] = m_objectSet.ObjectIntersectsBox(oi, lBox);
                        bool r = rfa[k] = m_objectSet.ObjectIntersectsBox(oi, rBox);
                        if (l)
                        {
                            if (r) numBoth++;
                            else   numLeft++;
                        }
                        else if (r) numRight++;

                        if (inParams.BoxArray != null)
                        {
                            if (l) lObjBox.ExtendBy(inParams.BoxArray[oi]);
                            if (r) rObjBox.ExtendBy(inParams.BoxArray[oi]);
                        }

                        //if (inParams.ReportProgress && ((k & 0xff) == 0))
                        //{
                        //    parameters.m_progress += progressStep;
                        //    Report.Progress(3, parameters.m_progress);
                        //}
                    }
                    //if (inParams.ReportProgress && ((objectIndexArray.Length & 0xff) != 0))
                    //{
                    //    parameters.m_progress = progressStart + progressPass;
                    //    Report.Progress(3, parameters.m_progress);
                    //}

                    double sl = double.NaN, sr = double.NaN;

                    if (inParams.BoxArray != null)
                    {
                        lObjBox = Box.Intersection(lObjBox, lBox);
                        rObjBox = Box.Intersection(rObjBox, rBox);

                        if (lObjBox.IsValid)
                        {
                            if (rObjBox.IsValid)
                            {
                                double lMax = lObjBox.Max[dim];
                                double rMin = rObjBox.Min[dim];

                                if (lMax + 2 * inParams.Eps < rMin)
                                {
                                    sl = lMax + inParams.Eps;
                                    sr = rMin - inParams.Eps;
                                    smin = Fun.Min(smin, sl);
                                    smax = Fun.Max(smax, sr);
                                }
                                else
                                    sl = split;
                            }
                            else
                            {
                                sl = lObjBox.Max[dim] + inParams.Eps;
                                smin = Fun.Min(smin, sl);
                            }
                        }
                        else
                        {
                            if (rObjBox.IsValid)
                            {
                                sr = rObjBox.Min[dim] - inParams.Eps;
                                smax = Fun.Max(smax, sr);
                            }
                            else
                                sr = split; // should never happen
                        }

                    }
                    else
                        sl = split;

                    var lastLeft = leftFlags;
                    var lastRight = rightFlags;
                    for (var si = 0; si < 2; si++)
                    {
                        var s = si == 0 ? sl : sr;
                        if (s.IsNaN()) continue;

                        double cost;
                        var t = s - delta;
                        if (count > c_costThreshold)
                        {
                            /* ---------------------------------------------------
                                For large leafs cost can be approximated to be
                                logarithmic in the number of subobjects.
                            --------------------------------------------------- */
                            cost = (((t * (b + c) + b * c)
                                    * Fun.Log(1.0 + (double)numLeft
                                            + c_splitPenalty * (double)numBoth)) +
                                (((a - t) * (b + c) + b * c)
                                    * Fun.Log(1.0 + (double)numRight
                                            + c_splitPenalty * (double)numBoth))
                            ) * invBoxArea;
                        }
                        else
                        {
                            /* ---------------------------------------------------
                                For small leafs cost can be approximated to be
                                proportional to the number of subobjects. Thus
                                for small leafs, central splits are more strongly
                                preferred.
                            --------------------------------------------------- */
                            cost = (((t * (b + c) + b * c)
                                    * ((double)numLeft
                                            + c_splitPenalty * (double)numBoth)) +
                                (((a - t) * (b + c) + b * c)
                                    * ((double)numRight
                                            + c_splitPenalty * (double)numBoth))
                            ) * invBoxArea;
                        }

                        cost *= inParams.CostFactor[dim];

                        if (f == 0.5) cost *= (1.0 / c_midSplitPref);
                        if (dim == largestDim) cost *= (1.0 / c_majorAxisPref);

                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            bestDim = dim;
                            bestSplit = s;
                            bestLeft = numLeft + numBoth;
                            bestRight = numRight + numBoth;
                            leftFlags = lfa;
                            rightFlags = rfa;
                        }
                    }

                    // re-use last flag arrays (might be null -> second working array will be allocated)
                    if (lfa == leftFlags)
                    {
                        lfa = lastLeft;
                        rfa = lastRight;
                    }
                }
            }

            return bestDim;
        }

        #endregion  

        #region IBoundingBox3d Members

        public Box3d BoundingBox3d
        {
            get { return m_box; }
            set { m_box = value; }
        }

        #endregion

        #region IFieldCodeable Members

        /// <summary>
        /// For compact storage we use short names for all node types, and do
        /// not store any sizes and version number for the nodes.
        /// </summary>
        static readonly TypeInfo[] s_typeInfoArray = new[]
            {
                new TypeInfo("n", typeof(TypeCoder.Null), TypeInfo.Option.Active),
                new TypeInfo("i", typeof(KdInner),      TypeInfo.Option.None),
                new TypeInfo("b", typeof(KdInnerBoth),  TypeInfo.Option.None),
                new TypeInfo("x", typeof(KdInnerBothX), TypeInfo.Option.None),
                new TypeInfo("y", typeof(KdInnerBothY), TypeInfo.Option.None),
                new TypeInfo("z", typeof(KdInnerBothZ), TypeInfo.Option.None),
                new TypeInfo("l", typeof(KdInnerLeft),  TypeInfo.Option.None),
                new TypeInfo("r", typeof(KdInnerRight), TypeInfo.Option.None),
                new TypeInfo("o", typeof(KdLeaf),       TypeInfo.Option.None),
                new TypeInfo("s", typeof(KdLeafSlice),  TypeInfo.Option.None),
                new TypeInfo("f", typeof(KdFlat),       TypeInfo.Option.None),
            };

        static readonly TypeInfo[] s_floatTypeInfoArray = new[]
            {
                new TypeInfo("n", typeof(TypeCoder.Null), TypeInfo.Option.Active),
                new TypeInfo("i", typeof(KdFloatInner),      TypeInfo.Option.None),
                new TypeInfo("b", typeof(KdFloatInnerBoth),  TypeInfo.Option.None),
                new TypeInfo("x", typeof(KdFloatInnerBothX), TypeInfo.Option.None),
                new TypeInfo("y", typeof(KdFloatInnerBothY), TypeInfo.Option.None),
                new TypeInfo("z", typeof(KdFloatInnerBothZ), TypeInfo.Option.None),
                new TypeInfo("l", typeof(KdFloatInnerLeft),  TypeInfo.Option.None),
                new TypeInfo("r", typeof(KdFloatInnerRight), TypeInfo.Option.None),
                new TypeInfo("o", typeof(KdLeaf),       TypeInfo.Option.None),
            };

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "Tree",
                        (c,o) =>
                        {
                            // deactivating referencs avoids creating large
                            // unused tables during coding of pure trees
                            c.Add(TypeCoder.Default.NoReference);
                            if (c.StreamVersion == 0)
                            {
                                // read old nodes with float spilt values
                                c.Add(s_floatTypeInfoArray);
                                KdNode streamTree = null;
                                c.CodeT(ref streamTree);
                                // and convert them to nodes with double split values
                                ((KdIntersectionTree)o).Tree = streamTree.ToDouble();
                                c.Del(s_floatTypeInfoArray);
                            }
                            else
                            {
                                c.Add(s_typeInfoArray);
                                c.CodeT(ref ((KdIntersectionTree)o).Tree);
                                c.Del(s_typeInfoArray);
                            }
                            c.Del(TypeCoder.Default.NoReference);
                        } ),
                new FieldCoder(1, "Box",
                        (c,o) => c.CodeBox3d(ref ((KdIntersectionTree)o).m_box) ),
                new FieldCoder(2, "ObjectSet",
                        (c,o) => c.CodeT(ref ((KdIntersectionTree)o).m_objectSet) ),
                new FieldCoder(3, "LeafSet",
                        (c,o) => c.CodeT(ref ((KdIntersectionTree)o).m_leafSet) ),
            };
        }

        #endregion
    }

    #region KdQueryPoint

    public struct KdQueryPoint
    {
        public V3d Point;
        public IIntersectableObjectSet ObjectSet;
        public ObjectClosestPoint ClosestPoint;
        public Func<IIntersectableObjectSet, int, bool> ObjectFilter;
        public Func<IIntersectableObjectSet, int, int, ObjectClosestPoint, bool> PointFilter;
    }

    #endregion

    #region KdIntersectionRay

    public struct KdIntersectionRay
    {
        public FastRay3d FastRay;
        public IIntersectableObjectSet ObjectSet;
        public ObjectRayHit Hit;
        public Func<IIntersectableObjectSet, int, bool> ObjectFilter;
        public Func<IIntersectableObjectSet, int, int, RayHit3d, bool> HitFilter;
    }

    #endregion

    #region Helper Data Structures

    public struct TP3d
    {
        public double T;
        public V3d P;
    }


    internal class Counts
    {
        public int InnerCount;
        public int ObjectCount;
    }

    internal struct AxisIndex
    {
        public AxisIndex(short axis, int index) { Axis = axis; Index = index; }

        public static AxisIndex Null = new(0, -1);

        public short Axis;
        public int Index;
    }

    #endregion

    #region KdNode

    public abstract class KdNode
    {
        public KdNode() { }

        public abstract bool Intersect(
                ref KdIntersectionRay ray,
                ref TP3d tpmin, ref TP3d tpmax
                );

        public abstract bool ClosestPoint(
                ref KdQueryPoint query, ref V3d pmin, ref V3d pmax
                );

        public abstract KdNode Intersect(
                ref Box3d box, Box.Flags outsideFlags);

        public virtual KdNode Intersect(
                ref FastHull3d hull, Hull.Flags32 outsideFlags)
            {
                throw new NotImplementedException();
            }

        public abstract IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector);

        public abstract IEnumerable<KdLeaf> Leafs();

        public virtual KdNode ToDouble()
        {
            return this;
        }

        internal virtual void Count(Counts counts) { }

        internal virtual AxisIndex Flatten(Counts counts, KdFlat flat) { return AxisIndex.Null; }

        internal static bool SphereDoesNotTouchSplitFace(
                int axis, double value,
                V3d queryPoint, double distanceSquared,
                V3d pmin, V3d pmax)
            {
                int a1 = (axis + 1) % 3;
                int a2 = (axis + 2) % 3;
                if (queryPoint[a1] < pmin[a1])
                {
                    if (queryPoint[a2] < pmin[a2])
                    {
                        if ((queryPoint[a1] - pmin[a1]).Square()
                            + (queryPoint[a2] - pmin[a2]).Square()
                            + (queryPoint[axis] - value).Square()
                            > distanceSquared)
                            return true;
                    }
                    else if (queryPoint[a2] > pmax[a2])
                    {
                        if ((queryPoint[a1] - pmin[a1]).Square()
                            + (queryPoint[a2] - pmax[a2]).Square()
                            + (queryPoint[axis] - value).Square()
                            > distanceSquared)
                            return true;
                    }
                    else
                    {
                        if ((queryPoint[a1] - pmin[a1]).Square()
                            + (queryPoint[axis] - value).Square()
                            > distanceSquared)
                            return true;
                    }
                }
                else if (queryPoint[a1] > pmax[a1])
                {
                    if (queryPoint[a2] < pmin[a2])
                    {
                        if ((queryPoint[a1] - pmax[a1]).Square()
                            + (queryPoint[a2] - pmin[a2]).Square()
                            + (queryPoint[axis] - value).Square()
                            > distanceSquared)
                            return true;
                    }
                    else if (queryPoint[a2] > pmax[a2])
                    {
                        if ((queryPoint[a1] - pmax[a1]).Square()
                            + (queryPoint[a2] - pmax[a2]).Square()
                            + (queryPoint[axis] - value).Square()
                            > distanceSquared)
                            return true;
                    }
                    else
                    {
                        if ((queryPoint[a1] - pmax[a1]).Square()
                            + (queryPoint[axis] - value).Square()
                            > distanceSquared)
                            return true;
                    }
                }
                else
                {
                    if (queryPoint[a2] < pmin[a2])
                    {
                        if ((queryPoint[a2] - pmin[a2]).Square()
                            + (queryPoint[axis] - value).Square()
                            > distanceSquared)
                            return true;
                    }
                    else if (queryPoint[a2] > pmax[a2])
                    {
                        if ((queryPoint[a2] - pmax[a2]).Square()
                            + (queryPoint[axis] - value).Square()
                            > distanceSquared)
                            return true;
                    }
                    else
                    {
                        // inside face, distance check was enough
                    }
                }
                return false;
            }
    }

    #endregion

    #region KdFlat

    public class KdFlat : KdNode, IFieldCodeable
    {
        internal short m_rootAxis;   // -3 | -2 | -1 | 0 | count
        internal int m_rootIndex;

        internal double[] m_splitArray;
        internal int[] m_nodeArray; //     triples: 2 x counts+axes, leftIndex, rightIndex
        internal int[] m_indexArray;

        private static short LeftAxis(int axes) { return (short)(axes & 0xffff); }
        private static short RightAxis(int axes) { return (short)(axes >> 16); }
        internal static int Axes(short leftAxis, short rightAxis)
        {
            return (int)(ushort)leftAxis | ((int)(ushort)rightAxis << 16);
        }

        public KdFlat()
        {
            m_rootAxis = 0; m_rootIndex = 0;
            m_splitArray = null; m_nodeArray = null; m_indexArray = null;
        }

        public KdFlat(short rootAxis, int rootIndex, double[] splitArray, int[] nodeArray, int[] indexArray)
        {
            m_rootAxis = rootAxis; m_rootIndex = rootIndex;
            m_splitArray = splitArray; m_nodeArray = nodeArray; m_indexArray = indexArray;
        }

        public bool Intersect(short axis, int index,
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            if (axis > 0)
                return kdRay.ObjectSet.ObjectsIntersectRay(
                        m_indexArray, index, axis, kdRay.FastRay,
                        kdRay.ObjectFilter, kdRay.HitFilter,
                        tpmin.T, tpmax.T, ref kdRay.Hit);
            axis += 3;
            var value = m_splitArray[index];
            index *= 3;
            var axes = m_nodeArray[index];
            short leftAxis = LeftAxis(axes), rightAxis = RightAxis(axes);
            if (tpmin.P[axis] < value)
            {
                if (tpmax.P[axis] < value)
                    return (leftAxis != 0 && Intersect(leftAxis, m_nodeArray[index + 1], ref kdRay, ref tpmin, ref tpmax));
                if (tpmax.P[axis] > value)
                {
                    TP3d tp;
                    tp.T = (value - kdRay.FastRay.Ray.Origin[axis]) * kdRay.FastRay.InvDir[axis];
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (leftAxis != 0
                            && Intersect(leftAxis, m_nodeArray[index + 1], ref kdRay, ref tpmin, ref tp))
                        || (rightAxis != 0
                            && Intersect(rightAxis, m_nodeArray[index + 2], ref kdRay, ref tp, ref tpmax));
                }
                return (leftAxis != 0
                        && Intersect(leftAxis, m_nodeArray[index + 1], ref kdRay, ref tpmin, ref tpmax))
                    || (rightAxis != 0
                        && Intersect(rightAxis, m_nodeArray[index + 2], ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmin.P[axis] > value)
            {
                if (tpmax.P[axis] > value)
                    return (rightAxis != 0
                        && Intersect(rightAxis, m_nodeArray[index + 2], ref kdRay, ref tpmin, ref tpmax));
                if (tpmax.P[axis] < value)
                {
                    TP3d tp;
                    tp.T = (value - kdRay.FastRay.Ray.Origin[axis])
                               * kdRay.FastRay.InvDir[axis];
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (rightAxis != 0
                            && Intersect(rightAxis, m_nodeArray[index + 2], ref kdRay, ref tpmin, ref tp))
                        || (leftAxis != 0
                            && Intersect(leftAxis, m_nodeArray[index + 1], ref kdRay, ref tp, ref tpmax));
                }
                return (rightAxis != 0
                        && Intersect(rightAxis, m_nodeArray[index + 2], ref kdRay, ref tpmin, ref tpmax))
                    || (leftAxis != 0
                        && Intersect(leftAxis, m_nodeArray[index + 1], ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmax.P[axis] < value)
                return (rightAxis != 0
                        && Intersect(rightAxis, m_nodeArray[index + 2], ref kdRay, ref tpmin, ref tpmin))
                    || (leftAxis != 0
                        && Intersect(leftAxis, m_nodeArray[index + 1], ref kdRay, ref tpmin, ref tpmax));
            if (tpmax.P[axis] > value)
                return (leftAxis != 0
                        && Intersect(leftAxis, m_nodeArray[index + 1], ref kdRay, ref tpmin, ref tpmin))
                    || (rightAxis != 0
                        && Intersect(rightAxis, m_nodeArray[index + 2], ref kdRay, ref tpmin, ref tpmax));
            return (leftAxis != 0
                    && Intersect(leftAxis, m_nodeArray[index + 1], ref kdRay, ref tpmin, ref tpmax))
                // the bitwise or is correct, since we may not exit early here
                | (rightAxis != 0
                    && Intersect(rightAxis, m_nodeArray[index + 2], ref kdRay, ref tpmin, ref tpmax));
        }

        public override bool Intersect(
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            return Intersect(m_rootAxis, m_rootIndex, ref kdRay, ref tpmin, ref tpmax);
        }


        public bool ClosestPoint(short axis, int index,
                ref KdQueryPoint query, ref V3d pmin, ref V3d pmax)
        {
            if (axis > 0)
                return query.ObjectSet.ClosestPoint(
                        m_indexArray, index, axis, query.Point, // TODO: range
                        query.ObjectFilter, query.PointFilter,
                        ref query.ClosestPoint);
            axis += 3;
            var value = m_splitArray[index];
            index *= 3;
            var axes = m_nodeArray[index];
            short leftAxis = LeftAxis(axes), rightAxis = RightAxis(axes);
            bool result = false;
            if (query.Point[axis] < value)
            {
                if (leftAxis != 0)
                {
                    V3d pmid = pmax; pmid[axis] = value;
                    result |= ClosestPoint(leftAxis, m_nodeArray[index + 1], ref query, ref pmin, ref pmid);
                }
                if (query.Point[axis] + query.ClosestPoint.Distance < value)
                    return result;
                if (SphereDoesNotTouchSplitFace(axis, value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                if (rightAxis != 0)
                {
                    V3d pmid = pmin; pmid[axis] = value;
                    result |= ClosestPoint(rightAxis, m_nodeArray[index + 2], ref query, ref pmid, ref pmax);
                }
            }
            else
            {
                if (rightAxis != 0)
                {
                    V3d pmid = pmin; pmid[axis] = value;
                    result |= ClosestPoint(rightAxis, m_nodeArray[index + 2], ref query, ref pmid, ref pmax);
                }
                if (query.Point[axis] - query.ClosestPoint.Distance > value)
                    return result;
                if (SphereDoesNotTouchSplitFace(axis, value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                if (leftAxis != 0)
                {
                    V3d pmid = pmax; pmid[axis] = value;
                    result |= ClosestPoint(leftAxis, m_nodeArray[index + 1], ref query, ref pmin, ref pmid);
                }
            }
            return result;
        }

        public override bool ClosestPoint(ref KdQueryPoint query, ref V3d pmin, ref V3d pmax)
        {
            return ClosestPoint(m_rootAxis, m_rootIndex, ref query, ref pmin, ref pmax);
        }

        public KdNode Intersect(short axis, int index, ref Box3d box, Box.Flags outsideFlags)
        {
            if (outsideFlags == Box.Flags.All) return new KdNodeRef(this);

            if (axis > 0)
                return new KdLeafSlice(m_indexArray, index, axis);
            axis += 3;
            var value = m_splitArray[index];
            index *= 3;
            var axes = m_nodeArray[index];
            short leftAxis = LeftAxis(axes), rightAxis = RightAxis(axes);

            if (box.Min[axis] > value)
                return rightAxis != 0
                    ? Intersect(rightAxis, m_nodeArray[index + 2], ref box, outsideFlags)
                    : null;

            if (box.Max[axis] < value)
                return leftAxis != 0
                    ? Intersect(leftAxis, m_nodeArray[index + 1], ref box, outsideFlags)
                    : null;

            var left = leftAxis != 0
                ? Intersect(leftAxis, m_nodeArray[index + 1], ref box,
                        outsideFlags | (Box.Flags)((int)Box.Flags.MaxX << (int)axis))
                : null;

            var right = rightAxis != 0
                ? Intersect(rightAxis, m_nodeArray[index + 2], ref box,
                        outsideFlags | (Box.Flags)((int)Box.Flags.MinX << (int)axis))
                : null;

            if (left == null) return right;
            if (right == null) return left;

            return new KdInner((byte)axis, value, left, right);
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outsideFlags)
        {
            return Intersect(m_rootAxis, m_rootIndex, ref box, outsideFlags);
        }

        public IEnumerable<int> Indices(short axis, int index,
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            if (axis > 0)
            {
                if (borderSelector != null)
                {
                    foreach (var i in borderSelector(m_indexArray, index, axis))
                        yield return i;
                    yield break;
                }
                foreach (var i in interiorSelector(m_indexArray, index, axis))
                    yield return i;
                yield break;
            }
            var value = m_splitArray[index];
            index *= 3;
            var axes = m_nodeArray[index];
            short leftAxis = LeftAxis(axes), rightAxis = RightAxis(axes);
            if (leftAxis != 0)
                foreach (var i in Indices(leftAxis, m_nodeArray[index + 1], interiorSelector, borderSelector))
                    yield return i;
            if (rightAxis != 0)
                foreach (var i in Indices(rightAxis, m_nodeArray[index + 2], interiorSelector, borderSelector))
                    yield return i;
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            return Indices(m_rootAxis, m_rootIndex, interiorSelector, borderSelector);
        }

        internal void Count(short axis, int index, Counts counts)
        {
            if (axis > 0)
            {
                counts.ObjectCount += axis;
                return;
            }
            index *= 3;
            var axes = m_nodeArray[index];
            short leftAxis = LeftAxis(axes), rightAxis = RightAxis(axes);

            counts.InnerCount += 1;

            if (leftAxis != 0) Count(leftAxis, m_nodeArray[index + 1], counts);
            if (rightAxis != 0) Count(rightAxis, m_nodeArray[index + 2], counts);
        }

        internal override void Count(Counts counts)
        {
            Count(m_rootAxis, m_rootIndex, counts);
        }

        internal AxisIndex Flatten(short axis, int index, Counts counts, KdFlat flat)
        {
            if (axis > 0)
            {
                var newIndex = counts.ObjectCount;
                counts.ObjectCount = index + axis;
                m_indexArray.CopyTo(index, axis, flat.m_indexArray, newIndex);
                return new AxisIndex(axis, newIndex);
            }
            else
            {
                var value = m_splitArray[index];
                index *= 3;
                var axes = m_nodeArray[index];
                short leftAxis = LeftAxis(axes), rightAxis = RightAxis(axes);

                var newIndex = counts.InnerCount++;
                var newIndex3 = newIndex * 3;

                var leftAxisIndex =
                        leftAxis != 0 ? Flatten(leftAxis, m_nodeArray[index + 1], counts, flat)
                                         : AxisIndex.Null;

                var rightAxisIndex =
                        rightAxis != 0 ? Flatten(rightAxis, m_nodeArray[index + 2], counts, flat)
                                          : AxisIndex.Null;

                flat.m_splitArray[newIndex] = value;
                m_nodeArray[newIndex3] = Axes(leftAxisIndex.Axis, rightAxisIndex.Axis);
                m_nodeArray[newIndex3 + 1] = leftAxisIndex.Index;
                m_nodeArray[newIndex3 + 2] = rightAxisIndex.Index;
                return new AxisIndex(axis, newIndex);
            }
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            return Flatten(m_rootAxis, m_rootIndex, counts, flat);
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "rootAxis", (c,o) => c.CodeShort(ref ((KdFlat)o).m_rootAxis) ),
                new FieldCoder(1, "rootIndex", (c,o) => c.CodeInt(ref ((KdFlat)o).m_rootIndex) ),
                new FieldCoder(2, "splitArray", (c,o) => c.CodeDoubleArray(ref ((KdFlat)o).m_splitArray) ),
                new FieldCoder(3, "nodeArray", (c,o) => c.CodeIntArray(ref ((KdFlat)o).m_nodeArray) ),
                new FieldCoder(4, "indexArray", (c,o) => c.CodeIntArray(ref ((KdFlat)o).m_indexArray) ),
            };
        }
    }

    #endregion

    #region KdInner

    public class KdInner : KdNode, IFieldCodeable
    {
        public byte m_axis;
        public double m_value;
        public KdNode m_left;
        public KdNode m_right;

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "axis", (c,o) => c.CodeByte(ref ((KdInner)o).m_axis) ),
                new FieldCoder(1, "value", (c,o) => c.CodeDouble(ref ((KdInner)o).m_value) ),
                new FieldCoder(2, "left", (c,o) => c.CodeT(ref ((KdInner)o).m_left) ),
                new FieldCoder(3, "right", (c,o) => c.CodeT(ref ((KdInner)o).m_right) ),
            };
        }

        public KdInner()
        {
            m_axis = 0; m_value = 0.0f; m_left = null; m_right = null;
        }

        public KdInner(byte axis, double value, KdNode left, KdNode right)
        {
            m_axis = axis; m_value = value; m_left = left; m_right = right;
        }

        public override bool Intersect(
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            if (tpmin.P[m_axis] < m_value)
            {
                if (tpmax.P[m_axis] < m_value)
                    return (m_left != null
                        && m_left.Intersect(ref kdRay, ref tpmin, ref tpmax));
                if (tpmax.P[m_axis] > m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin[m_axis])
                               * kdRay.FastRay.InvDir[m_axis];
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (m_left != null
                            && m_left.Intersect(ref kdRay, ref tpmin, ref tp))
                        || (m_right != null
                            && m_right.Intersect(ref kdRay, ref tp, ref tpmax));
                }
                return (m_left != null
                        && m_left.Intersect(ref kdRay, ref tpmin, ref tpmax))
                    || (m_right != null
                        && m_right.Intersect(ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmin.P[m_axis] > m_value)
            {
                if (tpmax.P[m_axis] > m_value)
                    return (m_right != null
                        && m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
                if (tpmax.P[m_axis] < m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin[m_axis])
                               * kdRay.FastRay.InvDir[m_axis];
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (m_right != null
                            && m_right.Intersect(ref kdRay, ref tpmin, ref tp))
                        || (m_left != null
                            && m_left.Intersect(ref kdRay, ref tp, ref tpmax));
                }
                return (m_right != null
                        && m_right.Intersect(ref kdRay, ref tpmin, ref tpmax))
                    || (m_left != null
                        && m_left.Intersect(ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmax.P[m_axis] < m_value)
                return (m_right != null
                        && m_right.Intersect(ref kdRay, ref tpmin, ref tpmin))
                    || (m_left != null
                        && m_left.Intersect(ref kdRay, ref tpmin, ref tpmax));
            if (tpmax.P[m_axis] > m_value)
                return (m_left != null
                        && m_left.Intersect(ref kdRay, ref tpmin, ref tpmin))
                    || (m_right != null
                        && m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
            return (m_left != null
                    && m_left.Intersect(ref kdRay, ref tpmin, ref tpmax))
                // the bitwise or is correct, since we may not exit early here
                | (m_right != null
                    && m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outFlags)
        {
            if (outFlags == Box.Flags.All) return new KdNodeRef(this);

            if (box.Min[m_axis] > m_value)
                return m_right?.Intersect(ref box, outFlags);

            if (box.Max[m_axis] < m_value)
                return m_left?.Intersect(ref box, outFlags);

            var left = m_left?.Intersect(ref box,
                        outFlags | (Box.Flags)((int)Box.Flags.MaxX << (int)m_axis));

            var right = m_right?.Intersect(ref box,
                        outFlags | (Box.Flags)((int)Box.Flags.MinX << (int)m_axis));

            if (left == null) return right;
            if (right == null) return left;

            return new KdInner(m_axis, m_value, left, right);
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            if (m_left != null)
            {
                if (m_right != null)
                    return m_left.Indices(interiorSelector, borderSelector)
                            .Concat(m_right.Indices(interiorSelector, borderSelector));
                else
                    return m_left.Indices(interiorSelector, borderSelector);
            }
            else
            {
                if (m_right != null)
                    return m_right.Indices(interiorSelector, borderSelector);
                else
                    return Enumerable.Empty<int>();
            }
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            if (m_left != null)
            {
                if (m_right != null)
                    return m_left.Leafs().Concat(m_right.Leafs());
                else
                    return m_left.Leafs();
            }
            else
            {
                if (m_right != null)
                    return m_right.Leafs();
                else
                    return Enumerable.Empty<KdLeaf>();
            }
        }

        public override bool ClosestPoint(
            ref KdQueryPoint query, ref V3d pmin, ref V3d pmax
            )
        {
            bool result = false;
            if (query.Point[m_axis] < m_value)
            {
                if (m_left != null)
                {
                    V3d pmid = pmax; pmid[m_axis] = m_value;
                    result |= m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
                if (query.Point[m_axis] + query.ClosestPoint.Distance < m_value)
                    return result;
                if (SphereDoesNotTouchSplitFace(m_axis, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                if (m_right != null)
                {
                    V3d pmid = pmin; pmid[m_axis] = m_value;
                    result |= m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
            }
            else
            {
                if (m_right != null)
                {
                    V3d pmid = pmin; pmid[m_axis] = m_value;
                    result |= m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
                if (query.Point[m_axis] - query.ClosestPoint.Distance > m_value)
                    return result;
                if (SphereDoesNotTouchSplitFace(m_axis, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                if (m_left != null)
                {
                    V3d pmid = pmax; pmid[m_axis] = m_value;
                    result |= m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
            }
            return result;
        }

        internal override void Count(Counts counts)
        {
            counts.InnerCount += 1;
            if (m_left != null) m_left.Count(counts);
            if (m_right != null) m_right.Count(counts);
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            var index = counts.InnerCount++;
            var axis = (short)(m_axis - 3);

            flat.m_splitArray[index] = m_value;

            var leftAxisIndex = m_left != null ? m_left.Flatten(counts, flat) : AxisIndex.Null;
            var rightAxisIndex = m_right != null ? m_right.Flatten(counts, flat) : AxisIndex.Null;

            var index3 = index * 3;
            flat.m_nodeArray[index3] = KdFlat.Axes(leftAxisIndex.Axis, rightAxisIndex.Axis);
            flat.m_nodeArray[index3 + 1] = leftAxisIndex.Index;
            flat.m_nodeArray[index3 + 2] = rightAxisIndex.Index;

            return new AxisIndex(axis, index);
        }
    }

    #endregion

    #region KdInnerBoth

    internal class KdInnerBoth : KdNode, IFieldCodeable
    {
        public byte m_axis;
        public double m_value;
        public KdNode m_left;
        public KdNode m_right;

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "axis", (c,o) => c.CodeByte(ref ((KdInnerBoth)o).m_axis) ),
                new FieldCoder(1, "value", (c,o) => c.CodeDouble(ref ((KdInnerBoth)o).m_value) ),
                new FieldCoder(2, "left", (c,o) => c.CodeT(ref ((KdInnerBoth)o).m_left) ),
                new FieldCoder(3, "right", (c,o) => c.CodeT(ref ((KdInnerBoth)o).m_right) ),
            };
        }

        public KdInnerBoth()
        {
            m_axis = 0; m_value = 0.0f; m_left = null; m_right = null;
        }

        public KdInnerBoth(byte axis, double value, KdNode left, KdNode right)
        {
            m_axis = axis; m_value = value; m_left = left; m_right = right;
        }

        public override bool Intersect(
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            if (tpmin.P[m_axis] < m_value)
            {
                if (tpmax.P[m_axis] < m_value)
                    return m_left.Intersect(ref kdRay, ref tpmin, ref tpmax);
                if (tpmax.P[m_axis] > m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin[m_axis])
                               * kdRay.FastRay.InvDir[m_axis];
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (m_left.Intersect(ref kdRay, ref tpmin, ref tp)
                            || m_right.Intersect(ref kdRay, ref tp, ref tpmax));
                }
                return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmax)
                        || m_right.Intersect(ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmin.P[m_axis] > m_value)
            {
                if (tpmax.P[m_axis] > m_value)
                    return m_right.Intersect(ref kdRay, ref tpmin, ref tpmax);
                if (tpmax.P[m_axis] < m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin[m_axis])
                               * kdRay.FastRay.InvDir[m_axis];
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (m_right.Intersect(ref kdRay, ref tpmin, ref tp)
                            || m_left.Intersect(ref kdRay, ref tp, ref tpmax));
                }
                return (m_right.Intersect(ref kdRay, ref tpmin, ref tpmax)
                        ||  m_left.Intersect(ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmax.P[m_axis] < m_value)
                return (m_right.Intersect(ref kdRay, ref tpmin, ref tpmin)
                        || m_left.Intersect(ref kdRay, ref tpmin, ref tpmax));
            if (tpmax.P[m_axis] > m_value)
                return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmin)
                        || m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
            return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmax)
                    // the bitwise or is correct, since we may not exit early here
                    | m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outFlags)
        {
            if (outFlags == Box.Flags.All) return new KdNodeRef(this);

            if (box.Min[m_axis] > m_value)
                return m_right.Intersect(ref box, outFlags);

            if (box.Max[m_axis] < m_value)
                return m_left.Intersect(ref box, outFlags);

            var left = m_left.Intersect(ref box,
                        outFlags | (Box.Flags)((int)Box.Flags.MaxX << (int)m_axis));

            var right = m_right.Intersect(ref box,
                        outFlags | (Box.Flags)((int)Box.Flags.MinX << (int)m_axis));

            if (left == null) return right;
            if (right == null) return left;

            return new KdInnerBoth(m_axis, m_value, left, right);
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            return m_left.Indices(interiorSelector, borderSelector)
                    .Concat(m_right.Indices(interiorSelector, borderSelector));
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            return m_left.Leafs().Concat(m_right.Leafs());
        }

        public override bool ClosestPoint(
            ref KdQueryPoint query, ref V3d pmin, ref V3d pmax
            )
        {
            bool result = false;
            if (query.Point[m_axis] < m_value)
            {
                {
                    V3d pmid = pmax; pmid[m_axis] = m_value;
                    result |= m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
                if (query.Point[m_axis] + query.ClosestPoint.Distance < m_value)
                    return result;
                if (SphereDoesNotTouchSplitFace(m_axis, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                {
                    V3d pmid = pmin; pmid[m_axis] = m_value;
                    result |= m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
            }
            else
            {
                {
                    V3d pmid = pmin; pmid[m_axis] = m_value;
                    result |= m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
                if (query.Point[m_axis] - query.ClosestPoint.Distance > m_value)
                    return result;
                if (SphereDoesNotTouchSplitFace(m_axis, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                {
                    V3d pmid = pmax; pmid[m_axis] = m_value;
                    result |= m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
            }
            return result;
        }

        internal override void Count(Counts counts)
        {
            counts.InnerCount += 1;
            m_left.Count(counts);
            m_right.Count(counts);
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            var index = counts.InnerCount++;
            var axis = (short)(m_axis - 3);

            flat.m_splitArray[index] = m_value;

            var leftAxisIndex = m_left.Flatten(counts, flat);
            var rightAxisIndex = m_right.Flatten(counts, flat);

            var index3 = index * 3;
            flat.m_nodeArray[index3] = KdFlat.Axes(leftAxisIndex.Axis, rightAxisIndex.Axis);
            flat.m_nodeArray[index3 + 1] = leftAxisIndex.Index;
            flat.m_nodeArray[index3 + 2] = rightAxisIndex.Index;

            return new AxisIndex(axis, index);
        }

    }

    #endregion

    #region KdInnerBothX

    internal class KdInnerBothX : KdNode, IFieldCodeable
    {
        public double m_value;
        public KdNode m_left;
        public KdNode m_right;

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "value", (c,o) => c.CodeDouble(ref ((KdInnerBothX)o).m_value) ),
                new FieldCoder(1, "left", (c,o) => c.CodeT(ref ((KdInnerBothX)o).m_left) ),
                new FieldCoder(2, "right", (c,o) => c.CodeT(ref ((KdInnerBothX)o).m_right) ),
            };
        }

        public KdInnerBothX()
        {
            m_value = 0.0f; m_left = null; m_right = null;
        }

        public KdInnerBothX(double value, KdNode left, KdNode right)
        {
            m_value = value; m_left = left; m_right = right;
        }

        public override bool Intersect(
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            if (tpmin.P.X < m_value)
            {
                if (tpmax.P.X < m_value)
                    return m_left.Intersect(ref kdRay, ref tpmin, ref tpmax);
                if (tpmax.P.X > m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin.X)
                               * kdRay.FastRay.InvDir.X;
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (m_left.Intersect(ref kdRay, ref tpmin, ref tp)
                            || m_right.Intersect(ref kdRay, ref tp, ref tpmax));
                }
                return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmax)
                        || m_right.Intersect(ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmin.P.X > m_value)
            {
                if (tpmax.P.X > m_value)
                    return m_right.Intersect(ref kdRay, ref tpmin, ref tpmax);
                if (tpmax.P.X < m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin.X)
                               * kdRay.FastRay.InvDir.X;
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (m_right.Intersect(ref kdRay, ref tpmin, ref tp)
                            || m_left.Intersect(ref kdRay, ref tp, ref tpmax));
                }
                return (m_right.Intersect(ref kdRay, ref tpmin, ref tpmax)
                        || m_left.Intersect(ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmax.P.X < m_value)
                return (m_right.Intersect(ref kdRay, ref tpmin, ref tpmin)
                        || m_left.Intersect(ref kdRay, ref tpmin, ref tpmax));
            if (tpmax.P.X > m_value)
                return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmin)
                        || m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
            return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmax)
                    // the bitwise or is correct, since we may not exit early here
                    | m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outFlags)
        {
            if (outFlags == Box.Flags.All) return new KdNodeRef(this);

            if (box.Min.X > m_value)
                return m_right.Intersect(ref box, outFlags);

            if (box.Max.X < m_value)
                return m_left.Intersect(ref box, outFlags);

            var left = m_left.Intersect(ref box, outFlags | Box.Flags.MaxX);
            var right = m_right.Intersect(ref box, outFlags | Box.Flags.MinX);

            if (left == null) return right;
            if (right == null) return left;

            return new KdInnerBothX(m_value, left, right);
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            return m_left.Indices(interiorSelector, borderSelector)
                    .Concat(m_right.Indices(interiorSelector, borderSelector));
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            return m_left.Leafs().Concat(m_right.Leafs());
        }

        public override bool ClosestPoint(
            ref KdQueryPoint query, ref V3d pmin, ref V3d pmax
            )
        {
            bool result = false;
            if (query.Point.X < m_value)
            {
                {
                    V3d pmid = pmax; pmid.X = m_value;
                    result |= m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
                if (query.Point.X + query.ClosestPoint.Distance < m_value)
                    return result;
                if (SphereDoesNotTouchSplitFace(0, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                {
                    V3d pmid = pmin; pmid.X = m_value;
                    result |= m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
            }
            else
            {
                {
                    V3d pmid = pmin; pmid.X = m_value;
                    result |= m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
                if (query.Point.X - query.ClosestPoint.Distance > m_value)
                    return result;
                if (SphereDoesNotTouchSplitFace(0, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                {
                    V3d pmid = pmax; pmid.X = m_value;
                    result |= m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
            }
            return result;
        }

        internal override void Count(Counts counts)
        {
            counts.InnerCount += 1;
            m_left.Count(counts);
            m_right.Count(counts);
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            var index = counts.InnerCount++;
            short axis = -3;

            flat.m_splitArray[index] = m_value;

            var leftAxisIndex = m_left.Flatten(counts, flat);
            var rightAxisIndex = m_right.Flatten(counts, flat);

            var index3 = index * 3;
            flat.m_nodeArray[index3] = KdFlat.Axes(leftAxisIndex.Axis, rightAxisIndex.Axis);
            flat.m_nodeArray[index3 + 1] = leftAxisIndex.Index;
            flat.m_nodeArray[index3 + 2] = rightAxisIndex.Index;

            return new AxisIndex(axis, index);
        }
    }

    #endregion

    #region KdInnerBothY

    internal class KdInnerBothY : KdNode, IFieldCodeable
    {
        public double m_value;
        public KdNode m_left;
        public KdNode m_right;

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "value", (c,o) => c.CodeDouble(ref ((KdInnerBothY)o).m_value) ),
                new FieldCoder(1, "left", (c,o) => c.CodeT(ref ((KdInnerBothY)o).m_left) ),
                new FieldCoder(2, "right", (c,o) => c.CodeT(ref ((KdInnerBothY)o).m_right) ),
            };
        }

        public KdInnerBothY()
        {
            m_value = 0.0f; m_left = null; m_right = null;
        }

        public KdInnerBothY(double value, KdNode left, KdNode right)
        {
            m_value = value; m_left = left; m_right = right;
        }

        public override bool Intersect(
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            if (tpmin.P.Y < m_value)
            {
                if (tpmax.P.Y < m_value)
                    return m_left.Intersect(ref kdRay, ref tpmin, ref tpmax);
                if (tpmax.P.Y > m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin.Y)
                               * kdRay.FastRay.InvDir.Y;
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (m_left.Intersect(ref kdRay, ref tpmin, ref tp)
                            || m_right.Intersect(ref kdRay, ref tp, ref tpmax));
                }
                return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmax)
                        || m_right.Intersect(ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmin.P.Y > m_value)
            {
                if (tpmax.P.Y > m_value)
                    return m_right.Intersect(ref kdRay, ref tpmin, ref tpmax);
                if (tpmax.P.Y < m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin.Y)
                               * kdRay.FastRay.InvDir.Y;
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (m_right.Intersect(ref kdRay, ref tpmin, ref tp)
                            || m_left.Intersect(ref kdRay, ref tp, ref tpmax));
                }
                return (m_right.Intersect(ref kdRay, ref tpmin, ref tpmax)
                        || m_left.Intersect(ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmax.P.Y < m_value)
                return (m_right.Intersect(ref kdRay, ref tpmin, ref tpmin)
                        || m_left.Intersect(ref kdRay, ref tpmin, ref tpmax));
            if (tpmax.P.Y > m_value)
                return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmin)
                        || m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
            return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmax)
                    // the bitwise or is correct, since we may not exit early here
                    | m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outFlags)
        {
            if (outFlags == Box.Flags.All) return new KdNodeRef(this);

            if (box.Min.Y > m_value)
                return m_right.Intersect(ref box, outFlags);

            if (box.Max.Y < m_value)
                return m_left.Intersect(ref box, outFlags);

            var left = m_left.Intersect(ref box, outFlags | Box.Flags.MaxY);
            var right = m_right.Intersect(ref box, outFlags | Box.Flags.MinY);

            if (left == null) return right;
            if (right == null) return left;

            return new KdInnerBothY(m_value, left, right);
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            return m_left.Indices(interiorSelector, borderSelector)
                    .Concat(m_right.Indices(interiorSelector, borderSelector));
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            return m_left.Leafs().Concat(m_right.Leafs());
        }

        public override bool ClosestPoint(
            ref KdQueryPoint query, ref V3d pmin, ref V3d pmax
            )
        {
            bool result = false;
            if (query.Point.Y < m_value)
            {
                {
                    V3d pmid = pmax; pmid.Y = m_value;
                    result |= m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
                if (query.Point.Y + query.ClosestPoint.Distance < m_value)
                    return result;
                if (SphereDoesNotTouchSplitFace(1, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                {
                    V3d pmid = pmin; pmid.Y = m_value;
                    result |= m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
            }
            else
            {
                {
                    V3d pmid = pmin; pmid.Y = m_value;
                    result |= m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
                if (query.Point.Y - query.ClosestPoint.Distance > m_value)
                    return result;
                if (SphereDoesNotTouchSplitFace(1, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                {
                    V3d pmid = pmax; pmid.Y = m_value;
                    result |= m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
            }
            return result;
        }

        internal override void Count(Counts counts)
        {
            counts.InnerCount += 1;
            m_left.Count(counts);
            m_right.Count(counts);
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            var index = counts.InnerCount++;
            short axis = -2;

            flat.m_splitArray[index] = m_value;

            var leftAxisIndex = m_left.Flatten(counts, flat);
            var rightAxisIndex = m_right.Flatten(counts, flat);

            var index3 = index * 3;
            flat.m_nodeArray[index3] = KdFlat.Axes(leftAxisIndex.Axis, rightAxisIndex.Axis);
            flat.m_nodeArray[index3 + 1] = leftAxisIndex.Index;
            flat.m_nodeArray[index3 + 2] = rightAxisIndex.Index;

            return new AxisIndex(axis, index);
        }
    }

    #endregion

    #region KdInnerBothZ

    internal class KdInnerBothZ : KdNode, IFieldCodeable
    {
        public double m_value;
        public KdNode m_left;
        public KdNode m_right;

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "value", (c,o) => c.CodeDouble(ref ((KdInnerBothZ)o).m_value) ),
                new FieldCoder(1, "left", (c,o) => c.CodeT(ref ((KdInnerBothZ)o).m_left) ),
                new FieldCoder(2, "right", (c,o) => c.CodeT(ref ((KdInnerBothZ)o).m_right) ),
            };
        }

        public KdInnerBothZ()
        {
            m_value = 0.0f; m_left = null; m_right = null;
        }

        public KdInnerBothZ(double value, KdNode left, KdNode right)
        {
            m_value = value; m_left = left; m_right = right;
        }

        public override bool Intersect(
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            if (tpmin.P.Z < m_value)
            {
                if (tpmax.P.Z < m_value)
                    return m_left.Intersect(ref kdRay, ref tpmin, ref tpmax);
                if (tpmax.P.Z > m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin.Z)
                               * kdRay.FastRay.InvDir.Z;
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (m_left.Intersect(ref kdRay, ref tpmin, ref tp)
                            || m_right.Intersect(ref kdRay, ref tp, ref tpmax));
                }
                return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmax)
                        || m_right.Intersect(ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmin.P.Z > m_value)
            {
                if (tpmax.P.Z > m_value)
                    return m_right.Intersect(ref kdRay, ref tpmin, ref tpmax);
                if (tpmax.P.Z < m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin.Z)
                               * kdRay.FastRay.InvDir.Z;
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return (m_right.Intersect(ref kdRay, ref tpmin, ref tp)
                            || m_left.Intersect(ref kdRay, ref tp, ref tpmax));
                }
                return (m_right.Intersect(ref kdRay, ref tpmin, ref tpmax)
                        || m_left.Intersect(ref kdRay, ref tpmax, ref tpmax));
            }
            if (tpmax.P.Z < m_value)
                return (m_right.Intersect(ref kdRay, ref tpmin, ref tpmin)
                        || m_left.Intersect(ref kdRay, ref tpmin, ref tpmax));
            if (tpmax.P.Z > m_value)
                return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmin)
                        || m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
            return (m_left.Intersect(ref kdRay, ref tpmin, ref tpmax)
                    // the bitwise or is correct, since we may not exit early here
                    | m_right.Intersect(ref kdRay, ref tpmin, ref tpmax));
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outFlags)
        {
            if (outFlags == Box.Flags.All) return new KdNodeRef(this);

            if (box.Min.Z > m_value)
                return m_right.Intersect(ref box, outFlags);

            if (box.Max.Z < m_value)
                return m_left.Intersect(ref box, outFlags);

            var left = m_left.Intersect(ref box, outFlags | Box.Flags.MaxZ);
            var right = m_right.Intersect(ref box, outFlags | Box.Flags.MinZ);

            if (left == null) return right;
            if (right == null) return left;

            return new KdInnerBothZ(m_value, left, right);
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            return m_left.Indices(interiorSelector, borderSelector)
                    .Concat(m_right.Indices(interiorSelector, borderSelector));
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            return m_left.Leafs().Concat(m_right.Leafs());
        }

        public override bool ClosestPoint(
            ref KdQueryPoint query, ref V3d pmin, ref V3d pmax
            )
        {
            bool result = false;
            if (query.Point.Z < m_value)
            {
                {
                    V3d pmid = pmax; pmid.Z = m_value;
                    result |= m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
                if (query.Point.Z + query.ClosestPoint.Distance < m_value)
                    return result;
                if (SphereDoesNotTouchSplitFace(2, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                {
                    V3d pmid = pmin; pmid.Z = m_value;
                    result |= m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
            }
            else
            {
                {
                    V3d pmid = pmin; pmid.Z = m_value;
                    result |= m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
                if (query.Point.Z - query.ClosestPoint.Distance > m_value)
                    return result;
                if (SphereDoesNotTouchSplitFace(2, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return result;
                {
                    V3d pmid = pmax; pmid.Z = m_value;
                    result |= m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
            }
            return result;
        }

        internal override void Count(Counts counts)
        {
            counts.InnerCount += 1;
            m_left.Count(counts);
            m_right.Count(counts);
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            var index = counts.InnerCount++;
            short axis = -1;

            flat.m_splitArray[index] = m_value;

            var leftAxisIndex = m_left.Flatten(counts, flat);
            var rightAxisIndex = m_right.Flatten(counts, flat);

            var index3 = index * 3;
            flat.m_nodeArray[index3] = KdFlat.Axes(leftAxisIndex.Axis, rightAxisIndex.Axis);
            flat.m_nodeArray[index3 + 1] = leftAxisIndex.Index;
            flat.m_nodeArray[index3 + 2] = rightAxisIndex.Index;

            return new AxisIndex(axis, index);
        }
    }

    #endregion

    #region KdInnerLeft

    internal class KdInnerLeft : KdNode, IFieldCodeable
    {
        public byte m_axis;
        public double m_value;
        public KdNode m_left;

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "axis", (c,o) => c.CodeByte(ref ((KdInnerLeft)o).m_axis) ),
                new FieldCoder(1, "value", (c,o) => c.CodeDouble(ref ((KdInnerLeft)o).m_value) ),
                new FieldCoder(2, "left", (c,o) => c.CodeT(ref ((KdInnerLeft)o).m_left) ),
            };
        }

        public KdInnerLeft()
        {
            m_axis = 0; m_value = 0.0f; m_left = null;
        }

        public KdInnerLeft(byte axis, double value, KdNode left)
        {
            m_axis = axis; m_value = value; m_left = left;
        }

        public override bool Intersect(
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            if (tpmin.P[m_axis] < m_value)
            {
                if (tpmax.P[m_axis] < m_value)
                    return m_left.Intersect(ref kdRay, ref tpmin, ref tpmax);
                if (tpmax.P[m_axis] > m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin[m_axis])
                               * kdRay.FastRay.InvDir[m_axis];
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return m_left.Intersect(ref kdRay, ref tpmin, ref tp);
                }
                return m_left.Intersect(ref kdRay, ref tpmin, ref tpmax);
            }
            if (tpmin.P[m_axis] > m_value)
            {
                if (tpmax.P[m_axis] > m_value) return false;
                if (tpmax.P[m_axis] < m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin[m_axis])
                               * kdRay.FastRay.InvDir[m_axis];
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return m_left.Intersect(ref kdRay, ref tp, ref tpmax);
                }
                return m_left.Intersect(ref kdRay, ref tpmax, ref tpmax);
            }
            if (tpmax.P[m_axis] > m_value)
                return m_left.Intersect(ref kdRay, ref tpmin, ref tpmin);
            return m_left.Intersect(ref kdRay, ref tpmin, ref tpmax);
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outFlags)
        {
            if (outFlags == Box.Flags.All) return new KdNodeRef(this);

            if (box.Min[m_axis] > m_value)
                return null;

            if (box.Max[m_axis] < m_value)
                return m_left.Intersect(ref box, outFlags);

            return m_left.Intersect(ref box,
                        outFlags | (Box.Flags)((int)Box.Flags.MaxX << (int)m_axis));
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            return m_left.Indices(interiorSelector, borderSelector);
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            return m_left.Leafs();
        }

        public override bool ClosestPoint(
            ref KdQueryPoint query, ref V3d pmin, ref V3d pmax
            )
        {
            if (query.Point[m_axis] < m_value)
            {
                V3d pmid = pmax; pmid[m_axis] = m_value;
                return m_left.ClosestPoint(ref query, ref pmin, ref pmid);
            }
            else
            {
                if (query.Point[m_axis] - query.ClosestPoint.Distance > m_value)
                    return false;
                if (SphereDoesNotTouchSplitFace(m_axis, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return false;
                {
                    V3d pmid = pmax; pmid[m_axis] = m_value;
                    return m_left.ClosestPoint(ref query, ref pmin, ref pmid);
                }
            }
        }

        internal override void Count(Counts counts)
        {
            counts.InnerCount += 1;
            m_left.Count(counts);
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            var index = counts.InnerCount++;
            short axis = (short)(m_axis - 3);

            flat.m_splitArray[index] = m_value;

            var leftAxisIndex = m_left.Flatten(counts, flat);

            var index3 = index * 3;
            flat.m_nodeArray[index3] = KdFlat.Axes(leftAxisIndex.Axis, 0);
            flat.m_nodeArray[index3 + 1] = leftAxisIndex.Index;
            flat.m_nodeArray[index3 + 2] = -1;

            return new AxisIndex(axis, index);
        }
    }

    #endregion

    #region KdInnerRight

    internal class KdInnerRight : KdNode, IFieldCodeable
    {
        public byte m_axis;
        public double m_value;
        public KdNode m_right;

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "axis", (c,o) => c.CodeByte(ref ((KdInnerRight)o).m_axis) ),
                new FieldCoder(1, "value", (c,o) => c.CodeDouble(ref ((KdInnerRight)o).m_value) ),
                new FieldCoder(2, "right", (c,o) => c.CodeT(ref ((KdInnerRight)o).m_right) ),
            };
        }

        public KdInnerRight()
        {
            m_axis = 0; m_value = 0.0f; m_right = null;
        }

        public KdInnerRight(byte axis, double value, KdNode right)
        {
            m_axis = axis; m_value = value; m_right = right;
        }

        public override bool Intersect(
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            if (tpmin.P[m_axis] < m_value)
            {
                if (tpmax.P[m_axis] < m_value) return false;
                if (tpmax.P[m_axis] > m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin[m_axis])
                               * kdRay.FastRay.InvDir[m_axis];
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return m_right.Intersect(ref kdRay, ref tp, ref tpmax);
                }
                return m_right.Intersect(ref kdRay, ref tpmax, ref tpmax);
            }
            if (tpmin.P[m_axis] > m_value)
            {
                if (tpmax.P[m_axis] > m_value)
                    return m_right.Intersect(ref kdRay, ref tpmin, ref tpmax);
                if (tpmax.P[m_axis] < m_value)
                {
                    TP3d tp;
                    tp.T = (m_value - kdRay.FastRay.Ray.Origin[m_axis])
                               * kdRay.FastRay.InvDir[m_axis];
                    tp.P = kdRay.FastRay.Ray.GetPointOnRay(tp.T);
                    return m_right.Intersect(ref kdRay, ref tpmin, ref tp);
                }
                return m_right.Intersect(ref kdRay, ref tpmin, ref tpmax);
            }
            if (tpmax.P[m_axis] < m_value)
                return m_right.Intersect(ref kdRay, ref tpmin, ref tpmin);
            return m_right.Intersect(ref kdRay, ref tpmin, ref tpmax);
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outFlags)
        {
            if (outFlags == Box.Flags.All) return new KdNodeRef(this);

            if (box.Min[m_axis] > m_value)
                return m_right.Intersect(ref box, outFlags);

            if (box.Max[m_axis] < m_value)
                return null;

            return m_right.Intersect(ref box,
                        outFlags | (Box.Flags)((int)Box.Flags.MinX << (int)m_axis));
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            return m_right.Indices(interiorSelector, borderSelector);
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            return m_right.Leafs();
        }

        public override bool ClosestPoint(
            ref KdQueryPoint query, ref V3d pmin, ref V3d pmax
            )
        {
            if (query.Point[m_axis] < m_value)
            {
                if (query.Point[m_axis] + query.ClosestPoint.Distance < m_value)
                    return false;
                if (SphereDoesNotTouchSplitFace(m_axis, m_value, query.Point,
                                query.ClosestPoint.DistanceSquared, pmin, pmax))
                    return false;
                {
                    V3d pmid = pmin; pmid[m_axis] = m_value;
                    return m_right.ClosestPoint(ref query, ref pmid, ref pmax);
                }
            }
            else
            {
                V3d pmid = pmin; pmid[m_axis] = m_value;
                return m_right.ClosestPoint(ref query, ref pmid, ref pmax);
            }
        }

        internal override void Count(Counts counts)
        {
            counts.InnerCount += 1;
            m_right.Count(counts);
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            var index = counts.InnerCount++;
            short axis = (short)(m_axis - 3);

            flat.m_splitArray[index] = m_value;

            var rightAxisIndex = m_right.Flatten(counts, flat);

            var index3 = index * 3;
            flat.m_nodeArray[index3] = KdFlat.Axes(0, rightAxisIndex.Axis);
            flat.m_nodeArray[index3 + 1] = -1;
            flat.m_nodeArray[index3 + 2] = rightAxisIndex.Index;

            return new AxisIndex(axis, index);
        }
    }

    #endregion

    #region KdLeaf

    public class KdLeaf : KdNode, IFieldCodeable
    {
        public int[] m_objectIndexArray;

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            yield return
                new FieldCoder(0, "indices", (c, o) => c.CodeIntArray(ref ((KdLeaf)o).m_objectIndexArray));
        }

        public KdLeaf() { m_objectIndexArray = null;  }

        public KdLeaf(int[] objectIndexArray)
        {
            m_objectIndexArray = objectIndexArray;
        }

        public override bool Intersect(
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            return kdRay.ObjectSet.ObjectsIntersectRay(
                    m_objectIndexArray, 0, m_objectIndexArray.Length, kdRay.FastRay,
                    kdRay.ObjectFilter, kdRay.HitFilter,
                    tpmin.T, tpmax.T, ref kdRay.Hit);
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags flags)
        {
            return this;
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            if (borderSelector != null) return
                borderSelector(m_objectIndexArray, 0, m_objectIndexArray.Length);
            return interiorSelector(m_objectIndexArray, 0, m_objectIndexArray.Length);
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            yield return this;
        }

        public IEnumerable<int> Indices()
        {
            return m_objectIndexArray;
        }

        public override bool ClosestPoint(
            ref KdQueryPoint query, ref V3d pmin, ref V3d pmax
            )
        {
            return query.ObjectSet.ClosestPoint(
                    m_objectIndexArray, 0, m_objectIndexArray.Length, query.Point,
                    query.ObjectFilter, query.PointFilter,
                    ref query.ClosestPoint);
        }

        public IEnumerable<int> IndicesWhere(Func<int, bool> filter)
        {
            foreach (int index in m_objectIndexArray)
                if (filter(index)) yield return index;
        }

        internal override void Count(Counts counts)
        {
            counts.ObjectCount += m_objectIndexArray.Length;
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            var index = counts.ObjectCount;
            if (m_objectIndexArray.Length > 32767)
                throw new ArgumentException("leaf node with more than 32767 items");
            var axis = (short)m_objectIndexArray.Length;
            counts.ObjectCount = index + axis;
            m_objectIndexArray.CopyTo(axis, flat.m_indexArray, index);
            return new AxisIndex(axis, index);
        }
    }

    #endregion

    #region KdLeafSlice

    public class KdLeafSlice : KdNode, IFieldCodeable
    {
        private int[] m_objectIndexArray;
        private int m_first;
        private int m_count;

        public KdLeafSlice() { m_objectIndexArray = null; m_first = 0; m_count = 0; }

        public KdLeafSlice(int[] objectIndexArray, int first, int count)
        {
            m_objectIndexArray = objectIndexArray; m_first = first; m_count = count;
        }

        public override bool Intersect(ref KdIntersectionRay ray, ref TP3d tpmin, ref TP3d tpmax)
        {
            return ray.ObjectSet.ObjectsIntersectRay(
                    m_objectIndexArray, m_first, m_count, ray.FastRay,
                    ray.ObjectFilter, ray.HitFilter,
                    tpmin.T, tpmax.T, ref ray.Hit);
        }

        public override bool ClosestPoint(ref KdQueryPoint query, ref V3d pmin, ref V3d pmax)
        {
            return query.ObjectSet.ClosestPoint(
                    m_objectIndexArray, m_first, m_count, query.Point,
                    query.ObjectFilter, query.PointFilter,
                    ref query.ClosestPoint);
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outsideFlags)
        {
            return this;
        }

        public override IEnumerable<int> Indices(Func<int[], int, int, IEnumerable<int>> interiorSelector, Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            if (borderSelector != null)
                return borderSelector(m_objectIndexArray, m_first, m_count);
            return interiorSelector(m_objectIndexArray, m_first, m_count);
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            throw new NotImplementedException();
        }

        internal override void Count(Counts counts)
        {
            counts.ObjectCount += m_count;
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            var index = counts.ObjectCount;
            if (m_count > 32767)
                throw new ArgumentException("leaf node with more than 32767 items");
            var axis = (short)m_count;
            counts.ObjectCount = index + axis;
            m_objectIndexArray.CopyTo(m_first, m_count, flat.m_indexArray, index);
            return new AxisIndex(axis, index);
        }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            yield return
                new FieldCoder(0, "indices", (c, o) => c.CodeIntArray(ref ((KdLeafSlice)o).m_objectIndexArray));
            yield return
                new FieldCoder(1, "first", (c, o) => c.CodeInt(ref ((KdLeafSlice)o).m_first));
            yield return
                new FieldCoder(2, "count", (c, o) => c.CodeInt(ref ((KdLeafSlice)o).m_count));
        }
    }

    #endregion

    #region KdNodeRef

    internal class KdNodeRef : KdNode
    {
        readonly KdNode m_node;

        public KdNodeRef(KdNode node)
        {
            m_node = node;
        }

        public override bool Intersect(ref KdIntersectionRay ray, ref TP3d tpmin, ref TP3d tpmax)
        {
            return m_node.Intersect(ref ray, ref tpmin, ref tpmax);
        }

        public override bool ClosestPoint(ref KdQueryPoint query, ref V3d pmin, ref V3d pmax)
        {
            return m_node.ClosestPoint(ref query, ref pmin, ref pmax);
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outsideFlags)
        {
            return m_node.Intersect(ref box, outsideFlags);
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            return m_node.Indices(interiorSelector, null);
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            return m_node.Leafs();
        }

        internal override void Count(Counts counts)
        {
            m_node.Count(counts);
        }

        internal override AxisIndex Flatten(Counts counts, KdFlat flat)
        {
            return m_node.Flatten(counts, flat);
        }


    }

    #endregion

    #region KdNode Extensions

    public static class KdNodeExtensions
    {
        public static KdNode Flatten(this KdNode node)
        {
            var counts = new Counts();
            node.Count(counts);

            var splitArray = new double[counts.InnerCount];
            var nodeArray = new int[counts.InnerCount * 3];
            var indexArray = new int[counts.ObjectCount];

            var flat = new KdFlat(0, 0, splitArray, nodeArray, indexArray);

            try
            {
                var axisIndex = node.Flatten(new Counts(), flat);

                flat.m_rootAxis = axisIndex.Axis;
                flat.m_rootIndex = axisIndex.Index;
            }
            catch (ArgumentException)
            {
                Report.Warn("cannot flatten KdTree with leafs containing more than 32767 items");
                return node;
            }

            return flat;
        }
    }

    #endregion

    #region Legacy Nodes with float precision

    #region KdFloatNode

    internal abstract class KdFloatNode : KdNode
    {
        public override bool Intersect(
                ref KdIntersectionRay kdRay, ref TP3d tpmin, ref TP3d tpmax)
        {
            throw new NotImplementedException();
        }

        public override KdNode Intersect(ref Box3d box, Box.Flags outFlags)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<KdLeaf> Leafs()
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<int> Indices(
                Func<int[], int, int, IEnumerable<int>> interiorSelector,
                Func<int[], int, int, IEnumerable<int>> borderSelector)
        {
            throw new NotImplementedException();
        }

        public override bool ClosestPoint(
            ref KdQueryPoint query, ref V3d pmin, ref V3d pmax
            )
        {
            throw new NotImplementedException();
        }
    }

    #endregion

    #region KdFloatInner

    internal class KdFloatInner : KdFloatNode, IFieldCodeable
    {
        public byte m_axis;
        public float m_value;
        public KdNode m_left;
        public KdNode m_right;

        public override KdNode ToDouble()
        {
            return new KdInner(m_axis, m_value, m_left.ToDouble(), m_right.ToDouble());
        }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "axis", (c,o) => c.CodeByte(ref ((KdFloatInner)o).m_axis) ),
                new FieldCoder(1, "value", (c,o) => c.CodeFloat(ref ((KdFloatInner)o).m_value) ),
                new FieldCoder(2, "left", (c,o) => c.CodeT(ref ((KdFloatInner)o).m_left) ),
                new FieldCoder(3, "right", (c,o) => c.CodeT(ref ((KdFloatInner)o).m_right) ),
            };
        }

        public KdFloatInner()
        {
            m_axis = 0; m_value = 0.0f; m_left = null; m_right = null;
        }

        public KdFloatInner(byte axis, float value, KdNode left, KdNode right)
        {
            m_axis = axis; m_value = value; m_left = left; m_right = right;
        }
    }

    #endregion

    #region KdFloatInnerBoth

    internal class KdFloatInnerBoth : KdFloatNode, IFieldCodeable
    {
        public byte m_axis;
        public float m_value;
        public KdNode m_left;
        public KdNode m_right;

        public override KdNode ToDouble()
        {
            return new KdInnerBoth(m_axis, m_value, m_left.ToDouble(), m_right.ToDouble());
        }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "axis", (c,o) => c.CodeByte(ref ((KdFloatInnerBoth)o).m_axis) ),
                new FieldCoder(1, "value", (c,o) => c.CodeFloat(ref ((KdFloatInnerBoth)o).m_value) ),
                new FieldCoder(2, "left", (c,o) => c.CodeT(ref ((KdFloatInnerBoth)o).m_left) ),
                new FieldCoder(3, "right", (c,o) => c.CodeT(ref ((KdFloatInnerBoth)o).m_right) ),
            };
        }

        public KdFloatInnerBoth()
        {
            m_axis = 0; m_value = 0.0f; m_left = null; m_right = null;
        }

        public KdFloatInnerBoth(byte axis, float value, KdNode left, KdNode right)
        {
            m_axis = axis; m_value = value; m_left = left; m_right = right;
        }
    }

    #endregion

    #region KdFloatInnerBothX

    internal class KdFloatInnerBothX : KdFloatNode, IFieldCodeable
    {
        public float m_value;
        public KdNode m_left;
        public KdNode m_right;

        public override KdNode ToDouble()
        {
            return new KdInnerBothX(m_value, m_left.ToDouble(), m_right.ToDouble());
        }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "value", (c,o) => c.CodeFloat(ref ((KdFloatInnerBothX)o).m_value) ),
                new FieldCoder(1, "left", (c,o) => c.CodeT(ref ((KdFloatInnerBothX)o).m_left) ),
                new FieldCoder(2, "right", (c,o) => c.CodeT(ref ((KdFloatInnerBothX)o).m_right) ),
            };
        }

        public KdFloatInnerBothX()
        {
            m_value = 0.0f; m_left = null; m_right = null;
        }

        public KdFloatInnerBothX(float value, KdNode left, KdNode right)
        {
            m_value = value; m_left = left; m_right = right;
        }
    }

    #endregion

    #region KdFloatInnerBothY

    internal class KdFloatInnerBothY : KdFloatNode, IFieldCodeable
    {
        public float m_value;
        public KdNode m_left;
        public KdNode m_right;

        public override KdNode ToDouble()
        {
            return new KdInnerBothY(m_value, m_left.ToDouble(), m_right.ToDouble());
        }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "value", (c,o) => c.CodeFloat(ref ((KdFloatInnerBothY)o).m_value) ),
                new FieldCoder(1, "left", (c,o) => c.CodeT(ref ((KdFloatInnerBothY)o).m_left) ),
                new FieldCoder(2, "right", (c,o) => c.CodeT(ref ((KdFloatInnerBothY)o).m_right) ),
            };
        }

        public KdFloatInnerBothY()
        {
            m_value = 0.0f; m_left = null; m_right = null;
        }

        public KdFloatInnerBothY(float value, KdNode left, KdNode right)
        {
            m_value = value; m_left = left; m_right = right;
        }

    }

    #endregion

    #region KdFloatInnerBothZ

    internal class KdFloatInnerBothZ : KdFloatNode, IFieldCodeable
    {
        public float m_value;
        public KdNode m_left;
        public KdNode m_right;

        public override KdNode ToDouble()
        {
            return new KdInnerBothZ(m_value, m_left.ToDouble(), m_right.ToDouble());
        }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "value", (c,o) => c.CodeFloat(ref ((KdFloatInnerBothZ)o).m_value) ),
                new FieldCoder(1, "left", (c,o) => c.CodeT(ref ((KdFloatInnerBothZ)o).m_left) ),
                new FieldCoder(2, "right", (c,o) => c.CodeT(ref ((KdFloatInnerBothZ)o).m_right) ),
            };
        }

        public KdFloatInnerBothZ()
        {
            m_value = 0.0f; m_left = null; m_right = null;
        }

        public KdFloatInnerBothZ(float value, KdNode left, KdNode right)
        {
            m_value = value; m_left = left; m_right = right;
        }
    }

    #endregion

    #region KdFloatInnerLeft

    internal class KdFloatInnerLeft : KdFloatNode, IFieldCodeable
    {
        public byte m_axis;
        public float m_value;
        public KdNode m_left;

        public override KdNode ToDouble()
        {
            return new KdInnerLeft(m_axis, m_value, m_left.ToDouble());
        }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "axis", (c,o) => c.CodeByte(ref ((KdFloatInnerLeft)o).m_axis) ),
                new FieldCoder(1, "value", (c,o) => c.CodeFloat(ref ((KdFloatInnerLeft)o).m_value) ),
                new FieldCoder(2, "left", (c,o) => c.CodeT(ref ((KdFloatInnerLeft)o).m_left) ),
            };
        }

        public KdFloatInnerLeft()
        {
            m_axis = 0; m_value = 0.0f; m_left = null;
        }

        public KdFloatInnerLeft(byte axis, float value, KdNode left)
        {
            m_axis = axis; m_value = value; m_left = left;
        }
    }

    #endregion

    #region KdFloatInnerRight

    internal class KdFloatInnerRight : KdFloatNode, IFieldCodeable
    {
        public byte m_axis;
        public float m_value;
        public KdNode m_right;

        public override KdNode ToDouble()
        {
            return new KdInnerRight(m_axis, m_value, m_right.ToDouble());
        }

        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            return new[]
            {
                new FieldCoder(0, "axis", (c,o) => c.CodeByte(ref ((KdFloatInnerRight)o).m_axis) ),
                new FieldCoder(1, "value", (c,o) => c.CodeFloat(ref ((KdFloatInnerRight)o).m_value) ),
                new FieldCoder(2, "right", (c,o) => c.CodeT(ref ((KdFloatInnerRight)o).m_right) ),
            };
        }

        public KdFloatInnerRight()
        {
            m_axis = 0; m_value = 0.0f; m_right = null;
        }

        public KdFloatInnerRight(byte axis, float value, KdNode right)
        {
            m_axis = axis; m_value = value; m_right = right;
        }
    }

    #endregion

    #endregion
}
