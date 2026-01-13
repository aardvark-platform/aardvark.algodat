using Aardvark.Geometry.Tests;
using System;
using System.IO;

namespace Aardvark.Algodat.Tests.Bugs;

internal static class Bug20260112
{
    public static void Run()
    {
        var filename = @"W:\Datasets\Vgm\Data\2026-01-12_bugreport\aussen_color.e57";
        if (File.Exists(filename))
        {
            Program.ParsePointCloudFile(filename, verbose: true);
        }
        else
        {
            Console.WriteLine($"File not found: {filename}");
        }
    }
}
