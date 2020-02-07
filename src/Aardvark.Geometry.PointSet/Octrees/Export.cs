/*
    Copyright (C) 2006-2019. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class ExportExtensions
    {
        /// <summary>
        /// </summary>
        public class ExportPointSetInfo
        {
            /// <summary>
            /// Number of points in exported tree (sum of leaf points).
            /// </summary>
            public readonly long PointCountTree;

            /// <summary>
            /// Number of leaf points already processed.
            /// </summary>
            public readonly long ProcessedLeafPointCount;

            public ExportPointSetInfo(long pointCountTree, long processedLeafPointCount = 0L)
            {
                PointCountTree = pointCountTree;
                ProcessedLeafPointCount = processedLeafPointCount;
            }

            /// <summary>
            /// Progress [0,1].
            /// </summary>
            public double Progress => (double)ProcessedLeafPointCount / (double)PointCountTree;

            /// <summary>
            /// Returns new ExportPointSetInfo with ProcessedLeafPointCount incremented by x. 
            /// </summary>
            public ExportPointSetInfo AddProcessedLeafPoints(long x)
                => new ExportPointSetInfo(PointCountTree, ProcessedLeafPointCount + x);
        }

        /// <summary>
        /// Exports complete pointset (metadata, nodes, referenced blobs) to another store.
        /// </summary>
        public static ExportPointSetInfo ExportPointSet(
            this Storage self, 
            string pointSetId, 
            Storage exportStorage, 
            Action<ExportPointSetInfo> onProgress, 
            bool verbose, CancellationToken ct
            )
        {
            PointSet pointSet = null;

            try
            {
                pointSet = self.GetPointSet(pointSetId);
                if (pointSet == null)
                {
                    Report.Warn($"No PointSet with id '{pointSetId}' in store. Trying to load node with this id.");
                }
            }
            catch
            {
                Report.Warn($"Entry with id '{pointSetId}' is not a PointSet. Trying to load node with this id.");
            }

            if (pointSet == null)
            {
                var (success, root) = self.TryGetPointCloudNode(pointSetId);
                if (success)
                {
                    var ersatzPointSetKey = Guid.NewGuid().ToString();
                    Report.Warn($"Created PointSet with key '{ersatzPointSetKey}'.");
                    var ersatzPointSet = new PointSet(self, ersatzPointSetKey, root, root.PointCountCell);
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
            if (onProgress == null) onProgress = _ => { };

            var info = new ExportPointSetInfo(pointset.Root.Value.PointCountTree);

            var pointSetId = pointset.Id;
            var root = pointset.Root.Value;
            var totalNodeCount = root.CountNodes(outOfCore: true);
            if (verbose) Report.Line($"total node count = {totalNodeCount:N0}");

            // export pointset metainfo
            exportStorage.Add(pointSetId, pointset.Encode());
            // Report.Line($"exported {pointSetId} (pointset metainfo, json)");

            // export octree (recursively)
            var exportedNodeCount = 0L;
            ExportNode(root.Id);
            if (verbose) Console.Write("\r");
            return info;

            void ExportNode(Guid key)
            {
                ct.ThrowIfCancellationRequested();

                // missing subnode (null) is encoded as Guid.Empty
                if (key == Guid.Empty) return;

                // try to load node
                Durable.Def def = Durable.Octree.Node;
                object raw = null;
                try
                {
                    (def, raw) = self.GetDurable(key);
                }
                catch
                {
                    var n = self.GetPointCloudNode(key);
                    raw = n.Properties;
                }
                var nodeProps = raw as IReadOnlyDictionary<Durable.Def, object>;
                exportStorage.Add(key, def, nodeProps, false);
                //Report.Line($"exported {key} (node)");

                // references
                var rs = GetReferences(nodeProps);
                foreach (var kv in rs)
                {
                    var k = (Guid)kv.Value;
                    var buffer = self.GetByteArray(k);
                    exportStorage.Add(k, buffer);
                    //Report.Line($"exported {k} (reference)");
                }

                exportedNodeCount++;
                if (verbose) Console.Write($"\r{exportedNodeCount}/{totalNodeCount}");

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
                        info = info.AddProcessedLeafPoints((int)pointCountCell);
                    }
                    else
                    {
                        Report.Warn("Invariant 2f7bb751-e6d4-4d4a-98a3-eabd6fd9b156.");
                    }
                    
                    onProgress(info);
                }
            }

            IDictionary<Durable.Def, object> GetReferences(IReadOnlyDictionary<Durable.Def, object> node)
            {
                var rs = new Dictionary<Durable.Def, object>();
                foreach (var kv in node)
                {
                    if (kv.Key == Durable.Octree.NodeId) continue;

                    if (kv.Key.Type == Durable.Primitives.GuidDef.Id)
                    {
                        rs[kv.Key] = kv.Value;
                    }
                }
                return rs;
            }
        }
    }
}
