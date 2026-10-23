using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class KdFlatEmptyTests
    {
        private sealed class EmptyProbe : IIntersectableObjectSet
        {
            public int Calls;
            public int ObjectCount => 0;
            public Box3d ObjectBoundingBox(int index = -1) => Box3d.Unit;
            public bool ClosestPoint(int[] indices, int first, int count, V3d point, Func<IIntersectableObjectSet,int,bool> objects, Func<IIntersectableObjectSet,int,int,ObjectClosestPoint,bool> points, ref ObjectClosestPoint result)
            { Calls++; Assert.That(count, Is.Zero); return false; }
            public bool ObjectsIntersectRay(int[] indices, int first, int count, FastRay3d ray, Func<IIntersectableObjectSet,int,bool> objects, Func<IIntersectableObjectSet,int,int,RayHit3d,bool> hits, double min, double max, ref ObjectRayHit result)
            { Calls++; Assert.That(count, Is.Zero); return false; }
            public bool ObjectIntersectsBox(int index, Box3d box) => throw new Exception("Empty object box callback");
            public bool ObjectIsInsideBox(int index, Box3d box) => throw new Exception("Empty object containment callback");
            public void ObjectHitInfo(ObjectRayHit hit, ref ObjectHitInfo info) => throw new Exception("Empty hit callback");
        }
        private static T Field<T>(KdFlat flat, string name) => (T)typeof(KdFlat).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(flat);
        private sealed class Snapshot
        {
            private readonly object[] values;
            private readonly Array[] copies;
            private static readonly string[] Names = { "m_rootAxis","m_rootIndex","m_splitArray","m_nodeArray","m_indexArray" };
            public Snapshot(KdFlat flat) { values=Names.Select(n => Field<object>(flat,n)).ToArray(); copies=values.Select(v => v is Array a ? (Array)a.Clone() : null).ToArray(); }
            public void Check(KdFlat flat, string context)
            {
                for (int i=0;i<Names.Length;i++)
                {
                    var actual=Field<object>(flat,Names[i]);
                    if (values[i] is Array) { Assert.That(actual, Is.SameAs(values[i]),context+": "+Names[i]); Assert.That(actual, Is.EqualTo(copies[i]),context+": unchanged contents "+Names[i]); }
                    else Assert.That(actual, Is.EqualTo(values[i]),context+": "+Names[i]);
                }
            }
        }
        private static ObjectClosestPoint Closest(IIntersectableObjectSet owner, double bound = 10) => new ObjectClosestPoint {
            Distance=bound,DistanceSquared=bound*bound,Point=new V3d(-7,-8,-9),Coord=new V2d(.125,.75),SetObject=new SetObject(owner,91),ObjectStack=new List<SetObject>{new SetObject(owner,42),new SetObject(owner,99)} };
        private static ObjectRayHit Hit(IIntersectableObjectSet owner) => new ObjectRayHit(17) { RayHit=new RayHit3d {T=17,Point=new V3d(-7,-8,-9),Coord=new V2d(.125,.75),Part=23,BackSide=true}, SetObject=new SetObject(owner,91),Tag=new object(),ObjectStack=new List<SetObject>{new SetObject(owner,42),new SetObject(owner,99)} };
        private static void Stack(List<SetObject> actual, List<SetObject> expected, string context)
        {
            if (expected==null) { Assert.That(actual, Is.Null,context); return; }
            Assert.That(actual, Is.Not.Null,context); if (actual==null) return;
            Assert.That(actual.Count, Is.EqualTo(expected.Count),context);
            for (int i=0;i<Math.Min(actual.Count,expected.Count);i++) { Assert.That(actual[i].Index, Is.EqualTo(expected[i].Index),context); Assert.That(actual[i].Set, Is.SameAs(expected[i].Set),context); }
        }
        private static void SameClosest(ObjectClosestPoint actual, ObjectClosestPoint expected, string context, bool seed = false)
        {
            Assert.That(actual.Distance, Is.EqualTo(expected.Distance),context); Assert.That(actual.DistanceSquared, Is.EqualTo(expected.DistanceSquared),context);
            Assert.That(actual.Point, Is.EqualTo(expected.Point),context); Assert.That(actual.Coord, Is.EqualTo(expected.Coord),context);
            Assert.That(actual.SetObject.Index, Is.EqualTo(expected.SetObject.Index),context); Assert.That(actual.SetObject.Set, Is.SameAs(expected.SetObject.Set),context);
            Stack(actual.ObjectStack,expected.ObjectStack,context);
            if (seed) { Assert.That(actual.ObjectStack, Is.SameAs(expected.ObjectStack),context); Assert.That(actual.ObjectStack.Select(s=>s.Index), Is.EqualTo(new[]{42,99}),context); Assert.That(actual.ObjectStack.All(s=>ReferenceEquals(s.Set,expected.SetObject.Set)), Is.True,context); }
        }
        private static void SameHit(ObjectRayHit actual, ObjectRayHit expected, string context, bool seed = false)
        {
            Assert.That(actual.RayHit, Is.EqualTo(expected.RayHit),context); Assert.That(actual.SetObject.Index, Is.EqualTo(expected.SetObject.Index),context);
            Assert.That(actual.SetObject.Set, Is.SameAs(expected.SetObject.Set),context); Assert.That(actual.Tag, Is.SameAs(expected.Tag),context); Stack(actual.ObjectStack,expected.ObjectStack,context);
            if (seed) { Assert.That(actual.ObjectStack, Is.SameAs(expected.ObjectStack),context); Assert.That(actual.ObjectStack.Select(s=>s.Index), Is.EqualTo(new[]{42,99}),context); Assert.That(actual.ObjectStack.All(s=>ReferenceEquals(s.Set,expected.SetObject.Set)), Is.True,context); }
        }
        private static IEnumerable<(string Name,KdFlat Flat)> EmptyFlats()
        {
            yield return ("default",new KdFlat());
            foreach (var index in new[]{-1,0,7,int.MaxValue})
            foreach (var buffers in new[]{0,1,2})
                yield return ($"encoded index={index}, buffers={buffers}",new KdFlat(0,index,buffers==0?null:buffers==1?Array.Empty<double>():new[]{3.25,7.5},buffers==0?null:buffers==1?Array.Empty<int>():new[]{1,71,72,73,74,75},buffers==0?null:buffers==1?Array.Empty<int>():new[]{91,92,93}));
            yield return ("empty leaf",(KdFlat)new KdLeaf(Array.Empty<int>()).Flatten());
            foreach (var first in new[]{0,2,5}) yield return ($"empty slice first={first}",(KdFlat)new KdLeafSlice(new[]{int.MaxValue,99,98,97,int.MinValue},first,0).Flatten());
        }
        private static IEnumerable<int> Slice(int[] indices, int first, int count) => indices.Skip(first).Take(count);

        [Test]
        public void EmptyPublicTreesKeepMissResultsAndCallbacksAcrossFlattening()
        {
            Assert.Multiple(() => {
                foreach (var kind in new[]{0,1,2})
                foreach (var optimize in new[]{false,true})
                {
                    var probe=new EmptyProbe(); IIntersectableObjectSet set=kind==0 ? new EmptyIntersectableObjectSet() : kind==1 ? new IntersectableTriangleSet(Array.Empty<V3f>()) : probe;
                    var tree=new KdIntersectionTree(set,KdIntersectionTree.BuildFlags.Raytracing|KdIntersectionTree.BuildFlags.NoMultithreading|(optimize?KdIntersectionTree.BuildFlags.EmptySpaceOptimization:0));
                    for (int stage=0;stage<3;stage++)
                    {
                        if (stage>0) tree.Flatten(); var calls=probe.Calls;
                        foreach (var bound in new[]{0.0,1,10,double.PositiveInfinity})
                        foreach (var point in new[]{new V3d(.25,.25,.5),new V3d(-2,3,4)})
                        {
                            var context=$"public kind={kind}, optimize={optimize}, stage={stage}, bound={bound}, point={point}";
                            var seed=Closest(set,bound);var result=seed;var found=true;
                            Assert.DoesNotThrow(() => found=tree.ClosestPoint(point,(_,_)=>throw new Exception("Empty object predicate"),(_,_,_,_)=>throw new Exception("Empty point predicate"),ref result),context);
                            Assert.That(found, Is.False,context); SameClosest(result,seed,context,true);
                            var plain=seed; Assert.DoesNotThrow(() => found=tree.ClosestPoint(point,ref plain),context); Assert.That(found, Is.False,context); SameClosest(plain,seed,context,true);
                        }
                        foreach (var origin in new[]{new V3d(-1,.25,.25),new V3d(.25,.25,.25),new V3d(-1,2,2)})
                        {
                            var context=$"public ray kind={kind}, optimize={optimize}, stage={stage}, origin={origin}"; var seed=Hit(set);var result=seed;var found=true;
                            Assert.DoesNotThrow(() => found=tree.Intersect(new FastRay3d(origin,V3d.XAxis),(_,_)=>throw new Exception("Empty object predicate"),(_,_,_,_)=>throw new Exception("Empty hit predicate"),0,10,ref result),context);
                            Assert.That(found, Is.False,context);SameHit(result,seed,context,true);
                            var plain=seed;Assert.DoesNotThrow(() => found=tree.Intersect(new FastRay3d(origin,V3d.XAxis),0,10,ref plain),context);Assert.That(found, Is.False,context);SameHit(plain,seed,context,true);
                        }
                        foreach (var box in new[]{Box3d.Unit,new Box3d(new V3d(-2),new V3d(2)),new Box3d(new V3d(5),new V3d(6))})
                        {
                            var context=$"public box kind={kind}, optimize={optimize}, stage={stage}, box={box}";
                            Assert.DoesNotThrow(() => Assert.That(tree.ObjectsIntersectingBox(box), Is.Empty,context),context);
                            Assert.DoesNotThrow(() => Assert.That(tree.ObjectsInsideBox(box), Is.Empty,context),context);
                            if (stage>0) Assert.DoesNotThrow(() => Assert.That(tree.NodesIntersectingBox(box), Is.Null,context),context);
                        }
                        Assert.That(tree.Count, Is.Zero);
                        if (stage>0) { Assert.That(tree.Tree, Is.TypeOf<KdFlat>()); Assert.That(probe.Calls, Is.EqualTo(calls),"flat empty must not dispatch even zero-length object slices"); }
                    }
                }
            });
        }

        [Test]
        public void ZeroRootsAndFlattenedEmptySlicesSkipBuffersStateChangesAndSelectors()
        {
            Assert.Multiple(() => {
                foreach (var fixture in EmptyFlats())
                {
                    var flat=fixture.Flat;var snapshot=new Snapshot(flat);var probe=new EmptyProbe();
                    foreach (var explicitAxis in new[]{false,true})
                    foreach (var nullSet in new[]{false,true})
                    {
                        var context=fixture.Name+$", explicit={explicitAxis}, nullSet={nullSet}";
                        var seed=Closest(probe);var query=new KdQueryPoint {Point=new V3d(1,2,3),ObjectSet=nullSet?null:probe,ClosestPoint=seed,ObjectFilter=(_,_)=>throw new Exception("Empty filter"),PointFilter=(_,_,_,_)=>throw new Exception("Empty point filter")};
                        var saved=query;var min=new V3d(-3,-4,-5);var max=new V3d(6,7,8);var found=true;
                        Assert.DoesNotThrow(() => found=explicitAxis?flat.ClosestPoint(0,int.MinValue,ref query,ref min,ref max):flat.ClosestPoint(ref query,ref min,ref max),context);
                        Assert.That(found, Is.False,context);SameClosest(query.ClosestPoint,seed,context,true);
                        Assert.That(min, Is.EqualTo(new V3d(-3,-4,-5)),context);Assert.That(max, Is.EqualTo(new V3d(6,7,8)),context);
                        Assert.That(query.Point, Is.EqualTo(saved.Point),context);Assert.That(query.ObjectSet, Is.SameAs(saved.ObjectSet),context);Assert.That(query.ObjectFilter, Is.SameAs(saved.ObjectFilter),context);Assert.That(query.PointFilter, Is.SameAs(saved.PointFilter),context);
                        var hit=Hit(probe);var ray=new KdIntersectionRay {FastRay=new FastRay3d(new V3d(1,2,3),V3d.ZAxis),Hit=hit,ObjectSet=nullSet?null:probe,ObjectFilter=(_,_)=>throw new Exception("Empty filter"),HitFilter=(_,_,_,_)=>throw new Exception("Empty hit filter")};
                        var savedRay=ray;var a=new TP3d {T=-2,P=new V3d(-1,-2,-3)};var b=new TP3d {T=11,P=new V3d(4,5,6)};
                        Assert.DoesNotThrow(() => found=explicitAxis?flat.Intersect(0,int.MaxValue,ref ray,ref a,ref b):flat.Intersect(ref ray,ref a,ref b),context);
                        Assert.That(found, Is.False,context);SameHit(ray.Hit,hit,context,true);
                        Assert.That(a.T, Is.EqualTo(-2),context);Assert.That(a.P, Is.EqualTo(new V3d(-1,-2,-3)),context);Assert.That(b.T, Is.EqualTo(11),context);Assert.That(b.P, Is.EqualTo(new V3d(4,5,6)),context);
                        Assert.That(ray.FastRay, Is.EqualTo(savedRay.FastRay),context);Assert.That(ray.ObjectSet, Is.SameAs(savedRay.ObjectSet),context);Assert.That(ray.ObjectFilter, Is.SameAs(savedRay.ObjectFilter),context);Assert.That(ray.HitFilter, Is.SameAs(savedRay.HitFilter),context);
                        foreach (var flags in new[]{Box.Flags.None,Box.Flags.MinX,Box.Flags.All})
                        {
                            var box=Box3d.Unit;KdNode node=null;Assert.DoesNotThrow(() => node=explicitAxis?flat.Intersect(0,int.MinValue,ref box,flags):flat.Intersect(ref box,flags),context+$", flags={flags}");
                            Assert.That(node, Is.Null,context+$", flags={flags}");Assert.That(box, Is.EqualTo(Box3d.Unit),context);
                        }
                        for (int selectors=0;selectors<4;selectors++)
                        {
                            int called=0;Func<int[],int,int,IEnumerable<int>> reject=(_,_,_)=>{called++;throw new Exception("Empty selector");};
                            var interior=(selectors&1)==0?null:reject;var border=(selectors&2)==0?null:reject;
                            for (int repeat=0;repeat<2;repeat++) Assert.DoesNotThrow(() => Assert.That(explicitAxis?flat.Indices(0,int.MinValue,interior,border):flat.Indices(interior,border), Is.Empty,context),context);
                            Assert.That(called, Is.Zero,context);
                        }
                    }
                    Assert.That(probe.Calls, Is.Zero,fixture.Name);snapshot.Check(flat,fixture.Name);
                }
            });
        }

        [Test]
        public void EmptyCopiesAndCompositionsConsumeNoSourceOrDestinationStorage()
        {
            Assert.Multiple(() => {
                foreach (var fixture in EmptyFlats())
                {
                    var snapshot=new Snapshot(fixture.Flat);KdNode current=fixture.Flat;
                    for (int repeat=0;repeat<2;repeat++)
                    {
                        KdNode copy=null;Assert.DoesNotThrow(() => copy=current.Flatten(),fixture.Name);
                        Assert.That(copy, Is.TypeOf<KdFlat>(),fixture.Name);if (copy is not KdFlat flat) break;
                        Assert.That(Field<short>(flat,"m_rootAxis"), Is.Zero,fixture.Name);Assert.That(Field<int>(flat,"m_rootIndex"), Is.EqualTo(-1),fixture.Name);
                        Assert.That(Field<double[]>(flat,"m_splitArray"), Is.Empty,fixture.Name);Assert.That(Field<int[]>(flat,"m_nodeArray"), Is.Empty,fixture.Name);Assert.That(Field<int[]>(flat,"m_indexArray"), Is.Empty,fixture.Name);
                        Assert.That(flat.Indices(null,null), Is.Empty,fixture.Name);current=copy;
                    }
                    // Native records surround the empty flat source: nonempty flat copying is deliberately not used here.
                    var native=new KdInner(0,.5,new KdLeaf(new[]{10}),new KdInner(1,1.5,fixture.Flat,new KdLeaf(new[]{20,21})));
                    KdNode composition=null;Assert.DoesNotThrow(() => composition=native.Flatten(),fixture.Name+": composition");
                    Assert.That(composition, Is.TypeOf<KdFlat>(),fixture.Name+": composition");
                    if (composition is KdFlat result)
                    {
                        Assert.That(Field<short>(result,"m_rootAxis"), Is.EqualTo(-3),fixture.Name);Assert.That(Field<int>(result,"m_rootIndex"), Is.Zero,fixture.Name);
                        Assert.That(Field<double[]>(result,"m_splitArray"), Is.EqualTo(new[]{.5,1.5}),fixture.Name);
                        Assert.That(Field<int[]>(result,"m_nodeArray"), Is.EqualTo(new[]{unchecked((int)0xfffe0001),0,1,2<<16,-1,1}),fixture.Name);
                        Assert.That(Field<int[]>(result,"m_indexArray"), Is.EqualTo(new[]{10,20,21}),fixture.Name);Assert.That(result.Indices(Slice,null), Is.EqualTo(new[]{10,20,21}),fixture.Name);
                    }
                    snapshot.Check(fixture.Flat,fixture.Name);
                }
            });
        }

        [Test]
        public void PopulatedLeavesAndSplitTreesRetainQueriesEnumerationAndSafeCopies()
        {
            foreach (var count in new[]{1,4,32})
            foreach (var indexed in new[]{false,true})
            foreach (var optimize in new[]{false,true})
            {
                var positions=Enumerable.Range(0,count).SelectMany(i=>new[]{new V3f(4*i,0,3),new V3f(4*i+2,0,3),new V3f(4*i,2,3)}).ToArray();
                var set=indexed?new IntersectableTriangleSet(Enumerable.Range(0,positions.Length).ToArray(),positions):new IntersectableTriangleSet(positions);
                var tree=new KdIntersectionTree(set,KdIntersectionTree.BuildFlags.Raytracing|KdIntersectionTree.BuildFlags.NoMultithreading|(optimize?KdIntersectionTree.BuildFlags.EmptySpaceOptimization:0));
                var native=tree.Tree;var context=$"count={count}, indexed={indexed}, optimize={optimize}";
                Assert.That(native.Leafs().Count(), count==32?Is.GreaterThan(1):Is.EqualTo(1),context+": genuine subdivision");var candidates=native.Indices(Slice,null).ToArray();
                var closest=new List<(int Id,double Bound,bool Found,ObjectClosestPoint Value)>();var hits=new List<(int Id,double Bound,bool Found,ObjectRayHit Value)>();
                foreach (var id in new[]{0,count/2,count-1}.Distinct())
                foreach (var bound in new[]{2.0,Math.BitIncrement(2),3,10})
                {
                    var c=Closest(set,bound);var cf=tree.ClosestPoint(new V3d(4*id+.25,.25,1),ref c);closest.Add((id,bound,cf,c));
                    var h=Hit(set);var hf=tree.Intersect(new FastRay3d(new V3d(4*id+.25,.25,0),V3d.ZAxis),0,bound,ref h);hits.Add((id,bound,hf,h));
                }
                tree.Flatten();Assert.That(tree.Tree, Is.TypeOf<KdFlat>(),context);var flat=(KdFlat)tree.Tree;var snapshot=new Snapshot(flat);
                Assert.That(flat.Indices(Slice,null), Is.EqualTo(candidates),context);Assert.That(flat.Indices(null,Slice), Is.EqualTo(candidates),context);
                foreach (var row in closest)
                {
                    var c=Closest(set,row.Bound);var found=tree.ClosestPoint(new V3d(4*row.Id+.25,.25,1),ref c);var label=context+$", closest id={row.Id}, bound={row.Bound}";
                    Assert.That(found, Is.EqualTo(row.Found),label);Assert.That(found, Is.EqualTo(row.Bound>2),label);SameClosest(c,row.Value,label);
                    if (found) { Assert.That(double.IsFinite(c.Distance)&&double.IsFinite(c.DistanceSquared)&&double.IsFinite(c.Point.X)&&double.IsFinite(c.Point.Y)&&double.IsFinite(c.Point.Z), Is.True,label);Assert.That(c.Distance, Is.EqualTo(2),label);Assert.That(c.Point, Is.EqualTo(new V3d(4*row.Id+.25,.25,3)),label);Assert.That(c.SetObject.Index, Is.EqualTo(row.Id),label); }
                }
                foreach (var row in hits)
                {
                    var h=Hit(set);h.Tag=row.Value.Tag;var found=tree.Intersect(new FastRay3d(new V3d(4*row.Id+.25,.25,0),V3d.ZAxis),0,row.Bound,ref h);var label=context+$", ray id={row.Id}, bound={row.Bound}";
                    Assert.That(found, Is.EqualTo(row.Found),label);Assert.That(found, Is.EqualTo(row.Bound>3),label);SameHit(h,row.Value,label);
                    if (found) { Assert.That(double.IsFinite(h.RayHit.T)&&double.IsFinite(h.RayHit.Point.X)&&double.IsFinite(h.RayHit.Point.Y)&&double.IsFinite(h.RayHit.Point.Z), Is.True,label);Assert.That(h.RayHit.T, Is.EqualTo(3),label);Assert.That(h.RayHit.Point, Is.EqualTo(new V3d(4*row.Id+.25,.25,3)),label);Assert.That(h.SetObject.Index, Is.EqualTo(row.Id),label); }
                }
                foreach (var flags in new[]{Box.Flags.None,Box.Flags.All})
                {
                    var box=new Box3d(new V3d(-1,-1,2),new V3d(4*count,3,4));var reference=native.Intersect(ref box,flags);var pruned=flat.Intersect(ref box,flags);
                    Assert.That(pruned.Indices(Slice,null), Is.EqualTo(reference.Indices(Slice,null)),context+": nonempty box traversal");
                }
                snapshot.Check(flat,context);
                var leaf=(KdFlat)new KdLeaf(Enumerable.Range(0,count).ToArray()).Flatten();var leafSnapshot=new Snapshot(leaf);var copied=leaf.Flatten();
                Assert.That(copied.Indices(Slice,null), Is.EqualTo(Enumerable.Range(0,count)),context+": compact flat-leaf copy");leafSnapshot.Check(leaf,context);
            }
        }
    }
}
