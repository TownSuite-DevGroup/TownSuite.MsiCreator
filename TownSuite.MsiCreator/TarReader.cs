using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TownSuite.MsiCreator
{
    /// <summary>
    /// Extracts a tar stream. The same code runs on net48, which has no System.Formats.Tar, and on net8.
    ///
    /// Reads ustar, GNU (././@LongLink names) and pax (per-entry and global extended headers: path,
    /// linkpath, size) archives -- what GNU tar, bsdtar and .NET's TarFile write. Every entry must land
    /// inside the destination. Links, devices and FIFOs are refused, because an installer cannot carry them.
    /// </summary>
    internal static class TarReader
    {
        private const int BlockSize = 512;

        public static void ExtractToDirectory(Stream tar, string destination, string archiveName)
        {
            var root = Path.GetFullPath(destination);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                root += Path.DirectorySeparatorChar;
            }

            var header = new byte[BlockSize];
            var globalPax = new Dictionary<string, string>(StringComparer.Ordinal);
            Dictionary<string, string> entryPax = null;
            string gnuLongName = null;
            string gnuLongLink = null;
            bool sawEntry = false;

            while (true)
            {
                int read = ReadBlock(tar, header);
                if (read == 0)
                {
                    break; // end of stream without the zero blocks some writers omit
                }
                if (read < BlockSize)
                {
                    throw new InvalidDataException($"'{archiveName}' ends partway through a tar header.");
                }
                if (IsZeroBlock(header))
                {
                    break; // end-of-archive marker
                }
                if (!ChecksumMatches(header))
                {
                    throw new InvalidDataException(sawEntry
                        ? $"'{archiveName}' has a corrupt tar header."
                        : $"'{archiveName}' is not a tar archive.");
                }
                sawEntry = true;

                char type = (char)header[156];
                long size = ParseNumber(header, 124, 12);

                switch (type)
                {
                    case 'L': // GNU long name for the next entry
                        gnuLongName = ReadString(tar, size);
                        continue;
                    case 'K': // GNU long link target for the next entry
                        gnuLongLink = ReadString(tar, size);
                        continue;
                    case 'x': // pax extended header for the next entry
                        entryPax = ParsePax(ReadBytes(tar, size), archiveName);
                        continue;
                    case 'g': // pax global header for all following entries
                        foreach (var kv in ParsePax(ReadBytes(tar, size), archiveName))
                        {
                            globalPax[kv.Key] = kv.Value;
                        }
                        continue;
                }

                string name = Pick("path", entryPax, globalPax) ?? gnuLongName ?? UstarName(header);
                string linkName = Pick("linkpath", entryPax, globalPax) ?? gnuLongLink ?? ReadField(header, 157, 100);
                string paxSize = Pick("size", entryPax, globalPax);
                if (paxSize != null)
                {
                    size = long.Parse(paxSize, System.Globalization.CultureInfo.InvariantCulture);
                }
                entryPax = null;
                gnuLongName = null;
                gnuLongLink = null;

                string relative = Normalize(name);
                switch (type)
                {
                    case '0':
                    case '\0':
                    case '7': // contiguous file: a regular file to every reader
                        {
                            var target = Target(root, relative, name, archiveName);
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            using (var output = File.Create(target))
                            {
                                CopyExactly(tar, output, size, archiveName);
                            }
                            SkipPadding(tar, size);
                            break;
                        }
                    case '5':
                        if (relative.Length > 0)
                        {
                            Directory.CreateDirectory(Target(root, relative, name, archiveName));
                        }
                        SkipData(tar, size);
                        break;
                    case '1':
                    case '2':
                        throw new InvalidDataException(
                            $"'{archiveName}' holds a link ({name} -> {linkName}); installers cannot contain links.");
                    default:
                        throw new InvalidDataException(
                            $"'{archiveName}' holds '{name}', a tar entry of type '{type}' (device, FIFO or unknown) that an installer cannot contain.");
                }
            }
        }

        private static string Pick(string key, Dictionary<string, string> entry, Dictionary<string, string> global)
        {
            if (entry != null && entry.TryGetValue(key, out var value)) return value;
            return global.TryGetValue(key, out value) ? value : null;
        }

        // POSIX ustar splits long names into prefix + name; GNU tar uses the prefix bytes for other data,
        // so the prefix only applies when the magic is exactly "ustar\0".
        private static string UstarName(byte[] header)
        {
            string name = ReadField(header, 0, 100);
            bool posixUstar = header[257] == (byte)'u' && header[258] == (byte)'s' && header[259] == (byte)'t' &&
                              header[260] == (byte)'a' && header[261] == (byte)'r' && header[262] == 0;
            if (posixUstar)
            {
                string prefix = ReadField(header, 345, 155);
                if (prefix.Length > 0) return prefix + "/" + name;
            }
            return name;
        }

        private static string Normalize(string name)
        {
            var relative = name.Replace('\\', '/');
            while (relative.StartsWith("./", StringComparison.Ordinal)) relative = relative.Substring(2);
            return relative.TrimEnd('/');
        }

        private static string Target(string root, string relative, string name, string archiveName)
        {
            if (relative.Length == 0 || relative.StartsWith("/", StringComparison.Ordinal) ||
                (relative.Length > 1 && relative[1] == ':'))
            {
                throw new IOException($"Archive entry '{name}' in '{archiveName}' has an absolute or empty path.");
            }
            var target = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"Archive entry '{name}' in '{archiveName}' would extract outside the destination.");
            }
            return target;
        }

        // pax records: "<length> <key>=<value>\\n", where <length> counts the whole record.
        private static Dictionary<string, string> ParsePax(byte[] data, string archiveName)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            int pos = 0;
            while (pos < data.Length && data[pos] != 0)
            {
                int space = Array.IndexOf(data, (byte)' ', pos);
                if (space < 0 ||
                    !int.TryParse(Encoding.ASCII.GetString(data, pos, space - pos), out int length) ||
                    length <= space - pos || pos + length > data.Length)
                {
                    throw new InvalidDataException($"'{archiveName}' has a malformed pax header.");
                }
                // Record minus "<length> " and the trailing newline.
                string record = Encoding.UTF8.GetString(data, space + 1, pos + length - space - 2);
                int eq = record.IndexOf('=');
                if (eq > 0)
                {
                    result[record.Substring(0, eq)] = record.Substring(eq + 1);
                }
                pos += length;
            }
            return result;
        }

        private static long ParseNumber(byte[] header, int offset, int length)
        {
            // GNU base-256 for values that do not fit in octal: high bit of the first byte set.
            if ((header[offset] & 0x80) != 0)
            {
                long value = header[offset] & 0x7F;
                for (int i = 1; i < length; i++) value = (value << 8) | header[offset + i];
                return value;
            }
            string text = Encoding.ASCII.GetString(header, offset, length).Trim(' ', '\0');
            return text.Length == 0 ? 0 : Convert.ToInt64(text, 8);
        }

        private static string ReadField(byte[] header, int offset, int length)
        {
            int end = Array.IndexOf(header, (byte)0, offset, length);
            int count = (end < 0 ? offset + length : end) - offset;
            return Encoding.UTF8.GetString(header, offset, count);
        }

        private static bool ChecksumMatches(byte[] header)
        {
            long expected;
            try
            {
                expected = ParseNumber(header, 148, 8);
            }
            catch (FormatException)
            {
                return false;
            }
            long sum = 0;
            for (int i = 0; i < BlockSize; i++)
            {
                sum += (i >= 148 && i < 156) ? (byte)' ' : header[i];
            }
            return sum == expected;
        }

        private static bool IsZeroBlock(byte[] block)
        {
            foreach (var b in block) if (b != 0) return false;
            return true;
        }

        private static int ReadBlock(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int n = stream.Read(buffer, total, buffer.Length - total);
                if (n == 0) break;
                total += n;
            }
            return total;
        }

        private static byte[] ReadBytes(Stream stream, long size)
        {
            if (size > 16 * 1024 * 1024)
            {
                throw new InvalidDataException("A tar extended header is larger than 16 MB.");
            }
            var data = new byte[size];
            if (ReadBlock(stream, data) != size) throw new InvalidDataException("A tar archive ends partway through an entry.");
            SkipPadding(stream, size);
            return data;
        }

        private static string ReadString(Stream stream, long size) =>
            Encoding.UTF8.GetString(ReadBytes(stream, size)).TrimEnd('\0');

        private static void CopyExactly(Stream source, Stream output, long size, string archiveName)
        {
            var buffer = new byte[81920];
            long remaining = size;
            while (remaining > 0)
            {
                int n = source.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                if (n == 0) throw new InvalidDataException($"'{archiveName}' ends partway through an entry.");
                output.Write(buffer, 0, n);
                remaining -= n;
            }
        }

        private static void SkipData(Stream stream, long size)
        {
            CopyExactly(stream, Stream.Null, size, "the tar archive");
            SkipPadding(stream, size);
        }

        private static void SkipPadding(Stream stream, long size)
        {
            long padding = (BlockSize - size % BlockSize) % BlockSize;
            if (padding > 0) CopyExactly(stream, Stream.Null, padding, "the tar archive");
        }
    }
}
