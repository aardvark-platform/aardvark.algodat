/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using static Aardvark.Data.Points.Import.Ascii;

namespace Aardvark.Data.Points
{
    /// <summary></summary>
    internal unsafe class LineParserState
    {
        public byte* p;
        public byte* end;

        public bool IsInvalid = false;

        public V3d Position;
        public C4b Color = C4b.Black;
        public V3f Normal;
        public int Intensity;
    }
    
    /// <summary>
    /// Various line parsers.
    /// </summary>
    public static class LineParsers
    {
        internal static Dictionary<Token, Action<LineParserState>> s_parsers = new()
        {
            // Position
            { Token.PositionX, state => ParseFloat64(state, x => state.Position.X = x) },
            { Token.PositionY, state => ParseFloat64(state, y => state.Position.Y = y) },
            { Token.PositionZ, state => ParseFloat64(state, z => state.Position.Z = z) },

            // Normal
            { Token.NormalX, state => ParseFloat32(state, x => state.Normal.X = x) },
            { Token.NormalY, state => ParseFloat32(state, y => state.Normal.Y = y) },
            { Token.NormalZ, state => ParseFloat32(state, z => state.Normal.Z = z) },

            // Color
            { Token.ColorR, state => ParseByte(state, r => state.Color.R = r) },
            { Token.ColorG, state => ParseByte(state, g => state.Color.G = g) },
            { Token.ColorB, state => ParseByte(state, b => state.Color.B = b) },
            { Token.ColorA, state => ParseByte(state, a => state.Color.A = a) },

            { Token.ColorRf, state => ParseFloat32(state, r => { if (r >= 0.0 && r <= 1.0) state.Color.R = (byte)(255 * r); else state.IsInvalid = true; }) },
            { Token.ColorGf, state => ParseFloat32(state, g => { if (g >= 0.0 && g <= 1.0) state.Color.G = (byte)(255 * g); else state.IsInvalid = true; }) },
            { Token.ColorBf, state => ParseFloat32(state, b => { if (b >= 0.0 && b <= 1.0) state.Color.B = (byte)(255 * b); else state.IsInvalid = true; }) },
            { Token.ColorAf, state => ParseFloat32(state, a => { if (a >= 0.0 && a <= 1.0) state.Color.A = (byte)(255 * a); else state.IsInvalid = true; }) },

            // Intensity
            { Token.Intensity, state => ParseFloat64(state, i => state.Intensity = (int)i) },

            // Skip
            { Token.Skip, state => ParseSkip(state) },
        };

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// </summary>
        public static Chunk CustomDurable(byte[] buffer, int count, double filterDist, Token[] layout, uint? partIndices)
        {
            var hasColor = layout.HasColorTokens();
            var hasNormal = layout.HasNormalTokens();
            var hasIntensity = layout.HasIntensityTokens();

            var ps = new List<V3d>();
            var cs = hasColor ? new List<C4b>() : null;
            var ns = hasNormal ? new List<V3f>() : null;
            var js = hasIntensity ? new List<int>() : null;

            var prev = V3d.PositiveInfinity;
            var filterDistM = -filterDist;
            var doFilterDist = filterDist > 0.0;

            var tokenParsers = layout.Map(x => s_parsers[x]);

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
                        state.IsInvalid = false;

                        for (var i = 0; i < tokenParsers.Length; i++)
                        {
                            tokenParsers[i](state);
                            if (state.IsInvalid) break;
                        }

                        SkipToNextLine(state);
                        if (state.IsInvalid) continue;

                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (Utils.DistLessThanL1(ref state.Position, ref prev, filterDist)) continue;
                            prev = state.Position;
                        }

