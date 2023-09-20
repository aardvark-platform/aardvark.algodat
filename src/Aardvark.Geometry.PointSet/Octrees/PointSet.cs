﻿/*
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
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// An immutable set of points.
    /// </summary>
    public class PointSet
    {
        /// <summary>
        /// The empty pointset.
        /// </summary>
        public static readonly PointSet Empty = new(null!, "PointSet.Empty");

        #region Construction

        /// <summary>
        /// Creates PointSet from given points and colors.
        /// </summary>
        public static PointSet Create(Storage storage, string key,
            IList<V3d> positions, IList<C4b> colors, IList<V3f> normals, IList<int> intensities, IList<byte> classifications, object? partIndices,
            int octreeSplitLimit, bool generateLod, bool isTemporaryImportNode, CancellationToken ct = default
            )
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var bounds = new Box3d(positions);
            var builder = InMemoryPointSet.Build(positions, colors, normals, intensities, classifications, partIndices, new Cell(bounds), octreeSplitLimit);
            var root = builder.ToPointSetNode(storage, isTemporaryImportNode);

            var result = new PointSet(storage, key, root.Id, octreeSplitLimit);

            if (result.Root.Value == null) throw new InvalidOperationException("Invariant 5492d57b-add1-48bf-9721-5087c957d81e.");

            var config = ImportConfig.Default
                .WithRandomKey()
                .WithCancellationToken(ct)
                ;

            if (generateLod)
            {
                result = result.GenerateLod(config);
            }

            return result;
        }

        /// <summary>
        /// Creates pointset from given root cell.
        /// </summary>
        public PointSet(Storage storage, string key, Guid rootCellId, int splitLimit)
        {
            Storage = storage;
            Id = key ?? throw new ArgumentNullException(nameof(key));
            SplitLimit = splitLimit;

            Root = new PersistentRef<IPointCloudNode>(rootCellId.ToString(), storage.GetPointCloudNode!,
                k => { var (a, b) = storage.TryGetPointCloudNode(k); return (a, b); }
                );
        }

        /// <summary>
        /// Creates pointset from given root cell.
        /// </summary>
        public PointSet(Storage storage, string key, IPointCloudNode root, int splitLimit)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            Storage = storage;
            Id = key ?? throw new ArgumentNullException(nameof(key));
            SplitLimit = splitLimit;

            Root = new PersistentRef<IPointCloudNode>(root.Id.ToString(), storage.GetPointCloudNode!, storage.TryGetPointCloudNode);
        }

        /// <summary>
        /// Creates empty pointset.
        /// </summary>
        public PointSet(Storage storage, string key)
        {
            Storage = storage;
            Id = key ?? throw new ArgumentNullException(nameof(key));
            SplitLimit = 0;

            Root = null!;
        }

        #endregion

        #region Properties (state to serialize)

        /// <summary>
        /// </summary>
        public string Id { get; init; }

        /// <summary>
        /// </summary>
        public int SplitLimit { get; init; }
        
        /// <summary>
        /// </summary>
        public PersistentRef<IPointCloudNode> Root { get; init; }

        /// <summary>
        /// Range (inclusive) of part indices, or invalid range if no part indices are stored.
        /// </summary>
        public Range1i PartIndexRange { get; init; } = Range1i.Invalid;

        #endregion

        #region Json

        /// <summary>
        /// </summary>
        public JsonNode ToJson() => JsonSerializer.SerializeToNode(new
        {
            Id,
            RootCellId = Root.Id,
            OctreeId = Root.Id,
            SplitLimit,
            PartIndexRange
        })!;

        /// <summary>
        /// </summary>
        public static PointSet Parse(JsonNode json, Storage storage)
        {
            var o = json.AsObject() ?? throw new Exception($"Expected JSON object, but found {json}.");

            var octreeId = (string?)o["OctreeId"] ?? (string?)o["RootCellId"];
            var octree = octreeId != null
                ? new PersistentRef<IPointCloudNode>(octreeId, storage.GetPointCloudNode!, storage.TryGetPointCloudNode)
                : null 
                ;

            // backwards compatibility: if split limit is not set, guess as number of points in root cell
            var splitLimit = o.TryGetPropertyValue("SplitLimit", out var x) ? (int)x! : 8192;

            // id
            var id = (string?)o["Id"] ?? throw new Exception("Missing id. Error 71730558-699e-4128-b0f2-130fd04672e9.");

            // part index range (JsonArray)
            var partIndexRangeArray = (JsonArray?)o["PartIndexRange"];
            var partIndexRange = partIndexRangeArray != null
                ? new Range1i((int)partIndexRangeArray[0]!, (int)partIndexRangeArray[1]!)
                : Range1i.Invalid
                ;

            //
            return new PointSet(storage, id, octree?.Value ?? PointSetNode.Empty, splitLimit).WithPartIndexRange(partIndexRange);
        }

        #endregion

        #region Properties (derived, non-serialized)

        /// <summary>
        /// </summary>
        [JsonIgnore]
        public Storage Storage { get; init; }

        /// <summary>
        /// Returns true if pointset is empty.
        /// </summary>
        public bool IsEmpty => Root == null || Root.Id == Guid.Empty.ToString();

        /// <summary>
        /// Gets total number of points in dataset.
        /// </summary>
        public long PointCount => IsEmpty ? 0 : (Root?.Value?.PointCountTree ?? 0);

        /// <summary>
        /// Gets bounds of dataset root cell.
        /// </summary>
        public Box3d Bounds => Root?.Value?.Cell.BoundingBox ?? Box3d.Invalid;

        /// <summary>
        /// Gets exact bounding box of all points in pointcloud.
        /// </summary>
        public Box3d BoundingBox
        {
            get
            {
                try
                {
                    return Root.Value.BoundingBoxExactGlobal;
                }
                catch (NullReferenceException)
                {
                    return Box3d.Invalid;
                }
            }
        }

        /// <summary></summary>
        public bool HasColors => Root != null && Root.Value.HasColors;

        /// <summary></summary>
        public bool HasIntensities => Root != null && Root.Value.HasIntensities;
        
        /// <summary></summary>
        public bool HasClassifications => Root != null && Root.Value.HasClassifications;

        /// <summary></summary>
        public bool HasKdTree => Root != null && Root.Value.HasKdTree;
        
        /// <summary></summary>
        public bool HasNormals => Root != null && Root.Value.HasNormals;

        /// <summary></summary>
        public bool HasPositions => Root != null && Root.Value.HasPositions;


        /// <summary></summary>
        public bool HasPartIndexRange => PartIndexRange.IsValid;

        #endregion

        #region Immutable operations

        /// <summary>
        /// </summary>
        public PointSet Merge(PointSet other, Action<long> pointsMergedCallback, ImportConfig config)
        {
            if (other.IsEmpty) return this;
            if (this.IsEmpty) return other;
            if (this.Storage != other.Storage) throw new InvalidOperationException("Invariant 3267c283-3192-438b-a219-821d67ac5061.");

            if (Root.Value is PointSetNode root && other.Root.Value is PointSetNode otherRoot)
            {
                var merged = root.Merge(otherRoot, pointsMergedCallback, config);
                var id = $"{Guid.NewGuid()}.json";
                return new PointSet(Storage, id, merged.Item1.Id, SplitLimit);
            }
            else
            {
                throw new InvalidOperationException($"Cannot merge {Root.Value.GetType()} with {other.Root.Value.GetType()}.");
            }
        }

        //public PointSet WithPartIndexRange(Range1i x) => new() { Id = Id, PartIndexRange = x, Root = Root, SplitLimit = SplitLimit, Storage = Storage };

        public PointSet WithPartIndexRange(Range1i x) => new(Storage, Id, Guid.Parse(Root.Id), SplitLimit) { PartIndexRange = x };

        #endregion
    }
}
