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
using System.Linq;
using System.Collections.Generic;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Importers for various formats.
    /// </summary>
    public static partial class PointCloud
    {
        /// <summary>
        /// Imports single chunk.
        /// </summary>
        public static PointSet Chunks(Chunk chunk, ImportConfig config)
            => Chunks(new[] { chunk }, config);
        
        /// <summary>
        /// Imports sequence of chunks.
        /// </summary>
        public static PointSet Chunks(IEnumerable<Chunk> chunks, ImportConfig config)
        {
            //var xs1 = chunks.Select(x => x.ImmutableFilterSequentialMinDist(config.MinDist)).ToArray();
            //var xs2 = xs1.Map(config.Reproject, null, config.MaxDegreeOfParallelism, config.CancellationToken).ToArray();
            //var xs3 = xs2.MapReduce(config.WithRandomKey());
            //var xs4 = xs3.GenerateLod(config);

            return chunks
                .Select(x => x.ImmutableFilterSequentialMinDist(config.MinDist))
                .Map(config.Reproject, null, config.MaxDegreeOfParallelism, config.CancellationToken)
                .MapReduce(config.WithRandomKey().WithProgressCallback(x => config.ProgressCallback(x * 0.66)))
                .GenerateLod(config.WithProgressCallback(x => config.ProgressCallback(0.66 + x * 0.34)))
                ;
        }
    }
}
