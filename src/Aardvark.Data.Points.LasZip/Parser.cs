/*
   Copyright (C) 2017-2022. Stefan Maierhofer.

   This code has been COPIED from https://github.com/stefanmaierhofer/LASzip.

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/
using Aardvark.Base;
using LASzip.Net;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace LASZip
{
    /// <summary>
    /// </summary>
    public struct Info
    {
        /// <summary>
        /// Total number of points.
        /// </summary>
        public readonly long Count;

        /// <summary>
        /// Bounding box of point cloud.
        /// </summary>
        public readonly Box3d Bounds;

        /// <summary>
        /// </summary>
        public Info(long count, Box3d bounds)
        {
            Count = count;
            Bounds = bounds;
        }
    }

    /// <summary>
    /// Chunk of points.
    /// </summary>
    public class Points
    {
        public V3d[] Positions;
        public ushort[] Intensities;
        public byte[] ReturnNumbers;
        public byte[] NumberOfReturnsOfPulses;
        public BitArray ScanDirectionFlags;
        public BitArray EdgeOfFlightLines;
        public byte[] Classifications;
        public byte[] ScanAngleRanks;
        public byte[] UserDatas;
        public ushort[] PointSourceIds;

        public double[] GpsTimes;

        public C3b[] Colors;

        public byte[] WavePacketDescriptorIndices;
        public ulong[] BytesOffsetToWaveformDatas;
        public uint[] WaveformPacketSizesInBytes;
        public float[] ReturnPointWaveformLocations;
        public float[] Xts;
        public float[] Yts;
        public float[] Zts;

        /// <summary>
        /// Number of points.
        /// </summary>
        public int Count => Positions.Length;
    }

    /// <summary>
    /// </summary>
    public static class Parser
    {
        #region ReadInfo

        /// <summary>
        /// Returns info for given dataset.
        /// </summary>
        public static Info ReadInfo(string filename)
        {
            var reader = new laszip();
            reader.open_reader(filename, out _);
            return ReadInfo(reader);
        }

        /// <summary>
        /// Returns info for given dataset.
        /// </summary>
        public static Info ReadInfo(Stream stream)
        {
            var reader = new laszip();
            reader.open_reader_stream(stream, out _);
            return ReadInfo(reader);
        }

        private static Info ReadInfo(laszip reader)
        {
            var count = reader.header.number_of_point_records;

            var bounds = new Box3d(
                new V3d(
                    reader.header.min_x,
                    reader.header.min_y,
                    reader.header.min_z),
                new V3d(
                    reader.header.max_x,
                    reader.header.max_y,
                    reader.header.max_z)
                );

            reader.close_reader();

            return new Info(count, bounds);
        }

        #endregion

        /// <summary>
        /// Reads point data from file and returns chunks of given size.
        /// </summary>
        public static IEnumerable<Points> ReadPoints(string filename, int numberOfPointsPerChunk, bool verbose)
        {
            var reader = new laszip();
            reader.open_reader(filename, out _);
            return ReadPoints(reader, numberOfPointsPerChunk, verbose);
        }
        /// <summary>
        /// Reads point data from file and returns chunks of given size.
        /// </summary>
        public static IEnumerable<Points> ReadPoints(string filename, int numberOfPointsPerChunk)
            => ReadPoints(filename, numberOfPointsPerChunk, verbose: false);

        /// <summary>
        /// Reads point data from stream and returns chunks of given size.
        /// </summary>
        public static IEnumerable<Points> ReadPoints(Stream stream, int numberOfPointsPerChunk, bool verbose)
        {
            var reader = new laszip();
            reader.open_reader_stream(stream, out _);
            return ReadPoints(reader, numberOfPointsPerChunk, verbose);
        }
        /// <summary>
        /// Reads point data from stream and returns chunks of given size.
        /// </summary>
        public static IEnumerable<Points> ReadPoints(Stream stream, int numberOfPointsPerChunk)
            => ReadPoints(stream, numberOfPointsPerChunk, verbose: false);

        private static IEnumerable<Points> ReadPoints(laszip reader, int numberOfPointsPerChunk, bool verbose)
        {
            if (numberOfPointsPerChunk < 1) throw new ArgumentOutOfRangeException(nameof(numberOfPointsPerChunk));

            ulong n = reader.header.number_of_point_records;
            if (n == 0) n = reader.header.extended_number_of_point_records;

            var f               = reader.header.point_data_format;
            var hasGpsTime      = f == 1           || f == 3 || f == 4 || f == 5 || f == 6 || f == 7 || f == 8 || f == 9 || f == 10;
            var hasColor        =           f == 2 || f == 3           || f == 5           || f == 7 || f == 8           || f == 10;
            var hasWavePacket   =                               f == 4 || f == 5                               || f == 9 || f == 10;

            for (var j = 0ul; j < n; j += (uint)numberOfPointsPerChunk)
            {
                if (j + (uint)numberOfPointsPerChunk > n) numberOfPointsPerChunk = (int)(n - j);
                if (verbose) Console.WriteLine($"j: {j}, numberOfPointsPerChunk: {numberOfPointsPerChunk}, n: {n}");

                // POINT10
                var p = new double[3];
                var ps = new V3d[numberOfPointsPerChunk];
                var intensities = new ushort[numberOfPointsPerChunk];
                var returnNumbers = new byte[numberOfPointsPerChunk];
                var numberOfReturnsOfPulses = new byte[numberOfPointsPerChunk];
                var scanDirectionFlags = new BitArray(numberOfPointsPerChunk);
                var edgeOfFlightLines = new BitArray(numberOfPointsPerChunk);
                var classifications = new byte[numberOfPointsPerChunk];
                var scanAngleRanks = new byte[numberOfPointsPerChunk];
                var userDatas = new byte[numberOfPointsPerChunk];
                var pointSourceIds = new ushort[numberOfPointsPerChunk];

                // GPSTIME10
                var gpsTimes = hasGpsTime ? new double[numberOfPointsPerChunk] : null;

                // RGB12
                var colorsRaw = hasColor ? new C3us[numberOfPointsPerChunk] : null;
                bool colorIs8Bit = true;

                // WAVEPACKET13
                var wavePacketDescriptorIndices = hasWavePacket ? new byte[numberOfPointsPerChunk] : null;
                var bytesOffsetToWaveformDatas = hasWavePacket ? new ulong[numberOfPointsPerChunk] : null;
                var waveformPacketSizesInBytes = hasWavePacket ? new uint[numberOfPointsPerChunk] : null;
                var returnPointWaveformLocations = hasWavePacket ? new float[numberOfPointsPerChunk] : null;
                var xts = hasWavePacket ? new float[numberOfPointsPerChunk] : null;
                var yts = hasWavePacket ? new float[numberOfPointsPerChunk] : null;
                var zts = hasWavePacket ? new float[numberOfPointsPerChunk] : null;

                for (var i = 0; i < numberOfPointsPerChunk; i++)
                {
                    reader.read_point();

                    reader.get_coordinates(p);
                    ps[i] = new V3d(p);
                    intensities[i] = reader.point.intensity;
                    returnNumbers[i] = reader.point.return_number;
                    numberOfReturnsOfPulses[i] = Math.Max(reader.point.number_of_returns, reader.point.extended_number_of_returns);
                    scanDirectionFlags[i] = reader.point.scan_direction_flag != 0;
                    edgeOfFlightLines[i] = reader.point.edge_of_flight_line != 0;
                    classifications[i] = reader.point.classification;
                    scanAngleRanks[i] = (byte)reader.point.scan_angle_rank;
                    userDatas[i] = reader.point.user_data;
                    pointSourceIds[i] = reader.point.point_source_ID;

                    if (hasGpsTime) gpsTimes[i] = reader.point.gps_time;

                    if (hasColor)
                    {
                        var c = new C3us(reader.point.rgb[0], reader.point.rgb[1], reader.point.rgb[2]);
                        colorsRaw[i] = c;
                        if (colorIs8Bit && (c.R > 255 || c.G > 255 || c.B > 255)) colorIs8Bit = false;
                    }

                    if (hasWavePacket)
                    {
                        var buffer = reader.point.wave_packet;
                        wavePacketDescriptorIndices[i] = buffer[0];
                        bytesOffsetToWaveformDatas[i] = BitConverter.ToUInt64(buffer, 1);
                        waveformPacketSizesInBytes[i] = BitConverter.ToUInt32(buffer, 9);
                        returnPointWaveformLocations[i] = BitConverter.ToSingle(buffer, 13);
                        xts[i] = BitConverter.ToSingle(buffer, 17);
                        yts[i] = BitConverter.ToSingle(buffer, 21);
                        zts[i] = BitConverter.ToSingle(buffer, 25);
                    }
                }

                var colors = (colorsRaw, colorIs8Bit) switch
                {
                    (null, _) => null,
                    (not null, true) => colorsRaw.Map(c => new C3b(c.R, c.G, c.B)),
                    (not null, false) => colorsRaw.Map(c => new C3b(c.R >> 8, c.G >> 8, c.B >> 8))
                };

                //if (verbose)
                //{
                //    if (ps?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] positions");
                //    if (intensities?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] intensities");
                //    if (returnNumbers?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] returnNumbers");
                //    if (numberOfReturnsOfPulses?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] numberOfReturnsOfPulses");
                //    if (classifications?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] classifications");
                //    if (scanAngleRanks?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] scanAngleRanks");
                //    if (userDatas?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] userDatas");
                //    if (pointSourceIds?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] pointSourceIds");
                //    if (gpsTimes?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] gpsTimes");
                //    if (colors?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] colors");
                //    if (wavePacketDescriptorIndices?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] wavePacketDescriptorIndices");
                //    if (bytesOffsetToWaveformDatas?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] bytesOffsetToWaveformDatas");
                //    if (waveformPacketSizesInBytes?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] waveformPacketSizesInBytes");
                //    if (returnPointWaveformLocations?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] returnPointWaveformLocations");
                //    if (xts?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] xts");
                //    if (yts?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] yts");
                //    if (zts?.Distinct()?.Count() > 1) Report.WarnNoPrefix("[Laszip.ReadPoints] zts");
                //}

                yield return new Points
                {
                    Positions = ps,
                    Intensities = intensities,
                    ReturnNumbers = returnNumbers,
                    NumberOfReturnsOfPulses = numberOfReturnsOfPulses,
                    ScanDirectionFlags = scanDirectionFlags,
                    EdgeOfFlightLines = edgeOfFlightLines,
                    Classifications = classifications,
                    ScanAngleRanks = scanAngleRanks,
                    UserDatas = userDatas,
                    PointSourceIds = pointSourceIds,

                    GpsTimes = gpsTimes,

                    Colors = colors,

                    WavePacketDescriptorIndices = wavePacketDescriptorIndices,
                    BytesOffsetToWaveformDatas = bytesOffsetToWaveformDatas,
                    WaveformPacketSizesInBytes = waveformPacketSizesInBytes,
                    ReturnPointWaveformLocations = returnPointWaveformLocations,
                    Xts = xts,
                    Yts = yts,
                    Zts = zts
                };
            }

            reader.close_reader();
        }
    }
}
