using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Data.Points;
using NUnit.Framework;
using Defs = Aardvark.Data.Points.GenericChunk.Defs;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class GenericChunkAttributeUpdateTests
    {
        private static readonly Durable.Def Extra = new Durable.Def(new Guid("7b30e8dc-e781-4abb-b91e-359ba8618a44"),"bounds-extra","Unrelated per-point values",Durable.Primitives.Int32Array.Id,true);
        private abstract class SequenceState { public int Enumerations, Moves, Disposals; }
        private sealed class Once<T> : SequenceState, IEnumerable<T>
        {
            private readonly T[] values;private readonly Exception error;
            public Once(T[] values,Exception error=null){this.values=values;this.error=error;}
            public IEnumerator<T> GetEnumerator(){if(++Enumerations!=1)throw new InvalidOperationException("Enumerated twice");return Iterate().GetEnumerator();}
            IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
            private IEnumerable<T> Iterate(){try{for(int i=0;i<values.Length;i++){Moves++;if(error!=null&&i==1)throw error;yield return values[i];}Moves++;}finally{Disposals++;}}
        }
        private sealed class Input
        {
            public int Kind;public Durable.Def Def;public Array Values;public Func<Exception,SequenceState> Sequence;
            public override string ToString()=>Def.Name;
        }
        private static Input Case<T>(int kind,Durable.Def def,int count,params T[] values)
        {var array=Enumerable.Range(0,count).Select(i=>values[i%values.Length]).ToArray();return new Input{Kind=kind,Def=def,Values=array,Sequence=error=>new Once<T>(array,error)};}
        private static IEnumerable<Input> Inputs(int n)
        {
            yield return Case(0,Defs.Colors3b,n,new C3b(1,2,3),new C3b(4,5,6),new C3b(7,8,9));
            yield return Case(0,Defs.Colors4b,n,new C4b(1,2,3,4),new C4b(5,6,7,8),new C4b(9,10,11,12));
            yield return Case(0,Defs.Colors3f,n,new C3f(.25f,.5f,.75f),new C3f(0,1,0),new C3f(1,0,1));
            yield return Case(0,Defs.Colors4f,n,new C4f(.25f,.5f,.75f,1),new C4f(0,1,0,.5f),new C4f(1,0,1,0));
            yield return Case(1,Defs.Normals3f,n,new V3f(1,2,3),new V3f(-4,5,6),new V3f(7,8,-9));
            yield return Case(2,Defs.Intensities1b,n,(byte)0,(byte)17,byte.MaxValue);
            yield return Case(2,Defs.Intensities1s,n,short.MinValue,(short)17,short.MaxValue);
            yield return Case(2,Defs.Intensities1us,n,(ushort)0,(ushort)17,ushort.MaxValue);
            yield return Case(2,Defs.Intensities1i,n,int.MinValue,17,int.MaxValue);
            yield return Case(2,Defs.Intensities1ui,n,0u,17u,uint.MaxValue);
            yield return Case(2,Defs.Intensities1f,n,-1.25f,17f,101.5f);
            yield return Case(2,Defs.Intensities1d,n,-1.25,17.0,101.5);
            yield return Case(3,Defs.Classifications1b,n,(byte)0,(byte)17,byte.MaxValue);
            yield return Case(3,Defs.Classifications1s,n,short.MinValue,(short)17,short.MaxValue);
            yield return Case(3,Defs.Classifications1us,n,(ushort)0,(ushort)17,ushort.MaxValue);
            yield return Case(3,Defs.Classifications1i,n,int.MinValue,17,int.MaxValue);
            yield return Case(3,Defs.Classifications1ui,n,0u,17u,uint.MaxValue);
            yield return Case(3,Defs.ClassificationsString,n,"ground","tree","α");
        }
        private static (Durable.Def Def,Array Values) Positions(int representation,int count)
        {
            var p=new[]{new V3d(1,2,3),new V3d(-3,5,-7),new V3d(7,-11,13)}.Take(count).ToArray();
            return representation switch{0=>(Defs.Positions2f,p.Select(v=>new V2f((float)v.X,(float)v.Y)).ToArray()),1=>(Defs.Positions2d,p.Select(v=>v.XY).ToArray()),2=>(Defs.Positions3f,p.Select(v=>(V3f)v).ToArray()),_=>(Defs.Positions3d,p)};
        }
        private static Durable.Def Key(GenericChunk source,int kind)=>kind switch{0=>source.ColorsDef,1=>source.NormalsDef,2=>source.IntensitiesDef,_=>source.ClassificationsDef};
        // CLR primitive-array compatibility makes unsigned arrays match earlier signed-array arms.
        private static Durable.Def Selected(Input input,bool enumerable)=>!enumerable&&input.Def==Defs.Intensities1us?Defs.Intensities1s:!enumerable&&input.Def==Defs.Intensities1ui?Defs.Intensities1i:!enumerable&&input.Def==Defs.Classifications1us?Defs.Classifications1s:!enumerable&&input.Def==Defs.Classifications1ui?Defs.Classifications1i:input.Def;
        private static GenericChunk Update(GenericChunk source,int kind,object input)=>kind switch{0=>source.WithColors(input),1=>source.WithNormals(input),2=>source.WithIntensities(input),_=>source.WithClassifications(input)};
        private static GenericChunk Source(int representation,int count,int kind,bool replace,Box3d? box)
        {
            var p=Positions(representation,count);var data=new Dictionary<Durable.Def,object>{{p.Def,p.Values},{Defs.Colors4b,Enumerable.Repeat(new C4b(201,202,203,204),count).ToArray()},{Defs.Normals3f,Enumerable.Repeat(new V3f(-1,-2,-3),count).ToArray()},{Defs.Intensities1i,Enumerable.Repeat(-91,count).ToArray()},{Defs.Classifications1b,Enumerable.Repeat((byte)211,count).ToArray()},{Extra,Enumerable.Range(90,count).ToArray()}};
            var old=kind switch{0=>Defs.Colors4b,1=>Defs.Normals3f,2=>Defs.Intensities1i,_=>Defs.Classifications1b};if(!replace)data.Remove(old);return new GenericChunk(data,box);
        }
        private static long[] Bits(Box3d b)=>new[]{b.Min.X,b.Min.Y,b.Min.Z,b.Max.X,b.Max.Y,b.Max.Z}.Select(BitConverter.DoubleToInt64Bits).ToArray();
        private sealed class Snapshot
        {
            private readonly GenericChunk source;private readonly object data;private readonly KeyValuePair<Durable.Def,object>[] entries;private readonly Array[] copies;private readonly long[] bounds;
            public Snapshot(GenericChunk source){this.source=source;data=source.Data;entries=source.Data.ToArray();copies=entries.Select(e=>(Array)((Array)e.Value).Clone()).ToArray();bounds=Bits(source.BoundingBox);}
            public void Check(string context){Assert.That(source.Data,Is.SameAs(data),context);Assert.That(source.Data.Count,Is.EqualTo(entries.Length),context);Assert.That(Bits(source.BoundingBox),Is.EqualTo(bounds),context);for(int i=0;i<entries.Length;i++){Assert.That(source.Data[entries[i].Key],Is.SameAs(entries[i].Value),context);Assert.That(source.Data[entries[i].Key],Is.EqualTo(copies[i]),context);}}
        }
        private static void Verify(GenericChunk source,Input input,bool enumerable,string context)
        {
            var snapshot=new Snapshot(source);var old=Key(source,input.Kind);var sequence=enumerable?input.Sequence(null):null;var result=Update(source,input.Kind,enumerable?(object)sequence:input.Values);
            Assert.That(result,Is.Not.SameAs(source),context);Assert.That(result.Count,Is.EqualTo(source.Count),context);Assert.That(result.PositionsDef,Is.EqualTo(source.PositionsDef),context);Assert.That(result.Positions,Is.SameAs(source.Positions),context);
            var selected=Selected(input,enumerable);Assert.That(Bits(result.BoundingBox),Is.EqualTo(Bits(source.BoundingBox)),context+": cached bounds");Assert.That(Key(result,input.Kind),Is.EqualTo(selected),context+": selected definition");
            Assert.That(result.Data[selected].GetType(),Is.EqualTo(input.Values.GetType()),context);Assert.That(result.Data[selected],Is.EqualTo(input.Values),context);
            if(!enumerable)Assert.That(result.Data[selected],Is.SameAs(input.Values),context+": array sharing");
            else{Assert.That(sequence.Enumerations,Is.EqualTo(1),context);Assert.That(sequence.Moves,Is.EqualTo(source.Count+1),context);Assert.That(sequence.Disposals,Is.EqualTo(1),context);if(source.Count>0)Assert.That(result.Data[selected],Is.Not.SameAs(input.Values),context);}
            Assert.That(result.Data.Count,Is.EqualTo(source.Data.Count+(old==null?1:0)),context);
            foreach(var entry in source.Data)if(entry.Key!=old)Assert.That(result.Data[entry.Key],Is.SameAs(entry.Value),context+": unrelated "+entry.Key.Name);
            if(old!=null&&old!=selected)Assert.That(result.Has(old),Is.False,context+": replaced definition removed");snapshot.Check(context);
        }

        [Test]
        public void SuppliedAndCachedBoundsSurviveAttributeOnlyUpdates()
        {
            var nan=BitConverter.Int64BitsToDouble(unchecked((long)0x7ff8000000000042));
            var boxes=new[]{new Box3d(new V3d(-100,-200,-300),new V3d(100,200,300)),Box3d.Invalid,new Box3d(new V3d(-0.0,nan,double.NegativeInfinity),new V3d(+0.0,17,double.PositiveInfinity))};
            Assert.Multiple(()=>{
                foreach(int representation in new[]{0,1,2,3})foreach(int count in new[]{0,1,3})foreach(bool replace in new[]{false,true})foreach(var input in Inputs(count))foreach(bool enumerable in new[]{false,true})foreach(var box in boxes)
                {var source=Source(representation,count,input.Kind,replace,box);Verify(source,input,enumerable,$"supplied positions={representation}, count={count}, replace={replace}, input={input}, enumerable={enumerable}, boxbits=[{string.Join(",",Bits(box))}]");}
                // Attribute updates no longer incidentally refresh cached bounds after external edits to shared arrays.
                foreach(int representation in new[]{0,1,2,3})foreach(int kind in new[]{0,1,2,3})
                {
                    var source=Source(representation,3,kind,true,null);var cached=Bits(source.BoundingBox);var p=(Array)source.Positions;
                    object changed=representation switch{0=>new V2f(999,888),1=>new V2d(999,888),2=>new V3f(999,888,777),_=>new V3d(999,888,777)};p.SetValue(changed,0);
                    var input=Inputs(3).First(x=>x.Kind==kind);Verify(source,input,false,$"edited shared positions={representation}, kind={kind}");Assert.That(Bits(source.BoundingBox),Is.EqualTo(cached));Assert.That(((Array)source.Positions).GetValue(0),Is.EqualTo(changed));
                }
            });
        }

        [Test]
        public void ComputedBoundsSharingAndMaterializationRetainTheirContracts()
        {
            Assert.Multiple(()=>{
                foreach(int representation in new[]{0,1,2,3})foreach(int count in new[]{0,1,3})foreach(bool replace in new[]{false,true})foreach(var input in Inputs(count))foreach(bool enumerable in new[]{false,true})
                {
                    var source=Source(representation,count,input.Kind,replace,null);var context=$"automatic positions={representation}, count={count}, replace={replace}, input={input}, enumerable={enumerable}";
                    if(count>0){Assert.That(source.BoundingBox.IsValid,Is.True,context);var expected=count==1?new Box3d(new V3d(1,2,representation<2?0:3)):new Box3d(new V3d(-3,-11,representation<2?0:-7),new V3d(7,5,representation<2?0:13));Assert.That(source.BoundingBox,Is.EqualTo(expected),context);}
                    Verify(source,input,enumerable,context);
                }
            });
        }

        [Test]
        public void InvalidInputsAndEnumerationFailuresLeaveTheSourceUnchanged()
        {
            Assert.Multiple(()=>{
                foreach(int representation in new[]{0,1,2,3})foreach(int count in new[]{0,1,3})foreach(bool replace in new[]{false,true})foreach(var input in Inputs(count+1))foreach(bool enumerable in new[]{false,true})
                {
                    var source=Source(representation,count,input.Kind,replace,new Box3d(new V3d(-100),new V3d(100)));var snapshot=new Snapshot(source);var sequence=enumerable?input.Sequence(null):null;var context=$"length positions={representation}, count={count}, replace={replace}, input={input}, enumerable={enumerable}";
                    var error=Assert.Throws<ArgumentException>(()=>Update(source,input.Kind,enumerable?(object)sequence:input.Values),context);if(error!=null){Assert.That(error.ParamName,Is.Null,context);Assert.That(error.Message,Does.Contain($"must have length {count}, but has {count+1}"),context);}if(enumerable){Assert.That(sequence.Enumerations,Is.EqualTo(1),context);Assert.That(sequence.Disposals,Is.EqualTo(1),context);}snapshot.Check(context);
                }
                foreach(int kind in new[]{0,1,2,3})
                {
                    var source=Source(3,3,kind,true,null);var snapshot=new Snapshot(source);var unsupported=kind switch{0=>(object)new double[3],1=>new V3d[3],2=>new decimal[3],_=>new long[3]};var name=new[]{"newColors","newNormals","newIntensities","newClassifications"}[kind];
                    var error=Assert.Throws<ArgumentException>(()=>Update(source,kind,unsupported));if(error!=null){Assert.That(error.ParamName,Is.EqualTo(name));Assert.That(error.Message,Does.Contain(unsupported.GetType().ToString()));}
                    Assert.Throws<NullReferenceException>(()=>Update(source,kind,null));snapshot.Check("unsupported kind="+kind);
                }
                foreach(var input in Inputs(3))
                {
                    var source=Source(2,3,input.Kind,true,null);var snapshot=new Snapshot(source);var failure=new InvalidOperationException("enumeration failure");var sequence=input.Sequence(failure);
                    Assert.That(Assert.Throws<InvalidOperationException>(()=>Update(source,input.Kind,sequence)),Is.SameAs(failure),input.ToString());Assert.That(sequence.Enumerations,Is.EqualTo(1));Assert.That(sequence.Disposals,Is.EqualTo(1));snapshot.Check("throwing "+input);
                }
            });
        }

        [Test]
        public void PositionReplacementAndMappingStillRecomputeBounds()
        {
            foreach(int representation in new[]{0,1,2,3})foreach(int count in new[]{0,1,3})
            {
                var source=Source(representation,count,0,true,new Box3d(new V3d(-1000),new V3d(1000)));var snapshot=new Snapshot(source);var p=Positions(representation,count);var shifted=Array.CreateInstance(p.Values.GetType().GetElementType(),count);
                for(int i=0;i<count;i++){var v=new[]{new V3d(1,2,3),new V3d(-3,5,-7),new V3d(7,-11,13)}[i]+new V3d(10,20,30);object item=representation switch{0=>new V2f((float)v.X,(float)v.Y),1=>v.XY,2=>(V3f)v,_=>v};shifted.SetValue(item,i);}
                var expected=new GenericChunk(new Dictionary<Durable.Def,object>{{p.Def,shifted}}).BoundingBox;var result=source.WithPositions(shifted);var mapped=source.ImmutableMapPositions(v=>v+new V3d(10,20,30));var context=$"position-changing representation={representation}, count={count}";
                foreach(var chunk in new[]{result,mapped}){Assert.That(Bits(chunk.BoundingBox),Is.EqualTo(Bits(expected)),context);Assert.That(Bits(chunk.BoundingBox),Is.Not.EqualTo(Bits(source.BoundingBox)),context);Assert.That(chunk.Positions,Is.EqualTo(shifted),context);foreach(var entry in source.Data)if(entry.Key!=source.PositionsDef)Assert.That(chunk.Data[entry.Key],Is.SameAs(entry.Value),context);}
                Assert.That(result.Positions,Is.SameAs(shifted),context);snapshot.Check(context);
            }
        }
    }
}
