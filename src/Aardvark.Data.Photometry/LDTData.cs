using Aardvark.Base;
using System;
using System.IO;
using System.Text;

#pragma warning disable 1591 // missing XML comments

namespace Aardvark.Data.Photometry
{
    public class LDTLampData
    {
        public int Number;
        public string Type;
        public double TotalFlux;
        public string Color;
        public string ColorRendering;
        public double Wattage;
    }
    
    public enum LDTSymmetry
    {
        None     = 0, // No symmetry
        Vertical = 1, // Full symmetrically
        C0       = 2, // Measurement data from 0 - 180
        C1       = 3, // Measurement data from 270 - 90
        Quarter  = 4  // Measurement data from 0 - 90
    }

    public enum LDTItype
    {
        PointVerticalSymmetry = 1,
        Linear = 2,         // can be subdivided in longitudinal and transverse directions
        PointWithOtherSymmetry = 3
    }

    /// <summary>
    /// Holds the data represented in an EULUMDATA luminaire data file
    /// http://www.helios32.com/Eulumdat.htm
    /// </summary>
    public class LDTData
    {
        public static LDTData FromFile(String filename)
        {
            return LDTParser.Parse(filename);
        }

        public static LDTData FromStream(Stream stream)
        {
            return new LDTParser().Parse(stream);
        }

        public static LDTData FromString(string data)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
            return FromStream(stream);
        }

        public static LDTData ParseMeta(string filename)
        {
            return new LDTParser().ParseMeta(filename);
        }

        public LDTData() { }

        public Matrix<double> Data { get; set; }
        public double[] VerticleAngles { get; set; }
        public double[] HorizontalAngles { get; set; }
        public int PlaneCount { get; set; }
        public double HorAngleStep { get; set; }
        public int ValuesPerPlane { get; set; }
        public double VertAngleStep { get; set; }
        public LDTSymmetry Symmetry { get; set; }
        public LDTItype Itype { get; set; }
        public string CompanyName { get; set; }
        public string LuminaireName { get; set; }
        public string LuminaireNumber { get; set; }
        public string FileName { get; set; }
        public int LengthLuminaire { get; set; }
        public int WidthLuminaire { get; set; }
        public int HeightLuminare { get; set; }
        public int LengthLuminousArea { get; set; }
        public int WidthLuminousArea { get; set; }
        public int HeightLuminousAreaC0 { get; set; }
        public int HeightLuminousAreaC90 { get; set; }
        public int HeightLuminousAreaC180 { get; set; }
        public int HeightLuminousAreaC270 { get; set; }
        public double DownwardFluxFraction { get; set; }
        public double LightOutputRatioLuminaire { get; set; }
        public double ConversionIntensity { get; set; }
        public double Tilt { get; set; }
        public LDTLampData[] LampSets { get; set; }
        public double[] DirectRatios { get; set; }
    }
}
