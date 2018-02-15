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
using Uncodium.SimpleStore;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// General info for a point cloud data file.
    /// </summary>
    public class ImportConfig
    {
        public static readonly ImportConfig Default = new ImportConfig();

        #region Properties

        public CancellationToken CancellationToken { get; private set; } = CancellationToken.None;

        public bool CreateOctreeLod { get; private set; } = false;

        public string Key { get; private set; } = null;

        public int MaxDegreeOfParallelism { get; private set; } = 0;

        public double MinDist { get; private set; } = 0.0;

        public int OctreeSplitLimit { get; private set; } = 8192;

        public Action<double> ProgressCallback { get; private set; } = _ => { };

        /// <summary>
        /// Large files should be read in chunks with this maximum size.
        /// </summary>
        public int ReadBufferSizeInBytes { get; private set; } = 4 * 1024 * 1024;
        
        public Func<IList<V3d>, IList<V3d>> Reproject { get; private set; } =  null;
        
        public Storage Storage { get; private set; } = null;
        
        public bool Verbose { get; private set; } = false;
        
        #endregion

        #region Immutable updates

        private ImportConfig() { }
        
        public ImportConfig(ImportConfig x)
        {
            CancellationToken = x.CancellationToken;
            CreateOctreeLod = x.CreateOctreeLod;
            Key = x.Key;
            MaxDegreeOfParallelism = x.MaxDegreeOfParallelism;
            MinDist = x.MinDist;
            OctreeSplitLimit = x.OctreeSplitLimit;
            ProgressCallback = x.ProgressCallback;
            ReadBufferSizeInBytes = x.ReadBufferSizeInBytes;
            Reproject = x.Reproject;
            Storage = x.Storage;
            Verbose = x.Verbose;
        }

        public ImportConfig WithCancellationToken(CancellationToken x) => new ImportConfig(this) { CancellationToken = x };

        public ImportConfig WithCreateOctreeLod(bool x) => new ImportConfig(this) { CreateOctreeLod = x };

        public ImportConfig WithKey(string x) => new ImportConfig(this) { Key = x };
        
        public ImportConfig WithRandomKey() => WithKey(Guid.NewGuid().ToString());

        public ImportConfig WithMaxDegreeOfParallelism(int x) => new ImportConfig(this) { MaxDegreeOfParallelism = x };

        public ImportConfig WithMinDist(double x) => new ImportConfig(this) { MinDist = x };

        public ImportConfig WithOctreeSplitLimit(int x) => new ImportConfig(this) { OctreeSplitLimit = x };

        public ImportConfig WithProgressCallback(Action<double> x) => new ImportConfig(this) { ProgressCallback = x ?? throw new ArgumentNullException() };

        public ImportConfig WithReadBufferSizeInBytes(int x) => new ImportConfig(this) { ReadBufferSizeInBytes = x };

        public ImportConfig WithStorage(Storage x) => new ImportConfig(this) { Storage = x };

        public ImportConfig WithInMemoryStore() => new ImportConfig(this) { Storage = new SimpleMemoryStore().ToPointCloudStore() };

        public ImportConfig WithVerbose(bool x) => new ImportConfig(this) { Verbose = x };

        #endregion
    }
}
