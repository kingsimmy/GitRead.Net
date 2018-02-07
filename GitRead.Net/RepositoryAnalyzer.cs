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
            return GetPathAndHashForFiles(commitHash).Select(x => x.path);
        }

        public IEnumerable<FileLineCount> GetFileLineCounts()
        {
            string head = repositoryReader.ReadHead();
            string commitHash = repositoryReader.ReadBranch(head);
            return GetFileLineCounts(commitHash);
        }

        public IEnumerable<FileLineCount> GetFileLineCounts(string commitHash)
        {
            int GetCount(string hash)
            {
                string content = repositoryReader.ReadBlob(hash);
                return content.Length == 0 ? 0 : content.Count(c => c == '\n') + 1;
            }
            return GetPathAndHashForFiles(commitHash).Select(x => new FileLineCount(x.path, x.mode == TreeEntryMode.RegularExecutableFile ? 0 : GetCount(x.hash)));
        }

        public CommitDelta GetChanges(string commitHash)
        {
            Commit commit = repositoryReader.ReadCommit(commitHash);
            List<string> added = new List<string>();
            List<string> deleted = new List<string>();
            List<string> modified = new List<string>();
            if (commit.Parents.Count == 0 || commit.Parents.Count > 2)
            {
                return new CommitDelta(added, deleted, modified);
            }
            string commitHashParent1 = commit.Parents[0];
            Dictionary<string, string> filePathToHashParent1 = GetPathAndHashForFiles(commitHashParent1).ToDictionary(x => x.path, x => x.hash);
            Dictionary<string, string> filePathToHashParent2 = null;
            if (commit.Parents.Count > 1)
            {
                string commitHashParent2 = commit.Parents[1];
                filePathToHashParent2 = GetPathAndHashForFiles(commitHashParent2).ToDictionary(x => x.path, x => x.hash);
            }
            
            Dictionary<string, string> filePathToHashNow = GetPathAndHashForFiles(commitHash).ToDictionary(x => x.path, x => x.hash);
            HashSet<string> allFilePaths = new HashSet<string>(Enumerable.Concat(filePathToHashNow.Keys, filePathToHashParent1.Keys));
            if(filePathToHashParent2 != null)
            {
                allFilePaths.UnionWith(filePathToHashParent2.Keys);
            }
            foreach (string filePath in allFilePaths)
            {
                bool existedInCommitNow = filePathToHashNow.TryGetValue(filePath, out string hashNow);
                bool existedInCommitBefore1 = filePathToHashParent1.TryGetValue(filePath, out string hashBefore1);                
                if (filePathToHashParent2 != null)
                {
                    bool existedInCommitBefore2 = filePathToHashParent2.TryGetValue(filePath, out string hashBefore2);
                    if ((existedInCommitBefore1 && existedInCommitNow && hashBefore1 != hashNow) && (existedInCommitBefore2 && existedInCommitNow && hashBefore2 != hashNow))
                    {
                        modified.Add(filePath);
                    }
                    else if ((existedInCommitBefore1 || existedInCommitBefore2) && !existedInCommitNow)
                    {
                        deleted.Add(filePath);
                    }
                    else if (!existedInCommitBefore1 && !existedInCommitBefore2 && existedInCommitNow)
                    {
                        added.Add(filePath);
                    }
                }
                else if(existedInCommitBefore1 && existedInCommitNow && hashBefore1 != hashNow)
                {
                    modified.Add(filePath);
                }
                else if (existedInCommitBefore1 && !existedInCommitNow)
                {
                    deleted.Add(filePath);
                }
                else if (!existedInCommitBefore1 && existedInCommitNow)
                {
                    added.Add(filePath);
                }
            }
            return new CommitDelta(added, deleted, modified);
        }

        private IEnumerable<(string path, string hash, TreeEntryMode mode)> GetPathAndHashForFiles(string commitHash)
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
                            yield return (folder + treeEntry.Name, treeEntry.Hash, treeEntry.Mode);
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