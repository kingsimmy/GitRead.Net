using GitRead.Net.Data;
using NUnit.Framework;
using System.Collections.Generic;
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
            string repoDir = ExtractZippedRepo("TestRepo01");
            Reader reader = new Reader(repoDir);
            string res = reader.ReadLooseFile("d670460b4b4aece5915caf5c68d12f560a9fe3e4");
            Assert.AreEqual("test content\n", res);
        }

        [Test]
        public void TestTwo()
        {
            string repoDir = ExtractZippedRepo("TestRepo02");
            Reader reader = new Reader(repoDir);
            string hash = reader.GetBranch("master");
            Commit commit = reader.ReadCommit(hash);
            StringAssert.AreEqualIgnoringCase("ce2d3a85f185830a19e84d404155bf9847ede8b8", commit.Tree);
            IReadOnlyList<TreeEntry> res = reader.ReadTree(commit.Tree);
            Assert.AreEqual(res.Count, 1);
            StringAssert.AreEqualIgnoringCase("31d6d2184fe8deab8e52bd9581d67f35d4ecd5ca", res[0].Hash);
            Assert.AreEqual("mydocument.txt", res[0].Name);
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