namespace GitRead.Net.Data
{
    public class TreeEntry
    {
        public TreeEntry(string name, string hash, string mode)
        {
            Name = name;
            Hash = hash;
            Mode = mode;
        }

        public string Name { get; }

        public string Hash { get; }

        public string Mode { get; }
    }
}