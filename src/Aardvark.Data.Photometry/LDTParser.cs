using Aardvark.Base;
using System;
using System.IO;

namespace Aardvark.Data.Photometry
{
    class LDTParser : TextParser
    {
        public static LDTData Parse(String filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                return new LDTParser().Parse(stream);
            }
        }

        public LDTData ParseMeta(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                using (var sr = new StreamReader(stream))
                {
                    return ReadMeta(sr);
                }
            }
        }

        private LDTData ReadMeta(StreamReader sr)
        {
            var ldt = new LDTData();
            // 1-5
            ldt.CompanyName = sr.ReadLine();
            ldt.Itype = (LDTItype)Enum.Parse(typeof(LDTItype), sr.ReadLine());
            ldt.Symmetry = (LDTSymmetry)Enum.Parse(typeof(LDTSymmetry), sr.ReadLine());
            ldt.PlaneCount = ReadIntLine(sr);
            ldt.HorAngleStep = ReadDoubleLine(sr);
            // 6-10
            ldt.ValuesPerPlane = ReadIntLine(sr);
            ldt.VertAngleStep = ReadDoubleLine(sr);
            var measurementReportNumber = sr.ReadLine();
            ldt.LuminaireName = sr.ReadLine();
            ldt.LuminaireNumber = sr.ReadLine();
            // 11-15
            ldt.FileName = sr.ReadLine();
            var dateUser = sr.ReadLine();
            ldt.LengthLuminaire = ReadIntLine(sr);
            ldt.WidthLuminaire = ReadIntLine(sr);
            ldt.HeightLuminare = ReadIntLine(sr);
            // 16-20
            ldt.LengthLuminousArea = ReadIntLine(sr);
            ldt.WidthLuminousArea = ReadIntLine(sr);
            ldt.HeightLuminousAreaC0 = ReadIntLine(sr);
            ldt.HeightLuminousAreaC90 = ReadIntLine(sr);
            ldt.HeightLuminousAreaC180 = ReadIntLine(sr);
            // 21-25
            ldt.HeightLuminousAreaC270 = ReadIntLine(sr);
            ldt.DownwardFluxFraction = ReadDoubleLine(sr);
            ldt.LightOutputRatioLuminaire = ReadDoubleLine(sr);
            ldt.ConversionIntensity = ReadDoubleLine(sr);
            ldt.Tilt = ReadDoubleLine(sr);
            // 26 - SET INFO
            var numberOfSets = ReadIntLine(sr);
            ldt.LampSets = new LDTLampData[numberOfSets].SetByIndex(i =>
            {
                var numberOfLamps = ReadIntLine(sr);
                var typeOfLamps = sr.ReadLine();
                var totalLuminousFluxOfLamps = ReadDoubleLine(sr);

                var color = sr.ReadLine();
                //var colorAppearence = color[0];
                //var colorTemperature = color[1];

                var colorRendering = sr.ReadLine();
                //var colorRenderingGroup = color[0];
                //var colorRenderingIndex = color[1];

                var wattageInclBallasts = ReadDoubleLine(sr);

                return new LDTLampData()
                {
                    Number = numberOfLamps,
                    Type = typeOfLamps,
                    TotalFlux = totalLuminousFluxOfLamps,
                    Color = color,
                    ColorRendering = colorRendering,
                    Wattage = wattageInclBallasts,
                };
            });

            return ldt;
        }

        public LDTData Parse(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var ldt = ReadMeta(sr);

                // RATIO
                ldt.DirectRatios = new double[10].SetByIndex(i => ReadDoubleLine(sr));

                // ANGLES C (planes)
                ldt.HorizontalAngles = new double[ldt.PlaneCount].SetByIndex(i => ReadDoubleLine(sr));

                // ANGLES G (ON A PLANE)
                ldt.VerticleAngles = new double[ldt.ValuesPerPlane].SetByIndex(i => ReadDoubleLine(sr));

                // DATA
                var mc1 = 1;
                var mc2 = 1;

                switch (ldt.Symmetry)
                {
                    case (LDTSymmetry.None):
                        mc1 = 1;
                        mc2 = ldt.PlaneCount;
                        break;
                    case (LDTSymmetry.Vertical):
                        mc1 = 1;
                        mc2 = 1;
                        break;
                    case (LDTSymmetry.C0):
                        mc1 = 1;
                        mc2 = ldt.PlaneCount / 2 + 1;
                        break;
                    case (LDTSymmetry.C1):
                        mc1 = 3 * (ldt.PlaneCount / 4) + 1;
                        mc2 = mc1 + ldt.PlaneCount / 2;
                        break;
                    case (LDTSymmetry.Quarter):
                        mc1 = 1;
                        mc2 = ldt.PlaneCount / 4 + 1;
                        break;
                }

                int measurePlanes = (mc2 - mc1 + 1);
                int dataLength = measurePlanes * ldt.ValuesPerPlane;

                var data = new double[dataLength].SetByIndex(i => ReadDoubleLine(sr));
                ldt.Data = Matrix.Create(data, ldt.ValuesPerPlane, measurePlanes);

                return ldt;
            }
        }
    }
}