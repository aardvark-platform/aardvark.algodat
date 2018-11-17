/*
    Copyright (C) 2006-2018. Aardvark Platform Team. http://github.com/aardvark-platform.
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
#define PARANOID

using Aardvark.Base;
using Aardvark.Data.Points;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public static class Merge
    {
        /// <summary>
        /// Merges a set of non-overlapping nodes.
        /// </summary>
        public static IPointCloudNode NonOverlapping(Storage storage, IStoreResolver resolver, IPointCloudNode[] nodes, ImportConfig config)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (config == null) throw new ArgumentNullException(nameof(config));
            
            if (nodes.Length == 0) throw new ArgumentOutOfRangeException(nameof(nodes), "Need at least 1 node (0 given).");
            if (nodes.Length == 1) return nodes[0];

            var id = Guid.NewGuid().ToString();
            var cell = new Cell(new Box3d(nodes.Select(x => x.Cell.BoundingBox)));
            var center = cell.GetCenter();
            var boundingBoxExact = new Box3d(nodes.Select(x => x.BoundingBoxExact));
            var pointCountTree = nodes.Sum(x => x.PointCountTree);

#if PARANOID
            var test = containedIn(cell, nodes);
            if (test.Length != nodes.Length) throw new InvalidOperationException();
            IPointCloudNode[] containedIn(Cell c, IEnumerable<IPointCloudNode> ns)
                => ns.Where(x => c.Contains(x.Cell)).ToArray();
#endif

            // sort nodes into subcells
            var buckets = new List<IPointCloudNode>[8].SetByIndex(_ => new List<IPointCloudNode>());
            var subcells = cell.Children;
            foreach (var n in nodes)
            {
                var notInserted = true;
                for (var i = 0; i < 8; i++)
                {
                    if (subcells[i].Contains(n.Cell))
                    {
                        buckets[i].Add(n);
                        notInserted = false;
                        break;
                    }
                }
                if (notInserted)
                {
                    Report.Warn($"Skipped cell {n.Cell} because it overlaps.");
                    Report.Warn($"Current cell {cell} has following nodes to insert:");
                    foreach (var x in nodes) Report.Warn($"  {x.Cell}");
                    //throw new InvalidOperationException();
                }
            }

            // create subcells
            var subnodes = new PersistentRef<IPointCloudNode>[8];
            for (var i = 0; i < 8; i++)
            {
                var bucket = buckets[i];

                if (bucket.Count == 0)
                {
                    subnodes[i] = null;
                    continue;
                }

                if (bucket.Count == 1 && bucket[0].Cell == subcells[i])
                {
                    Console.WriteLine($"FOO -> {bucket[0].CountNodes()}");
                    var localId = bucket[0].Id;
                    subnodes[i] = new PersistentRef<IPointCloudNode>(localId, (_id, _ct) => storage.GetPointCloudNode(localId, resolver, _ct));
                    bucket.Clear();
                    continue;
                }

                var subnode = NonOverlapping(storage, resolver, bucket.ToArray(), config);
                subnodes[i] = new PersistentRef<IPointCloudNode>(subnode.Id, (_id, _ct) => storage.GetPointCloudNode(_id, resolver, _ct));
            }

            // create node
            var result = new PointCloudNode(storage, id, cell, boundingBoxExact, pointCountTree, subnodes, storeOnCreation: true);

            // generate lod
            if (config.CreateOctreeLod)
            {
                result = result.GenerateLod(config);
            }

            // normals
            if (config.EstimateNormals != null)
            {
                result = result.RegenerateNormals(config.EstimateNormals, config.ProgressCallback, config.CancellationToken);
            }

            storage.Add(id, result);

            return result;
        }
    }
}
