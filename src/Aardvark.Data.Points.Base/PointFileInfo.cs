/*
   Aardvark Platform
   Copyright (C) 2006-2023  Aardvark Platform Team
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
