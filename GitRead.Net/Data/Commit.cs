using System.Collections.Generic;

namespace GitRead.Net.Data
{
    public class Commit
    {
        public Commit(string hash, string tree, List<string> parents, string author, string message)
        {
            Hash = hash;
            Tree = tree;
            Author = author;
            Parents = parents;
            Message = message;
        }

        public string Hash { get; }

        public string Tree { get; }

        public IReadOnlyCollection<string> Parents { get; }

        public string Author { get; }

        public string Message { get; }
    }
}