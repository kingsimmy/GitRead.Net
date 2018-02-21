namespace GitRead.Net.Data
{
    public class FileChange
    {
        public FileChange(string path, int numberOfLinesAdded, int numberOfLinesDeleted)
        {
            Path = path;
            NumberOfLinesAdded = numberOfLinesAdded;
            NumberOfLinesDeleted = numberOfLinesDeleted;
        }

        public string Path { get; }

        public int NumberOfLinesAdded { get; }

        public int NumberOfLinesDeleted { get; }

        public override string ToString()
        {
            return $"{Path} {NumberOfLinesAdded} added {NumberOfLinesDeleted} deleted";
        }
    }
}