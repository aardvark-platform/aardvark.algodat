using Aardvark.Base;
using Aardvark.Geometry.Points;
using Aardvark.Geometry.Quadtree;
using System;

namespace Scratch
{
    class Program
    {
        static void Main(string[] args)
        {
            //var filename = @"T:\Vgm\Data\E57\JBs_Haus.e57";
            //var foo = PointCloud.Import(filename);
            //Console.WriteLine(foo.PointCount);

            var data = new[] {
                1.0, 1.0, 2.0, 2.0,
                1.5, 1.6, 1.7, 1.8,
                1.6, 1.7, 2.0, 2.2,
                };

            var mapping = new LayerMapping(origin: new Cell2d(500_000, 2_000, 0), width: 4, height: 3);

            var layer = Layer.Create(Defs.Quadtree.Heights1d, data, mapping);

            var w = Layer.Window(new Box2l(new V2l(1, 0), new V2l(3, 2)), layer);
        }
    }
}
