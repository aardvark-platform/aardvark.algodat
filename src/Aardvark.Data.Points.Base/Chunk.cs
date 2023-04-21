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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

#pragma warning disable CS1591

namespace Aardvark.Data.Points
{
    /// <summary>
    /// Parsers emit a sequence of chunks of points with optional colors, normals, and intensities.
    /// </summary>
    public class Chunk
    {
        /// <summary>
        /// Appends two lists. Also works for null args: a + null -> a, null + b -> b, null + null -> null.
        /// </summary>
        private static IList<T>? Append<T>(IList<T>? l, IList<T>? r)
        {
            if (l == null) return r;
            if (r == null) return l;

            var ll = new List<T>(l);
            ll.AddRange(r);
            return ll;
        }

        public static readonly Chunk Empty = new(Array.Empty<V3d>(), null, null, null, null);

        public readonly IList<V3d> Positions;
        public readonly IList<C4b>? Colors;
        public readonly IList<V3f>? Normals;
        public readonly IList<int>? Intensities;
        public readonly IList<byte>? Classifications;

        public GenericChunk ToGenericChunk()
        {
            var data = ImmutableDictionary<Durable.Def, object>.Empty
                .Add(GenericChunk.Defs.Positions3d, Positions.ToArray())
                ;
            if (Colors != null) data = data.Add(GenericChunk.Defs.Colors4b, Colors.ToArray());
            if (Normals != null) data = data.Add(GenericChunk.Defs.Normals3f, Normals.ToArray());
            if (Intensities != null) data = data.Add(GenericChunk.Defs.Intensities1i, Intensities.ToArray());
            if (Classifications != null) data = data.Add(GenericChunk.Defs.Classifications1b, Classifications.ToArray());
            return new GenericChunk(data, BoundingBox);
        }

        public readonly Box3d BoundingBox;

        public int Count => Positions.Count;

        public bool IsEmpty => Count == 0;

        public bool HasPositions => true;

        [MemberNotNullWhen(true, nameof(Colors))]
        public bool HasColors => Colors != null;

        [MemberNotNullWhen(true, nameof(Normals))]
        public bool HasNormals => Normals != null;

        [MemberNotNullWhen(true, nameof(Intensities))]
        public bool HasIntensities => Intensities != null;

        [MemberNotNullWhen(true, nameof(Classifications))]
        public bool HasClassifications => Classifications != null;

        public static Chunk ImmutableMerge(Chunk a, Chunk b)
        {
            if (a is null || a.IsEmpty) return b;
            if (b is null || b.IsEmpty) return a;

            ImmutableList<V3d> ps;
            {
                var ps0 = (a.Positions is ImmutableList<V3d> x0) ? x0 : ImmutableList<V3d>.Empty.AddRange(a.Positions);
                var ps1 = (b.Positions is ImmutableList<V3d> x1) ? x1 : ImmutableList<V3d>.Empty.AddRange(b.Positions);
                ps = ps0.AddRange(ps1);
            }

            ImmutableList<C4b>? cs = null;
            if (a.HasColors)
            {
                var cs0 = (a.Colors is ImmutableList<C4b> x2) ? x2 : ImmutableList<C4b>.Empty.AddRange(a.Colors);
                if (b.HasColors)
                {
                    var cs1 = (b.Colors is ImmutableList<C4b> x3) ? x3 : ImmutableList<C4b>.Empty.AddRange(b.Colors);
                    cs = cs0.AddRange(cs1);
                }
                else
                {
                    cs = cs0;
                }
            }

            ImmutableList<V3f>? ns = null;
            if (a.HasNormals)
            {
                var ns0 = (a.Normals is ImmutableList<V3f> x4) ? x4 : ImmutableList<V3f>.Empty.AddRange(a.Normals);
                if (b.HasNormals)
                {
                    var ns1 = (b.Normals is ImmutableList<V3f> x5) ? x5 : ImmutableList<V3f>.Empty.AddRange(b.Normals);
                    ns = ns0.AddRange(ns1);
                }
                else
                {
                    ns = ns0;
                }
            }

            ImmutableList<int>? js = null;
            if (a.HasIntensities)
            {
                var js0 = (a.Intensities is ImmutableList<int> x6) ? x6 : ImmutableList<int>.Empty.AddRange(a.Intensities);
                if (b.HasIntensities)
                {
                    var js1 = (b.Intensities is ImmutableList<int> x7) ? x7 : ImmutableList<int>.Empty.AddRange(b.Intensities);
                    js = js0.AddRange(js1);
                }
                else
                {
                    js = js0;
                }
            }

            ImmutableList<byte>? ks = null;
            if (a.HasClassifications)
            {
                var ks0 = (a.Classifications is ImmutableList<byte> x8) ? x8 : ImmutableList<byte>.Empty.AddRange(a.Classifications);
                if (b.HasClassifications)
                {
                    var ks1 = (b.Classifications is ImmutableList<byte> x9) ? x9 : ImmutableList<byte>.Empty.AddRange(b.Classifications);
                    ks = ks0.AddRange(ks1);
                }
                else
                {
                    ks = ks0;
                }
            }

            return new Chunk(ps, cs, ns, js, ks, new Box3d(a.BoundingBox, b.BoundingBox));
        }

