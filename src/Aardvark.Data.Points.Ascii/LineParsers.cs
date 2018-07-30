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
using System.Runtime.CompilerServices;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// Various line parsers.
    /// </summary>
    public static class LineParsers
    {
        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [byte R] [byte G] [byte B] \n
        /// </summary>
        public static Chunk? XYZRGB(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>();
            var position = V3d.Zero; var color = C4b.Black;

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

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, null);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [byte R] [byte G] [byte B] [byte A] \n
        /// </summary>
        public static Chunk? XYZRGBA(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>();
            var position = V3d.Zero; var color = C4b.Black;

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
                        if (!ParseC4bFromByteRGBA(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
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

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, null);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [float R] [float G] [float B] \n
        /// </summary>
        public static Chunk? XYZRGBf(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>();
            var position = V3d.Zero; var color = C4b.Black;

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
                        if (!ParseC4bFromFloatRGB(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
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

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, null);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [float R] [float G] [float B] [float A] \n
        /// </summary>
        public static Chunk? XYZRGBAf(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>();
            var position = V3d.Zero; var color = C4b.Black;

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
                        if (!ParseC4bFromFloatRGBA(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
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

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, null);
        }



        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [int INTENSITY] [byte R] [byte G] [byte B] \n
        /// </summary>
        public static Chunk? XYZIRGB(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>(); var js = new List<int>();
            var position = V3d.Zero; var color = C4b.Black; var intensity = 0;

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
                        if (!ParseInt(ref p, end, ref intensity)) { SkipToNextLine(ref p, end); continue; }
                        if (!ParseC4bFromByteRGB(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
                        SkipToNextLine(ref p, end);

                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (SkipBecauseOfMinDist(ref position, ref prev, filterDist)) continue;
                            prev = position;
                        }

                        // add point to chunk
                        ps.Add(position); cs.Add(color); js.Add(intensity);
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, js);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [int INTENSITY] [byte R] [byte G] [byte B] [byte A] \n
        /// </summary>
        public static Chunk? XYZIRGBA(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>(); var js = new List<int>();
            var position = V3d.Zero; var color = C4b.Black; var intensity = 0;

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
                        if (!ParseInt(ref p, end, ref intensity)) { SkipToNextLine(ref p, end); continue; }
                        if (!ParseC4bFromByteRGBA(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
                        SkipToNextLine(ref p, end);

                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (SkipBecauseOfMinDist(ref position, ref prev, filterDist)) continue;
                            prev = position;
                        }

                        // add point to chunk
                        ps.Add(position); cs.Add(color); js.Add(intensity);
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, js);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [int INTENSITY] [float R] [float G] [float B] \n
        /// </summary>
        public static Chunk? XYZIRGBf(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>(); var js = new List<int>();
            var position = V3d.Zero; var color = C4b.Black; var intensity = 0;

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
                        if (!ParseInt(ref p, end, ref intensity)) { SkipToNextLine(ref p, end); continue; }
                        if (!ParseC4bFromFloatRGB(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
                        SkipToNextLine(ref p, end);

                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (SkipBecauseOfMinDist(ref position, ref prev, filterDist)) continue;
                            prev = position;
                        }

                        // add point to chunk
                        ps.Add(position); cs.Add(color); js.Add(intensity);
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, js);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [int INTENSITY] [float R] [float G] [float B] [float A] \n
        /// </summary>
        public static Chunk? XYZIRGBAf(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>(); var js = new List<int>();
            var position = V3d.Zero; var color = C4b.Black; var intensity = 0;

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
                        if (!ParseInt(ref p, end, ref intensity)) { SkipToNextLine(ref p, end); continue; }
                        if (!ParseC4bFromFloatRGBA(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
                        SkipToNextLine(ref p, end);

                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (SkipBecauseOfMinDist(ref position, ref prev, filterDist)) continue;
                            prev = position;
                        }

                        // add point to chunk
                        ps.Add(position); cs.Add(color); js.Add(intensity);
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, js);
        }



        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [byte R] [byte G] [byte B] [int INTENSITY] \n
        /// </summary>
        public static Chunk? XYZRGBI(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>(); var js = new List<int>();
            var position = V3d.Zero; var color = C4b.Black; var intensity = 0;

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
                        if (!ParseInt(ref p, end, ref intensity)) { SkipToNextLine(ref p, end); continue; }
                        SkipToNextLine(ref p, end);

                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (SkipBecauseOfMinDist(ref position, ref prev, filterDist)) continue;
                            prev = position;
                        }

                        // add point to chunk
                        ps.Add(position); cs.Add(color); js.Add(intensity);
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, js);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [byte R] [byte G] [byte B] [byte A] [int INTENSITY] \n
        /// </summary>
        public static Chunk? XYZRGBAI(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>(); var js = new List<int>();
            var position = V3d.Zero; var color = C4b.Black; var intensity = 0;

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
                        if (!ParseC4bFromByteRGBA(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
                        if (!ParseInt(ref p, end, ref intensity)) { SkipToNextLine(ref p, end); continue; }
                        SkipToNextLine(ref p, end);

                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (SkipBecauseOfMinDist(ref position, ref prev, filterDist)) continue;
                            prev = position;
                        }

                        // add point to chunk
                        ps.Add(position); cs.Add(color); js.Add(intensity);
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, js);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [float R] [float G] [float B] [int INTENSITY] \n
        /// </summary>
        public static Chunk? XYZRGBfI(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>(); var js = new List<int>();
            var position = V3d.Zero; var color = C4b.Black; var intensity = 0;

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
                        if (!ParseC4bFromFloatRGB(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
                        if (!ParseInt(ref p, end, ref intensity)) { SkipToNextLine(ref p, end); continue; }
                        SkipToNextLine(ref p, end);

                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (SkipBecauseOfMinDist(ref position, ref prev, filterDist)) continue;
                            prev = position;
                        }

                        // add point to chunk
                        ps.Add(position); cs.Add(color); js.Add(intensity);
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, js);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [float R] [float G] [float B] [float A] [int INTENSITY] \n
        /// </summary>
        public static Chunk? XYZRGBAfI(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>(); var cs = new List<C4b>(); var js = new List<int>();
            var position = V3d.Zero; var color = C4b.Black; var intensity = 0;

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
                        if (!ParseC4bFromFloatRGBA(ref p, end, ref color)) { SkipToNextLine(ref p, end); continue; }
                        if (!ParseInt(ref p, end, ref intensity)) { SkipToNextLine(ref p, end); continue; }
                        SkipToNextLine(ref p, end);

                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (SkipBecauseOfMinDist(ref position, ref prev, filterDist)) continue;
                            prev = position;
                        }

                        // add point to chunk
                        ps.Add(position); cs.Add(color); js.Add(intensity);
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, js);
        }


        #region Private

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool SkipBecauseOfMinDist(ref V3d position, ref V3d prev, double filterDist)
        {
            var
            d = position.X - prev.X; if (d < 0) d = -d; if (d < filterDist) return true;
            d = position.Y - prev.Y; if (d < 0) d = -d; if (d < filterDist) return true;
            d = position.Z - prev.Z; if (d < 0) d = -d; if (d < filterDist) return true;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool SkipToNextLine(ref byte* p, byte* end)
        {
            while (p < end && *p != '\n') p++;
            p++;
            return p < end;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseFloat64(ref byte* p, byte* end, ref double result)
        {
            if (p >= end) return false;

            while (*p == ' ' || p >= end) p++;
            if (p >= end) return false;

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
                    case ' ': result = minus ? -x : x; return true;
                    default: return false;
                }
                p++;
            }
            if (p >= end) { result = minus ? -x : x; return true; }

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
                    case ' ': result = minus ? -x - y : x + y; return true;
                    default: return false;
                }
                r *= 0.1;
                p++;
            }
            result = minus ? -x - y : x + y;
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseFloat32(ref byte* p, byte* end, ref float result)
        {
            if (p >= end) return false;

            while (*p == ' ' || p >= end) p++;
            if (p >= end) return false;

            var minus = *p == ((byte)'-');
            if (minus) p++;

            var x = 0.0f;
            var parse = true;
            while (parse && p < end)
            {
                switch ((char)*p)
                {
                    case '0': x = x * 10.0f; break;
                    case '1': x = x * 10.0f + 1.0f; break;
                    case '2': x = x * 10.0f + 2.0f; break;
                    case '3': x = x * 10.0f + 3.0f; break;
                    case '4': x = x * 10.0f + 4.0f; break;
                    case '5': x = x * 10.0f + 5.0f; break;
                    case '6': x = x * 10.0f + 6.0f; break;
                    case '7': x = x * 10.0f + 7.0f; break;
                    case '8': x = x * 10.0f + 8.0f; break;
                    case '9': x = x * 10.0f + 9.0f; break;
                    case '.': parse = false; break;
                    case ' ': result = minus ? -x : x; return true;
                    default: return false;
                }
                p++;
            }
            if (p >= end) { result = minus ? -x : x; return true; }

            var y = 0.0f;
            var r = 0.1f;
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
                    case ' ': result = minus ? -x - y : x + y; return true;
                    default: return false;
                }
                r *= 0.1f;
                p++;
            }
            result = minus ? -x - y : x + y; return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseInt(ref byte* p, byte* end, ref int result)
        {
            if (p >= end) return false;

            while (*p == ' ' || p >= end) p++;
            if (p >= end) return false;

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
                    case '\n':
                    case ' ': result = minus ? -x : x; return true;
                    default: return false;
                }
                p++;
            }
            result = minus ? -x : x;
            return true;
        }
        
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseV3d(ref byte* p, byte* end, ref V3d result)
        {
            if (!ParseFloat64(ref p, end, ref result.X)) return false;
            if (!ParseFloat64(ref p, end, ref result.Y)) return false;
            if (!ParseFloat64(ref p, end, ref result.Z)) return false;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseV3f(ref byte* p, byte* end, ref V3f result)
        {
            if (!ParseFloat32(ref p, end, ref result.X)) return false;
            if (!ParseFloat32(ref p, end, ref result.Y)) return false;
            if (!ParseFloat32(ref p, end, ref result.Z)) return false;
            return true;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseC4bFromByteRGB(ref byte* p, byte* end, ref C4b result)
        {
            var r = 0; var g = 0; var b = 0;
            if (ParseInt(ref p, end, ref r))
            {
                if (ParseInt(ref p, end, ref g))
                {
                    if (ParseInt(ref p, end, ref b))
                    {
                        result.R = (byte)r;
                        result.G = (byte)g;
                        result.B = (byte)b;
                        return true;
                    }
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseC4bFromByteRGBA(ref byte* p, byte* end, ref C4b result)
        {
            var r = 0; var g = 0; var b = 0; var a = 0;
            if (ParseInt(ref p, end, ref r))
            {
                if (ParseInt(ref p, end, ref g))
                {
                    if (ParseInt(ref p, end, ref b))
                    {
                        if (ParseInt(ref p, end, ref a))
                        {
                            result.R = (byte)r;
                            result.G = (byte)g;
                            result.B = (byte)b;
                            result.A = (byte)a;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseC4bFromFloatRGB(ref byte* p, byte* end, ref C4b result)
        {
            var r = 0.0f; var g = 0.0f; var b = 0.0f;
            if (ParseFloat32(ref p, end, ref r))
            {
                if (ParseFloat32(ref p, end, ref g))
                {
                    if (ParseFloat32(ref p, end, ref b))
                    {
                        result.R = (byte)(255 * r);
                        result.G = (byte)(255 * g);
                        result.B = (byte)(255 * b);
                        return true;
                    }
                }
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseC4bFromFloatRGBA(ref byte* p, byte* end, ref C4b result)
        {
            var r = 0.0f; var g = 0.0f; var b = 0.0f; var a = 0.0f;
            if (ParseFloat32(ref p, end, ref r))
            {
                if (ParseFloat32(ref p, end, ref g))
                {
                    if (ParseFloat32(ref p, end, ref b))
                    {
                        if (ParseFloat32(ref p, end, ref b))
                        {
                            result.R = (byte)(255 * r);
                            result.G = (byte)(255 * g);
                            result.B = (byte)(255 * b);
                            result.A = (byte)(255 * a);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        #endregion
    }
}
