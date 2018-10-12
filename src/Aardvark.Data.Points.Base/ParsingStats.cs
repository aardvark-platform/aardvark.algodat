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
