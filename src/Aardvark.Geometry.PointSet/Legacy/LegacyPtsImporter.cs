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
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Importer for a .pts data stream.
    /// </summary>
    public static class LegacyPtsImporter
    {
        /// <summary>
        /// Imports .pts data into store.
        /// </summary>
        /// <param name="storage">Store to import .pts data into.</param>
        /// <param name="key">Store key for imported pointset.</param>
        /// <param name="stream">Stream to read .pts data from.</param>
        /// <param name="streamLength">Length of stream in bytes.</param>
        /// <param name="chunkSizeInBytes">Read chunks of this size (and process in parallel).</param>
        /// <param name="octreeSplitLimit">Split limit for octree.</param>
        /// <param name="filterMinDistanceOnImport">Skip points nearer to the previous point by this distance.</param>
        /// <param name="maxLevelOfParallelism">Processes this number of chunks in parallel.</param>
        /// <param name="progress">Progress callback.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns></returns>
        [Obsolete]
        public static Task<PointSet> Import(
            Storage storage,
            string key,
            Stream stream,
            long streamLength,
            int chunkSizeInBytes,
            int octreeSplitLimit,
            double filterMinDistanceOnImport,
            int maxLevelOfParallelism,
            Action<double> progress,
            CancellationToken ct
            )
        {
            var dd = filterMinDistanceOnImport * filterMinDistanceOnImport;
            return LegacyImporter.Import(storage, key,
                a => LegacyPtsParser.Parse(stream, streamLength, chunkSizeInBytes, maxLevelOfParallelism, dd, a, ct),
                (int)(streamLength / chunkSizeInBytes) + 1,
                octreeSplitLimit, maxLevelOfParallelism, progress, ct
                );
        }
    }
}