        public static Chunk ImmutableMerge(params Chunk[] chunks)
        {
            if (chunks == null || chunks.Length == 0) return Empty;
            if (chunks.Length == 1) return chunks[0];

            var head = chunks[0];
            var totalCount = chunks.Sum(c => c.Count);

            var ps = new V3d[totalCount];
            var cs = head.HasColors ? new C4b[totalCount] : null;
            var ns = head.HasNormals ? new V3f[totalCount] : null;
            var js = head.HasIntensities ? new int[totalCount] : null;
            var ks = head.HasClassifications ? new byte[totalCount] : null;

            var offset = 0;
            foreach (var chunk in chunks)
            {
                if (chunk.IsEmpty) continue;

#pragma warning disable CS8602
                if (ps != null) chunk.Positions.CopyTo(ps, offset);
                if (cs != null) chunk.Colors.CopyTo(cs, offset);
                if (ns != null) chunk.Normals.CopyTo(ns, offset);
                if (js != null) chunk.Intensities.CopyTo(js, offset);
                if (ks != null) chunk.Classifications.CopyTo(ks, offset);
#pragma warning restore CS8602

                offset += chunk.Count;
            }

            if (ps == null) throw new Exception("Invariant 4cc7d585-9a46-4ba2-892a-95fce9ed06da.");
            return new Chunk(ps, cs, ns, js, ks, new Box3d(chunks.Select(x => x.BoundingBox)));
        }

        public static Chunk ImmutableMerge(IEnumerable<Chunk> chunks)
            => ImmutableMerge(chunks.ToArray());

