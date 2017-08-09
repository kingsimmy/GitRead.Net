using System.Text;
using System.IO;
using System.IO.Compression;
using GitRead.Net.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GitRead.Net
{
    public class Reader
    {
        private const int whiteSpace = ' ';
        private const int nullChar = '\0';
        private readonly byte[] oneByteBuffer = new byte[1];
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

        internal string ReadLooseFile(string hash)
        {
            using (FileStream fileStream = OpenStreamForDeflate(hash))
            using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(deflateStream, Encoding.UTF8))
            {
                StringBuilder gitFileType = new StringBuilder();
                int ch;
                while ((ch = reader.Read()) != whiteSpace)
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

        internal IReadOnlyList<TreeEntry> ReadTree(string hash)
        {
            List<TreeEntry> entries = new List<TreeEntry>();
            using (FileStream fileStream = OpenStreamForDeflate(hash))
            using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress))
            {
                string gitFileType = ReadString(deflateStream, whiteSpace);
                if (gitFileType.ToString() != "tree")
                {
                    throw new Exception("Invalid tree object");
                }
                string gitFileSize = ReadString(deflateStream, nullChar);
                int length = int.Parse(gitFileSize);
                while (length > 0)
                {
                    string mode = ReadString(deflateStream, whiteSpace);
                    string name = ReadString(deflateStream, nullChar);
                    byte[] buffer = new byte[20];
                    deflateStream.Read(buffer, 0, 20);
                    string itemHash = String.Concat(buffer.Select(x => x.ToString("X2")));
                    length -= (mode.Length + 1 + name.Length + 1 + itemHash.Length);
                    entries.Add(new TreeEntry(name, itemHash, mode));
                }
            }
            return entries;
        }

        internal Commit ReadCommit(string hash)
        {
            using (FileStream fileStream = OpenStreamForDeflate(hash))
            using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(deflateStream, Encoding.UTF8))
            {
                StringBuilder gitFileType = new StringBuilder();
                int ch;
                while ((ch = reader.Read()) != whiteSpace)
                {
                    gitFileType.Append((char)ch);
                }
                if(gitFileType.ToString() != "commit")
                {
                    throw new Exception("Invalid commit object");
                }
                StringBuilder gitFileSize = new StringBuilder();
                while ((ch = reader.Read()) != nullChar)
                {
                    gitFileSize.Append((char)ch);
                }
                string treeLine = reader.ReadLine();
                if (!treeLine.StartsWith("tree"))
                {
                    throw new Exception("Invalid commit object");
                }
                string tree = treeLine.Substring(5);
                string authorLine = reader.ReadLine();
                if (!authorLine.StartsWith("author"))
                {
                    throw new Exception("Invalid commit object");
                }
                string author = authorLine.Substring(7);
                return new Commit(tree, null, author);
            }
        }

        private string ReadString(Stream stream, int delimiter)
        {
            StringBuilder builder = new StringBuilder();
            char ch = char.MaxValue;
            stream.Read(oneByteBuffer, 0, 1);
            ch = Encoding.UTF8.GetChars(oneByteBuffer)[0];
            while (ch != delimiter)
            {
                builder.Append(ch);
                stream.Read(oneByteBuffer, 0, 1);
                ch = Encoding.UTF8.GetChars(oneByteBuffer)[0];
            } while (ch != delimiter);
            return builder.ToString();
        }

        private FileStream OpenStreamForDeflate(string hash)
        {
            string folderName = hash.Substring(0, 2);
            string fileName = hash.Substring(2);
            FileStream fileStream = File.OpenRead(Path.Combine(repoPath, ".git", "objects", folderName, fileName));
            fileStream.Seek(2, SeekOrigin.Begin);
            return fileStream;
        }
    }
}