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
        /// <summary></summary>
        private unsafe class LineParserState
        {
            public byte* p;
            public byte* end;

            public bool IsInvalid = false;

            public V3d Position = V3d.NaN;
            public C4b Color = C4b.Black;
            public V3f Normal = V3f.NaN;
            public int Intensity = 0;

            public void Reset()
            {
                IsInvalid = false;
                Position = V3d.NaN;
                Color = C4b.Black;
                Normal = V3f.NaN;
                Intensity = 0;
            }
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [int I] [byte R] [byte G] [byte B] \n
        /// </summary>
        public static Chunk? XYZIRGB(byte[] buffer, int count, double filterDist)
        {
            var ps = new List<V3d>();
            var cs = new List<C4b>(); 
            var js = new List<int>();

            var prev = V3d.PositiveInfinity;
            var filterDistM = -filterDist;
            var doFilterDist = filterDist > 0.0;
            
            unsafe
            {
                fixed (byte* begin = buffer)
                {
                    var state = new LineParserState
                    {
                        p = begin,
                        end = begin + count
                    };
                    while (state.p < state.end)
                    {
                        // parse single line
                        state.Reset();
                        if (!ParsePositionV3d(state, ref state.Position)) { SkipToNextLine(state); continue; }
                        ParseInt(state, x => state.Intensity = x); if (state.IsInvalid) { SkipToNextLine(state); continue; }
                        if (!ParseC4bFromByteRGB(state, ref state.Color)) { SkipToNextLine(state); continue; }
                        SkipToNextLine(state);

                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (SkipBecauseOfMinDist(ref state.Position, ref prev, filterDist)) continue;
                            prev = state.Position;
                        }

                        // add point to chunk
                        ps.Add(state.Position);
                        cs.Add(state.Color);
                        js.Add(state.Intensity);
                    }
                }
            }

            if (ps.Count == 0) return null;
            return new Chunk(ps, cs, null, js);
        }
        
        #region Private

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SkipBecauseOfMinDist(ref V3d position, ref V3d prev, double filterDist)
        {
            var
            d = position.X - prev.X; if (d < 0) d = -d; if (d < filterDist) return true;
            d = position.Y - prev.Y; if (d < 0) d = -d; if (d < filterDist) return true;
            d = position.Z - prev.Z; if (d < 0) d = -d; if (d < filterDist) return true;
            return false;
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool SkipToNextLine(LineParserState state)
        {
            while (state.p < state.end && *state.p != '\n') state.p++;
            state.p++;
            return state.p < state.end;
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void ParseFloat64(LineParserState state, Action<double> setResult)
        {
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            while (*state.p == ' ' || state.p >= state.end) state.p++;
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            var minus = *state.p == ((byte)'-');
            if (minus) state.p++;

            var x = 0.0;
            var parse = true;
            while (parse && state.p < state.end)
            {
                switch ((char)*state.p)
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
                    case ' ': setResult(minus ? -x : x); return;
                    default: { state.IsInvalid = true; return; }
                }
                state.p++;
            }
            if (state.p >= state.end) { setResult(minus ? -x : x); return; }

            var y = 0.0;
            var r = 0.1;
            while (state.p < state.end)
            {
                switch ((char)*state.p)
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
                    case ' ': setResult(minus ? -x - y : x + y); return;
                    default: { state.IsInvalid = true; return; };
                }
                r *= 0.1;
                state.p++;
            }
            setResult(minus ? -x - y : x + y);
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void ParseFloat32(LineParserState state, Action<float> setResult)
        {
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            while (*state.p == ' ' || state.p >= state.end) state.p++;
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            var minus = *state.p == ((byte)'-');
            if (minus) state.p++;

            var x = 0.0f;
            var parse = true;
            while (parse && state.p < state.end)
            {
                switch ((char)*state.p)
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
                    case ' ': setResult(minus ? -x : x); return;
                    default: { state.IsInvalid = true; return; }
                }
                state.p++;
            }
            if (state.p >= state.end) { setResult(minus ? -x : x); return; }

            var y = 0.0f;
            var r = 0.1f;
            while (state.p < state.end)
            {
                switch ((char)*state.p)
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
                    case ' ': setResult(minus ? -x - y : x + y); return;
                    default: { state.IsInvalid = true; return; }
                }
                r *= 0.1f;
                state.p++;
            }
            setResult(minus ? -x - y : x + y);
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void ParseInt(LineParserState state, Action<int> setResult)
        {
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            while (*state.p == ' ' || state.p >= state.end) state.p++;
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            var minus = *state.p == ((byte)'-');
            if (minus) state.p++;

            var x = 0;
            while (state.p < state.end)
            {
                switch ((char)*state.p)
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
                    case ' ': setResult(minus ? -x : x); return;
                    default: { state.IsInvalid = true; return; }
                }
                state.p++;
            }
            setResult(minus ? -x : x);
        }



        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParsePositionV3d(LineParserState state, ref V3d result)
        {
            ParseFloat64(state, x => state.Position.X = x); if (state.IsInvalid) return false;
            ParseFloat64(state, x => state.Position.Y = x); if (state.IsInvalid) return false;
            ParseFloat64(state, x => state.Position.Z = x); if (state.IsInvalid) return false;
            return true;
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseNormalV3f(LineParserState state, ref V3f result)
        {
            ParseFloat32(state, x => state.Normal.X = x); if (state.IsInvalid) return false;
            ParseFloat32(state, x => state.Normal.Y = x); if (state.IsInvalid) return false;
            ParseFloat32(state, x => state.Normal.Z = x); if (state.IsInvalid) return false;
            return true;
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseC4bFromByteRGB(LineParserState state, ref C4b result)
        {
            var r = 0; var g = 0; var b = 0;
            ParseInt(state, x => r = x); if (state.IsInvalid) return false;
            ParseInt(state, x => g = x); if (state.IsInvalid) return false;
            ParseInt(state, x => b = x); if (state.IsInvalid) return false;
            result.R = (byte)r;
            result.G = (byte)g;
            result.B = (byte)b;
            return true;
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseC4bFromByteRGBA(LineParserState state, ref C4b result)
        {
            var r = 0; var g = 0; var b = 0; var a = 0;
            ParseInt(state, x => r = x); if (state.IsInvalid) return false;
            ParseInt(state, x => g = x); if (state.IsInvalid) return false;
            ParseInt(state, x => b = x); if (state.IsInvalid) return false;
            ParseInt(state, x => a = x); if (state.IsInvalid) return false;
            result.R = (byte)r;
            result.G = (byte)g;
            result.B = (byte)b;
            result.A = (byte)a;
            return true;
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseC4bFromFloatRGB(LineParserState state, ref C4b result)
        {
            var r = 0; var g = 0; var b = 0;
            ParseInt(state, x => r = x); if (state.IsInvalid) return false;
            ParseInt(state, x => g = x); if (state.IsInvalid) return false;
            ParseInt(state, x => b = x); if (state.IsInvalid) return false;
            result.R = (byte)(255 * r);
            result.G = (byte)(255 * g);
            result.B = (byte)(255 * b);
            return true;
        }

        /// <summary></summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe bool ParseC4bFromFloatRGBA(LineParserState state, ref C4b result)
        {
            var r = 0; var g = 0; var b = 0; var a = 0;
            ParseInt(state, x => r = x); if (state.IsInvalid) return false;
            ParseInt(state, x => g = x); if (state.IsInvalid) return false;
            ParseInt(state, x => b = x); if (state.IsInvalid) return false;
            ParseInt(state, x => a = x); if (state.IsInvalid) return false;
            result.R = (byte)(255 * r);
            result.G = (byte)(255 * g);
            result.B = (byte)(255 * b);
            result.A = (byte)(255 * a);
            return true;
        }

        #endregion
    }
}
