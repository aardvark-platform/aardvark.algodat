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

//#define READONLY

using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using Newtonsoft.Json;
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
    /// An immutable point cloud node.
    /// </summary>
    public class PointSetNode : IPointCloudNode
    {
        private static readonly string GuidEmptyString = Guid.Empty.ToString();

        /// <summary>
        /// This node has been stored during out-of-core import, without derived properties, like kd-trees, etc.
        /// The value of this attribute has no meaning.
        /// </summary>
        public static readonly Durable.Def TemporaryImportNode = new Durable.Def(
            new Guid("01bcdfee-b01e-40ff-ad02-7ea724198f10"),
            "Durable.Octree.TemporaryImportNode",
            "This node has been stored during out-of-core import, without derived properties, like kd-trees, etc. The value of this attribute has no meaning.",
            Durable.Primitives.Int32.Id,
            isArray: false
            );

        public static readonly PointSetNode Empty = new PointSetNode(
            null, writeToStore: false,
            (Durable.Octree.NodeId, Guid.Empty),
            (Durable.Octree.Cell, Cell.Unit),
            (Durable.Octree.BoundingBoxExactLocal, Box3f.Unit - new V3f(0.5f)),
            (Durable.Octree.BoundingBoxExactGlobal, Box3d.Unit),
            (Durable.Octree.PointCountCell, 0),
            (Durable.Octree.PointCountTreeLeafs, 0L),
            (Durable.Octree.PositionsLocal3fReference, Guid.Empty),
            (Durable.Octree.PointRkdTreeFDataReference, Guid.Empty),
            (Durable.Octree.PositionsLocal3fCentroid, V3f.Zero),
            (Durable.Octree.PositionsLocal3fDistToCentroidStdDev, 0.0f),
            (Durable.Octree.AveragePointDistance, 0.0f),
            (Durable.Octree.AveragePointDistanceStdDev, 0.0f),
            (Durable.Octree.MinTreeDepth, 0),
            (Durable.Octree.MaxTreeDepth, 0)
            );

        #region Construction

        /// <summary>
        /// Creates node.
        /// </summary>
        public PointSetNode(Storage storage, bool writeToStore, params (Durable.Def, object)[] props)
            : this(
                  ImmutableDictionary<Durable.Def, object>.Empty.AddRange(props.Select(x => new KeyValuePair<Durable.Def, object>(x.Item1, x.Item2))),
                  storage, writeToStore
                  )
        {
        }

        /// <summary>
        /// Creates node.
        /// </summary>
        public PointSetNode(
            ImmutableDictionary<Durable.Def, object> data,
            Storage storage, bool writeToStore
            )
        {
            if (!data.ContainsKey(Durable.Octree.NodeId)) throw new ArgumentException(
                "Missing Durable.Octree.NodeId. Invariant a14a3d76-36cd-430b-b160-42d7a53f916d."
                );

            if (!data.ContainsKey(Durable.Octree.Cell)) throw new ArgumentException(
                "Missing Durable.Octree.Cell. Invariant f6f13915-c254-4d88-b60f-5e673c13ef59."
                );

            var isObsoleteFormat = data.ContainsKey(Durable.Octree.PointRkdTreeDDataReference);

            Storage = storage;
            Data = data;

            //Report.Line($"{this.Id} {this.Cell}");
            //if (Id == Guid.Empty) Debugger.Break();
            //if (IsLeaf && !HasClassifications) Debugger.Break();
            //if (!IsTemporaryImportNode) Debugger.Break();

            var bboxCell = Cell.BoundingBox;
            Center = bboxCell.Center;
            Corners = bboxCell.ComputeCorners();

#if DEBUG && NEVERMORE
            if (isObsoleteFormat)
            {
                Report.Line($"new PointSetNode({Id}), obsolete format");
            }
#endif

#region Subnodes

            if (Data.TryGetValue(Durable.Octree.SubnodesGuids, out object o) && o is Guid[] subNodeIds)
            {
                Subnodes = new PersistentRef<IPointCloudNode>[8];
                for (var i = 0; i < 8; i++)
                {
                    if (subNodeIds[i] == Guid.Empty) continue;
                    var pRef = new PersistentRef<IPointCloudNode>(subNodeIds[i].ToString(), storage.GetPointCloudNode, storage.TryGetPointCloudNode);
                    Subnodes[i] = pRef;

#if DEBUG && NEVERMORE
                    // ensure that child cells have correct exponent (one less)
                    if (pRef.Value.Cell.Exponent + 1 != Cell.Exponent) throw new InvalidOperationException("Invariant a5308834-2509-4af5-8986-c717da792611.");
#endif
                }

#if DEBUG
                //var n0count = 0;
                //var n1count = 0;
                //foreach (var n in Subnodes)
                //{
                //    if (n == null) continue;
                //    if (n.Value.HasNormals) n1count++; else n0count++;
                //}
                //if (n0count > 0 && n1count > 0)
                //{
                //    var foo1 = Subnodes.Map(n => n?.Value?.HasNormals);
                //    var foo2 = Subnodes.Map(n => n?.Value?.IsLeaf);
                //    throw new Exception();
                //}
#endif
            }
            else
            {
                //Debugger.Break();
            }

#endregion

#region Per-point attributes

            var psId = PositionsId;
            var csId = ColorsId;
            var kdId = KdTreeId;
            var nsId = NormalsId;
            var isId = IntensitiesId;
            var ksId = ClassificationsId;

            if (psId != null) PersistentRefs[Durable.Octree.PositionsLocal3fReference] = new PersistentRef<V3f[]>(psId.Value, storage.GetV3fArray, storage.TryGetV3fArray);
            if (csId != null) PersistentRefs[Durable.Octree.Colors4bReference] = new PersistentRef<C4b[]>(csId.Value, storage.GetC4bArray, storage.TryGetC4bArray);
            if (kdId != null)
            {
                if (isObsoleteFormat)
                {
                    PersistentRefs[Durable.Octree.PointRkdTreeFDataReference] =
                        new PersistentRef<PointRkdTreeF<V3f[], V3f>>(kdId.Value, LoadKdTreeObsolete, TryLoadKdTreeObsolete)
                        ;
                }
                else
                {
                    PersistentRefs[Durable.Octree.PointRkdTreeFDataReference] =
                        new PersistentRef<PointRkdTreeF<V3f[], V3f>>(kdId.Value, LoadKdTree, TryLoadKdTree)
                        ;
                }
            }
            if (nsId != null) PersistentRefs[Durable.Octree.Normals3fReference] = new PersistentRef<V3f[]>(nsId.Value, storage.GetV3fArray, storage.TryGetV3fArray);
            if (isId != null) PersistentRefs[Durable.Octree.Intensities1iReference] = new PersistentRef<int[]>(isId.Value, storage.GetIntArray, storage.TryGetIntArray);
            if (ksId != null) PersistentRefs[Durable.Octree.Classifications1bReference] = new PersistentRef<byte[]>(ksId.Value, storage.GetByteArray, storage.TryGetByteArray);

#endregion

#region Durable.Octree.BoundingBoxExactLocal, Durable.Octree.BoundingBoxExactGlobal

            if (HasPositions && (!HasBoundingBoxExactLocal || !HasBoundingBoxExactGlobal))
            {
                if (!HasBoundingBoxExactLocal) // why only for new format? (!isObsoleteFormat && !HasBoundingBoxExactLocal)
                {
                    var bboxExactLocal = Positions.Value.Length > 0 ? new Box3f(Positions.Value) : Box3f.Invalid;
                    Data = Data.Add(Durable.Octree.BoundingBoxExactLocal, bboxExactLocal);
                }

                if (!HasBoundingBoxExactGlobal)
                {
                    if (HasBoundingBoxExactLocal && IsLeaf)
                    {
                        var bboxExactGlobal = (Box3d)BoundingBoxExactLocal + Center;
                        Data = Data.Add(Durable.Octree.BoundingBoxExactGlobal, bboxExactGlobal);
                    }
                    else if (isObsoleteFormat)
                    {
                        var bboxExactGlobal = Cell.BoundingBox;
                        Data = Data.Add(Durable.Octree.BoundingBoxExactGlobal, bboxExactGlobal);
                    }
                    else
                    {
                        var bboxExactGlobal = new Box3d(Subnodes.Where(x => x != null).Select(x => x.Value.BoundingBoxExactGlobal));
                        Data = Data.Add(Durable.Octree.BoundingBoxExactGlobal, bboxExactGlobal);
                    }
                }
            }

#endregion

#region Durable.Octree.PointCountCell

            if (!Has(Durable.Octree.PointCountCell))
            {
                Data = Data.Add(Durable.Octree.PointCountCell, HasPositions ? Positions.Value.Length : 0);
            }

#endregion

#region Durable.Octree.PointCountTreeLeafs

            if (!Has(Durable.Octree.PointCountTreeLeafs))
            {
                if (IsLeaf)
                {
                    Data = Data.Add(Durable.Octree.PointCountTreeLeafs, (long)PointCountCell);
                }
                else
                {
                    Data = Data.Add(Durable.Octree.PointCountTreeLeafs, Subnodes.Where(x => x != null).Sum(x => x.Value.PointCountTree));
                }
            }

#endregion

#region Centroid*

            if (HasPositions && (!HasCentroidLocal || !HasCentroidLocalStdDev))
            {
                if (!isObsoleteFormat)
                {
                    var ps = Positions.Value;
                    var centroid = ps.ComputeCentroid();

                    if (!HasCentroidLocal)
                    {
                        Data = Data.Add(Durable.Octree.PositionsLocal3fCentroid, centroid);
                    }

                    var dists = ps.Map(p => (p - centroid).Length);
                    var (avg, sd) = dists.ComputeAvgAndStdDev();

                    if (!HasCentroidLocalStdDev)
                    {
                        Data = Data.Add(Durable.Octree.PositionsLocal3fDistToCentroidStdDev, sd);
                    }
                }
            }
#endregion

#region TreeDepth*

            if (!HasMinTreeDepth || !HasMaxTreeDepth)
            {
                if (IsLeaf)
                {
                    if (!HasMinTreeDepth)
                    {
                        Data = Data.Add(Durable.Octree.MinTreeDepth, 0);
                    }
                    if (!HasMaxTreeDepth)
                    {
                        Data = Data.Add(Durable.Octree.MaxTreeDepth, 0);
                    }
                }
                else
                {
                    if (isObsoleteFormat)
                    {
                        Data = Data.Add(Durable.Octree.MinTreeDepth, 0);
                        Data = Data.Add(Durable.Octree.MaxTreeDepth, 0);
                    }
                    else
                    {
                        var min = 1;
                        var max = 0;
                        for (var i = 0; i < 8; i++)
                        {
                            var r = Subnodes[i];
                            if (r == null) continue;
                            var n = r.Value;
                            min = Math.Min(min, 1 + n.MinTreeDepth);
                            max = Math.Max(max, 1 + n.MaxTreeDepth);
                        }
                        if (!HasMinTreeDepth)
                        {
                            Data = Data.Add(Durable.Octree.MinTreeDepth, min);
                        }
                        if (!HasMaxTreeDepth)
                        {
                            Data = Data.Add(Durable.Octree.MaxTreeDepth, max);
                        }
                    }
                }
            }

#endregion

#region KdTree

            if (HasPositions && !HasKdTree && !IsTemporaryImportNode)
            {
#if !READONLY
                kdId = ComputeAndStoreKdTree(Storage, Positions.Value);
                Data = Data.Add(Durable.Octree.PointRkdTreeFDataReference, kdId);
                PersistentRefs[Durable.Octree.PointRkdTreeFDataReference] =
                    new PersistentRef<PointRkdTreeF<V3f[], V3f>>(kdId.Value, LoadKdTree, TryLoadKdTree)
                    ;
#else
                //Debugger.Break();
#endif
            }

            #endregion

            if ((HasPositions && Positions.Value.Length != PointCountCell) ||
                (HasColors && Colors.Value.Length != PointCountCell) ||
                (HasNormals && Normals.Value.Length != PointCountCell) ||
                (HasIntensities && Intensities.Value.Length != PointCountCell) ||
                (HasClassifications && Classifications.Value.Length != PointCountCell)
                )
            {
#if DEBUG
                throw new InvalidOperationException(
#else
                Report.Error(
#endif
                    $"[PointSetNode] Inconsistent counts. Id = {Id}. " +
                    $"PointCountCell={PointCountCell}, " +
                    $"Positions={Positions?.Value?.Length}, " +
                    $"Colors={Colors?.Value?.Length}, " +
                    $"Normals={Normals?.Value?.Length}, " +
                    $"Intensities={Intensities?.Value?.Length}, " +
                    $"Classifications={Classifications?.Value?.Length}, " +
                    $"Invariant b714b13e-35fb-4186-9c6c-ca6abbc46a4d."
                    );
            }


            if (writeToStore)
            {
                //Report.Warn($"[writeToStore] {Id}");
                WriteToStore();
            }

#if DEBUG
            if (IsLeaf)
            {
                if (PointCountCell != PointCountTree)
                    throw new InvalidOperationException("Invariant 9464f38c-dc98-4d68-a8ac-0baed9f182b4.");

                if (!(
                    Has(Durable.Octree.PositionsLocal3fReference) ||
                    Has(Durable.Octree.PositionsLocal3f) ||
                    Has(Durable.Octree.PositionsLocal3b) ||
                    Has(Durable.Octree.PositionsLocal3us) ||
                    Has(Durable.Octree.PositionsLocal3ui) ||
                    Has(Durable.Octree.PositionsLocal3ul)
                    ))
                    throw new ArgumentException("Invariant 663c45a4-1286-45ba-870c-fb4ceebdf318.");

                if (Has(Durable.Octree.PositionsLocal3fReference) && PositionsId == null)
                    throw new InvalidOperationException("Invariant ba64ffe9-ada4-4fff-a4e9-0916c1cc9992.");

                #if !READONLY
                if (KdTreeId == null && !Has(TemporaryImportNode))
                    throw new InvalidOperationException("Invariant 606e8a7b-6e75-496a-bc2a-dfbe6e2c9b10.");
                #endif
            }
#endif

            PointRkdTreeF<V3f[], V3f> LoadKdTree(string key)
            {
                var value = Storage.GetPointRkdTreeFData(key);
                var ps = Positions.Value;
                return new PointRkdTreeF<V3f[], V3f>(
                    3, ps.Length, ps,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, 1e-6f,
                    value
                    );
            }

            (bool, PointRkdTreeF<V3f[], V3f>) TryLoadKdTree(string key)
            {
                var (ok, value) = Storage.TryGetPointRkdTreeFData(key);
                if (ok == false) return (false, default);
                var ps = Positions.Value;
                return (true, new PointRkdTreeF<V3f[], V3f>(
                    3, ps.Length, ps,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, 1e-6f,
                    value
                    ));
            }

            PointRkdTreeF<V3f[], V3f> LoadKdTreeObsolete(string key)
            {
                var value = Storage.GetPointRkdTreeFDataFromD(key);
                var ps = Positions.Value;
                return new PointRkdTreeF<V3f[], V3f>(
                    3, ps.Length, ps,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, 1e-6f,
                    value
                    );
            }

            (bool, PointRkdTreeF<V3f[], V3f>) TryLoadKdTreeObsolete(string key)
            {
                var (ok, value) = Storage.TryGetPointRkdTreeFDataFromD(key);
                if (ok == false) return (false, default);
                var ps = Positions.Value;
                return (true, new PointRkdTreeF<V3f[], V3f>(
                    3, ps.Length, ps,
                    (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                    (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                    (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, 1e-6f,
                    value
                    ));
            }
        }

        /// <summary>
        /// Writes this node to store.
        /// </summary>
        public PointSetNode WriteToStore()
        {
            this.CheckDerivedAttributes();
            Storage.Add(Id.ToString(), this);
            return this;
        }

        IPointCloudNode IPointCloudNode.WriteToStore() => WriteToStore();

#endregion

#region Properties (state to serialize)

        /// <summary>
        /// Durable properties.
        /// </summary>
        private ImmutableDictionary<Durable.Def, object> Data { get; } = ImmutableDictionary<Durable.Def, object>.Empty;

#endregion

#region Properties (derived/runtime, non-serialized)

        /// <summary>
        /// Runtime.
        /// </summary>
        [JsonIgnore]
        public readonly Storage Storage;

        /// <summary>
        /// Runtime.
        /// </summary>
        [JsonIgnore]
        private Dictionary<Durable.Def, object> PersistentRefs { get; } = new Dictionary<Durable.Def, object>();

#region Cell attributes

        /// <summary>
        /// This node's unique id (16 bytes).
        /// </summary>
        [JsonIgnore]
        public Guid Id => (Guid)Data.Get(Durable.Octree.NodeId);

        /// <summary>
        /// Returns true if this a temporary import node (without computed properties like kd-tree).
        /// </summary>
        [JsonIgnore]
        public bool IsTemporaryImportNode => Has(TemporaryImportNode);

        /// <summary>
        /// This node's index/bounds.
        /// </summary>
        [JsonIgnore]
        public Cell Cell => (Cell)Data.Get(Durable.Octree.Cell);

        /// <summary>
        /// Octree. Number of points in this cell.
        /// Durable definition 172e1f20-0ffc-4d9c-9b3d-903fca41abe3.
        /// </summary>
        [JsonIgnore]
        public int PointCountCell => (int)Data.Get(Durable.Octree.PointCountCell);


        /// <summary>
        /// Number of points in this tree (sum of leaves).
        /// </summary>
        [JsonIgnore]
        public long PointCountTree => (long)Data.Get(Durable.Octree.PointCountTreeLeafs);

        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// </summary>
        [JsonIgnore]
        public Guid?[] SubnodeIds
        {
            get
            {
                if (Data.TryGetValue(Durable.Octree.SubnodesGuids, out object o))
                {
                    return ((Guid[])o).Map(x => x != Guid.Empty ? x : (Guid?)null);
                }
                else
                {
                    return null;
                }
            }
        }

#endregion

#region Positions

        /// <summary></summary>
        [JsonIgnore]
        public bool HasPositions => 
            Data.ContainsKey(Durable.Octree.PositionsLocal3fReference) ||
            Data.ContainsKey(Durable.Octree.PositionsLocal3f) ||
            Data.ContainsKey(Durable.Octree.PositionsLocal3b) ||
            Data.ContainsKey(Durable.Octree.PositionsLocal3us) ||
            Data.ContainsKey(Durable.Octree.PositionsLocal3ui) ||
            Data.ContainsKey(Durable.Octree.PositionsLocal3ul)
            ;

        /// <summary></summary>
        [JsonIgnore]
        public Guid? PositionsId => Data.TryGetValue(Durable.Octree.PositionsLocal3fReference, out var id) ? (Guid?)id : null;

        /// <summary>
        /// Point positions relative to cell's center, or null if no positions.
        /// </summary>
        [JsonIgnore]
        public PersistentRef<V3f[]> Positions
        {
            get
            {
                if (PersistentRefs.TryGetValue(Durable.Octree.PositionsLocal3fReference, out object o))
                {
                    if (((PersistentRef<V3f[]>)o).Id != GuidEmptyString)
                    {
                        return (PersistentRef<V3f[]>)o;
                    }
                    else
                    {
                        var ps = Array.Empty<V3f>();
                        return new PersistentRef<V3f[]>(Guid.Empty, _ => ps, _ => (true, ps));
                    }
                }
                else if (Data.TryGetValue(Durable.Octree.PositionsLocal3f, out o))
                {
                    var ps = (V3f[])o;
                    return new PersistentRef<V3f[]>(Guid.Empty, _ => ps, _ => (true, ps));
                }
                else if (Data.TryGetValue(Durable.Octree.PositionsLocal3b, out o))
                {
                    var qs = (byte[])o;
                    var hsize = Math.Pow(2.0, Cell.Exponent - 1);
                    var step = (2.0 * hsize) / (byte.MaxValue + 1);
                    var pCount = PointCountCell;
                    var ps = new V3f[pCount];
                    for (int i = 0, j = 0; i < pCount; i++)
                        ps[i] = new V3f(qs[j++] * step - hsize, qs[j++] * step - hsize, qs[j++] * step - hsize);
                    return new PersistentRef<V3f[]>(Guid.Empty, _ => ps, _ => (true, ps));
                }
                else if (Data.TryGetValue(Durable.Octree.PositionsLocal3us, out o))
                {
                    var qs = (ushort[])o;
                    var hsize = Math.Pow(2.0, Cell.Exponent - 1);
                    var step = (2.0 * hsize) / (ushort.MaxValue + 1);
                    var pCount = PointCountCell;
                    var ps = new V3f[pCount];
                    for (int i = 0, j = 0; i < pCount; i++)
                        ps[i] = new V3f(qs[j++] * step - hsize, qs[j++] * step - hsize, qs[j++] * step - hsize);
                    return new PersistentRef<V3f[]>(Guid.Empty, _ => ps, _ => (true, ps));
                }
                else if (Data.TryGetValue(Durable.Octree.PositionsLocal3ui, out o))
                {
                    var qs = (uint[])o;
                    var hsize = Math.Pow(2.0, Cell.Exponent - 1);
                    var step = (2.0 * hsize) / ((ulong)uint.MaxValue + 1);
                    var pCount = PointCountCell;
                    var ps = new V3f[pCount];
                    for (int i = 0, j = 0; i < pCount; i++)
                        ps[i] = new V3f(qs[j++] * step - hsize, qs[j++] * step - hsize, qs[j++] * step - hsize);
                    return new PersistentRef<V3f[]>(Guid.Empty, _ => ps, _ => (true, ps));
                }
                else if (Data.TryGetValue(Durable.Octree.PositionsLocal3ul, out o))
                {
                    var qs = (ulong[])o;
                    var hsize = Math.Pow(2.0, Cell.Exponent - 1);
                    var step = (2.0 * hsize) / ((double)ulong.MaxValue + 1);
                    var pCount = PointCountCell;
                    var ps = new V3f[pCount];
                    for (int i = 0, j = 0; i < pCount; i++)
                        ps[i] = new V3f(qs[j++] * step - hsize, qs[j++] * step - hsize, qs[j++] * step - hsize);
                    return new PersistentRef<V3f[]>(Guid.Empty, _ => ps, _ => (true, ps));
                }
                else
                    return null;
            }
        }

        /// <summary>
        /// Point positions (absolute), or null if no positions.
        /// </summary>
        [JsonIgnore]
        public V3d[] PositionsAbsolute => Positions?.Value.Map(p => new V3d(Center.X + p.X, Center.Y + p.Y, Center.Z + p.Z));

#endregion

#region BoundingBoxExactLocal

        /// <summary></summary>
        [JsonIgnore]
        public bool HasBoundingBoxExactLocal => Data.ContainsKey(Durable.Octree.BoundingBoxExactLocal);

        /// <summary></summary>
        [JsonIgnore]
        public Box3f BoundingBoxExactLocal => Data.TryGetValue(Durable.Octree.BoundingBoxExactLocal, out var o) ? (Box3f)o : default;

#endregion

#region BoundingBoxExactGlobal

        /// <summary></summary>
        [JsonIgnore]
        public bool HasBoundingBoxExactGlobal => Data.ContainsKey(Durable.Octree.BoundingBoxExactGlobal);

        /// <summary></summary>
        [JsonIgnore]
        public Box3d BoundingBoxExactGlobal => Data.TryGetValue(Durable.Octree.BoundingBoxExactGlobal, out var o) ? (Box3d)o : default;

#endregion

#region Colors

        /// <summary></summary>
        [JsonIgnore]
        public bool HasColors =>
            Data.ContainsKey(Durable.Octree.Colors4bReference) ||
            Data.ContainsKey(Durable.Octree.Colors4b)
            ;

        /// <summary></summary>
        [JsonIgnore]
        public Guid? ColorsId => Data.TryGetValue(Durable.Octree.Colors4bReference, out var id) ? (Guid?)id : null;

        /// <summary>
        /// Point colors, or null if no points.
        /// </summary>
        [JsonIgnore]
        public PersistentRef<C4b[]> Colors
        {
            get
            {
                if (PersistentRefs.TryGetValue(Durable.Octree.Colors4bReference, out object o) && ((PersistentRef<C4b[]>)o).Id != GuidEmptyString)
                {
                    return (PersistentRef<C4b[]>)o;
                }
                else if (Data.TryGetValue(Durable.Octree.Colors4b, out o))
                {
                    var xs = (C4b[])o;
                    return new PersistentRef<C4b[]>(Guid.Empty, _ => xs, _ => (true, xs));
                }
                else
                {
                    return null;
                }
            }
        }

#endregion

#region Normals

        /// <summary></summary>
        [JsonIgnore]
        public bool HasNormals =>
            Data.ContainsKey(Durable.Octree.Normals3fReference) ||
            Data.ContainsKey(Durable.Octree.Normals3f)
            ;

        /// <summary></summary>
        [JsonIgnore]
        public Guid? NormalsId => Data.TryGetValue(Durable.Octree.Normals3fReference, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<V3f[]> Normals
        {
            get
            {
                if (PersistentRefs.TryGetValue(Durable.Octree.Normals3fReference, out object o) && ((PersistentRef<V3f[]>)o).Id != GuidEmptyString)
                {
                    return (PersistentRef<V3f[]>)o;
                }
                else if (Data.TryGetValue(Durable.Octree.Normals3f, out o))
                {
                    var xs = (V3f[])o;
                    return new PersistentRef<V3f[]>(Guid.Empty, _ => xs, _ => (true, xs));
                }
                else
                {
                    return null;
                }
            }
        }

#endregion

#region Intensities

        /// <summary></summary>
        [JsonIgnore]
        public bool HasIntensities =>
            Data.ContainsKey(Durable.Octree.Intensities1iReference) ||
            Data.ContainsKey(Durable.Octree.Intensities1i)
            ;

        /// <summary></summary>
        [JsonIgnore]
        public Guid? IntensitiesId => Data.TryGetValue(Durable.Octree.Intensities1iReference, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<int[]> Intensities
        {
            get
            {
                if (PersistentRefs.TryGetValue(Durable.Octree.Intensities1iReference, out object o) && ((PersistentRef<int[]>)o).Id != GuidEmptyString)
                {
                    return (PersistentRef<int[]>)o;
                }
                else if (Data.TryGetValue(Durable.Octree.Intensities1i, out o))
                {
                    var xs = (int[])o;
                    return new PersistentRef<int[]>(Guid.Empty, _ => xs, _ => (true, xs));
                }
                else
                {
                    return null;
                }
            }
        }

#endregion

#region Classifications

        /// <summary></summary>
        [JsonIgnore]
        public bool HasClassifications =>
            Data.ContainsKey(Durable.Octree.Classifications1bReference) ||
            Data.ContainsKey(Durable.Octree.Classifications1b)
            ;

        /// <summary></summary>
        [JsonIgnore]
        public Guid? ClassificationsId => Data.TryGetValue(Durable.Octree.Classifications1bReference, out var id) ? (Guid?)id : null;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<byte[]> Classifications
        {
            get
            {
                if (PersistentRefs.TryGetValue(Durable.Octree.Classifications1bReference, out object o) && ((PersistentRef<byte[]>)o).Id != GuidEmptyString)
                {
                    return (PersistentRef<byte[]>)o;
                }
                else if (Data.TryGetValue(Durable.Octree.Classifications1b, out o))
                {
                    var xs = (byte[])o;
                    return new PersistentRef<byte[]>(Guid.Empty, _ => xs, _ => (true, xs));
                }
                else
                {
                    return null;
                }
            }
        }

#endregion

#region Velocities

        /// <summary>
        /// Deprecated. Always returns false. Use custom attributes instead.
        /// </summary>
        [Obsolete("Use custom attributes instead.")]
        public bool HasVelocities => false;

        /// <summary>
        /// Deprecated. Always returns null. Use custom attributes instead.
        /// </summary>
        [Obsolete("Use custom attributes instead.")]
        public PersistentRef<V3f[]> Velocities => null;

#endregion

#region KdTree

        /// <summary></summary>
        [JsonIgnore]
        public bool HasKdTree =>
            Data.ContainsKey(Durable.Octree.PointRkdTreeFDataReference) ||
            Data.ContainsKey(Durable.Octree.PointRkdTreeDDataReference)
            ;

        /// <summary></summary>
        [JsonIgnore]
        public Guid? KdTreeId =>
            Data.TryGetValue(Durable.Octree.PointRkdTreeFDataReference, out var id)
            ? (Guid?)id
            : (Data.TryGetValue(Durable.Octree.PointRkdTreeDDataReference, out var id2) ? (Guid?)id2 : null)
            ;

        /// <summary></summary>
        [JsonIgnore]
        public PersistentRef<PointRkdTreeF<V3f[], V3f>> KdTree =>
            PersistentRefs.TryGetValue(Durable.Octree.PointRkdTreeFDataReference, out object x)
            ? (PersistentRef<PointRkdTreeF<V3f[], V3f>>)x
            : null
            ;

#endregion

#region CentroidLocal

        /// <summary></summary>
        public bool HasCentroidLocal => PointCountCell > 0 || Data.ContainsKey(Durable.Octree.PositionsLocal3fCentroid);

        private V3f? m_centroid;
        private float? m_centroidStdDev;

        private static (V3f, float) ComputeStuffs(V3f[] ps)
        {
            if(ps.Length <= 0) return (V3f.NaN, 0);
            if(ps.Length <= 1) return (ps[0], 0);

            var sum = V3d.Zero;
            var sumSq = 0.0;
            foreach(var p in ps)
            {
                var pd = (V3d)p;
                sum += pd;
                sumSq += pd.LengthSquared;
            }

            var avg = sum / ps.Length;
            var avgSq = sumSq / (ps.Length - 1);
            var factor = (double)ps.Length / (ps.Length - 1);

            var variance = avgSq - factor * avg.LengthSquared;

            return ((V3f)avg, (float)Fun.Sqrt(variance));
        }

        /// <summary></summary>
        public V3f CentroidLocal
        {
            get
            {
                if (m_centroid != null) return m_centroid.Value;
                if (Data.TryGetValue(Durable.Octree.PositionsLocal3fCentroid, out var local))
                {
                    m_centroid = (V3f)local;
                    return m_centroid.Value;
                }
                var (centroid, stddev) = ComputeStuffs(Positions.Value);
                m_centroid = centroid;
                m_centroidStdDev = stddev;
                return m_centroid.Value;
            }
        }

        /// <summary></summary>
        public bool HasCentroidLocalStdDev => PointCountCell > 0 || Data.ContainsKey(Durable.Octree.PositionsLocal3fDistToCentroidStdDev);

        /// <summary></summary>
        public float CentroidLocalStdDev
        {
            get
            {
                if (m_centroidStdDev != null) return m_centroidStdDev.Value;
                if (Data.TryGetValue(Durable.Octree.PositionsLocal3fDistToCentroidStdDev, out var local))
                {
                    m_centroidStdDev = (float)local;
                    return m_centroidStdDev.Value;
                }
                var (centroid, stddev) = ComputeStuffs(Positions.Value);
                m_centroid = centroid;
                m_centroidStdDev = stddev;
                return m_centroidStdDev.Value;
            }
        }

#endregion

#region TreeDepth

            /// <summary></summary>
        public bool HasMinTreeDepth => Data.ContainsKey(Durable.Octree.MinTreeDepth);

        /// <summary></summary>
        public int MinTreeDepth => (int)Data.Get(Durable.Octree.MinTreeDepth);

        /// <summary></summary>
        public bool HasMaxTreeDepth => Data.ContainsKey(Durable.Octree.MaxTreeDepth);

        /// <summary></summary>
        public int MaxTreeDepth => (int)Data.Get(Durable.Octree.MaxTreeDepth);

#endregion

#region PointDistance

        /// <summary></summary>
        public bool HasPointDistanceAverage => Data.ContainsKey(Durable.Octree.AveragePointDistance);

        /// <summary></summary>
        public float PointDistanceAverage => (Data.TryGetValue(Durable.Octree.AveragePointDistance, out var value) && value is float x) ? x : -1.0f;

        /// <summary></summary>
        public bool HasPointDistanceStandardDeviation => Data.ContainsKey(Durable.Octree.AveragePointDistanceStdDev);

        /// <summary></summary>
        public float PointDistanceStandardDeviation => (Data.TryGetValue(Durable.Octree.AveragePointDistanceStdDev, out var value) && value is float x) ? x : -1.0f;

#endregion

        /// <summary>
        /// Subnodes (8), or null if leaf.
        /// </summary>
        [JsonIgnore]
        public readonly PersistentRef<IPointCloudNode>[] Subnodes;

        /// <summary>
        /// Center of this node's cell.
        /// </summary>
        [JsonIgnore]
        public readonly V3d Center;

        /// <summary>
        /// Corners of this node's cell.
        /// </summary>
        [JsonIgnore]
        public readonly V3d[] Corners;

        /// <summary>
        /// Node has no subnodes.
        /// </summary>
        [JsonIgnore]
        public bool IsLeaf => SubnodeIds == null;

        /// <summary>
        /// Node has subnodes.
        /// </summary>
        [JsonIgnore]
        public bool IsNotLeaf => SubnodeIds != null;
        
        /// <summary>
        /// Gets whether this node is centered at the origin.
        /// </summary>
        [JsonIgnore]
        public bool IsCenteredAtOrigin => Cell.IsCenteredAtOrigin;

        /// <summary>
        /// Gets number of subnodes.
        /// </summary>
        [JsonIgnore]
        public int SubnodeCount => SubnodeIds == null ? 0 : SubnodeIds.Count(x => x != null);

        #endregion

        #region Immutable updates (With...)

        /// <summary>
        /// Returns new node with replaced subnodes.
        /// Attention:
        /// All node properties (except Cell, BoundingBoxExactGlobal, and PointCountTreeLeafs) are removed, because they would no longer be valid for new subnode data.
        /// Use LodExtensions.GenerateLod to recompute these properties.
        /// </summary>
        public IPointCloudNode WithSubNodes(IPointCloudNode[] subnodes)
        {
            if (subnodes == null) throw new ArgumentNullException(nameof(subnodes));

#if DEBUG
            for (var i = 0; i < 8; i++)
            {
                var sn = subnodes[i]; if (sn == null) continue;
                if (sn.Cell.Exponent != this.Cell.Exponent - 1)
                {
                    throw new InvalidOperationException("Invariant c79cd9a4-3e44-46c8-9a7f-5f7e09627f1a.");
                }
            }
#endif

            var pointCountTree = subnodes.Sum(x => x?.PointCountTree);
            var bbExactGlobal = new Box3d(subnodes.Where(x => x != null).Select(x => x.BoundingBoxExactGlobal));

            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(Durable.Octree.NodeId, Guid.NewGuid())
                .Add(Durable.Octree.Cell, Cell)
                .Add(Durable.Octree.BoundingBoxExactGlobal, bbExactGlobal)
                .Add(Durable.Octree.PointCountTreeLeafs, pointCountTree ?? 0L)
                .Add(Durable.Octree.SubnodesGuids, subnodes.Map(x => x?.Id ?? Guid.Empty))
                ;
            if (IsTemporaryImportNode) data = data.Add(TemporaryImportNode, 0);

            var result = new PointSetNode(data, this.Storage, writeToStore: true);
            return result;
        }

        /// <summary>
        /// Returns new node (with new id) with added/replaced data.
        /// Node is NOT written to store.
        /// Call WriteToStore on the result if you want this.
        /// </summary>
        public PointSetNode With(IReadOnlyDictionary<Durable.Def, object> replacements)
        {
            if (replacements.ContainsKey(Durable.Octree.NodeId))
                throw new InvalidOperationException($"Node id must not be assigned manually. Error 20855582-cbbc-496a-8730-fa49d7af8f5b.");

            var data = Data
                .Remove(Durable.Octree.NodeId)
                .Add(Durable.Octree.NodeId, Guid.NewGuid())
                ;
            foreach (var kv in replacements) data = data.SetItem(kv.Key, kv.Value);

            return new PointSetNode(data, Storage, false);
        }

        /// <summary>
        /// Returns new node with removed data.
        /// </summary>
        public PointSetNode Without(params Durable.Def[] defs)
        {
            var data = Data
                .RemoveRange(defs)
                .Remove(Durable.Octree.NodeId)
                .Add(Durable.Octree.NodeId, Guid.NewGuid())
                ;
            return new PointSetNode(data, Storage, false);
        }

        /// <summary>
        /// Adds kd-tree to temporary import node.
        /// </summary>
        public PointSetNode WithComputedKdTree()
        {
            if (!IsTemporaryImportNode)
                throw new InvalidOperationException("Only allowed on temporary import nodes. Invariant de875e86-4c30-44d4-9e53-cbaf4e24854f.");

            if (HasKdTree) return this;

            var kdId = ComputeAndStoreKdTree(Storage, Positions.Value);
            return With(ImmutableDictionary<Durable.Def, object>.Empty.Add(Durable.Octree.PointRkdTreeFDataReference, kdId));
        }

        /// <summary>
        /// Computes and stores kd-tree from given points.
        /// Returns id of stored kd-tree.
        /// </summary>
        private static Guid ComputeAndStoreKdTree(Storage storage, V3f[] ps)
        {
            var kdTree = new PointRkdTreeF<V3f[], V3f>(
                3, ps.Length, ps,
                (xs, i) => xs[(int)i], (v, i) => (float)v[i],
                (a, b) => Vec.Distance(a, b), (i, a, b) => b - a,
                (a, b, c) => Vec.DistanceToLine(a, b, c), Fun.Lerp, 0.000001f
                );
            Guid kdId = Guid.NewGuid();
            storage.Add(kdId, kdTree.Data);
            return kdId;
        }

#endregion

#region Durable codec

        /// <summary>
        /// </summary>
        public byte[] Encode() => Aardvark.Data.Codec.Serialize(Durable.Octree.Node, Data);

        /// <summary>
        /// </summary>
        public static PointSetNode Decode(Storage storage, byte[] buffer)
        {
            var (def, o) = Aardvark.Data.Codec.Deserialize(buffer);
            if (def != Durable.Octree.Node) throw new InvalidOperationException("Invariant 5cc4cbfe-07c8-4b92-885d-b1d397210e41.");
            var data = (ImmutableDictionary<Durable.Def, object>)o;
            return new PointSetNode(data, storage, false);
        }

#endregion

#region IPointCloudNode

        Guid IPointCloudNode.Id => Id;

        Cell IPointCloudNode.Cell => Cell;

        V3d IPointCloudNode.Center => Center;

        long IPointCloudNode.PointCountTree => PointCountTree;

        /// <summary></summary>
        public bool Has(Durable.Def what) => Data.ContainsKey(what);

        /// <summary></summary>
        public bool TryGetValue(Durable.Def what, out object o) => Data.TryGetValue(what, out o);

        /// <summary></summary>
        public IReadOnlyDictionary<Durable.Def, object> Properties => Data;

        Storage IPointCloudNode.Storage => Storage;

        /// <summary></summary>
        public bool IsMaterialized => true;

        /// <summary></summary>
        public IPointCloudNode Materialize() => this;

        PersistentRef<IPointCloudNode>[] IPointCloudNode.Subnodes => Subnodes;

        public Box3d BoundingBoxApproximate
        {
            get
            {
                var cellBounds = Cell.BoundingBox;
                if(HasBoundingBoxExactGlobal)
                {
                    var be = BoundingBoxExactGlobal;
                    if(be != cellBounds) return be;
                }

                if (HasBoundingBoxExactLocal)
                {
                    return (Box3d)BoundingBoxExactLocal + Center;
                }

                if(HasPositions)
                {
                    var ps = Positions.Value;
                    if (ps.Length > 0) return (Box3d)(new Box3f(ps)) + Center;
                }

                return cellBounds;
            }        
        }

        IPointCloudNode IPointCloudNode.With(IReadOnlyDictionary<Durable.Def, object> replacements) => With(replacements);

#endregion
    }
}
