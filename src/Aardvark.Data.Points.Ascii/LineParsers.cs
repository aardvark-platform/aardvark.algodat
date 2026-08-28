/*
    Copyright (C) 2006-2025. Aardvark Platform Team. http://github.com/aardvark-platform.
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
    /// Single-pass numeric line parsers for LF- or CRLF-delimited ASCII point records.
    /// Floating-point fields support invariant decimal and scientific notation.
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
            => Custom(buffer, count, filterDist,
            [
                Token.PositionX, Token.PositionY, Token.PositionZ,
                Token.Intensity,
                Token.ColorR, Token.ColorG, Token.ColorB
            ], partIndices);

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [SKIP] [byte R] [byte G] [byte B] \n
        /// </summary>
        public static Chunk XYZSRGB(byte[] buffer, int count, double filterDist, int? partIndices)
            => Custom(buffer, count, filterDist,
            [
                Token.PositionX, Token.PositionY, Token.PositionZ,
                Token.Skip,
                Token.ColorR, Token.ColorG, Token.ColorB
            ], partIndices);

        /// <summary>
        /// Buffer is expected to contain ASCII. Lines separated by '\n'.
        /// Expected line format: [double X] [double Y] [double Z] [byte R] [byte G] [byte B] \n
        /// </summary>
        public static Chunk XYZRGB(byte[] buffer, int count, double filterDist, int? partIndices)
            => Custom(buffer, count, filterDist,
            [
                Token.PositionX, Token.PositionY, Token.PositionZ,
                Token.ColorR, Token.ColorG, Token.ColorB
            ], partIndices);

        #region Private
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDelimiter(byte c)
            => c == ' ' || c == '\t' || c == '\r' || c == '\n';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe bool SkipToNextLine(LineParserState state)
        {
            var p = state.p;
            while (p < state.end && *p != '\n') p++;
            if (p < state.end) p++;
            state.p = p;
            return p < state.end;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ParseFloat64(LineParserState state, Action<double> setResult)
        {
            var p = state.p;
            var end = state.end;
            while (p < end && (*p == ' ' || *p == '\t')) p++;
            if (p >= end || *p == '\n' || *p == '\r')
            {
                state.p = p;
                state.IsInvalid = true;
                return;
            }

            var minus = *p == '-';
            if (minus || *p == '+') p++;

            var value = 0.0;
            var hasDigits = false;
            while (p < end)
            {
                var digit = (uint)(*p - '0');
                if (digit > 9) break;
                value = value * 10.0 + digit;
                hasDigits = true;
                p++;
            }

            var fraction = 0.0;
            if (p < end && *p == '.')
            {
                p++;
                var scale = 0.1;
                while (p < end)
                {
                    var digit = (uint)(*p - '0');
                    if (digit > 9) break;
                    fraction += digit * scale;
                    scale *= 0.1;
                    hasDigits = true;
                    p++;
                }
            }

            if (!hasDigits)
            {
                state.p = p;
                state.IsInvalid = true;
                return;
            }

            var exponent = 0;
            var minusExponent = false;
            if (p < end && (*p == 'e' || *p == 'E'))
            {
                p++;
                if (p < end && (*p == '-' || *p == '+'))
                {
                    minusExponent = *p == '-';
                    p++;
                }

                var hasExponentDigits = false;
                while (p < end)
                {
                    var digit = (uint)(*p - '0');
                    if (digit > 9) break;
                    exponent = exponent < 1000 ? exponent * 10 + (int)digit : 10000;
                    hasExponentDigits = true;
                    p++;
                }

                if (!hasExponentDigits)
                {
                    state.p = p;
                    state.IsInvalid = true;
                    return;
                }
            }

            if (p < end && !IsDelimiter(*p))
            {
                state.p = p;
                state.IsInvalid = true;
                return;
            }

            value += fraction;
            if (exponent != 0 && value != 0.0)
                value *= Math.Pow(10.0, minusExponent ? -exponent : exponent);

            state.p = p;
            setResult(minus ? -value : value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ParseFloat32(LineParserState state, Action<float> setResult)
        {
            var p = state.p;
            var end = state.end;
            while (p < end && (*p == ' ' || *p == '\t')) p++;
            if (p >= end || *p == '\n' || *p == '\r')
            {
                state.p = p;
                state.IsInvalid = true;
                return;
            }

            var minus = *p == '-';
            if (minus || *p == '+') p++;

            var value = 0.0f;
            var hasDigits = false;
            while (p < end)
            {
                var digit = (uint)(*p - '0');
                if (digit > 9) break;
                value = value * 10.0f + digit;
                hasDigits = true;
                p++;
            }

            var fraction = 0.0f;
            if (p < end && *p == '.')
            {
                p++;
                var scale = 0.1f;
                while (p < end)
                {
                    var digit = (uint)(*p - '0');
                    if (digit > 9) break;
                    fraction += digit * scale;
                    scale *= 0.1f;
                    hasDigits = true;
                    p++;
                }
            }

            if (!hasDigits)
            {
                state.p = p;
                state.IsInvalid = true;
                return;
            }

            var exponent = 0;
            var minusExponent = false;
            if (p < end && (*p == 'e' || *p == 'E'))
            {
                p++;
                if (p < end && (*p == '-' || *p == '+'))
                {
                    minusExponent = *p == '-';
                    p++;
                }

                var hasExponentDigits = false;
                while (p < end)
                {
                    var digit = (uint)(*p - '0');
                    if (digit > 9) break;
                    exponent = exponent < 1000 ? exponent * 10 + (int)digit : 10000;
                    hasExponentDigits = true;
                    p++;
                }

                if (!hasExponentDigits)
                {
                    state.p = p;
                    state.IsInvalid = true;
                    return;
                }
            }

            if (p < end && !IsDelimiter(*p))
            {
                state.p = p;
                state.IsInvalid = true;
                return;
            }

            value += fraction;
            if (exponent != 0 && value != 0.0f)
                value = (float)(value * Math.Pow(10.0, minusExponent ? -exponent : exponent));

            state.p = p;
            setResult(minus ? -value : value);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ParseInt(LineParserState state, Action<int> setResult)
        {
            var p = state.p;
            var end = state.end;
            while (p < end && (*p == ' ' || *p == '\t')) p++;
            if (p >= end || *p == '\n' || *p == '\r')
            {
                state.p = p;
                state.IsInvalid = true;
                return;
            }

            var minus = *p == '-';
            if (minus) p++;

            var value = 0;
            while (p < end)
            {
                var digit = (uint)(*p - '0');
                if (digit <= 9)
                {
                    value = value * 10 + (int)digit;
                    p++;
                    continue;
                }

                if (IsDelimiter(*p))
                {
                    state.p = p;
                    setResult(minus ? -value : value);
                    return;
                }

                state.p = p;
                state.IsInvalid = true;
                return;
            }

            state.p = p;
            setResult(minus ? -value : value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ParseByte(LineParserState state, Action<byte> setResult)
        {
            var p = state.p;
            var end = state.end;
            while (p < end && *p == ' ') p++;
            if (p >= end || *p == '\n' || *p == '\r')
            {
                state.p = p;
                state.IsInvalid = true;
                return;
            }
            
            var value = 0;
            while (p < end)
            {
                var digit = (uint)(*p - '0');
                if (digit <= 9)
                {
                    value = value * 10 + (int)digit;
                    p++;
                    continue;
                }

                if (*p == ' ' || *p == '\r' || *p == '\n')
                {
                    state.p = p;
                    if (value < 256) setResult((byte)value);
                    else state.IsInvalid = true;
                    return;
                }

                state.p = p;
                state.IsInvalid = true;
                return;
            }

            state.p = p;
            if (value < 256) setResult((byte)value);
            else state.IsInvalid = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void ParseSkip(LineParserState state)
        {
            var p = state.p;
            var end = state.end;
            while (p < end && (*p == ' ' || *p == '\t')) p++;
            if (p >= end || *p == '\n' || *p == '\r')
            {
                state.p = p;
                state.IsInvalid = true;
                return;
            }

            while (p < end && !IsDelimiter(*p)) p++;
            state.p = p;
        }

        #endregion
    }
}
