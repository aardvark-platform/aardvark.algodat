/*
   Aardvark Platform
   Copyright (C) 2006-2025  Aardvark Platform Team
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
    /// <remarks></remarks>
    public class PointFileInfo(string fileName, PointCloudFileFormat format, long fileSizeInBytes, long pointCount, Box3d bounds)
    {
        /// <summary>
        /// Fully qualified file name.
        /// </summary>
        public string FileName { get; } = fileName;

        /// <summary>
        /// Format of point cloud file.
        /// </summary>
        public PointCloudFileFormat Format { get; } = format;

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long FileSizeInBytes { get; } = fileSizeInBytes;

        /// <summary>
        /// Number of points in file.
        /// </summary>
        public long PointCount { get; } = pointCount;

        /// <summary>
        /// Bounding box of points in file.
        /// </summary>
        public Box3d Bounds { get; } = bounds;
    }

    /// <summary></summary>
    /// <remarks></remarks>
    public class PointFileInfo<T>(string fileName, PointCloudFileFormat format, long fileSizeInBytes, long pointCount, Box3d bounds, T metadata) : PointFileInfo(fileName, format, fileSizeInBytes, pointCount, bounds)
    {
        /// <summary></summary>
        public T Metadata { get; } = metadata;
    }
}
