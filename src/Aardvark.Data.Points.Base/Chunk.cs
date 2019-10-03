/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
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
    public struct Chunk
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
        public static readonly Chunk Empty = new Chunk();

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

            return new Chunk(ps, cs, ns, js, ks, new Box3d(a.BoundingBox, b.BoundingBox));
        }

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
            IList<C4b> colors = null,
            IList<V3f> normals = null,
            IList<int> intensities = null,
            IList<byte> classifications = null,
            Box3d? bbox = null
            )
        {
            //if (colors != null && colors.Count != positions?.Count) throw new ArgumentException(nameof(colors));
            if (normals != null && normals.Count != positions?.Count) throw new ArgumentException(nameof(normals));
            if (intensities != null && intensities.Count != positions?.Count) throw new ArgumentException(nameof(intensities));

            if (positions != null && colors != null && positions.Count != colors.Count)
            {
                colors = new C4b[positions.Count];
                Report.Warn("[Chunk-ctor] inconsistent length: pos.length = {0} vs cs.length = {1}", positions.Count, colors.Count);
            }

            Positions = positions;
            Colors = colors;
            Normals = normals;
            Intensities = intensities;
            Classifications = classifications;
            BoundingBox = bbox ?? new Box3d(positions);
        }

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
                Box3d.Union(BoundingBox, other.BoundingBox)
            );
        }


        /// <summary>
        /// Immutable update of positions.
        /// </summary>
        public Chunk WithPositions(IList<V3d> newPositions) => new Chunk(newPositions, Colors, Normals, Intensities, Classifications);
        
        /// <summary>
        /// Immutable update of colors.
        /// </summary>
        public Chunk WithColors(IList<C4b> newColors) => new Chunk(Positions, newColors, Normals, Intensities, Classifications, BoundingBox);

        /// <summary>
        /// Immutable update of normals.
        /// </summary>
        public Chunk WithNormals(IList<V3f> newNormals) => new Chunk(Positions, Colors, newNormals, Intensities, Classifications, BoundingBox);
        
        /// <summary>
        /// Immutable update of normals.
        /// </summary>
        public Chunk WithIntensities(IList<int> newIntensities) => new Chunk(Positions, Colors, Normals, newIntensities, Classifications, BoundingBox);
        
        /// <summary>
        /// Immutable update of classifications.
        /// </summary>
        public Chunk WithClassifications(IList<byte> newClassifications) => new Chunk(Positions, Colors, Normals, Intensities, newClassifications, BoundingBox);
        
        /// <summary>
        /// Removes points which are less than minDist from previous point (L2, Euclidean).
        /// </summary>
        public Chunk ImmutableFilterSequentialMinDistL2(double minDist)
        {
            if (minDist <= 0.0 || Positions == null) return this;
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
            if (!HasPositions) return this;

            var smallestCellExponent = Fun.Log2(config.MinDist).Ceiling();
            var positions = Positions;
            var take = new bool[Count];
            var foo = new List<int>(positions.Count); for (var i = 0; i < positions.Count; i++) foo.Add(i);
            filter(bounds, foo).Wait();

            async Task filter(Cell c, List<int> ia)
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
                        ? filter(c.GetOctant(i), subias[i])
                        : Task.Run(() => filter(c.GetOctant(_i), subias[_i]))
                        ;
                    ts.Add(t);
                }
                await Task.WhenAll(ts);
            }

            var self = this;
            var ps = Positions.Where((_, i) => take[i]).ToList();
            var cs = HasColors ? Colors.Where((_, i) => take[i]).ToList() : null;
            var ns = HasNormals ? Normals.Where((_, i) => take[i]).ToList() : null;
            var js = HasIntensities ? Intensities.Where((_, i) => take[i]).ToList() : null;
            if (config.Verbose)
            {
                var removedCount = this.Count - ps.Count;
                if (removedCount > 0)
                {
                    //Report.Line($"[ImmutableFilterMinDistByCell] {this.Count:N0} - {removedCount:N0} -> {ps.Count:N0}");
                }
            }
            return new Chunk(ps, cs, ns, js);
        }

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
                var self = this;
                var ps = HasPositions ? ia.Map(i => self.Positions[i]) : null;
                var cs = HasColors ? ia.Map(i => self.Colors[i]) : null;
                var ns = HasNormals ? ia.Map(i => self.Normals[i]) : null;
                var js = HasIntensities ? ia.Map(i => self.Intensities[i]) : null;
                var ks = HasClassifications ? ia.Map(i => self.Classifications[i]) : null;

                if (verbose)
                {
                    var removedCount = Positions.Count - ps.Length;
                    var removedPercent = (removedCount / (double)Positions.Count) * 100.0;
                    Report.Line($"removed {removedCount:N0} duplicate points ({removedPercent:0.00}% of {Positions.Count:N0})");
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

        #region ImmutableFilterBy...

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByPosition(Func<V3d, bool> predicate)
        {
            if (!HasPositions) return this;

            var ps = new List<V3d>();
            var cs = Colors != null ? new List<C4b>() : null;
            var ns = Normals != null ? new List<V3f>() : null;
            var js = Intensities != null ? new List<int>() : null;
            var ks = Classifications != null ? new List<byte>() : null;

            for (var i = 0; i < Positions.Count; i++)
            {
                if (predicate(Positions[i]))
                {
                    ps.Add(Positions[i]);
                    if (cs != null) cs.Add(Colors[i]);
                    if (ns != null) ns.Add(Normals[i]);
                    if (js != null) js.Add(Intensities[i]);
                    if (ks != null) ks.Add(Classifications[i]);
                }
            }
            return new Chunk(ps, cs, ns, js, ks);
        }

        /// <summary>
        /// Returns chunk with points which are inside given box.
        /// </summary>
        public Chunk ImmutableFilterByBox3d(Box3d filter) 
            => ImmutableFilterByPosition(filter.Contains);

        /// <summary>
        /// Returns chunk with points which are inside given cell.
        /// </summary>
        public Chunk ImmutableFilterByCell(Cell filter)
            => ImmutableFilterByPosition(filter.BoundingBox.Contains);

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByColor(Func<C4b, bool> predicate)
        {
            if (!HasColors) return this;

            var ps = Positions != null ? new List<V3d>() : null;
            var cs = new List<C4b>();
            var ns = Normals != null ? new List<V3f>() : null;
            var js = Intensities != null ? new List<int>() : null;
            var ks = Classifications != null ? new List<byte>() : null;

            for (var i = 0; i < Colors.Count; i++)
            {
                if (predicate(Colors[i]))
                {
                    if (ps != null) ps.Add(Positions[i]);
                    cs.Add(Colors[i]);
                    if (ns != null) ns.Add(Normals[i]);
                    if (js != null) js.Add(Intensities[i]);
                    if (ks != null) ks.Add(Classifications[i]);
                }
            }
            return new Chunk(ps, cs, ns, js, ks);
        }

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByNormal(Func<V3f, bool> predicate)
        {
            if (!HasNormals) return this;

            var ps = Positions != null ? new List<V3d>() : null;
            var cs = Colors != null ? new List<C4b>() : null;
            var ns = new List<V3f>();
            var js = Intensities != null ? new List<int>() : null;
            var ks = Classifications != null ? new List<byte>() : null;

            for (var i = 0; i < Normals.Count; i++)
            {
                if (predicate(Normals[i]))
                {
                    if (ps != null) ps.Add(Positions[i]);
                    if (cs != null) cs.Add(Colors[i]);
                    ns.Add(Normals[i]);
                    if (js != null) js.Add(Intensities[i]);
                    if (ks != null) ks.Add(Classifications[i]);
                }
            }
            return new Chunk(ps, cs, ns, js, ks);
        }

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByIntensity(Func<int, bool> predicate)
        {
            if (!HasNormals) return this;

            var ps = Positions != null ? new List<V3d>() : null;
            var cs = Colors != null ? new List<C4b>() : null;
            var ns = Normals != null ? new List<V3f>() : null;
            var js = new List<int>();
            var ks = Classifications != null ? new List<byte>() : null;

            for (var i = 0; i < Intensities.Count; i++)
            {
                if (predicate(Intensities[i]))
                {
                    if (ps != null) ps.Add(Positions[i]);
                    if (cs != null) cs.Add(Colors[i]);
                    if (ns != null) ns.Add(Normals[i]);
                    js.Add(Intensities[i]);
                    if (ks != null) ks.Add(Classifications[i]);
                }
            }
            return new Chunk(ps, cs, ns, js);
        }

        /// <summary>
        /// Returns chunk with points for which given predicate is true.
        /// </summary>
        public Chunk ImmutableFilterByClassification(Func<byte, bool> predicate)
        {
            if (!HasClassifications) return this;

            var ps = Positions != null ? new List<V3d>() : null;
            var cs = Colors != null ? new List<C4b>() : null;
            var ns = Normals != null ? new List<V3f>() : null;
            var js = Intensities != null ? new List<int>() : null;
            var ks = new List<byte>();

            for (var i = 0; i < Intensities.Count; i++)
            {
                if (predicate(Classifications[i]))
                {
                    if (ps != null) ps.Add(Positions[i]);
                    if (cs != null) cs.Add(Colors[i]);
                    if (ns != null) ns.Add(Normals[i]);
                    if (js != null) js.Add(Intensities[i]);
                    ks.Add(Classifications[i]);
                }
            }
            return new Chunk(ps, cs, ns, js, ks);
        }

        #endregion
    }
}
