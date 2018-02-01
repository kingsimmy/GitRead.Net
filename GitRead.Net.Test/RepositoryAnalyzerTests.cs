using System.Collections.Generic;
using System.Linq;
using GitRead.Net.Data;
using NUnit.Framework;

namespace GitRead.Net.Test
{
    [TestFixture]
    public class RepositoryAnalyzerTests
    {
        [Test]
        public void TestCountCommits()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            int count = repositoryAnalyzer.GetTotalNumberOfCommits();
            Assert.AreEqual(157, count);
        }

        [Test]
        public void TestGetFilePathsHead()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            List<string> filePaths = repositoryAnalyzer.GetFilePaths().ToList();
            Assert.AreEqual(152, filePaths.Count);
            Assert.True(filePaths.Contains(".gitattributes"));
            Assert.True(filePaths.Contains(@"meetings\README.md"));
            Assert.True(filePaths.Contains(@"proposals\rejected\README.md"));
        }

        [Test]
        public void TestGetFilePathsSpecificCommit()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            List<string> filePaths = repositoryAnalyzer.GetFilePaths("7981ea1fb4d89571fd41e3b75f7c9f7fc178e837").ToList();
            Assert.AreEqual(6, filePaths.Count);
            Assert.True(filePaths.Contains("README.md"));
            Assert.True(filePaths.Contains(@"design-notes\Notes-2016-11-16.md"));
            Assert.True(filePaths.Contains(@"design-notes\csharp-language-design-notes-2017.md"));
            Assert.True(filePaths.Contains(@"proposals\async-streams.md"));
            Assert.True(filePaths.Contains(@"proposals\nullable-reference-types.md"));
            Assert.True(filePaths.Contains(@"spec\spec.md"));
        }

        [Test]
        public void TestGetChangesByCommit()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            CommitDelta changes = repositoryAnalyzer.GetChanges("7981ea1fb4d89571fd41e3b75f7c9f7fc178e837");
            Assert.AreEqual(2, changes.Added.Count);
            Assert.AreEqual(0, changes.Deleted.Count);
            Assert.AreEqual(0, changes.Modified.Count);
            Assert.True(changes.Added.Contains(@"proposals\nullable-reference-types.md"));
            Assert.True(changes.Added.Contains(@"design-notes\Notes-2016-11-16.md"));
        }

        [Test]
        public void TestGetFileLineCounts()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            Dictionary<string, int> lineCounts = repositoryAnalyzer.GetFileLineCounts("7981ea1fb4d89571fd41e3b75f7c9f7fc178e837").ToDictionary(x => x.FilePath, x => x.LineCount);
            Assert.AreEqual(6, lineCounts.Count);
            Assert.AreEqual(10, lineCounts.GetValueOrDefault(@"README.md", -1));
            Assert.AreEqual(0, lineCounts.GetValueOrDefault(@"proposals\async-streams.md",-1));
            Assert.AreEqual(126, lineCounts.GetValueOrDefault(@"proposals\nullable-reference-types.md", -1));
            Assert.AreEqual(74, lineCounts.GetValueOrDefault(@"design-notes\Notes-2016-11-16.md", -1));
            Assert.AreEqual(0, lineCounts.GetValueOrDefault(@"design-notes\csharp-language-design-notes-2017.md", -1));
            Assert.AreEqual(0, lineCounts.GetValueOrDefault(@"spec\spec.md", -1));
        }
    }
}