        /// <summary>
        /// </summary>
        /// <param name="positions">Optional.</param>
        /// <param name="colors">Optional. Either null or same number of elements as positions.</param>
        /// <param name="normals">Optional. Either null or same number of elements as positions.</param>
        /// <param name="intensities">Optional. Either null or same number of elements as positions.</param>
        /// <param name="classifications">Optional. Either null or same number of elements as positions.</param>
        /// <param name="bbox">Optional. If null, then bbox will be constructed from positions.</param>
        public Chunk(
            IList<V3d> positions,
            IList<C4b>? colors = null,
            IList<V3f>? normals = null,
            IList<int>? intensities = null,
            IList<byte>? classifications = null,
            Box3d? bbox = null
            )
        {
            if (positions == null) throw new ArgumentNullException(
                nameof(positions), "Error ad93ff8e-d1a9-4c96-a626-43c69617371e."
                );

            var countMismatch = false;

            if (colors != null && colors.Count != positions.Count)
            {
                countMismatch = true; //throw new ArgumentException(nameof(colors));
                Report.Warn($"Expected {positions.Count} color values, but found {colors.Count}. " +
                    $"Warning 3c3ac3d4-c4de-4ad6-a925-d2fbaece1634."
                    );
            }
            if (normals != null && normals.Count != positions.Count)
            {
                countMismatch = true; //throw new ArgumentException(nameof(normals));
                Report.Warn($"Expected {positions.Count} normal values, but found {normals.Count}. " +
                    $"Warning 0e6b1e22-25a1-4814-8dc7-add137edcd45."
                    );
            }
            if (intensities != null && intensities.Count != positions.Count)
            {
                countMismatch = true; //throw new ArgumentException(nameof(intensities));
                Report.Warn($"Expected {positions.Count} intensity values, but found {intensities.Count}. " +
                    $"Warning ea548345-6f32-4504-8189-4dd4a4531708."
                    );
            }
            if (classifications != null && classifications.Count != positions.Count)
            {
                countMismatch = true; //throw new ArgumentException(nameof(classifications));
                Report.Warn(
                    $"Expected {positions.Count} classification values, but found {classifications.Count}. " +
                    $"Warning 0c82c851-986f-4690-980b-7a3a240bf499."
                    );
            }

            if (countMismatch)
            {
                var minCount = positions.Count;
                if (colors          != null && colors.Count          != positions.Count) minCount = Math.Min(minCount, colors.Count         );
                if (normals         != null && normals.Count         != positions.Count) minCount = Math.Min(minCount, normals.Count        );
                if (intensities     != null && intensities.Count     != positions.Count) minCount = Math.Min(minCount, intensities.Count    );
                if (classifications != null && classifications.Count != positions.Count) minCount = Math.Min(minCount, classifications.Count);

                if (                           positions      .Count != minCount) positions       = positions      .Take(minCount).ToArray();
                if (colors          != null && colors         .Count != minCount) colors          = colors         .Take(minCount).ToArray();
                if (normals         != null && normals        .Count != minCount) normals         = normals        .Take(minCount).ToArray();
                if (intensities     != null && intensities    .Count != minCount) intensities     = intensities    .Take(minCount).ToArray();
                if (classifications != null && classifications.Count != minCount) classifications = classifications.Take(minCount).ToArray();
            }

            //if (colors != null && positions.Count != colors.Count)
            //{
            //    Report.Warn("[Chunk-ctor] inconsistent length: pos.length = {0} vs cs.length = {1}", positions.Count, colors.Count);
            //    colors = new C4b[positions.Count];
            //}

            //if (positions.Any(p => p.IsNaN)) throw new ArgumentException("One or more positions are NaN.");

            Positions       = positions;
            Colors          = colors;
            Normals         = normals;
            Intensities     = intensities;
            Classifications = classifications;
            BoundingBox     = bbox ?? (positions.Count > 0 ? new Box3d(positions) : Box3d.Invalid);
        }

        public IEnumerable<Chunk> Split(int chunksize)
        {
            if (chunksize < 1) throw new Exception();
            if (chunksize >= Count)
            {
                yield return this;
            }
            else
            {
                var i = 0;
                while (i < Count)
                {
                    yield return new Chunk(
                        Positions.Skip(i).Take(chunksize).ToArray(),
                        colors: HasColors ? Colors.Skip(i).Take(chunksize).ToArray() : null,
                        normals: HasNormals ? Normals.Skip(i).Take(chunksize).ToArray() : null,
                        intensities: HasIntensities ? Intensities.Skip(i).Take(chunksize).ToArray() : null,
                        classifications: HasClassifications ? Classifications.Skip(i).Take(chunksize).ToArray() : null
                        );

                    i += chunksize;
                }
            }
        }

