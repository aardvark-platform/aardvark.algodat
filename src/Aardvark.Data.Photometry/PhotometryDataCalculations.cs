using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace Aardvark.Data.Photometry
{
    public partial class LightMeasurementData
    {
        /// <summary>
        /// Calculates the luminous flux from an equidistant measurement dataset
        /// </summary>
        public static double CalculateLumFlux(Matrix<double> equiDistantData, VerticalRangeMode vertRangeMode)
        {
            double sum = 0.0;
            double weightSum = 0.0;
            double vFact = 0.0;
            //double vFact1 = 0.0;

            double angleScale = vertRangeMode == VerticalRangeMode.Full ? 1.0 : 0.5;
            double angleOffset = vertRangeMode == VerticalRangeMode.Top ? 0.5 : 0.0;

            //Report.BeginTimed("CalcLumFlux");

            // each vertical angle has to be weighted according to circumference of  at "equator"
            var mtxInfo = equiDistantData.Info;
            long xs = mtxInfo.DSX, xj = mtxInfo.JXY;
            long ys = mtxInfo.DSY, yj = mtxInfo.JY0;
            long i = mtxInfo.FirstIndex;
            for (long xe = i + xs, x = mtxInfo.FX; i != xe; i += xj, x++)
            {
                // calc cos of vAngle
                var vAngle = x / (double)(equiDistantData.Size.X - 1); // weight bottom/to with slightly offseted angles -> otherwise they would have weight=0
                vAngle = vAngle * angleScale + angleOffset;

                vFact = Fun.Sin(vAngle * Constant.Pi); // radius of circle -> circumference = 2r*PI

                weightSum += vFact * equiDistantData.Size.Y;

                // Calculate weight for interpolated value (between x and  x+1 -> x+0.5)
                //var vAngle1 = (x + 0.5) / (double)(Raw.Size.X - 1); // weight bottom/to with slightly offseted angles -> otherwise they would have weight=0
                //vAngle1 = vAngle1 * angleScale + angleOffset;

                //vFact1 = Fun.Sin(vAngle1 * Constant.Pi); // radius of circle -> circumference = 2r*PI

                //weightSum += vFact1 * Raw.Size.Y;

                for (long ye = i + ys, y = mtxInfo.FY; i != ye; i += yj, y++)
                {
                    var value = equiDistantData[x, y];
                    sum += value * vFact;

                    //if (x + 1 < Raw.Size.X)
                    //{
                    //    var value1 = Raw[x + 1, y];
                    //    sum += (value1 + value) * 0.5 * vFact1;
                    //}
                }
            }

            //Report.End();

            var lumFlux = sum / weightSum;

            if (vertRangeMode != VerticalRangeMode.Full)
                lumFlux /= 2; // half the angles are zero -> average flux over sphere is half
            return lumFlux * Constant.PiTimesFour;
        }

        /// <summary>
        /// Calculates the luminous flux from the measurement data.
        /// </summary>
        public double CalculateLumFlux()
        {
            var lumFlux = 0.0;

            double segmentAreaFull = 0.0; // full sphere segment area till current angle when looping over data

            // weight factor for each data segment
            var hs = this.HorizontalSymmetry;
            var connectToFirst = hs != HorizontalSymmetryMode.Half && hs != HorizontalSymmetryMode.Quarter;
            var segCount = this.Intensities.Size.Y; // number of c-planes data rows
            if (!connectToFirst) segCount -= 1;
            var dataAreaFactor = 1.0 / segCount;

            var weight0 = 0.0;

            var mtxInfo = this.Intensities.Info;
            long xs = mtxInfo.DSX, xj = mtxInfo.JXY;
            long ys = mtxInfo.DSY, yj = mtxInfo.JY0;
            long i = mtxInfo.FirstIndex;
            for (long xe = i + xs, x = mtxInfo.FX; i != xe; i += xj, x++)
            {
                // x: vertical angle [0, 180] or [0, 90] or [90, 180]
                if (x == 0) { i += ys; continue; }

                var phi = this.VerticalAngles[x] * Constant.RadiansPerDegree;

                // calculate area of full segment till theta1
                var a = Fun.Sin(phi);
                var h = 1 - Fun.Cos(phi);
                var segmentAreaPhi1 = Constant.Pi * (a.Square() + h.Square());

                // area of current segment
                var dataSegmentArea = (segmentAreaPhi1 - segmentAreaFull) * dataAreaFactor;

                segmentAreaFull = segmentAreaPhi1;

                // weight data points by circumference of measurement angle
                var weight1 = a; // circumference is actually 2pi * r, but the constant factor can be omitted when calculating the weighed average

                // if a weight is 0, this means we are the pole (0° or 180°) -> give half weight to pole sample
                if (weight0 == 0) weight0 = weight1 * 0.5;
                if (weight1 == 0) weight1 = weight0 * 0.5;

                var weightNorm = 1.0 / ((weight0 + weight1) * 2); // 1.0 / weightSum -> x2 as there are two samples each

                var iTheta0 = 0.0;
                // y: horizontal angle / c-plane [0] or [0-360] or [0-90] or [0-180] or [-90-90] or [90-270]
                for (long ye = i + ys, y = mtxInfo.FY; i != ye; i += yj, y++)
                {
                    var iTheta1 = this.Intensities[x, y] * weight1 + this.Intensities[x - 1, y] * weight0;
                    if (y > 0)
                    {
                        var iavg = (iTheta0 + iTheta1) * weightNorm;
                        lumFlux += iavg * dataSegmentArea;
                    }

                    iTheta0 = iTheta1;
                }

                if (connectToFirst)
                {
                    // connect to first data row
                    var iTheta1 = this.Intensities[x, 0] * weight1 + this.Intensities[x - 1, 0] * weight0;
                    var iavg = (iTheta0 + iTheta1) * weightNorm;
                    lumFlux += iavg * dataSegmentArea;
                }

                weight0 = weight1;
            }

            return lumFlux;
        }

        /// <summary>
        /// Retrieves the major intensity and direction angles of the photometric measurement.
        /// </summary>
        /// <returns>(Intensity, C-Plane in degrees, angle Gamma in degrees)</returns>
        public (double, double, double) CalculateMajor()
        {
            V2l majorIndex = V2l.Zero;
            var majorInt = 0.0;

            Intensities.ForeachCoord(crd =>
            {
                var value = Intensities[crd];

                if (value > majorInt)
                {
                    majorInt = value;
                    majorIndex = crd;
                }
            });

            var gamma = VerticalAngles[majorIndex.X];
            var cplane = HorizontalAngles[majorIndex.Y];

            return (majorInt, cplane, gamma);
        }

        /// <summary>
        /// Finds the maximum intensity and direction of a given measurement plane.
        /// The specified plane must be one of the horizontal measurement angles,
        /// interpolated c0 planes are not supported.
        /// </summary>
        /// <param name="plane">[0, 360]</param>
        /// <returns>(Intensity, angle Gamma in degrees)</returns>
        public (double, double) CalculateCMajor(double plane)
        {
            var index = GetPlaneIndex(plane);

            // c-plane values:
            var cPlaneValues = Intensities.GetRow(index);
            var cMaxIndex = cPlaneValues.Elements.MaxIndex();
            var cMaxValue = cPlaneValues[cMaxIndex];

            var gamma = VerticalAngles[cMaxIndex];

            return (cMaxValue, gamma);
        }

        /// <summary>
        /// Calculates luminous flux per zone
        /// Zones: 0-10, 10-20, 20-30, ... 170-180 
        /// Gives a total of 18 zones.
        /// </summary>
        /// <returns>Array of length 18 containing luminous flux per zone</returns>
        public double[] CalculateZones()
        {
            // zones 0-10, 10-20, 20-30, ... 170-180 
            var zones = new double[18];

            double segmentAreaFull = 0.0; // full sphere segment area till current angle when looping over data

            // weight factor for each data segment
            var hs = this.HorizontalSymmetry;
            var connectToFirst = hs != HorizontalSymmetryMode.Half && hs != HorizontalSymmetryMode.Quarter;
            var segCount = this.Intensities.Size.Y; // number of c-planes data rows
            if (!connectToFirst) segCount -= 1;
            var dataAreaFactor = 1.0 / segCount;

            bool changeZone = false;
            bool repeatVertical = false;
            int zone = 0;

            var weight0 = 0.0;

            var mtxInfo = this.Intensities.Info;
            long xs = mtxInfo.DSX, xj = mtxInfo.JXY;
            long ys = mtxInfo.DSY, yj = mtxInfo.JY0;
            long i = mtxInfo.FirstIndex;
            for (long xe = i + xs, x = mtxInfo.FX; i != xe; i += xj, x++)
            {
                // x: vertical angle [0, 180] or [0, 90] or [90, 180]
                if (x == 0) { i += ys; continue; }

                var phi = this.VerticalAngles[x];

                if (changeZone) { zone++; changeZone = false; }

                // clamp theta1 to zone limit
                if (phi > (zone + 1) * 10)
                {
                    phi = (zone + 1) * 10;
                    repeatVertical = true;
                }

                changeZone = phi == (zone + 1) * 10;

                phi *= Constant.RadiansPerDegree;
                // calculate area of full segment till theta1
                var a = Fun.Sin(phi);
                var h = 1 - Fun.Cos(phi);
                var segmentAreaPhi1 = Constant.Pi * (a.Square() + h.Square());

                // area of current segment
                var dataSegmentArea = (segmentAreaPhi1 - segmentAreaFull) * dataAreaFactor;

                segmentAreaFull = segmentAreaPhi1;

                // weight data points by circumference of measurement angle
                var weight1 = a; // circumference is actually 2pi * r, but the constant factor can be omitted when calculating the weighed average

                // if a weight is 0, this means we are the pole (0° or 180°) -> give half weight to pole sample
                if (weight0 == 0) weight0 = weight1 * 0.5;
                if (weight1 == 0) weight1 = weight0 * 0.5;

                var weightNorm = 1.0 / ((weight0 + weight1) * 2); // 1.0 / weightSum -> x2 as there are two samples each

                var iTheta0 = 0.0;
                // y: horizontal angle / c-plane [0] or [0-360] or [0-90] or [0-180] or [-90-90] or [90-270]
                for (long ye = i + ys, y = mtxInfo.FY; i != ye; i += yj, y++)
                {
                    var iTheta1 = this.Intensities[x, y] * weight1 + this.Intensities[x - 1, y] * weight0;
                    if (y > 0)
                    {
                        var iavg = (iTheta0 + iTheta1) * weightNorm;
                        zones[zone] += iavg * dataSegmentArea;
                    }

                    iTheta0 = iTheta1;
                }

                if (connectToFirst)
                {
                    // connect to first data row
                    var iTheta1 = this.Intensities[x, 0] * weight1 + this.Intensities[x - 1, 0] * weight0;
                    var iavg = (iTheta0 + iTheta1) * weightNorm;
                    zones[zone] += iavg * dataSegmentArea;
                }

                weight0 = weight1;

                if (repeatVertical)
                {
                    repeatVertical = false;
                    i -= ys;
                    i -= xj;
                    x--;
                }
            }

            return zones;
        }
    }

    /// <summary>
    /// Collection of IntensityProfileSampler extension 
    /// </summary>
    public static class PhotometryDataCalculations
    {
        /// <summary>
        /// Samples the luminous flux per zone using an IntensityProfileSampler.
        /// The result should be approximately equal to CalculateZones.
        /// A sampleCount for at least 8192 should be used for reliable results.
        /// Zones: 0-10, 10-20, 20-30, ... 170-180 
        /// Gives a total of 18 zones.
        /// </summary>
        /// <returns>Array of length 18 containing luminous flux per zone</returns>
        public static double[] SampleZones(this IntensityProfileSampler sampler, int sampleCount)
        {
            // zones 0-10, 10-20, 20-30, ... 170-180 
            var zones = new double[18];

            var rnd = new RandomSystem(18);
            var rndSeries = new HaltonRandomSeries(2, rnd);

            for (int i = 0; i < sampleCount; i++)
            {
                var v = RandomSample.Spherical(rndSeries, 0);

                var phi = Fun.AcosC(-v.Z); // 0 to pi [0, 180°]

                var zi = Fun.Min((int)(phi * 18 * Constant.PiInv), 17);

                zones[zi] += sampler.GetIntensity(v);
            }

            var norm = Constant.PiTimesFour / sampleCount;
            zones.Apply(x => x * norm);

            return zones;
        }

        /// <summary>
        /// Calculates the luminous flux by using sampling to integrate luminous intensity function.
        /// </summary>
        public static double SampleLumFlux(this IntensityProfileSampler sampler, int sampleCount)
        {
            var rnd = new RandomSystem(18);
            var rndSeries = new HaltonRandomSeries(2, rnd);

            var lumFlux = 0.0;
            for (int i = 0; i < sampleCount; i++)
            {
                var v = RandomSample.Spherical(rndSeries, 0);
                lumFlux += sampler.GetIntensity(v);
            }

            lumFlux *= Constant.PiTimesFour / sampleCount;

            return lumFlux;
        }
    }
}
