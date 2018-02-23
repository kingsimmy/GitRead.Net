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
            return GetCommits().Count();
        }

        /// <summary>
        /// Returns the filepath for every file which exists in the repository as of the head.
        /// </summary>
        public IEnumerable<string> GetFilePaths()
        {
            string head = repositoryReader.ReadHead();
            string commitHash = repositoryReader.ReadBranch(head);
            return GetFilePaths(commitHash);
        }

        /// <summary>
        /// Returns the filepath for every file which exists in the repository as of a specific commit.
        /// </summary>
        /// <param name="commitHash">The hash of the commit to run for</param>
        public IEnumerable<string> GetFilePaths(string commitHash)
        {
            return GetPathAndHashForFiles(commitHash).Select(x => x.Path);
        }

        /// <summary>
        /// For every file which exists as of the head this method returns the filepath along with the number of lines in the file.
        /// </summary>
        public IEnumerable<FileLineCount> GetFileLineCounts()
        {
            string head = repositoryReader.ReadHead();
            string commitHash = repositoryReader.ReadBranch(head);
            return GetFileLineCounts(commitHash);
        }

        /// <summary>
        /// For every file which exists as of a specific commit this method returns the filepath along with the number of lines in the file.
        /// </summary>
        /// <param name="commitHash">The hash of the commit to run for</param>
        public IEnumerable<FileLineCount> GetFileLineCounts(string commitHash)
        {
            return GetPathAndHashForFiles(commitHash).Select(x => new FileLineCount(x.Path, GetLineCount(x.Hash, x.Mode)));
        }

        /// <summary>
        /// Returns commits which modified the specified file ordered by most recent change through to the commit which created the file.
        /// </summary>
        /// <param name="filePath">The path to the file which you want to see the commit history of</param>
        public IReadOnlyList<Commit> GetCommitsForOneFilePath(string filePath)
        {
            string head = repositoryReader.ReadHead();
            string commitHash = repositoryReader.ReadBranch(head);
            return GetCommitsForOneFilePath(filePath, commitHash);
        }

        /// <summary>
        /// Returns commits which modified the specified file going back from the specified commit.
        /// Results are ordered by most recent change through to the commit which created the file.
        /// </summary>
        /// <param name="filePath">The path to the file which you want to see the commit history of</param>
        /// <param name="commitHash">The hash of the commit to start looking back from</param>
        public IReadOnlyList<Commit> GetCommitsForOneFilePath(string filePath, string commitHash)
        {
            List<string> contentHashes = new List<string>();
            Dictionary<string, Commit> earliestCommit = new Dictionary<string, Commit>();
            foreach (Commit commit in GetCommits(commitHash))
            {
                if (TryGetContentHashForPath(commit.Tree, filePath, out string contentHash))
                {
                    if (!earliestCommit.ContainsKey(contentHash))
                    {
                        contentHashes.Add(contentHash);
                        earliestCommit[contentHash] = commit;
                    }
                    else if (earliestCommit[contentHash].Timestamp > commit.Timestamp)
                    {
                        earliestCommit[contentHash] = commit;
                    }
                }
            }
            return contentHashes.Select(x => earliestCommit[x]).ToList();
        }

        /// <summary>
        /// Provides a list of commits which modified a file ordered by most recent for every file which exists as of the head.
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<Commit>> GetCommitsForAllFilePaths()
        {
            string head = repositoryReader.ReadHead();
            string commitHash = repositoryReader.ReadBranch(head);
            return GetCommitsForAllFilePaths(commitHash);
        }

        /// <summary>
        /// Provides a list of commits which modified a file ordered by most recent for every file which exists as of a specific commit.
        /// </summary>
        /// <param name="commitHash">The hash of the commit to run for</param>
        public IReadOnlyDictionary<string, IReadOnlyList<Commit>> GetCommitsForAllFilePaths(string commitHash)
        {
            Dictionary<string, List<string>> contentHashesByPath = GetFilePaths(commitHash).ToDictionary(x => x, x=> new List<string>());
            Dictionary<string, Commit> earliestCommit = new Dictionary<string, Commit>();
            Dictionary<string, IReadOnlyList<TreeEntry>> treeCache = new Dictionary<string, IReadOnlyList<TreeEntry>>();
            foreach (Commit commit in GetCommits())
            {
                foreach (string filePath in contentHashesByPath.Keys)
                {
                    if (TryGetContentHashForPath(commit.Tree, filePath, out string contentHash, treeCache))
                    {
                        if (!earliestCommit.ContainsKey(contentHash))
                        {
                            contentHashesByPath[filePath].Add(contentHash);
                            earliestCommit[contentHash] = commit;
                        }
                        else if (earliestCommit[contentHash].Timestamp > commit.Timestamp)
                        {
                            earliestCommit[contentHash] = commit;
                        }
                    }
                }
            }
            Dictionary<string, IReadOnlyList<Commit>> result = new Dictionary<string, IReadOnlyList<Commit>>();
            foreach (string filePath in contentHashesByPath.Keys)
            {
                result[filePath] = contentHashesByPath[filePath].Select(x => earliestCommit[x]).ToList();
            }
            return result;
        }

        /// <summary>
        /// For a specific commit this provides a list of files added, a list of files modified and a list of the files deleted by that commit.
        /// </summary>
        /// <param name="commitHash">The hash of the commit to run for</param>
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
            if (filePathToHashParent2 != null)
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
                        int linesAdded = 0;
                        int linesDeleted = 0;
                        if (before1.Mode != TreeEntryMode.RegularExecutableFile && before2.Mode != TreeEntryMode.RegularExecutableFile && now.Mode != TreeEntryMode.RegularExecutableFile)
                        {
                            (linesAdded, linesDeleted) =
                                DiffGenerator.GetLinesChanged(repositoryReader.ReadBlob(before1.Hash), repositoryReader.ReadBlob(before2.Hash), repositoryReader.ReadBlob(now.Hash));
                        }
                        modified.Add(new FileChange(now.Path, linesAdded, linesDeleted));
                    }
                    else if ((existedInCommitBefore1 || existedInCommitBefore2) && !existedInCommitNow)
                    {
                        deleted.Add(new FileChange(before1.Path, 0, GetLineCount(before1.Hash, before1.Mode)));
                    }
                    else if (!existedInCommitBefore1 && !existedInCommitBefore2 && existedInCommitNow)
                    {
                        added.Add(new FileChange(now.Path, GetLineCount(now.Hash, now.Mode), 0));
                    }
                }
                else if (existedInCommitBefore1 && existedInCommitNow && before1.Hash != now.Hash)
                {
                    int linesAdded = 0;
                    int linesDeleted = 0;
                    if (before1.Mode != TreeEntryMode.RegularExecutableFile && now.Mode != TreeEntryMode.RegularExecutableFile)
                    {
                        (linesAdded, linesDeleted) = DiffGenerator.GetLinesChanged(repositoryReader.ReadBlob(before1.Hash), repositoryReader.ReadBlob(now.Hash));
                    }
                    modified.Add(new FileChange(now.Path, linesAdded, linesDeleted));
                }
                else if (existedInCommitBefore1 && !existedInCommitNow)
                {
                    deleted.Add(new FileChange(before1.Path, 0, GetLineCount(before1.Hash, before1.Mode)));
                }
                else if (!existedInCommitBefore1 && existedInCommitNow)
                {
                    added.Add(new FileChange(now.Path, GetLineCount(now.Hash, now.Mode), 0));
                }
            }
            return new CommitDelta(added, deleted, modified);
        }

        /// <summary>
        /// Yields commits starting with the head commit followed by its parents and then the parents of those commits until 
        /// the last commit yielded is the original commit created in the repository.
        /// This method implements a topological sorting which ensures that the the parent of a commit 'x' will never be before 'x'.
        /// </summary>
        public IEnumerable<Commit> GetCommits()
        {
            string head = repositoryReader.ReadHead();
            string commitHash = repositoryReader.ReadBranch(head);
            return GetCommits(commitHash);
        }

        /// <summary>
        /// Yields commits starting with the specified commit followed by its parents and then the parents of those commits until 
        /// the last commit yielded is the original commit created in the repository.
        /// This method implements a topological sorting which ensures that the the parent of a commit 'x' will never be before 'x'.
        /// </summary>
        public IEnumerable<Commit> GetCommits(string commitHash)
        {
            Dictionary<string, int> inDegree = new Dictionary<string, int>() { { commitHash, 0 } };
            Dictionary<string, Commit> readCommits = new Dictionary<string, Commit>();
            Queue<string> toReadCommits = new Queue<string>();
            toReadCommits.Enqueue(commitHash);
            while (toReadCommits.Count > 0)
            {
                string hash = toReadCommits.Dequeue();
                if (readCommits.ContainsKey(hash))
                {
                    continue;
                }
                Commit current = repositoryReader.ReadCommit(hash);
                readCommits.Add(hash, current);
                foreach (string parentHash in current.Parents)
                {
                    toReadCommits.Enqueue(parentHash);
                    if (!inDegree.TryGetValue(parentHash, out int val))
                    {
                        val = 0;
                    }
                    inDegree[parentHash] = val + 1;
                }
            }
            while (inDegree.Count > 0)
            {
                Commit current = inDegree.Where(x => x.Value == 0).Select(x => readCommits[x.Key]).OrderBy(x => x.Timestamp).Last();
                inDegree.Remove(current.Hash);
                foreach (string parentHash in current.Parents)
                {
                    inDegree[parentHash]--;
                    toReadCommits.Enqueue(parentHash);
                }
                yield return current;
            }
        }

        private bool TryGetContentHashForPath(string rootTreeHash, string filePath, out string contentHash, Dictionary<string, IReadOnlyList<TreeEntry>> treeCache = null)
        {
            string treeHash = rootTreeHash;
            string[] segments = filePath.Split(Path.DirectorySeparatorChar);
            foreach (string segment in segments.Take(segments.Length - 1))
            {
                treeHash = GetEntries(treeCache, treeHash).Where(x => x.Mode == TreeEntryMode.Directory && x.Name == segment).FirstOrDefault()?.Hash;
                if (treeHash == null)
                {
                    contentHash = null;
                    return false;
                }
            }
            contentHash = GetEntries(treeCache, treeHash).Where(x => x.Name == segments[segments.Length - 1]).FirstOrDefault()?.Hash;
            return contentHash != null;
        }

        private IReadOnlyList<TreeEntry> GetEntries(Dictionary<string, IReadOnlyList<TreeEntry>> treeCache, string treeHash)
        {
            if (treeCache == null)
            {
                return repositoryReader.ReadTree(treeHash);
            }
            if (!treeCache.TryGetValue(treeHash, out IReadOnlyList<TreeEntry> entries))
            {
                entries = repositoryReader.ReadTree(treeHash);
                treeCache[treeHash] = entries;
            }
            return entries;
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