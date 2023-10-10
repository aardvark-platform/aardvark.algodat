/*
    Copyright (C) 2006-2023. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System;
using System.Collections.Generic;
using System.Threading;

namespace Aardvark.Data.Points
{
    /// <summary>
    /// General info for a point cloud data file.
    /// </summary>
    public class ImportConfig
    {
        /// <summary>
        /// Default configuration.
        /// </summary>
        public static readonly ImportConfig Default = new();

        #region Properties

        /// <summary></summary>
        public ParseConfig ParseConfig { get; private set; } = ParseConfig.Default;
        
        /// <summary>
        /// Store imported point cloud with this key.
        /// </summary>
        public string? Key { get; private set; } = null;

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

        /// <summary>Normalizes point density globally using MinDist distance.</summary>
        public bool NormalizePointDensityGlobal { get; private set; } = false;

        /// <summary>
        /// Max number of points in octree cell.
        /// </summary>
        public int OctreeSplitLimit { get; private set; } = 8192;

        /// <summary></summary>
        public Action<double> ProgressCallback { get; private set; } = _ => { };

        /// <summary></summary>
        public Func<IList<V3d>, IList<V3d>>? Reproject { get; private set; } = null;

        /// <summary></summary>
        public Storage Storage { get; init; } = null!;

        /// <summary></summary>
        public bool Verbose => ParseConfig.Verbose;

        /// <summary></summary>
        public int MaxChunkPointCount => ParseConfig.MaxChunkPointCount;

        #endregion

        #region Immutable updates

        private ImportConfig() { }

        /// <summary></summary>
        public ImportConfig(ImportConfig x)
        {
            Key = x.Key;
            NormalizePointDensityGlobal = x.NormalizePointDensityGlobal;
            OctreeSplitLimit = x.OctreeSplitLimit;
            ProgressCallback = x.ProgressCallback;
            ParseConfig = x.ParseConfig;
            Reproject = x.Reproject;
            Storage = x.Storage;
        }

        /// <summary></summary>
        public ImportConfig WithCancellationToken(CancellationToken x) => new(this) { ParseConfig = ParseConfig.WithCancellationToken(x) };

        /// <summary></summary>
        public ImportConfig WithKey(string x) => new(this) { Key = x };

        /// <summary></summary>
        public ImportConfig WithKey(Guid x) => new(this) { Key = x.ToString() };

        /// <summary></summary>
        public ImportConfig WithRandomKey() => WithKey(Guid.NewGuid().ToString());

        /// <summary></summary>
        public ImportConfig WithMaxDegreeOfParallelism(int x) => new(this) { ParseConfig = ParseConfig.WithMaxDegreeOfParallelism(x) };

        /// <summary></summary>
        public ImportConfig WithMinDist(double x) => new(this) { ParseConfig = ParseConfig.WithMinDist(x) };

        /// <summary></summary>
        public ImportConfig WithNormalizePointDensityGlobal(bool x) => new(this) { NormalizePointDensityGlobal = x };

        /// <summary></summary>
        public ImportConfig WithOctreeSplitLimit(int x) => new(this) { OctreeSplitLimit = x };

        /// <summary></summary>
        public ImportConfig WithProgressCallback(Action<double> x) => new(this) { ProgressCallback = x ?? throw new(nameof(x)) };

        /// <summary></summary>
        public ImportConfig WithReadBufferSizeInBytes(int x) => new(this) { ParseConfig = ParseConfig.WithReadBufferSizeInBytes(x) };

        /// <summary></summary>
        public ImportConfig WithMaxChunkPointCount(int x) => new(this) { ParseConfig = ParseConfig.WithMaxChunkPointCount(Math.Max(x, 1)) };

        /// <summary></summary>
        public ImportConfig WithReproject(Func<IList<V3d>, IList<V3d>> x) => new(this) { Reproject = x };

        /// <summary></summary>
        public ImportConfig WithVerbose(bool x) => new(this) { ParseConfig = ParseConfig.WithVerbose(x) };

        /// <summary></summary>
        public ImportConfig WithStorage(Storage x) => new(this) { Storage = x };

        /// <summary></summary>
        public ImportConfig WithPartIndexOffset(int x) => new(this) { ParseConfig = ParseConfig.WithPartIndexOffset(x) };

        /// <summary></summary>
        public ImportConfig WithEnabledProperties(EnabledProperties x) => new(this) { ParseConfig = ParseConfig.WithEnabledProperties(x) };

        /// <summary></summary>
        public ImportConfig WithEnabledClassifications(bool enabled) => new(this) { ParseConfig = ParseConfig.WithEnabledProperties(ParseConfig.EnabledProperties.WithClassifications(enabled)) };

        /// <summary></summary>
        public ImportConfig WithEnabledColors(bool enabled) => new(this) { ParseConfig = ParseConfig.WithEnabledProperties(ParseConfig.EnabledProperties.WithColors(enabled)) };

        /// <summary></summary>
        public ImportConfig WithEnabledIntensities(bool enabled) => new(this) { ParseConfig = ParseConfig.WithEnabledProperties(ParseConfig.EnabledProperties.WithIntensities(enabled)) };

        /// <summary></summary>
        public ImportConfig WithEnabledNormals(bool enabled) => new(this) { ParseConfig = ParseConfig.WithEnabledProperties(ParseConfig.EnabledProperties.WithNormals(enabled)) };

        /// <summary></summary>
        public ImportConfig WithEnabledPartIndices(bool enabled) => new(this) { ParseConfig = ParseConfig.WithEnabledProperties(ParseConfig.EnabledProperties.WithPartIndices(enabled)) };

        #endregion
    }
}
