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
using System;
using System.IO;
using System.Security.Cryptography;

namespace Aardvark.Geometry.Points;

/// <summary>
/// </summary>
public static class FileHelpers
{
    /// <summary>
    /// Computes MD5 synchronously and disposes the hash provider before returning.
    /// If fast is set to true, then only the first 1 MiB of data is used for hash.
    /// Returns the digest bytes formatted as a Guid string.
    /// </summary>
    public static string ComputeMd5Hash(string filename, bool fast)
    {
        var MiB = 1024 * 1024;

        var filesize = new FileInfo(filename).Length;

        if (fast)
        {
            if (filesize <= MiB)
            {
                var buffer = File.ReadAllBytes(filename);
                using var md5 = MD5.Create();
                return new Guid(md5.ComputeHash(buffer)).ToString();
            }
            else
            {
                using var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None);
                var buffer = new byte[MiB];
                if (fs.Read(buffer, 0, MiB) != MiB) throw new InvalidOperationException();
                using var md5 = MD5.Create();
                return new Guid(md5.ComputeHash(buffer)).ToString();
            }
        }
        else
        {
            using var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.None);
            using var md5 = MD5.Create();
            return new Guid(md5.ComputeHash(fs)).ToString();
        }
    }
}
