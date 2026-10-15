using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace TownSuite.MsiCreator
{
    internal enum SourceFormat
    {
        Directory,
        Zip,
        TarZstd,
        TarGzip,
        Tar,
        Unknown
    }

    /// <summary>
    /// The folder the installer is built from, and the temporary folder to delete afterwards when the
    /// source was an archive. A folder the caller passed in is never deleted.
    /// </summary>
    internal sealed class ResolvedSource : IDisposable
    {
        public ResolvedSource(string directory, string temporaryDirectory)
        {
            Directory = directory;
            TemporaryDirectory = temporaryDirectory;
        }

        public string Directory { get; }

        /// <summary>Null when the source was a folder used in place.</summary>
        public string TemporaryDirectory { get; }

        public void Dispose()
        {
            if (TemporaryDirectory == null || !System.IO.Directory.Exists(TemporaryDirectory))
            {
                return;
            }
            try
            {
                System.IO.Directory.Delete(TemporaryDirectory, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up temporary directory: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Turns -Src (or -SrcZip / -SrcBinDirectory) into the folder the installer is built from. The source
    /// may be a folder, a .zip, or a tar archive compressed with Zstandard (.tar.zst), gzip (.tar.gz) or
    /// not at all; archives are recognised from their first bytes, so the file name does not matter.
    /// TownSuite release pipelines publish .tar.zst packages (TownSuite.IssueTracker#16326).
    ///
    /// A source whose only content is one folder -- the x64\ wrapping a release package -- is
    /// flattened to that folder when the main executable is not at the top.
    /// </summary>
    internal static class SourceResolver
    {
        public static ResolvedSource Resolve(string source, string mainExecutable)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("No source was given. Pass -Src with a folder, .zip or .tar.zst.");
            }

            var format = Detect(source);
            string root;
            string temporaryDirectory = null;

            switch (format)
            {
                case SourceFormat.Directory:
                    root = Path.GetFullPath(source);
                    break;
                case SourceFormat.Unknown:
                    if (!File.Exists(source))
                    {
                        throw new FileNotFoundException($"The source '{source}' does not exist.", source);
                    }
                    throw new InvalidDataException(
                        $"The source '{source}' is not a folder, zip, or tar archive (.tar, .tar.zst, .tar.gz).");
                default:
                    temporaryDirectory = Path.Combine(Path.GetTempPath(), "msicreator-" + Guid.NewGuid().ToString("N"));
                    System.IO.Directory.CreateDirectory(temporaryDirectory);
                    try
                    {
                        if (format == SourceFormat.Zip)
                        {
                            ZipFile.ExtractToDirectory(source, temporaryDirectory);
                        }
                        else
                        {
                            ExtractTar(source, temporaryDirectory);
                        }
                    }
                    catch
                    {
                        new ResolvedSource(temporaryDirectory, temporaryDirectory).Dispose();
                        throw;
                    }
                    root = temporaryDirectory;
                    break;
            }

            var resolved = new ResolvedSource(Flatten(root, mainExecutable), temporaryDirectory);
            if (!string.IsNullOrWhiteSpace(mainExecutable) &&
                !File.Exists(Path.Combine(resolved.Directory, mainExecutable)))
            {
                resolved.Dispose();
                throw new FileNotFoundException(
                    $"The main executable '{mainExecutable}' is not in '{source}' (looked in its top folder, " +
                    "and in its only subfolder when it holds nothing else).");
            }
            return resolved;
        }

        public static SourceFormat Detect(string path)
        {
            if (System.IO.Directory.Exists(path))
            {
                return SourceFormat.Directory;
            }
            if (!File.Exists(path))
            {
                return SourceFormat.Unknown;
            }

            var header = new byte[262];
            int read;
            using (var fs = File.OpenRead(path))
            {
                read = ReadFully(fs, header);
            }

            if (read >= 4 && header[0] == 0x50 && header[1] == 0x4B &&
                ((header[2] == 0x03 && header[3] == 0x04) || (header[2] == 0x05 && header[3] == 0x06)))
            {
                return SourceFormat.Zip;
            }
            // Zstandard frame magic number 0xFD2FB528, little endian.
            if (read >= 4 && header[0] == 0x28 && header[1] == 0xB5 && header[2] == 0x2F && header[3] == 0xFD)
            {
                return SourceFormat.TarZstd;
            }
            if (read >= 2 && header[0] == 0x1F && header[1] == 0x8B)
            {
                return SourceFormat.TarGzip;
            }
            // ustar magic at offset 257 (POSIX and GNU tar headers).
            if (read >= 262 && header[257] == (byte)'u' && header[258] == (byte)'s' && header[259] == (byte)'t' &&
                header[260] == (byte)'a' && header[261] == (byte)'r')
            {
                return SourceFormat.Tar;
            }
            return SourceFormat.Unknown;
        }

        private static void ExtractTar(string archive, string destination)
        {
            using (var fs = File.OpenRead(archive))
            using (var stream = Decompress(fs, Detect(archive)))
            {
                TarReader.ExtractToDirectory(stream, destination, archive);
            }
        }

        private static Stream Decompress(Stream source, SourceFormat format)
        {
            switch (format)
            {
                case SourceFormat.TarZstd:
                    return new ZstdSharp.DecompressionStream(source);
                case SourceFormat.TarGzip:
                    return new GZipStream(source, CompressionMode.Decompress);
                default:
                    return source;
            }
        }

        private static string Flatten(string root, string mainExecutable)
        {
            if (!string.IsNullOrWhiteSpace(mainExecutable) && File.Exists(Path.Combine(root, mainExecutable)))
            {
                return root;
            }
            var files = System.IO.Directory.GetFiles(root);
            var folders = System.IO.Directory.GetDirectories(root);
            return files.Length == 0 && folders.Length == 1 ? folders[0] : root;
        }

        private static int ReadFully(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int n = stream.Read(buffer, total, buffer.Length - total);
                if (n == 0)
                {
                    break;
                }
                total += n;
            }
            return total;
        }
    }
}
