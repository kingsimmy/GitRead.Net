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
    }
}