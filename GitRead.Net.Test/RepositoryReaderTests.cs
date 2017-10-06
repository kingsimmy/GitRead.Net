using GitRead.Net.Data;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace GitRead.Net.Test
{
    [TestFixture]
    public class RepositoryReaderTests
    {
        [Test]
        public void Test01ReadLooseBlob()
        {
            string repoDir = TestUtils.ExtractZippedRepo("TestRepo01");
            RepositoryReader reader = new RepositoryReader(repoDir);
            string res = reader.ReadBlob("d670460b4b4aece5915caf5c68d12f560a9fe3e4");
            Assert.AreEqual("test content\n", res);
        }

        [Test]
        public void Test02ReadCommit()
        {
            string repoDir = TestUtils.ExtractZippedRepo("TestRepo02");
            RepositoryReader reader = new RepositoryReader(repoDir);
            string hash = reader.ReadBranch("master");
            Commit commit = reader.ReadCommit(hash);
            StringAssert.AreEqualIgnoringCase("ce2d3a85f185830a19e84d404155bf9847ede8b8", commit.Tree);
        }

        [Test]
        public void Test02ReadTree()
        {
            string repoDir = TestUtils.ExtractZippedRepo("TestRepo02");
            RepositoryReader reader = new RepositoryReader(repoDir);
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
            RepositoryReader reader = new RepositoryReader(repoDir);
            string res = reader.ReadBlob("31d6d2184fe8deab8e52bd9581d67f35d4ecd5ca");
            Assert.AreEqual("abc xyz", res);
        }

        [Test]
        public void TestCsharplangReadPackedRef()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryReader reader = new RepositoryReader(repoDir);
            string res = reader.ReadBranch("master");
            Assert.AreEqual("411106b0108a37789ed3d53fd781acf8f75ef97b", res);
        }

        [Test]
        public void TestCsharplangReadIndex()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            PackIndexReader reader = new PackIndexReader(repoDir);
            long packFileOffset = reader.ReadIndex("pack-dae4b1886286da035b337f24ab5b707ad18d8a3c", "411106b0108a37789ed3d53fd781acf8f75ef97b");
            Assert.AreEqual(744249, packFileOffset);
        }

        [Test]
        public void TestCsharplangReadPackfile()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryReader reader = new RepositoryReader(repoDir);
            string hash = "411106b0108a37789ed3d53fd781acf8f75ef97b";
            Commit res = reader.ReadCommit(hash);
            Assert.AreEqual(res.Message, "Add design notes\n");
            Assert.AreEqual(res.Tree, "1af7239766b45f2c85f422a99867919ca9e1e935");
            Assert.AreEqual(res.Author, "Mads Torgersen");
            Assert.AreEqual(res.EmailAddress, "mads.torgersen@microsoft.com");
            Assert.AreEqual(res.Timestamp, new DateTime(2017,8,9,0,17,9));
            Assert.AreEqual(res.TimeZoneOffset, "-0700");
        }

        [Test]
        public void TestCsharplangReadTree()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryReader reader = new RepositoryReader(repoDir);
            string hash = "1af7239766b45f2c85f422a99867919ca9e1e935";
            var res = reader.ReadTree(hash);
            Assert.AreEqual(res.Count, 6);
            Assert.AreEqual(res[0].Hash, "176A458F94E0EA5272CE67C36BF30B6BE9CAF623");
            Assert.AreEqual(res[0].Mode, TreeEntryMode.RegularNonExecutableFile);
            Assert.AreEqual(res[0].Name, ".gitattributes");
        }

    }
}