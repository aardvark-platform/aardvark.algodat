using System;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyMeshShortFaceQueryTests
    {
        private sealed class Fixture
        {
            public PolyMesh Mesh;
            public int[] Kinds;
            public int Tri => Array.IndexOf(Kinds,3);
            public int Quad => Array.IndexOf(Kinds,4);
        }
        private static Fixture Make(int[] kinds, int prefix = 0, int spare = 0, int columns = 1)
        {
            var positions=new List<V3d>();var via=Enumerable.Repeat(int.MaxValue,prefix).ToList();var first=new List<int>{prefix};var counts=new List<int>();
            for (int column=0;column<columns;column++)
            foreach (int count in kinds)
            {
                double x=4*column;V3d[] points=count==3?new[]{new V3d(x,0,3),new V3d(x+2,0,3),new V3d(x,2,3)}:count==4?new[]{new V3d(x,0,7),new V3d(x+2,0,7),new V3d(x+2,2,7),new V3d(x,2,7)}:new[]{new V3d(x+1.5,-2,1),new V3d(x+1.75,-2,1)}.Take(count).ToArray();
                foreach (var p in points) {via.Add(positions.Count);positions.Add(p);}first.Add(via.Count);counts.Add(count);
            }
            var active=via.Count;via.AddRange(Enumerable.Repeat(int.MinValue,spare));
            var mesh=new PolyMesh{PositionArray=positions.ToArray(),VertexIndexArray=via.ToArray(),FirstIndexArray=first.ToArray()};mesh.VertexIndexCount=active;
            return new Fixture{Mesh=mesh,Kinds=counts.ToArray()};
        }
        private sealed class Snapshot
        {
            private readonly PolyMesh mesh;private readonly V3d[] p,pc;private readonly int[] v,vc,f,fc;private readonly int[] counts;
            public Snapshot(PolyMesh m){mesh=m;p=m.PositionArray;pc=p?.ToArray();v=m.VertexIndexArray;vc=v?.ToArray();f=m.FirstIndexArray;fc=f?.ToArray();counts=new[]{m.VertexCount,m.VertexIndexCount,m.FaceCount};}
            public void Check(string label){Assert.That(mesh.PositionArray,Is.SameAs(p),label);Assert.That(mesh.PositionArray,Is.EqualTo(pc),label);Assert.That(mesh.VertexIndexArray,Is.SameAs(v),label);Assert.That(mesh.VertexIndexArray,Is.EqualTo(vc),label);Assert.That(mesh.FirstIndexArray,Is.SameAs(f),label);Assert.That(mesh.FirstIndexArray,Is.EqualTo(fc),label);Assert.That(new[]{mesh.VertexCount,mesh.VertexIndexCount,mesh.FaceCount},Is.EqualTo(counts),label);}
        }
        private static ObjectClosestPoint Closest(PolyMesh mesh,double bound) => new ObjectClosestPoint{Distance=bound,DistanceSquared=bound*bound,Point=new V3d(-7,-8,-9),Coord=new V2d(.125,.75),SetObject=new SetObject(mesh,91),ObjectStack=new List<SetObject>{new SetObject(mesh,42),new SetObject(mesh,99)}};
        private static ObjectRayHit Hit(PolyMesh mesh) => new ObjectRayHit(20){RayHit=new RayHit3d{T=20,Point=new V3d(-7,-8,-9),Coord=new V2d(.125,.75),Part=23,BackSide=true},SetObject=new SetObject(mesh,91),Tag=new object(),ObjectStack=new List<SetObject>{new SetObject(mesh,42),new SetObject(mesh,99)}};
        private static void Stack(List<SetObject> actual,List<SetObject> expected,IIntersectableObjectSet owner,string label)
        {
            Assert.That(actual,Is.SameAs(expected),label);if(actual==null)return;
            Assert.That(actual.Select(s=>s.Index),Is.EqualTo(new[]{42,99}),label);Assert.That(actual.All(s=>ReferenceEquals(s.Set,owner)),Is.True,label);
        }
        private static void Same(ObjectClosestPoint a,ObjectClosestPoint b,string label)
        {
            Assert.That(a.Distance,Is.EqualTo(b.Distance),label);Assert.That(a.DistanceSquared,Is.EqualTo(b.DistanceSquared),label);Assert.That(a.Point,Is.EqualTo(b.Point),label);Assert.That(a.Coord,Is.EqualTo(b.Coord),label);Assert.That(a.SetObject.Index,Is.EqualTo(b.SetObject.Index),label);Assert.That(a.SetObject.Set,Is.SameAs(b.SetObject.Set),label);Stack(a.ObjectStack,b.ObjectStack,b.SetObject.Set,label);
        }
        private static void Same(ObjectRayHit a,ObjectRayHit b,string label)
        {
            Assert.That(a.RayHit,Is.EqualTo(b.RayHit),label);Assert.That(a.SetObject.Index,Is.EqualTo(b.SetObject.Index),label);Assert.That(a.SetObject.Set,Is.SameAs(b.SetObject.Set),label);Assert.That(a.Tag,Is.SameAs(b.Tag),label);Stack(a.ObjectStack,b.ObjectStack,b.SetObject.Set,label);
        }
        private static bool Finite(V3d p) => double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z);

        [Test]
        public void EmptyBoundsDoNotBorrowCornersAndFacadesForwardStoredPointBounds()
        {
            Assert.Multiple(() => {
                foreach (var prefix in new[]{0,2,5})
                foreach (var storage in new[]{0,1,2})
                {
                    var label=$"empty bounds prefix={prefix}, storage={storage}";
                    var mesh=new PolyMesh{FirstIndexArray=new[]{prefix,prefix},VertexIndexArray=storage==0?null:Enumerable.Repeat(storage==1?0:int.MaxValue,prefix+2).ToArray(),PositionArray=storage==0?null:new[]{new V3d(8,9,10)}};var snapshot=new Snapshot(mesh);
                    foreach (Func<Box3d> read in new Func<Box3d>[]{()=>mesh.BoundingBoxOfFace(0),()=>mesh.ObjectBoundingBox(0),()=>mesh.GetFace(0).BoundingBox3d,()=>new PolyMesh.Polygon(mesh,0).BoundingBox3d,()=>((IBoundingBox3d)mesh.GetFace(0)).BoundingBox3d})
                        Assert.DoesNotThrow(()=>Assert.That(read(),Is.EqualTo(Box3d.Invalid),label),label);
                    Assert.That(mesh.ObjectIsInsideBox(0,Box3d.Unit),Is.True,label+": containment remains unchanged");snapshot.Check(label);
                }
                foreach (var prefix in new[]{0,2})
                foreach (var spare in new[]{0,4})
                {
                    var fixture=Make(new[]{0,1,2,3,0,4,0},prefix,spare);var mesh=fixture.Mesh;var snapshot=new Snapshot(mesh);
                    for(int i=0;i<fixture.Kinds.Length;i++)
                    {
                        var points=mesh.VertexIndexArray.Skip(mesh.FirstIndexArray[i]).Take(fixture.Kinds[i]).Select(v=>mesh.PositionArray[v]).ToArray();var expected=points.Length==0?Box3d.Invalid:new Box3d(points[0]);foreach(var p in points.Skip(1))expected.ExtendBy(p);
                        var label=$"bounds prefix={prefix}, spare={spare}, face={i}, corners={fixture.Kinds[i]}";
                        Assert.DoesNotThrow(()=>Assert.That(mesh.BoundingBoxOfFace(i),Is.EqualTo(expected),label),label);Assert.DoesNotThrow(()=>Assert.That(mesh.GetFace(i).BoundingBox3d,Is.EqualTo(expected),label),label);Assert.DoesNotThrow(()=>Assert.That(new PolyMesh.Polygon(mesh,i).BoundingBox3d,Is.EqualTo(expected),label),label);
                    }
                    snapshot.Check("mixed bounds");
                }
            });
        }

        [Test]
        public void ShortSurfaceMissesReadNoVerticesAndPreserveFiltersSlicesAndCallerState()
        {
            Assert.Multiple(() => {
                foreach(int count in new[]{0,1,2})
                foreach(int prefix in new[]{0,3})
                foreach(int spare in new[]{0,1,4})
                foreach(int inaccessible in new[]{0,1})
                {
                    var label=$"short corners={count}, prefix={prefix}, spare={spare}, inaccessible={inaccessible}";
                    var indices=Enumerable.Repeat(int.MaxValue,prefix+count+spare).ToArray();if(inaccessible==0)for(int i=0;i<count;i++)indices[prefix+i]=i;
                    var mesh=new PolyMesh{PositionArray=inaccessible==1?null:Enumerable.Range(0,count).Select(i=>new V3d(i,0,3)).ToArray(),VertexIndexArray=indices,FirstIndexArray=new[]{prefix,prefix+count}};var snapshot=new Snapshot(mesh);
                    foreach(int slice in new[]{0,1,2})
                    {
                        var selected=slice==2?new[]{int.MaxValue,0,-1}:new[]{int.MaxValue,0,int.MinValue};int first=slice==2?0:1,length=slice==0?0:slice==1?1:3;var trace=new List<int>();
                        Func<IIntersectableObjectSet,int,bool> filter=(owner,index)=>{Assert.That(owner,Is.SameAs(mesh),label);trace.Add(index);return index==0;};
                        var seed=Closest(mesh,10);var c=seed;var found=true;
                        Assert.DoesNotThrow(()=>found=mesh.ClosestPoint(selected,first,length,V3d.Zero,filter,(_,_,_,_)=>throw new Exception("Short point predicate"),ref c),label);Assert.That(found,Is.False,label);Same(c,seed,label);
                        Assert.That(trace,Is.EqualTo(selected.Skip(first).Take(length)),label+": original-index predicate order");trace.Clear();
                        var hit=Hit(mesh);var h=hit;Assert.DoesNotThrow(()=>found=mesh.ObjectsIntersectRay(selected,first,length,new FastRay3d(V3d.Zero,V3d.ZAxis),filter,(_,_,_,_)=>throw new Exception("Short hit predicate"),0,10,ref h),label);Assert.That(found,Is.False,label);Same(h,hit,label);Assert.That(trace,Is.EqualTo(selected.Skip(first).Take(length)),label);
                    }
                    foreach(var box in new[]{Box3d.Unit,new Box3d(new V3d(-10),new V3d(10)),Box3d.Invalid})Assert.DoesNotThrow(()=>Assert.That(mesh.ObjectIntersectsBox(0,box),Is.False,label),label);
                    // Null filters still skip the face; rejecting invalid IDs precedes face-array access.
                    foreach(var selected in new[]{new[]{0},new[]{int.MaxValue,-1}})
                    {
                        var seed=Closest(mesh,1);var c=seed;var hseed=Hit(mesh);var h=hseed;Func<IIntersectableObjectSet,int,bool> filter=selected.Length==1?null:(_,_)=>false;var found=true;
                        Assert.DoesNotThrow(()=>found=mesh.ClosestPoint(selected,0,selected.Length,V3d.Zero,filter,null,ref c),label);Assert.That(found,Is.False,label);Same(c,seed,label);
                        Assert.DoesNotThrow(()=>found=mesh.ObjectsIntersectRay(selected,0,selected.Length,new FastRay3d(V3d.Zero,V3d.ZAxis),filter,null,0,10,ref h),label);Assert.That(found,Is.False,label);Same(h,hseed,label);
                    }
                    var error=new InvalidOperationException("object predicate");var ecSeed=Closest(mesh,10);var ec=ecSeed;var ehSeed=Hit(mesh);var eh=ehSeed;
                    Assert.That(Assert.Throws<InvalidOperationException>(()=>mesh.ClosestPoint(new[]{0},0,1,V3d.Zero,(_,_)=>throw error,null,ref ec)),Is.SameAs(error),label);Same(ec,ecSeed,label);
                    Assert.That(Assert.Throws<InvalidOperationException>(()=>mesh.ObjectsIntersectRay(new[]{0},0,1,new FastRay3d(V3d.Zero,V3d.ZAxis),(_,_)=>throw error,null,0,10,ref eh)),Is.SameAs(error),label);Same(eh,ehSeed,label);
                    snapshot.Check(label);
                }
            });
        }

        private static void Direct(Fixture fixture,int[] selected,int policy,double bound,double lower,int reject,string context)
        {
            var mesh=fixture.Mesh;var reference=Make(new[]{3,4}).Mesh;int[] map={fixture.Tri,fixture.Quad};
            bool Include(int id)=>policy==2?id==fixture.Quad:policy==3?fixture.Kinds[id]<3:policy==4?false:policy==5?id==fixture.Tri:true;
            var referenceIds=selected.Where(id=>fixture.Kinds[id]>=3).Select(id=>id==fixture.Tri?0:1).ToArray();var padded=Enumerable.Concat(Enumerable.Concat(new[]{int.MaxValue},selected),new[]{int.MinValue}).ToArray();var trace=new List<int>();
            Func<IIntersectableObjectSet,int,bool> filter=policy==0?null:(owner,id)=>{Assert.That(owner,Is.SameAs(mesh),context);trace.Add(id);return Include(id);};
            var point=new V3d(.25,1.25,0);var seed=Closest(mesh,bound);var c=seed;var expected=seed;int pointCalls=0;
            bool ef=reference.ClosestPoint(referenceIds,0,referenceIds.Length,point,(_,id)=>Include(map[id]),null,ref expected);if(ef)expected.SetObject=new SetObject(mesh,map[expected.SetObject.Index]);
            var found=!ef;Assert.DoesNotThrow(()=>found=mesh.ClosestPoint(padded,1,selected.Length,point,filter,(_,_,_,_)=>{pointCalls++;return false;},ref c),context);Assert.That(found,Is.EqualTo(ef),context);Same(c,expected,context);Assert.That(pointCalls,Is.Zero,context+": existing point-filter policy");Assert.That(trace,Is.EqualTo(policy==0?Array.Empty<int>():selected),context);trace.Clear();
            if(found){Assert.That(Finite(c.Point)&&double.IsFinite(c.Distance)&&double.IsFinite(c.DistanceSquared),Is.True,context);Assert.That(c.Distance,Is.EqualTo(c.SetObject.Index==fixture.Tri?3:7),context);Assert.That(c.Point,Is.EqualTo(new V3d(.25,1.25,c.Distance)),context);}
            var seedHit=Hit(mesh);var h=seedHit;var expectedHit=seedHit;var events=new List<(int,int,RayHit3d)>();var expectedEvents=new List<(int,int,RayHit3d)>();
            bool Reject(int id)=>reject==1||reject==2&&id==fixture.Tri;
            var fast=new FastRay3d(point,V3d.ZAxis);
            bool eh=reference.ObjectsIntersectRay(referenceIds,0,referenceIds.Length,fast,(_,id)=>Include(map[id]),(_,id,part,raw)=>{expectedEvents.Add((map[id],part,raw));return Reject(map[id]);},lower,bound,ref expectedHit);if(eh)expectedHit.SetObject=new SetObject(mesh,map[expectedHit.SetObject.Index]);
            Assert.DoesNotThrow(()=>found=mesh.ObjectsIntersectRay(padded,1,selected.Length,fast,filter,(owner,id,part,raw)=>{Assert.That(owner,Is.SameAs(mesh),context);Assert.That(fixture.Kinds[id],Is.GreaterThanOrEqualTo(3),context);Assert.That(Finite(raw.Point)&&double.IsFinite(raw.T),Is.True,context);events.Add((id,part,raw));return Reject(id);},lower,bound,ref h),context);
            Assert.That(found,Is.EqualTo(eh),context);Same(h,expectedHit,context);Assert.That(events,Is.EqualTo(expectedEvents),context+": complete callback payloads");Assert.That(trace,Is.EqualTo(policy==0?Array.Empty<int>():selected),context);
            if(found){Assert.That(Finite(h.RayHit.Point)&&double.IsFinite(h.RayHit.T),Is.True,context);Assert.That(h.RayHit.T,Is.EqualTo(h.SetObject.Index==fixture.Tri?3:7),context);Assert.That(h.RayHit.Part,Is.EqualTo(h.SetObject.Index==fixture.Tri?0:1),context);}
        }
        private static void Matrix(Fixture fixture,string context)
        {
            var all=Enumerable.Range(0,fixture.Kinds.Length).ToArray();var selections=new[]{all,all.Reverse().ToArray(),new[]{fixture.Quad,fixture.Tri,fixture.Quad},all.Where(i=>fixture.Kinds[i]<3).ToArray()};
            foreach(var selected in selections)
            foreach(int policy in new[]{0,1,2,3,4,5})
            foreach(double bound in new[]{2.0,4,8})Direct(fixture,selected,policy,bound,0,0,context+$", indices=[{string.Join(",",selected)}], policy={policy}, bound={bound}");
            foreach(double bound in new[]{3.0,Math.BitIncrement(3),7,Math.BitIncrement(7),10})
            foreach(int reject in new[]{0,1,2})Direct(fixture,all,1,bound,3.5,reject,context+$", cutoff={bound}, lower=3.5, reject={reject}");
            foreach(var box in new[]{new Box3d(new V3d(.2,.2,2.9),new V3d(.4,.4,3.1)),new Box3d(new V3d(.2,1.1,6.9),new V3d(.4,1.4,7.1)),new Box3d(new V3d(-10),new V3d(10))})
            for(int id=0;id<fixture.Kinds.Length;id++)
            {
                var expected=fixture.Kinds[id]>=3&&Make(new[]{3,4}).Mesh.ObjectIntersectsBox(id==fixture.Tri?0:1,box);
                Assert.DoesNotThrow(()=>Assert.That(fixture.Mesh.ObjectIntersectsBox(id,box),Is.EqualTo(expected),context+$", box face={id}"),context);
            }
        }

        [Test]
        public void MixedShortFacesRespectOriginalIndicesFiltersSlicesAndValidSurfacePayloads()
        {
            Assert.Multiple(() => {
                foreach(var kinds in new[]{new[]{0,1,2,3,4},new[]{3,0,1,2,4},new[]{3,4,0,1,2},new[]{0,3,1,4,2,0}})
                foreach(int prefix in new[]{0,2})
                foreach(int spare in new[]{0,4})
                {var fixture=Make(kinds,prefix,spare);var snapshot=new Snapshot(fixture.Mesh);var context=$"mixed kinds=[{string.Join(",",kinds)}], prefix={prefix}, spare={spare}";Matrix(fixture,context);snapshot.Check(context);}
            });
        }

        [Test]
        public void OrdinaryTriangleAndQuadQueriesRetainBoundsCutoffsAndPayloads()
        {
            foreach(int prefix in new[]{0,2})
            foreach(int spare in new[]{0,4})
            {var fixture=Make(new[]{3,4},prefix,spare);var snapshot=new Snapshot(fixture.Mesh);Matrix(fixture,$"ordinary prefix={prefix}, spare={spare}");Assert.That(fixture.Mesh.BoundingBoxOfFace(0),Is.EqualTo(new Box3d(new V3d(0,0,3),new V3d(2,2,3))));Assert.That(fixture.Mesh.BoundingBoxOfFace(1),Is.EqualTo(new Box3d(new V3d(0,0,7),new V3d(2,2,7))));snapshot.Check("ordinary");}
        }

        [Test]
        public void MixedFaceKdTreesKeepSurfaceQueriesWithBothBoundsStrategies()
        {
            Assert.Multiple(() => {
                foreach(bool optimize in new[]{false,true})
                foreach(int spare in new[]{0,4})
                {
                    var fixture=Make(new[]{0,3,1,4,2,0},0,spare,8);var mesh=fixture.Mesh;var snapshot=new Snapshot(mesh);KdIntersectionTree tree=null;var context=$"kd optimize={optimize}, spare={spare}";
                    Assert.DoesNotThrow(()=>tree=new KdIntersectionTree(mesh,KdIntersectionTree.BuildFlags.Raytracing|KdIntersectionTree.BuildFlags.NoMultithreading|(optimize?KdIntersectionTree.BuildFlags.EmptySpaceOptimization:0)),context);Assert.That(tree,Is.Not.Null,context);if(tree==null)continue;
                    Assert.That(tree.Tree.Leafs().Count(),Is.GreaterThan(1),context+": genuine subdivision");
                    for(int stage=0;stage<2;stage++)
                    {
                        if(stage==1)tree.Flatten();
                        foreach(int column in new[]{0,3,7})
                        foreach(int policy in new[]{0,1,2,3})
                        foreach(double bound in new[]{2.0,4,8})
                        {
                            var label=context+$", stage={stage}, column={column}, policy={policy}, bound={bound}";int tri=6*column+1,quad=6*column+3;
                            Func<IIntersectableObjectSet,int,bool> filter=policy==0?null:(owner,id)=>{Assert.That(owner,Is.SameAs(mesh),label);return policy==1?fixture.Kinds[id]==4:policy==2?fixture.Kinds[id]<3:false;};
                            double distance=policy==0?3:7;bool expected=policy<2&&bound>distance;var point=new V3d(4*column+.25,1.25,0);var seed=Closest(mesh,bound);var c=seed;var found=!expected;
                            Assert.DoesNotThrow(()=>found=tree.ClosestPoint(point,filter,(_,_,_,_)=>throw new Exception("Existing mesh point-filter policy"),ref c),label);Assert.That(found,Is.EqualTo(expected),label);
                            if(expected){Assert.That(Finite(c.Point)&&double.IsFinite(c.Distance)&&double.IsFinite(c.DistanceSquared),Is.True,label);Assert.That(c.Distance,Is.EqualTo(distance),label);Assert.That(c.Point,Is.EqualTo(new V3d(point.X,point.Y,distance)),label);Assert.That(c.SetObject.Index,Is.EqualTo(policy==0?tri:quad),label);Assert.That(c.SetObject.Set,Is.SameAs(mesh),label);Assert.That(c.Coord,Is.EqualTo(seed.Coord),label);Stack(c.ObjectStack,seed.ObjectStack,mesh,label);}else Same(c,seed,label);
                            var hs=Hit(mesh);var h=hs;Assert.DoesNotThrow(()=>found=tree.Intersect(new FastRay3d(point,V3d.ZAxis),filter,null,0,bound,ref h),label);Assert.That(found,Is.EqualTo(expected),label);
                            if(expected){Assert.That(Finite(h.RayHit.Point)&&double.IsFinite(h.RayHit.T),Is.True,label);Assert.That(h.RayHit.T,Is.EqualTo(distance),label);Assert.That(h.RayHit.Point,Is.EqualTo(new V3d(point.X,point.Y,distance)),label);Assert.That(h.SetObject.Set,Is.SameAs(mesh),label);Assert.That(h.SetObject.Index,Is.EqualTo(policy==0?tri:quad),label);Assert.That(h.RayHit.Part,Is.EqualTo(policy==0?0:1),label);Assert.That(h.Tag,Is.SameAs(hs.Tag),label);Assert.That(h.ObjectStack,Is.Null,label);}else Same(h,hs,label);
                        }
                    }
                    snapshot.Check(context);
                }
            });
        }
    }
}
