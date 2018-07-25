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
using Aardvark.Data.Points;

namespace Aardvark.Geometry.Points
{
    /// <summary></summary>
    public static class PointCloudFormatExtensions
    {
        /// <summary></summary>
        public static PointSet ImportFile(this PointCloudFormat self, string filename, ImportConfig config)
            => PointCloud.Chunks(self.ParseFile(filename, config), config);
        
        /// <summary>
        /// Pts file format.
        /// </summary>
        public static readonly PointCloudFormat PtsFormat;

        /// <summary>
        /// E57 file format.
        /// </summary>
        public static readonly PointCloudFormat E57Format;

        static PointCloudFormatExtensions()
        {
            PtsFormat = new PointCloudFormat("pts", new[] { ".pts" }, PointCloud.PtsInfo, Data.Points.Import.Pts.Chunks);
            PointCloudFormat.Register(PtsFormat);

            E57Format = new PointCloudFormat("e57", new[] { ".e57" }, PointCloud.E57Info, PointCloud.E57);
            PointCloudFormat.Register(E57Format);
        }
    }
}
