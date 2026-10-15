using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using NUnit.Framework;
using TownSuite.MsiCreator;

namespace TownSuite.MsiCreator.Tests
{
    [TestFixture]
    public class SourceResolverTests
    {
        private const string MainExe = "TownSuite.exe";
        private string _root = null!;
        private string _build = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "sourceresolver-" + Guid.NewGuid().ToString("N"));
            // A release package: the build inside a single top-level x64 folder.
            _build = Path.Combine(_root, "build");
            Directory.CreateDirectory(Path.Combine(_build, "x64", "lib"));
            File.WriteAllText(Path.Combine(_build, "x64", MainExe), "exe");
            File.WriteAllText(Path.Combine(_build, "x64", "lib", "a.dll"), "dll");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        private static string Fixture(string name) =>
            Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles", name);

        private void AssertBuildAt(string directory)
        {
            Assert.That(File.ReadAllText(Path.Combine(directory, MainExe)), Is.EqualTo("exe"));
            Assert.That(File.ReadAllText(Path.Combine(directory, "lib", "a.dll")), Is.EqualTo("dll"));
        }

        // Writes the build as a GNU-format tar, the format ts-jenkins-shared-library's tarzst.ps1 produces.
        private string WriteTar(string path, Func<Stream, Stream> compress)
        {
            using (var fs = File.Create(path))
            using (var stream = compress(fs))
            {
                TarFile.CreateFromDirectory(Path.Combine(_build, "x64"), stream, includeBaseDirectory: true);
            }
            return path;
        }

        private static Stream Zstd(Stream s) => new ZstdSharp.CompressionStream(s, 3);

        [Test]
        public void Zip_IsExtractedFlattenedAndCleanedUp()
        {
            string zip = Path.Combine(_root, "pkg.zip");
            ZipFile.CreateFromDirectory(_build, zip);

            string temp;
            using (var source = SourceResolver.Resolve(zip, MainExe))
            {
                AssertBuildAt(source.Directory);
                Assert.That(Path.GetFileName(source.Directory), Is.EqualTo("x64"));
                temp = source.TemporaryDirectory;
                Assert.That(Directory.Exists(temp), Is.True);
            }
            Assert.That(Directory.Exists(temp), Is.False);
        }

        [Test]
        public void TarZst_IsExtractedAndFlattened()
        {
            string archive = WriteTar(Path.Combine(_root, "pkg.tar.zst"), Zstd);

            Assert.That(SourceResolver.Detect(archive), Is.EqualTo(SourceFormat.TarZstd));
            using (var source = SourceResolver.Resolve(archive, MainExe))
            {
                AssertBuildAt(source.Directory);
            }
        }

        [Test]
        public void TarGz_IsExtracted()
        {
            string archive = WriteTar(Path.Combine(_root, "pkg.tar.gz"), s => new GZipStream(s, CompressionLevel.Fastest));

            Assert.That(SourceResolver.Detect(archive), Is.EqualTo(SourceFormat.TarGzip));
            using (var source = SourceResolver.Resolve(archive, MainExe))
            {
                AssertBuildAt(source.Directory);
            }
        }

        [Test]
        public void PlainTar_IsExtracted()
        {
            string archive = WriteTar(Path.Combine(_root, "pkg.tar"), s => s);

            Assert.That(SourceResolver.Detect(archive), Is.EqualTo(SourceFormat.Tar));
            using (var source = SourceResolver.Resolve(archive, MainExe))
            {
                AssertBuildAt(source.Directory);
            }
        }

        [Test]
        public void Format_IsReadFromContent_NotTheName()
        {
            // native-installers saves every download as source.zip.
            string archive = WriteTar(Path.Combine(_root, "source.zip"), Zstd);

            Assert.That(SourceResolver.Detect(archive), Is.EqualTo(SourceFormat.TarZstd));
            using (var source = SourceResolver.Resolve(archive, MainExe))
            {
                AssertBuildAt(source.Directory);
            }
        }

        [Test]
        public void LongPaths_FromBsdtarGnuFormat_AreExtracted()
        {
            // Made with bsdtar --format=gnutar (as on the Windows agents) and the zstd CLI.
            using (var source = SourceResolver.Resolve(Fixture("longpath-bsd-gnutar.tar.zst"), "UpdateServices.exe"))
            {
                string deep = Path.Combine(source.Directory, "runtimes", "win-x64", "native",
                    "a-directory-name-long-enough-to-need-a-pax-path-record", "another-long-segment-0123456789",
                    "file-with-a-long-name-too.dll");
                Assert.That(File.ReadAllText(deep), Is.EqualTo("deep\n"));
            }
        }

        [Test]
        public void Directory_IsUsedInPlaceFlattenedAndNeverDeleted()
        {
            using (var source = SourceResolver.Resolve(_build, MainExe))
            {
                AssertBuildAt(source.Directory);
                Assert.That(source.TemporaryDirectory, Is.Null);
            }
            Assert.That(File.Exists(Path.Combine(_build, "x64", MainExe)), Is.True);
        }

        [Test]
        public void MissingSource_Throws()
        {
            Assert.Throws<FileNotFoundException>(() => SourceResolver.Resolve(Path.Combine(_root, "nope.tar.zst"), MainExe));
        }

