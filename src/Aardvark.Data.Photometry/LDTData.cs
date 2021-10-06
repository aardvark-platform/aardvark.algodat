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

    /// <summary>
    /// ELUMDAT Symmetry indicator - Isym
    /// Specifies how the luminaire has been measured and how the data needs to be interpreted
    /// </summary>
    public enum LDTSymmetry
    {
        None     = 0, // No symmetry
        Vertical = 1, // Full symmetrically
        C0       = 2, // Measurement data from 0 - 180
        C1       = 3, // Measurement data from 270 - 90
        Quarter  = 4  // Measurement data from 0 - 90
    }

    /// <summary>
    /// ELUMDAT Type indicator - Ityp
    /// Indicates the luminaire type and describes its symmetry character. It does not necessarily mean that the measurement data 
    /// is also perfectly symmetrical according to this (e.g. Ityp = 1 does not force Isym = 1)
    /// </summary>
    public enum LDTItype
    {
        PointSource = 0,           // point source with no symmetry
        PointVerticalSymmetry = 1, // symmetry about the vertical axis
        Linear = 2,                // linear luminaire / can be subdivided in longitudinal and transverse directions
        PointWithOtherSymmetry = 3 // point source with any other symmetry
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

        /// <summary>
        /// Intensity distribution normalized to cd per 1000 lumen
        /// </summary>
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
