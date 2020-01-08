using Aardvark.Base;
using Aardvark.Base.Coder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Aardvark.Data.Photometry
{
    /// <summary>
    /// Horizontal symmetry specification of photometric measurement data
    /// </summary>
    public enum HorizontalSymmetryMode
    {
        /// No Symmetry
        None,
        /// Full symmetrically
        Full,
        /// Mirrored along one plane (0-180 or -90 to 90 or 90 to 270)
        Half,
        /// 0 - 90
        Quarter,
        /// Measurement angles do not match any common symmetry case
        Unknown
    }

    /// <summary>
    /// Vertical data range specification of photometric measurements
    /// </summary>
    public enum VerticalRangeMode
    {
        /// 0-180
        Full,
        /// 0 - 90 (e.g. Ceiling-mounted-light)
        Bottom,
        /// 90 - 180 (e.g. Floor-mounted-light)
        Top,
    }

    /// <summary>
    /// Unified representation of photometry measurement data.
    /// </summary>
    [RegisterTypeInfo(Version=4)]
    public partial class LightMeasurementData : IFieldCodeable, IAwakeable
    {
        /// <summary>
        /// Specified luminous flux in lumen supplied by the photometric report.
        /// </summary>
        public double LumFlux;

        /// <summary>
        /// Measured intensities in cd [Vertical, Horizontal]
        /// X = VerticalAngles, Y = HorizontalAngles (C-Planes)
        /// 
        ///       0°  5° 10° ...
        ///      ---------------
        /// C0  | x   x   x  ...
        /// C30 | x   x   x  ...
        /// C60 | x   x   x  ...
        ///   . | .   .   .  .
        ///   . | .   .   .    .
        ///   
        /// </summary>
        public Matrix<double> Intensities;

        /// <summary>
        /// Horizontal symmetry type
        /// </summary>
        public HorizontalSymmetryMode HorizontalSymmetry;

        /// <summary>
        /// Vertical symmetry type
        /// </summary>
        public VerticalRangeMode VerticalRange = VerticalRangeMode.Full;

        /// <summary>
        /// Horizontal angles of measurement in degree in counter clockwise order.
        /// Typical cases of measurement angles are:
        /// Full symmetry - single value annotated as 0° 
        /// Complete measurement (no symmetry) - [0, 360-measureDistance] or [0, 360] in some IES files
        /// Symmetry across C0-C180 plane - [0, 180]
        /// Symmetry across C90-C70 plane - [270, 90] in LDT or [90, 270] in IES
        /// Symmetry across C0-C180 and C90-C270 planes - [0, 90]
        /// </summary>
        public double[] HorizontalAngles;

        /// <summary>
        /// Vertical angles of measurement in degree where 0 is pointing to the bottom and 180 to the top.
        /// Typical measurement angles ranges are [0, 180], [0, 90] and [90, 180].
        /// </summary>
        public double[] VerticalAngles;

        /// <summary>
        /// Name of the measurement data (LuminaireDescription or LuminaireName)
        /// </summary>
        public string Name;

        /// <summary>
        /// Creates an empty LightMeasurementData (need to be initialized programmatically)
        /// </summary>
        public LightMeasurementData() { }

        /// <summary>
        /// Create a LightMeasurementData from IES data
        /// </summary>
        public LightMeasurementData(IESData ies)
        {
            Name = ies.Labels.Get(IESLabel.LuminaireDescription, null);

            Intensities = ies.Data;

            var firstHoriz = ies.HorizontalAngles[0];
            var lastHoriz = ies.HorizontalAngles[ies.HorizontalAngleCount - 1];
            var firstVert = ies.VerticleAngles[0];
            var lastVert = ies.VerticleAngles[ies.VerticalAngleCount - 1];

            HorizontalAngles = ies.HorizontalAngles;
            VerticalAngles = ies.VerticleAngles;

            var range = (firstHoriz - lastHoriz).Abs();
            if (HorizontalAngles.Length == 1) HorizontalSymmetry = HorizontalSymmetryMode.Full;
            else if (range == 90) HorizontalSymmetry = HorizontalSymmetryMode.Quarter;
            else if (range == 180) HorizontalSymmetry = HorizontalSymmetryMode.Half;
            else if (range > 180) HorizontalSymmetry = HorizontalSymmetryMode.None; 
            // NOTE: this is an error in the draft IES LM-63 - 1995 standard, because
            // the 360 - degree plane is coincident with the 0 - degree plane. It
            // should read "greater than 180 degrees and less than 360 degrees"
            else { HorizontalSymmetry = HorizontalSymmetryMode.Unknown; Report.Warn("IES-File {0} has a non-compatible HorizontalSymmetryMode", Name); }

            if (firstVert == 0 && lastVert == 180) VerticalRange = VerticalRangeMode.Full;
            else if (firstVert == 0 && lastVert == 90) VerticalRange = VerticalRangeMode.Bottom;
            else if (firstVert == 90 && lastVert == 180) VerticalRange = VerticalRangeMode.Top;
            else Report.Warn("IES-File {0} has a non-valid VerticalRangeMode", Name);
            
            LumFlux = ies.NumberOfLamps * ies.LumenPerLamp * ies.CandelaMultiplier;
        }

        /// <summary>
        /// Create a LightMeasurementData from LDT data
        /// </summary>
        public LightMeasurementData(LDTData ldt)
        {
            Name = ldt.LuminaireName;
            
            var isAbsolute = ldt.LampSets.FirstOrDefault().TrySelect(x => x.Number < 0, true);
            if (!isAbsolute)
            {
                var scale = ldt.LampSets.Sum(x => x.TotalFlux / 1000);
                var scaled = ldt.Data.Copy();
                scaled.Apply(x => x * scale);

                Intensities = scaled;
            }
            else
            {
                Intensities = ldt.Data;
            }

            HorizontalSymmetry = ConvertSymmetry(ldt.Symmetry);
            
            // LDT has all horizontal angles written, possible more that the data array -> filter in that case
            HorizontalAngles = ldt.Symmetry == LDTSymmetry.C0 ? FilterAnglesBetween(ldt.HorizontalAngles, 0, 180)
                             : ldt.Symmetry == LDTSymmetry.C1 ? FilterAnglesBetween(ldt.HorizontalAngles, 270, 359.9).Concat(FilterAnglesBetween(ldt.HorizontalAngles, 0, 90)).ToArray()
                             : ldt.Symmetry == LDTSymmetry.Quarter ? FilterAnglesBetween(ldt.HorizontalAngles, 0, 90)
                             : ldt.HorizontalAngles;

            VerticalAngles = ldt.VerticleAngles; 

            if (ldt.VerticleAngles.First() == 0 && ldt.VerticleAngles.Last() == 180) VerticalRange = VerticalRangeMode.Full;
            else if (ldt.VerticleAngles.First() == 0 && ldt.VerticleAngles.Last() == 90) VerticalRange = VerticalRangeMode.Bottom;
            else if (ldt.VerticleAngles.First() == 90 && ldt.VerticleAngles.Last() == 180) VerticalRange = VerticalRangeMode.Top;
            else Report.Warn("LDT-File {0} has a non-valid VerticalRangeMode", Name);
            
            var lor = ldt.LightOutputRatioLuminaire / 100;
            LumFlux = ldt.LampSets.Sum(x => x.TotalFlux) * lor;
        }

        private int GetPlaneIndex(double plane)
        {
            // NOTE: throw IndexNotFound exception
            // plane must be [0; 360]

            // early exit if there is full symmetry
            if (this.HorizontalSymmetry == HorizontalSymmetryMode.Full)
                return 0;

            var dataPlane = plane % 360.0;
            // only special case if there is no full 360° measurement
            if (this.HorizontalSymmetry != HorizontalSymmetryMode.None)
            {
                var first = this.HorizontalAngles.First();
                var last = this.HorizontalAngles.Last();
                var range = (first - last).Abs(); // measurement 270-90

                //var z = -first / 360.0f;
                //var w = 360.0 / range;

                //var t = dataPlane / 360.0;
                //var v = 1.0 - Fun.Abs(1.0f - Fun.Abs(((t + z) * w) % 2.0));
                //var a = (v * range + first) % 360;

                var a2 = range - Fun.Abs(range - Fun.Abs((dataPlane - first) % (range * 2))); // mirror and repeat if outside range
                dataPlane = (a2 + first) % 360; 
            }
            
            // get index of measurement data
            return this.HorizontalAngles.IndexOf(dataPlane);
        }

        /// <summary>
        /// Returns photometric measurement values along a C-Plane with (vertical-angle in degrees, intensity).
        /// The plane [0; 360] must be one of the horizontal measurement planes of the photometric report, 
        /// interpolated values are not supported.
        /// 
        /// Example: plane=0 gives C0-180 360° measurement values
        ///          plane=15 gives C15-195 measurement values
        ///          plane=180 gives C180-0 measurement values (left-right mirrored values of C0-180)
        /// </summary>
        public List<(double, double)> GetCPlane(double plane)
        {
            var index = GetPlaneIndex(plane);

            // c-plane values:
            var cPlaneValues = Intensities.GetRow(index);

            var values = new List<(double, double)>((int)cPlaneValues.Count * 2 - 2);

            int i = 0;
            foreach (var value in cPlaneValues.Elements)
            {
                values.Add((VerticalAngles[i++], value));
            }

            // stitch second half of c-plane to angle-value pairs (+180°)            

            var index180 = GetPlaneIndex(plane + 180);
            var cPlaneValues180 = Intensities.GetRow(index180);
            cPlaneValues180 = cPlaneValues180.SubVector(1, cPlaneValues180.Size - 2); // skip angle at top and bottom (would be twice otherwise)

            i = 1;
            foreach (var value in cPlaneValues180.Elements)
            {
                values.Add((-VerticalAngles[i++], value));
            }

            return values;
        }
        
        /// <summary>
        /// Builds an equidistant measurement data matrix. 
        /// NOTE: The symmetry type is the same.
        /// </summary>
        /// <returns>Equidistant measurement data or original in case it is already equidistant</returns>
        public Matrix<double> BuildEquidistantMatrix()
        {
            var result = Intensities;

            if (IsNonEquidistant(HorizontalAngles)) result = FixNonEquidistantAngleStep(HorizontalAngles, result, true);
            if (IsNonEquidistant(VerticalAngles)) result = FixNonEquidistantAngleStep(VerticalAngles, result, false);

            return result;
        }

        private Matrix<double> FixNonEquidistantAngleStep(double[] angles, Matrix<double> data, bool horizontalFix)
        {
            var minStep = Double.MaxValue;

            for (var i = 0; i < angles.Length - 1; i++)
            {
                var diff = AngleDifference(angles[i + 1], angles[i]);
                if (diff < minStep) minStep = diff;
            }

            if (minStep <= 0)
                throw new Exception();

            var valuesPerPlane = data.SX;
            var planeCount = data.SY;

            var elementCount = (long)((AngleDifference(angles[angles.Length - 1], angles[0])) / minStep) + 1;

            if (elementCount >= 4096)
            {
                elementCount = 4096;
                Report.Warn("Repairing Non-Equidistant Photometry Steps results into too many elements (Max. Elements per C-Plane is 4096)!");
            }

            if (horizontalFix) planeCount = elementCount;
            else valuesPerPlane = elementCount;

            var tempMatrix = new Matrix<double>(valuesPerPlane, planeCount);

            if (horizontalFix)
            {
                // Horizontal interpolation
                for (var valueOnPlane = 0; valueOnPlane < valuesPerPlane; valueOnPlane++)
                {
                    var searchIndex = 0;

                    for (var plane = 0; plane < planeCount; plane++)
                    {
                        var myAngle = angles[0] + plane * minStep;

                        // check if upper lookupAngle is to small
                        if (myAngle > angles[searchIndex + 1]) searchIndex++;

                        var refLower = angles[searchIndex];
                        var refUpper = angles[searchIndex + 1];

                        var lowerV = data[valueOnPlane, searchIndex];
                        var upperV = data[valueOnPlane, searchIndex + 1];

                        var t = (myAngle - refLower) / (refUpper - refLower);

                        var value = Fun.Lerp(t, lowerV, upperV);

                        tempMatrix[valueOnPlane, plane] = value;
                    }
                }
            }
            else
            {
                // Vertical interpolation
                for (var plane = 0; plane < planeCount; plane++)
                {
                    var searchIndex = 0;

                    for (var valueOnPlane = 0; valueOnPlane < valuesPerPlane; valueOnPlane++)
                    {
                        var myAngle = angles[0] + valueOnPlane * minStep;

                        // check if upper lookupAngle is to small
                        if (myAngle > angles[searchIndex + 1]) searchIndex++;

                        var refLower = angles[searchIndex];
                        var refUpper = angles[searchIndex + 1];

                        var lowerV = data[searchIndex, plane];
                        var upperV = data[searchIndex + 1, plane];

                        var t = (myAngle - refLower) / (refUpper - refLower);

                        var value = Fun.Lerp(t, lowerV, upperV);

                        tempMatrix[valueOnPlane, plane] = value;
                    }
                }
            }

            return tempMatrix;
        }

        private double AngleDifference(double a, double b)
        {
            var diff = a - b;
            var mod = diff - 360 * Fun.Round(diff / 360);
            return mod;
        }

        private bool IsNonEquidistant(double[] angles)
        {
            if (angles.Length < 3) return false;

            var minStep = AngleDifference(angles[1], angles[0]);

            for (var i = 1; i < angles.Length - 1; i++)
            {
                var diff = AngleDifference(angles[i + 1], angles[i]);
                if (diff != minStep) return true;
            }

            return false;
        }

        private double[] FilterAnglesBetween(double[] angles, double lower, double upper)
        {
            //return angles.SkipWhile(v => v < lower).TakeWhile(v => v <= upper).ToArray();

            var list = new List<double>();

            for (var i = 0; i < angles.Length; i++)
            {
                var angle = angles[i];
                if (angle >= lower && angle <= upper) list.Add(angle);
            }

            return list.ToArray();
        }

        #region IFieldCodeable Members
        /// <summary>
        /// Implementation of FieldCoders for serialization
        /// </summary>
        public IEnumerable<FieldCoder> GetFieldCoders(int coderVersion)
        {
            yield return new FieldCoder(0, "HorizontalSymmetry", (c, o) => c.CodeT(ref ((LightMeasurementData)o).HorizontalSymmetry));
            yield return new FieldCoder(1, "VerticalRange", (c, o) => c.CodeT(ref ((LightMeasurementData)o).VerticalRange));
            yield return new FieldCoder(2, "Intensities", (c, o) => c.CodeMatrix_of_Double_(ref ((LightMeasurementData)o).Intensities));
            yield return new FieldCoder(3, "Name", (c, o) => c.CodeString(ref ((LightMeasurementData)o).Name));
            yield return new FieldCoder(4, "CalculatedLumFlux", 1, 2, (c, o) => c.CodeDouble(ref ((LightMeasurementData)o).LumFlux));
            yield return new FieldCoder(5, "LumFlux", 2, int.MaxValue, (c, o) => c.CodeDouble(ref ((LightMeasurementData)o).LumFlux));
            yield return new FieldCoder(6, "HorizontalAngles", 3, int.MaxValue, (c, o) => c.CodeDoubleArray(ref ((LightMeasurementData)o).HorizontalAngles));
            yield return new FieldCoder(7, "VerticalAngles", 3, int.MaxValue, (c, o) => c.CodeDoubleArray(ref ((LightMeasurementData)o).VerticalAngles));
        }
        #endregion

        /// <summary>
        /// Loads an IES or LDT (EULUMDAT) photometry file.
        /// </summary>
        public static LightMeasurementData FromFile(string fileName)
        {
            if (!File.Exists(fileName))
                throw new Exception(String.Format("could not find light intensity profile: \"{0}\"", Path.GetFileName(fileName)));

            if (fileName.ToLowerInvariant().EndsWith("ldt")) 
            {
                return new LightMeasurementData(LDTData.FromFile(fileName));
            }
            else if (fileName.ToLowerInvariant().EndsWith("ies")) 
            {
                return new LightMeasurementData(IESData.FromFile(fileName));
            }
            else
            {
                throw new Exception(String.Format("light intensity profile: \"{0}\" has invalid format", Path.GetFileName(fileName)));
            }
        }

        /// <summary>
        /// Callback after de-serialization in order to perform version conversion.
        /// </summary>
        public void Awake(int codedVersion)
        {
            if (codedVersion < 4)
            {
                var ldtSymmetry = (LDTSymmetry)this.HorizontalSymmetry;
                this.HorizontalSymmetry = ConvertSymmetry(ldtSymmetry);

                var voffset = this.VerticalRange == VerticalRangeMode.Top ? 90 : 0;
                var vdelta = (this.VerticalRange == VerticalRangeMode.Full ? 180 : 90) / (double)Fun.Max(this.Intensities.SX - 1, 1);
                var hoffset = ldtSymmetry == LDTSymmetry.C1 ? 90 : 0;
                var hdelta = (ldtSymmetry == LDTSymmetry.C0 || ldtSymmetry == LDTSymmetry.C1 ? 180 :
                                ldtSymmetry == LDTSymmetry.Quarter ? 90 :
                                ldtSymmetry == LDTSymmetry.None ? 0 : 360) / (double)Fun.Max(this.Intensities.SY - 1, 1);
                this.VerticalAngles = new double[this.Intensities.SX].SetByIndex(i => voffset + vdelta * i);
                this.HorizontalAngles = new double[this.Intensities.SY].SetByIndex(i => hoffset + hdelta * i);
    
                if (ldtSymmetry == LDTSymmetry.C1)
                    this.HorizontalAngles = this.HorizontalAngles.Map(x => x < 180 ? x + 180 : x - 180);

                // in early versions the LumLux was not been saved -> force calculation, does not perfectly match the rounded LumFlux specified in the meta data of LDT or IES
                if (codedVersion == 0)
                {
                    var equidistanceData = BuildEquidistantMatrix();
                    LumFlux = CalculateLumFlux(equidistanceData, this.VerticalRange);
                }
            }
        }

        static HorizontalSymmetryMode ConvertSymmetry(LDTSymmetry sym)
        {
            return sym == LDTSymmetry.None ? HorizontalSymmetryMode.None :
                   sym == LDTSymmetry.C0 || sym == LDTSymmetry.C1 ? HorizontalSymmetryMode.Half :
                   sym == LDTSymmetry.Quarter ? HorizontalSymmetryMode.Quarter : HorizontalSymmetryMode.Full;
        }
    }
}
