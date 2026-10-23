using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class KdFlatFlattenTests
    {
        private static int Pack(short left, short right) => (ushort)left | ((int)(ushort)right << 16);
        private static T Field<T>(KdFlat flat, string name) => (T)typeof(KdFlat).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(flat);
        private static int[] Candidates(KdNode node) => node.Indices((a,f,c) => a.Skip(f).Take(c), null).ToArray();
        private static Action Snapshot(KdFlat source, string context)
        {
            var axis = Field<short>(source,"m_rootAxis"); var root = Field<int>(source,"m_rootIndex");
            var splits = Field<double[]>(source,"m_splitArray"); var nodes = Field<int[]>(source,"m_nodeArray"); var indices = Field<int[]>(source,"m_indexArray");
            var splitBits = splits.Select(BitConverter.DoubleToInt64Bits).ToArray(); var nodeValues = (int[])nodes.Clone(); var indexValues = (int[])indices.Clone();
            return () => {
                Assert.That(Field<short>(source,"m_rootAxis"), Is.EqualTo(axis), context + ": source root axis");
                Assert.That(Field<int>(source,"m_rootIndex"), Is.EqualTo(root), context + ": source root offset");
                Assert.That(Field<double[]>(source,"m_splitArray"), Is.SameAs(splits), context);
                Assert.That(Field<int[]>(source,"m_nodeArray"), Is.SameAs(nodes), context);
                Assert.That(Field<int[]>(source,"m_indexArray"), Is.SameAs(indices), context);
                Assert.That(splits.Select(BitConverter.DoubleToInt64Bits), Is.EqualTo(splitBits), context + ": source split bits");
                Assert.That(nodes, Is.EqualTo(nodeValues), context + ": source node records");
                Assert.That(indices, Is.EqualTo(indexValues), context + ": source candidate storage");
            };
        }
        private static void Layout(KdFlat flat, short axis, double[] splits, int[] nodes, int[] indices, string context)
        {
            Assert.That(Field<short>(flat,"m_rootAxis"), Is.EqualTo(axis), context + ": encoded axis");
            Assert.That(Field<int>(flat,"m_rootIndex"), Is.Zero, context + ": rebased root");
            Assert.That(Field<double[]>(flat,"m_splitArray").Select(BitConverter.DoubleToInt64Bits), Is.EqualTo(splits.Select(BitConverter.DoubleToInt64Bits)), context + ": split order/bits");
            Assert.That(Field<int[]>(flat,"m_nodeArray"), Is.EqualTo(nodes), context + ": packed axes and child offsets");
            Assert.That(Field<int[]>(flat,"m_indexArray"), Is.EqualTo(indices), context + ": compact candidate storage");
            Assert.That(Candidates(flat), Is.EqualTo(indices), context + ": ordered candidates");
        }
        private static KdFlat Split(byte dimension, int rootPadding = 0, int leafPadding = 0, int oneSided = 0)
        {
            short next = (short)((dimension+1)%3-3), last = (short)((dimension+2)%3-3);
            var splits = Enumerable.Repeat(-999.0, rootPadding + (oneSided == 0 ? 3 : 1)).ToArray();
            var nodes = Enumerable.Repeat(-999, splits.Length*3).ToArray();
            var ids = Enumerable.Repeat(-999,leafPadding+6).ToArray();
            new[] { 41, 7, 42, 7 }.CopyTo(ids,leafPadding);
            splits[rootPadding] = -2.25;
            if (oneSided == 0)
            {
                splits[rootPadding+1] = 1.5; splits[rootPadding+2] = 4.75;
                new[] { Pack(next,last),rootPadding+1,rootPadding+2, Pack(1,1),leafPadding,leafPadding+1, Pack(1,1),leafPadding+2,leafPadding+3 }.CopyTo(nodes,rootPadding*3);
            }
            else new[] { Pack((short)(oneSided==1?2:0),(short)(oneSided==2?2:0)),oneSided==1?leafPadding:-1,oneSided==2?leafPadding:-1 }.CopyTo(nodes,rootPadding*3);
            return new KdFlat((short)(dimension-3),rootPadding,splits,nodes,ids);
        }

        [Test]
        public void ExistingSplitTreesReflattenWithExactEncodingAndImmutableSources()
        {
            Assert.Multiple(() => {
                foreach (byte axis in new byte[] { 0,1,2 })
                foreach (var rootPadding in new[] { 0,1 })
                foreach (var leafPadding in new[] { 0,2 })
                foreach (var oneSided in new[] { 0,1,2 })
                {
                    var context = $"axis={axis}, rootPadding={rootPadding}, leafPadding={leafPadding}, oneSided={oneSided}";
                    var source = Split(axis,rootPadding,leafPadding,oneSided); var unchanged = Snapshot(source,context);
                    var ids = oneSided==0 ? new[] { 41,7,42,7 } : new[] { 41,7 };
                    var splits = oneSided==0 ? new[] { -2.25,1.5,4.75 } : new[] { -2.25 };
                    var nodes = oneSided==0 ? new[] { Pack((short)((axis+1)%3-3),(short)((axis+2)%3-3)),1,2, Pack(1,1),0,1, Pack(1,1),2,3 }
                        : new[] { Pack((short)(oneSided==1?2:0),(short)(oneSided==2?2:0)),oneSided==1?0:-1,oneSided==2?0:-1 };
                    KdNode result = null; Assert.DoesNotThrow(() => result=source.Flatten(),context); unchanged();
                    Assert.That(result, Is.TypeOf<KdFlat>(),context); Assert.That(result, Is.Not.SameAs(source),context);
                    if (result is KdFlat flat)
                    {
                        Layout(flat,(short)(axis-3),splits,nodes,ids,context);
                        Assert.That(Field<int[]>(flat,"m_indexArray"), Is.Not.SameAs(Field<int[]>(source,"m_indexArray")),context);
                        var flatState = Snapshot(flat,context + ": result"); var second = (KdFlat)flat.Flatten();
                        Layout(second,(short)(axis-3),splits,nodes,ids,context + ": repeat"); flatState(); unchanged();
                    }
                }
            });
        }

        private static KdNode Leaf(int[] ids, bool sliced) => sliced ? new KdLeafSlice(Enumerable.Concat(new[] { -99,-98 },Enumerable.Concat(ids,new[] { -97 })).ToArray(),2,ids.Length) : new KdLeaf(ids);
        [Test]
        public void CompositionsPreserveSlicesAndSharedSourcesAtDestinationOffsets()
        {
            Assert.Multiple(() => {
                foreach (var offset in new[] { 0,1,5 })
                foreach (var prefixLength in new[] { 1,3 })
                foreach (var suffixLength in new[] { 1,2 })
                foreach (var sliced in new[] { false,true })
                {
                    var context = $"leafOffset={offset}, prefix={prefixLength}, suffix={suffixLength}, sliced={sliced}";
                    var storage = Enumerable.Repeat(-999,offset+4).ToArray(); storage[offset]=20; storage[offset+1]=21;
                    var source = new KdFlat(2,offset,Array.Empty<double>(),Array.Empty<int>(),storage); var unchanged = Snapshot(source,context);
                    var prefix = Enumerable.Range(100,prefixLength).ToArray(); var suffix = Enumerable.Range(200,suffixLength).ToArray();
                    var tree = new KdInner(0,0,Leaf(prefix,sliced),new KdInner(1,1,source,Leaf(suffix,!sliced)));
                    var expected = Enumerable.Concat(prefix,Enumerable.Concat(new[] { 20,21 },suffix)).ToArray();
                    KdNode flat = null; Assert.DoesNotThrow(() => flat=tree.Flatten(),context); unchanged();
                    Assert.That(flat, Is.TypeOf<KdFlat>(),context);
                    if (flat is KdFlat) Assert.That(Candidates(flat), Is.EqualTo(expected),context + ": no overlap or gap");
                    Assert.That(Candidates(tree), Is.EqualTo(expected),context + ": source composition");
                }
                foreach (var depth in new[] { 0,1,3 })
                foreach (var copies in new[] { 1,2 })
                {
                    var context = $"shared split: precedingDepth={depth}, copies={copies}";
                    var shared = Split(0); var unchanged = Snapshot(shared,context); var original = Candidates(shared);
                    KdNode source = new KdInner(0,0,new KdLeaf(new[] { 80 }),shared);
                    var expected = Enumerable.Concat(new[] { 80 },original).ToArray();
                    if (copies==2) { source=new KdInner(1,10,source,shared); expected=Enumerable.Concat(expected,original).ToArray(); }
                    for (int i=0;i<depth;i++) { source=new KdInner((byte)(i%3),20+i,new KdLeafSlice(new[] { -1,90+i,-1 },1,1),source); expected=Enumerable.Concat(new[] { 90+i },expected).ToArray(); }
                    KdNode flat = null; Assert.DoesNotThrow(() => flat=source.Flatten(),context);
                    // Check even after an exception: the old copier could corrupt a later source node before failing.
                    unchanged(); Assert.That(Candidates(shared), Is.EqualTo(original),context + ": shared candidates survive");
                    Assert.That(flat, Is.TypeOf<KdFlat>(),context);
                    if (flat is KdFlat) Assert.That(Candidates(flat), Is.EqualTo(expected),context + ": destination sequence");
                }
            });
        }

        private static IntersectableTriangleSet Triangles(int count, bool indexed)
        {
            var positions = Enumerable.Range(0,count).SelectMany(i => new[] { new V3f(4*i,0,1),new V3f(4*i+2,0,1),new V3f(4*i,2,1) }).ToArray();
            return indexed ? new IntersectableTriangleSet(Enumerable.Range(0,positions.Length).Select(i => positions.Length-1-i).ToArray(),positions.Reverse().ToArray()) : new IntersectableTriangleSet(positions);
        }
        private const KdIntersectionTree.BuildFlags Flags = KdIntersectionTree.BuildFlags.Raytracing | KdIntersectionTree.BuildFlags.NoMultithreading;
        private static ObjectClosestPoint Closest(IIntersectableObjectSet set, double bound) => new ObjectClosestPoint { Distance=bound,DistanceSquared=bound*bound,Point=new V3d(-7,-8,-9),Coord=new V2d(.125,.75),SetObject=new SetObject(set,99),ObjectStack=new List<SetObject> { new SetObject(set,42) } };
        private static ObjectRayHit Hit(IIntersectableObjectSet set, double bound) => new ObjectRayHit(bound) { RayHit=new RayHit3d(bound) { Point=new V3d(-7,-8,-9),Coord=new V2d(.125,.75),Part=17,BackSide=true },SetObject=new SetObject(set,99),ObjectStack=new List<SetObject> { new SetObject(set,42) },Tag=new object() };
        private static void Queries(KdIntersectionTree before, KdIntersectionTree after, IntersectableTriangleSet set, int id, string context)
        {
            foreach (var bound in new[] { 1.0,2.0,10.0 })
            {
                var a=Closest(set,bound); var b=a; var seed=a; var query=new V3d(4*id+.25,.25,3);
                var foundA=before.ClosestPoint(query,ref a); var foundB=after.ClosestPoint(query,ref b);
                Assert.That(foundB, Is.EqualTo(foundA),context + $": closest bound={bound}"); Assert.That(foundA, Is.EqualTo(bound>2),context + ": independent cutoff");
                Assert.That(b.Distance, Is.EqualTo(a.Distance),context); Assert.That(b.DistanceSquared, Is.EqualTo(a.DistanceSquared),context); Assert.That(b.Point, Is.EqualTo(a.Point),context);
                Assert.That(b.Coord, Is.EqualTo(a.Coord),context); Assert.That(b.SetObject.Set, Is.SameAs(a.SetObject.Set),context); Assert.That(b.SetObject.Index, Is.EqualTo(a.SetObject.Index),context);
                Assert.That(b.ObjectStack.Select(s => s.Index), Is.EqualTo(a.ObjectStack.Select(s => s.Index)),context);
                if (foundB)
                {
                    Assert.That(double.IsFinite(b.Distance) && double.IsFinite(b.DistanceSquared) && double.IsFinite(b.Point.X) && double.IsFinite(b.Point.Y) && double.IsFinite(b.Point.Z), Is.True,context);
                    Assert.That(b.Distance, Is.EqualTo(2),context); Assert.That(b.Point, Is.EqualTo(new V3d(4*id+.25,.25,1)),context); Assert.That(b.SetObject.Index, Is.EqualTo(id),context);
                }
                else Assert.That(b, Is.EqualTo(seed),context + ": seeded miss");
            }
            foreach (var y in new[] { .25,9.0 })
            foreach (var bound in new[] { 2.0,10.0 })
            {
                var ray=new FastRay3d(new V3d(4*id+.25,y,-2),V3d.ZAxis); var a=Hit(set,bound); var b=a; var seed=a;
                var foundA=before.Intersect(ray,0,bound,ref a); var foundB=after.Intersect(ray,0,bound,ref b);
                Assert.That(foundB, Is.EqualTo(foundA),context + ": ray result"); Assert.That(foundA, Is.EqualTo(y<1 && bound>3),context + ": independent ray");
                Assert.That(b.RayHit, Is.EqualTo(a.RayHit),context); Assert.That(b.SetObject.Set, Is.SameAs(a.SetObject.Set),context); Assert.That(b.SetObject.Index, Is.EqualTo(a.SetObject.Index),context); Assert.That(b.Tag, Is.SameAs(a.Tag),context);
                Assert.That(b.ObjectStack?.Select(s => s.Index), Is.EqualTo(a.ObjectStack?.Select(s => s.Index)),context);
                if (foundB)
                {
                    Assert.That(double.IsFinite(b.RayHit.T) && double.IsFinite(b.RayHit.Point.X) && double.IsFinite(b.RayHit.Point.Y) && double.IsFinite(b.RayHit.Point.Z), Is.True,context);
                    Assert.That(b.RayHit.T, Is.EqualTo(3),context); Assert.That(b.SetObject.Index, Is.EqualTo(id),context);
                }
                else Assert.That(b, Is.EqualTo(seed),context + ": seeded ray miss");
            }
        }

        [Test]
        public void BoxSubtreesOfSubdividedTrianglesKeepCandidatesAndQueryResults()
        {
            Assert.Multiple(() => {
                foreach (var count in new[] { 32,96 })
                foreach (var indexed in new[] { false,true })
                foreach (var partial in new[] { false,true })
                {
                    var context=$"triangles={count}, indexed={indexed}, partial={partial}"; var set=Triangles(count,indexed); var positions=(V3f[])set.Positions.Clone(); var indices=set.Indices==null?null:(int[])set.Indices.Clone();
                    var tree=new KdIntersectionTree(set,Flags); Assert.That(tree.Tree.Leafs().Count(), Is.GreaterThan(1),context + ": genuinely split fixture");
                    var ordinary=Candidates(tree.Tree); tree.Flatten(); Assert.That(Candidates(tree.Tree), Is.EqualTo(ordinary),context + ": first flatten control");
                    var state=Snapshot((KdFlat)tree.Tree,context);
                    var box=partial ? new Box3d(new V3d(count,-5,-5),new V3d(3*count+2,5,5)) : tree.BoundingBox3d;
                    if (!partial) box.EnlargeBy(10);
                    var node=tree.NodesIntersectingBox(box); Assert.That(node, Is.Not.Null,context); var expected=Candidates(node); Assert.That(expected.Length, Is.GreaterThan(0),context);
                    var before=new KdIntersectionTree(set,Flags) { Tree=node }; KdNode flattened=null;
                    Assert.DoesNotThrow(() => flattened=node.Flatten(),context); state(); Assert.That(flattened, Is.TypeOf<KdFlat>(),context);
                    if (flattened is not KdFlat) continue;
                    Assert.That(Candidates(flattened), Is.EqualTo(expected),context + ": exact candidate order");
                    var after=new KdIntersectionTree(set,Flags) { Tree=flattened };
                    // Box pruning returns conservative leaf candidates, not an exact geometry selection.
                    var distinct=expected.Distinct().Where(id => box.Contains(new V3d(4*id+.25,.25,1))).ToArray();
                    Assert.That(distinct.Length, Is.GreaterThan(0),context + ": interior query samples");
                    foreach (var id in new[] { distinct[0],distinct[distinct.Length/2],distinct[distinct.Length-1] }) Queries(before,after,set,id,context);
                    state(); Assert.That(Candidates(node), Is.EqualTo(expected),context + ": original subtree still usable");
                    Assert.That(set.Positions, Is.EqualTo(positions),context + ": geometry unchanged"); Assert.That(set.Indices, Is.EqualTo(indices),context);
                }
            });
        }

        [Test]
        public void OrdinaryFlatteningAndFlatLeafControlsKeepOrderAndSlices()
        {
            foreach (byte axis in new byte[] { 0,1,2 })
            foreach (var sliced in new[] { false,true })
            {
                var ids=new[] { 8,3,8 }; var source=new KdInner(axis,1.25,Leaf(ids,sliced),new KdLeaf(new[] { 5,2 }));
                var flat=(KdFlat)source.Flatten(); Layout(flat,(short)(axis-3),new[] { 1.25 },new[] { Pack(3,2),0,3 },new[] { 8,3,8,5,2 },$"ordinary axis={axis}, slice={sliced}");
                Assert.That(Candidates(source), Is.EqualTo(new[] { 8,3,8,5,2 }));
            }
            foreach (var offset in new[] { 0,4 })
            {
                var storage=Enumerable.Concat(Enumerable.Repeat(-99,offset),new[] { 8,3,8,-98 }).ToArray();
                var leaf=new KdFlat(3,offset,Array.Empty<double>(),Array.Empty<int>(),storage); var unchanged=Snapshot(leaf,"flat leaf");
                Layout((KdFlat)leaf.Flatten(),3,Array.Empty<double>(),Array.Empty<int>(),new[] { 8,3,8 },$"flat leaf offset={offset}"); unchanged();
            }
        }
    }
}
