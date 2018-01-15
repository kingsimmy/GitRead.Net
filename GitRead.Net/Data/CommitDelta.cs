using System.Collections.Generic;

namespace GitRead.Net.Data
{
    public class CommitDelta
    {
        public CommitDelta(List<string> added, List<string> deleted, List<string> modified)
        {
            Added = added;
            Deleted = deleted;
            Modified = modified;
        }

        public IReadOnlyList<string> Added { get; }

        public IReadOnlyList<string> Deleted { get; }

        public IReadOnlyList<string> Modified { get; }
    }
}