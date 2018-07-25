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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aardvark.Data.Points
{
    /// <summary></summary>
    public class PointCloudFormat
    {
        /// <summary></summary>
        public string Description { get; }

        /// <summary></summary>
        public string[] FileExtensions { get; }

        /// <summary></summary>
        public PointFileInfo ParseFileInfo(string filename, ImportConfig config) => f_parseFileInfo(filename, config);

        /// <summary></summary>
        public IEnumerable<Chunk> ParseFile(string filename, ImportConfig config) => f_parseFile(filename, config);

        /// <summary></summary>
        public PointCloudFormat(
            string description,
            string[] fileExtensions,
            Func<string, ImportConfig, PointFileInfo> parseFileInfo,
            Func<string, ImportConfig, IEnumerable<Chunk>> parseFile
            )
        {
            Description = description ?? throw new ArgumentNullException(nameof(description));
            FileExtensions = fileExtensions?.Map(x => x.ToLowerInvariant()) ?? throw new ArgumentNullException(nameof(fileExtensions));
            f_parseFileInfo = parseFileInfo;
            f_parseFile = parseFile;
        }

        private readonly Func<string, ImportConfig, PointFileInfo> f_parseFileInfo;
        private readonly Func<string, ImportConfig, IEnumerable<Chunk>> f_parseFile;

        #region Registry

        /// <summary>
        /// Unknown file format.
        /// </summary>
        public static readonly PointCloudFormat Unknown = new PointCloudFormat("unknown", new string[0], null, null);

        /// <summary>
        /// Unknown file format.
        /// </summary>
        public static readonly PointCloudFormat Store = new PointCloudFormat("store", new string[0], null, null);

        /// <summary>
        /// </summary>
        public static PointCloudFormat Register(PointCloudFormat format)
        {
            lock (s_registry)
            {
                if (s_registry.ContainsKey(format.Description))
                    throw new InvalidOperationException($"PointCloudFormat '{format.Description}' is already registered.");
                
                s_registry[format.Description] = format;
                return format;
            }
        }

        /// <summary>
        /// </summary>
        public static PointCloudFormat FromFileName(string filename)
        {
            lock (s_registry)
            {
                var ext = GetExt(filename);
                var formats = s_registry
                    .SelectMany(kv => kv.Value.FileExtensions.Select(x => Tuple.Create(x, kv.Value)))
                    .Where(x => x.Item1 == ext)
                    .ToArray()
                    ;
                if (formats.Length == 0) return Unknown;
                if (formats.Length > 1) throw new Exception($"More than 1 file format registered for '{filename}' ({string.Join(", ", formats.Select(x => $"'{x.Item2.Description}'"))}).");
                return formats.Single().Item2;
            }
        }

        private static string GetExt(string filename) => Path.GetExtension(filename).ToLowerInvariant();
        private static Dictionary<string, PointCloudFormat> s_registry = new Dictionary<string, PointCloudFormat>();

        #endregion
    }
}
