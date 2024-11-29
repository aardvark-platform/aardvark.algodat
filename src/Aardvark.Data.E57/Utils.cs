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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PPS = Aardvark.Data.E57.PointPropertySemantics;

#pragma warning disable IDE0130 // Namespace does not match folder structure

namespace System.Runtime.CompilerServices
{
    internal class IsExternalInit { }
}

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Aardvark.Data.E57
{
    public enum PointPropertySemantics
    {
        CartesianX,
        CartesianY,
        CartesianZ,
        SphericalRange,
        SphericalAzimuth,
        SphericalElevation,
        RowIndex,
        ColumnIndex,
        ReturnCount,
        ReturnIndex,
        TimeStamp,
        Intensity,
        ColorRed,
        ColorGreen,
        ColorBlue,
        CartesianInvalidState,
        SphericalInvalidState,
        IsTimeStampInvalid,
        IsIntensityInvalid,
        IsColorInvalid,

        Classification,

        NormalX,
        NormalY,
        NormalZ,

        Reflectance,
    }

    internal static class Utils
    {
        public static IValueBuffer ValueBuffer<T>() => new ValueBuffer<T>();

        public static IValueBuffer ValueBuffer(PPS sem) => sem switch
        {
            PPS.CartesianInvalidState => ValueBuffer<byte>(),
            PPS.CartesianX            => ValueBuffer<double>(),
            PPS.CartesianY            => ValueBuffer<double>(),
            PPS.CartesianZ            => ValueBuffer<double>(),
            PPS.ColorBlue             => ValueBuffer<byte>(),
            PPS.ColorGreen            => ValueBuffer<byte>(),
            PPS.ColorRed              => ValueBuffer<byte>(),
            PPS.ColumnIndex           => ValueBuffer<uint>(),
            PPS.Intensity             => ValueBuffer<int>(),
            PPS.IsColorInvalid        => ValueBuffer<byte>(),
            PPS.IsIntensityInvalid    => ValueBuffer<byte>(),
            PPS.IsTimeStampInvalid    => ValueBuffer<byte>(),
            PPS.NormalX               => ValueBuffer<float>(),
            PPS.NormalY               => ValueBuffer<float>(),
            PPS.NormalZ               => ValueBuffer<float>(),
            PPS.Reflectance           => ValueBuffer<float>(),
            PPS.ReturnCount           => ValueBuffer<uint>(),
            PPS.ReturnIndex           => ValueBuffer<uint>(),
            PPS.RowIndex              => ValueBuffer<uint>(),
            PPS.SphericalAzimuth      => ValueBuffer<double>(),
            PPS.SphericalElevation    => ValueBuffer<double>(),
            PPS.SphericalInvalidState => ValueBuffer<byte>(),
            PPS.SphericalRange        => ValueBuffer<double>(),
            PPS.TimeStamp             => ValueBuffer<double>(),
            PPS.Classification        => ValueBuffer<int>(),
            _ => throw new Exception($"Unknown PointPropertySemantics \"{sem}\". Error 0c98368f-a99f-4a88-a3c6-b2c3ac81d478.")
        };
    }

    internal interface IValueBuffer
    {
        int Count { get; }
        Array Consume(int n);
    }

    internal class ValueBuffer<T> : IValueBuffer
    {
        private List<T> _value = [];
        public void AddRange(T[] xs) => _value.AddRange(xs);
        public int Count => _value.Count;
        public Array Consume(int n)
        {
            if (n < 1 || n > _value.Count) throw new ArgumentOutOfRangeException(nameof(n));
            T[] result;
            if (n == _value.Count)
            {
                result = [.. _value];
                _value.Clear();
            }
            else
            {
                result = _value.Take(n).ToArray();
                _value = new(_value.Skip(n));
            }
            return result;
        }
    }

    internal class ValueBufferSet
    {
        private readonly Dictionary<PPS, IValueBuffer> _data = [];

        public ValueBufferSet(IEnumerable<PPS> keys)
        {
            foreach (var key in keys) _data[key] = Utils.ValueBuffer(key);
        }

        public void Append<T>(PPS sem, T[] xs) => ((ValueBuffer<T>)_data[sem]).AddRange(xs);

        public int CountMin => _data.Min(kv => kv.Value.Count);

        public int CountMax => _data.Max(kv => kv.Value.Count);
        
        public ImmutableDictionary<PPS, Array> Consume(int n)
            => _data.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.Consume(n));
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member