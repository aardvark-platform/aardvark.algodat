using Aardvark.Base;
using Aardvark.Geometry.Points;
using Aardvark.Geometry.Quadtree;
using Microsoft.FSharp.Core;
using System;

namespace Scratch
{
    class Program
    {
        static void Quadtree1()
        {
            var data = new[] {
                1.0, 1.0, 2.0, 2.0,
                1.5, 1.6, 1.7, 1.8,
                1.6, 1.7, 2.0, 2.2,
                };

            var mapping = new LayerMapping(origin: new Cell2d(0, 0, 0), width: 4, height: 3);

            var layer = Layer.Create(Defs.Quadtree.Heights1d, data, mapping);

            var w = ((ILayer)layer).WithWindow(new Box2l(new V2l(1, 0), new V2l(3, 2)));

            var q = Quadtree.Create(layer);
        }

        static void Quadtree2()
        {
            var width = 3000;
            var height = 2000;

            Report.Line($"create quadtree from raster");
            Report.Line($"width {width:N0} x height {height:N0}");
            Report.Line($"total samples ... {width*height:N0}");
            Report.BeginTimed($"generate random height values");
            var r = new Random();
            var data = new double[width * height];
            for (var i = 0; i < data.Length; i++) data[i] = r.NextDouble();
            Report.EndTimed();
            

            var mapping = new LayerMapping(origin: new Cell2d(0, 0, 0), width, height);

            var layer = Layer.Create(Defs.Quadtree.Heights1d, data, mapping);

            //var layerWindow = ((ILayer)layer).WithWindow(new Box2l(new V2l(100, 200), new V2l(2500, 1800)));
            //var q = Quadtree.Create(layerWindow.Value);

            Report.BeginTimed($"building quadtree");
            var q = Quadtree.Create(layer);
            Report.EndTimed();

            Report.Line($"# nodes ... {Quadtree.Count(FSharpOption<INode>.Some(q))}");
            Report.Line($"# inner ... {Quadtree.CountInner(FSharpOption<INode>.Some(q))}");
            Report.Line($"# leafs ... {Quadtree.CountLeafs(FSharpOption<INode>.Some(q))}");
        }

        static void Main(string[] args)
        {
            //var filename = @"T:\Vgm\Data\E57\JBs_Haus.e57";
            //var foo = PointCloud.Import(filename);
            //Console.WriteLine(foo.PointCount);

            Quadtree2();

            //var c = new Cell2d(1, 2, 8);
            //var bounds = c.GetBoundsForExponent(0);
            //Console.WriteLine($"{bounds}");
            //Console.WriteLine($"{bounds.Center}");

            //Console.WriteLine($"{new Box2l(0,0,256,256).Intersection(new Box2l(256,0,256,512))}");
        }
    }
}
