using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Aardvark.Base;
using NUnit.Framework;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class PolyMeshSubsetVertexCountTests
    {
        private static readonly Symbol VDirect="subset-v-direct", VIndexed="subset-v-indexed", FDirect="subset-f-direct", FIndexed="subset-f-indexed", CDirect="subset-c-direct", CIndexed="subset-c-indexed", EdgeData="subset-edge", Marker="subset-instance";
        private static readonly int[][] Faces={new[]{0,2,1},new[]{2,3,4,1},new[]{4,2,0}};
        private static V3d Position(int i)=>new V3d(i+.25,i*i+.5,-i-1);
        private static PolyMesh Make(int active,int capacity,bool attributes)
        {
            var p=Enumerable.Range(0,capacity).Select(i=>i<active?Position(i):new V3d(double.NaN,1000+i,double.PositiveInfinity)).ToArray();
            var vertex=new SymbolDict<Array>{{PolyMesh.Property.Positions,p}};
            if(attributes){vertex[PolyMesh.Property.Normals]=Enumerable.Range(0,capacity).Select(i=>new V3d(i+1,2,3)).ToArray();vertex[PolyMesh.Property.Colors]=Enumerable.Range(0,capacity).Select(i=>new C4f(i*.125f,.5f,1,1)).ToArray();vertex[VDirect]=Enumerable.Range(0,capacity).Select(i=>i+.75).ToArray();vertex[VIndexed]=Enumerable.Range(0,capacity).Select(i=>i%3).ToArray();vertex[-VIndexed]=new[]{"v0","v1","v2","unused"};}
            var faces=active==0?Array.Empty<int[]>():Faces;var first=new int[faces.Length+1];var indices=new List<int>();for(int i=0;i<faces.Length;i++){indices.AddRange(faces[i]);first[i+1]=indices.Count;}int corners=indices.Count;indices.AddRange(new[]{int.MaxValue,int.MinValue});
            var mesh=new PolyMesh{VertexAttributes=vertex,FirstIndexArray=first,VertexIndexArray=indices.ToArray()};if(active!=capacity)mesh.VertexCount=active;mesh.VertexIndexCount=corners;
            if(attributes){mesh.FaceAttributes[FDirect]=Enumerable.Range(0,faces.Length).Select(i=>10+i).ToArray();mesh.FaceAttributes[FIndexed]=Enumerable.Range(0,faces.Length).Select(i=>(2*i)%3).ToArray();mesh.FaceAttributes[-FIndexed]=new[]{"f0","f1","f2","unused"};mesh.FaceVertexAttributes[CDirect]=Enumerable.Range(0,corners).Select(i=>new V2d(i,i+.25)).ToArray();mesh.FaceVertexAttributes[CIndexed]=Enumerable.Range(0,corners).Select(i=>i%4).ToArray();mesh.FaceVertexAttributes[-CIndexed]=new[]{new V3f(1,2,3),new V3f(4,5,6),new V3f(7,8,9),new V3f(10,11,12),new V3f(99)};mesh.EdgeAttributes[EdgeData]=new[]{71,72};mesh.InstanceAttributes[Marker]=new object();}
            return mesh;
        }
        private sealed class Snapshot
        {
            private readonly PolyMesh mesh;private readonly int[] counts;private readonly object[] refs;private readonly Array[] copies;private readonly SymbolDict<Array>[] dictionaries;private readonly KeyValuePair<Symbol,Array>[][] entries;private readonly SymbolDict<object> instances;private readonly KeyValuePair<Symbol,object>[] instanceEntries;
            public Snapshot(PolyMesh mesh)
            {
                this.mesh=mesh;counts=new[]{mesh.VertexCount,mesh.Get<int>(PolyMesh.Property.VertexCount),mesh.FaceCount,mesh.VertexIndexCount};refs=new object[]{mesh.PositionArray,mesh.FirstIndexArray,mesh.VertexIndexArray};copies=new Array[]{mesh.PositionArray,mesh.FirstIndexArray,mesh.VertexIndexArray}.Select(a=>a==null?null:(Array)a.Clone()).ToArray();
                dictionaries=new[]{mesh.VertexAttributes,mesh.FaceAttributes,mesh.FaceVertexAttributes,mesh.EdgeAttributes};entries=dictionaries.Select(d=>d.ToArray()).ToArray();attributeCopies=entries.Select(es=>es.Select(e=>e.Value==null?null:(Array)e.Value.Clone()).ToArray()).ToArray();instances=mesh.InstanceAttributes;instanceEntries=instances.ToArray();
            }
            private readonly Array[][] attributeCopies;
            public void Check(string context)
            {
                Assert.That(new[]{mesh.VertexCount,mesh.Get<int>(PolyMesh.Property.VertexCount),mesh.FaceCount,mesh.VertexIndexCount},Is.EqualTo(counts),context);
                var current=new object[]{mesh.PositionArray,mesh.FirstIndexArray,mesh.VertexIndexArray};for(int i=0;i<current.Length;i++){Assert.That(current[i],Is.SameAs(refs[i]),context);Assert.That(current[i],Is.EqualTo(copies[i]),context);}
                var dicts=new[]{mesh.VertexAttributes,mesh.FaceAttributes,mesh.FaceVertexAttributes,mesh.EdgeAttributes};for(int d=0;d<dicts.Length;d++){Assert.That(dicts[d],Is.SameAs(dictionaries[d]),context);Assert.That(dicts[d].Count,Is.EqualTo(entries[d].Length),context);for(int i=0;i<entries[d].Length;i++){Assert.That(dicts[d][entries[d][i].Key],Is.SameAs(entries[d][i].Value),context);Assert.That(dicts[d][entries[d][i].Key],Is.EqualTo(attributeCopies[d][i]),context);}}
                Assert.That(mesh.InstanceAttributes,Is.SameAs(instances),context);Assert.That(mesh.InstanceAttributes.Count,Is.EqualTo(instanceEntries.Length),context);foreach(var e in instanceEntries)Assert.That(mesh.InstanceAttributes[e.Key],Is.SameAs(e.Value),context);
            }
        }
        private sealed class Selection : IEnumerable<int>
        {
            private readonly int[] values;public int Enumerations,Disposals;
            public Selection(int[] values){this.values=values;}
            public IEnumerator<int> GetEnumerator(){if(++Enumerations>1)throw new InvalidOperationException("Selection enumerated twice");return Iterate().GetEnumerator();}
            IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
            private IEnumerable<int> Iterate(){try{foreach(var i in values)yield return i;}finally{Disposals++;}}
        }
        private static void Verify(PolyMesh source,PolyMesh result,int[] selection,bool compact,string context)
        {
            var oldCorners=selection.SelectMany(fi=>Enumerable.Range(source.FirstIndexArray[fi],source.FirstIndexArray[fi+1]-source.FirstIndexArray[fi])).ToArray();var oldVertices=oldCorners.Select(i=>source.VertexIndexArray[i]).ToArray();var back=new List<int>();foreach(int index in oldVertices)if(!back.Contains(index))back.Add(index);
            int expectedCount=compact?back.Count:source.VertexCount;Assert.That(result.VertexCount,Is.EqualTo(expectedCount),context+": active count");Assert.That(result.Get<int>(PolyMesh.Property.VertexCount),Is.EqualTo(expectedCount),context+": stored count");
            var vertices=result.Vertices.ToArray();Assert.That(vertices.Length,Is.EqualTo(expectedCount),context+": enumeration count");Assert.That(vertices.Select(v=>v.Index),Is.EqualTo(Enumerable.Range(0,expectedCount)),context);
            for(int i=0;i<Math.Min(vertices.Length,expectedCount);i++){var expected=source.PositionArray[compact?back[i]:i];Assert.That(vertices[i].Mesh,Is.SameAs(result),context);Assert.That(vertices[i].Position,Is.EqualTo(expected),context);Assert.That(double.IsFinite(vertices[i].Position.X)&&double.IsFinite(vertices[i].Position.Y)&&double.IsFinite(vertices[i].Position.Z),Is.True,context);}
            var first=new List<int>{0};foreach(int fi in selection)first.Add(first.Last()+source.FirstIndexArray[fi+1]-source.FirstIndexArray[fi]);Assert.That(result.FirstIndexArray,Is.EqualTo(first),context);Assert.That(result.VertexIndexArray,Is.EqualTo(compact?oldVertices.Select(i=>back.IndexOf(i)):oldVertices),context);Assert.That(result.FaceCount,Is.EqualTo(selection.Length),context);Assert.That(result.VertexIndexCount,Is.EqualTo(oldCorners.Length),context);
            Assert.That(result.VertexAttributes,Is.Not.SameAs(source.VertexAttributes),context);foreach(var e in source.VertexAttributes)
            {
                if(!compact||e.Key.IsNegative){Assert.That(result.VertexAttributes[e.Key],Is.SameAs(e.Value),context+": shared vertex storage "+e.Key);continue;}
                var actual=result.VertexAttributes[e.Key];Assert.That(actual.GetType(),Is.EqualTo(e.Value.GetType()),context);Assert.That(actual.Length,Is.EqualTo(back.Count),context);for(int i=0;i<back.Count;i++)Assert.That(actual.GetValue(i),Is.EqualTo(e.Value.GetValue(back[i])),context);if(back.Count>0)Assert.That(actual,Is.Not.SameAs(e.Value),context);
            }
            Assert.That(result.PositionArray,Is.SameAs(result.VertexAttributes.GetOrDefault(PolyMesh.Property.Positions)),context);if(!compact)Assert.That(result.PositionArray,Is.SameAs(source.PositionArray),context);
            if(source.VertexAttributes.ContainsKey(PolyMesh.Property.Normals))Assert.That(result.NormalArray,Is.SameAs(result.VertexAttributes[PolyMesh.Property.Normals]),context);if(source.VertexAttributes.ContainsKey(PolyMesh.Property.Colors))Assert.That(result.ColorArray,Is.SameAs(result.VertexAttributes[PolyMesh.Property.Colors]),context);
            foreach(var pair in new[]{(Source:source.FaceAttributes,Result:result.FaceAttributes,Map:selection),(Source:source.FaceVertexAttributes,Result:result.FaceVertexAttributes,Map:oldCorners)})
            {Assert.That(pair.Result,Is.Not.SameAs(pair.Source),context);foreach(var e in pair.Source){var actual=pair.Result[e.Key];if(e.Key.IsNegative)Assert.That(actual,Is.SameAs(e.Value),context+": value pool");else{Assert.That(actual.GetType(),Is.EqualTo(e.Value.GetType()),context);Assert.That(actual.Length,Is.EqualTo(pair.Map.Length),context);for(int i=0;i<pair.Map.Length;i++)Assert.That(actual.GetValue(i),Is.EqualTo(e.Value.GetValue(pair.Map[i])),context);if(pair.Map.Length>0)Assert.That(actual,Is.Not.SameAs(e.Value),context);}}}
            Assert.That(result.InstanceAttributes,Is.Not.SameAs(source.InstanceAttributes),context);foreach(var e in source.InstanceAttributes)Assert.That(result.InstanceAttributes[e.Key],Is.SameAs(e.Value),context);Assert.That(result.EdgeAttributes,Is.Empty,context+": existing edge/topology omission");Assert.That(result.HasTopology,Is.False,context);
        }
        private static void Append(PolyMesh source,PolyMesh subset,string context)
        {
            int count=source.VertexCount;var active=source.PositionArray?.Take(count).ToArray();var point=new V3d(99,98,97);var oldBuffer=subset.PositionArray;
            if(oldBuffer!=null&&oldBuffer.Length==0){Assert.Throws<IndexOutOfRangeException>(()=>subset.AddVertex(point),context+": existing empty-buffer append limitation");Assert.That(subset.VertexCount,Is.Zero,context);}
            else{int index=-1;Assert.DoesNotThrow(()=>index=subset.AddVertex(point),context);Assert.That(index,Is.EqualTo(count),context+": next append index");Assert.That(subset.VertexCount,Is.EqualTo(count+1),context);Assert.That(subset.Get<int>(PolyMesh.Property.VertexCount),Is.EqualTo(count+1),context);if(index>=0&&index<subset.PositionArray.Length)Assert.That(subset.PositionArray[index],Is.EqualTo(point),context);if(oldBuffer!=null&&count<oldBuffer.Length)Assert.That(source.PositionArray[count],Is.EqualTo(point),context+": existing shared spare-slot writes");}
            Assert.That(source.VertexCount,Is.EqualTo(count),context);Assert.That(source.Get<int>(PolyMesh.Property.VertexCount),Is.EqualTo(count),context);if(active!=null)Assert.That(source.PositionArray.Take(count),Is.EqualTo(active),context+": source active prefix");
        }

        [Test]
        public void NoncompactingSubsetsRetainActiveVerticesOwnershipAndAppendIndices()
        {
            Assert.Multiple(()=>{
                foreach(int capacity in new[]{6,8,32})foreach(bool attributes in new[]{false,true})foreach(var selected in new[]{new[]{0,1,2},new[]{2,0},new[]{1,1,0},new[]{1},Array.Empty<int>()})foreach(bool enumerable in new[]{false,true})
                {
                    var source=Make(6,capacity,attributes);var snapshot=new Snapshot(source);var sequence=new Selection(selected);var context=$"noncompact capacity={capacity}, attrs={attributes}, faces=[{string.Join(",",selected)}], enumerable={enumerable}";
                    var subset=source.SubSetOfFaces(enumerable?(IEnumerable<int>)sequence:selected,false);Verify(source,subset,selected,false,context);snapshot.Check(context+": after subsetting");if(enumerable){Assert.That(sequence.Enumerations,Is.EqualTo(1),context);Assert.That(sequence.Disposals,Is.EqualTo(1),context);}Append(source,subset,context);
                }
            });
        }

        [Test]
        public void ZeroActiveAndEmptyStorageSubsetsPreserveNullEmptyAndPreallocatedBuffers()
        {
            Assert.Multiple(()=>{
                foreach(int capacity in new[]{1,16,31})foreach(bool attributes in new[]{false,true})
                {var source=Make(0,capacity,attributes);var snapshot=new Snapshot(source);var context=$"zero-active capacity={capacity}, attrs={attributes}";var subset=source.SubSetOfFaces(Array.Empty<int>(),false);Verify(source,subset,Array.Empty<int>(),false,context);snapshot.Check(context);Append(source,subset,context);}
                foreach(int kind in new[]{0,1,2})foreach(bool compact in new[]{false,true})
                {
                    if(kind==1&&compact)continue; // Explicit null positive-key storage is not a compactable array.
                    var source=new PolyMesh();if(kind==1)source.PositionArray=null;if(kind==2)source.PositionArray=Array.Empty<V3d>();var snapshot=new Snapshot(source);var context=$"empty kind={kind}, compact={compact}";var subset=source.SubSetOfFaces(Array.Empty<int>(),compact);
                    Verify(source,subset,Array.Empty<int>(),compact,context);Assert.That(subset.VertexAttributes.ContainsKey(PolyMesh.Property.Positions),Is.EqualTo(source.VertexAttributes.ContainsKey(PolyMesh.Property.Positions)),context);snapshot.Check(context);Append(source,subset,context);snapshot.Check(context+": after append control");
                }
            });
        }

        [Test]
        public void CompactingProjectionValuePoolsAndSelectionFailuresKeepExistingContracts()
        {
            foreach(int capacity in new[]{6,8,32})foreach(bool attributes in new[]{false,true})foreach(var selected in new[]{new[]{0,1,2},new[]{2,0},new[]{1,1,0},new[]{1},Array.Empty<int>()})
            {var source=Make(6,capacity,attributes);var snapshot=new Snapshot(source);var context=$"compact capacity={capacity}, attrs={attributes}, faces=[{string.Join(",",selected)}]";var subset=source.SubSetOfFaces(selected,true);Verify(source,subset,selected,true,context);snapshot.Check(context);}
            foreach(bool compact in new[]{false,true})
            {var source=Make(6,32,true);var snapshot=new Snapshot(source);Assert.Throws<ArgumentNullException>(()=>source.SubSetOfFaces(null,compact));foreach(int invalid in new[]{-1,int.MaxValue})Assert.Throws<IndexOutOfRangeException>(()=>source.SubSetOfFaces(new[]{invalid},compact));snapshot.Check("selection failures");}
        }
    }
}
