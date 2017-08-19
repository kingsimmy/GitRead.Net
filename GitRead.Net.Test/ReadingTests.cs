using GitRead.Net.Data;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;

namespace GitRead.Net.Test
{
    [TestFixture]
    public class ReadingTests
    {
        [Test]
        public void Test01ReadLooseBlob()
        {
            string repoDir = TestUtils.ExtractZippedRepo("TestRepo01");
            Reader reader = new Reader(repoDir);
            string res = reader.ReadLooseFile("d670460b4b4aece5915caf5c68d12f560a9fe3e4");
            Assert.AreEqual("test content\n", res);
        }

        [Test]
        public void Test02ReadCommit()
        {
            string repoDir = TestUtils.ExtractZippedRepo("TestRepo02");
            Reader reader = new Reader(repoDir);
            string hash = reader.GetBranch("master");
            Commit commit = reader.ReadCommit(hash);
            StringAssert.AreEqualIgnoringCase("ce2d3a85f185830a19e84d404155bf9847ede8b8", commit.Tree);
        }

        [Test]
        public void Test02ReadTree()
        {
            string repoDir = TestUtils.ExtractZippedRepo("TestRepo02");
            Reader reader = new Reader(repoDir);
            IReadOnlyList<TreeEntry> res = reader.ReadTree("ce2d3a85f185830a19e84d404155bf9847ede8b8");
            Assert.AreEqual(res.Count, 1);
            StringAssert.AreEqualIgnoringCase("31d6d2184fe8deab8e52bd9581d67f35d4ecd5ca", res[0].Hash);
            Assert.AreEqual("mydocument.txt", res[0].Name);
            Assert.AreEqual(TreeEntryMode.RegularNonExecutableFile, res[0].Mode);
        }

        [Test]
        public void Test02ReadLooseBlob()
        {
            string repoDir = TestUtils.ExtractZippedRepo("TestRepo02");
            Reader reader = new Reader(repoDir);
            string res = reader.ReadLooseFile("31d6d2184fe8deab8e52bd9581d67f35d4ecd5ca");
            Assert.AreEqual("abc xyz", res);
        }

        [Test]
        public void TestCsharplangReadPackedRef()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            Reader reader = new Reader(repoDir);
            string res = reader.GetBranch("master");
            Assert.AreEqual("411106b0108a37789ed3d53fd781acf8f75ef97b", res);
        }

        [Test]
        public void TestCsharplangReadIndex()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            Reader reader = new Reader(repoDir);
            long packFileOffset = reader.ReadIndex("pack-dae4b1886286da035b337f24ab5b707ad18d8a3c", "411106b0108a37789ed3d53fd781acf8f75ef97b");
            Assert.AreEqual(744249, packFileOffset);
        }

        [Test]
        public void TestCsharplangReadPackfile()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            Reader reader = new Reader(repoDir);
            string hash = "411106b0108a37789ed3d53fd781acf8f75ef97b";
            reader.ReadPackFile("pack-dae4b1886286da035b337f24ab5b707ad18d8a3c", hash, 744249, (FileStream f) => reader.ReadCommitFromStream(f, hash));
        }
    }
}