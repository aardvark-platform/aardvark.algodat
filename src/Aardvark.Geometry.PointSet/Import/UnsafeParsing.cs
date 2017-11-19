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
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    internal static unsafe class HighPerformanceParsing
    {
        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [byte R] [byte G] [byte B] \n
        /// </summary>
        internal static Chunk? ParseLinesXYZRGB(
            byte[] buffer, int count, double filterDist
            )
        {
            var ps = new List<V3d>();
            var cs = new List<C4b>();
            var prev = V3d.PositiveInfinity;

            unsafe
            {
                fixed (byte* begin = buffer)
                {
                    var p = begin;
                    var end = p + count;
                    while (p < end)
                    {
                        var x = ParseDouble(ref p, end);
                        var y = ParseDouble(ref p, end);
                        var z = ParseDouble(ref p, end);

                        var r = ParseInt(ref p, end);
                        var g = ParseInt(ref p, end);
                        var b = ParseInt(ref p, end);

                        SkipToNextLine(ref p, end);

                        if (double.IsNaN(z)) continue;

                        var dx = x - prev.X; if (dx < 0) dx = -dx;
                        var dy = y - prev.Y; if (dy < 0) dy = -dy;
                        var dz = z - prev.Z; if (dz < 0) dz = -dz;
                        if (dx > filterDist || dy > filterDist || dz > filterDist)
                        {
                            ps.Add(new V3d(x, y, z));
                            cs.Add(new C4b(r.Value, g.Value, b.Value));
                            prev.X = x; prev.Y = y; prev.Z = z;
                        }
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, new Box3d(ps));
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [int INTENSITY] [byte R] [byte G] [byte B] \n
        /// </summary>
        internal static Chunk? ParseLinesXYZIRGB(
            byte[] buffer, int count, double filterDist
            )
        {
            var ps = new List<V3d>();
            var cs = new List<C4b>();
            var prev = V3d.PositiveInfinity;

            unsafe
            {
                fixed (byte* begin = buffer)
                {
                    var p = begin;
                    var end = p + count;
                    while (p < end)
                    {
                        var x = ParseDouble(ref p, end);
                        var y = ParseDouble(ref p, end);
                        var z = ParseDouble(ref p, end);

                        ParseInt(ref p, end); // ignore intensity

                        var r = ParseInt(ref p, end);
                        var g = ParseInt(ref p, end);
                        var b = ParseInt(ref p, end);

                        SkipToNextLine(ref p, end);

                        if (double.IsNaN(z) || !b.HasValue) continue;

                        var dx = x - prev.X; if (dx < 0) dx = -dx;
                        var dy = y - prev.Y; if (dy < 0) dy = -dy;
                        var dz = z - prev.Z; if (dz < 0) dz = -dz;
                        if (dx > filterDist || dy > filterDist || dz > filterDist)
                        {
                            ps.Add(new V3d(x, y, z));
                            cs.Add(new C4b(r.Value, g.Value, b.Value));
                            prev.X = x; prev.Y = y; prev.Z = z;
                        }
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, new Box3d(ps));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SkipToNextLine(ref byte* p, byte* end)
        {
            while (p < end && *p != '\n') p++;
            p++;
            return p < end;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double ParseDouble(ref byte* p, byte* end)
        {
            if (p >= end) return double.NaN;

            while (*p == ' ' || p >= end) p++;
            if (p >= end) return double.NaN;

            var minus = *p == ((byte)'-');
            if (minus) p++;

            var x = 0.0;
            var parse = true;
            while (parse && p < end)
            {
                switch ((char)*p)
                {
                    case '0': x = x * 10.0; break;
                    case '1': x = x * 10.0 + 1.0; break;
                    case '2': x = x * 10.0 + 2.0; break;
                    case '3': x = x * 10.0 + 3.0; break;
                    case '4': x = x * 10.0 + 4.0; break;
                    case '5': x = x * 10.0 + 5.0; break;
                    case '6': x = x * 10.0 + 6.0; break;
                    case '7': x = x * 10.0 + 7.0; break;
                    case '8': x = x * 10.0 + 8.0; break;
                    case '9': x = x * 10.0 + 9.0; break;
                    case '.': parse = false; break;
                    case ' ': return minus ? -x : x;
                    default: return double.NaN;
                }
                p++;
            }
            if (p >= end) return minus ? -x : x;

            var y = 0.0;
            var r = 0.1;
            while (p < end)
            {
                switch ((char)*p)
                {
                    case '0': break;
                    case '1': y = y + r; break;
                    case '2': y = y + r * 2; break;
                    case '3': y = y + r * 3; break;
                    case '4': y = y + r * 4; break;
                    case '5': y = y + r * 5; break;
                    case '6': y = y + r * 6; break;
                    case '7': y = y + r * 7; break;
                    case '8': y = y + r * 8; break;
                    case '9': y = y + r * 9; break;
                    case ' ': return minus ? -x - y : x + y;
                    default: return double.NaN;
                }
                r *= 0.1;
                p++;
            }
            return minus ? -x - y : x + y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int? ParseInt(ref byte* p, byte* end)
        {
            if (p >= end) return null;

            while (*p == ' ' || p >= end) p++;
            if (p >= end) return null;

            var minus = *p == ((byte)'-');
            if (minus) p++;

            var x = 0;
            while (p < end)
            {
                switch ((char)*p)
                {
                    case '0': x = x * 10; break;
                    case '1': x = x * 10 + 1; break;
                    case '2': x = x * 10 + 2; break;
                    case '3': x = x * 10 + 3; break;
                    case '4': x = x * 10 + 4; break;
                    case '5': x = x * 10 + 5; break;
                    case '6': x = x * 10 + 6; break;
                    case '7': x = x * 10 + 7; break;
                    case '8': x = x * 10 + 8; break;
                    case '9': x = x * 10 + 9; break;
                    case '\r':
                    case ' ': return minus ? -x : x;
                    default: return null;
                }
                p++;
            }
            return minus ? -x : x;
        }
    }
}