                        // add point to chunk
                        ps.Add(state.Position);
                        cs?.Add(state.Color);
                        ns?.Add(state.Normal);
                        js?.Add(state.Intensity);
                    }
                }
            }

            if (ps.Count == 0) return Chunk.Empty;
            return new Chunk(ps, cs, ns, js, classifications: null, partIndices: partIndices, partIndexRange: null, bbox: null);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// </summary>
        public static Chunk Custom(byte[] buffer, int count, double filterDist, Token[] layout, int? partIndices)
        {
            var hasColor = layout.HasColorTokens();
            var hasNormal = layout.HasNormalTokens();
            var hasIntensity = layout.HasIntensityTokens();

            var ps = new List<V3d>();
            var cs = hasColor ? new List<C4b>() : null;
            var ns = hasNormal ? new List<V3f>() : null;
            var js = hasIntensity ? new List<int>() : null;

            var prev = V3d.PositiveInfinity;
            var filterDistM = -filterDist;
            var doFilterDist = filterDist > 0.0;

            var tokenParsers = layout.Map(x => s_parsers[x]);

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
                        state.IsInvalid = false;

                        for (var i = 0; i < tokenParsers.Length; i++)
                        {
                            tokenParsers[i](state);
                            if (state.IsInvalid) break;
                        }

                        SkipToNextLine(state);
                        if (state.IsInvalid) continue;
                        
                        // min dist filtering
                        if (doFilterDist)
                        {
                            if (Utils.DistLessThanL1(ref state.Position, ref prev, filterDist)) continue;
                            prev = state.Position;
                        }

                        // add point to chunk
                        ps.Add(state.Position);
                        cs?.Add(state.Color);
                        ns?.Add(state.Normal);
                        js?.Add(state.Intensity);
                    }
                }
            }

            if (ps.Count == 0) return Chunk.Empty;
            return new Chunk(ps, cs, ns, js, classifications: null, partIndices, partIndexRange: null, bbox: null);
        }

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [int I] [byte R] [byte G] [byte B] \n
        /// </summary>
        public static Chunk XYZIRGB(byte[] buffer, int count, double filterDist, int? partIndices)
            => Custom(buffer, count, filterDist, new[]
            {
                Token.PositionX, Token.PositionY, Token.PositionZ,
                Token.Intensity,
                Token.ColorR, Token.ColorG, Token.ColorB
            }, partIndices);

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [SKIP] [byte R] [byte G] [byte B] \n
        /// </summary>
        public static Chunk XYZSRGB(byte[] buffer, int count, double filterDist, int? partIndices)
            => Custom(buffer, count, filterDist, new[]
            {
                Token.PositionX, Token.PositionY, Token.PositionZ,
                Token.Skip,
                Token.ColorR, Token.ColorG, Token.ColorB
            }, partIndices);

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [byte R] [byte G] [byte B] \n
        /// </summary>
        public static Chunk XYZRGB(byte[] buffer, int count, double filterDist, int? partIndices)
            => Custom(buffer, count, filterDist, new[]
            {
                Token.PositionX, Token.PositionY, Token.PositionZ,
                Token.ColorR, Token.ColorG, Token.ColorB
            }, partIndices);

        #region Private
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe bool SkipToNextLine(LineParserState state)
        {
            while (state.p < state.end && *state.p != '\n') state.p++;
            state.p++;
            return state.p < state.end;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ParseFloat64(LineParserState state, Action<double> setResult)
        {
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            while ((*state.p == ' ' || *state.p == '\t') && state.p < state.end) state.p++;
            if (state.p >= state.end || *state.p == '\n' || *state.p == '\r') { state.IsInvalid = true; return; }

            var minus = *state.p == ((byte)'-');
            if (minus) state.p++;
            else if (*state.p == ((byte)'+')) state.p++;

            var x = 0.0;
            var parse = true;
            while (parse && state.p < state.end)
            {
                switch ((char)*state.p)
                {
                    case '0': x *= 10.0; break;
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
                    case '\n':
                    case '\r':
                    case '\t':
                    case ' ': setResult(minus ? -x : x); return;
                    default: { state.IsInvalid = true; return; }
                }
                state.p++;
            }
            if (state.p >= state.end) { setResult(minus ? -x : x); return; }

            var y = 0.0;
            var r = 0.1;
            var noExponent = true;
            while (noExponent && state.p < state.end)
            {
                switch ((char)*state.p)
                {
                    case '0': break;
                    case '1': y += r; break;
                    case '2': y += r * 2; break;
                    case '3': y += r * 3; break;
                    case '4': y += r * 4; break;
                    case '5': y += r * 5; break;
                    case '6': y += r * 6; break;
                    case '7': y += r * 7; break;
                    case '8': y += r * 8; break;
                    case '9': y += r * 9; break;
                    case 'e':
                    case 'E': noExponent = false; break;
                    case '\n':
                    case '\r':
                    case '\t':
                    case ' ': setResult(minus ? -x - y : x + y); return;
                    default: { state.IsInvalid = true; return; };
                }
                r *= 0.1;
                state.p++;
            }

            if (!noExponent)
            {
                var minusExponent = *state.p == ((byte)'-');
                if (minusExponent) state.p++;
                else if (*state.p == ((byte)'+')) state.p++;

                var e = 0;
                while (state.p < state.end)
                {
                    switch ((char)*state.p)
                    {
                        case '0': e *= 10; break;
                        case '1': e = e * 10 + 1; break;
                        case '2': e = e * 10 + 2; break;
                        case '3': e = e * 10 + 3; break;
                        case '4': e = e * 10 + 4; break;
                        case '5': e = e * 10 + 5; break;
                        case '6': e = e * 10 + 6; break;
                        case '7': e = e * 10 + 7; break;
                        case '8': e = e * 10 + 8; break;
                        case '9': e = e * 10 + 9; break;
                        case '\n':
                        case '\r':
                        case '\t':
                        case ' ': setResult((minus ? -x - y : x + y) * Math.Pow(10, minusExponent ? -e : e)); return;
                        default: { state.IsInvalid = true; return; }
                    }
                    state.p++;
                }
            }

            setResult(minus ? -x - y : x + y);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ParseFloat32(LineParserState state, Action<float> setResult)
        {
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            while ((*state.p == ' ' || *state.p == '\t') && state.p < state.end) state.p++;
            if (state.p >= state.end || *state.p == '\n' || *state.p == '\r') { state.IsInvalid = true; return; }

            var minus = *state.p == ((byte)'-');
            if (minus) state.p++;
            else if (*state.p == ((byte)'+')) state.p++;

            var x = 0.0f;
            var parse = true;
            while (parse && state.p < state.end)
            {
                switch ((char)*state.p)
                {
                    case '0': x *= 10.0f; break;
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
                    case '\t':
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
                    case '1': y += r; break;
                    case '2': y += r * 2; break;
                    case '3': y += r * 3; break;
                    case '4': y += r * 4; break;
                    case '5': y += r * 5; break;
                    case '6': y += r * 6; break;
                    case '7': y += r * 7; break;
                    case '8': y += r * 8; break;
                    case '9': y += r * 9; break;
                    case '\t':
                    case ' ': setResult(minus ? -x - y : x + y); return;
                    default: { state.IsInvalid = true; return; }
                }
                r *= 0.1f;
                state.p++;
            }
            setResult(minus ? -x - y : x + y);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ParseInt(LineParserState state, Action<int> setResult)
        {
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            while ((*state.p == ' ' || *state.p == '\t') && state.p < state.end) state.p++;
            if (state.p >= state.end || *state.p == '\n' || *state.p == '\r') { state.IsInvalid = true; return; }

            var minus = *state.p == ((byte)'-');
            if (minus) state.p++;

            var x = 0;
            while (state.p < state.end)
            {
                switch ((char)*state.p)
                {
                    case '0': x *= 10; break;
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
                    case '\t':
                    case ' ': setResult(minus ? -x : x); return;
                    default: { state.IsInvalid = true; return; }
                }
                state.p++;
            }
            setResult(minus ? -x : x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ParseByte(LineParserState state, Action<byte> setResult)
        {
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            while (*state.p == ' ' && state.p < state.end) state.p++;
            if (state.p >= state.end || *state.p == '\n' || *state.p == '\r') { state.IsInvalid = true; return; }
            
            var x = 0;
            while (state.p < state.end)
            {
                switch ((char)*state.p)
                {
                    case '0': x *= 10; break;
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
                    case ' ': if (x < 256) setResult((byte)x); else state.IsInvalid = true; return;
                    default: { state.IsInvalid = true; return; }
                }
                state.p++;
            }
            if (x < 256) setResult((byte)x); else state.IsInvalid = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ParseSkip(LineParserState state)
        {
            if (state.p >= state.end) { state.IsInvalid = true; return; }

            while ((*state.p == ' ' || *state.p == '\t') && state.p < state.end) state.p++;
            if (state.p >= state.end || *state.p == '\n' || *state.p == '\r') { state.IsInvalid = true; return; }
            
            while (state.p < state.end)
            {
                switch ((char)*state.p)
                {
                    case '\r':
                    case '\n':
                    case '\t':
                    case ' ': return;
                }
                state.p++;
            }
        }

        #endregion
    }
}
