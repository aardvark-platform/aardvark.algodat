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
using System;
using System.Collections.Generic;
using System.Threading;
using Aardvark.Base;
using Uncodium.SimpleStore;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// General info for a point cloud data file.
    /// </summary>
    public class ImportConfig
    {
        /// <summary>
        /// Default configuration.
        /// </summary>
        public static readonly ImportConfig Default = new ImportConfig();

        #region Properties

        /// <summary></summary>
        public CancellationToken CancellationToken { get; private set; } = CancellationToken.None;
        
        /// <summary>
        /// Store imported pointcloud with this key.
        /// </summary>
        public string Key { get; private set; } = null;

        /// <summary></summary>
        public int MaxDegreeOfParallelism { get; private set; } = 0;

        /// <summary>
        /// Remove points on import with less than this distance to previous point.
        /// </summary>
        public double MinDist { get; private set; } = 0.0;

        /// <summary>Removes duplicate points in chunk after MinDist filtering and before Reproject and EstimateNormals.</summary>
        public bool DeduplicateChunks { get; private set; } = true;

        /// <summary>
        /// Max number of points in octree cell.
        /// </summary>
        public int OctreeSplitLimit { get; private set; } = 8192;

        /// <summary></summary>
        public Action<double> ProgressCallback { get; private set; } = _ => { };

        /// <summary>
        /// Large files should be read in chunks with this maximum size.
        /// </summary>
        public int ReadBufferSizeInBytes { get; private set; } = 256 * 1024 * 1024;

        /// <summary></summary>
        public int MaxChunkPointCount { get; private set; } = 1024 * 1024;

        /// <summary></summary>
        public Func<IList<V3d>, IList<V3d>> Reproject { get; private set; } = null;

        /// <summary>
        /// Positions -> Normals.
        /// </summary>
        public Func<IList<V3d>, IList<V3f>> EstimateNormals { get; private set; } = null;

        /// <summary></summary>
        public Storage Storage { get; private set; } = null;

        /// <summary></summary>
        public bool Verbose { get; private set; } = false;

        #endregion

        #region Immutable updates

        private ImportConfig() { }

        /// <summary></summary>
        public ImportConfig(ImportConfig x)
        {
            CancellationToken = x.CancellationToken;
            Key = x.Key;
            MaxDegreeOfParallelism = x.MaxDegreeOfParallelism;
            MinDist = x.MinDist;
            DeduplicateChunks = x.DeduplicateChunks;
            OctreeSplitLimit = x.OctreeSplitLimit;
            ProgressCallback = x.ProgressCallback;
            ReadBufferSizeInBytes = x.ReadBufferSizeInBytes;
            MaxChunkPointCount = x.MaxChunkPointCount;
            Reproject = x.Reproject;
            EstimateNormals = x.EstimateNormals;
            Storage = x.Storage;
            Verbose = x.Verbose;
        }

        /// <summary></summary>
        public ImportConfig WithCancellationToken(CancellationToken x) => new ImportConfig(this) { CancellationToken = x };
        
        /// <summary></summary>
        public ImportConfig WithKey(string x) => new ImportConfig(this) { Key = x };

        /// <summary></summary>
        public ImportConfig WithRandomKey() => WithKey(Guid.NewGuid().ToString());

        /// <summary></summary>
        public ImportConfig WithMaxDegreeOfParallelism(int x) => new ImportConfig(this) { MaxDegreeOfParallelism = x };

        /// <summary></summary>
        public ImportConfig WithMinDist(double x) => new ImportConfig(this) { MinDist = x };

        /// <summary></summary>
        public ImportConfig WithDeduplicateChunks(bool x) => new ImportConfig(this) { DeduplicateChunks = x };

        /// <summary></summary>
        public ImportConfig WithOctreeSplitLimit(int x) => new ImportConfig(this) { OctreeSplitLimit = x };

        /// <summary></summary>
        public ImportConfig WithProgressCallback(Action<double> x) => new ImportConfig(this) { ProgressCallback = x ?? throw new ArgumentNullException() };

        /// <summary></summary>
        public ImportConfig WithReadBufferSizeInBytes(int x) => new ImportConfig(this) { ReadBufferSizeInBytes = x };

        /// <summary></summary>
        public ImportConfig WithMaxChunkPointCount(int x) => new ImportConfig(this) { MaxChunkPointCount = Math.Max(x, 1) };

        /// <summary></summary>
        public ImportConfig WithReproject(Func<IList<V3d>, IList<V3d>> x) => new ImportConfig(this) { Reproject = x };

        /// <summary></summary>
        public ImportConfig WithEstimateNormals(Func<IList<V3d>, IList<V3f>> x) => new ImportConfig(this) { EstimateNormals = x };

        /// <summary></summary>
        public ImportConfig WithStorage(Storage x) => new ImportConfig(this) { Storage = x };

        /// <summary></summary>
        public ImportConfig WithVerbose(bool x) => new ImportConfig(this) { Verbose = x };

        #endregion
    }
}
