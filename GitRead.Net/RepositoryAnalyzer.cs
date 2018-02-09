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
            return GetPathAndHashForFiles(commitHash).Select(x => x.Path);
        }

        public IEnumerable<FileLineCount> GetFileLineCounts()
        {
            string head = repositoryReader.ReadHead();
            string commitHash = repositoryReader.ReadBranch(head);
            return GetFileLineCounts(commitHash);
        }

        public IEnumerable<FileLineCount> GetFileLineCounts(string commitHash)
        {
            return GetPathAndHashForFiles(commitHash).Select(x => new FileLineCount(x.Path, GetLineCount(x.Hash, x.Mode)));
        }

        public CommitDelta GetChanges(string commitHash)
        {
            Commit commit = repositoryReader.ReadCommit(commitHash);
            List<FileChange> added = new List<FileChange>();
            List<FileChange> deleted = new List<FileChange>();
            List<FileChange> modified = new List<FileChange>();
            if (commit.Parents.Count == 0 || commit.Parents.Count > 2)
            {
                return new CommitDelta(added, deleted, modified);
            }
            string commitHashParent1 = commit.Parents[0];
            Dictionary<string, PathHashMode> filePathToHashParent1 = GetPathAndHashForFiles(commitHashParent1).ToDictionary(x => x.Path);
            Dictionary<string, PathHashMode> filePathToHashParent2 = null;
            if (commit.Parents.Count > 1)
            {
                string commitHashParent2 = commit.Parents[1];
                filePathToHashParent2 = GetPathAndHashForFiles(commitHashParent2).ToDictionary(x => x.Path);
            }
            
            Dictionary<string, PathHashMode> filePathToHashNow = GetPathAndHashForFiles(commitHash).ToDictionary(x => x.Path);
            HashSet<string> allFilePaths = new HashSet<string>(Enumerable.Concat(filePathToHashNow.Keys, filePathToHashParent1.Keys));
            if(filePathToHashParent2 != null)
            {
                allFilePaths.UnionWith(filePathToHashParent2.Keys);
            }
            foreach (string filePath in allFilePaths)
            {
                bool existedInCommitNow = filePathToHashNow.TryGetValue(filePath, out PathHashMode now);
                bool existedInCommitBefore1 = filePathToHashParent1.TryGetValue(filePath, out PathHashMode before1);                
                if (filePathToHashParent2 != null)
                {
                    bool existedInCommitBefore2 = filePathToHashParent2.TryGetValue(filePath, out PathHashMode before2);
                    if ((existedInCommitBefore1 && existedInCommitNow && before1.Hash != now.Hash) && (existedInCommitBefore2 && existedInCommitNow && before2.Hash != now.Hash))
                    {
                        var changes = GetLinesChanged(before1.Hash, before2.Hash, now.Hash);
                        modified.Add(new FileChange(now.Path, changes.added + changes.deleted));
                    }
                    else if ((existedInCommitBefore1 || existedInCommitBefore2) && !existedInCommitNow)
                    {
                        deleted.Add(new FileChange(before1.Path, GetLineCount(before1.Hash, before1.Mode)));
                    }
                    else if (!existedInCommitBefore1 && !existedInCommitBefore2 && existedInCommitNow)
                    {
                        added.Add(new FileChange(now.Path, GetLineCount(now.Hash, now.Mode)));
                    }
                }
                else if(existedInCommitBefore1 && existedInCommitNow && before1.Hash != now.Hash)
                {
                    var changes = GetLinesChanged(before1.Hash, before1.Mode, now.Hash, now.Mode);
                    modified.Add(new FileChange(now.Path, changes.added +  changes.deleted));
                }
                else if (existedInCommitBefore1 && !existedInCommitNow)
                {
                    deleted.Add(new FileChange(before1.Path, GetLineCount(before1.Hash, before1.Mode)));
                }
                else if (!existedInCommitBefore1 && existedInCommitNow)
                {
                    added.Add(new FileChange(now.Path, GetLineCount(now.Hash, now.Mode)));
                }
            }
            return new CommitDelta(added, deleted, modified);
        }
        
        private (int added, int deleted) GetLinesChanged(string hash1, TreeEntryMode mode1, string hash2, TreeEntryMode mode2)
        {
            if(mode1 == TreeEntryMode.RegularExecutableFile || mode2 == TreeEntryMode.RegularExecutableFile)
            {
                return (0, 0);
            }
            Dictionary<int, List<string>> allLines1 = repositoryReader.ReadBlob(hash1).Split('\n')
                .GroupBy(x => x.GetHashCode()).ToDictionary(x => x.Key, x => x.ToList());
            Dictionary<int, List<string>> allLines2 = repositoryReader.ReadBlob(hash2).Split('\n')
                .GroupBy(x => x.GetHashCode()).ToDictionary(x => x.Key, x => x.ToList());
            int added = 0;
            int deleted = 0;
            foreach(int hashCode in Enumerable.Concat(allLines1.Keys, allLines2.Keys).Distinct())
            {
                bool existed1 = allLines1.TryGetValue(hashCode, out List<string> lines1);
                bool existed2 = allLines2.TryGetValue(hashCode, out List<string> lines2);
                if (existed1 && existed2)
                {
                    List<string> lines2Copy = new List<string>(lines2);
                    foreach(string line in lines1)
                    {
                        if (lines2Copy.Contains(line))
                        {
                            lines2Copy.Remove(line);
                        }
                        else
                        {
                            deleted++;
                        }
                    }
                    foreach (string line in lines2)
                    {
                        if (lines1.Contains(line))
                        {
                            lines1.Remove(line);
                        }
                        else
                        {
                            added++;
                        }
                    }
                }
                else if (existed1)
                {
                    deleted += lines1.Count;
                }
                else if (existed2)
                {
                    added += lines2.Count;
                }
            }
            return (added, deleted);
        }

        private (int added, int deleted) GetLinesChanged(string hashBefore1, string hashBefore2, string hashNow)
        {
            Dictionary<int, List<string>> allLinesBefore1 = repositoryReader.ReadBlob(hashBefore1).Split('\n')
                .GroupBy(x => x.GetHashCode()).ToDictionary(x => x.Key, x => x.ToList());
            Dictionary<int, List<string>> allLinesBefore2 = repositoryReader.ReadBlob(hashBefore2).Split('\n')
                .GroupBy(x => x.GetHashCode()).ToDictionary(x => x.Key, x => x.ToList());
            Dictionary<int, List<string>> allLinesNow = repositoryReader.ReadBlob(hashNow).Split('\n')
                .GroupBy(x => x.GetHashCode()).ToDictionary(x => x.Key, x => x.ToList());
            int added = 0;
            int deleted = 0;
            foreach (KeyValuePair<int, List<string>> before in allLinesBefore1)
            {
                if (allLinesNow.TryGetValue(before.Key, out List<string> linesNow))
                {
                    foreach(string line in before.Value)
                    {
                        if (!linesNow.Contains(line))
                        {
                            deleted++;
                        }
                    }
                }
                else
                {
                    deleted += before.Value.Count;
                }
            }
            foreach (KeyValuePair<int, List<string>> before in allLinesBefore2)
            {
                if (allLinesNow.TryGetValue(before.Key, out List<string> linesNow))
                {
                    foreach (string line in before.Value)
                    {
                        if (!linesNow.Contains(line))
                        {
                            deleted++;
                        }
                    }
                }
                else
                {
                    deleted += before.Value.Count;
                }
            }
            foreach (KeyValuePair<int, List<string>> now in allLinesNow)
            {
                List<string> linesBefore1 = allLinesBefore1.ContainsKey(now.Key) ? allLinesBefore1[now.Key] : new List<string>();
                List<string> linesBefore2 = allLinesBefore2.ContainsKey(now.Key) ? allLinesBefore2[now.Key] : new List<string>();
                foreach(string line in now.Value)
                {
                    if (linesBefore1.Contains(line))
                    {
                        linesBefore1.Remove(line);
                        continue;
                    }
                    else if (linesBefore2.Contains(line))
                    {
                        linesBefore2.Remove(line);
                        continue;
                    }
                    else
                    {
                        added++;
                    }
                }
            }
            return (added, deleted);
        }

        private int GetLineCount(string hash, TreeEntryMode mode)
        {
            if (mode == TreeEntryMode.RegularExecutableFile)
            {
                return 0;
            }
            string content = repositoryReader.ReadBlob(hash);
            return content.Length == 0 ? 0 : content.Count(c => c == '\n') + 1;
        }

        private IEnumerable<PathHashMode> GetPathAndHashForFiles(string commitHash)
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
                            yield return new PathHashMode(folder + treeEntry.Name, treeEntry.Hash, treeEntry.Mode);
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

        private class PathHashMode
        {
            public PathHashMode(string path, string hash, TreeEntryMode mode)
            {
                Path = path;
                Hash = hash;
                Mode = mode;
            }

            public string Path { get; }

            public string Hash { get; }

            public TreeEntryMode Mode { get; }
        }
    }
}