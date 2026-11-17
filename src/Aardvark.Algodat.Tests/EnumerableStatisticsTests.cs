/*
    Copyright (C) 2006-2024. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program. If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Geometry.Points;
using NUnit.Framework;
using System;
using System.Linq;

namespace Aardvark.Geometry.Tests;

[TestFixture]
public class EnumerableStatisticsTests
{
    [Test]
    public void FiniteExtremesAndTinyPairsAvoidIntermediateOverflowAndUnderflow()
    {
        Assert.Multiple(() =>
        {
            foreach (var count in new[] { 2, 3, 8192 })
                Check(Enumerable.Repeat(float.MaxValue, count).ToArray(), float.MaxValue, 0, $"repeated MaxValue, n={count}");
            foreach (var magnitude in new[] { float.MaxValue, 1e20f, 1e-30f, float.Epsilon })
            foreach (var reverse in new[] { false, true })
            {
                var pair = reverse ? new[] { magnitude, -magnitude } : new[] { -magnitude, magnitude };
                Check(pair, 0, magnitude, $"symmetric magnitude={magnitude:R}, reverse={reverse}");
            }
        });
    }

    [Test]
    public void ConstantArraysKeepTheirAverageAndHaveExactlyZeroSpread()
    {
        Assert.Multiple(() =>
        {
            foreach (var count in new[] { 8192, 32768 })
            foreach (var value in new[] { 0.1f, -0.1f, 3.25f, 1e-30f })
                Check(Enumerable.Repeat(value, count).ToArray(), value, 0, $"constant={value:R}, n={count}");
        });
    }

    [Test]
    public void OrdinaryStatisticsUsePopulationNormalizationAndAnUnroundedMean()
    {
        Check(new[] { 1f, 3f }, 2, 1, "population pair, not sample normalization");
        Check(new[] { -2f, 0f, 2f }, 0, Math.Sqrt(8.0 / 3), "three centered values");
        Check(new[] { 1f, 2f, 3f, 4f }, 2.5, Math.Sqrt(1.25), "four values");
        Check(new[] { 1f, 1f, 2f }, 4.0 / 3, Math.Sqrt(2.0 / 9), "fractional mean");
        foreach (var offset in new[] { 10000000f, 16777216f })
            Check(new[] { offset, offset, offset + 2 }, (double)offset + 2.0 / 3, Math.Sqrt(8.0 / 9),
                $"variance around unrounded mean, offset={offset:R}");
        foreach (var count in new[] { 8192, 32768 })
            Check(Enumerable.Range(0, count).Select(i => (float)(i % 4)).ToArray(), 1.5, Math.Sqrt(1.25), $"ordinary repeated pattern, n={count}");
    }

    [Test]
    public void EmptySingletonNullAndNonfiniteInputsKeepTheirExistingBehavior()
    {
        var empty = Array.Empty<float>().ComputeAvgAndStdDev();
        Assert.That(float.IsNaN(empty.avg) && float.IsNaN(empty.stddev), Is.True, "empty input");
        Assert.Throws<NullReferenceException>(() => ((float[])null).ComputeAvgAndStdDev());
        foreach (var value in new[] { float.MaxValue, -float.MaxValue, 1f, -1f, 0.1f, -0.1f, float.Epsilon, -float.Epsilon, 0f, -0f })
        {
            var result = new[] { value }.ComputeAvgAndStdDev();
            var expected = value == 0 ? 0f : value;
            Assert.That(BitConverter.SingleToInt32Bits(result.avg), Is.EqualTo(BitConverter.SingleToInt32Bits(expected)), $"singleton={value:R}");
            Assert.That(BitConverter.SingleToInt32Bits(result.stddev), Is.Zero, $"singleton spread={value:R}");
        }
        foreach (var entry in new[]
        {
            (Values: new[] { float.NaN }, Average: float.NaN),
            (Values: new[] { float.PositiveInfinity }, Average: float.PositiveInfinity),
            (Values: new[] { float.NegativeInfinity }, Average: float.NegativeInfinity),
            (Values: new[] { 1f, float.PositiveInfinity }, Average: float.PositiveInfinity),
            (Values: new[] { -1f, float.NegativeInfinity }, Average: float.NegativeInfinity),
            (Values: new[] { float.NegativeInfinity, float.PositiveInfinity }, Average: float.NaN),
            (Values: new[] { float.NaN, 1f }, Average: float.NaN),
            (Values: new[] { 1f, float.NaN }, Average: float.NaN),
            (Values: new[] { float.PositiveInfinity, float.PositiveInfinity }, Average: float.PositiveInfinity),
            (Values: new[] { float.MaxValue, float.MaxValue, float.NegativeInfinity }, Average: float.NaN),
            (Values: new[] { -float.MaxValue, -float.MaxValue, float.PositiveInfinity }, Average: float.NaN),
            (Values: new[] { float.MaxValue, float.MaxValue, float.PositiveInfinity }, Average: float.PositiveInfinity)
        })
        {
            var before = Bits(entry.Values);
            var result = entry.Values.ComputeAvgAndStdDev();
            var context = "nonfinite input " + string.Join(",", entry.Values.Select(x => x.ToString("R")));
            Assert.That(float.IsNaN(result.stddev), Is.True, context + ", spread");
            if (float.IsNaN(entry.Average)) Assert.That(float.IsNaN(result.avg), Is.True, context + ", average");
            else Assert.That(result.avg, Is.EqualTo(entry.Average), context + ", average");
            Assert.That(Bits(entry.Values), Is.EqualTo(before), context + ", input unchanged");
        }
    }

    private static void Check(float[] values, double expectedAverage, double expectedSpread, string context)
    {
        Assert.That(double.IsFinite(expectedAverage) && double.IsFinite(expectedSpread), Is.True, context + ", finite reference");
        var before = Bits(values);
        var (average, spread) = values.ComputeAvgAndStdDev();
        Assert.That(float.IsFinite(average) && float.IsFinite(spread), Is.True, context + ", finite result");
        Assert.That(average, Is.EqualTo((float)expectedAverage), context + ", average");
        if (expectedSpread == 0)
            Assert.That(spread, Is.Zero, context + ", exact zero spread");
        else
        {
            Assert.That(spread, Is.GreaterThan(0), context + ", nonzero spread");
            Assert.That(Math.Abs((double)spread / expectedSpread - 1), Is.LessThanOrEqualTo(2e-7), context + ", relative spread error");
        }
        Assert.That(Bits(values), Is.EqualTo(before), context + ", input bits unchanged");
    }

    private static int[] Bits(float[] values) => values.Select(BitConverter.SingleToInt32Bits).ToArray();
}
