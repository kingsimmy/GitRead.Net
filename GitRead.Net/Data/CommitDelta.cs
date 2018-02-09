using System.Collections.Generic;

namespace GitRead.Net.Data
{
    public class CommitDelta
    {
        public CommitDelta(List<FileChange> added, List<FileChange> deleted, List<FileChange> modified)
        {
            Added = added;
            Deleted = deleted;
            Modified = modified;
        }

        public IReadOnlyList<FileChange> Added { get; }

        public IReadOnlyList<FileChange> Deleted { get; }

        public IReadOnlyList<FileChange> Modified { get; }

        public override string ToString()
        {
            return $"Added: {Added.Count} Deleted: {Deleted.Count} Modified: {Modified.Count}";
        }
    }
}