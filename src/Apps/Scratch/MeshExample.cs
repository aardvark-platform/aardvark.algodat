using Aardvark.Base;
using Aardvark.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Scratch;

public static class MeshExample
{
    public static void Run()
    {
        var n = 2; // split into n*n quads

        var box = Box3d.Unit;

        var s = box.Size3d;
        var quads = new []
            {
                Split(new V3d(box.Min.X, box.Min.Y, box.Max.Z), s.XOO, s.OYO, n), // top
                Split(new V3d(box.Min.X, box.Min.Y, box.Min.Z), s.OYO, s.XOO, n), // bottom
                Split(new V3d(box.Min.X, box.Min.Y, box.Min.Z), s.XOO, s.OOZ, n), // front
                Split(new V3d(box.Min.X, box.Max.Y, box.Min.Z), s.OOZ, s.XOO, n), // back
                Split(new V3d(box.Max.X, box.Min.Y, box.Min.Z), s.OYO, s.OOZ, n), // right
                Split(new V3d(box.Min.X, box.Min.Y, box.Min.Z), s.OOZ, s.OYO, n), // left
            }
            .SelectMany(x => x)
            .ToArray()
            ;

        foreach (var q in quads)
        {
            Console.WriteLine(q);
        }

        var mesh = CreateMesh(quads);

        Console.WriteLine();
        Console.WriteLine($"#faces   : {mesh.FaceCount,8}");
        Console.WriteLine($"#edges   : {mesh.EdgeCount,8}");
        Console.WriteLine($"#vertices: {mesh.VertexCount,8}");
    }

    public static Quad3d[] Split(V3d o, V3d a, V3d b, int n)
    {
        var da = a / n; var db = b / n;
        var quads = new Quad3d[n * n];
        var qi = 0;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                quads[qi++] = new Quad3d(p0: o + i * da + j * db, edge01: da, edge03: db);
            }
        }
        return quads;
    }

    public static PolyMesh CreateMesh(Quad3d[] quads)
    {
        var v2i = new Dictionary<V3d, int>(); // vertex to index lookup (to deduplicate vertices)

        var m = new PolyMesh();
        foreach (var q in quads) m.AddFace(index(q.P0), index(q.P1), index(q.P2), index(q.P3));
        m.BuildTopology();
        return m;

        int index(V3d v)
        {
            if (!v2i.TryGetValue(v, out var i)) v2i[v] = i = m.AddVertex(v);
            return i;
        }
    }
}
