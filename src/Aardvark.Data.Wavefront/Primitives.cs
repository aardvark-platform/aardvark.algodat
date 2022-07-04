using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Aardvark.Data.Wavefront
{
    public static class Primitives
    {
        public static C3f ParseColor(IList<Text> x)
        {
            if (x.Count < 3) throw new InvalidOperationException();
            return new C3f(x[0].ParseFloatValue(),
                           x[1].ParseFloatValue(),
                           x[2].ParseFloatValue());
        }

        public static V3f ParseTexCoord(IList<Text> x)
        {
            if (x.Count < 1) throw new InvalidOperationException();
            return new V3f(x[0].ParseFloatValue(),
                           x.Count > 1 ? 1 - x[1].ParseFloatValue() : 0, // temporary hack for sponza.obj
                           x.Count > 2 ? x[2].ParseFloatValue() : 0);
        }

        public static V3f ParseVector(IList<Text> x)
        {
            if (x.Count < 3) throw new InvalidOperationException();
            return new V3f(x[0].ParseFloatValue(),
                           x[1].ParseFloatValue(),
                           x[2].ParseFloatValue());
        }

        public static V4f ParseVertex(IList<Text> x)
        {
            if (x.Count < 3) throw new InvalidOperationException();
            return new V4f(x[0].ParseFloatValue(),
                           x[1].ParseFloatValue(),
                           x[2].ParseFloatValue(),
                           x.Count > 3 ? x[3].ParseFloatValue() : 1);
        }

        public static float ParseFloat(IList<Text> x)
        {
            if (x.Count < 1) throw new InvalidOperationException();
            return x[0].ParseFloatValue();
        }

        public static float ParseInt(IList<Text> x)
        {
            if (x.Count < 1) throw new InvalidOperationException();
            return x[0].ParseIntValue();
        }

        public static string ParseMap(IList<Text> x, string baseDir = null)
        {
            if (x.Count == 0)
                return null;
            // if (x.Count < 1) throw new InvalidOperationException();
            // Options:

            //-bm mult
            //-blendu on | off
            //-blendv on | off
            //-cc on | off
            //-clamp on | off
            //-imfchan r | g | b | m | l | z
            //-mm base gain
            //-o u v w
            //-s u v w
            //-t u v w
            //-texres value

            var fileName = x.Last().ToString();

            if (!string.IsNullOrEmpty(baseDir))
                fileName = Path.Combine(baseDir, fileName);

            return fileName; // skip options and just take texture filename
        }

        /// <summary>
        /// Parses an integer from a Text that is already trimed of whitespace
        /// NOTE: Use Text.ParsedValueOfIntAt(0) that contains an own parsing implementation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ParseIntValue(this Text t)
        {
            return t.ParsedValueOfIntAt(0).Value;
        }

        /// <summary>
        /// Parses a floating point value from a Text that is already trimed of whitespace
        /// NOTE: Text.ParseFloat would also use float.Parse() but has additional validation overhead that is skipped here
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ParseFloatValue(this Text input)
        {
            return float.Parse(input.ToString(), CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses a double point value from a Text that is already trimed of whitespace
        /// NOTE: Text.ParseDouble would also use double.Parse() but has additional validation overhead that is skipped here
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ParseDoubleValue(this Text input)
        {
            return double.Parse(input.ToString(), CultureInfo.InvariantCulture);
        }
    }
}