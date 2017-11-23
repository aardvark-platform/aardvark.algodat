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

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// General info for a point cloud data file.
    /// </summary>
    public class ImportConfig
    {
        /// <summary>
        /// </summary>
        public static readonly ImportConfig Default = new ImportConfig();

        #region Properties

        /// <summary>
        /// Large files should be read in chunks with this maximum size.
        /// </summary>
        public int ReadBufferSizeInBytes = 4 * 1024 * 1024;

        /// <summary></summary>
        public Func<IList<V3d>, IList<V3d>> Reproject = null;

        /// <summary></summary>
        public double MinDist = 0.0;

        /// <summary></summary>
        public Storage Storage;

        /// <summary></summary>
        public string Key;

        /// <summary></summary>
        public int OctreeSplitLimit = 8192;

        /// <summary></summary>
        public bool CreateOctreeLod = false;

        /// <summary></summary>
        public int MaxLevelOfParallelism = 0;

        /// <summary></summary>
        public bool Verbose = false;

        /// <summary></summary>
        public ProgressReporter Progress = ProgressReporter.None;

        /// <summary></summary>
        public CancellationToken CancellationToken = CancellationToken.None;

        #endregion

        #region Immutable updates

        /// <summary></summary>
        public ImportConfig WithKey(string newKey) => new ImportConfig
        {
            ReadBufferSizeInBytes = ReadBufferSizeInBytes,
            Reproject = Reproject,
            MinDist = MinDist,
            Storage = Storage,
            Key = newKey,
            OctreeSplitLimit = OctreeSplitLimit,
            CreateOctreeLod = CreateOctreeLod,
            MaxLevelOfParallelism = MaxLevelOfParallelism,
            Verbose = Verbose,
            Progress = Progress,
            CancellationToken = CancellationToken
        };

        /// <summary></summary>
        public ImportConfig WithRandomKey() => WithKey(Guid.NewGuid().ToString());

        #endregion
    }
}
