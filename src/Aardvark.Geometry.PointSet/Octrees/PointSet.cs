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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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
        public static readonly PointSet Empty = new PointSet(null, "PointSet.Empty");

        #region Construction

        /// <summary>
        /// Creates PointSet from given points and colors.
        /// </summary>
        public static PointSet Create(Storage storage, string key,
            IList<V3d> positions, IList<C4b> colors, IList<V3f> normals, IList<int> intensities, IList<byte> classifications,
            int octreeSplitLimit, bool generateLod, CancellationToken ct
            )
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var bounds = new Box3d(positions);
            var builder = InMemoryPointSet.Build(positions, colors, normals, intensities, classifications, bounds, octreeSplitLimit);
            var root = builder.ToPointSetCell(storage, ct: ct);
            var result = new PointSet(storage, key, root.Id, octreeSplitLimit, typeof(PointSetNode).Name);
            var config = ImportConfig.Default
                .WithRandomKey()
                .WithCancellationToken(ct)
                ;
            if (generateLod) result = result.GenerateLod(config);
            return result;
        }

        /// <summary>
        /// Creates pointset from given root cell.
        /// </summary>
        public PointSet(Storage storage, string key, Guid? rootCellId, long splitLimit, string rootType)
        {
            Storage = storage;
            Id = key ?? throw new ArgumentNullException(nameof(key));
            SplitLimit = splitLimit;
            RootType = rootType ?? throw new ArgumentNullException(nameof(rootType));

            if (rootCellId.HasValue)
            {
                if (rootType == typeof(PointSetNode).Name)
                {
                    Root = new PersistentRef<IPointCloudNode>(rootCellId.ToString(), storage.GetPointSetNode);
                    OldRoot = new PersistentRef<PointSetNode>(rootCellId.ToString(), storage.GetPointSetNode);
                }
                else
                {
                    Root = new PersistentRef<IPointCloudNode>(rootCellId.ToString(), storage.GetPointCloudNode);
                    OldRoot = new PersistentRef<PointSetNode>(rootCellId.ToString(), (_, __) => throw new InvalidOperationException());
                }
            }
        }

        /// <summary>
        /// Creates empty pointset.
        /// </summary>
        public PointSet(Storage storage, string key)
        {
            Storage = storage;
            Id = key ?? throw new ArgumentNullException(nameof(key));
            SplitLimit = 0;
        }

        #endregion

        #region Properties (state to serialize)

        /// <summary>
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// </summary>
        public long SplitLimit { get; }
        
        /// <summary>
        /// </summary>
        public PersistentRef<PointSetNode> OldRoot { get; }

        /// <summary>
        /// </summary>
        public string RootType { get; }

        /// <summary>
        /// </summary>
        public PersistentRef<IPointCloudNode> Root { get; }

        #endregion

        #region Json

        /// <summary>
        /// </summary>
        public JObject ToJson()
        {
            return JObject.FromObject(new
            {
                Id,
                RootCellId = OldRoot?.Id,
                SplitLimit,
                RootType
            });
        }

        /// <summary>
        /// </summary>
        public static PointSet Parse(JObject json, Storage storage)
        {
            var rootCellId = (string)json["RootCellId"];
            var root = rootCellId != null ? new PersistentRef<PointSetNode>(rootCellId, storage.GetPointSetNode) : null;
            
            // backwards compatibility: if split limit is not set, guess as number of points in root cell
            var splitLimitRaw = (string)json["SplitLimit"];
            var splitLimit = splitLimitRaw != null ? long.Parse(splitLimitRaw) : root.Value.PointCount;

            //
            var rootType = (string)json["RootType"] ?? typeof(PointSetNode).ToString();

            return new PointSet(storage, (string)json["Id"], Guid.Parse(rootCellId), splitLimit, typeof(PointSetNode).Name);
        }

        #endregion

        #region Properties (derived, non-serialized)

        /// <summary>
        /// </summary>
        [JsonIgnore]
        public readonly Storage Storage;

        /// <summary>
        /// Returns true if pointset is empty.
        /// </summary>
        public bool IsEmpty => OldRoot == null;

        /// <summary>
        /// Gets total number of points in dataset.
        /// </summary>
        public long PointCount => OldRoot?.Value?.PointCountTree ?? 0;

        /// <summary>
        /// Gets bounds of dataset root cell.
        /// </summary>
        public Box3d Bounds => OldRoot?.Value?.BoundingBoxExact ?? Box3d.Invalid;

        /// <summary>
        /// Gets exact bounding box of all points from coarsest LoD.
        /// </summary>
        public Box3d BoundingBox
        {
            get
            {
                try
                {
                    return OldRoot.Value.HasPositions()
                        ? new Box3d(OldRoot.Value.GetPositionsAbsolute())
                        : new Box3d(OldRoot.Value.GetLodPositionsAbsolute())
                        ;
                }
                catch (NullReferenceException)
                {
                    return Box3d.Invalid;
                }
            }
        }

        /// <summary></summary>
        public bool HasColors => OldRoot != null ? OldRoot.GetValue(default).HasColors() : false;

        /// <summary></summary>
        public bool HasIntensities => OldRoot != null ? OldRoot.GetValue(default).HasIntensities() : false;
        
        /// <summary></summary>
        public bool HasClassifications => OldRoot != null ? OldRoot.GetValue(default).HasClassifications() : false;

        /// <summary></summary>
        public bool HasKdTree => OldRoot != null ? OldRoot.GetValue(default).HasKdTree() : false;
        
        /// <summary></summary>
        public bool HasLodColors => OldRoot != null ? OldRoot.GetValue(default).HasLodColors() : false;

        /// <summary></summary>
        public bool HasLodIntensities => OldRoot != null ? OldRoot.GetValue(default).HasLodIntensities() : false;
        
        /// <summary></summary>
        public bool HasLodClassifications => OldRoot != null ? OldRoot.GetValue(default).HasLodClassifications() : false;

        /// <summary></summary>
        public bool HasLodKdTree => OldRoot != null ? OldRoot.GetValue(default).HasLodKdTree() : false;

        /// <summary></summary>
        public bool HasLodNormals => OldRoot != null ? OldRoot.GetValue(default).HasLodNormals() : false;

        /// <summary></summary>
        public bool HasLodPositions => OldRoot != null ? OldRoot.GetValue(default).HasLodPositions() : false;

        /// <summary></summary>
        public bool HasNormals => OldRoot != null ? OldRoot.GetValue(default).HasNormals() : false;

        /// <summary></summary>
        public bool HasPositions => OldRoot != null ? OldRoot.GetValue(default).HasPositions() : false;

        #endregion

        #region Immutable operations

        /// <summary>
        /// </summary>
        public PointSet Merge(PointSet other, CancellationToken ct)
        {
            if (other.IsEmpty) return this;
            if (this.IsEmpty) return other;
            if (this.Storage != other.Storage) throw new InvalidOperationException();


            if (OldRoot.Value is PointSetNode root && other.OldRoot.Value is PointSetNode otherRoot)
            {
                var merged = root.Merge(otherRoot, SplitLimit, ct);
                var id = $"{Guid.NewGuid()}.json";
                return new PointSet(Storage, id, merged.Id, SplitLimit, typeof(PointSetNode).Name);
            }
            else
            {
                throw new InvalidOperationException($"Cannot merge {OldRoot.Value.GetType()} with {other.OldRoot.Value.GetType()}.");
            }
        }

        #endregion
    }
}
