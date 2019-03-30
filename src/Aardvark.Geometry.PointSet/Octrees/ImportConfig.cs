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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Aardvark.Base;
using Aardvark.Geometry;
using Aardvark.Geometry.Points;
using Uncodium.SimpleStore;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// General info for a point cloud data file.
    /// </summary>
    public class ImportConfig
    {
        //private static ImmutableHashSet<DurableData> Hull(DurableData a, ImmutableHashSet<DurableData> set)
        //{
        //    if (set.Contains(a)) return set;
        //    else
        //    {
        //        set = set.Add(a);
        //        foreach (var d in a.DependsOn) { set = Hull(d, set); }
        //        return set;
        //    }
        //}

        //private static ImmutableList<DurableData> Sort(IEnumerable<DurableData> atts)
        //{
        //    var arr = atts.Aggregate(ImmutableHashSet<DurableData>.Empty, (s, c) => Hull(c, s)).ToArray();

        //    var sofar = ImmutableHashSet<DurableData>.Empty;
        //    for (int i = 0; i < arr.Length; i++)
        //    {
        //        var self = arr[i];
        //        var deps = self.DependsOn;
        //        if (deps.IsSubsetOf(sofar))
        //        {
        //            sofar = sofar.Add(self);
        //        }
        //        else
        //        {
        //            var p = sofar;
        //            var sat = false;
        //            for (int j = i + 1; j < arr.Length; j++)
        //            {
        //                var aj = arr[j];
        //                p = p.Add(aj);
        //                arr[j - 1] = aj;

        //                if (deps.IsSubsetOf(p))
        //                {
        //                    arr[j] = self;
        //                    i--;
        //                    sat = true;
        //                    break;
        //                }

        //            }

        //            if (!sat) throw new Exception("strange");
        //        }
        //    }

        //    return ImmutableList.Create(arr);
        //}



        /// <summary>
        /// Default configuration.
        /// </summary>
        public static readonly ImportConfig Default = new ImportConfig();

        #region Properties

        /// <summary></summary>
        public ParseConfig ParseConfig { get; private set; } = ParseConfig.Default;
        
        /// <summary>
        /// Store imported pointcloud with this key.
        /// </summary>
        public string Key { get; private set; } = null;

        /// <summary></summary>
        public CancellationToken CancellationToken => ParseConfig.CancellationToken;

        /// <summary></summary>
        public int MaxDegreeOfParallelism => ParseConfig.MaxDegreeOfParallelism;

        /// <summary>
        /// Remove points on import with less than this distance to previous point.
        /// </summary>
        public double MinDist => ParseConfig.MinDist;
        /// <summary>
        /// Large files should be read in chunks with this maximum size.
        /// </summary>
        public int ReadBufferSizeInBytes => ParseConfig.ReadBufferSizeInBytes;

        /// <summary>Removes duplicate points in chunk after MinDist filtering and before Reproject and EstimateNormals.</summary>
        public bool DeduplicateChunks { get; private set; } = true;



        /// <summary>
        /// Max number of points in octree cell.
        /// </summary>
        public int OctreeSplitLimit { get; private set; } = 8192;

        /// <summary></summary>
        public Action<double> ProgressCallback { get; private set; } = _ => { };


        /// <summary></summary>
        public Func<IList<V3d>, IList<V3d>> Reproject { get; private set; } = null;

        /// <summary>
        /// Positions -> Normals.
        /// </summary>
        public Func<PointRkdTreeD<V3f[], V3f>, V3f[], V3f[]> EstimateNormalsKdTree { get; private set; } = null;

        /// <summary>
        /// Positions -> Normals.
        /// </summary>
        public Func<IList<V3d>, IList<V3f>> EstimateNormals { get; private set; } = null;

        /// <summary></summary>
        public Storage Storage { get; private set; } = null;

        /// <summary></summary>
        public bool Verbose => ParseConfig.Verbose;

        /// <summary></summary>
        public int MaxChunkPointCount => ParseConfig.MaxChunkPointCount;

        ///// <summary>Per-cell attributes.</summary>
        //public ImmutableList<DurableData> CellAttributes { get; private set; } =
        //    Sort(new DurableData[]  {
        //        Geometry.Points.CellAttributes.BoundingBoxExactLocal,
        //        Geometry.Points.CellAttributes.AveragePointDistance,
        //        Geometry.Points.CellAttributes.AveragePointDistanceStdDev,
        //        Geometry.Points.CellAttributes.TreeMinDepth,
        //        Geometry.Points.CellAttributes.TreeMaxDepth,
        //        Geometry.Points.CellAttributes.PointCountCell,
        //    });

        #endregion

        #region Immutable updates

        private ImportConfig() { }

        /// <summary></summary>
        public ImportConfig(ImportConfig x)
        {
            Key = x.Key;
            DeduplicateChunks = x.DeduplicateChunks;
            OctreeSplitLimit = x.OctreeSplitLimit;
            ProgressCallback = x.ProgressCallback;
            ParseConfig = x.ParseConfig;
            Reproject = x.Reproject;
            EstimateNormals = x.EstimateNormals;
            EstimateNormalsKdTree = x.EstimateNormalsKdTree;
            Storage = x.Storage;
            //CellAttributes = x.CellAttributes;
        }

        /// <summary></summary>
        public ImportConfig WithCancellationToken(CancellationToken x) => new ImportConfig(this) { ParseConfig = ParseConfig.WithCancellationToken(x) };
        
        /// <summary></summary>
        public ImportConfig WithKey(string x) => new ImportConfig(this) { Key = x };

        /// <summary></summary>
        public ImportConfig WithRandomKey() => WithKey(Guid.NewGuid().ToString());

        /// <summary></summary>
        public ImportConfig WithMaxDegreeOfParallelism(int x) => new ImportConfig(this) { ParseConfig = ParseConfig.WithMaxDegreeOfParallelism(x) };

        /// <summary></summary>
        public ImportConfig WithMinDist(double x) => new ImportConfig(this) { ParseConfig = ParseConfig.WithMinDist(x) };

        /// <summary></summary>
        public ImportConfig WithDeduplicateChunks(bool x) => new ImportConfig(this) { DeduplicateChunks = x };

        /// <summary></summary>
        public ImportConfig WithOctreeSplitLimit(int x) => new ImportConfig(this) { OctreeSplitLimit = x };

        /// <summary></summary>
        public ImportConfig WithProgressCallback(Action<double> x) => new ImportConfig(this) { ProgressCallback = x ?? throw new ArgumentNullException() };

        /// <summary></summary>
        public ImportConfig WithReadBufferSizeInBytes(int x) => new ImportConfig(this) { ParseConfig = ParseConfig.WithReadBufferSizeInBytes(x) };

        /// <summary></summary>
        public ImportConfig WithMaxChunkPointCount(int x) => new ImportConfig(this) { ParseConfig = ParseConfig.WithMaxChunkPointCount(Math.Max(x, 1)) };

        /// <summary></summary>
        public ImportConfig WithReproject(Func<IList<V3d>, IList<V3d>> x) => new ImportConfig(this) { Reproject = x };

        /// <summary></summary>
        public ImportConfig WithEstimateKdNormals(Func<PointRkdTreeD<V3f[], V3f>, V3f[], V3f[]> x) => new ImportConfig(this) { EstimateNormalsKdTree = x };

        /// <summary></summary>
        public ImportConfig WithEstimateNormals(Func<IList<V3d>, IList<V3f>> x) => new ImportConfig(this) { EstimateNormals = x };

        /// <summary></summary>
        public ImportConfig WithStorage(Storage x) => new ImportConfig(this) { Storage = x };

        /// <summary></summary>
        public ImportConfig WithVerbose(bool x) => new ImportConfig(this) { ParseConfig = ParseConfig.WithVerbose(x) };

        ///// <summary></summary>
        //public ImportConfig WithCellAttributes(IEnumerable<DurableData> atts) => new ImportConfig(this) { CellAttributes = Sort(atts) };

        ///// <summary></summary>
        //public ImportConfig WithCellAttributes(params DurableData[] atts) => new ImportConfig(this) { CellAttributes = Sort(atts) };

        #endregion
    }
}
