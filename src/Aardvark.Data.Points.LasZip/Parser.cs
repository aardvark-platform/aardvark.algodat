/*
    Copyright (C) 2017-2020. Stefan Maierhofer.

    This code has been COPIED from https://github.com/stefanmaierhofer/LASzip.

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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Aardvark.Base;
using laszip.net;

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
            var reader = new laszip_dll();
            var compressed = false;
            reader.laszip_open_reader(filename, ref compressed);
            return ReadInfo(reader);
        }

        /// <summary>
        /// Returns info for given dataset.
        /// </summary>
        public static Info ReadInfo(Stream stream)
        {
            var reader = new laszip_dll();
            var compressed = false;
            reader.laszip_open_reader(stream, ref compressed);
            return ReadInfo(reader);
        }

        private static Info ReadInfo(laszip_dll reader)
        {
            var count = reader.header.number_of_point_records;

            var bounds = new Box3d(
                new V3d(
                    reader.header.x_scale_factor * reader.header.min_x + reader.header.x_offset,
                    reader.header.y_scale_factor * reader.header.min_y + reader.header.y_offset,
                    reader.header.z_scale_factor * reader.header.min_z + reader.header.z_offset),
                new V3d(
                    reader.header.x_scale_factor * reader.header.max_x + reader.header.x_offset,
                    reader.header.y_scale_factor * reader.header.max_y + reader.header.y_offset,
                    reader.header.z_scale_factor * reader.header.max_z + reader.header.z_offset)
                );

            reader.laszip_close_reader();

            return new Info(count, bounds);
        }

        #endregion

        /// <summary>
        /// Reads point data from file and returns chunks of given size.
        /// </summary>
        public static IEnumerable<Points> ReadPoints(string filename, int numberOfPointsPerChunk)
        {
            var reader = new laszip_dll();
            var isCompressed = false;
            reader.laszip_open_reader(filename, ref isCompressed);
            return ReadPoints(reader, numberOfPointsPerChunk);
        }

        /// <summary>
        /// Reads point data from stream and returns chunks of given size.
        /// </summary>
        public static IEnumerable<Points> ReadPoints(Stream stream, int numberOfPointsPerChunk)
        {
            var reader = new laszip_dll();
            var isCompressed = false;
            reader.laszip_open_reader(stream, ref isCompressed);
            return ReadPoints(reader, numberOfPointsPerChunk);
        }

        private static IEnumerable<Points> ReadPoints(laszip_dll reader, int numberOfPointsPerChunk)
        {
            var n = reader.header.number_of_point_records;
            //var numberOfChunks = n / numberOfPointsPerChunk;

            var format = reader.header.point_data_format;
            var hasGpsTime = format == 1 || format == 3 || format == 4 || format == 5;
            var hasColor = format == 2 || format == 3 || format == 5;
            var hasWavePacket = format == 4 || format == 5;

            for (var j = 0; j < n; j += numberOfPointsPerChunk)
            {
                if (j + numberOfPointsPerChunk > n) numberOfPointsPerChunk = (int)(n - j);
                //Console.WriteLine($"j: {j}, numberOfPointsPerChunk: {numberOfPointsPerChunk}, n: {n}");

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
                var colors = hasColor ? new C3b[numberOfPointsPerChunk] : null;

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
                    reader.laszip_read_point();

                    reader.laszip_get_coordinates(p);
                    ps[i] = new V3d(p);
                    intensities[i] = reader.point.intensity;
                    returnNumbers[i] = reader.point.return_number;
                    numberOfReturnsOfPulses[i] = reader.point.number_of_returns_of_given_pulse;
                    scanDirectionFlags[i] = reader.point.scan_direction_flag != 0;
                    edgeOfFlightLines[i] = reader.point.edge_of_flight_line != 0;
                    classifications[i] = reader.point.classification;
                    scanAngleRanks[i] = (byte)reader.point.scan_angle_rank;
                    userDatas[i] = reader.point.user_data;
                    pointSourceIds[i] = reader.point.point_source_ID;

                    if (hasGpsTime) gpsTimes[i] = reader.point.gps_time;

                    if (hasColor) colors[i] = new C3b(reader.point.rgb[0] >> 8, reader.point.rgb[1] >> 8, reader.point.rgb[2] >> 8);

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

            reader.laszip_close_reader();
        }
    }
}
