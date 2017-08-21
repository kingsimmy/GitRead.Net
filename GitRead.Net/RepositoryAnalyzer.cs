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
            string head = repositoryReader.ReadHead();
            string hashForCommit = repositoryReader.ReadBranch(head);            
            HashSet<string> readCommits = new HashSet<string>();
            List<string> toReadCommits = new List<string>() { hashForCommit };
            while (toReadCommits.Count > 0)
            {
                string hash = toReadCommits[0];
                toReadCommits.RemoveAt(0);
                Commit current = repositoryReader.ReadCommit(hash);
                readCommits.Add(current.Hash);
                toReadCommits.AddRange(current.Parents.Where(x => !readCommits.Contains(x)));
            }
            return readCommits.Count;
        }
    }
}