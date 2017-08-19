using System.IO;
using System.IO.Compression;

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
    }
}