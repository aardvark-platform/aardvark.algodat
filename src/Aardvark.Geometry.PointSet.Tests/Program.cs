using Aardvark.Base;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Uncodium.SimpleStore;
using Aardvark.Geometry.Points;
using Aardvark.Data.E57;

namespace Aardvark.Geometry.Tests
{
    public unsafe class Program
    {
        internal static void TestE57()
        {
            var filename = @"T:\Vgm\Data\E57\Register360_Berlin Office_1.e57";
            var fileSizeInBytes = new FileInfo(filename).Length;
            var stream = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read);

            ASTM_E57.VerifyChecksums(stream, fileSizeInBytes);
            var header = ASTM_E57.E57FileHeader.Parse(stream);

            var data = header.E57Root.Data3D.Map(x => x.Points.ReadData().Take(1).ToList());

            //var ps = PointCloud.Parse(filename, ImportConfig.Default)
            //    .SelectMany(x => x.Positions)
            //    .ToArray()
            //    ;
        }

        public static void Main(string[] args)
        {
            TestE57();
        }
    }
}
