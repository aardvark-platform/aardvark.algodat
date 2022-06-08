/*
   Copyright (C) 2018-2022. Stefan Maierhofer.

   This code is based on https://github.com/stefanmaierhofer/Ply.Net (copied and extended).

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

#if !NETCOREAPP
using System;
using System.ComponentModel;
namespace System.Runtime.CompilerServices
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class IsExternalInit { }
}
#endif

namespace Ply.Net
{
    internal static class Extensions
    {
        public static R[] Map<T, R>(this Span<T> xs, Func<T, R> map)
        {
            var rs = new R[xs.Length];
            for (var i = 0; i < xs.Length; i++) rs[i] = map(xs[i]);
            return rs;
        }

        public static R[] Map<T, R>(this T[] xs, Func<T, R> map)
        {
            var rs = new R[xs.Length];
            for (var i = 0; i < xs.Length; i++) rs[i] = map(xs[i]);
            return rs;
        }

        public static readonly char[] s_whiteSpace = new char[] { ' ', '\t', '\n', '\r' };
        public static string[] SplitOnWhitespace(this string s) => s.Split(s_whiteSpace, StringSplitOptions.RemoveEmptyEntries);
    }
}