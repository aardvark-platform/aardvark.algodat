/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

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
        private static IList<T> Append<T>(IList<T> l, IList<T> r)
        {
            if (l == null) return r;
            if (r == null) return l;

            var ll = new List<T>(l);
            ll.AddRange(r);
            return (List<T>)ll;
        }

        /// <summary></summary>
        public static readonly Chunk Empty = new Chunk(null, null, null, null, null, null);

        /// <summary></summary>
        public readonly IList<V3d> Positions;
        /// <summary></summary>
        public readonly IList<C4b> Colors;
        /// <summary></summary>
        public readonly IList<V3f> Normals;
        /// <summary></summary>
        public readonly IList<int> Intensities;
        /// <summary></summary>
        public readonly IList<byte> Classifications;
        /// <summary></summary>
        public readonly IList<V3f> Velocities;

        /// <summary></summary>
        public readonly Box3d BoundingBox;

        /// <summary></summary>
        public int Count => Positions != null ? Positions.Count : 0;

        /// <summary></summary>
        public bool IsEmpty => Count == 0;

        /// <summary></summary>
        public bool HasPositions => Positions != null && Positions.Count > 0;
        /// <summary></summary>
        public bool HasColors => Colors != null && Colors.Count > 0;
        /// <summary></summary>
        public bool HasNormals => Normals != null && Normals.Count > 0;
        /// <summary></summary>
        public bool HasIntensities => Intensities != null && Intensities.Count > 0;
        /// <summary></summary>
        public bool HasClassifications => Classifications != null && Classifications.Count > 0;
        /// <summary></summary>
        public bool HasVelocities => Velocities != null && Velocities.Count > 0;

        /// <summary>
        /// </summary>
        public static Chunk ImmutableMerge(Chunk a, Chunk b)
        {
            if (a.IsEmpty) return b;
            if (b.IsEmpty) return a;

            ImmutableList<V3d> ps = null;
            if (a.HasPositions)
            {
                var ps0 = (a.Positions is ImmutableList<V3d> x0) ? x0 : ImmutableList<V3d>.Empty.AddRange(a.Positions);
                var ps1 = (b.Positions is ImmutableList<V3d> x1) ? x1 : ImmutableList<V3d>.Empty.AddRange(b.Positions);
                ps = ps0.AddRange(ps1);
            }

            ImmutableList<C4b> cs = null;
            if (a.HasColors)
            {
                var cs0 = (a.Colors is ImmutableList<C4b> x2) ? x2 : ImmutableList<C4b>.Empty.AddRange(a.Colors);
                var cs1 = (b.Colors is ImmutableList<C4b> x3) ? x3 : ImmutableList<C4b>.Empty.AddRange(b.Colors);
                cs = cs0.AddRange(cs1);
            }

            ImmutableList<V3f> ns = null;
            if (a.HasNormals)
            {
                var ns0 = (a.Normals is ImmutableList<V3f> x4) ? x4 : ImmutableList<V3f>.Empty.AddRange(a.Normals);
                var ns1 = (b.Normals is ImmutableList<V3f> x5) ? x5 : ImmutableList<V3f>.Empty.AddRange(b.Normals);
                ns = ns0.AddRange(ns1);
            }

            ImmutableList<int> js = null;
            if (a.HasIntensities)
            {
                var js0 = (a.Intensities is ImmutableList<int> x6) ? x6 : ImmutableList<int>.Empty.AddRange(a.Intensities);
                var js1 = (b.Intensities is ImmutableList<int> x7) ? x7 : ImmutableList<int>.Empty.AddRange(b.Intensities);
                js = js0.AddRange(js1);
            }

            ImmutableList<byte> ks = null;
            if (a.HasClassifications)
            {
                var ks0 = (a.Classifications is ImmutableList<byte> x8) ? x8 : ImmutableList<byte>.Empty.AddRange(a.Classifications);
                var ks1 = (b.Classifications is ImmutableList<byte> x9) ? x9 : ImmutableList<byte>.Empty.AddRange(b.Classifications);
                ks = ks0.AddRange(ks1);
            }

            ImmutableList<V3f> vs = null;
            if (a.HasVelocities)
            {
                var vs0 = (a.Velocities is ImmutableList<V3f> x10) ? x10 : ImmutableList<V3f>.Empty.AddRange(a.Velocities);
                var vs1 = (b.Velocities is ImmutableList<V3f> x11) ? x11 : ImmutableList<V3f>.Empty.AddRange(b.Velocities);
                vs = vs0.AddRange(vs1);
            }

            return new Chunk(ps, cs, ns, js, ks, vs, new Box3d(a.BoundingBox, b.BoundingBox));
        }

        /// <summary>
        /// </summary>
        public static Chunk ImmutableMerge(params Chunk[] chunks)
        {
            if (chunks == null || chunks.Length == 0) return Empty;
            if (chunks.Length == 1) return chunks[0];

            var head = chunks[0];
            var totalCount = chunks.Sum(c => c.Count);

            var ps = head.HasPositions ? new V3d[totalCount] : null;
            var cs = head.HasColors ? new C4b[totalCount] : null;
            var ns = head.HasNormals ? new V3f[totalCount] : null;
            var js = head.HasIntensities ? new int[totalCount] : null;
            var ks = head.HasClassifications ? new byte[totalCount] : null;
            var vs = head.HasVelocities ? new V3f[totalCount] : null;

            var offset = 0;
            foreach (var chunk in chunks)
            {
                if (ps != null) chunk.Positions.CopyTo(ps, offset);
                if (cs != null) chunk.Colors.CopyTo(cs, offset);
                if (ns != null) chunk.Normals.CopyTo(ns, offset);
                if (js != null) chunk.Intensities.CopyTo(js, offset);
                if (ks != null) chunk.Classifications.CopyTo(ks, offset);
                if (vs != null) chunk.Velocities.CopyTo(vs, offset);
                offset += chunk.Count;
            }

            return new Chunk(ps, cs, ns, js, ks, vs, new Box3d(chunks.Select(x => x.BoundingBox)));
        }

        /// <summary>
        /// </summary>
        public static Chunk ImmutableMerge(IEnumerable<Chunk> chunks)
            => ImmutableMerge(chunks.ToArray());

        /// <summary>
        /// </summary>
        /// <param name="positions">Optional.</param>
        /// <param name="colors">Optional. Either null or same number of elements as positions.</param>
        /// <param name="normals">Optional. Either null or same number of elements as positions.</param>
        /// <param name="intensities">Optional. Either null or same number of elements as positions.</param>
        /// <param name="classifications">Optional. Either null or same number of elements as positions.</param>
        /// <param name="velocities">Optional. Either null or same number of elements as positions.</param>
        /// <param name="bbox">Optional. If null, then bbox will be constructed from positions.</param>
        public Chunk(
            IList<V3d> positions,
            IList<C4b> colors = null,
            IList<V3f> normals = null,
            IList<int> intensities = null,
            IList<byte> classifications = null,
            IList<V3f> velocities = null,
            Box3d? bbox = null
            )
        {
            //if (colors != null && colors.Count != positions?.Count) throw new ArgumentException(nameof(colors));
            if (normals != null && normals.Count != positions?.Count) throw new ArgumentException(nameof(normals));
            if (intensities != null && intensities.Count != positions?.Count) throw new ArgumentException(nameof(intensities));
            if (velocities != null && velocities.Count != positions?.Count) throw new ArgumentException(nameof(velocities));

            if (positions != null && colors != null && positions.Count != colors.Count)
            {
                colors = new C4b[positions.Count];
                Report.Warn("[Chunk-ctor] inconsistent length: pos.length = {0} vs cs.length = {1}", positions.Count, colors.Count);
            }

            Positions = positions != null && positions.Count > 0 ? positions : null;
            Colors = colors != null && colors.Count > 0 ? colors : null;
            Normals = normals != null && normals.Count > 0 ? normals : null;
            Intensities = intensities != null && intensities.Count > 0 ? intensities : null;
            Classifications = classifications != null && classifications.Count > 0 ? classifications : null;
            Velocities = velocities != null && velocities.Count > 0 ? velocities : null;
            BoundingBox = bbox ?? (positions != null ? new Box3d(positions) : Box3d.Invalid);
        }

        /// <summary>
        /// </summary>
        /// <param name="positions">Optional.</param>
        /// <param name="colors">Optional. Either null or same number of elements as positions.</param>
        /// <param name="normals">Optional. Either null or same number of elements as positions.</param>
        /// <param name="intensities">Optional. Either null or same number of elements as positions.</param>
        /// <param name="classifications">Optional. Either null or same number of elements as positions.</param>
        /// <param name="bbox">Optional. If null, then bbox will be constructed from positions.</param>
        public Chunk(IList<V3d> positions, IList<C4b> colors, IList<V3f> normals, IList<int> intensities, IList<byte> classifications, Box3d? bbox) 
            : this(positions, colors, normals, intensities, classifications, null, bbox)
        { }

        /// <summary>
        /// Creates new chunk which is union of this chunk and other. 
        /// </summary>
        public Chunk Union(Chunk other)
        {
            return new Chunk(
                Append(Positions, other.Positions),
                Append(Colors, other.Colors),
                Append(Normals, other.Normals),
                Append(Intensities, other.Intensities),
                Append(Classifications, other.Classifications),
                Append(Velocities, other.Velocities),
                Box.Union(BoundingBox, other.BoundingBox)
            );
        }

        /// <summary>
        /// Immutable update of positions.
        /// </summary>
        public Chunk WithPositions(IList<V3d> newPositions) => new Chunk(newPositions, Colors, Normals, Intensities, Classifications);
        
        /// <summary>
        /// Immutable update of colors.
        /// </summary>
        public Chunk WithColors(IList<C4b> newColors) => new Chunk(Positions, newColors, Normals, Intensities, Classifications, Velocities, BoundingBox);

        /// <summary>
        /// Immutable update of normals.
        /// </summary>
        public Chunk WithNormals(IList<V3f> newNormals) => new Chunk(Positions, Colors, newNormals, Intensities, Classifications, Velocities, BoundingBox);
        
        /// <summary>
        /// Immutable update of normals.
        /// </summary>
        public Chunk WithIntensities(IList<int> newIntensities) => new Chunk(Positions, Colors, Normals, newIntensities, Classifications, Velocities, BoundingBox);
        
        /// <summary>
        /// Immutable update of classifications.
        /// </summary>
        public Chunk WithClassifications(IList<byte> newClassifications) => new Chunk(Positions, Colors, Normals, Intensities, newClassifications, Velocities, BoundingBox);
        
        /// <summary>
        /// Returns chunk with duplicate point positions removed.
        /// </summary>
        public Chunk ImmutableDeduplicate(bool verbose)
        {
            if (!HasPositions) return this;

            var dedup = new HashSet<V3d>();
            var ia = new List<int>();
            for (var i = 0; i < Count; i++)
            {
                if (dedup.Add(Positions[i])) ia.Add(i);
            }
            var hasDuplicates = ia.Count < Count;

            if (hasDuplicates)
            {
                var ps = HasPositions ? ia.Map(i => Positions[i]) : null;
                var cs = HasColors ? ia.Map(i => Colors[i]) : null;
                var ns = HasNormals ? ia.Map(i => Normals[i]) : null;
                var js = HasIntensities ? ia.Map(i => Intensities[i]) : null;
                var ks = HasClassifications ? ia.Map(i => Classifications[i]) : null;

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
        
        /// <summary>
        /// Removes points which are less than minDist from previous point.
        /// </summary>
        public Chunk ImmutableMapPositions(Func<V3d, V3d> mapping)
            => new Chunk(Positions.Map(mapping), Colors, Normals, Intensities, Classifications);

        /// <summary>
        /// </summary>
        public Chunk ImmutableMergeWith(IEnumerable<Chunk> others)
            => ImmutableMerge(this, ImmutableMerge(others));

        /// <summary>
        /// </summary>
        public Chunk ImmutableMergeWith(params Chunk[] others)
            => ImmutableMerge(this, ImmutableMerge(others));

        /// <summary>
        /// Splits this chunk into multiple chunks according to key of i-th point in chunk.
        /// </summary>
        public Dictionary<TKey, Chunk> GroupBy<TKey>(Func<Chunk, int, TKey> keySelector)
        {
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
                    HasPositions ? ia.Map(i => Positions[i]) : null,
                    HasColors ? ia.Map(i => Colors[i]) : null,
                    HasNormals ? ia.Map(i => Normals[i]) : null,
                    HasIntensities ? ia.Map(i => Intensities[i]) : null,
                    HasClassifications ? ia.Map(i => Classifications[i]) : null
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
            var ps = HasPositions ? new List<V3d>() : null;
            var cs = HasColors ? new List<C4b>() : null;
            var ns = HasNormals ? new List<V3f>() : null;
            var js = HasIntensities ? new List<int>() : null;
            var ks = HasClassifications ? new List<byte>() : null;

            for (var i = 0; i < Count; i++)
            {
                if (predicate(this, i))
                {
                    if (ps != null) ps.Add(Positions[i]);
                    if (cs != null) cs.Add(Colors[i]);
                    if (ns != null) ns.Add(Normals[i]);
                    if (js != null) js.Add(Intensities[i]);
                    if (ks != null) ks.Add(Classifications[i]);
                }
            }
            return new Chunk(ps, cs, ns, js, ks);
        }

        /// <summary>
        /// Removes points which are less than minDist from previous point (L2, Euclidean).
        /// </summary>
        public Chunk ImmutableFilterSequentialMinDistL2(double minDist)
        {
            if (minDist < 0.0) throw new ArgumentOutOfRangeException(nameof(minDist));
            var minDistSquared = minDist * minDist;

            var ps = new List<V3d>();
            var cs = Colors != null ? new List<C4b>() : null;
            var ns = Normals != null ? new List<V3f>() : null;
            var js = Intensities != null ? new List<int>() : null;
            var ks = Classifications != null ? new List<byte>() : null;

            var last = V3d.MinValue;
            for (var i = 0; i < Positions.Count; i++)
            {
                var p = Positions[i];

                if (Utils.DistLessThanL2(ref p, ref last, minDistSquared)) continue;

                last = p;
                ps.Add(p);
                if (cs != null) cs.Add(Colors[i]);
                if (ns != null) ns.Add(Normals[i]);
                if (js != null) js.Add(Intensities[i]);
                if (ks != null) ks.Add(Classifications[i]);
            }
            return new Chunk(ps, cs, ns, js, ks);
        }

        /// <summary>
        /// Removes points which are less than minDist from previous point (L1, Manhattan).
        /// </summary>
        public Chunk ImmutableFilterSequentialMinDistL1(double minDist)
        {
            if (minDist <= 0.0 || Positions == null) return this;

            var ps = new List<V3d>();
            var cs = Colors != null ? new List<C4b>() : null;
            var ns = Normals != null ? new List<V3f>() : null;
            var js = Intensities != null ? new List<int>() : null;
            var ks = Classifications != null ? new List<byte>() : null;

            var prev = V3d.MinValue;
            for (var i = 0; i < Positions.Count; i++)
            {
                var p = Positions[i];

                if (Utils.DistLessThanL1(ref p, ref prev, minDist)) continue;

                prev = p;
                ps.Add(p);
                if (cs != null) cs.Add(Colors[i]);
                if (ns != null) ns.Add(Normals[i]);
                if (js != null) js.Add(Intensities[i]);
                if (ks != null) ks.Add(Classifications[i]);
            }
            return new Chunk(ps, cs, ns, js);
        }

        /// <summary>
        /// Returns chunk with duplicate point positions removed.
        /// </summary>
        public Chunk ImmutableFilterMinDistByCell(Cell bounds, ParseConfig config)
        {
            if (!HasPositions || Positions.Count <= 1) return this;

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

            var ps = Positions.Where((_, i) => take[i]).ToList();
            var cs = HasColors ? Colors.Where((_, i) => take[i]).ToList() : null;
            var ns = HasNormals ? Normals.Where((_, i) => take[i]).ToList() : null;
            var js = HasIntensities ? Intensities.Where((_, i) => take[i]).ToList() : null;
            if (config.Verbose)
            {
                var removedCount = Count - ps.Count;
                if (removedCount > 0)
                {
                    //Report.Line($"[ImmutableFilterMinDistByCell] {this.Count:N0} - {removedCount:N0} -> {ps.Count:N0}");
                }
            }
            return new Chunk(ps, cs, ns, js);
        }

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByPosition(Func<V3d, bool> predicate)
            => ImmutableFilter((c, i) => predicate(c.Positions[i]));

        /// <summary>
        /// Returns chunk with points which are inside given box.
        /// </summary>
        public Chunk ImmutableFilterByBox3d(Box3d filter)
            => ImmutableFilter((c, i) => filter.Contains(c.Positions[i]));

        /// <summary>
        /// Returns chunk with points p.XY which are inside given box.
        /// </summary>
        public Chunk ImmutableFilterByBoxXY(Box2d filter)
            => ImmutableFilter((c, i) => { var p = c.Positions[i]; return p.X >= filter.Min.X && p.X < filter.Max.X && p.Y >= filter.Min.Y && p.Y < filter.Max.Y; });

        /// <summary>
        /// Returns chunk with points which are inside given cell.
        /// </summary>
        public Chunk ImmutableFilterByCell(Cell filter)
        {
            var bb = filter.BoundingBox;
            return ImmutableFilter((c, i) => bb.Contains(c.Positions[i]));
        }

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByColor(Func<C4b, bool> predicate)
            => ImmutableFilter((c, i) => predicate(c.Colors[i]));

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByNormal(Func<V3f, bool> predicate)
            => ImmutableFilter((c, i) => predicate(c.Normals[i]));

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByIntensity(Func<int, bool> predicate)
            => ImmutableFilter((c, i) => predicate(c.Intensities[i]));

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByClassification(Func<byte, bool> predicate)
            => ImmutableFilter((c, i) => predicate(c.Classifications[i]));

        #endregion
    }
}