        /// <summary>
        /// Creates new chunk which is union of this chunk and other. 
        /// </summary>
        public Chunk Union(Chunk other)
        {
            if (other.IsEmpty) return this;

            var ps = Append(Positions, other.Positions);
            if (ps == null) throw new Exception("Invariant c799ccc6-4edf-4530-b1eb-59d93ef69727.");
            return new Chunk(
                ps,
                Append(Colors, other.Colors),
                Append(Normals, other.Normals),
                Append(Intensities, other.Intensities),
                Append(Classifications, other.Classifications),
                Box.Union(BoundingBox, other.BoundingBox)
            );
        }

        /// <summary>
        /// Immutable update of positions.
        /// </summary>
        public Chunk WithPositions(IList<V3d> newPositions) => new(newPositions, Colors, Normals, Intensities, Classifications);

        /// <summary>
        /// Immutable update of colors.
        /// </summary>
        public Chunk WithColors(IList<C4b> newColors) => new(Positions, newColors, Normals, Intensities, Classifications, BoundingBox);

        /// <summary>
        /// Immutable update of normals.
        /// </summary>
        public Chunk WithNormals(IList<V3f> newNormals) => new(Positions, Colors, newNormals, Intensities, Classifications, BoundingBox);

        /// <summary>
        /// Immutable update of normals.
        /// </summary>
        public Chunk WithIntensities(IList<int> newIntensities) => new(Positions, Colors, Normals, newIntensities, Classifications, BoundingBox);

        /// <summary>
        /// Immutable update of classifications.
        /// </summary>
        public Chunk WithClassifications(IList<byte> newClassifications) => new(Positions, Colors, Normals, Intensities, newClassifications, BoundingBox);

        /// <summary>
        /// Returns chunk with duplicate point positions removed.
        /// </summary>
        public Chunk ImmutableDeduplicate(bool verbose)
        {
            if (IsEmpty) return Empty;
            if (!HasPositions || Positions == null) return this;

            var dedup = new HashSet<V3d>();
            var ia = new List<int>();
            for (var i = 0; i < Count; i++)
            {
                if (dedup.Add(Positions[i])) ia.Add(i);
            }
            var hasDuplicates = ia.Count < Count;

            if (hasDuplicates)
            {
                T[]? f<T>(bool has, IList<T>? xs) => (has && xs != null) ? ia.MapToArray(i => xs[i]) : null;
                var ps = f(HasPositions, Positions); if (ps == null) throw new InvalidOperationException();
                var cs = f(HasColors, Colors);
                var ns = f(HasNormals, Normals);
                var js = f(HasIntensities, Intensities);
                var ks = f(HasClassifications, Classifications);

                if (verbose)
                {
                    var removedCount = Positions.Count - ps.Length;
                    var removedPercent = (removedCount / (double)Positions.Count) * 100.0;
                    Report.Line($"removed {removedCount:N0} duplicate points ({removedPercent:0.00}% of {Positions.Count:N0})");
#if DEBUG
                    if (ps.Length == 1) Report.Warn($"Bam! Complete chunk collapsed to a single point.");
#endif
                }

                return new Chunk(ps, cs, ns, js, ks);
            }
            else
            {
                return this;
            }
        }

        public Chunk ImmutableMapPositions(Func<V3d, V3d> mapping)
            => IsEmpty ? Empty : new(Positions.Map(mapping), Colors, Normals, Intensities, Classifications);

        public Chunk ImmutableMergeWith(IEnumerable<Chunk> others)
            => ImmutableMerge(this, ImmutableMerge(others));

        public Chunk ImmutableMergeWith(params Chunk[] others)
            => ImmutableMerge(this, ImmutableMerge(others));

