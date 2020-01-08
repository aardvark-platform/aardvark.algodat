using Aardvark.Base;
using System;
using System.IO;
using System.Text;

#pragma warning disable 1591 // missing XML comments

namespace Aardvark.Data.Photometry
{
    public static class IESLabel
    {
        public static readonly Symbol Manufacturer = "MANUFAC";
        public static readonly Symbol TestReportNumber = "TEST";
        public static readonly Symbol LuminaireCatalog = "LUMCAT";
        public static readonly Symbol LuminaireDescription = "LUMINAIRE";
        public static readonly Symbol LampCatalog = "LAMPCAT";
        public static readonly Symbol LampDescription = "LAMP";
        public static readonly Symbol IssueDate = "ISSUEDATE";
    }
    public enum IESformat
    {
        IES1986,
        IES1991,
        IES1995,
        IES2002,
        Unkown
    }

    public enum IESUnitType
    {
        Feet = 1,
        Meter = 2
    }

    public enum IESPhotometricType
    {
        /// <summary>
        /// Type C photometry is normally used for architectural and roadway
        /// luminaires. The polar axis of the photometric web coincides with the
        /// vertical axis of the luminaire, and the 0-180 degree photometric plane
        /// coincides with the luminaire's major axis (length).
        /// </summary>
        C = 1,
        /// <summary>
        /// Type B photometry is normally used for adjustable outdoor area and sports
        /// lighting luminaires. The polar axis of the luminaire coincides with the
        /// minor axis (width) of the luminaire, and the 0-180 degree photometric
        /// plane coinicides with the luminaire's vertical axis.
        /// </summary>
        B = 2, 
        /// <summary>
        /// Type A photometry is normally used for automotive headlights and signal
        /// lights. The polar axis of the luminaire coincides with the major axis
        /// (length) of the luminaire, and the 0-180 degree photometric plane
        /// coinicides with the luminaire's vertical axis.
        /// </summary>
        A = 3,
    }

    /// <summary>
    /// Holds the data represented in an IES luminaire data file
    /// http://lumen.iee.put.poznan.pl/kw/iesna.txt
    /// </summary>
    public class IESData
    {
        public static IESData FromFile(String filePath)
        {
            return IESParser.Parse(filePath);
        }

        public static IESData FromStream(Stream stream)
        {
            return new IESParser().Parse(stream);
        }

        public static IESData FromString(string data)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
            return FromStream(stream);
        }

        public static IESData ParseMeta(String filePath)
        {
            return new IESParser().ParseMeta(filePath);
        }

        public IESData() { }

        public IESformat Format { get; set; }
        public Matrix<double> Data { get; set; }
        public double[] VerticleAngles { get; set; }
        public double[] HorizontalAngles { get; set; }
        public int HorizontalAngleCount { get; set; }
        public int VerticalAngleCount { get; set; }
        public SymbolDict<String> Labels { get; set; }
        public IESUnitType Unit { get; set; }
        public IESPhotometricType Photometric { get; set; }

        /// <summary>
        /// Luminous opening in C0-C180 plane.
        /// </summary>
        public double LuminaireWidth { get; set; }

        /// <summary>
        /// Luminous opening in C90-C270 plane.
        /// </summary>
        public double LuminaireLength { get; set; }

        /// <summary>
        /// Vertical luminous opening
        /// </summary>
        public double LuminaireHeight { get; set; }
        public double CandelaMultiplier { get; set; }
        public double LumenPerLamp { get; set; }
        public int NumberOfLamps { get; set; }
        public double InputWatts { get; set; }
    }
}
