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
using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// Class must have static constructor which registers file format, e.g.
    /// static Foo()
    /// {
    ///     ...
    ///     PointCloudFileFormat.Register(FooFormat);
    /// }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PointCloudFileFormatAttributeAttribute : Attribute
    {
    }

    /// <summary></summary>
    /// <remarks></remarks>
    public class PointCloudFileFormat(
        string description,
        string[] fileExtensions,
        Func<string, ParseConfig, PointFileInfo>? parseFileInfo,
        Func<string, ParseConfig, IEnumerable<Chunk>>? parseFile
            )
    {
        static PointCloudFileFormat()
        {
            RegisterViaIntrospection();
        }

        private static void RegisterViaIntrospection()
        {
            var xs = Introspection.GetAllTypesWithAttribute<PointCloudFileFormatAttributeAttribute>().ToArray();
            foreach (var x in xs)
            {
                try
                {
                    var t = x.Item1;
                    RuntimeHelpers.RunClassConstructor(t.TypeHandle);
                    //Console.WriteLine($"[PointCloudFileFormat] registered {x.Item1.FullName}");
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[PointCloudFileFormat] registered {x.Item1.FullName} [failed]");
                    Console.WriteLine(e.Message);
                    Console.ResetColor();
                }
            }
        }

        /// <summary></summary>
        public string Description { get; } = description ?? throw new ArgumentNullException(nameof(description));

        /// <summary></summary>
        public string[] FileExtensions { get; } = fileExtensions?.MapToArray(x => x.ToLowerInvariant()) ?? throw new ArgumentNullException(nameof(fileExtensions));

        /// <summary></summary>
        public PointFileInfo ParseFileInfo(string filename, ParseConfig config)
            => f_parseFileInfo != null ? f_parseFileInfo(filename, config) : throw new Exception("No info parser defined.");

        /// <summary></summary>
        public IEnumerable<Chunk> ParseFile(string filename, ParseConfig config)
            => f_parseFile != null ? f_parseFile(filename, config) : throw new Exception("No parser defined.");

        private readonly Func<string, ParseConfig, PointFileInfo>? f_parseFileInfo = parseFileInfo;
        private readonly Func<string, ParseConfig, IEnumerable<Chunk>>? f_parseFile = parseFile;

        #region Registry

        /// <summary>
        /// Unknown file format.
        /// </summary>
        public static readonly PointCloudFileFormat Unknown = new("unknown", [], null, null);

        /// <summary>
        /// Unknown file format.
        /// </summary>
        public static readonly PointCloudFileFormat Store = new("store", [], null, null);

        /// <summary>
        /// </summary>
        public static PointCloudFileFormat Register(PointCloudFileFormat format)
        {
            lock (s_registry)
            {
                if (s_registry.ContainsKey(format.Description))
                    throw new InvalidOperationException($"PointCloudFileFormat '{format.Description}' is already registered.");
                
                s_registry[format.Description] = format;
                return format;
            }
        }

        /// <summary>
        /// </summary>
        public static PointCloudFileFormat FromFileName(string filename)
        {
            var result = Lookup();
            
            if (result == Unknown)
            {
                var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                Introspection.RegisterAllAssembliesInPath(path);
                RegisterViaIntrospection();
                result = Lookup();
            }

            return result;

            PointCloudFileFormat Lookup()
            {
                lock (s_registry)
                {
                    var ext = GetExt(filename);
                    var formats = s_registry
                        .SelectMany(kv => kv.Value.FileExtensions.Select(x => Tuple.Create(x, kv.Value)))
                        .Where(x => x.Item1 == ext)
                        .ToArray()
                        ;
                    if (formats.Length == 0)
                    {
                        return Unknown;
                    }
                    else if (formats.Length > 1)
                    {
                        throw new Exception($"More than 1 file format registered for '{filename}' ({string.Join(", ", formats.Select(x => $"'{x.Item2.Description}'"))}).");
                    }
                    else
                    {
                        return formats.Single().Item2;
                    }
                }
            }
        }

        private static string GetExt(string filename) => Path.GetExtension(filename).ToLowerInvariant();
        private static readonly Dictionary<string, PointCloudFileFormat> s_registry = [];

        #endregion
    }
}