        /// <summary>
        /// Splits this chunk into multiple chunks according to key of i-th point in chunk.
        /// </summary>
        public Dictionary<TKey, Chunk> GroupBy<TKey>(Func<Chunk, int, TKey> keySelector)
        {
            if (IsEmpty) return new Dictionary<TKey, Chunk>();

            var dict = new Dictionary<TKey, List<int>>();
            for (var i = 0; i < Count; i++)
            {
                var k = keySelector(this, i);
                if (!dict.TryGetValue(k, out var ia)) dict[k] = ia = new List<int>();
                ia.Add(i);
            }

            var result = new Dictionary<TKey, Chunk>();
            foreach (var kv in dict)
            {
                var ia = kv.Value;
                result[kv.Key] = new Chunk(
                    ia.Map(i => Positions[i]),
                    Colors          != null ? ia.Map(i => Colors[i])          : null,
                    Normals         != null ? ia.Map(i => Normals[i])         : null,
                    Intensities     != null ? ia.Map(i => Intensities[i])     : null,
                    Classifications != null ? ia.Map(i => Classifications[i]) : null
                    );
            }
            return result;
        }

        #region ImmutableFilter...

        /// <summary>
        /// Returns chunk with points for which predicate is true.
        /// </summary>
        public Chunk ImmutableFilter(Func<Chunk, int, bool> predicate)
        {
            if (IsEmpty) return Empty;

            var ps = new List<V3d>();
            var cs = HasColors ? new List<C4b>() : null;
            var ns = HasNormals ? new List<V3f>() : null;
            var js = HasIntensities ? new List<int>() : null;
            var ks = HasClassifications ? new List<byte>() : null;

            for (var i = 0; i < Count; i++)
            {
                if (predicate(this, i))
                {
#pragma warning disable CS8602
                    ps.Add(Positions[i]);
                    if (cs != null) cs.Add(Colors[i]);
                    if (ns != null) ns.Add(Normals[i]);
                    if (js != null) js.Add(Intensities[i]);
                    if (ks != null) ks.Add(Classifications[i]);
#pragma warning restore CS8602
                }
            }

            return new Chunk(ps, cs, ns, js, ks);
        }

        /// <summary>
        /// Removes points which are less than minDist from previous point (L2, Euclidean).
        /// </summary>
        public Chunk ImmutableFilterSequentialMinDistL2(double minDist)
        {
            if (IsEmpty) return Empty;
            if (minDist <= 0.0 || !HasPositions) return this;
            var minDistSquared = minDist * minDist;

            var ps = new List<V3d>();
            var cs = Colors != null ? new List<C4b>() : null;
            var ns = Normals != null ? new List<V3f>() : null;
            var js = Intensities != null ? new List<int>() : null;
            var ks = Classifications != null ? new List<byte>() : null;

            var last = V3d.MinValue;
            if (Positions == null) throw new InvalidOperationException();
            for (var i = 0; i < Positions.Count; i++)
            {
                var p = Positions[i];

                if (Utils.DistLessThanL2(ref p, ref last, minDistSquared)) continue;

                last = p;
                ps.Add(p);
#pragma warning disable CS8602
                if (cs != null) cs.Add(Colors[i]);
                if (ns != null) ns.Add(Normals[i]);
                if (js != null) js.Add(Intensities[i]);
                if (ks != null) ks.Add(Classifications[i]);
#pragma warning restore CS8602
            }
            return new Chunk(ps, cs, ns, js, ks);
        }

        /// <summary>
        /// Removes points which are less than minDist from previous point (L1, Manhattan).
        /// </summary>
        public Chunk ImmutableFilterSequentialMinDistL1(double minDist)
        {
            if (IsEmpty) return Empty;
            if (minDist <= 0.0 || !HasPositions) return this;

            var ps = new List<V3d>();
            var cs = Colors != null ? new List<C4b>() : null;
            var ns = Normals != null ? new List<V3f>() : null;
            var js = Intensities != null ? new List<int>() : null;
            var ks = Classifications != null ? new List<byte>() : null;

            var prev = V3d.MinValue;
            if (Positions == null) throw new InvalidOperationException();
            for (var i = 0; i < Positions.Count; i++)
            {
                var p = Positions[i];

                if (Utils.DistLessThanL1(ref p, ref prev, minDist)) continue;

                prev = p;
                ps.Add(p);
#pragma warning disable CS8602
                if (cs != null) cs.Add(Colors[i]);
                if (ns != null) ns.Add(Normals[i]);
                if (js != null) js.Add(Intensities[i]);
                if (ks != null) ks.Add(Classifications[i]);
#pragma warning restore CS8602
            }
            return new Chunk(ps, cs, ns, js, ks);
        }

