using NUnit.Framework;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace GitRead.Net.Test
{
    [TestFixture]
    public class ReadingTests
    {
        [Test]
        public void TestOne()
        {
            string repoName = "TestRepo01";
            string repoDir = ExtractZippedRepo(repoName);
            Reader reader = new Reader(repoDir);
            string res = reader.ReadLooseFile("d670460b4b4aece5915caf5c68d12f560a9fe3e4");
            Assert.AreEqual("test content\n", res);
        }

        [Test]
        public void TestTwo()
        {
            string repoName = "TestRepo02";
            string repoDir = ExtractZippedRepo(repoName);
            Reader reader = new Reader(repoDir);
            string hash = reader.GetBranch("master");
            string res = reader.ReadLooseFile(hash);
            Assert.True(res.StartsWith("tree"));
        }

        private string ExtractZippedRepo(string repoName)
        {
            string repoDir = Path.GetTempPath() + repoName;
            if (Directory.Exists(repoDir))
            {
                Directory.Delete(repoDir, true);
            }
            using (Stream resourceStream = GetType().GetTypeInfo().Assembly.GetManifestResourceStream($"GitRead.Net.Test.Repos.{repoName}.zip"))
            using (ZipArchive archive = new ZipArchive(resourceStream))
            {
                archive.ExtractToDirectory(repoDir);
            }
            return repoDir;
        }
    }
}