﻿using System.Text;
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
        private readonly byte[] fourByteBuffer = new byte[4];
        private readonly byte[] eightByteBuffer = new byte[8];
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

        internal void ReadPackFile(string name, long offset)
        {
            byte[] lengthBuffer = new byte[1];
            using (FileStream fileStream = File.OpenRead(Path.Combine(repoPath, "objects", "pack", name + ".pack")))
            {
                fileStream.Seek(offset, SeekOrigin.Begin);
                fileStream.Read(lengthBuffer, 0, 1);
                PackFileObjectType packFileObjectType = (PackFileObjectType)((lengthBuffer[0] & 0b0111_0000) >> 4);
                ulong length = (ulong)(lengthBuffer[0] & 0b0000_1111); //First four bits are dropped as they are they are readNextByte indicator and packFileObjectType
                while ((lengthBuffer[0] & 0b1000_0000) != 0)
                {
                    fileStream.Read(lengthBuffer, 0, 1);
                    length = length << 7;
                    length = length + (byte)(lengthBuffer[0] & 0b0111_1111); //First bit is dropped as it is the readNextByte indicator
                }
                switch (packFileObjectType)
                {
                    case PackFileObjectType.Commit:
                        Commit result = ReadCommitFromStream(fileStream, length);
                        return;
                }
            }
        }

        internal Commit ReadCommitFromStream(FileStream fileStream, ulong inflatedSize)
        {
            fileStream.Seek(2, SeekOrigin.Current);
            using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(deflateStream, Encoding.UTF8))
            {
                return ReadCommitCore(reader);
            }
        }

        internal long ReadIndex(string name, string hash)
        {
            using (FileStream fileStream = File.OpenRead(Path.Combine(repoPath, "objects", "pack", name + ".idx")))
            {
                byte[] buffer = new byte[4];
                fileStream.Read(buffer, 0, 4);
                if (buffer[0] != 255 || buffer[1] != 116 || buffer[2] != 79 || buffer[3] != 99)
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
                int endIndex = ReadInt32(fileStream);
                byte[] hashBytes = HexStringToBytes(hash);
                int indexForHash = BinarySearch(fileStream, hashBytes, numberOfHashesToSkip, endIndex);
                int lastFanoutPos = 4 + 4 + (255 * 4);
                int totalNumberOfHashes = ReadInt32(fileStream, lastFanoutPos);
                int indexInto4ByteOffsets = lastFanoutPos + 4 + (20 * totalNumberOfHashes) + (4 * totalNumberOfHashes) + (4 * indexForHash);
                fileStream.Seek(indexInto4ByteOffsets, SeekOrigin.Begin);
                fileStream.Read(fourByteBuffer, 0, 4);
                bool use8ByteOffsets = (fourByteBuffer[3] & 0b1000_0000) != 0;
                long offset;
                if (!use8ByteOffsets)
                {
                    Array.Reverse(fourByteBuffer);
                    offset = BitConverter.ToInt32(fourByteBuffer, 0);
                }
                else
                {
                    fourByteBuffer[3] = (byte)(fourByteBuffer[3] & 0b0111_1111);
                    Array.Reverse(fourByteBuffer);
                    int indexInto8ByteOffsets = BitConverter.ToInt32(fourByteBuffer, 0);
                    indexInto4ByteOffsets = lastFanoutPos + 4 + (20 * totalNumberOfHashes) + (4 * totalNumberOfHashes) + (4 * totalNumberOfHashes) + (4 * indexInto8ByteOffsets);
                    offset = ReadInt64(fileStream, indexInto4ByteOffsets);
                }
                return offset;
            }
        }

        private int ReadInt32(FileStream fileStream, int pos = -1)
        {
            if (pos != -1)
            {
                fileStream.Seek(pos, SeekOrigin.Begin);
            }
            fileStream.Read(fourByteBuffer, 0, 4);
            Array.Reverse(fourByteBuffer);
            return BitConverter.ToInt32(fourByteBuffer, 0);
        }

        private long ReadInt64(FileStream fileStream, int pos)
        {
            fileStream.Seek(pos, SeekOrigin.Begin);
            fileStream.Read(eightByteBuffer, 0, 8);
            Array.Reverse(eightByteBuffer);
            return BitConverter.ToInt64(eightByteBuffer, 0);
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
                if (gitFileType.ToString() != "commit")
                {
                    throw new Exception("Invalid commit object");
                }
                StringBuilder gitFileSize = new StringBuilder();
                while ((ch = reader.Read()) != nullChar)
                {
                    gitFileSize.Append((char)ch);
                }
                return ReadCommitCore(reader);
            }
        }

        private static Commit ReadCommitCore(StreamReader reader)
        {
            string treeLine = reader.ReadLine();
            if (!treeLine.StartsWith("tree"))
            {
                throw new Exception("Invalid commit object");
            }
            string tree = treeLine.Substring(5);
            List<string> parents = new List<string>();
            string line = reader.ReadLine();
            while (line.StartsWith("parent"))
            {
                parents.Add(line.Substring(7));
                line = reader.ReadLine();
            }
            string authorLine = line;
            if (!authorLine.StartsWith("author"))
            {
                throw new Exception("Invalid commit object");
            }
            string author = authorLine.Substring(7);
            string committerLine = reader.ReadLine();
            if (!committerLine.StartsWith("committer"))
            {
                throw new Exception("Invalid commit object");
            }
            string committer = committerLine.Substring(10);
            reader.ReadLine();
            string message = reader.ReadToEnd();
            return new Commit(tree, parents, author, message);
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