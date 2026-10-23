using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class GenericChunkColorTests
    {
        private static GenericChunk Points(int count) => new GenericChunk(new Dictionary<Durable.Def, object> {
            [GenericChunk.Defs.Positions3d] = Enumerable.Range(0, count).Select(i => new V3d(2 * i - 1, i, -i)).ToArray(),
            [GenericChunk.Defs.Intensities1i] = Enumerable.Range(100, count).ToArray()
        });

        private static byte[] Bytes<T>(T[] array) where T : unmanaged => MemoryMarshal.AsBytes(array.AsSpan()).ToArray();

        private static void Routes<T>(T[] input, C4b[] expected, Durable.Def definition) where T : unmanaged
        {
            foreach (var route in new[] { "constructor", "array", "list", "iterator" })
            {
                var context = $"{typeof(T).Name}, route={route}, count={input.Length}";
                var inputBits = Bytes(input); var list = new List<T>(input); var enumerations = 0;
                IEnumerable<T> Iterate()
                {
                    enumerations++;
                    foreach (var item in input) yield return item;
                }
                var original = Points(input.Length).WithColors(new C4b[input.Length]);
                var originalData = original.Data;
                var positions = original.Positions; var positionBits = Bytes((V3d[])positions);
                var intensities = (int[])original.Data[GenericChunk.Defs.Intensities1i]; var intensityBits = Bytes(intensities);
                var oldColors = (C4b[])original.Colors; var oldBits = Bytes(oldColors);
                var chunk = route == "constructor" ? new GenericChunk(original.Data.Remove(GenericChunk.Defs.Colors4b).Add(definition, input))
                    : original.WithColors(route == "array" ? (object)input : route == "list" ? list : Iterate());
                Assert.That(chunk.Count, Is.EqualTo(input.Length), context + ": count");
                Assert.That(chunk.HasColors, Is.True, context + ": present even when empty");
                Assert.That(chunk.ColorsDef, Is.EqualTo(definition), context + ": color definition");
                Assert.That(chunk.Colors.GetType(), Is.EqualTo(typeof(T[])), context + ": stored representation");
                Assert.That(chunk.Data.Keys.Count(GenericChunk.Defs.ColorsSupportedDefs.Contains), Is.EqualTo(1), context + ": replacement removes old color definition");
                Assert.That(chunk.Positions, Is.SameAs(positions), context + ": positions reference");
                Assert.That(chunk.Data[GenericChunk.Defs.Intensities1i], Is.SameAs(intensities), context + ": unrelated attribute reference");
                Assert.That(chunk.BoundingBox, Is.EqualTo(original.BoundingBox), context + ": bounds");
                if (route == "constructor" || route == "array") Assert.That(chunk.Colors, Is.SameAs(input), context + ": array storage identity");
                else if (input.Length > 0) Assert.That(chunk.Colors, Is.Not.SameAs(input), context + ": materialized storage");
                var storedBits = Bytes((T[])chunk.Colors);
                C4b[] actual = null;
                Assert.DoesNotThrow(() => actual = chunk.ColorsAsC4b, context);
                if (actual != null)
                {
                    Assert.That(actual, Is.EqualTo(expected), context + ": exact ordered RGBA bytes");
                    var again = chunk.ColorsAsC4b;
                    Assert.That(again, Is.EqualTo(expected), context + ": repeated conversion");
                    if (typeof(T) == typeof(C4b))
                    {
                        Assert.That(actual, Is.SameAs(chunk.Colors), context + ": C4b identity");
                        Assert.That(again, Is.SameAs(actual), context + ": C4b repeated identity");
                    }
                    else
                    {
                        Assert.That(actual, Is.Not.SameAs(chunk.Colors), context + ": converted array ownership");
                        Assert.That(again, Is.Not.SameAs(actual), context + ": independent conversions");
                        Array.Clear(actual, 0, actual.Length);
                        Assert.That(again, Is.EqualTo(expected), context + ": mutation does not affect other conversion");
                    }
                }
                else Assert.That(actual, Is.Not.Null, context + ": present colors must not return null");
                Assert.That(Bytes(input), Is.EqualTo(inputBits), context + ": original input bits unchanged");
                Assert.That(Bytes(list.ToArray()), Is.EqualTo(inputBits), context + ": list input unchanged");
                Assert.That(Bytes((T[])chunk.Colors), Is.EqualTo(storedBits), context + ": stored color bits unchanged");
                Assert.That(enumerations, Is.EqualTo(route == "iterator" ? 1 : 0), context + ": single materialization");
                Assert.That(original.Data, Is.SameAs(originalData), context + ": original data unchanged");
                Assert.That(original.Colors, Is.SameAs(oldColors), context + ": original colors reference");
                Assert.That(Bytes(oldColors), Is.EqualTo(oldBits), context + ": original colors unchanged");
                Assert.That(Bytes((V3d[])positions), Is.EqualTo(positionBits), context + ": position bits unchanged");
                Assert.That(Bytes(intensities), Is.EqualTo(intensityBits), context + ": unrelated values unchanged");
            }
        }

        [Test]
        public void FloatColorsUseNormalizedConstructorsAcrossAllInputRoutes()
        {
            var rgb = new[] { new C3f(0, 0, 0), new C3f(1, 1, 1), new C3f(.25f, .5f, .75f),
                new C3f(-.25f, 1.25f, .1f), new C3f(.2f, .3f, .4f),
                new C3f(float.MinValue, float.MaxValue, float.Epsilon),
                new C3f(-0.0f, MathF.BitDecrement(1), MathF.BitIncrement(0)) };
            var rgba = new[] { new C4f(0, .5f, 1, 0), new C4f(1, .25f, .75f, 1), new C4f(.25f, .5f, .75f, .5f),
                new C4f(-.25f, 1.25f, .1f, -.5f), new C4f(.2f, .3f, .4f, 1.5f),
                new C4f(float.MinValue, float.MaxValue, float.Epsilon, float.MinValue),
                new C4f(-0.0f, MathF.BitDecrement(1), MathF.BitIncrement(0), float.MaxValue) };
            var rgbExpected = new[] { new C4b(0, 0, 0, 255), new C4b(255, 255, 255, 255), new C4b(63, 127, 191, 255),
                new C4b(0, 255, 25, 255), new C4b(51, 76, 102, 255), new C4b(0, 255, 0, 255), new C4b(0, 255, 0, 255) };
            var rgbaExpected = new[] { new C4b(0, 127, 255, 0), new C4b(255, 63, 191, 255), new C4b(63, 127, 191, 127),
                new C4b(0, 255, 25, 0), new C4b(51, 76, 102, 255), new C4b(0, 255, 0, 0), new C4b(0, 255, 0, 255) };
            Assert.That(rgb.Select(c => new C4b(c)), Is.EqualTo(rgbExpected), "literal RGB expectations also match existing constructors");
            Assert.That(rgba.Select(c => new C4b(c)), Is.EqualTo(rgbaExpected), "literal RGBA expectations also match existing constructors");
            Assert.Multiple(() => {
                Routes(Array.Empty<C3f>(), Array.Empty<C4b>(), GenericChunk.Defs.Colors3f);
                Routes(new[] { new C3f(0, .5f, 1) }, new[] { new C4b(0, 127, 255, 255) }, GenericChunk.Defs.Colors3f);
                Routes(rgb, rgbExpected, GenericChunk.Defs.Colors3f);
                Routes(Array.Empty<C4f>(), Array.Empty<C4b>(), GenericChunk.Defs.Colors4f);
                Routes(new[] { new C4f(0, .5f, 1, .25f) }, new[] { new C4b(0, 127, 255, 63) }, GenericChunk.Defs.Colors4f);
                Routes(rgba, rgbaExpected, GenericChunk.Defs.Colors4f);
            });
        }

        [Test]
        public void ByteAndMissingColorsKeepIdentityAlphaAndOwnership()
        {
            Routes(Array.Empty<C3b>(), Array.Empty<C4b>(), GenericChunk.Defs.Colors3b);
            Routes(new[] { new C3b(0, 127, 255) }, new[] { new C4b(0, 127, 255, 255) }, GenericChunk.Defs.Colors3b);
            Routes(new[] { new C3b(255, 0, 1), new C3b(1, 2, 3), new C3b(0, 0, 0) },
                new[] { new C4b(255, 0, 1, 255), new C4b(1, 2, 3, 255), new C4b(0, 0, 0, 255) }, GenericChunk.Defs.Colors3b);
            foreach (var input in new[] { Array.Empty<C4b>(), new[] { new C4b(0, 127, 255, 0) },
                new[] { new C4b(255, 0, 1, 1), new C4b(1, 2, 3, 127), new C4b(0, 0, 0, 255) } })
                Routes(input, input.ToArray(), GenericChunk.Defs.Colors4b);
            foreach (var count in new[] { 0, 1, 8 })
            {
                var chunk = Points(count);
                Assert.That(chunk.HasColors, Is.False, $"missing count={count}");
                Assert.That(chunk.ColorsDef, Is.Null, $"missing definition count={count}");
                Assert.That(chunk.Colors, Is.Null, $"missing storage count={count}");
                Assert.That(chunk.ColorsAsC4b, Is.Null, $"missing conversion count={count}");
            }
            Assert.That(GenericChunk.Empty.ColorsAsC4b, Is.Null, "shared empty chunk");
        }

        [Test]
        public void ConstructorAndUpdateValidationDoNotBroadenInputContracts()
        {
            var original = Points(1);
            var colors = new (Durable.Def Def, Array Array, object List, object Iterator)[] {
                (GenericChunk.Defs.Colors3f, new C3f[1], new List<C3f> { default }, Enumerable.Repeat(default(C3f), 1)),
                (GenericChunk.Defs.Colors4f, new C4f[1], new List<C4f> { default }, Enumerable.Repeat(default(C4f), 1)),
                (GenericChunk.Defs.Colors3b, new C3b[1], new List<C3b> { default }, Enumerable.Repeat(default(C3b), 1)),
                (GenericChunk.Defs.Colors4b, new C4b[1], new List<C4b> { default }, Enumerable.Repeat(default(C4b), 1))
            };
            foreach (var c in colors)
            {
                foreach (var nonArray in new[] { c.List, c.Iterator })
                    Assert.That(Assert.Throws<ArgumentException>(() => new GenericChunk(original.Data.Add(c.Def, nonArray))).Message,
                        Does.Contain("must be array"), $"constructor {c.Def.Name}/{nonArray.GetType()}");
                foreach (var count in new[] { 0, 2 })
                {
                    var wrongLength = Array.CreateInstance(c.Array.GetType().GetElementType(), count);
                    Assert.Throws<ArgumentException>(() => new GenericChunk(original.Data.Add(c.Def, wrongLength)), $"constructor length={count}/{c.Def.Name}");
                    Assert.Throws<ArgumentException>(() => original.WithColors(wrongLength), $"update length={count}/{c.Def.Name}");
                }
            }
            Assert.Throws<ArgumentException>(() => original.WithColors(new C3d[1]));
            Assert.That(original.HasColors, Is.False, "failed updates do not modify the receiver");
            Assert.That(original.Count, Is.EqualTo(1));
        }
    }
}
