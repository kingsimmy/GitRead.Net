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
            this.repoPath = repoPath.EndsWith(".git") ? repoPath : Path.Combine(repoPath, ".git");
        }

        public string ReadHead()
        {
            string[] lines = File.ReadAllLines(Path.Combine(repoPath, "HEAD"));
            return lines[0];
        }

        public string GetBranch(string branchName)
        {
            string refFilePath = Path.Combine(repoPath, "refs", "heads", branchName);
            if (File.Exists(refFilePath))
            {
                string[] lines = File.ReadAllLines(refFilePath);
                return lines[0];
            }
            string packedRefsFilePath = Path.Combine(repoPath, "packed-refs");
            if (File.Exists(packedRefsFilePath))
            {
                return File.ReadAllLines(packedRefsFilePath)
                    .Where(x => !x.StartsWith("#"))
                    .Select(x => x.Split(' '))
                    .Where(x => x[1].EndsWith(branchName))
                    .First()[0];
            }
            throw new Exception($"Could not find file {refFilePath} or file {packedRefsFilePath}");
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

        internal void ReadIndex(string name, string hash)
        {
            using (FileStream fileStream = File.OpenRead(Path.Combine(repoPath, "objects", "pack", name + ".idx")))
            {
                byte[] buffer = new byte[4];
                fileStream.Read(buffer, 0, 4);
                if(buffer[0] != 255 || buffer[1] != 116 || buffer[2] != 79 || buffer[3] != 99)
                {
                    throw new Exception("Invalid index file");
                }
                fileStream.Read(buffer, 0, 4);
                if (buffer[0] != 0 || buffer[1] != 0 || buffer[2] != 0 || buffer[3] != 2)
                {
                    throw new Exception("Invalid index file version");
                }
                int fanoutIndex = Convert.ToInt32(hash.Substring(0, 2), 16); //Gives us a number between 0 and 255
                int numberOfHashesToSkip;
                if (fanoutIndex == 0)
                {
                    numberOfHashesToSkip = 0;
                }
                else
                {
                    fileStream.Seek((fanoutIndex - 1) * 4, SeekOrigin.Current);
                    fileStream.Read(buffer, 0, 4);
                    Array.Reverse(buffer);
                    numberOfHashesToSkip = BitConverter.ToInt32(buffer, 0);
                }
                fileStream.Read(buffer, 0, 4);
                Array.Reverse(buffer);
                int endIndex = BitConverter.ToInt32(buffer, 0);
                byte[] hashBytes = HexStringToBytes(hash);
                int indexForHash = BinarySearch(fileStream, hashBytes, numberOfHashesToSkip, endIndex);
            }
        }

        private byte[] HexStringToBytes(string str)
        {
            byte[] res = new byte[str.Length / 2];
            for(int i = 0; i < res.Length; i++)
            {
                res[i] = Convert.ToByte(str.Substring(i * 2, 2), 16);
            }
            return res;
        }

        private int BinarySearch(FileStream fileStream, byte[] hash, int startIndex, int endIndex)
        {
            byte[] buffer = new byte[20];
            int startOfHashes = 4 + 4 + (256 * 4);
            int toCheck = (startIndex + endIndex + 1) / 2;
            fileStream.Seek(startOfHashes + (toCheck * 20), SeekOrigin.Begin);
            fileStream.Read(buffer, 0, 20);
            string readHash = String.Concat(buffer.Select(x => x.ToString("X2")));
            ComparisonResult comparison = Compare(hash, buffer);
            if (comparison == ComparisonResult.Equal)
            {
                return toCheck;
            }
            if(startIndex == endIndex)
            {
                throw new Exception("Could not find hash");
            }
            if(comparison == ComparisonResult.Less)
            {
                return BinarySearch(fileStream, hash, startIndex, toCheck - 1);
            }
            else
            {
                return BinarySearch(fileStream, hash, toCheck + 1, endIndex);
            }
        }

        private ComparisonResult Compare(byte[] a, byte[] b)
        {
            for(int i = 0; i < a.Length; i++)
            {
                if(a[i] < b[i])
                {
                    return ComparisonResult.Less;
                }
                if(a[i] > b[i])
                {
                    return ComparisonResult.Greater;
                }
            }
            return ComparisonResult.Equal;
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
            FileStream fileStream = File.OpenRead(Path.Combine(repoPath, "objects", folderName, fileName));
            fileStream.Seek(2, SeekOrigin.Begin);
            return fileStream;
        }

        private enum ComparisonResult
        {
            Less,
            Equal,
            Greater
        }
    }
}