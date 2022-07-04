using System;
using System.Collections.Generic;
using Aardvark.Base;

namespace Aardvark.Data.Wavefront
{
    internal static class WavefrontParser
    {
        public static void Parse<T>(T parseState, Dictionary<Text, Action<T, IList<Text>>> elementProcessors) where T: StreamParser<T>
        {
            var elementValues = new List<Text>();

            for( ; parseState.NextLine(); )
            {
                var line = parseState.Line;
                line = line.WhiteSpaceTrimmed;
                if (line.IsEmpty) continue;
                
                if (line[0] == '#')
                    continue;

                // read element type
                var sep = SkipToWhiteSpace(line);
                var elementType = line.SubText(0, sep);
                if (elementType.IsEmpty) throw new InvalidOperationException();

                // go to next element
                line = line.SubText(sep).WhiteSpaceAtStartTrimmed;

                var ep = elementProcessors.Get(elementType);
                if (ep != null)
                {
                    while (!line.IsEmpty)
                    {
                        // read argument
                        sep = SkipToWhiteSpace(line);
                        var arg = line.SubText(0, sep);

                        // go to next element
                        line = line.SubText(sep).WhiteSpaceAtStartTrimmed;

                        elementValues.Add(arg);
                    }
                    try
                    {
                        ep(parseState, elementValues);
                    }
                    catch (Exception e)
                    {
                        Report.Warn("error parsing object element: {0} {1}\n{2}", elementType, String.Join(" ", elementValues), e.Message);
                    }
                }
                else
                    Report.Warn("object element \"{0}\" not supported", elementType);

                elementValues.Clear();
            }
        }

        static int SkipToWhiteSpace(Text txt)
        {
            int pos = 0;
            int cnt = txt.Count;
            while (pos < cnt)
            {
                var ch = txt[pos];
                if (ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n')
                    break;
                pos++;
            }
            return pos;
        }
    }
}
