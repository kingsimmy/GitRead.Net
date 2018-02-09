namespace GitRead.Net.Data
{
    public class FileChange
    {
        public FileChange(string path, int numberOfLines)
        {
            Path = path;
            NumberOfLines = numberOfLines;
        }

        public string Path { get; }

        public int NumberOfLines { get; }

        public override string ToString()
        {
            return $"{Path} {NumberOfLines}";
        }
    }
}