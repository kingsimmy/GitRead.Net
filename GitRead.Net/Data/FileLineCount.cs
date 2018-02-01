namespace GitRead.Net.Data
{
    public class FileLineCount
    {
        public FileLineCount(string filePath, int lineCount)
        {
            FilePath = filePath;
            LineCount = lineCount;
        }

        public string FilePath { get; }

        public int LineCount { get; }

        public override string ToString()
        {
            return $"{FilePath} {LineCount}";
        }
    }
}