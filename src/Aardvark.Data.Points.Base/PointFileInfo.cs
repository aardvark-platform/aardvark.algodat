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

namespace Aardvark.Data.Points
{
    /// <summary>
    /// General info for a point cloud data file.
    /// </summary>
    public class PointFileInfo
    {
        /// <summary>
        /// Fully qualified file name.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Format of point cloud file.
        /// </summary>
        public PointCloudFileFormat Format { get; }

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long FileSizeInBytes { get; }

        /// <summary>
        /// Number of points in file.
        /// </summary>
        public long PointCount { get; }

        /// <summary>
        /// Bounding box of points in file.
        /// </summary>
        public Box3d Bounds { get; }

        /// <summary></summary>
        public PointFileInfo(string fileName, PointCloudFileFormat format, long fileSizeInBytes, long pointCount, Box3d bounds)
        {
            FileName = fileName;
            Format = format;
            FileSizeInBytes = fileSizeInBytes;
            PointCount = pointCount;
            Bounds = bounds;
        }
    }

    /// <summary></summary>
    public class PointFileInfo<T> : PointFileInfo
    {
        /// <summary></summary>
        public T Metadata { get; }

        /// <summary></summary>
        public PointFileInfo(string fileName, PointCloudFileFormat format, long fileSizeInBytes, long pointCount, Box3d bounds, T metadata)
            : base(fileName, format, fileSizeInBytes, pointCount, bounds)
        {
            Metadata = metadata;
        }
    }
}
