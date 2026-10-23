#nullable enable

using System;
using System.Collections.Generic;
using Aardvark.Base;
using Aardvark.Physics.Sky;
using NUnit.Framework;

namespace Aardvark.Algodat.Tests
{
    [TestFixture]
    public class IPhysicalSkyExtensionsTests
    {
        private sealed class ProbeSky : IPhysicalSky
        {
            public static readonly C3f Color = new C3f(0.125f, 0.375f, 0.75f);
            public static readonly C3f Radiance = new C3f(101, 211, 307);
            public int ColorCalls, RadianceCalls;
            public V3d Direction;
            public Exception? ColorFailure, RadianceFailure;

            public C3f GetColor(V3d direction)
            {
                ++ColorCalls;
                Direction = direction;
                if (ColorFailure != null) throw ColorFailure;
                return Color;
            }

            public C3f GetRadiance(V3d direction)
            {
                ++RadianceCalls;
                Direction = direction;
                if (RadianceFailure != null) throw RadianceFailure;
                return Radiance;
            }
        }

        private static IEnumerable<(double Phi, double Theta)> Directions()
        {
            foreach (var phi in new[] { 0.0, 0.7, Math.PI / 2, Math.PI, 3 * Math.PI / 2, 2 * Math.PI, -0.4, 2 * Math.PI + 0.7 })
            foreach (var theta in new[] { 0.0, 0.6, Math.PI / 2 - 0.001 })
                yield return (phi, theta);
        }

        private static C3f Angular(IPhysicalSky sky, double phi, double theta, bool color)
            => color ? sky.GetColor(phi, theta) : sky.GetRadiance(phi, theta);

        private static void AssertDirection(V3d actual, double phi, double theta, string context)
        {
            var expected = Sky.V3dFromPhiTheta(phi, theta);
            for (var axis = 0; axis < 3; ++axis)
                Assert.That(BitConverter.DoubleToInt64Bits(actual[axis]),
                    Is.EqualTo(BitConverter.DoubleToInt64Bits(expected[axis])), $"{context}, axis={axis}");

            if (double.IsFinite(phi) && double.IsFinite(theta))
            {
                var independent = new V3d(-Math.Sin(theta) * Math.Sin(phi), -Math.Sin(theta) * Math.Cos(phi), Math.Cos(theta));
                Assert.That((actual - independent).Length, Is.LessThan(1e-15), context);
                Assert.That(actual.LengthSquared, Is.EqualTo(1).Within(1e-15), context);
            }
        }

        private static void CheckDispatchAndExceptions(bool color)
        {
            var directions = new List<(double Phi, double Theta)>(Directions())
            {
                (1.2, Math.PI), (0.7, -0.25), (-0.0, -0.0), (double.NaN, 0.3), (0.3, double.PositiveInfinity)
            };
            Assert.Multiple(() =>
            {
                foreach (var (phi, theta) in directions)
                {
                    var context = $"color={color}, phi={phi:R}, theta={theta:R}";
                    var probe = new ProbeSky();
                    Assert.That(Angular(probe, phi, theta, color), Is.EqualTo(color ? ProbeSky.Color : ProbeSky.Radiance), context);
                    Assert.That(probe.ColorCalls, Is.EqualTo(color ? 1 : 0), context);
                    Assert.That(probe.RadianceCalls, Is.EqualTo(color ? 0 : 1), context);
                    AssertDirection(probe.Direction, phi, theta, context);

                    var failure = new InvalidOperationException(context);
                    var unusedFailure = new NotSupportedException("unselected method must not be called");
                    probe = new ProbeSky
                    {
                        ColorFailure = color ? failure : unusedFailure,
                        RadianceFailure = color ? unusedFailure : failure
                    };
                    Assert.That(Assert.Throws<InvalidOperationException>(() => Angular(probe, phi, theta, color)), Is.SameAs(failure), context);
                    Assert.That(probe.ColorCalls, Is.EqualTo(color ? 1 : 0), context);
                    Assert.That(probe.RadianceCalls, Is.EqualTo(color ? 0 : 1), context);
                    AssertDirection(probe.Direction, phi, theta, context);
                }
                Assert.Throws<NullReferenceException>(() => Angular(null!, 0.2, 0.3, color));
            });
        }

        [Test]
        public void AngularColorDispatchesOnceWithConvertedDirectionAndForwardsExceptions()
            => CheckDispatchAndExceptions(color: true);

        [Test]
        public void AngularRadianceKeepsDispatchDirectionAndExceptionContracts()
            => CheckDispatchAndExceptions(color: false);

        private static IEnumerable<IPhysicalSky> Models()
        {
            for (var configuration = 0; configuration < 2; ++configuration)
            {
                var phi = configuration * 0.8;
                var theta = configuration == 0 ? Math.PI / 4 : 0.35 * Math.PI;
                yield return new CIESky(phi, theta, configuration == 0 ? CIESkyType.ClearSky2 : CIESkyType.OvercastSky1, 15000, 80000);
                yield return new PreethamSky(phi, theta, configuration == 0 ? 2.5 : 5.5);
                yield return new HosekSky(phi, theta, configuration == 0 ? 3 : 6, new C3f(0.3f, 0.5f, 0.7f), Col.Format.CieXYZ);
                yield return new AlienWorld(phi, theta, configuration == 0 ? 1 : 0.8, configuration == 0 ? 5800 : 4500, 3, 0.3);
            }
        }

        private static void CheckModelParity(bool color)
        {
            var configuration = 0;
            Assert.Multiple(() =>
            {
                foreach (var sky in Models())
                {
                    foreach (var (phi, theta) in Directions())
                    {
                        var context = $"{sky.GetType().Name}, configuration={configuration / 4}, color={color}, phi={phi:R}, theta={theta:R}";
                        var direction = Sky.V3dFromPhiTheta(phi, theta);
                        var expected = color ? sky.GetColor(direction) : sky.GetRadiance(direction);
                        var actual = Angular(sky, phi, theta, color);
                        for (var channel = 0; channel < 3; ++channel)
                        {
                            Assert.That(float.IsFinite(expected[channel]), Is.True, $"{context}, expected channel={channel}");
                            Assert.That(float.IsFinite(actual[channel]), Is.True, $"{context}, actual channel={channel}");
                            Assert.That(BitConverter.SingleToInt32Bits(actual[channel]),
                                Is.EqualTo(BitConverter.SingleToInt32Bits(expected[channel])), $"{context}, channel={channel}");
                            if (color) Assert.That(actual[channel], Is.InRange(0f, 1f), $"{context}, channel={channel}");
                        }
                        Assert.That(sky.GetColor(direction), Is.Not.EqualTo(sky.GetRadiance(direction)), $"{context}, distinguish color from radiance");
                    }
                    ++configuration;
                }
            });
        }

        [Test]
        public void AngularColorMatchesVectorDisplayColorAcrossSkyModels()
            => CheckModelParity(color: true);

        [Test]
        public void AngularRadianceStillMatchesVectorRadianceAcrossSkyModels()
            => CheckModelParity(color: false);
    }
}
