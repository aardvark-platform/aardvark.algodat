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
using System;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// </summary>
    public class ParsingStats
    {
        /// <summary>
        /// </summary>
        public DateTimeOffset T0 { get; }
        /// <summary>
        /// </summary>
        public long TotalBytesCount { get; }
        /// <summary>
        /// </summary>
        public long TotalBytesRead { get; private set; }
        /// <summary>
        /// </summary>
        public double BytesPerSecond { get; private set; }
        /// <summary>
        /// </summary>
        public double MiBsPerSecond => BytesPerSecond / (1024 * 1024);

        /// <summary>
        /// </summary>
        public ParsingStats(long totalBytesCount)
        {
            T0 = DateTimeOffset.UtcNow;
            TotalBytesCount = totalBytesCount;
        }

        /// <summary>
        /// </summary>
        public void ReportProgress(long totalBytesRead)
        {
            var t = DateTimeOffset.UtcNow;
            var dt = (t - T0).TotalSeconds;

            BytesPerSecond = totalBytesRead / dt;
            TotalBytesRead = totalBytesRead;
        }
    }
}
