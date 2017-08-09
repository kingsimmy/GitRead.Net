using System;

namespace GitRead.Net.Data
{
    internal class TreeEntry
    {
        public TreeEntry(string name, string hash, string mode)
        {
            Name = name;
            Hash = hash;
            Mode = (TreeEntryMode)Convert.ToInt32(mode, 8);
        }

        public string Name { get; }

        public string Hash { get; }

        public TreeEntryMode Mode { get; }
    }
}