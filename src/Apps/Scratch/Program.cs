using Aardvark.Geometry.Points;
using System;

namespace Scratch
{
    class Program
    {
        static void Main(string[] args)
        {
            var filename = @"T:\Vgm\Data\E57\JBs_Haus.e57";
            var foo = PointCloud.Import(filename);
            Console.WriteLine(foo.PointCount);
        }
    }
}
