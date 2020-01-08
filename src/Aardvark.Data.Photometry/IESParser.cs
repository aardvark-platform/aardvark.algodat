using Aardvark.Base;
using System;
using System.IO;
using System.Linq;

namespace Aardvark.Data.Photometry
{
    class IESParser : TextParser
    {
        public static IESData Parse(String filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                return new IESParser().Parse(stream);
            }
        }

        public IESData ParseMeta(String filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                using (var sr = new StreamReader(stream))
                {
                    return ReadMeta(sr);
                }
            }
        }

        private IESData ReadMeta(StreamReader sr)
        {
            var ies = new IESData()
            {
                Labels = new SymbolDict<string>()
            };

            String fileFormat = sr.ReadLine();
            // labels = new List<(string, string)>();

            // in case of old format 1986
            if (fileFormat.StartsWith("["))
            {
                var labelInput = LabelSplitForDict(fileFormat);
                ies.Labels.Add(labelInput[0], labelInput[1]);
                ies.Format = IESformat.IES1986;
            }
            else
            {
                if (fileFormat.Contains("1991")) ies.Format = IESformat.IES1991;
                else if (fileFormat.Contains("1995")) ies.Format = IESformat.IES1995;
                else if (fileFormat.Contains("2002")) ies.Format = IESformat.IES2002;
                else ies.Format = IESformat.Unkown;
            }

            String tempLabel = sr.ReadLine();
            while (tempLabel.StartsWith("["))
            {
                var labelInput = LabelSplitForDict(tempLabel);
                ies.Labels[labelInput[0]] = labelInput[1];
                tempLabel = sr.ReadLine();
            }

            var tilt = tempLabel;
            tilt.Remove(0, 5);
            // case parameters are availible
            if (tilt == ("INCLUDE"))
            {
                var lampToLuminaireGeometry = ReadIntLine(sr);
                var AnglesAndMultiplyingFactors = ReadIntLine(sr);
                var angles = ReadDoubleLine(sr);
                var multiplyingFactors = ReadDoubleLine(sr);
            }
            else if (tilt != ("NONE"))
            {
                var refFileName = tilt;
            } // else it is NONE...no additional information is given

            String[] para = StringSplit(sr.ReadLine());
            if (para.Length == 10)
            {
                ies.NumberOfLamps = ParseInt(para[0]);
                ies.LumenPerLamp = ParseDouble(para[1]);
                ies.CandelaMultiplier = ParseDouble(para[2]);
                ies.VerticalAngleCount = ParseInt(para[3]);
                ies.HorizontalAngleCount = ParseInt(para[4]);
                ies.Photometric = (IESPhotometricType)ParseInt(para[5]);
                ies.Unit = (IESUnitType)ParseInt(para[6]);
                ies.LuminaireWidth = ParseDouble(para[7]);
                ies.LuminaireLength = ParseDouble(para[8]);
                ies.LuminaireHeight = ParseDouble(para[9]);
            }
            else Console.WriteLine("ERROR - Parse ISE File (Parameters");

            String[] ballast = StringSplit(sr.ReadLine());
            if (ballast.Length == 3)
            {
                double ballastFactor = ParseDouble(ballast[0]);
                double ballastLampPhotometricFactor = ParseDouble(ballast[1]);
                ies.InputWatts = ParseDouble(ballast[2]);
            }
            else Console.WriteLine("ERROR - Parse ISE File (Ballast)");

            return ies;
        }

        public IESData Parse(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var ies = ReadMeta(sr);

                // Vertical Angles
                String[] verticalAngles = StringSplit(sr.ReadLine());
                while (verticalAngles.Length != ies.VerticalAngleCount)
                {
                    verticalAngles = verticalAngles.Concat(StringSplit(sr.ReadLine())).ToArray();
                }
                var vertAngleValues = verticalAngles.Map(str => ParseDouble(str));

                ies.VerticleAngles = vertAngleValues;

                // Horizontal Angles
                String[] horizontalAngles = StringSplit(sr.ReadLine());
                while (horizontalAngles.Length != ies.HorizontalAngleCount)
                {
                    horizontalAngles = horizontalAngles.Concat(StringSplit(sr.ReadLine())).ToArray();
                }
                var horizAngleValues = horizontalAngles.Map(str => ParseDouble(str));

                ies.HorizontalAngles = horizAngleValues;

                // Candela Values
                String[] candelaValues = StringSplit(sr.ReadLine());
                while (candelaValues.Length != ies.HorizontalAngleCount * ies.VerticalAngleCount)
                {
                    candelaValues = candelaValues.Concat(StringSplit(sr.ReadLine())).ToArray();
                }
                double[] candelaValuesParsed = candelaValues.Map(str => ParseDouble(str));

                ies.Data = new Matrix<double>(new V2i(ies.VerticalAngleCount, ies.HorizontalAngleCount)).SetByIndex(i => candelaValuesParsed[i]);

                return ies;
            }
        }
    }
}