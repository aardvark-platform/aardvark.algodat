/*
    Copyright (C) 2017. Aardvark Platform Team. http://github.com/aardvark-platform.
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
using System.Globalization;
using Aardvark.Base;

namespace Aardvark.Geometry.Points
{
    /// <summary>
    /// </summary>
    public struct ProgressToken
    {
        /// <summary>
        /// Valid progress range is [0.0, Max].
        /// </summary>
        public readonly double Max;

        /// <summary>
        /// Current progress (within Range).
        /// </summary>
        public readonly double Value;

        /// <summary>
        /// [0.0, 1.0].
        /// </summary>
        public double Ratio => Value / Max;

        /// <summary>
        /// </summary>
        public ProgressToken(double value, double max)
        {
            if (max <= 0.0) throw new ArgumentOutOfRangeException(nameof(max));
            Max = max;
            Value = Range1d.FromMinAndSize(0.0, max).Clamped(value);
        }
        
        /// <summary>
        /// </summary>
        public static ProgressToken operator +(ProgressToken a, ProgressToken b)
            => Progress.Token(a.Value + b.Value, a.Max + b.Max);

        /// <summary>
        /// </summary>
        public static ProgressToken operator *(ProgressToken a, double b)
            => Progress.Token(a.Value * b, a.Max * b);

        /// <summary>
        /// </summary>
        public static ProgressToken operator *(double a, ProgressToken b)
            => Progress.Token(a * b.Value, a * b.Max);

        /// <summary>
        /// </summary>
        public static ProgressToken operator /(ProgressToken a, double b)
            => Progress.Token(a.Value / b, a.Max / b);
        
        /// <summary>
        /// </summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "[{0:0.000}/{1:0.000}]", Value, Max);
        }
    }
    
    /// <summary>
    /// </summary>
    public class ProgressReporter
    {
        /// <summary>
        /// </summary>
        public static ProgressReporter None => new ProgressReporter();
       
        /// <summary>
        /// </summary>
        public ProgressReporter() => Current = Progress.Token(0.0, 1.0);

        /// <summary>
        /// </summary>
        public ProgressReporter(double max) => Current = Progress.Token(0.0, max);

        /// <summary>
        /// </summary>
        public ProgressToken Current { get; private set; }
        
        /// <summary>
        /// </summary>
        public void Report(ProgressToken current)
        {
            Current = current;
            if (m_subscriptions == null) return;
            foreach (var f in m_subscriptions) f?.Invoke(current);
        }

        /// <summary>
        /// </summary>
        public void Report(double current, double max)
        {
            Current = new ProgressToken(current, max);
            if (m_subscriptions == null) return;
            foreach (var f in m_subscriptions) f?.Invoke(Current);
        }

        /// <summary>
        /// </summary>
        public void ReportFinished()
        {
            Current = new ProgressToken(1.0, 1.0);
            if (m_subscriptions == null) return;
            foreach (var f in m_subscriptions) f?.Invoke(Current);
        }

        /// <summary>
        /// </summary>
        public ProgressReporter Subscribe(Action<ProgressToken> callback)
        {
            if (m_subscriptions == null) m_subscriptions = new List<Action<ProgressToken>>();
            m_subscriptions.Add(callback);
            return this;
        }

        /// <summary>
        /// </summary>
        public static ProgressReporter operator +(ProgressReporter a, ProgressReporter b) => Progress.Join(a, b);

        /// <summary>
        /// </summary>
        public static ProgressReporter operator *(ProgressReporter a, double b)
        {
            var result = Progress.Reporter();
            a.Subscribe(x => result.Report(x * b));
            return result;
        }

        /// <summary>
        /// </summary>
        public static ProgressReporter operator *(double b, ProgressReporter a) => a * b;

        /// <summary>
        /// </summary>
        public static ProgressReporter operator /(ProgressReporter a, double b)
        {
            var result = Progress.Reporter();
            a.Subscribe(x => result.Report(x / b));
            return result;
        }
        
        private List<Action<ProgressToken>> m_subscriptions;
    }
    
    /// <summary>
    /// </summary>
    public static class Progress
    {
        /// <summary>
        /// </summary>
        public static ProgressToken Token(double value, double max) => new ProgressToken(value, max);
        
        /// <summary>
        /// </summary>
        public static ProgressReporter Reporter() => new ProgressReporter();

        /// <summary>
        /// </summary>
        public static ProgressReporter Reporter(double max) => new ProgressReporter(max);

        /// <summary>
        /// </summary>
        public static ProgressReporter Join(this ProgressReporter a, ProgressReporter b)
        {
            var total = Reporter();
            a.Subscribe(x => total.Report(x + b.Current));
            b.Subscribe(x => total.Report(a.Current + x));
            return total;
        }

        /// <summary>
        /// </summary>
        public static ProgressReporter Normalize(this ProgressReporter a)
        {
            var total = Reporter();
            a.Subscribe(x => total.Report(x.Ratio, 1.0));
            return total;
        }
    }
}
