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
using Aardvark.Base;
using Aardvark.Data.Points;
using System;
using System.Linq;
using System.Threading;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// Generation of per-point normals for point clouds.
    /// </summary>
    public static class PointSetNormalsGeneration
    {
        /// <summary>
        /// Generates normals using k-nearest neighbours.
        /// </summary>
        public static PointSet GenerateNormals(this PointSet self, ImportConfig config)
        {
            if (config.EstimateNormals == null && config.EstimateNormalsKdTree == null) return self;

            var nodeCount = self.Octree.Value.CountNodes();
            var processedNodesCount = 0L;
            var result = self.GenerateNormals(() =>
            {
                config.CancellationToken.ThrowIfCancellationRequested();
                var i = Interlocked.Increment(ref processedNodesCount);
                if (config.Verbose) Console.Write($"[Normals] {i}/{nodeCount}\r");
                if (i % 100 == 0) config.ProgressCallback(i / (double)nodeCount);
            }, config);

            config.ProgressCallback(1.0);

            return result;
        }
        
        private static PointSet GenerateNormals(this PointSet self, Action callback, ImportConfig config)
        {
            if (self.IsEmpty) return self;
#pragma warning disable CS0618 // Type or member is obsolete
            var normals = self.Root.Value.GenerateNormals(callback, config);
#pragma warning restore CS0618 // Type or member is obsolete
            var result = new PointSet(self.Storage, config.Key, normals.Id, self.SplitLimit);
            self.Storage.Add(config.Key, result);
            return result;
        }
        
        private static PointSetNode GenerateNormals(this PointSetNode self, Action callback, ImportConfig config)
        {
            if (self == null) throw new ArgumentNullException(nameof(self));

            config.CancellationToken.ThrowIfCancellationRequested();

            callback?.Invoke();
            
            var subcells = self.Subnodes?.Map(x => x?.Value.GenerateNormals(callback, config));

            // generate normals ...
            var needsNormals = self.HasPositions && !self.HasNormals;
            var ns =
                needsNormals ?
                    (config.EstimateNormalsKdTree == null ?
                        config.EstimateNormals(self.PositionsAbsolute).ToArray() :
                        config.EstimateNormalsKdTree(self.KdTree.Value, self.Positions.Value)
                    ) : null;
            
            // store data ...
            var result = self;

            if (needsNormals)
            {
                var nsId = Guid.NewGuid();
                self.Storage.Add(nsId, ns);
                throw new NotImplementedException();
                //result = result.WithNormals(nsId, subcells);
            }
            
            return result;
        }
    }
}
