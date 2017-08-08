using System.Collections.Generic;

namespace GitRead.Net.Data
{
    public class Commit
    {
        public Commit(string tree, string[] parents, string author)
        {
            Tree = tree;
            Author = author;
            Parents = parents;
        }

        public string Tree { get; }

        public IReadOnlyCollection<string> Parents { get; }

        public string Author { get; }
    }
}