using System.Text;
using System.IO;
using System.IO.Compression;

namespace GitRead.Net
{
    public class Reader
    {
        private const int whiteSpace = ' ';
        private const int nullChar = '\0';

        private readonly string repoPath;

        public Reader(string repoPath)
        {
            this.repoPath = repoPath;
        }

        public string ReadHead()
        {
            string[] lines = File.ReadAllLines(Path.Combine(repoPath, ".git", "HEAD"));
            return lines[0];
        }

        public string GetBranch(string refPath)
        {
            string[] lines = File.ReadAllLines(Path.Combine(repoPath, ".git", "refs", "heads", refPath));
            return lines[0];
        }

        public string ReadLooseFile(string hash)
        {
            string folderName = hash.Substring(0, 2);
            string fileName = hash.Substring(2);
            FileStream fs = File.OpenRead(Path.Combine(repoPath, ".git", "objects", folderName, fileName));
            fs.Seek(2, SeekOrigin.Begin);
            using (StreamReader reader = new StreamReader(new DeflateStream(fs, CompressionMode.Decompress), Encoding.UTF8))
            {
                StringBuilder gitFileType = new StringBuilder();
                int ch;
                while((ch = reader.Read()) != whiteSpace)
                {
                    gitFileType.Append((char)ch);
                }
                StringBuilder gitFileSize = new StringBuilder();
                while ((ch = reader.Read()) != nullChar)
                {
                    gitFileSize.Append((char)ch);
                }
                string value = reader.ReadToEnd();
                
                return value;
            }
        }
    }
}