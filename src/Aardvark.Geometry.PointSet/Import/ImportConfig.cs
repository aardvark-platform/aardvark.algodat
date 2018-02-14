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

        /// <summary>
        /// Large files should be read in chunks with this maximum size.
        /// </summary>
        public int ReadBufferSizeInBytes = 4 * 1024 * 1024;
        
        public Func<IList<V3d>, IList<V3d>> Reproject = null;
        
        public double MinDist = 0.0;
        
        public Storage Storage;
        
        public string Key;
        
        public int OctreeSplitLimit = 8192;
        
        public bool CreateOctreeLod = false;
        
        public int MaxDegreeOfParallelism = 0;
        
        public bool Verbose = false;
        
        public CancellationToken CancellationToken = CancellationToken.None;

        #endregion

        #region Immutable updates
        
        public ImportConfig WithKey(string newKey) => new ImportConfig
        {
            ReadBufferSizeInBytes = ReadBufferSizeInBytes,
            Reproject = Reproject,
            MinDist = MinDist,
            Storage = Storage,
            Key = newKey,
            OctreeSplitLimit = OctreeSplitLimit,
            CreateOctreeLod = CreateOctreeLod,
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            Verbose = Verbose,
            CancellationToken = CancellationToken
        };
        
        public ImportConfig WithRandomKey() => WithKey(Guid.NewGuid().ToString());

        public ImportConfig WithInMemoryStore() => new ImportConfig
        {
            ReadBufferSizeInBytes = ReadBufferSizeInBytes,
            Reproject = Reproject,
            MinDist = MinDist,
            Storage = new SimpleMemoryStore().ToPointCloudStore(),
            Key = Key,
            OctreeSplitLimit = OctreeSplitLimit,
            CreateOctreeLod = CreateOctreeLod,
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            Verbose = Verbose,
            CancellationToken = CancellationToken
        };

        public ImportConfig WithVerbose(bool verbose) => new ImportConfig
        {
            ReadBufferSizeInBytes = ReadBufferSizeInBytes,
            Reproject = Reproject,
            MinDist = MinDist,
            Storage = Storage,
            Key = Key,
            OctreeSplitLimit = OctreeSplitLimit,
            CreateOctreeLod = CreateOctreeLod,
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            Verbose = verbose,
            CancellationToken = CancellationToken
        };

        #endregion
    }
}
