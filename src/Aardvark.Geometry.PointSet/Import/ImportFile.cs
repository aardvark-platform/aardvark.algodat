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
using System;
using System.Collections.Generic;
using System.IO;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Importers for various formats.
    /// </summary>
    public static partial class PointCloud
    {
        /// <summary>
        /// Gets general info for given point cloud file.
        /// </summary>
        public static PointFileInfo ParseFileInfo(string filename, ParseConfig config)
            => PointCloudFileFormat.FromFileName(filename).ParseFileInfo(filename, config);
        
        /// <summary>
        /// Parses file.
        /// Format is guessed based on file extension.
        /// </summary>
        public static IEnumerable<Chunk> Parse(string filename, ParseConfig config)
        {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (!File.Exists(filename)) throw new FileNotFoundException($"File does not exit ({filename}).", filename);
            return PointCloudFileFormat.FromFileName(filename).ParseFile(filename, config);
        }

        /// <summary>
        /// Imports file.
        /// Format is guessed based on file extension.
        /// </summary>
        public static PointSet Import(string filename, ImportConfig config = null)
        {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (!File.Exists(filename)) throw new FileNotFoundException("File does not exit.", filename);

            if (config == null)
            {
                config = ImportConfig.Default
                    .WithInMemoryStore()
                    .WithKey(FileHelpers.ComputeMd5Hash(filename, true))
                    ;
            }

            return PointCloudFileFormat.FromFileName(filename).ImportFile(filename, config);
        }

        /// <summary>
        /// Imports file into out-of-core store.
        /// Format is guessed based on file extension.
        /// </summary>
        public static PointSet Import(string filename, string storeDirectory, LruDictionary<string, object> cache)
        {
            if (filename == null) throw new ArgumentNullException(nameof(filename));
            if (!File.Exists(filename)) throw new FileNotFoundException("File does not exit.", filename);

            var config = ImportConfig.Default
                .WithStorage(OpenStore(storeDirectory, cache))
                .WithKey(FileHelpers.ComputeMd5Hash(filename, true))
                ;

            var result = PointCloudFileFormat.FromFileName(filename).ImportFile(filename, config);
            config.Storage.Flush();
            return result;
        }
    }
}
