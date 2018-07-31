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
using Aardvark.Base;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Aardvark.Data.Points.Import
{
    /// <summary>
    /// Importer for custom ASCII format.
    /// </summary>
    public static class CustomAscii
    {
        /// <summary>
        /// Custom ASCII parser tokens.
        /// </summary>
        public enum Token
        {
            /// <summary>
            /// Parses Position.X from double value.
            /// </summary>
            PositionX,

            /// <summary>
            /// Parses Position.Y from double value.
            /// </summary>
            PositionY,

            /// <summary>
            /// Parses Position.Z from double value.
            /// </summary>
            PositionZ,



            /// <summary>
            /// Parses Normal.X from float value.
            /// </summary>
            NormalX,

            /// <summary>
            /// Parses Normal.Y from float value.
            /// </summary>
            NormalY,

            /// <summary>
            /// Parses Normal.Z from float value.
            /// </summary>
            NormalZ,



            /// <summary>
            /// Parses Color.R from byte value [0, 255].
            /// </summary>
            ColorR,

            /// <summary>
            /// Parses Color.G from byte value [0, 255].
            /// </summary>
            ColorG,

            /// <summary>
            /// Parses Color.B from byte value [0, 255].
            /// </summary>
            ColorB,

            /// <summary>
            /// Parses Color.A from byte value [0, 255].
            /// </summary>
            ColorA,



            /// <summary>
            /// Parses Color.R from float value [0.0, 1.0].
            /// </summary>
            ColorRf,

            /// <summary>
            /// Parses Color.G from float value [0.0, 1.0].
            /// </summary>
            ColorGf,

            /// <summary>
            /// Parses Color.B from float value [0.0, 1.0].
            /// </summary>
            ColorBf,

            /// <summary>
            /// Parses Color.A from float value [0.0, 1.0].
            /// </summary>
            ColorAf,



            /// <summary>
            /// Parses Intensity from int value.
            /// </summary>
            Intensity,
        }

        /// <summary>
        /// </summary>
        public static PointCloudFileFormat CreateFormat(string description, Token[] lineDefinition)
            => new PointCloudFileFormat(description, new string[0],
                (filename, config) => CustomAsciiInfo(filename, lineDefinition, config),
                (filename, config) => Chunks(filename, lineDefinition, config)
                );

        /// <summary>
        /// Parses ASCII file.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(string filename, Token[] lineDefinition, ImportConfig config)
        {
            Chunk? lineParser(byte[] buffer, int count, double filterDist)
                => LineParsers.Custom(buffer, count, filterDist, lineDefinition);
            return Parsing.AsciiLines(lineParser, filename, config);
        }

        /// <summary>
        /// Parses ASCII stream.
        /// </summary>
        public static IEnumerable<Chunk> Chunks(this Stream stream, long streamLengthInBytes, Token[] lineDefinition, ImportConfig config)
        {
            Chunk? lineParser(byte[] buffer, int count, double filterDist)
                => LineParsers.Custom(buffer, count, filterDist, lineDefinition);
            return Parsing.AsciiLines(lineParser, stream, streamLengthInBytes, config);
        }

        /// <summary>
        /// Gets general info for custom ASCII file.
        /// </summary>
        public static PointFileInfo CustomAsciiInfo(string filename, Token[] lineDefinition, ImportConfig config)
        {
            var filesize = new FileInfo(filename).Length;
            var pointCount = 0L;
            var pointBounds = Box3d.Invalid;
            foreach (var chunk in Chunks(filename, lineDefinition, ImportConfig.Default))
            {
                pointCount += chunk.Count;
                pointBounds.ExtendBy(chunk.BoundingBox);
            }
            var format = CreateFormat("Custom ASCII", lineDefinition);
            return new PointFileInfo(filename, format, filesize, pointCount, pointBounds);
        }




        /// <summary>
        /// https://msdn.microsoft.com/en-us/magazine/mt808499.aspx
        /// </summary>
        public static void Foo()
        {
            const string src =
                @"
                using Aardvark.Base;
                using Aardvark.Data.Points;
                using System;
                using System.Collections.Generic;
                using System.Runtime.CompilerServices;
                using static Aardvark.Data.Points.LineParsers;

                namespace Aardvark.Data.Points.Import
                {
                    public static class CustomAsciiParser
                    {
                        public static void Test()
                        {
                            Console.WriteLine(""Hello World!"");
                        }

                        public static Chunk Foo(byte[] buffer, int count, double filterDist)
                        {
                            var ps = new List<V3d>();
                            var position = V3d.Zero;
                            
                            var cs = new List<C4b>();
                            var color = C4b.Black;
                            
                            var prev = V3d.PositiveInfinity;
                            var filterDistM = -filterDist;
                            var doFilterDist = filterDist > 0.0;
                            
                            unsafe
                            {
                                fixed (byte* begin = buffer)
                                {
                                    var p = begin;
                                    var end = p + count;
                                    while (p < end)
                                    {
                                        // parse single line
                                        if (!ParseV3d(ref p, end, ref position)) { SkipToNextLine(ref p, end); continue; }
                                        if (!ParseC4bFromByteRGB(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
                                        SkipToNextLine(ref p, end);
                            
                                        // min dist filtering
                                        if (doFilterDist)
                                        {
                                            if (SkipBecauseOfMinDist(ref position, ref prev, filterDist)) continue;
                                            prev = position;
                                        }
                            
                                        // add point to chunk
                                        ps.Add(position); cs.Add(color);
                                    }
                                }
                            }
                            
                            if (ps.Count == 0) return Chunk.Empty;
                            return new Chunk(ps, cs, null, null);
                        }
                    }
                }
                ";

            //var node = CSharpSyntaxTree.ParseText(src).GetRoot();
            //Console.WriteLine(node);

            var tree = SyntaxFactory.ParseSyntaxTree(src);
            var fileName = "CustomAsciiParser.dll";

            var refs = new[]
            {
                typeof(object),
                typeof(V3d),
                typeof(Chunk),
                typeof(LineParsers)
            }
            .Map(t => MetadataReference.CreateFromFile(t.GetTypeInfo().Assembly.Location))
            ;
            var compilation = CSharpCompilation
                .Create(fileName)
                .WithOptions(new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    allowUnsafe: true
                    ))
                .AddReferences(refs)
                .AddSyntaxTrees(tree)
                ;
            //var path = Path.Combine(Directory.GetCurrentDirectory(), fileName);

            using (var ms = new MemoryStream())
            {
                var compilationResult = compilation.Emit(ms);

                if (compilationResult.Success)
                {
                    //var asm = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(ms);
                    var asm = Assembly.Load(ms.ToArray());
                    var result = asm
                        .GetType("Aardvark.Data.Points.Import.CustomAsciiParser")
                        .GetMethod("Test")
                        .Invoke(null, new object[] { })
                        ;
                }
                else
                {
                    foreach (var codeIssue in compilationResult.Diagnostics)
                    {
                        var issue = $"ID: {codeIssue.Id}, Message: {codeIssue.GetMessage()}, Location: { codeIssue.Location.GetLineSpan()}, Severity: { codeIssue.Severity}";
                        Console.WriteLine(issue);
                    }
                }
            }
        }

    }
}
