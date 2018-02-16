/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Threading;
using Aardvark.Base;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// An immutable set of colored points.
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
        public static PointSet Create(Storage storage, string key, IList<V3d> ps, IList<C4b> cs, IList<V3f> ns, int octreeSplitLimit, bool buildKdTree, CancellationToken ct)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var bounds = new Box3d(ps);
            var builder = InMemoryPointSet.Build(ps, cs, ns, bounds, octreeSplitLimit);
            var root = builder.ToPointSetCell(storage, ct: ct);
            var result = new PointSet(storage, key, root.Id, octreeSplitLimit);
            var config = new ImportConfig { Key = Guid.NewGuid().ToString(), CancellationToken = ct };
            if (buildKdTree) result = result.GenerateLod(config);
            return result;
        }

        /// <summary>
        /// </summary>
        internal PointSet(Storage storage, string key, Guid? rootCellId, long splitLimit)
        {
            Storage = storage;
            Id = key ?? throw new ArgumentNullException(nameof(key));
            SplitLimit = splitLimit;
            if (rootCellId.HasValue)
            {
                Root = new PersistentRef<PointSetNode>(rootCellId.ToString(), storage.GetPointSetNode);
            }
        }

        /// <summary>
        /// Creates empty pointset.
        /// </summary>
        internal PointSet(Storage storage, string key)
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
        public PersistentRef<PointSetNode> Root { get; }

        #endregion

        #region Json

        /// <summary>
        /// </summary>
        public JObject ToJson()
        {
            return JObject.FromObject(new
            {
                Id = Id,
                RootCellId = Root?.Id,
                SplitLimit = SplitLimit
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

            return new PointSet(storage, (string)json["Id"], Guid.Parse(rootCellId), splitLimit);
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
        /// Gets bounding box of dataset.
        /// </summary>
        public Box3d Bounds => Root?.Value?.BoundingBox ?? Box3d.Invalid;

        #endregion

        #region Immutable operations

        /// <summary>
        /// </summary>
        public PointSet Merge(PointSet other, CancellationToken ct)
        {
            if (other.IsEmpty) return this;
            if (this.IsEmpty) return other;
            if (this.Storage != other.Storage) throw new InvalidOperationException();

            var merged = Root.Value.Merge(other.Root.Value, SplitLimit, ct);
            var id = $"{Guid.NewGuid()}.json";
            return new PointSet(Storage, id, merged.Id, SplitLimit);
        }

        #endregion
    }
}
