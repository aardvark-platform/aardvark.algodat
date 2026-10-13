using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyLineTests
    {
        private static PolyLine[] Owners()
        {
            var positions = new[] { V3d.Zero, V3d.IOO, V3d.OIO, V3d.OOI };
            // Identical geometry, even shared arrays, does not confer element identity.
            return new[] { new PolyLine { PositionArray = positions }, new PolyLine { PositionArray = positions } };
        }

        [Test]
        public void BoxedLineEqualityMatchesTypedOwnerAndIndexIdentity()
        {
            var owners = Owners();
            var lines = owners.SelectMany(owner => owner.Lines).Append(default).ToArray();
            Assert.Multiple(() =>
            {
                for (var i = 0; i < lines.Length; i++)
                for (var j = 0; j < lines.Length; j++)
                {
                    var left = lines[i]; var right = lines[j];
                    object boxedLeft = left; object boxedRight = right;
                    var expected = ReferenceEquals(left.PolyLine, right.PolyLine) && left.Index == right.Index;
                    var context = $"pair=({i},{j}), indices=({left.Index},{right.Index})";
                    Assert.That(left == right, Is.EqualTo(expected), context + ": equality operator");
                    Assert.That(left != right, Is.EqualTo(!expected), context + ": inequality operator");
                    Assert.That(left.Equals(right), Is.EqualTo(expected), context + ": typed equality");
                    Assert.That(((IEquatable<PolyLine.Line>)left).Equals(right), Is.EqualTo(expected), context + ": interface equality");
                    Assert.That(left.Equals(boxedRight), Is.EqualTo(expected), context + ": struct object overload");
                    Assert.That(boxedLeft.Equals(boxedRight), Is.EqualTo(expected), context + ": boxed receiver");
                    Assert.That(object.Equals(boxedLeft, boxedRight), Is.EqualTo(expected), context + ": static object equality");
                    Assert.That(boxedRight.Equals(boxedLeft), Is.EqualTo(expected), context + ": boxed symmetry");
                    if (left.PolyLine != null)
                    {
                        Assert.That(boxedLeft.GetHashCode(), Is.EqualTo(left.GetHashCode()), context + ": boxing preserves hash");
                        if (expected) Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()), context + ": equal hashes");
                    }
                }
            });
        }

        [Test]
        public void UnequalAndHeterogeneousLineComparisonsAreSafe()
        {
            var owners = Owners();
            Assert.Multiple(() =>
            {
                foreach (var index in Enumerable.Range(0, owners[0].LineCount))
                {
                    var line = new PolyLine.Line(owners[0], index);
                    object boxed = line;
                    var cases = new (string Name, object Value)[]
                    {
                        ("different owner", new PolyLine.Line(owners[1], index)),
                        ("different index", new PolyLine.Line(owners[0], (index + 1) % owners[0].LineCount)),
                        ("same-owner vertex", new PolyLine.Vertex(owners[0], index)),
                        ("other-owner vertex", new PolyLine.Vertex(owners[1], index)),
                        ("null", null), ("unrelated object", new object()), ("integer", index), ("string", "line")
                    };
                    foreach (var item in cases)
                    {
                        var context = $"index={index}, comparison={item.Name}";
                        Assert.DoesNotThrow(() =>
                        {
                            Assert.That(line.Equals(item.Value), Is.False, context + ": struct receiver");
                            Assert.That(boxed.Equals(item.Value), Is.False, context + ": boxed receiver");
                            Assert.That(object.Equals(boxed, item.Value), Is.False, context + ": static equality");
                            Assert.That(object.Equals(item.Value, boxed), Is.False, context + ": reverse equality");
                        }, context);
                    }
                }
            });
        }

        [Test]
        public void ObjectKeyedCollectionsRetrieveAndDeduplicateLineIdentity()
        {
            var owners = Owners();
            var lines = owners.SelectMany(owner => owner.Lines).ToArray();
            var objectSet = new HashSet<object>(); var typedSet = new HashSet<PolyLine.Line>();
            var objectMap = new Dictionary<object, int>(); var typedMap = new Dictionary<PolyLine.Line, int>();
            var objectTable = new System.Collections.Hashtable();
            for (var i = 0; i < lines.Length; i++)
            {
                objectSet.Add(lines[i]); typedSet.Add(lines[i]);
                objectMap.Add(lines[i], i); typedMap.Add(lines[i], i); objectTable.Add(lines[i], i);
            }
            Assert.Multiple(() =>
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    var key = new PolyLine.Line(lines[i].PolyLine, lines[i].Index);
                    object boxedKey = key;
                    var context = $"key={i}, index={key.Index}";
                    Assert.That(objectSet.Contains(boxedKey), Is.True, context + ": object membership");
                    Assert.That(typedSet.Contains(key), Is.True, context + ": typed membership");
                    Assert.That(objectMap.TryGetValue(boxedKey, out var value), Is.True, context + ": object retrieval");
                    Assert.That(value, Is.EqualTo(i), context + ": object value");
                    Assert.That(typedMap[key], Is.EqualTo(i), context + ": typed retrieval");
                    Assert.That(objectTable[boxedKey], Is.EqualTo(i), context + ": non-generic retrieval");
                    Assert.That(objectSet.Add(boxedKey), Is.False, context + ": object deduplication");
                    Assert.That(typedSet.Add(key), Is.False, context + ": typed deduplication");
                    objectMap[boxedKey] = 100 + i; typedMap[key] = 100 + i; objectTable[boxedKey] = 100 + i;
                    Assert.That(objectMap[lines[i]], Is.EqualTo(100 + i), context + ": object update");
                    Assert.That(typedMap[lines[i]], Is.EqualTo(100 + i), context + ": typed update");
                    Assert.That(objectTable[lines[i]], Is.EqualTo(100 + i), context + ": non-generic update");
                    var vertex = new PolyLine.Vertex(key.PolyLine, key.Index);
                    Assert.DoesNotThrow(() =>
                    {
                        Assert.That(objectSet.Contains(vertex), Is.False, context + ": vertex is not a line key");
                        Assert.That(objectMap.ContainsKey(vertex), Is.False, context + ": vertex is not a map key");
                        Assert.That(objectTable.ContainsKey(vertex), Is.False, context + ": vertex is not a table key");
                    }, context + ": heterogeneous lookup");
                }
                Assert.That(objectSet.Count, Is.EqualTo(lines.Length), "object set retains distinct owners and indices");
                Assert.That(typedSet.Count, Is.EqualTo(lines.Length), "typed set control");
                Assert.That(objectMap.Count, Is.EqualTo(lines.Length), "object map deduplicates updates");
                Assert.That(typedMap.Count, Is.EqualTo(lines.Length), "typed map control");
                Assert.That(objectTable.Count, Is.EqualTo(lines.Length), "non-generic map deduplicates updates");
            });
        }

        [Test]
        public void PreboxedLineComparisonsDoNotAllocate()
        {
            var owners = Owners();
            object left = new PolyLine.Line(owners[0], 0);
            var rights = new object[] { new PolyLine.Line(owners[0], 0), new PolyLine.Line(owners[0], 1), new PolyLine.Line(owners[1], 0) };
            foreach (var right in rights)
            {
                var count = 0;
                for (var i = 0; i < 4096; i++) if (left.Equals(right)) count++;
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var i = 0; i < 16384; i++) if (left.Equals(right)) count++;
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                GC.KeepAlive(count);
                Assert.That(allocated, Is.Zero, $"preboxed comparison ownerSame={ReferenceEquals(((PolyLine.Line)left).PolyLine, ((PolyLine.Line)right).PolyLine)}, rightIndex={((PolyLine.Line)right).Index}");
            }
        }
    }
}
