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
            var root = builder.ToPointSetNode(storage, ct: ct);

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
        public PointSet(Storage storage, string key, Guid? rootCellId, int splitLimit)
        {
            Storage = storage;
            Id = key ?? throw new ArgumentNullException(nameof(key));
            SplitLimit = splitLimit;

            if (rootCellId.HasValue)
            {
                Root = new PersistentRef<IPointCloudNode>(rootCellId.Value.ToString(), storage.GetPointCloudNode,
                    k => { var (a, b) = storage.TryGetPointCloudNode(k); return (a, b); }
                    );
            }
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

            if (key != null)
            {
                Root = new PersistentRef<IPointCloudNode>(root.Id.ToString(), storage.GetPointCloudNode, storage.TryGetPointCloudNode);
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
        public int SplitLimit { get; }
        
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
                RootCellId = Root?.Id,
                OctreeId = Root?.Id,
                SplitLimit
            });
        }

        /// <summary>
        /// </summary>
        public static PointSet Parse(JObject json, Storage storage)
        {
            var octreeId = (string)json["OctreeId"] ?? (string)json["RootCellId"];
            var octree = octreeId != null
                ? new PersistentRef<IPointCloudNode>(octreeId, storage.GetPointCloudNode, storage.TryGetPointCloudNode)
                : null
                ;
            
            // backwards compatibility: if split limit is not set, guess as number of points in root cell
            var splitLimitRaw = (string)json["SplitLimit"];
            var splitLimit = splitLimitRaw != null ? int.Parse(splitLimitRaw) : 8192;

            // id
            var id = (string)json["Id"];

            //
            return new PointSet(storage, id, octree.Value, splitLimit);
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
        public bool IsEmpty => Root == null;

        /// <summary>
        /// Gets total number of points in dataset.
        /// </summary>
        public long PointCount => Root?.Value?.PointCountTree ?? 0;

        /// <summary>
        /// Gets bounds of dataset root cell.
        /// </summary>
        public Box3d Bounds => Root?.Value?.Cell.BoundingBox ?? Box3d.Invalid;

        /// <summary>
        /// Gets exact bounding box of all points from coarsest LoD.
        /// </summary>
        public Box3d BoundingBox
        {
            get
            {
                try
                {
                    return new Box3d(Root.Value.PositionsAbsolute);
                }
                catch (NullReferenceException)
                {
                    return Box3d.Invalid;
                }
            }
        }

        /// <summary></summary>
        public bool HasColors => Root != null ? Root.Value.HasColors : false;

        /// <summary></summary>
        public bool HasIntensities => Root != null ? Root.Value.HasIntensities : false;
        
        /// <summary></summary>
        public bool HasClassifications => Root != null ? Root.Value.HasClassifications : false;

        /// <summary></summary>
        public bool HasKdTree => Root != null ? Root.Value.HasKdTree : false;
        
        /// <summary></summary>
        public bool HasNormals => Root != null ? Root.Value.HasNormals : false;

        /// <summary></summary>
        public bool HasPositions => Root != null ? Root.Value.HasPositions : false;

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
                return new PointSet(Storage, id, merged.Id, SplitLimit);
            }
            else
            {
                throw new InvalidOperationException($"Cannot merge {Root.Value.GetType()} with {other.Root.Value.GetType()}.");
            }
        }

        #endregion
    }
}
