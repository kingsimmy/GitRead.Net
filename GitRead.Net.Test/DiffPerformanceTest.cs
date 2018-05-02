using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GitRead.Net.Data;
using NUnit.Framework;
using static GitRead.Net.RepositoryAnalyzer;

namespace GitRead.Net.Test
{
    [TestFixture]
    public class DiffPerformanceTest
    {
        [Test]
        public void PerformanceTest()
        {
            int comparisons = 600;
            Prepare(comparisons, out string[] content1, out string[] content2);
            int repeatCount = 25;
            double total = 0;
            foreach (int repeat in Enumerable.Range(0, repeatCount))
            {
                TestContext.Progress.Write(repeat);
                Stopwatch watch = Stopwatch.StartNew();
                for (int i = 0; i < comparisons; i++)
                {
                    DiffGenerator.GetLinesChanged(content1[0], content2[0]);
                }
                watch.Stop();
                total += watch.ElapsedMilliseconds;
            }
            Console.WriteLine($"Average {total / repeatCount}ms");
            TestContext.Progress.WriteLine($"Average {total / repeatCount}ms");
        }

        private void Prepare(int comparisons, out string[] content1, out string[] content2)
        {
            string repoDir = TestUtils.ExtractZippedRepo("vcpkg.git");
            RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(repoDir);
            RepositoryReader repositoryReader = new RepositoryReader(repoDir);
            content1 = new string[comparisons];
            content2 = new string[comparisons];
            int i = 0;
            foreach (Commit commit in repositoryAnalyzer.GetCommits().Where(x => x.Parents.Any()))
            {
                Dictionary<string, PathHashMode> current = repositoryAnalyzer.GetPathAndHashForFiles(commit.Hash).ToDictionary(x => x.Path);
                Dictionary<string, PathHashMode> parent = repositoryAnalyzer.GetPathAndHashForFiles(commit.Parents[0]).ToDictionary(x => x.Path);
                foreach ((string hash1, string hash2) in current.Keys.Intersect(parent.Keys).Select(x => (current[x].Hash, parent[x].Hash)))
                {
                    if (hash1 != hash2)
                    {
                        content1[i] = repositoryReader.ReadBlob(hash1);
                        content2[i] = repositoryReader.ReadBlob(hash2);
                        i++;
                    }
                    if (i == comparisons)
                    {
                        break;
                    }
                }
                if (i == comparisons)
                {
                    break;
                }
            }
        }
    }
}
