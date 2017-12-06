using GitRead.Net.Data;
using System.Collections.Generic;
using System.Linq;
using System.IO;

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

        public IEnumerable<string> GetFilePaths()
        {
            string head = repositoryReader.ReadHead();
            string commitHash = repositoryReader.ReadBranch(head);
            return GetFilePaths(commitHash);
        }

        public IEnumerable<string> GetFilePaths(string commitHash)
        {
            Commit commit = repositoryReader.ReadCommit(commitHash);
            Queue<(string, string)> treeHashes = new Queue<(string, string)>();
            treeHashes.Enqueue((commit.Tree, string.Empty));
            while (treeHashes.Count > 0)
            {
                (string hash, string folder) = treeHashes.Dequeue();
                foreach (TreeEntry treeEntry in repositoryReader.ReadTree(hash))
                {
                    switch (treeEntry.Mode)
                    {
                        case TreeEntryMode.Directory:
                            treeHashes.Enqueue((treeEntry.Hash, folder + treeEntry.Name + Path.DirectorySeparatorChar));
                            break;
                        case TreeEntryMode.RegularExecutableFile:
                        case TreeEntryMode.RegularNonExecutableFile:
                        case TreeEntryMode.RegularNonExecutableGroupWriteableFile:
                            yield return folder + treeEntry.Name;
                            break;
                    }
                }
            }
        }

        public IEnumerable<Commit> Commits
        {
            get
            {
                string head = repositoryReader.ReadHead();
                string commitHash = repositoryReader.ReadBranch(head);
                HashSet<string> readCommits = new HashSet<string>();
                Queue<string> toReadCommits = new Queue<string>();
                toReadCommits.Enqueue(commitHash);
                while (toReadCommits.Count > 0)
                {
                    string hash = toReadCommits.Dequeue();
                    if (readCommits.Contains(hash))
                    {
                        continue;
                    }
                    Commit current = repositoryReader.ReadCommit(hash);
                    yield return current;
                    readCommits.Add(hash);
                    foreach(string parentHash in current.Parents)
                    {
                        toReadCommits.Enqueue(parentHash);
                    }
                }
            }
        }
    }
}