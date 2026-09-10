/*
    Copyright (C) 2006-2024. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using Aardvark.Data;
using Aardvark.Data.Points;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public static class ExportExtensions
{
    /// <summary>
    /// </summary>
    public class ExportPointSetInfo(
        long pointCountTree,
        long processedLeafPointCount = 0L
        )
    {
        /// <summary>
        /// Number of points in the exported backing tree (sum of leaf points).
        /// For filtered views this includes unselected points needed by the view.
        /// </summary>
        public readonly long PointCountTree = pointCountTree;

        /// <summary>
        /// Number of backing-tree leaf points whose nodes and blobs have been copied.
        /// </summary>
        public readonly long ProcessedLeafPointCount = processedLeafPointCount;

        /// <summary>
        /// Progress [0,1]. A completed empty export has progress 1.
        /// </summary>
        public double Progress => PointCountTree == 0 ? 1 : (double)ProcessedLeafPointCount / PointCountTree;

        /// <summary>
        /// Returns new ExportPointSetInfo with ProcessedLeafPointCount incremented by x. 
        /// </summary>
        public ExportPointSetInfo AddProcessedLeafPoints(long x)
            => new(PointCountTree, ProcessedLeafPointCount + x);
    }

    /// <summary>
    /// Exports a pointset and its persisted reference closure to another store,
    /// preserving node IDs, filter definitions, and attribute/kd-tree payloads.
    /// Filtered and nested views retain their complete backing trees. Export by
    /// pointset ID does not evaluate filters, materialize selected arrays, or visit
    /// transient children. The node-ID compatibility fallback may decode the root.
    /// Progress counts backing-tree leaf points, not the view's selected-point estimate.
    /// The completion callback (including for empty exports) runs only after all
    /// dependencies have been copied. Cancellation may leave a partial destination.
    /// </summary>
    public static ExportPointSetInfo ExportPointSet(
        this Storage self, 
        string pointSetId, 
        Storage exportStorage, 
        Action<ExportPointSetInfo> onProgress, 
        bool verbose, CancellationToken ct
        )
    {
        ct.ThrowIfCancellationRequested();
        PointSet? pointSet = null;

        try
        {
            // GetPointSet/PointSet.Parse resolve the root, which evaluates filters.
            // Read only the metadata here and leave the root reference lazy.
            if (self.HasCache && self.Cache.TryGetValue(pointSetId, out var cached) && cached is PointSet cachedPointSet)
            {
                pointSet = cachedPointSet;
            }
            else if (self.GetByteArray(pointSetId) is { } buffer)
            {
                var json = JsonNode.Parse(buffer, new JsonNodeOptions { PropertyNameCaseInsensitive = true })!;
                var id = (string?)json["Id"] ?? throw new InvalidOperationException("Missing pointset Id.");
                var rootId = (string?)json["OctreeId"] ?? (string?)json["RootCellId"];
                var splitLimit = json.AsObject().TryGetPropertyValue("SplitLimit", out var x) ? (int)x! : 8192;
                pointSet = new PointSet(self, id, string.IsNullOrEmpty(rootId) ? Guid.Empty : Guid.Parse(rootId), splitLimit);
                if (self.HasCache) self.Cache.Add(pointSetId, pointSet, buffer.Length, onRemove: default);
            }
            if (pointSet == null)
            {
                Report.Warn($"No PointSet with id '{pointSetId}' in store. Trying to load node with this id.");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            ct.ThrowIfCancellationRequested();
            Report.Warn($"Entry with id '{pointSetId}' is not a PointSet. Trying to load node with this id.");
        }

        if (pointSet == null)
        {
            ct.ThrowIfCancellationRequested();
            var found = self.TryGetPointCloudNode(pointSetId, out var root);
            ct.ThrowIfCancellationRequested();
            if (found)
            {
                var ersatzPointSetKey = Guid.NewGuid().ToString();
                Report.Warn($"Created PointSet with key '{ersatzPointSetKey}'.");
                var ersatzPointSet = new PointSet(self, ersatzPointSetKey, root!, root!.PointCountCell);
                self.Add(ersatzPointSetKey, ersatzPointSet);

                return ExportPointSet(self, ersatzPointSet, exportStorage, onProgress, verbose, ct);
            }
            else
            {
                throw new Exception($"No node with id '{pointSetId}' in store. Giving up. Invariant 48028b00-4538-4169-a2fc-ca009d56e012.");
            }
        }
        else
        {
            return ExportPointSet(self, pointSet, exportStorage, onProgress, verbose, ct);
        }
    }

    /// <summary>
    /// Exports complete pointset (metadata, nodes, referenced blobs) to another store.
    /// </summary>
    private static ExportPointSetInfo ExportPointSet(
        this Storage self, 
        PointSet pointset, 
        Storage exportStorage, 
        Action<ExportPointSetInfo> onProgress, 
        bool verbose, CancellationToken ct
        )
    {
        ct.ThrowIfCancellationRequested();
        onProgress ??= _ => { };

        ExportPointSetInfo? info = null;

        exportStorage.Add(pointset.Id, pointset.Encode());

        // Stream persisted references; a separate node-count pass would evaluate
        // filtered children and double the ordinary-tree traversal.
        var exportedNodeCount = 0L;
        ExportNode(Guid.Parse(pointset.Root.Id));
        ct.ThrowIfCancellationRequested();
        var total = info?.PointCountTree ?? 0;
        info = new ExportPointSetInfo(total, total);
        onProgress(info);
        if (verbose) Console.Write("\r");
        return info;

        void ExportNode(Guid key)
        {
            ct.ThrowIfCancellationRequested();

            // missing subnode (null) is encoded as Guid.Empty
            if (key == Guid.Empty) return;

            // try to load node
            var def = Durable.Octree.Node;
            object? raw = null;
            try
            {
                var buffer = self.GetByteArray(key);
                if (buffer != null) (def, raw) = Data.Codec.Deserialize(StorageExtensions.UnGZip(buffer));
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                ct.ThrowIfCancellationRequested();
                var n = self.GetPointCloudNode(key)!;
                raw = n.Properties;
            }

            if (raw is not IReadOnlyDictionary<Durable.Def, object> nodeProps)
                throw new InvalidOperationException($"Missing node map {key}.");

            var isWrapper = nodeProps.TryGetValue(FilteredNode.Defs.FilteredNodeRootId, out var backingRoot);
            if (!isWrapper && info == null)
            {
                var count = nodeProps.TryGetValue(Durable.Octree.PointCountTreeLeafs, out var treeCount)
                    ? (long)treeCount
                    : self.GetPointCloudNode(key).PointCountTree;
                info = new ExportPointSetInfo(count);
            }

            ct.ThrowIfCancellationRequested();
            exportStorage.Add(key, def, nodeProps, false);

            // Copy opaque payloads directly, without a per-node reference dictionary.
            foreach (var kv in nodeProps)
            {
                if (kv.Key == Durable.Octree.NodeId || kv.Key == FilteredNode.Defs.FilteredNodeRootId) continue;
                if (kv.Key.Type != Durable.Primitives.GuidDef.Id) continue;
                var k = (Guid)kv.Value;
                if (k == Guid.Empty) continue;
                ct.ThrowIfCancellationRequested();
                var buffer = self.GetByteArray(k) ?? throw new InvalidOperationException($"Missing referenced blob {k}.");
                ct.ThrowIfCancellationRequested();
                exportStorage.Add(k, buffer);
            }

            exportedNodeCount++;
            if (verbose) Console.Write($"\r{exportedNodeCount} nodes exported");

            // Wrappers are not leaves. Their root is another node (possibly another
            // wrapper), not an opaque blob or a transient filtered-child reference.
            if (isWrapper)
            {
                ExportNode((Guid)backingRoot!);
                return;
            }

            // children
            nodeProps.TryGetValue(Durable.Octree.SubnodesGuids, out var subnodeGuids);
            if (subnodeGuids != null)
            {
                foreach (var x in (Guid[])subnodeGuids) ExportNode(x);
            }
            else
            {
                if (nodeProps.TryGetValue(Durable.Octree.PointCountCell, out var pointCountCell))
                {
                    info = info!.AddProcessedLeafPoints((int)pointCountCell);
                }
                else
                {
                    Report.Warn("Invariant 2f7bb751-e6d4-4d4a-98a3-eabd6fd9b156.");
                }
                
                // Defer progress 1 until traversal has returned: later zero-point
                // leaves may still have dependencies, and an empty tree has no leaves.
                if (info!.ProcessedLeafPointCount < info.PointCountTree) onProgress(info);
            }
        }
    }
}
