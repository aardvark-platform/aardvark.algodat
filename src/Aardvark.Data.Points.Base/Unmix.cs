/*
   Aardvark Platform
   Copyright (C) 2006-2024  Aardvark Platform Team
   https://aardvark.graphics

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using System.Collections.Generic;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public static class ChunkExtensions
    {
        /// <summary>
        /// Merges many chunks into a single chunk. 
        /// </summary>
        public static Chunk Union(this IEnumerable<Chunk> chunks)
        {
            var result = Chunk.Empty;
            foreach (var chunk in chunks) result = result.Union(chunk);
            return result;
        }
    }
}