        /// <summary>
        /// Returns chunk with duplicate point positions removed.
        /// </summary>
        public Chunk ImmutableFilterMinDistByCell(Cell bounds, ParseConfig config)
        {
            if (IsEmpty) return Empty;
            if (config.MinDist <= 0.0 || !HasPositions) return this;
            if (Positions == null) throw new InvalidOperationException();

            var smallestCellExponent = Fun.Log2(config.MinDist).Ceiling();
            var positions = Positions;
            var take = new bool[Count];
            var foo = new List<int>(positions.Count); for (var i = 0; i < positions.Count; i++) foo.Add(i);
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

            List<T>? f<T>(bool has, IList<T>? xs) => has ? xs.Where((_, i) => take[i]).ToList() : null;
            var ps = f(HasPositions, Positions);
            var cs = f(HasColors, Colors);
            var ns = f(HasNormals, Normals);
            var js = f(HasIntensities, Intensities);
            var ks = f(HasClassifications, Classifications);

            if (config.Verbose)
            {
                var removedCount = Count - ps?.Count;
                if (removedCount > 0)
                {
                    //Report.Line($"[ImmutableFilterMinDistByCell] {this.Count:N0} - {removedCount:N0} -> {ps.Count:N0}");
                }
            }

            if (ps == null) throw new Exception("Invariant 84440204-5496-479f-ad3e-9e45e5dd16c1.");
            return new Chunk(ps, cs, ns, js, ks);
        }

#pragma warning disable CS8602

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByPosition(Func<V3d, bool> predicate)
            => Positions != null ? ImmutableFilter((c, i) => predicate(c.Positions[i])) : this;

        /// <summary>
        /// Returns chunk with points which are inside given box.
        /// </summary>
        public Chunk ImmutableFilterByBox3d(Box3d filter)
            => Positions != null ? ImmutableFilter((c, i) => filter.Contains(c.Positions[i])) : this;

        /// <summary>
        /// Returns chunk with points p.XY which are inside given box.
        /// </summary>
        public Chunk ImmutableFilterByBoxXY(Box2d filter)
            => Positions != null ? ImmutableFilter((c, i) => { var p = c.Positions[i]; return p.X >= filter.Min.X && p.X < filter.Max.X && p.Y >= filter.Min.Y && p.Y < filter.Max.Y; }) : this;

        /// <summary>
        /// Returns chunk with points which are inside given cell.
        /// </summary>
        public Chunk ImmutableFilterByCell(Cell filter)
        {
            var bb = filter.BoundingBox;
            return Positions != null ? ImmutableFilter((c, i) => bb.Contains(c.Positions[i])) : this;
        }

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByColor(Func<C4b, bool> predicate)
            => Colors != null ? ImmutableFilter((c, i) => predicate(c.Colors[i])) : this;

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByNormal(Func<V3f, bool> predicate)
            => Normals != null ? ImmutableFilter((c, i) => predicate(c.Normals[i])) : this;

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByIntensity(Func<int, bool> predicate)
            => Intensities != null ? ImmutableFilter((c, i) => predicate(c.Intensities[i])) : this;

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByClassification(Func<byte, bool> predicate)
            => Classifications != null ? ImmutableFilter((c, i) => predicate(c.Classifications[i])) : this;

#pragma warning restore CS8602

        #endregion
    }
}