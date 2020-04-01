/*
    Copyright (C) 2006-2020. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aardvark.Geometry
{
    public partial class PolyMesh
    {
        public class IAttributeArray
        {
            public IAttribute[] Array;
            public int Count;
            public Type Type;

            public IAttributeArray(Type type, int length)
            {
                Type = type;
                Array = new IAttribute[length];
                Count = 0;
            }

            public bool HasMatchingTypes => Array.All(a => a.ValueType == Type);
        }

        public static readonly SymbolDict<object> GroupingDefaultInstanceValues =
            new SymbolDict<object>
            {
                { PolyMesh.Property.Colors, C4f.White },
            };

        public class Grouping
        {
            public IEnumerable<PolyMesh> Meshes;
            public HashSet<PolyMesh> NonMatchingMeshes;
            public int MeshCount;
            public bool MatchingInstanceAttributeTypes;
            public bool InstanceAsFaceAttributes;
            public bool MatchingFaceAttributes;
            public bool MatchingVertexAttributes;
            public bool MatchingFaceVertexAttributes;
            public SymbolDict<Array> InstanceAttributeArrays;
            public SymbolDict<IAttributeArray> FaceAttributeArrays;
            public SymbolDict<IAttributeArray> VertexAttributeArrays;
            public SymbolDict<IAttributeArray> FaceVertexAttributeArrays;
            public IEnumerable<IGrouping<PolyMesh, Face>> Groups;

            public bool Valid
            {
                get
                {
                    return MatchingInstanceAttributeTypes
                            && InstanceAsFaceAttributes
                            && MatchingFaceAttributes
                            && MatchingVertexAttributes
                            && MatchingFaceVertexAttributes;
                }
            }

            public IEnumerable<PolyMesh> MatchingMeshes
            {
                get
                {
                    foreach (var m in Meshes)
                        if (!NonMatchingMeshes.Contains(m))
                            yield return m;
                }
            }

            public PolyMesh Mesh
            {
                get
                {
                    var mGroups = Groups;

                    var mc = mGroups.Count();

                    // compute face offsets for all meshes, face count
                    var foa = new int[mc + 1];
                    mGroups.ForEach((mg, mi) => foa[mi] = mg.Count());
                    int fc = foa.Integrate();

                    // compute face back maps for all meshes
                    var fbma = new int[mc][];
                    mGroups.ForEach((mg, mi) => // PAR, maybe too small a task)
                    {
                        int mfc = foa[mi + 1] - foa[mi];
                        var fbm = new int[mfc];
                        mg.ForEach((mf, mfi) => fbm[mfi] = mf.Index);
                        fbma[mi] = fbm;
                    });

                    // compute mesh array
                    var ma = new PolyMesh[mc];
                    mGroups.ForEach((mg, mi) => ma[mi] = mg.Key);

                    // compute face vertex offsets for all meshes
                    var fvoa = new int[mc + 1];
                    for (int mi = 0; mi < mc; mi++)
                    {
                        var m = ma[mi];
                        int[] mfia = m.FirstIndexArray;
                        int mfvc = 0;
                        foreach (var mfi in fbma[mi])
                            mfvc += mfia[mfi + 1] - mfia[mfi];
                        fvoa[mi] = mfvc;
                    }
                    int vic = fvoa.Integrate();

                    // compute vertex back maps and vertex offsets for all meshes, vertex count
                    var via = new int[vic];
                    var vbma = new int[mc][];
                    var voa = new int[mc + 1];
                    int voff = 0;
                    for (int mi = 0; mi < mc; mi++) // PAR, maybe too small a task
                    {
                        var m = ma[mi];
                        int[] mfia = m.FirstIndexArray, mvia = m.VertexIndexArray;
                        var vfm = new int[m.VertexCount].Set(-1);
                        int mvc = 0, mfc = fbma[mi].Length;
                        int fvi = fvoa[mi];
                        foreach (var mfi in fbma[mi])
                        {
                            int mfvi = mfia[mfi], mfve = mfia[mfi + 1];
                            while (mfvi < mfve)
                            {
                                int mvi = mvia[mfvi++];
                                int vi = vfm[mvi];
                                if (vi < 0) vfm[mvi] = vi = mvc++;
                                via[fvi++] = vi + voff;
                            }
                        }
                        vbma[mi] = vfm.CreateBackMap(mvc);
                        voa[mi] = mvc;
                        voff += mvc;
                    }
                    int vc = voa.Integrate();

                    // initialize first index array
                    var fia = new int[fc + 1];
                    for (int mi = 0; mi < mc; mi++)
                    {
                        var m = ma[mi]; var mfia = m.FirstIndexArray;
                        int fi = foa[mi];
                        int fvi = fvoa[mi];
                        foreach (var mfi in fbma[mi])
                        {
                            fia[fi++] = fvi; fvi += mfia[mfi + 1] - mfia[mfi];
                        }
                    }
                    fia[fc] = vic;

                    #region Handling Attributes

                    var faceAttributes = new SymbolDict<Array>(FaceAttributeArrays.Count);

                    if (InstanceAttributeArrays.Count > 0)
                    {
                        var indexArray = new int[fc];

                        for (int fi = 0, mi = 0; mi < mc; mi++)
                            for (int fe = foa[mi+1]; fi < fe; fi++)
                                indexArray[fi] = mi;

                        foreach (var kvp in InstanceAttributeArrays)
                        {
                            faceAttributes[kvp.Key] = indexArray;
                            faceAttributes[-kvp.Key] = kvp.Value;
                        }
                    }

                    foreach (var kvp in FaceAttributeArrays)
                    {
                        var aa = kvp.Value.Array;
                        if (aa.All(a => a.IndexArray == null))
                        {
                            var va = Array.CreateInstance(kvp.Value.Type, fc);
                            for (int mi = 0; mi < mc; mi++)
                                aa[mi].BackMappedCopyTo(fbma[mi], va, foa[mi]);
                            faceAttributes[kvp.Key] = va;
                        }
                        else
                        {
                            var ac = 0;
                            var afma = new int[mc][];
                            for (int mi = 0; mi < mc; mi++)
                            {
                                var mia = aa[mi].IndexArray;
                                var afm = new int[aa[mi].ValueArray.Length].Set(-1);
                                var fbm = fbma[mi];
                                if (mia != null)
                                {
                                    for (int fi = 0; fi < fbm.Length; fi++)
                                        afm.ForwardMapAdd(mia[fbm[fi]], ref ac);
                                }
                                else
                                {
                                    for (int fi = 0; fi < fbm.Length; fi++)
                                        afm[fi] = ac++;
                                }
                                afma[mi] = afm;
                            }

                            var indexArray = new int[fc];
                            var valueArray = Array.CreateInstance(kvp.Value.Type, ac);

                            for (int mi = 0; mi < mc; mi++)
                            {
                                var mia = aa[mi].IndexArray;
                                int[] afm = afma[mi], fbm = fbma[mi];
                                var fo = foa[mi];
                                if (mia != null)
                                {
                                    for (int fi = 0; fi < fbma.Length; fi++)
                                        indexArray[fo + fi] = afm[mia[fbm[fi]]];
                                }
                                else
                                {
                                    for (int fi = 0; fi < fbma.Length; fi++)
                                        indexArray[fo + fi] = afm[fi];
                                }
                                aa[mi].ForwardMappedCopyTo(afm, valueArray, 0);
                            }

                            faceAttributes[kvp.Key] = indexArray;
                            faceAttributes[-kvp.Key] = valueArray;
                        }
                    }

                    var vertexAttributes = new SymbolDict<Array>(VertexAttributeArrays.Count);

                    foreach (var kvp in VertexAttributeArrays)
                    {
                        var aa = kvp.Value.Array;
                        if (aa.All(a => a.IndexArray == null))
                        {
                            var va = Array.CreateInstance(kvp.Value.Type, vc);
                            for (int mi = 0; mi < mc; mi++)
                                aa[mi].BackMappedCopyTo(vbma[mi], va, voa[mi]);
                            vertexAttributes[kvp.Key] = va;
                        }
                        else
                        {
                            var ac = 0;
                            var afma = new int[mc][];
                            for (int mi = 0; mi < mc; mi++)
                            {
                                var mia = aa[mi].IndexArray;
                                var afm = new int[aa[mi].ValueArray.Length].Set(-1);
                                var vbm = vbma[mi];
                                if (mia != null)
                                {
                                    for (int vi = 0; vi < vbm.Length; vi++)
                                        afm.ForwardMapAdd(mia[vbm[vi]], ref ac);
                                }
                                else
                                {
                                    for (int vi = 0; vi < vbm.Length; vi++)
                                        afm[vi] = ac++;
                                }
                                afma[mi] = afm;
                            }

                            var indexArray = new int[vc];
                            var valueArray = Array.CreateInstance(kvp.Value.Type, ac);

                            for (int mi = 0; mi < mc; mi++)
                            {
                                var mia = aa[mi].IndexArray;
                                int[] afm = afma[mi], vbm = vbma[mi];
                                var vo = voa[mi];
                                if (mia != null)
                                {
                                    for (int vi = 0; vi < vbm.Length; vi++)
                                        indexArray[vo + vi] = afm[mia[vbm[vi]]];
                                }
                                else
                                {
                                    for (int vi = 0; vi < vbma.Length; vi++)
                                        indexArray[vo + vi] = afm[vi];
                                }
                                aa[mi].ForwardMappedCopyTo(afm, valueArray, 0);
                            }

                            vertexAttributes[kvp.Key] = indexArray;
                            vertexAttributes[-kvp.Key] = valueArray;
                        }
                    }

                    var faceVertexAttributes = new SymbolDict<Array>(FaceVertexAttributeArrays.Count);

                    foreach (var kvp in FaceVertexAttributeArrays)
                    {
                        var aa = kvp.Value.Array;
                        if (aa.All(a => a.IndexArray == null))
                        {
                            var va = Array.CreateInstance(kvp.Value.Type, vic);
                            for (int mi = 0; mi < mc; mi++)
                                aa[mi].BackMappedGroupCopyTo(
                                        fbma[mi], fc, ma[mi].VertexIndexArray, va, fvoa[mi]);
                            faceVertexAttributes[kvp.Key] = va;
                        }
                        else
                        {
                            var ac = 0;
                            var afma = new int[mc][];
                            for (int mi = 0; mi < mc; mi++)
                            {
                                var mia = aa[mi].IndexArray;
                                var afm = new int[aa[mi].ValueArray.Length].Set(-1);
                                var fbm = fbma[mi];
                                var fvo = fvoa[mi];
                                var mfia = ma[mi].FirstIndexArray;
                                if (mia != null)
                                {
                                    for (int fi = 0; fi < fbm.Length; fi++)
                                    {
                                        var ofi = fbm[fi];
                                        int ofvi = mfia[ofi], ofve = mfia[ofi + 1];
                                        while (ofvi < ofve)
                                            afm.ForwardMapAdd(mia[ofvi++], ref ac);
                                    }
                                }
                                else
                                {
                                    var fve = fvoa[mi + 1];
                                    for (int fvi = 0; fvo < fve; fvo++, fvi++)
                                        afm[fvi] = fvo;
                                }
                                afma[mi] = afm;
                            }

                            var indexArray = new int[vic];
                            var valueArray = Array.CreateInstance(kvp.Value.Type, ac);

                            for (int mi = 0; mi < mc; mi++)
                            {
                                var mia = aa[mi].IndexArray;
                                int[] afm = afma[mi], fbm = fbma[mi];
                                var fvo = fvoa[mi];
                                var mfia = ma[mi].FirstIndexArray;
                                if (mia != null)
                                {
                                    for (int fi = 0; fi < fbm.Length; fi++)
                                    {
                                        var ofi = fbm[fi];
                                        int ofvi = mfia[ofi], ofve = mfia[ofi + 1];
                                        while (ofvi < ofve)
                                            indexArray[fvo++] = afm[mia[ofvi++]];
                                    }
                                }
                                else
                                {
                                    var fve = fvoa[mi + 1];
                                    for (int fvi = 0; fvo < fve; fvo++, fvi++)
                                        indexArray[fvo] = afm[fvi];
                                }
                                aa[mi].ForwardMappedCopyTo(afm, valueArray, 0);
                            }

                            faceVertexAttributes[kvp.Key] = indexArray;
                            faceVertexAttributes[-kvp.Key] = valueArray;
                        }
                    }

                    #endregion

                    return new PolyMesh
                        {
                            FirstIndexArray = fia,
                            VertexIndexArray = via,
                            FaceAttributes = faceAttributes,
                            VertexAttributes = vertexAttributes,
                            FaceVertexAttributes = faceVertexAttributes,
                        };
                }
            }
        }

    }

    public static class PolyMeshGroupingExtensions
    {
        public static PolyMesh.Grouping Group(
                this IEnumerable<PolyMesh> meshes, SymbolDict<object> defaultInstanceValues = null)
        {
            return Group(from m in meshes from f in m.Faces select f, defaultInstanceValues);
        }

        public static PolyMesh.Grouping Group(
                this IEnumerable<PolyMesh.Face> faces, SymbolDict<object> defaultInstanceValues = null)
        {
            var groups = from face in faces group face by face.Mesh;

            var mc = groups.Count();

            var instanceAttributeArrays = new SymbolDict<Array>();
            var faceAttributeArrays = new SymbolDict<PolyMesh.IAttributeArray>();
            var vertexAttributeArrays = new SymbolDict<PolyMesh.IAttributeArray>();
            var faceVertexAttributeArrays = new SymbolDict<PolyMesh.IAttributeArray>();

            bool matchingInstanceAttributeTypes = true;

            if (defaultInstanceValues == null)
                defaultInstanceValues = PolyMesh.GroupingDefaultInstanceValues;

            var nonMatchingMeshes = new HashSet<PolyMesh>();

            groups.ForEach((mg, mi) =>
            {
                var m = mg.Key;

                foreach (var kvp in m.InstanceAttributes)
                {
                    var type = kvp.Value.GetType();
                    if (!instanceAttributeArrays.TryGetValue(kvp.Key, out Array a))
                        instanceAttributeArrays[kvp.Key]
                                = a = Array.CreateInstance(type, mc);
                    if (type != a.GetType().GetElementType())
                    {
                        matchingInstanceAttributeTypes = false;
                        nonMatchingMeshes.Add(m);
                    }
                    a.SetValue(kvp.Value, mi);
                }

                foreach (var a in m.FaceIAttributes)
                {
                    if (!faceAttributeArrays.TryGetValue(a.Name, out PolyMesh.IAttributeArray ca))
                        faceAttributeArrays[a.Name] = ca =
                                new PolyMesh.IAttributeArray(a.ValueType, mc);
                    if (ca.Type != a.ValueType)
                        nonMatchingMeshes.Add(m);
                    ca.Array[mi] = a; ca.Count++;
                }
                foreach (var a in m.VertexIAttributes)
                {
                    if (!vertexAttributeArrays.TryGetValue(a.Name, out PolyMesh.IAttributeArray ca))
                        vertexAttributeArrays[a.Name] = ca =
                                new PolyMesh.IAttributeArray(a.ValueType, mc);
                    if (ca.Type != a.ValueType)
                        nonMatchingMeshes.Add(m);
                    ca.Array[mi] = a; ca.Count++;
                }
                foreach (var a in m.FaceVertexIAttributes)
                {
                    if (!faceVertexAttributeArrays.TryGetValue(a.Name, out PolyMesh.IAttributeArray ca))
                        faceVertexAttributeArrays[a.Name] = ca =
                                new PolyMesh.IAttributeArray(a.ValueType, mc);
                    if (ca.Type != a.ValueType)
                        nonMatchingMeshes.Add(m);
                    ca.Array[mi] = a; ca.Count++;
                }
            });

            groups.ForEach((mg, mi) =>
            {
                var m = mg.Key;
                foreach (var ca in faceAttributeArrays)
                {
                    if (!m.FaceAttributes.Contains(ca.Key))
                        nonMatchingMeshes.Add(m);
                }

                foreach (var ca in vertexAttributeArrays)
                {
                    if (!m.VertexAttributes.Contains(ca.Key))
                        nonMatchingMeshes.Add(m);
                }

                foreach (var ca in faceVertexAttributeArrays)
                {
                    if (!m.FaceVertexAttributes.Contains(ca.Key))
                        nonMatchingMeshes.Add(m);
                }
            });

            bool instanceAsFaceAttributes = true;
            foreach (var kvp in instanceAttributeArrays)
            {
                if (faceAttributeArrays.Contains(kvp.Key))
                    instanceAsFaceAttributes = false;
            }

            return new PolyMesh.Grouping
            {
                Meshes = from g in groups select g.Key,
                NonMatchingMeshes = nonMatchingMeshes,
                MeshCount = mc,
                MatchingInstanceAttributeTypes = matchingInstanceAttributeTypes,
                InstanceAsFaceAttributes = instanceAsFaceAttributes,
                MatchingFaceAttributes = faceAttributeArrays.Values.All(
                        iaa => iaa.Count == mc && iaa.HasMatchingTypes),
                MatchingVertexAttributes = vertexAttributeArrays.Values.All(
                        iaa => iaa.Count == mc && iaa.HasMatchingTypes),
                MatchingFaceVertexAttributes = faceVertexAttributeArrays.Values.All(
                        iaa => iaa.Count == mc && iaa.HasMatchingTypes),
                Groups = groups,
                InstanceAttributeArrays = instanceAttributeArrays,
                FaceAttributeArrays = faceAttributeArrays,
                VertexAttributeArrays = vertexAttributeArrays,
                FaceVertexAttributeArrays = faceVertexAttributeArrays,
            };
        }
    }
}
