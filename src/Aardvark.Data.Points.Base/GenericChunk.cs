/*
   Aardvark Platform
   Copyright (C) 2006-2020  Aardvark Platform Team
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
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable CS1591

namespace Aardvark.Data.Points
{
    /// <summary>
    /// Parsers emit a sequence of chunks of points with optional colors, normals, and intensities.
    /// </summary>
    public class GenericChunk
    {
        /// <summary>
        /// The empty chunk.
        /// </summary>
        public static readonly GenericChunk Empty = new(
            perPointAttributes: ImmutableDictionary<Durable.Def, object>.Empty,
            bbox              : Box3d.Invalid
            );

        public static class Defs
        {
            private static Durable.Def Def(string id, string name, string description, Durable.Def type)
                => new(new Guid(id), name, description, type.Id, true);

            #region Positions

            /// <summary>Positions. V2f[].</summary>
            public static readonly Durable.Def Positions2f = Def("30180c1c-1858-42f0-9440-004b4554db5a", "Aardvark.Chunk.Positions2f", "Positions. V2f[].", Durable.Aardvark.V2fArray);

            /// <summary>Positions. V2d[].</summary>
            public static readonly Durable.Def Positions2d = Def("1257e31f-58d1-47a2-8252-74a0b9686e29", "Aardvark.Chunk.Positions2d", "Positions. V2d[].", Durable.Aardvark.V2dArray);

            /// <summary>Positions. V3f[].</summary>
            public static readonly Durable.Def Positions3f  = Def("1cc23a98-f387-4df6-a82f-1e73f87bd519", "Aardvark.Chunk.Positions3f", "Positions. V3f[].", Durable.Aardvark.V3fArray);
            
            /// <summary>Positions. V3d[].</summary>
            public static readonly Durable.Def Positions3d  = Def("b72e1359-6d05-4b61-8546-575f5280675a", "Aardvark.Chunk.Positions3d", "Positions. V3d[].", Durable.Aardvark.V3dArray);

            /// <summary>All durable defs interpreted as positions.</summary>
            public static readonly ImmutableHashSet<Durable.Def> PositionsSupportedDefs = ImmutableHashSet<Durable.Def>.Empty
                .Add(Positions2f)
                .Add(Positions2d)
                .Add(Positions3f)
                .Add(Positions3d)
                ;

            #endregion

            #region Colors

            /// <summary>Colors. C3b[].</summary>
            public static readonly Durable.Def Colors3b     = Def("52fa40ae-9a54-4a37-a2e3-4b46c78392e1", "Aardvark.Chunk.Colors3b", "Colors. C3b[].", Durable.Aardvark.C3bArray);

            /// <summary>Colors. C3f[].</summary>
            public static readonly Durable.Def Colors3f     = Def("eda92353-7e58-4898-b67c-812cb73a7184", "Aardvark.Chunk.Colors3f", "Colors. C3f[].", Durable.Aardvark.C3fArray);

            /// <summary>Colors. C4b[].</summary>
            public static readonly Durable.Def Colors4b     = Def("34d162f5-6462-4dd2-a8ec-36b1c326a6db", "Aardvark.Chunk.Colors4b", "Colors. C4b[].", Durable.Aardvark.C4bArray);

            /// <summary>Colors. C4f[].</summary>
            public static readonly Durable.Def Colors4f     = Def("47cae42a-26a9-403f-8a5a-34f63fb08eb1", "Aardvark.Chunk.Colors4f", "Colors. C4f[].", Durable.Aardvark.C4fArray);

            /// <summary>All durable defs interpreted as colors.</summary>
            public static readonly ImmutableHashSet<Durable.Def> ColorsSupportedDefs = ImmutableHashSet<Durable.Def>.Empty
                .Add(Colors3b)
                .Add(Colors3f)
                .Add(Colors4b)
                .Add(Colors4f)
                ;

            #endregion

            #region Normals

            /// <summary>Normals. V3f[].</summary>
            public static readonly Durable.Def Normals3f    = Def("a8ce542d-f810-4a34-8236-d39d5ceaa99c", "Aardvark.Chunk.Normals3f", "Normals. V3f[].", Durable.Aardvark.V3fArray);

            /// <summary>All durable defs interpreted as colors.</summary>
            public static readonly ImmutableHashSet<Durable.Def> NormalsSupportedDefs = ImmutableHashSet<Durable.Def>.Empty
                .Add(Normals3f)
                ;

            #endregion

            #region Intensities

            /// <summary>Intensities. byte[].</summary>
            public static readonly Durable.Def Intensities1b  = Def("3b2dddb6-6d5e-4d92-877c-d046ed026b8a", "Aardvark.Chunk.Intensities1b", "Intensities. byte[].", Durable.Primitives.UInt8Array);
            /// <summary>Intensities. Int16[].</summary>
            public static readonly Durable.Def Intensities1s = Def("71f63c81-9cd4-437d-801b-d2a012ea120c", "Aardvark.Chunk.Intensities1s", "Intensities. Int16[].", Durable.Primitives.Int16Array);
            /// <summary>Intensities. UInt16[].</summary>
            public static readonly Durable.Def Intensities1us = Def("75e9e5c3-b510-4e00-bb80-866976ef7df2", "Aardvark.Chunk.Intensities1us", "Intensities. UInt16[].", Durable.Primitives.UInt16Array);
            /// <summary>Intensities. Int32[].</summary>
            public static readonly Durable.Def Intensities1i = Def("d97c8f2e-8e47-4bea-a226-e655b8520dd7", "Aardvark.Chunk.Intensities1i", "Intensities. Int32[].", Durable.Primitives.Int32Array);
            /// <summary>Intensities. UInt32[].</summary>
            public static readonly Durable.Def Intensities1ui = Def("cc43d018-e67d-4d50-be74-989cb2a296c2", "Aardvark.Chunk.Intensities1ui", "Intensities. UInt32[].", Durable.Primitives.UInt32Array);
            /// <summary>Intensities. Float32[].</summary>
            public static readonly Durable.Def Intensities1f = Def("aee8ab41-0ae9-4987-9fd1-65ccef82f67b", "Aardvark.Chunk.Intensities1f", "Intensities. Float32[].", Durable.Primitives.Float32Array);
            /// <summary>Intensities. Float64[].</summary>
            public static readonly Durable.Def Intensities1d = Def("59daac88-bc42-4bc1-a76f-e777441efc21", "Aardvark.Chunk.Intensities1d", "Intensities. Float64[].", Durable.Primitives.Float64Array);

            /// <summary>All durable defs interpreted as colors.</summary>
            public static readonly ImmutableHashSet<Durable.Def> IntensitiesSupportedDefs = ImmutableHashSet<Durable.Def>.Empty
                .Add(Intensities1b)
                .Add(Intensities1s)
                .Add(Intensities1us)
                .Add(Intensities1i)
                .Add(Intensities1ui)
                .Add(Intensities1f)
                .Add(Intensities1d)
                ;

            #endregion

            #region Classifications

            /// <summary>Classifications. byte[].</summary>
            public static readonly Durable.Def Classifications1b = Def("3cf3a1b8-1000-4b2f-a674-f0718c60de72", "Aardvark.Chunk.Classifications1b", "Classifications. byte[].", Durable.Primitives.UInt8Array);
            /// <summary>Classifications. Int16[].</summary>
            public static readonly Durable.Def Classifications1s = Def("8673504a-5100-4dbc-87b6-0ca4f2382bcc", "Aardvark.Chunk.Classifications1s", "Classifications. Int16[].", Durable.Primitives.Int16Array);
            /// <summary>Classifications. UInt16[].</summary>
            public static readonly Durable.Def Classifications1us = Def("4cae2709-c86e-4d24-bba8-086d4845c817", "Aardvark.Chunk.Classifications1us", "Classifications. UInt16[].", Durable.Primitives.UInt16Array);
            /// <summary>Classifications. Int32[].</summary>
            public static readonly Durable.Def Classifications1i = Def("61fea872-aa6e-4249-ae9f-ad8fe75f8638", "Aardvark.Chunk.Classifications1i", "Classifications. Int32[].", Durable.Primitives.Int32Array);
            /// <summary>Classifications. UInt32[].</summary>
            public static readonly Durable.Def Classifications1ui = Def("3434e3d8-8812-4f7f-9f35-8150de42922c", "Aardvark.Chunk.Classifications1ui", "Classifications. UInt32[].", Durable.Primitives.UInt32Array);
            /// <summary>Classifications. string[].</summary>
            public static readonly Durable.Def ClassificationsString = Def("05d57d11-86a1-4bd4-bb6d-219a47fd9193", "Aardvark.Chunk.ClassificationsString", "Classifications. string[].", Durable.Primitives.StringUTF8Array);

            /// <summary>All durable defs interpreted as colors.</summary>
            public static readonly ImmutableHashSet<Durable.Def> ClassificationsSupportedDefs = ImmutableHashSet<Durable.Def>.Empty
                .Add(Classifications1b)
                .Add(Classifications1s)
                .Add(Classifications1us)
                .Add(Classifications1i)
                .Add(Classifications1ui)
                .Add(ClassificationsString)
                ;

            #endregion
        }

        #region Properties

        /// <summary>
        /// Per-point attributes.
        /// </summary>
        public ImmutableDictionary<Durable.Def, object> Data { get; }

        /// <summary>
        /// Exact bounding box of point positions.
        /// </summary>
        public Box3d BoundingBox { get; }

        /// <summary>
        /// Number of points in this chunk.
        /// </summary>
        public int Count { get; }

        public Durable.Def PositionsDef { get; }
        public bool HasPositions => true;
        public object Positions => Data[PositionsDef];
        public V3d[] PositionsAsV3d => Positions switch
        {
            V2f[] xs => xs.Map(x => (V3d)x.XYO),
            V2d[] xs => xs.Map(x => x.XYO),
            V3f[] xs => xs.Map(x => (V3d)x),
            V3d[] xs => xs,
            _ => throw new Exception($"Unsupported positions type {Positions.GetType()}.")
        };

        public Durable.Def? ColorsDef { get; }
        public bool HasColors => ColorsDef != null;
        public object? Colors => ColorsDef != null ? Data[ColorsDef] : null;
        public C4b[]? ColorsAsC4b => Colors switch
        {
            null => null,
            C3b[] xs => xs.Map(x => new C4b(x.R, x.G, x.B)),
            C4b[] xs => xs,
            _ => throw new Exception($"Unsupported colors type {Positions.GetType()}.")
        };

        public Durable.Def? NormalsDef { get; }
        public bool HasNormals => NormalsDef != null;
        public object? Normals => NormalsDef != null ? Data[NormalsDef] : null;
        public V3f[]? NormalsAsV3f => Normals switch
        {
            null => null,
            V3f[] xs => xs,
            _ => throw new Exception($"Unsupported normals type {Positions.GetType()}.")
        };

        public Durable.Def? IntensitiesDef { get; }
        public bool HasIntensities => IntensitiesDef != null;
        public object? Intensities => IntensitiesDef != null ? Data[IntensitiesDef] : null;
        public int[]? IntensitiesAsInt32 => Intensities switch
        {
            null => null,
            sbyte[] xs => xs.Map(x => (int)x),
            byte[] xs => xs.Map(x => (int)x),
            short[] xs => xs.Map(x => (int)x),
            ushort[] xs => xs.Map(x => (int)x),
            int[] xs => xs,
            _ => throw new Exception($"Unsupported intensities type {Positions.GetType()}.")
        };

        public Durable.Def? ClassificationsDef { get; }
        public bool HasClassifications => ClassificationsDef != null;
        public object? Classifications => ClassificationsDef != null ? Data[ClassificationsDef] : null;
        public byte[]? ClassificationsAsByte => Classifications switch
        {
            null => null,
            byte[] xs => xs,
            _ => throw new Exception($"Unsupported classifications type {Positions.GetType()}.")
        };

        #endregion

        /// <summary>
        /// Create new chunk.
        /// If no bbox is supplied, then it will be computed from point positions contained in data.
        /// </summary>
        public GenericChunk(IReadOnlyDictionary<Durable.Def, object> perPointAttributes, Box3d? bbox = null)
        {
            if (perPointAttributes == null) throw new ArgumentNullException(nameof(perPointAttributes));
            Data = ImmutableDictionary<Durable.Def, object>.Empty.AddRange(perPointAttributes);

            PositionsDef = perPointAttributes.Keys.Where(Defs.PositionsSupportedDefs.Contains).Single();
            switch (perPointAttributes[PositionsDef])
            {
                case V2f[] ps:
                    {
                        Count = ps.Length;
                        var bbox2 = bbox.HasValue ? Box2d.Invalid : (Box2d)new Box2f(ps);
                        BoundingBox = bbox ?? new Box3d(bbox2.Min.XYO, bbox2.Max.XYO);
                        break;
                    }

                case V2d[] ps:
                    {
                        Count = ps.Length;
                        var bbox2 = bbox.HasValue ? Box2d.Invalid : new Box2d(ps);
                        BoundingBox = bbox ?? new Box3d(bbox2.Min.XYO, bbox2.Max.XYO);
                        break;
                    }

                case V3f[] ps:
                    {
                        Count = ps.Length;
                        BoundingBox = bbox ?? (Box3d)new Box3f(ps);
                        break;
                    }

                case V3d[] ps:
                    {
                        Count = ps.Length;
                        BoundingBox = bbox ?? new Box3d(ps);
                        break;
                    }

                case null   : throw new ArgumentException("Positions must not be null.", nameof(perPointAttributes));
                default     : throw new ArgumentException($"Unknown positions type '{perPointAttributes[PositionsDef].GetType()}'.");
            }

            foreach (var kv in Data)
            {
                if (kv.Value is Array xs)
                {
                    if (xs.Length != Count) throw new ArgumentException($"Entry {kv.Key} must have length {Count}, but has {xs.Length}.");
                }
                else
                {
                    throw new ArgumentException($"Entry {kv.Key} must be array.");
                }
            }

            ColorsDef = perPointAttributes.Keys.Where(Defs.ColorsSupportedDefs.Contains).SingleOrDefault();
            NormalsDef = perPointAttributes.Keys.Where(Defs.ColorsSupportedDefs.Contains).SingleOrDefault();
            IntensitiesDef = perPointAttributes.Keys.Where(Defs.IntensitiesSupportedDefs.Contains).SingleOrDefault();
            ClassificationsDef = perPointAttributes.Keys.Where(Defs.ClassificationsSupportedDefs.Contains).SingleOrDefault();
        }

        /// <summary></summary>
        public bool IsEmpty => Count == 0;

        /// <summary>
        /// True if given 'perPointAttribute' is available.
        /// </summary>
        public bool Has(Durable.Def perPointAttribute) => Data.ContainsKey(perPointAttribute);

        #region With*

        /// <summary>
        /// Immutable update of positions.
        /// </summary>
        public GenericChunk WithPositions(object newPositions) => new(newPositions switch
        {
            V2f[] xs => DataWithoutPositions.Add(Defs.Positions2f, xs),
            IEnumerable<V2f> xs => DataWithoutPositions.Add(Defs.Positions2f, xs.ToArray()),
            V2d[] xs => DataWithoutPositions.Add(Defs.Positions2d, xs),
            IEnumerable<V2d> xs => DataWithoutPositions.Add(Defs.Positions2d, xs.ToArray()),
            V3f[] xs => DataWithoutPositions.Add(Defs.Positions3f, xs),
            IEnumerable<V3f> xs => DataWithoutPositions.Add(Defs.Positions3f, xs.ToArray()),
            V3d[] xs => DataWithoutPositions.Add(Defs.Positions3d, xs),
            IEnumerable<V3d> xs => DataWithoutPositions.Add(Defs.Positions3d, xs.ToArray()),
            _ => throw new ArgumentException($"Incompatible positions type {newPositions.GetType()}.", nameof(newPositions))
        });
        private ImmutableDictionary<Durable.Def, object> DataWithoutPositions => Data.Remove(PositionsDef);

        /// <summary>
        /// Immutable update of colors.
        /// </summary>
        public GenericChunk WithColors(object newColors) => new(newColors switch
        {
            C3b[] xs => DataWithoutColors.Add(Defs.Colors3b, xs),
            IEnumerable<C3b> xs => DataWithoutColors.Add(Defs.Colors3b, xs.ToArray()),
            C3f[] xs => DataWithoutColors.Add(Defs.Colors3f, xs),
            IEnumerable<C3f> xs => DataWithoutColors.Add(Defs.Colors3f, xs.ToArray()),
            C4b[] xs => DataWithoutColors.Add(Defs.Colors4b, xs),
            IEnumerable<C4b> xs => DataWithoutColors.Add(Defs.Colors4b, xs.ToArray()),
            C4f[] xs => DataWithoutColors.Add(Defs.Colors4f, xs),
            IEnumerable<C4f> xs => DataWithoutColors.Add(Defs.Colors4f, xs.ToArray()),
            _ => throw new ArgumentException($"Incompatible colors type {newColors.GetType()}.", nameof(newColors))
        });
        private ImmutableDictionary<Durable.Def, object> DataWithoutColors => ColorsDef != null ? Data.Remove(ColorsDef) : Data;

        /// <summary>
        /// Immutable update of normals.
        /// </summary>
        public GenericChunk WithNormals(object newNormals) => new(newNormals switch
        {
            V3f[] xs => DataWithoutNormals.Add(Defs.Normals3f, xs),
            IEnumerable<V3f> xs => DataWithoutNormals.Add(Defs.Normals3f, xs.ToArray()),
            _ => throw new ArgumentException($"Incompatible normals type {newNormals.GetType()}.", nameof(newNormals))
        });
        private ImmutableDictionary<Durable.Def, object> DataWithoutNormals => NormalsDef != null ? Data.Remove(NormalsDef) : Data;

        /// <summary>
        /// Immutable update of intensities.
        /// </summary>
        public GenericChunk WithIntensities(object newIntensities) => new(newIntensities switch
        {
            byte[] xs => DataWithoutIntensities.Add(Defs.Intensities1b, xs),
            IEnumerable<byte> xs => DataWithoutIntensities.Add(Defs.Intensities1b, xs.ToArray()),
            short[] xs => DataWithoutIntensities.Add(Defs.Intensities1s, xs),
            IEnumerable<short> xs => DataWithoutIntensities.Add(Defs.Intensities1s, xs.ToArray()),
            ushort[] xs => DataWithoutIntensities.Add(Defs.Intensities1us, xs),
            IEnumerable<ushort> xs => DataWithoutIntensities.Add(Defs.Intensities1us, xs.ToArray()),
            int[] xs => DataWithoutIntensities.Add(Defs.Intensities1i, xs),
            IEnumerable<int> xs => DataWithoutIntensities.Add(Defs.Intensities1i, xs.ToArray()),
            uint[] xs => DataWithoutIntensities.Add(Defs.Intensities1ui, xs),
            IEnumerable<uint> xs => DataWithoutIntensities.Add(Defs.Intensities1ui, xs.ToArray()),
            float[] xs => DataWithoutIntensities.Add(Defs.Intensities1f, xs),
            IEnumerable<float> xs => DataWithoutIntensities.Add(Defs.Intensities1f, xs.ToArray()),
            double[] xs => DataWithoutIntensities.Add(Defs.Intensities1d, xs),
            IEnumerable<double> xs => DataWithoutIntensities.Add(Defs.Intensities1d, xs.ToArray()),
            _ => throw new ArgumentException($"Incompatible intensities type {newIntensities.GetType()}.", nameof(newIntensities))
        });
        private ImmutableDictionary<Durable.Def, object> DataWithoutIntensities => IntensitiesDef != null ? Data.Remove(IntensitiesDef) : Data;

        /// <summary>
        /// Immutable update of classifications.
        /// </summary>
        public GenericChunk WithClassifications(object newClassifications) => new(newClassifications switch
        {
            byte[] xs => DataWithoutClassifications.Add(Defs.Classifications1b, xs),
            IEnumerable<byte> xs => DataWithoutClassifications.Add(Defs.Classifications1b, xs.ToArray()),
            short[] xs => DataWithoutClassifications.Add(Defs.Classifications1s, xs),
            IEnumerable<short> xs => DataWithoutClassifications.Add(Defs.Classifications1s, xs.ToArray()),
            ushort[] xs => DataWithoutClassifications.Add(Defs.Classifications1us, xs),
            IEnumerable<ushort> xs => DataWithoutClassifications.Add(Defs.Classifications1us, xs.ToArray()),
            int[] xs => DataWithoutClassifications.Add(Defs.Classifications1i, xs),
            IEnumerable<int> xs => DataWithoutClassifications.Add(Defs.Classifications1i, xs.ToArray()),
            uint[] xs => DataWithoutClassifications.Add(Defs.Classifications1ui, xs),
            IEnumerable<uint> xs => DataWithoutClassifications.Add(Defs.Classifications1ui, xs.ToArray()),
            string[] xs => DataWithoutClassifications.Add(Defs.ClassificationsString, xs),
            IEnumerable<string> xs => DataWithoutClassifications.Add(Defs.ClassificationsString, xs.ToArray()),
            _ => throw new ArgumentException($"Incompatible classifications type {newClassifications.GetType()}.", nameof(newClassifications))
        });
        private ImmutableDictionary<Durable.Def, object> DataWithoutClassifications => ClassificationsDef != null ? Data.Remove(ClassificationsDef) : Data;

        #endregion

        public static GenericChunk ImmutableMerge(GenericChunk a, GenericChunk b)
        {
            if (a.IsEmpty) return b;
            if (b.IsEmpty) return a;

            var keysA = new HashSet<Durable.Def>(a.Data.Keys);
            var keysB = new HashSet<Durable.Def>(b.Data.Keys);
            if (!keysA.SetEquals(keysB)) throw new InvalidOperationException("Cannot merge chunks with different attributes.");

            var totalCount = a.Count + b.Count;

            var data = ImmutableDictionary<Durable.Def, object>.Empty;
            foreach (var kv in a.Data)
            {
                var key = kv.Key;
                var xs = (Array)kv.Value;
                var ys = (Array)b.Data[key];

                var t = kv.Value.GetType().GetElementType();
                var rs = Array.CreateInstance(t, totalCount);

                Array.Copy(xs, 0, rs, 0,         xs.Length);
                Array.Copy(ys, 0, rs, xs.Length, ys.Length);

                data = data.Add(key, rs);
            }

            return new GenericChunk(data, new Box3d(a.BoundingBox, b.BoundingBox));
        }

        public static GenericChunk ImmutableMerge(params GenericChunk[] chunks)
        {
            if (chunks == null || chunks.Length == 0) return Empty;
            if (chunks.Length == 1) return chunks[0];

            var head = chunks[0];
            var headKeys = new HashSet<Durable.Def>(head.Data.Keys);
            foreach (var chunk in chunks)
            {
                if (headKeys.SetEquals(new HashSet<Durable.Def>(chunk.Data.Keys))) throw new InvalidOperationException("Cannot merge chunks with different attributes.");
            }
            

            var totalCount = chunks.Sum(c => c.Count);
            var totalBBox  = new Box3d(chunks.Select(x => x.BoundingBox));

            var data = ImmutableDictionary<Durable.Def, object>.Empty;
            foreach (var key in headKeys)
            {
                var t = head.Data[key].GetType().GetElementType();
                data = data.Add(key, Array.CreateInstance(t, totalCount));
            }

            var offset = 0;
            foreach (var chunk in chunks)
            {
                foreach (var kv in chunk.Data)
                {
                    var xs = (Array)kv.Value;
                    var rs = (Array)data[kv.Key];
                    Array.Copy(xs, 0, rs, offset, xs.Length);
                }
                offset += chunk.Count;
            }

            return new GenericChunk(data, totalBBox);
        }

        public static GenericChunk ImmutableMerge(IEnumerable<GenericChunk> chunks)
            => ImmutableMerge(chunks.ToArray());

        /// <summary>
        /// Creates new chunk which is union of this chunk and other. 
        /// </summary>
        public GenericChunk Union(GenericChunk other)
            => ImmutableMerge(this, other);

        private GenericChunk Subset(List<int> subsetIndices)
        {
            var data = ImmutableDictionary<Durable.Def, object>.Empty;
            foreach (var kv in Data)
            {
                data = data.Add(kv.Key, kv.Value.Subset(subsetIndices));
            }
            return new GenericChunk(data, BoundingBox);
        }
        /// <summary>
        /// Returns chunk with duplicate point positions removed.
        /// </summary>
        public GenericChunk ImmutableDeduplicate(bool verbose)
        {
            var positions = Data[PositionsDef];
            var ia = new List<int>();
            void Dedup<T>(T[] ps)
            {
                var dedup = new HashSet<T>();
                for (var i = 0; i < ps.Length; i++) if (dedup.Add(ps[i])) ia.Add(i);
            }
            switch (positions)
            {
                case V2f[] ps: Dedup(ps); break;
                case V2d[] ps: Dedup(ps); break;
                case V3f[] ps: Dedup(ps); break;
                case V3d[] ps: Dedup(ps); break;
                default: throw new Exception($"Unknown positions type {positions.GetType()}.");
            }
            var hasDuplicates = ia.Count < Count;

            if (hasDuplicates)
            {
                if (verbose)
                {
                    var removedCount = Count - ia.Count;
                    var removedPercent = (removedCount / (double)Count) * 100.0;
                    Report.Line($"Removed {removedCount:N0} duplicate points ({removedPercent:0.00}% of {Count:N0}).");
#if DEBUG
                    if (ia.Count == 1) Report.Warn($"Bam! Complete chunk collapsed to a single point.");
#endif
                }

                return Subset(ia);
            }
            else
            {
                return this;
            }
        }

        public GenericChunk ImmutableMapPositions(Func<V3d, V3d> mapping)
            => (Data[PositionsDef]) switch
            {
                V2f[] ps => WithPositions(ps.Map(p => (V2f)mapping((V3d)p.XYO).XY)),
                V2d[] ps => WithPositions(ps.Map(p => mapping(p.XYO).XY)),
                V3f[] ps => WithPositions(ps.Map(p => (V3f)mapping((V3d)p))),
                V3d[] ps => WithPositions(ps.Map(mapping)),
                _ => throw new Exception($"Unsupported positions type {Data[PositionsDef].GetType()}"),
            };

        public GenericChunk ImmutableMergeWith(IEnumerable<GenericChunk> others)
            => ImmutableMerge(this, ImmutableMerge(others));

        public GenericChunk ImmutableMergeWith(params GenericChunk[] others)
            => ImmutableMerge(this, ImmutableMerge(others));

        /// <summary>
        /// Splits this chunk into multiple chunks according to key of i-th point in chunk.
        /// </summary>
        public Dictionary<TKey, GenericChunk> GroupBy<TKey>(Func<GenericChunk, int, TKey> keySelector)
        {
            var dict = new Dictionary<TKey, List<int>>();
            for (var i = 0; i < Count; i++)
            {
                var k = keySelector(this, i);
                if (!dict.TryGetValue(k, out var ia)) dict[k] = ia = new List<int>();
                ia.Add(i);
            }

            var result = new Dictionary<TKey, GenericChunk>();
            foreach (var kv in dict)
            {
                var ia = kv.Value;
                result[kv.Key] = Subset(ia);
            }
            return result;
        }

        #region ImmutableFilter...

        /// <summary>
        /// Returns chunk with points for which predicate is true.
        /// </summary>
        public GenericChunk ImmutableFilter(Func<GenericChunk, int, bool> predicate)
        {
            var ia = new List<int>();
            for (var i = 0; i < Count; i++)
            {
                if (predicate(this, i)) ia.Add(i);
            }
            return Subset(ia);
        }

        /// <summary>
        /// Removes points which are less than minDist from previous point (L2, Euclidean).
        /// </summary>
        public GenericChunk ImmutableFilterSequentialMinDistL2(double minDist)
        {
            if (minDist <= 0.0 || Count <= 1) return this;
            var minDistSquared = minDist * minDist;

            switch (Data[PositionsDef])
            {
                case V2f[] ps:
                    {
                        var last = V2f.MinValue;
                        var ia = new List<int>();
                        for (var i = 0; i < ps.Length; i++)
                        {
                            var p = ps[i];
                            if (Utils.DistLessThanL2(ref p, ref last, minDistSquared)) continue;
                            last = p; ia.Add(i);
                        }
                        return Subset(ia);
                    }
                case V2d[] ps:
                    {
                        var last = V2d.MinValue;
                        var ia = new List<int>();
                        for (var i = 0; i < ps.Length; i++)
                        {
                            var p = ps[i];
                            if (Utils.DistLessThanL2(ref p, ref last, minDistSquared)) continue;
                            last = p; ia.Add(i);
                        }
                        return Subset(ia);
                    }
                case V3f[] ps:
                    {
                        var last = V3f.MinValue;
                        var ia = new List<int>();
                        for (var i = 0; i < ps.Length; i++)
                        {
                            var p = ps[i];
                            if (Utils.DistLessThanL2(ref p, ref last, minDistSquared)) continue;
                            last = p; ia.Add(i);
                        }
                        return Subset(ia);
                    }
                case V3d[] ps:
                    {
                        var last = V3d.MinValue;
                        var ia = new List<int>();
                        for (var i = 0; i < ps.Length; i++)
                        {
                            var p = ps[i];
                            if (Utils.DistLessThanL2(ref p, ref last, minDistSquared)) continue;
                            last = p; ia.Add(i);
                        }
                        return Subset(ia);
                    }
                default:
                    throw new Exception($"Unsupported type {Data[PositionsDef].GetType()}.");
            };
        }

        /// <summary>
        /// Removes points which are less than minDist from previous point (L1, Manhattan).
        /// </summary>
        public GenericChunk ImmutableFilterSequentialMinDistL1(double minDist)
        {
            if (minDist <= 0.0 || Count <= 1) return this;
            var minDistSquared = minDist * minDist;

            switch (Data[PositionsDef])
            {
                case V2f[] ps:
                    {
                        var last = V2f.MinValue;
                        var ia = new List<int>();
                        for (var i = 0; i < ps.Length; i++)
                        {
                            var p = ps[i];
                            if (Utils.DistLessThanL1(ref p, ref last, minDistSquared)) continue;
                            last = p; ia.Add(i);
                        }
                        return Subset(ia);
                    }
                case V2d[] ps:
                    {
                        var last = V2d.MinValue;
                        var ia = new List<int>();
                        for (var i = 0; i < ps.Length; i++)
                        {
                            var p = ps[i];
                            if (Utils.DistLessThanL1(ref p, ref last, minDistSquared)) continue;
                            last = p; ia.Add(i);
                        }
                        return Subset(ia);
                    }
                case V3f[] ps:
                    {
                        var last = V3f.MinValue;
                        var ia = new List<int>();
                        for (var i = 0; i < ps.Length; i++)
                        {
                            var p = ps[i];
                            if (Utils.DistLessThanL1(ref p, ref last, minDistSquared)) continue;
                            last = p; ia.Add(i);
                        }
                        return Subset(ia);
                    }
                case V3d[] ps:
                    {
                        var last = V3d.MinValue;
                        var ia = new List<int>();
                        for (var i = 0; i < ps.Length; i++)
                        {
                            var p = ps[i];
                            if (Utils.DistLessThanL1(ref p, ref last, minDistSquared)) continue;
                            last = p; ia.Add(i);
                        }
                        return Subset(ia);
                    }
                default:
                    throw new Exception($"Unsupported type {Data[PositionsDef].GetType()}.");
            };
        }

        /// <summary>
        /// Returns chunk with duplicate point positions removed.
        /// </summary>
        public GenericChunk ImmutableFilterMinDistByCell(Cell bounds, ParseConfig config)
        {
            if (config.MinDist <= 0.0 || Count <= 1) return this;

            var smallestCellExponent = Fun.Log2(config.MinDist).Ceiling();
            var positions = Data[PositionsDef] switch
            {
                V2f[] ps => ps.Map(p => (V3d)p.XYO),
                V2d[] ps => ps.Map(p => p.XYO),
                V3f[] ps => ps.Map(p => (V3d)p),
                V3d[] ps => ps,
                _ => throw new Exception($"Unsupported type {Data[PositionsDef].GetType()}.")
            };
            var take = new bool[Count];
            var foo = new List<int>(Count); for (var i = 0; i < Count; i++) foo.Add(i);
            Filter(bounds, foo).Wait();

            async Task Filter(Cell c, List<int> ia)
            {
#if DEBUG
                if (ia == null || ia.Count == 0) throw new InvalidOperationException();
                if (c.Exponent < smallestCellExponent) throw new InvalidOperationException();
#endif
                if (c.Exponent == smallestCellExponent)
                {
                    take[ia[0]] = true;
                    ia.Add(ia[0]);
                    return;
                }

                var center = c.GetCenter();
                var subias = new List<int>[8].SetByIndex(_ => new List<int>());
                for (var i = 0; i < ia.Count; i++)
                {
                    var p = positions[ia[i]];
                    var o = 0;
                    if (p.X >= center.X) o = 1;
                    if (p.Y >= center.Y) o |= 2;
                    if (p.Z >= center.Z) o |= 4;
                    subias[o].Add(ia[i]);
                }

                var ts = new List<Task>();
                for (var i = 0; i < 8; i++)
                {
                    if (subias[i].Count == 0) continue;
                    if (subias[i].Count == 1) { take[subias[i][0]] = true; continue; }
                    var _i = i;
                    var t = (subias[i].Count < 16384)
                        ? Filter(c.GetOctant(i), subias[i])
                        : Task.Run(() => Filter(c.GetOctant(_i), subias[_i]))
                        ;
                    ts.Add(t);
                }
                await Task.WhenAll(ts);
            }

            var ia = new List<int>();
            for (var i = 0; i < take.Length; i++) if (take[i]) ia.Add(i);

            if (config.Verbose)
            {
                var removedCount = Count - ia.Count;
                if (removedCount > 0)
                {
                    //Report.Line($"[ImmutableFilterMinDistByCell] {this.Count:N0} - {removedCount:N0} -> {ps.Count:N0}");
                }
            }

            return Subset(ia);
        }

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public GenericChunk ImmutableFilterByPosition(Func<V3d, bool> predicate)
        {
            var ia = new List<int>();
            switch (Data[PositionsDef])
            {
                case V2f[] xs: for (var i = 0; i < xs.Length; i++) if (predicate((V3d)xs[i].XYO)) ia.Add(i); break;
                case V2d[] xs: for (var i = 0; i < xs.Length; i++) if (predicate(xs[i].XYO)) ia.Add(i); break;
                case V3f[] xs: for (var i = 0; i < xs.Length; i++) if (predicate((V3d)xs[i])) ia.Add(i); break;
                case V3d[] xs: for (var i = 0; i < xs.Length; i++) if (predicate(xs[i])) ia.Add(i); break;
                default: throw new Exception($"Unsupported type {Data[PositionsDef].GetType()}.");
            }
            return Subset(ia);
        }

        /// <summary>
        /// Returns chunk with points which are inside given box.
        /// </summary>
        public GenericChunk ImmutableFilterByBox3d(Box3d filter)
            => ImmutableFilterByPosition(filter.Contains);

        /// <summary>
        /// Returns chunk with points p.XY which are inside given box.
        /// </summary>
        public GenericChunk ImmutableFilterByBoxXY(Box2d filter)
            => ImmutableFilterByPosition(p => p.X >= filter.Min.X && p.X < filter.Max.X && p.Y >= filter.Min.Y && p.Y < filter.Max.Y);

        /// <summary>
        /// Returns chunk with points which are inside given cell.
        /// </summary>
        public GenericChunk ImmutableFilterByCell(Cell filter)
            => ImmutableFilterByBox3d(filter.BoundingBox);

        private GenericChunk ImmutableFilterByColorGeneric<T>(Func<T, bool> predicate)
        {
            if (ColorsDef == null) return this;

            if (Data[ColorsDef] is T[] xs)
            {
                var ia = new List<int>();
                for (var i = 0; i < xs.Length; i++) if (predicate(xs[i])) ia.Add(i);
                return Subset(ia);
            }
            else
            {
                throw new Exception($"Unsupported type {Data[ColorsDef].GetType()}.");
            }
        }

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public GenericChunk ImmutableFilterByColor(Func<C3b, bool> predicate)
            => (ColorsDef == null) ? this : Data[ColorsDef] switch
            {
                C3b[] xs => ImmutableFilterByColorGeneric(predicate),
                C4b[] xs => ImmutableFilterByColorGeneric<C4b>(x => predicate(new C3b(x.R, x.G, x.B))),
                _ => throw new Exception($"Unsupported type {Data[ColorsDef].GetType()}."),
            };

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public GenericChunk ImmutableFilterByColor(Func<C4b, bool> predicate)
            => (ColorsDef == null) ? this : Data[ColorsDef] switch
            {
                C4b[] xs => ImmutableFilterByColorGeneric(predicate),
                _ => throw new Exception($"Unsupported type {Data[ColorsDef].GetType()}."),
            };

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public GenericChunk ImmutableFilterByColor(Func<C3f, bool> predicate)
            => (ColorsDef == null) ? this : Data[ColorsDef] switch
            {
                C3f[] xs => ImmutableFilterByColorGeneric(predicate),
                C4f[] xs => ImmutableFilterByColorGeneric<C4f>(x => predicate(new C3f(x.R, x.G, x.B))),
                _ => throw new Exception($"Unsupported type {Data[ColorsDef].GetType()}."),
            };

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public GenericChunk ImmutableFilterByColor(Func<C4f, bool> predicate)
            => (ColorsDef == null) ? this : Data[ColorsDef] switch
            {
                C4f[] xs => ImmutableFilterByColorGeneric(predicate),
                _ => throw new Exception($"Unsupported type {Data[ColorsDef].GetType()}."),
            };

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public GenericChunk ImmutableFilterByNormal(Func<V3f, bool> predicate)
        {
            if (NormalsDef == null) return this;

            var ia = new List<int>();
            switch (Data[NormalsDef])
            {
                case V3f[] xs: for (var i = 0; i < xs.Length; i++) if (predicate(xs[i])) ia.Add(i); break;
                default: throw new Exception($"Unsupported type {Data[NormalsDef].GetType()}.");
            }
            return Subset(ia);
        }

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public GenericChunk ImmutableFilterByIntensity(Func<int, bool> predicate)
        {
            if (IntensitiesDef == null) return this;

            var ia = new List<int>();
            switch (Data[IntensitiesDef])
            {
                case byte[] xs: for (var i = 0; i < xs.Length; i++) if (predicate(xs[i])) ia.Add(i); break;
                case sbyte[] xs: for (var i = 0; i < xs.Length; i++) if (predicate(xs[i])) ia.Add(i); break;
                case short[] xs: for (var i = 0; i < xs.Length; i++) if (predicate(xs[i])) ia.Add(i); break;
                case ushort[] xs: for (var i = 0; i < xs.Length; i++) if (predicate(xs[i])) ia.Add(i); break;
                case int[] xs: for (var i = 0; i < xs.Length; i++) if (predicate(xs[i])) ia.Add(i); break;
                default: throw new Exception($"Unsupported type {Data[IntensitiesDef].GetType()}.");
            }
            return Subset(ia);
        }

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public GenericChunk ImmutableFilterByClassification(Func<byte, bool> predicate)
        {
            if (ClassificationsDef == null) return this;

            var ia = new List<int>();
            switch (Data[ClassificationsDef])
            {
                case byte[] xs: for (var i = 0; i < xs.Length; i++) if (predicate(xs[i])) ia.Add(i); break;
                default: throw new Exception($"Unsupported type {Data[ClassificationsDef].GetType()}.");
            }
            return Subset(ia);
        }

        #endregion


        ///// <summary>
        ///// Appends two lists. Also works for null args: a + null -> a, null + b -> b, null + null -> null.
        ///// </summary>
        //private static IList<T> Append<T>(IList<T> l, IList<T> r)
        //{
        //    if (l == null) return r;
        //    if (r == null) return l;

        //    var ll = new List<T>(l);
        //    ll.AddRange(r);
        //    return ll;
        //}
    }
}
