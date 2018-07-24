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
using System;
using System.Collections.Generic;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Parsers emit a sequence of chunks of points with optional colors, normals, and intensities.
    /// </summary>
    public struct Chunk
    {
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

        /// <summary>
        /// </summary>
        /// <param name="positions">Optional.</param>
        /// <param name="colors">Optional. Either null or same number of elements as positions.</param>
        /// <param name="normals">Optional. Either null or same number of elements as positions.</param>
        /// <param name="intensities">Optional. Either null or same number of elements as positions.</param>
        /// <param name="bbox">Optional. If null, then bbox will be constructed from positions.</param>
        public Chunk(IList<V3d> positions, IList<C4b> colors = null, IList<V3f> normals = null, IList<int> intensities = null, Box3d? bbox = null)
        {
            if (colors != null && colors.Count != positions?.Count) throw new ArgumentException(nameof(colors));
            if (normals != null && normals.Count != positions?.Count) throw new ArgumentException(nameof(colors));
            if (intensities != null && intensities.Count != positions?.Count) throw new ArgumentException(nameof(colors));

            Positions = positions;
            Colors = colors;
            Normals = normals;
            Intensities = intensities;
            BoundingBox = bbox ?? new Box3d(positions);
        }

        /// <summary>
        /// Immutable update of positions.
        /// </summary>
        public Chunk WithPositions(IList<V3d> newPositions) => new Chunk(newPositions, Colors, Normals, Intensities);
        
        /// <summary>
        /// Immutable update of colors.
        /// </summary>
        public Chunk WithColors(IList<C4b> newColors) => new Chunk(Positions, newColors, Normals, Intensities, BoundingBox);

        /// <summary>
        /// Immutable update of normals.
        /// </summary>
        public Chunk WithNormals(IList<V3f> newNormals) => new Chunk(Positions, Colors, newNormals, Intensities, BoundingBox);
        
        /// <summary>
        /// Immutable update of normals.
        /// </summary>
        public Chunk WithIntensities(IList<int> newIntensities) => new Chunk(Positions, Colors, Normals, newIntensities, BoundingBox);

        /// <summary>
        /// Removes points which are less than minDist from previous point.
        /// </summary>
        public Chunk ImmutableFilterSequentialMinDist(double minDist)
        {
            if (minDist <= 0.0) return this;
            var minDistSquared = minDist * minDist;

            var ps = new List<V3d>();
            var cs = Colors != null ? new List<C4b>() : null;
            var ns = Normals != null ? new List<V3f>() : null;
            var js = Intensities != null ? new List<int>() : null;

            var last = V3d.MinValue;
            for (var i = 0; i < Positions.Count; i++)
            {
                var p = Positions[i];
                if (V3d.DistanceSquared(p, last) >= minDistSquared)
                {
                    last = p;
                    ps.Add(p);
                    if (cs != null) cs.Add(Colors[i]);
                    if (ns != null) ns.Add(Normals[i]);
                    if (js != null) js.Add(Intensities[i]);
                }
            }
            return new Chunk(ps, cs, ns, js);
        }

        /// <summary>
        /// Returns chunk with duplicate point positions removed.
        /// </summary>
        public Chunk ImmutableDeduplicate()
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
                return new Chunk(ps, cs, ns, js);
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
            => new Chunk(Positions.Map(mapping), Colors, Normals, Intensities);
    }
}
