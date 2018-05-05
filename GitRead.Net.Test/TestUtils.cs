using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace GitRead.Net.Test
{
    internal class TestUtils
    {
        internal static string ExtractZippedRepo(string repoName)
        {
            string repoDir = Path.GetTempPath() + repoName;
            if (Directory.Exists(repoDir))
            {
                Directory.Delete(repoDir, true);
            }
            using (Stream resourceStream = typeof(TestUtils).Assembly.GetManifestResourceStream($"GitRead.Net.Test.Repos.{repoName}.zip"))
            using (ZipArchive archive = new ZipArchive(resourceStream))
            {
                archive.ExtractToDirectory(repoDir);
            }
            return repoDir;
        }

        internal static Dictionary<string, string> ExtractZippedFiles(string zipName)
        {
            string filesDir = Path.GetTempPath() + zipName;
            if (Directory.Exists(filesDir))
            {
                Directory.Delete(filesDir, true);
            }
            using (Stream resourceStream = typeof(TestUtils).Assembly.GetManifestResourceStream($"GitRead.Net.Test.Files.{zipName}.zip"))
            using (ZipArchive archive = new ZipArchive(resourceStream))
            {
                archive.ExtractToDirectory(filesDir);
            }
            return Directory.EnumerateFiles(filesDir).ToDictionary(x => Path.GetFileName(x), x => File.ReadAllText(x));
        }
    }
}