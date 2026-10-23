using System;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyMeshVertexAppendTests
    {
        private static readonly Symbol Weight = "append-weight", Label = "append-label", Orphan = "append-orphan";
        private static V3d Position(int i) => new V3d(i+.25,2*i+1,-i-.5);
        private static V3d Normal(int i) => new V3d(i+1,-i,2);
        private static C4f Color(int i) => new C4f((i%5)*.25f,.5f,1,1);
        private static void Attributes(PolyMesh mesh)
        {
            var active = mesh.VertexCount; var capacity = mesh.PositionArray?.Length ?? 0; var attributes = mesh.VertexAttributes;
            attributes[PolyMesh.Property.Normals] = new V3d[capacity]; attributes[PolyMesh.Property.Colors] = new C4f[capacity];
            attributes[Weight] = new double[capacity]; attributes[Label] = new int[capacity];
            attributes[-Label] = new[] { "red", "green", "blue" }; attributes[-Orphan] = new[] { new V3d(7,8,9), new V3d(-1,-2,-3) };
            mesh.VertexAttributes = attributes;
            if (mesh.VertexCount != active) mesh.VertexCount = active;
            for (int i=0;i<active;i++) StoreAttributes(mesh,i);
        }
        private static void StoreAttributes(PolyMesh mesh, int i)
        {
            mesh.NormalArray[i] = Normal(i); mesh.ColorArray[i] = Color(i);
            ((double[])mesh.VertexAttributes[Weight])[i] = i+.5; ((int[])mesh.VertexAttributes[Label])[i] = i%3;
        }
        private static PolyMesh Empty(int route, bool attributes, out PolyMesh parent)
        {
            var mesh = new PolyMesh(); parent = null;
            switch (route)
            {
                case 0: case 2: mesh.PositionArray = new V3d[0]; break;
                case 1: mesh.VertexCount = 0; break;
                case 3: break; // Copy default/null position storage through existing copy initialization.
                case 4: mesh.VertexAttributes = new SymbolDict<Array> { { PolyMesh.Property.Positions, new V3d[0] } }; break;
                default: throw new ArgumentOutOfRangeException(nameof(route));
            }
            if (attributes) Attributes(mesh);
            if (route==2 || route==3) { parent=mesh; mesh=route==2 ? new PolyMesh(mesh) : mesh.Copy(); }
            return mesh;
        }
        private static void CheckValues(PolyMesh mesh, int count, bool attributes, string context)
        {
            Assert.That(mesh.VertexCount, Is.EqualTo(count),context + ": active count");
            Assert.That(mesh.Get<int>(PolyMesh.Property.VertexCount), Is.EqualTo(count),context + ": stored count");
            Assert.That(mesh.VertexAttributes[PolyMesh.Property.Positions], Is.SameAs(mesh.PositionArray),context + ": position cache");
            for (int i=0;i<count;i++)
            {
                Assert.That(mesh.PositionArray[i], Is.EqualTo(Position(i)),context + $": retained position {i}");
                Assert.That(mesh.GetVertex(i).Position, Is.EqualTo(Position(i)),context);
                var p=mesh.PositionArray[i]; Assert.That(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z), Is.True,context);
                if (attributes)
                {
                    Assert.That(mesh.NormalArray[i], Is.EqualTo(Normal(i)),context + $": retained normal {i}");
                    Assert.That(mesh.ColorArray[i], Is.EqualTo(Color(i)),context + $": retained color {i}");
                    Assert.That(((double[])mesh.VertexAttributes[Weight])[i], Is.EqualTo(i+.5),context);
                    Assert.That(((int[])mesh.VertexAttributes[Label])[i], Is.EqualTo(i%3),context);
                }
            }
        }
        private static void Append(PolyMesh mesh, int total, bool attributes, string context)
        {
            var dictionary=mesh.VertexAttributes; var face=mesh.FaceAttributes; var corner=mesh.FaceVertexAttributes; var edge=mesh.EdgeAttributes; var instance=mesh.InstanceAttributes;
            var palette=attributes ? mesh.VertexAttributes[-Label] : null; var orphan=attributes ? mesh.VertexAttributes[-Orphan] : null;
            for (int i=mesh.VertexCount;i<total;i++)
            {
                var wasNull=mesh.PositionArray==null; var oldCapacity=mesh.PositionArray?.Length ?? 0;
                var oldArrays=mesh.VertexAttributes.Where(k => k.Key.IsPositive).ToArray(); var label=context+$", append={i}, capacity={oldCapacity}";
                var index=-1; Assert.DoesNotThrow(() => index=mesh.AddVertex(Position(i)),label);
                Assert.That(index, Is.EqualTo(i),label + ": returned index");
                if (index<0) return; // Original failures must not prevent the remaining setup variants from running.
                var expectedCapacity=wasNull ? 16 : i+1>oldCapacity ? (oldCapacity==0 ? 16 : 2*oldCapacity) : oldCapacity;
                Assert.That(mesh.VertexCapacity, Is.EqualTo(expectedCapacity),label + ": null/zero/doubling policy");
                foreach (var kv in oldArrays)
                {
                    Assert.That(mesh.VertexAttributes[kv.Key].Length, Is.EqualTo(mesh.VertexCapacity),label + ": positive-key alignment " + kv.Key);
                    if (oldCapacity==expectedCapacity) Assert.That(mesh.VertexAttributes[kv.Key], Is.SameAs(kv.Value),label);
                    else Assert.That(mesh.VertexAttributes[kv.Key], Is.Not.SameAs(kv.Value),label);
                }
                if (attributes)
                {
                    Assert.That(mesh.NormalArray, Is.SameAs(mesh.VertexAttributes[PolyMesh.Property.Normals]),label);
                    Assert.That(mesh.ColorArray, Is.SameAs(mesh.VertexAttributes[PolyMesh.Property.Colors]),label);
                    Assert.That(mesh.NormalArray[i], Is.EqualTo(V3d.Zero),label + ": unwritten normal default");
                    Assert.That(mesh.ColorArray[i], Is.EqualTo(default(C4f)),label + ": unwritten color default");
                    Assert.That(((double[])mesh.VertexAttributes[Weight])[i], Is.Zero,label); Assert.That(((int[])mesh.VertexAttributes[Label])[i], Is.Zero,label);
                    StoreAttributes(mesh,i);
                    Assert.That(mesh.VertexAttributes[-Label], Is.SameAs(palette),label + ": indexed pool identity");
                    Assert.That(palette, Is.EqualTo(new[] { "red", "green", "blue" }),label + ": indexed pool values");
                    Assert.That(mesh.VertexAttributes[-Orphan], Is.SameAs(orphan),label + ": orphan pool identity");
                    Assert.That(orphan, Is.EqualTo(new[] { new V3d(7,8,9),new V3d(-1,-2,-3) }),label);
                }
                CheckValues(mesh,i+1,attributes,label);
                Assert.That(mesh.VertexAttributes, Is.SameAs(dictionary),label + ": existing resize dictionary");
                Assert.That(mesh.FaceAttributes, Is.SameAs(face),label); Assert.That(mesh.FaceVertexAttributes, Is.SameAs(corner),label);
                Assert.That(mesh.EdgeAttributes, Is.SameAs(edge),label); Assert.That(mesh.InstanceAttributes, Is.SameAs(instance),label);
                Assert.That(mesh.FaceCount, Is.Zero,label); Assert.That(mesh.VertexIndexCount, Is.Zero,label);
            }
        }

        [Test]
        public void InitializedEmptyStorageGrowsAlignedAttributesAndRetainsValuePools()
        {
            Assert.Multiple(() => {
                foreach (var route in Enumerable.Range(0,5))
                foreach (var attributes in new[] { false,true })
                {
                    var context=$"route={route}, attributes={attributes}"; var mesh=Empty(route,attributes,out var parent);
                    Assert.That(mesh.VertexCount, Is.Zero,context); Assert.That(mesh.VertexCapacity, Is.Zero,context);
                    if (parent!=null) Assert.That(mesh.VertexAttributes, Is.SameAs(parent.VertexAttributes),context + ": existing shallow sharing");
                    Append(mesh,65,attributes,context);
                    if (parent!=null) Assert.That(mesh.VertexAttributes, Is.SameAs(parent.VertexAttributes),context + ": sharing not repaired");
                }
            });
        }

        [Test]
        public void DefaultPreallocatedAndNonemptyAppendsRetainExistingGrowthRules()
        {
            Append(new PolyMesh(),65,false,"default null-buffer initialization");
            foreach (var capacity in new[] { 1,7,16 })
            foreach (var attributes in new[] { false,true })
            {
                var mesh=new PolyMesh(0,0,capacity); if (attributes) Attributes(mesh);
                Append(mesh,65,attributes,$"preallocated={capacity}, attributes={attributes}");
            }
            foreach (var attributes in new[] { false,true })
            {
                var mesh=new PolyMesh { PositionArray=Enumerable.Range(0,3).Select(Position).ToArray() }; if (attributes) Attributes(mesh);
                Append(mesh,65,attributes,$"nonempty prefix, attributes={attributes}");
            }
            foreach (var colors in new[] { false,true })
            {
                var mesh=new PolyMesh { PositionArray=Array.Empty<V3d>() };
                for (int i=0;i<65;i++) Assert.That(colors ? mesh.AddVertex(Position(i),Normal(i),Color(i)) : mesh.AddVertex(Position(i),Normal(i)), Is.EqualTo(i));
                Assert.That(mesh.VertexCount, Is.EqualTo(65)); Assert.That(mesh.VertexCapacity, Is.EqualTo(128));
                Assert.That(mesh.PositionArray.Take(65), Is.EqualTo(Enumerable.Range(0,65).Select(Position)));
                Assert.That(mesh.NormalArray.Take(65), Is.EqualTo(Enumerable.Range(0,65).Select(Normal)));
                if (colors) Assert.That(mesh.ColorArray.Take(65), Is.EqualTo(Enumerable.Range(0,65).Select(Color)));
            }
        }

        [Test]
        public void InitializedEmptyMeshesCanBuildAQueryableTriangle()
        {
            Assert.Multiple(() => {
                foreach (var route in Enumerable.Range(0,5))
                foreach (var attributes in new[] { false,true })
                {
                    var context=$"triangle route={route}, attributes={attributes}"; var mesh=Empty(route,attributes,out _);
                    var positions=new[] { new V3d(10,20,30),new V3d(14,20,30),new V3d(10,26,30) }; var completed=true;
                    for (int i=0;i<3;i++) { var index=-1; Assert.DoesNotThrow(() => index=mesh.AddVertex(positions[i]),context); Assert.That(index, Is.EqualTo(i),context); if (index<0) { completed=false; break; } }
                    if (!completed) continue;
                    Assert.That(mesh.AddFace(0,1,2), Is.Zero,context); Assert.That(mesh.VertexCount, Is.EqualTo(3),context);
                    Assert.That(mesh.FaceCount, Is.EqualTo(1),context); Assert.That(mesh.VertexIndexCount, Is.EqualTo(3),context);
                    Assert.That(mesh.PositionArray.Take(3), Is.EqualTo(positions),context);
                    Assert.That(mesh.BoundingBox3d, Is.EqualTo(new Box3d(new V3d(10,20,30),new V3d(14,26,30))),context);
                    var found=mesh.Intersects(new Ray3d(new V3d(10.5,20.5,28),V3d.ZAxis),out var hit);
                    Assert.That(found, Is.True,context);
                    if (found) { Assert.That(double.IsFinite(hit.T) && double.IsFinite(hit.Point.X) && double.IsFinite(hit.Point.Y) && double.IsFinite(hit.Point.Z), Is.True,context); Assert.That(hit.T, Is.EqualTo(2),context); Assert.That(hit.Point, Is.EqualTo(new V3d(10.5,20.5,30)),context); }
                    mesh.BuildTopology(); Assert.That(mesh.HasTopology, Is.True,context); Assert.That(mesh.EdgeCount, Is.EqualTo(3),context); Assert.That(mesh.BorderEdgeCount, Is.EqualTo(3),context);
                    Assert.That(mesh.GetFace(0).Edges.Select(e => e.FromVertexIndex), Is.EquivalentTo(new[] { 0,1,2 }),context);
                }
            });
        }
    }
}
