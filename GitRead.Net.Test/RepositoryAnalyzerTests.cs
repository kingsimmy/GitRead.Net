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
        public void TestGetChangesByCommitOneParentFilesAdded()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            CommitDelta changes = repositoryAnalyzer.GetChanges("7981ea1fb4d89571fd41e3b75f7c9f7fc178e837");
            Assert.AreEqual(2, changes.Added.Count);
            Assert.AreEqual(0, changes.Deleted.Count);
            Assert.AreEqual(0, changes.Modified.Count);
            Assert.True(changes.Added.Select(x => x.Path).Contains(@"proposals\nullable-reference-types.md"));
            Assert.True(changes.Added.Select(x => x.Path).Contains(@"design-notes\Notes-2016-11-16.md"));
        }
        

        [Test]
        public void TestGetChangesByCommitOneParentFilesModified()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            CommitDelta changes = repositoryAnalyzer.GetChanges("a5f82604eab5826bd1913cf63c7dfb8c2b187641");
            Assert.AreEqual(0, changes.Added.Count);
            Assert.AreEqual(0, changes.Deleted.Count);
            Assert.AreEqual(2, changes.Modified.Count);
            FileChange readmeChanges = changes.Modified.Where(x => x.Path == @"README.md").First();
            Assert.AreEqual(16, readmeChanges.NumberOfLines);
            FileChange proposalsReadmeChanges = changes.Modified.Where(x => x.Path == @"proposals\README.md").First();
            Assert.AreEqual(2, proposalsReadmeChanges.NumberOfLines);
        }

        [Test]
        public void TestGetChangesByCommitTwoParentsNoChange()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            CommitDelta changes = repositoryAnalyzer.GetChanges("dfb46ac110aacfade7a4a9491b272e6e8ffc4468");
            Assert.AreEqual(0, changes.Added.Count);
            Assert.AreEqual(0, changes.Deleted.Count);
            Assert.AreEqual(0, changes.Modified.Count);
        }
        
        [Test]
        public void TestGetChangesByCommitTwoParentsWithChange()
        {
            string repoDir = TestUtils.ExtractZippedRepo("vcpkg.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            CommitDelta changes = repositoryAnalyzer.GetChanges("dbab03a1a82913ae96bfa3c1613ade20b5ac438d");
            Assert.AreEqual(0, changes.Added.Count);
            Assert.AreEqual(0, changes.Deleted.Count);
            Assert.AreEqual(1, changes.Modified.Count);
            FileChange portfileChanges = changes.Modified.Where(x => x.Path == @"ports\openssl\portfile.cmake").First();
            Assert.AreEqual(1, portfileChanges.NumberOfLines);
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

        [Test]
        public void TestGetCommitsForPath()
        {
            string repoDir = TestUtils.ExtractZippedRepo("csharplang.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            List<Commit> commits = repositoryAnalyzer.GetCommitsForPath(@"proposals\lambda-attributes.md").ToList();
            Assert.AreEqual(2, commits.Count);
        }
    }
}