        [Test]
        public void UnknownFormat_Throws()
        {
            string file = Path.Combine(_root, "page.zip");
            File.WriteAllText(file, "<html>404</html>");

            Assert.Throws<InvalidDataException>(() => SourceResolver.Resolve(file, MainExe));
        }

        [Test]
        public void MissingMainExecutable_ThrowsAndCleansUp()
        {
            string zip = Path.Combine(_root, "pkg.zip");
            ZipFile.CreateFromDirectory(_build, zip);
            int before = Directory.GetDirectories(Path.GetTempPath(), "msicreator-*").Length;

            Assert.Throws<FileNotFoundException>(() => SourceResolver.Resolve(zip, "Other.exe"));
            Assert.That(Directory.GetDirectories(Path.GetTempPath(), "msicreator-*").Length, Is.EqualTo(before));
        }

        [Test]
        public void EntryOutsideDestination_IsRejected()
        {
            string archive = Path.Combine(_root, "escape.tar.zst");
            using (var fs = File.Create(archive))
            using (var zstd = Zstd(fs))
            using (var writer = new TarWriter(zstd, TarEntryFormat.Gnu, leaveOpen: false))
            {
                writer.WriteEntry(new GnuTarEntry(TarEntryType.RegularFile, "../escaped.txt") { DataStream = new MemoryStream(new byte[] { 1 }) });
            }

            Assert.Throws<IOException>(() => SourceResolver.Resolve(archive, MainExe));
            Assert.That(File.Exists(Path.Combine(Path.GetTempPath(), "escaped.txt")), Is.False);
        }

        [Test]
        public void Links_AreRejected()
        {
            string archive = Path.Combine(_root, "link.tar.zst");
            using (var fs = File.Create(archive))
            using (var zstd = Zstd(fs))
            using (var writer = new TarWriter(zstd, TarEntryFormat.Gnu, leaveOpen: false))
            {
                writer.WriteEntry(new GnuTarEntry(TarEntryType.SymbolicLink, "x64/link") { LinkName = "../../outside" });
            }

            var ex = Assert.Throws<InvalidDataException>(() => SourceResolver.Resolve(archive, MainExe));
            Assert.That(ex!.Message, Does.Contain("link"));
        }

        [Test]
        public void PaxFormat_FromGnuTar_UsesThePaxPath()
        {
            // GNU tar --format=pax writes a pax header before every entry; the 166-character path exists
            // only in its "path" record.
            using (var source = SourceResolver.Resolve(Fixture("pax-format.tar.zst"), "UpdateServices.exe"))
            {
                string deep = Path.Combine(source.Directory, "runtimes", "win-x64", "native",
                    "a-directory-name-long-enough-to-need-a-pax-path-record", "another-long-segment-0123456789",
                    "file-with-a-long-name-too.dll");
                Assert.That(File.ReadAllText(deep), Is.EqualTo("deep\n"));
                Assert.That(Directory.GetFiles(source.Directory, "*", SearchOption.AllDirectories), Has.Length.EqualTo(2),
                    "pax headers must not be extracted as files");
            }
        }

        [TestCase(TarEntryFormat.Pax)]
        [TestCase(TarEntryFormat.Gnu)]
        [TestCase(TarEntryFormat.Ustar)]
        public void LongNames_InEachTarFormat_AreExtracted(TarEntryFormat format)
        {
            // Ustar splits a long name into prefix + name; pax and GNU carry it in an extra header.
            string longDir = "x64/" + new string('d', 90) + "/" + new string('e', 60);
            string archive = Path.Combine(_root, $"long-{format}.tar.zst");
            using (var fs = File.Create(archive))
            using (var zstd = Zstd(fs))
            using (var writer = new TarWriter(zstd, format, leaveOpen: false))
            {
                writer.WriteEntry(NewEntry(format, TarEntryType.RegularFile, "x64/" + MainExe, "exe"));
                writer.WriteEntry(NewEntry(format, TarEntryType.RegularFile, longDir + "/f.dll", "deep"));
            }

            using (var source = SourceResolver.Resolve(archive, MainExe))
            {
                string deep = Path.Combine(source.Directory, new string('d', 90), new string('e', 60), "f.dll");
                Assert.That(File.ReadAllText(deep), Is.EqualTo("deep"));
            }
        }

        [Test]
        public void ZstdFrameWithoutATarInside_IsReportedAsNotATar()
        {
            string archive = Path.Combine(_root, "not-tar.tar.zst");
            using (var fs = File.Create(archive))
            using (var zstd = Zstd(fs))
            {
                var junk = new byte[2048];
                new Random(1).NextBytes(junk);
                zstd.Write(junk, 0, junk.Length);
            }

            var ex = Assert.Throws<InvalidDataException>(() => SourceResolver.Resolve(archive, MainExe));
            Assert.That(ex!.Message, Does.Contain("not a tar archive"));
        }

        private static TarEntry NewEntry(TarEntryFormat format, TarEntryType type, string name, string content)
        {
            TarEntry entry = format switch
            {
                TarEntryFormat.Pax => new PaxTarEntry(type, name),
                TarEntryFormat.Gnu => new GnuTarEntry(type, name),
                _ => new UstarTarEntry(type, name),
            };
            entry.DataStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            return entry;
        }
    }
}
