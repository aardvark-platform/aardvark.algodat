using Aardvark.Base;
using Aardvark.Data;
using Aardvark.Geometry.Points;
using Microsoft.FSharp.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Uncodium.SimpleStore;

#pragma warning disable IDE0060

namespace Scratch
{
    class Program
    {
        private static readonly Durable.Def[] DefsPositions = new[]
        {
            Durable.Octree.PositionsGlobal3d,
            Durable.Octree.PositionsGlobal3dReference,
            Durable.Octree.PositionsGlobal3f,
            Durable.Octree.PositionsGlobal3fReference,
            Durable.Octree.PositionsLocal3b,
            Durable.Octree.PositionsLocal3bReference,
            Durable.Octree.PositionsLocal3d,
            Durable.Octree.PositionsLocal3dReference,
            Durable.Octree.PositionsLocal3f,
            Durable.Octree.PositionsLocal3fReference,
            Durable.Octree.PositionsLocal3ui,
            Durable.Octree.PositionsLocal3uiReference,
            Durable.Octree.PositionsLocal3ul,
            Durable.Octree.PositionsLocal3ulReference,
            Durable.Octree.PositionsLocal3us,
            Durable.Octree.PositionsLocal3usReference,
        };
        private static readonly Durable.Def[] DefsColors = new[]
        {
            Durable.Octree.Colors3b,
            Durable.Octree.Colors3bReference,
            Durable.Octree.Colors4b,
            Durable.Octree.Colors4bDeprecated20201117,
            Durable.Octree.Colors4bReference,
            Durable.Octree.ColorsRGB565,
            Durable.Octree.ColorsRGB565Reference,
        };
        private static readonly Durable.Def[] DefsIntensities = new[]
        {
            Durable.Octree.Intensities1f,
            Durable.Octree.Intensities1fReference,
            Durable.Octree.Intensities1i,
            Durable.Octree.Intensities1iReference,
            Durable.Octree.IntensitiesWithOffset1f,
            Durable.Octree.IntensitiesWithOffset1fOffset,
            Durable.Octree.IntensitiesWithOffset1fRange,
            Durable.Octree.IntensitiesWithOffset1fReference,
        };
        private static readonly Durable.Def[] DefsClassifications = new[]
        {
            Durable.Octree.Classifications1b,
            Durable.Octree.Classifications1bReference,
            Durable.Octree.Classifications1i,
            Durable.Octree.Classifications1iReference,
            Durable.Octree.Classifications1s,
            Durable.Octree.Classifications1sReference,
        };

        private static readonly Durable.Def[] DefsNormals = new[]
        {
            Durable.Octree.Normals3f,
            Durable.Octree.Normals3fReference,
            Durable.Octree.Normals3sb,
            Durable.Octree.Normals3sbReference,
            Durable.Octree.NormalsOct16,
            Durable.Octree.NormalsOct16Reference,
            Durable.Octree.NormalsOct16P,
            Durable.Octree.NormalsOct16PReference,
        };

        private static readonly HashSet<Durable.Def> DefsRefs = new HashSet<Durable.Def>
        {
            Durable.Octree.Classifications1bReference,
            Durable.Octree.Classifications1iReference,
            Durable.Octree.Classifications1sReference,
            Durable.Octree.Colors3bReference,
            Durable.Octree.Colors4bReference,
            Durable.Octree.ColorsRGB565Reference,
            Durable.Octree.Densities1fReference,
            Durable.Octree.Intensities1fReference,
            Durable.Octree.Intensities1iReference,
            Durable.Octree.IntensitiesWithOffset1fReference,
            Durable.Octree.KdTreeIndexArrayReference,
            Durable.Octree.Normals3fReference,
            Durable.Octree.Normals3sbReference,
            Durable.Octree.NormalsOct16PReference,
            Durable.Octree.NormalsOct16Reference,
            Durable.Octree.PointRkdTreeDDataReference,
            Durable.Octree.PointRkdTreeFDataReference,
            Durable.Octree.PositionsGlobal3dReference,
            Durable.Octree.PositionsGlobal3fReference,
            Durable.Octree.PositionsLocal3bReference,
            Durable.Octree.PositionsLocal3dReference,
            Durable.Octree.PositionsLocal3fReference,
            Durable.Octree.PositionsLocal3uiReference,
            Durable.Octree.PositionsLocal3ulReference,
            Durable.Octree.PositionsLocal3usReference,
            Durable.Octree.Velocities3dReference,
            Durable.Octree.Velocities3fReference,
        };

