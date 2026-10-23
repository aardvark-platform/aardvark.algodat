/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.
    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.
    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
using Aardvark.Base;
using Aardvark.Data.Points;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry.Tests
{
    [TestFixture]
    public class ChunkTests
    {
        private sealed class ConstructionData
        {
            public readonly IList<V3d> P;public readonly IList<C4b> C;public readonly IList<V3f> N;public readonly IList<int> I;public readonly IList<byte> K;
            private readonly V3d[] ps;private readonly C4b[] cs;private readonly V3f[] ns;private readonly int[] js;private readonly byte[] ks;
            public ConstructionData(int count,int shorter=-1,int length=0,bool lists=false)
            {
                ps=Enumerable.Range(0,count).Select(i=>new V3d(i+1,2*i+2,3*i+3)).ToArray();cs=Enumerable.Range(0,shorter==0?length:count).Select(i=>new C4b(i+1,2,3,255)).ToArray();ns=Enumerable.Range(0,shorter==1?length:count).Select(i=>new V3f(i+1,2,3)).ToArray();js=Enumerable.Range(0,shorter==2?length:count).Select(i=>100+i).ToArray();ks=Enumerable.Range(0,shorter==3?length:count).Select(i=>(byte)(10+i)).ToArray();
                P=lists?(IList<V3d>)ps.ToList():(V3d[])ps.Clone();C=lists?(IList<C4b>)cs.ToList():(C4b[])cs.Clone();N=lists?(IList<V3f>)ns.ToList():(V3f[])ns.Clone();I=lists?(IList<int>)js.ToList():(int[])js.Clone();K=lists?(IList<byte>)ks.ToList():(byte[])ks.Clone();
            }
            public Chunk Create(object parts,Range1i? range=null,Box3d? bounds=null)=>new Chunk(P,C,N,I,K,parts,range,bounds);
            public void Unchanged(string context){Assert.That(P,Is.EqualTo(ps),context);Assert.That(C,Is.EqualTo(cs),context);Assert.That(N,Is.EqualTo(ns),context);Assert.That(I,Is.EqualTo(js),context);Assert.That(K,Is.EqualTo(ks),context);}
        }
        private static object ConstructorParts(int width,bool list,int count)
        {
            var values=new[]{11,12,90,99,123}.Take(count).ToArray();
            if(width==0){var a=values.Select(i=>(byte)i).ToArray();return list?(object)a.ToList():a;}
            if(width==1){var a=values.Select(i=>(short)i).ToArray();return list?(object)a.ToList():a;}
            return list?(object)values.ToList():values;
        }
        private static int[] ConstructorPartValues(object parts,int count)=>parts switch{null=>null,int x=>Enumerable.Repeat(x,count).ToArray(),uint x=>Enumerable.Repeat(unchecked((int)x),count).ToArray(),IList<byte> xs=>xs.Select(x=>(int)x).ToArray(),IList<short> xs=>xs.Select(x=>(int)x).ToArray(),IList<int> xs=>xs.ToArray(),_=>throw new Exception("Test part type")};
        private static void ConstructorResult(ConstructionData data,Chunk result,object parts,int count,Range1i? suppliedRange,Box3d? suppliedBounds,string context)
        {
            Assert.That(result.Count,Is.EqualTo(count),context);Assert.That(result.IsEmpty,Is.EqualTo(count==0),context);Assert.That(result.Positions,Is.EqualTo(data.P.Take(count)),context);Assert.That(result.Colors,Is.EqualTo(data.C.Take(count)),context);Assert.That(result.Normals,Is.EqualTo(data.N.Take(count)),context);Assert.That(result.Intensities,Is.EqualTo(data.I.Take(count)),context);Assert.That(result.Classifications,Is.EqualTo(data.K.Take(count)),context);
            Assert.That(result.Colors.Count,Is.EqualTo(count),context);Assert.That(result.Normals.Count,Is.EqualTo(count),context);Assert.That(result.Intensities.Count,Is.EqualTo(count),context);Assert.That(result.Classifications.Count,Is.EqualTo(count),context);
            if(data.P.Count==count)Assert.That(result.Positions,Is.SameAs(data.P),context);if(data.C.Count==count)Assert.That(result.Colors,Is.SameAs(data.C),context);if(data.N.Count==count)Assert.That(result.Normals,Is.SameAs(data.N),context);if(data.I.Count==count)Assert.That(result.Intensities,Is.SameAs(data.I),context);if(data.K.Count==count)Assert.That(result.Classifications,Is.SameAs(data.K),context);
            if(parts==null||parts is int||parts is uint)Assert.That(result.PartIndices,Is.SameAs(parts),context);
            else
            {
                var expected=ConstructorPartValues(parts,0).Take(count).ToArray();Assert.That(ConstructorPartValues(result.PartIndices,count),Is.EqualTo(expected),context+": retained part prefix");Assert.That(((System.Collections.ICollection)result.PartIndices).Count,Is.EqualTo(count),context+": part alignment");
                if(ConstructorPartValues(parts,0).Length==count)Assert.That(result.PartIndices,Is.SameAs(parts),context+": unchanged part identity");else Assert.That(result.PartIndices.GetType(),Is.EqualTo(parts is IList<byte>?typeof(byte[]):parts is IList<short>?typeof(short[]):typeof(int[])),context+": trimmed array width");
            }
            var values=ConstructorPartValues(parts,count)?.Take(count).ToArray();var expectedRange=suppliedRange??(parts is int scalar?new Range1i(scalar):parts is uint unsigned?new Range1i(unchecked((int)unsigned)):values==null?(Range1i?)null:values.Length==0?Range1i.Invalid:new Range1i(values.Min(),values.Max()));Assert.That(result.PartIndexRange,Is.EqualTo(expectedRange),context+": retained range");Assert.That(result.HasPartIndices,Is.EqualTo(parts!=null),context);Assert.That(result.HasPartIndexRange,Is.EqualTo(expectedRange!=null),context);
            var expectedBox=suppliedBounds??(count==0?Box3d.Invalid:new Box3d(data.P.Take(count)));Assert.That(result.BoundingBox,Is.EqualTo(expectedBox),context);foreach(var p in result.Positions)Assert.That(double.IsFinite(p.X)&&double.IsFinite(p.Y)&&double.IsFinite(p.Z),Is.True,context);data.Unchanged(context);
        }

        [Test]
        public void AttributeShorteningTrimsEveryPartIndexPrefixAndRetainsMetadata()
        {
            Assert.Multiple(()=>{
                foreach(int width in new[]{0,1,2})foreach(bool list in new[]{false,true})foreach(bool fieldsAsLists in new[]{false,true})foreach(int attribute in new[]{0,1,2,3})foreach(int length in new[]{0,2})foreach(bool explicitMetadata in new[]{false,true})
                {
                    var data=new ConstructionData(4,attribute,length,fieldsAsLists);var parts=ConstructorParts(width,list,4);var before=ConstructorPartValues(parts,0);Range1i? range=explicitMetadata?new Range1i(-17,200):null;Box3d? bounds=explicitMetadata?new Box3d(new V3d(-100),new V3d(100)):null;var context=$"trim width={width}, list={list}, fieldsAsLists={fieldsAsLists}, attribute={attribute}, count={length}, metadata={explicitMetadata}";Chunk result=null;
                    Assert.DoesNotThrow(()=>result=data.Create(parts,range,bounds),context);Assert.That(result,Is.Not.Null,context);if(result!=null)ConstructorResult(data,result,parts,length,range,bounds,context);Assert.That(ConstructorPartValues(parts,0),Is.EqualTo(before),context+": source indices unchanged");data.Unchanged(context);
                }
            });
        }

        [Test]
        public void TrimmedPartLabelsDoNotReplaceTheAppendedPointsLabelDuringMerges()
        {
            Assert.Multiple(()=>{
                foreach(int width in new[]{0,1,2})foreach(bool list in new[]{false,true})foreach(int attribute in new[]{0,1,2,3})
                {
                    var data=new ConstructionData(4,attribute,2);var parts=ConstructorParts(width,list,4);var before=ConstructorPartValues(parts,0);var left=data.Create(parts,new Range1i(11,99));var right=new Chunk(new[]{new V3d(100,200,300)},new[]{new C4b(7,8,9,255)},new[]{new V3f(7,8,9)},new[]{700},new byte[]{7},7,null,null);var context=$"merge width={width}, list={list}, attribute={attribute}";
                    foreach(var merged in new[]{left.Union(right),Chunk.ImmutableMerge(left,right),Chunk.ImmutableMerge(new[]{left,right})})
                    {Assert.That(merged.Count,Is.EqualTo(3),context);Assert.That(merged.Positions,Is.EqualTo(new[]{data.P[0],data.P[1],right.Positions[0]}),context);Assert.That(ConstructorPartValues(merged.PartIndices,3),Is.EqualTo(new[]{11,12,7}),context+": appended label must not be stale suffix90");Assert.That(merged.TryGetPartIndices(),Is.EqualTo(new[]{11,12,7}),context);Assert.That(merged.Colors.Count,Is.EqualTo(3),context);Assert.That(merged.Normals.Count,Is.EqualTo(3),context);Assert.That(merged.Intensities.Count,Is.EqualTo(3),context);Assert.That(merged.Classifications.Count,Is.EqualTo(3),context);Assert.That(merged.Colors[2],Is.EqualTo(right.Colors[0]),context);Assert.That(merged.Normals[2],Is.EqualTo(right.Normals[0]),context);Assert.That(merged.Intensities[2],Is.EqualTo(700),context);Assert.That(merged.Classifications[2],Is.EqualTo(7),context);Assert.That(merged.PartIndexRange,Is.EqualTo(new Range1i(7,99)),context+": explicit source range stays authoritative");}
                    Assert.That(left.Count,Is.EqualTo(2),context);Assert.That(right.Count,Is.EqualTo(1),context);Assert.That(right.PartIndices,Is.EqualTo(7),context);Assert.That(ConstructorPartValues(parts,0),Is.EqualTo(before),context);data.Unchanged(context);
                }
            });
        }

        [Test]
        public void AlignedShorterScalarAndNullPartInputsKeepExistingConstructionContracts()
        {
            Assert.Multiple(()=>{
                foreach(int width in new[]{0,1,2})foreach(bool list in new[]{false,true})foreach(bool fieldsAsLists in new[]{false,true})foreach(int count in new[]{0,4})
                {var data=new ConstructionData(count,lists:fieldsAsLists);var parts=ConstructorParts(width,list,count);Range1i? range=list?new Range1i(-17,200):null;var context=$"aligned width={width}, list={list}, fieldsAsLists={fieldsAsLists}, count={count}";ConstructorResult(data,data.Create(parts,range),parts,count,range,null,context);}
                foreach(int width in new[]{0,1,2})foreach(bool list in new[]{false,true})foreach(int length in new[]{0,2,5})
                {var data=new ConstructionData(4);var parts=ConstructorParts(width,list,length);Range1i? range=list?new Range1i(-17,200):null;var before=ConstructorPartValues(parts,0);var context=$"part mismatch width={width}, list={list}, length={length}";ConstructorResult(data,data.Create(parts,range),parts,Math.Min(4,length),range,null,context);Assert.That(ConstructorPartValues(parts,0),Is.EqualTo(before),context);}
                foreach(object parts in new object[]{null,7,17u})foreach(int attribute in new[]{-1,0,1,2,3})foreach(int length in new[]{0,2})
                {var data=new ConstructionData(4,attribute,length);int expected=attribute<0?4:length;ConstructorResult(data,data.Create(parts),parts,expected,null,null,$"scalar/null parts={parts}, attribute={attribute}, length={length}");}
                foreach(int width in new[]{0,1,2}){var data=new ConstructionData(4);var parts=ConstructorParts(width,true,4);var error=Assert.Throws<Exception>(()=>data.Create(parts));if(error!=null)Assert.That(error.Message,Does.Contain("8a975735-bb10-42bf-9f7f-4d570e5223e3"),"retained-list range reader remains unchanged");}
                Assert.Throws<ArgumentNullException>(()=>new Chunk(null,null,null,null,null,null,null,null));Assert.Throws<Exception>(()=>new ConstructionData(4).Create(new double[4]));
            });
        }

        private sealed class ConstructorWarningLog : ILogTarget
        {
            private readonly ILogTarget parent;public readonly List<string> Warnings=new List<string>();
            public ConstructorWarningLog(ILogTarget parent){this.parent=parent;}
            public void NewThreadIndex(int index)=>parent.NewThreadIndex(index);
            public void Log(int index,LogMsg message){if(message.Type==LogType.Warn)Warnings.Add(message.RightText);parent.Log(index,message);}
            public void Dispose(){}
        }
        [Test,NonParallelizable]
        public void ConstructorMismatchWarningConditionsAndMessagesRemainUnchanged()
        {
            var previous=Report.RootTarget;var log=new ConstructorWarningLog(previous);Report.RootTarget=log;
            try
            {
                var names=new[]{"color","normal","intensity","classification"};var ids=new[]{"3c3ac3d4-c4de-4ad6-a925-d2fbaece1634","0e6b1e22-25a1-4814-8dc7-add137edcd45","ea548345-6f32-4504-8189-4dd4a4531708","0c82c851-986f-4690-980b-7a3a240bf499"};var partIds=new[]{"a03f843f-d8cd-4a3d-a1c8-d48eb64c5b39","93c9f432-3e59-45a3-b350-9d5ba7bf9ee3","b94ca9d5-1322-4f57-9ebc-85ea8d3fa8bf"};
                foreach(int width in new[]{0,1,2})foreach(bool list in new[]{false,true})
                {
                    var parts=ConstructorParts(width,list,4);log.Warnings.Clear();new ConstructionData(4).Create(parts,new Range1i(11,99));Assert.That(log.Warnings,Is.Empty,"aligned part inputs do not warn");
                    foreach(int attribute in new[]{0,1,2,3}){log.Warnings.Clear();new ConstructionData(4,attribute,2).Create(parts,new Range1i(11,99));Assert.That(log.Warnings,Is.EqualTo(new[]{$"Expected 4 {names[attribute]} values, but found 2. Warning {ids[attribute]}."}),$"only attribute warning width={width}, list={list}, attribute={attribute}");}
                    log.Warnings.Clear();new ConstructionData(4).Create(ConstructorParts(width,list,2),new Range1i(11,99));Assert.That(log.Warnings,Is.EqualTo(new[]{$"Expected 4 part indices, but found 2. Warning {partIds[width]}."}),$"part mismatch width={width}, list={list}");
                }
            }
            finally{Report.RootTarget=previous;log.Dispose();}
        }

        [Test]
        public void Chunk_EmptyChunk1()
        {
            var x = Chunk.Empty;
            ClassicAssert.IsTrue(x.IsEmpty);
            ClassicAssert.IsTrue(x.HasPositions);
            ClassicAssert.IsTrue(!x.HasColors);
        }

        [Test]
        public void Chunk_EmptyChunk2()
        {
            var x = new Chunk(Array.Empty<V3d>(), Array.Empty<C4b>(), null, null, null, null, null, null);
            ClassicAssert.IsTrue(x.IsEmpty);
            ClassicAssert.IsTrue(x.HasPositions);
            ClassicAssert.IsTrue(x.HasColors);
        }

        [Test]
        public void ChunkFromPositionsOnly()
        {
            var x = new Chunk(new V3d[1]);
            ClassicAssert.IsTrue(x.Count == 1);
            ClassicAssert.IsTrue(x.HasPositions);
            ClassicAssert.IsTrue(!x.HasColors);
        }

        [Test]
        public void Chunk_ChunkFromPositionsAndColors()
        {
            var x = new Chunk(new V3d[1], new C4b[1], null, null, null, null, null, null);
            ClassicAssert.IsTrue(x.Count == 1);
            ClassicAssert.IsTrue(x.HasPositions);
            ClassicAssert.IsTrue(x.HasColors);
        }

        [Test]
        public void Chunk_BoundingBoxIsComputedFromPositions()
        {
            var x = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) });
            ClassicAssert.IsTrue(x.Count == 2);
            ClassicAssert.IsTrue(x.BoundingBox == new Box3d(new V3d(1, 2, 3), new V3d(4, 5, 6)));
        }

        [Test]
        public void Chunk_BoundingBoxIsNotComputedFromPositions()
        {
            var x = new Chunk(
                new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) },
                null, null, null, null, null, null,
                bbox: new Box3d(new V3d(0, 0, 0), new V3d(8, 8, 8))
                );
            ClassicAssert.IsTrue(x.Count == 2);
            ClassicAssert.IsTrue(x.BoundingBox == new Box3d(new V3d(0, 0, 0), new V3d(8, 8, 8)));
        }

        [Test]
        public void Chunk_MismatchingNumberOfColorsDoesNotFail1()
        {
            Assert.DoesNotThrow(() =>
            {
                var chunk = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) }, Array.Empty<C4b>(), null, null, null, null, null, null);
                ClassicAssert.IsTrue(chunk.Count == 0);
                ClassicAssert.IsTrue(chunk.Positions.Count == 0);
                ClassicAssert.IsTrue(chunk.Colors.Count == 0);
            });
        }

        [Test]
        public void Chunk_MismatchingNumberOfColorsDoesNotFail2()
        {
            Assert.DoesNotThrow(() =>
            {
                var chunk = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) }, new C4b[123], null, null, null, null, null, null);
                ClassicAssert.IsTrue(chunk.Count == 2);
                ClassicAssert.IsTrue(chunk.Positions.Count == 2);
                ClassicAssert.IsTrue(chunk.Colors.Count == 2);
            });
        }

        [Test]
        public void Chunk_MismatchingNumberOfColorsFails3()
        {
            Assert.Catch(() =>
            {
                new Chunk(null, new C4b[123],null, null, null, null, null, null);
            });
        }

        [Test]
        public void Chunk_EmptyColors()
        {
            _ = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) });
        }

        [Test]
        public void Chunk_EmptyPositionsButNonEmptyColorsFails()
        {
            Assert.Catch(() =>
            {
                new Chunk(null, new C4b[1], null, null, null, null, null, null);
            });
        }

        [Test]
        public void Chunk_ImmutableFilterSequentialMinDistL2()
        {
            var ps = new[] { new V3d(1, 2, 3), new V3d(1.5, 2, 3), new V3d(2, 2, 3), new V3d(2.5, 2, 3) };
            var a = new Chunk(ps.Copy());
            var b = a.ImmutableFilterSequentialMinDistL2(0.75);

            for (var i = 0; i < ps.Length; i++)
                ClassicAssert.IsTrue(a.Positions[i] == ps[i]);

            ClassicAssert.IsTrue(b.Positions.Count == 2);
            ClassicAssert.IsTrue(b.Positions[0] == ps[0]);
            ClassicAssert.IsTrue(b.Positions[1] == ps[2]);
        }

        [Test]
        public void Chunk_ImmutableMapPositions()
        {
            var a = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6) });
            var b = a.ImmutableMapPositions(p => p + new V3d(10, 20, 30));

            ClassicAssert.IsTrue(a.Positions[0] == new V3d(1, 2, 3));
            ClassicAssert.IsTrue(a.Positions[1] == new V3d(4, 5, 6));

            ClassicAssert.IsTrue(b.Positions[0] == new V3d(11, 22, 33));
            ClassicAssert.IsTrue(b.Positions[1] == new V3d(14, 25, 36));
        }

        [Test]
        public void Chunk_ImmutableDeduplicate_1()
        {
            var a = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6), new V3d(1, 2, 3), new V3d(4, 5, 6) });
            var b = a.ImmutableDeduplicate(verbose: false);

            ClassicAssert.IsTrue(a.Positions.Count == 4);
            ClassicAssert.IsTrue(b.Positions.Count == 2);
            ClassicAssert.IsTrue(b.Positions[0] == new V3d(1, 2, 3));
            ClassicAssert.IsTrue(b.Positions[1] == new V3d(4, 5, 6));
        }

        [Test]
        public void Chunk_ImmutableDeduplicate_2()
        {
            var a = new Chunk(new[] { new V3d(1, 2, 3), new V3d(4, 5, 6), new V3d(4, 5, 7), new V3d(4, 5, 8) });
            var b = a.ImmutableDeduplicate(verbose: false);

            ClassicAssert.IsTrue(a.Positions.Count == 4);
            ClassicAssert.IsTrue(b.Positions.Count == 4);
            ClassicAssert.IsTrue(b.Positions[0] == new V3d(1, 2, 3));
            ClassicAssert.IsTrue(b.Positions[1] == new V3d(4, 5, 6));
        }

        [Test]
        public void Chunk_ImmutableFilterMinDistByCell()
        {
            var a = new Chunk(
                positions: new[] { new V3d(0.4, 0.4, 0.4), new V3d(0.6, 0.6, 0.6), new V3d(1.1, 0.1, 0.1), new V3d(1.2, 0.2, 0.2) }
                );

            {
                var b = a.ImmutableFilterMinDistByCell(new Cell(0, 0, 0, 1), ParseConfig.Default.WithMinDist(1.0));
                ClassicAssert.IsTrue(b.Positions.Count == 2);
                ClassicAssert.IsTrue(b.Positions[0] == new V3d(0.4, 0.4, 0.4));
                ClassicAssert.IsTrue(b.Positions[1] == new V3d(1.1, 0.1, 0.1));
            }

            {
                var b = a.ImmutableFilterMinDistByCell(new Cell(0, 0, 0, 1), ParseConfig.Default.WithMinDist(0.5));
                ClassicAssert.IsTrue(b.Positions.Count == 3);
                ClassicAssert.IsTrue(b.Positions[0] == new V3d(0.4, 0.4, 0.4));
                ClassicAssert.IsTrue(b.Positions[1] == new V3d(0.6, 0.6, 0.6));
                ClassicAssert.IsTrue(b.Positions[2] == new V3d(1.1, 0.1, 0.1));
            }
        }

        [Test]
        public void Chunk_ImmutableFilterMinDistByCell_Parts()
        {
            var a = new Chunk(
                positions: new[] { new V3d(0.4, 0.4, 0.4), new V3d(0.6, 0.6, 0.6), new V3d(1.1, 0.1, 0.1), new V3d(1.2, 0.2, 0.2) },
                colors: null, normals: null, intensities: null, classifications: null,
                partIndices: new byte[] { 0, 1, 2, 3 }, partIndexRange: null,
                bbox: null
                );

            {
                var b = a.ImmutableFilterMinDistByCell(new Cell(0, 0, 0, 1), ParseConfig.Default.WithMinDist(1.0));
                ClassicAssert.IsTrue(b.Positions.Count == 2);
                var qs = b.PartIndices as byte[];
                ClassicAssert.NotNull(qs);
                ClassicAssert.IsTrue(qs[0] == 0);
                ClassicAssert.IsTrue(qs[1] == 2);
                ClassicAssert.IsTrue(b.HasPartIndices);
            }

            {
                var b = a.ImmutableFilterMinDistByCell(new Cell(0, 0, 0, 1), ParseConfig.Default.WithMinDist(0.5));
                ClassicAssert.IsTrue(b.Positions.Count == 3);
                var qs = b.PartIndices as byte[];
                ClassicAssert.NotNull(qs);
                ClassicAssert.IsTrue(qs[0] == 0);
                ClassicAssert.IsTrue(qs[1] == 1);
                ClassicAssert.IsTrue(qs[2] == 2);
                ClassicAssert.IsTrue(b.HasPartIndices);
            }
        }
    }
}
