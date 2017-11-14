using GitRead.Net.Data;
using System.Collections.Generic;
using System.Linq;

namespace GitRead.Net
{
    public class RepositoryAnalyzer
    {
        private readonly RepositoryReader repositoryReader;

        public RepositoryAnalyzer(string repoPath)
        {
            repositoryReader = new RepositoryReader(repoPath);
        }

        public int GetTotalNumberOfCommits()
        {
            return Commits.Count();
        }

        public IEnumerable<Commit> Commits
        {
            get
            {
                string head = repositoryReader.ReadHead();
                string hashForCommit = repositoryReader.ReadBranch(head);
                HashSet<string> readCommits = new HashSet<string>();
                List<string> toReadCommits = new List<string>() { hashForCommit };
                while (toReadCommits.Count > 0)
                {
                    string hash = toReadCommits.First();
                    toReadCommits.RemoveAt(0);
                    if (readCommits.Contains(hash))
                    {
                        continue;
                    }
                    Commit current = repositoryReader.ReadCommit(hash);
                    yield return current;
                    readCommits.Add(hash);                    
                    toReadCommits.AddRange(current.Parents);
                }
            }
        }
    }
}