        static void PointCloudStats(string storePath)
        {
            Report.Line();
            Report.BeginTimed($"{storePath}");

            var key = File.ReadAllText(Path.Combine(storePath, "key.txt"));

            using var storeRaw = new SimpleDiskStore(storePath);
            var store = storeRaw.ToPointCloudStore();

            var ps = store.GetPointSet(key);
            var root = ps.Root.Value;

            Report.Line($"key : {key}");
            Report.Line($"root: {root.Id}");

            Report.Line($"point count");
            Report.Line($"    root node: {root.PointCountCell,20:N0}");
            Report.Line($"    tree     : {root.PointCountTree,20:N0}");

            static void CheckDuplicateProperties(IPointCloudNode node, IEnumerable<Durable.Def> defs)
            {
                var exist = default(List<Durable.Def>);
                foreach (var def in defs)
                {
                    if (node.Properties.ContainsKey(def))
                    {
                        if (exist == null) exist = new List<Durable.Def>();
                        exist.Add(def);
                    }
                }
                if (exist?.Count > 1)
                {
                    Report.Begin($"duplicate properties in node {node.Id}");
                    foreach (var def in exist) Report.Line($"{def.Name}");
                    Report.End();
                }
            }

            Report.BeginTimed($"scanning nodes");
            var nodeCount           = 0L;
            var leafCount           = 0L;
            var nodeBlobsCount      = 0L;
            var nodeBlobsTotalBytes = 0L;
            var refBlobsCount       = 0L;
            var refBlobsTotalBytes  = 0L;

            root.ForEachNode(outOfCore: true, node =>
            {
                nodeCount++;
                if (node.IsLeaf) leafCount++;

                CheckDuplicateProperties(node, DefsClassifications);
                CheckDuplicateProperties(node, DefsColors);
                CheckDuplicateProperties(node, DefsIntensities);
                CheckDuplicateProperties(node, DefsNormals);
                CheckDuplicateProperties(node, DefsPositions);

                nodeBlobsCount++;
                nodeBlobsTotalBytes += storeRaw.Get(node.Id.ToString()).Length;

                var foo = new HashSet<Durable.Def>(node.Properties.Keys.Where(k => k.Name.EndsWith("Reference")));
                foreach (var k in foo)
                {
                    if (!DefsRefs.Contains(k)) throw new Exception($"DefsRefs does not contain {k}.");
                    var o = node.Properties[k];
                    var key = o switch
                    {
                        Guid x => x.ToString(),
                        string x => x,
                        _ => throw new Exception($"Unknown reference of type {o?.GetType()}")
                    };

                    refBlobsCount++;
                    refBlobsTotalBytes += storeRaw.Get(key).Length;
                }
            });
            Report.EndTimed();

            Report.Line();
            Report.Line($"node blobs");
            Report.Line($"    count       : {nodeBlobsCount,20:N0}");
            Report.Line($"    total bytes : {nodeBlobsTotalBytes,20:N0}");
            Report.Line($"ref blobs");
            Report.Line($"    count       : {refBlobsCount,20:N0}");
            Report.Line($"    total bytes : {refBlobsTotalBytes,20:N0}");
            Report.Line($"TOTAL");
            Report.Line($"    count       : {nodeBlobsCount + refBlobsCount,20:N0}");
            Report.Line($"    total bytes : {nodeBlobsTotalBytes + refBlobsTotalBytes,20:N0}");
            Report.Line();
            Report.Line($"node counts");
            Report.Line($"    total       : {nodeCount,20:N0}");
            Report.Line($"    leafs       : {leafCount,20:N0}");


            Report.EndTimed();
        }

        static void Main(string[] args)
        {
            var storePaths = new[]
            {
                @"T:\Vgm\Stores\Innenscan_FARO.e57_5.0.24",
                @"T:\Vgm\Stores\Innenscan_FARO.e57_5.0.28-prerelease0005"
            };

            foreach (var x in storePaths)
            {
                PointCloudStats(storePath: x);
            }
            
        }
    }
}
