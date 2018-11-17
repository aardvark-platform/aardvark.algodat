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
        public PointSet(Storage storage, string key, Guid? rootCellId, long splitLimit)
        {
            Storage = storage;
            Id = key ?? throw new ArgumentNullException(nameof(key));
            SplitLimit = splitLimit;
            OctreeRootType = typeof(PointSetNode).Name;

            if (rootCellId.HasValue)
            {
                Octree = new PersistentRef<IPointCloudNode>(rootCellId.ToString(), storage.GetPointSetNode);
                Root = new PersistentRef<PointSetNode>(rootCellId.ToString(), storage.GetPointSetNode);
            }
        }

        /// <summary>
        /// Creates pointset from given root cell.
        /// </summary>
        public PointSet(Storage storage, IStoreResolver resolver, string key, IPointCloudNode root, int splitLimit)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));

            Storage = storage;
            Id = key ?? throw new ArgumentNullException(nameof(key));
            SplitLimit = splitLimit;
            OctreeRootType = root.NodeType;

            var oldSchool = root as PointSetNode;

            if (key != null)
            {
                Octree = new PersistentRef<IPointCloudNode>(root.Id, (id, ct) => storage.GetPointCloudNode(id, resolver, ct));
                Root = oldSchool != null
                    ? new PersistentRef<PointSetNode>(oldSchool.Id.ToString(), storage.GetPointSetNode)
                    : new PersistentRef<PointSetNode>(root.Id, (_, __) => throw new InvalidOperationException());
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
        [Obsolete("Use Octree instead.")]
        public PersistentRef<PointSetNode> Root { get; }

        /// <summary>
        /// </summary>
        public string OctreeRootType { get; }

        /// <summary>
        /// </summary>
        public PersistentRef<IPointCloudNode> Octree { get; }

        #endregion

        #region Json

        /// <summary>
        /// </summary>
        public JObject ToJson()
        {
            return JObject.FromObject(new
            {
                Id,
                RootCellId = Octree?.Id,
                OctreeId = Octree?.Id,
                SplitLimit,
                OctreeRootType
            });
        }

        /// <summary>
        /// </summary>
        public static PointSet Parse(JObject json, Storage storage, IStoreResolver resolver)
        {
            var octreeId = (string)json["OctreeId"];
            if (octreeId == null) octreeId = (string)json["RootCellId"]; // backwards compatibility
            var octree = octreeId != null ? new PersistentRef<IPointCloudNode>(octreeId, storage.GetPointSetNode) : null;
            
            // backwards compatibility: if split limit is not set, guess as number of points in root cell
            var splitLimitRaw = (string)json["SplitLimit"];
            var splitLimit = splitLimitRaw != null ? int.Parse(splitLimitRaw) : 8192;

            // id
            var id = (string)json["Id"];

            //
            var rootType = (string)json["RootType"] ?? typeof(PointSetNode).ToString();
            if (rootType == "PointSetNode")
                return new PointSet(storage, id, Guid.Parse(octreeId), splitLimit); // backwards compatibility
            else
                return new PointSet(storage, resolver, id, octree.Value, splitLimit);
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
        public Box3d Bounds => Root?.Value?.BoundingBoxExact ?? Box3d.Invalid;

        /// <summary>
        /// Gets exact bounding box of all points from coarsest LoD.
        /// </summary>
        public Box3d BoundingBox
        {
            get
            {
                try
                {
                    return Root.Value.HasPositions()
                        ? new Box3d(Root.Value.GetPositionsAbsolute())
                        : new Box3d(Root.Value.GetLodPositionsAbsolute())
                        ;
                }
                catch (NullReferenceException)
                {
                    return Box3d.Invalid;
                }
            }
        }

        /// <summary></summary>
        public bool HasColors => Root != null ? Root.GetValue(default).HasColors() : false;

        /// <summary></summary>
        public bool HasIntensities => Root != null ? Root.GetValue(default).HasIntensities() : false;
        
        /// <summary></summary>
        public bool HasClassifications => Root != null ? Root.GetValue(default).HasClassifications() : false;

        /// <summary></summary>
        public bool HasKdTree => Root != null ? Root.GetValue(default).HasKdTree() : false;
        
        /// <summary></summary>
        public bool HasLodColors => Root != null ? Root.GetValue(default).HasLodColors() : false;

        /// <summary></summary>
        public bool HasLodIntensities => Root != null ? Root.GetValue(default).HasLodIntensities() : false;
        
        /// <summary></summary>
        public bool HasLodClassifications => Root != null ? Root.GetValue(default).HasLodClassifications() : false;

        /// <summary></summary>
        public bool HasLodKdTree => Root != null ? Root.GetValue(default).HasLodKdTree() : false;

        /// <summary></summary>
        public bool HasLodNormals => Root != null ? Root.GetValue(default).HasLodNormals() : false;

        /// <summary></summary>
        public bool HasLodPositions => Root != null ? Root.GetValue(default).HasLodPositions() : false;

        /// <summary></summary>
        public bool HasNormals => Root != null ? Root.GetValue(default).HasNormals() : false;

        /// <summary></summary>
        public bool HasPositions => Root != null ? Root.GetValue(default).HasPositions() : false;

        #endregion

        #region Immutable operations

        /// <summary>
        /// </summary>
        public PointSet Merge(PointSet other, CancellationToken ct)
        {
            if (other.IsEmpty) return this;
            if (this.IsEmpty) return other;
            if (this.Storage != other.Storage) throw new InvalidOperationException();


            if (Root.Value is PointSetNode root && other.Root.Value is PointSetNode otherRoot)
            {
                var merged = root.Merge(otherRoot, SplitLimit, ct);
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
