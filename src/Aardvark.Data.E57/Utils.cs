using Aardvark.Base;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PPS = Aardvark.Data.E57.PointPropertySemantics;

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

        NormalX,
        NormalY,
        NormalZ,
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
            PPS.ReturnCount           => ValueBuffer<uint>(),
            PPS.ReturnIndex           => ValueBuffer<uint>(),
            PPS.RowIndex              => ValueBuffer<uint>(),
            PPS.SphericalAzimuth      => ValueBuffer<double>(),
            PPS.SphericalElevation    => ValueBuffer<double>(),
            PPS.SphericalInvalidState => ValueBuffer<byte>(),
            PPS.SphericalRange        => ValueBuffer<double>(),
            PPS.TimeStamp             => ValueBuffer<double>(),
            _ => throw new NotImplementedException($"Unknown PointPropertySemantics \"{sem}\".")
        };
    }

    internal interface IValueBuffer
    {
        int Count { get; }
        Array Consume(int n);
    }

    internal class ValueBuffer<T> : IValueBuffer
    {
        private List<T> _value = new();
        public void AddRange(T[] xs) => _value.AddRange(xs);
        public int Count => _value.Count;
        public Array Consume(int n)
        {
            if (n < 1 || n > _value.Count) throw new ArgumentOutOfRangeException(nameof(n));
            T[] result;
            if (n == _value.Count)
            {
                result = _value.ToArray();
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
        private readonly Dictionary<PPS, IValueBuffer> _data = new();

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