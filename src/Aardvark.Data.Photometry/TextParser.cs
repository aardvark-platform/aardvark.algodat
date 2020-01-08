using System;
using System.Globalization;
using System.IO;

namespace Aardvark.Data.Photometry
{
    class TextParser
    {
        protected double ReadDoubleLine(StreamReader s)
        {
            var temp = s.ReadLine().Trim();

            if (String.IsNullOrEmpty(temp)) return 0.0f;
            return ParseDouble(temp);
        }

        protected int ReadIntLine(StreamReader s)
        {
            var temp = s.ReadLine().Trim();

            if (String.IsNullOrEmpty(temp)) return 0;
            return ParseInt(temp);
        }

        protected static double ParseDouble(String s)
        {
            return Double.Parse(s, CultureInfo.InvariantCulture);
        }

        protected static int ParseInt(String s)
        {
            return (int)Decimal.Parse(s); // NOTE use decimal parsing to not crash with floating point values (could report warning)
        }

        protected String[] StringSplit(String s)
        {
            char[] splitChar = new char[] { ' ' };
            return s.Split(splitChar, StringSplitOptions.RemoveEmptyEntries);
        }

        protected String[] LabelSplitForDict(String s)
        {
            String[] temp = s.Split(']');
            temp[0] = temp[0].Remove(0, 1).Trim();
            temp[1] = temp[1].Trim();

            return temp;
        }
    }
}
