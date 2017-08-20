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
            return lines[0].Split('/').Last();
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
            using (FileStream fileStream = File.OpenRead(GetObjectFilePath(hash)))
            using (DeflateStream deflateStream = GetDeflateStreamForZlibData(fileStream))
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
            using (FileStream fileStream = File.OpenRead(GetObjectFilePath(hash)))
            using (DeflateStream deflateStream = GetDeflateStreamForZlibData(fileStream))
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

        internal T ReadPackFile<T>(FileStream fileStream, string hash, long offset, Func<FileStream, ulong, T> extractFunc)
        {
            byte[] lengthBuffer = new byte[1];
            fileStream.Seek(offset, SeekOrigin.Begin);
            fileStream.Read(lengthBuffer, 0, 1);            
            PackFileObjectType packFileObjectType = (PackFileObjectType)((lengthBuffer[0] & 0b0111_0000) >> 4);
            ulong length = (ulong)(lengthBuffer[0] & 0b0000_1111); //First four bits are dropped as they are they are readNextByte indicator and packFileObjectType
            int counter = 0;
            while ((lengthBuffer[0] & 0b1000_0000) != 0)
            {
                counter++;
                fileStream.Read(lengthBuffer, 0, 1);
                length = length + (ulong)((lengthBuffer[0] & 0b0111_1111) << (4 + (7 * (counter - 1)))); //First bit is dropped as it is the readNextByte indicator
            }
            switch (packFileObjectType)
            {
                case PackFileObjectType.Commit:
                    return extractFunc(fileStream, length);
                case PackFileObjectType.Blob:
                    return extractFunc(fileStream, length);
                case PackFileObjectType.ObjOfsDelta:
                    long baseObjOffset = ReadVariableLengthOffset(fileStream);
                    DeflateStream deflateStream = GetDeflateStreamForZlibData(fileStream);
                    int deltaDataLength = (int)length;
                    var sourceDataTuple = ReadVariableLengthSize(deflateStream);
                    deltaDataLength -= sourceDataTuple.bytesRead;
                    var targetLengthTuple = ReadVariableLengthSize(deflateStream);
                    deltaDataLength -= targetLengthTuple.bytesRead;
                    byte[] deltaBytes = new byte[deltaDataLength];
                    deflateStream.Read(deltaBytes, 0, deltaDataLength);
                    byte[] baseBytes = ReadPackFile(fileStream, null, offset - baseObjOffset, (FileStream f, ulong l) => ReadZlibBytes(f, l));
                    if(baseBytes.Length != sourceDataTuple.length)
                    {
                        throw new Exception("Base object did not match expected length");
                    }
                    byte[] undeltifiedData = Undeltify(baseBytes, deltaBytes, targetLengthTuple.length);
                    return default(T);
            }
            return default(T);
        }

        private byte[] Undeltify(byte[] baseBytes, byte[] deltaBytes, long targetLength)
        {
            byte[] targetBuffer = new byte[targetLength];

            return targetBuffer;
        }

        private byte[] ReadZlibBytes(FileStream fileStream, ulong length)
        {
            byte[] result = new byte[length];
            using (DeflateStream deflateStream = GetDeflateStreamForZlibData(fileStream))
            {
                deflateStream.Read(result, 0, (int)length);
            }
            return result;
        }

        private long ReadVariableLengthOffset(Stream stream)
        {
            byte[] buffer = new byte[1];
            stream.Read(buffer, 0, 1);
            long result = buffer[0] & 0b0111_1111; //First bit is dropped as it is the readNextByte indicator
            while ((buffer[0] & 0b1000_0000) != 0)
            {
                stream.Read(buffer, 0, 1);
                result = result + 1;
                result = result << 7;
                result = result + (byte)(buffer[0] & 0b0111_1111); //First bit is dropped as it is the readNextByte indicator
            }
            return result;
        }

        private (int bytesRead, long length) ReadVariableLengthSize(Stream stream)
        {
            byte[] buffer = new byte[1];
            stream.Read(buffer, 0, 1);
            long result = buffer[0] & 0b0111_1111; //First bit is dropped as it is the readNextByte indicator
            int counter = 0;
            while ((buffer[0] & 0b1000_0000) != 0)
            {
                counter++;
                stream.Read(buffer, 0, 1);
                result = result + ((buffer[0] & 0b0111_1111) << (7 * counter)); //First bit is dropped as it is the readNextByte indicator
            }
            counter++;
            return (counter, result);
        }

        internal Commit ReadCommitFromStream(FileStream fileStream, string hash)
        {
            fileStream.Seek(2, SeekOrigin.Current);
            using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress))
            using (StreamReader reader = new StreamReader(deflateStream, Encoding.UTF8))
            {
                return ReadCommitCore(reader, hash);
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
                if(indexForHash == -1)
                {
                    return -1;
                }
                int lastFanoutPos = 4 + 4 + (255 * 4);
                int totalNumberOfHashes = ReadInt32(fileStream, lastFanoutPos);
                int indexInto4ByteOffsets = lastFanoutPos + 4 + (20 * totalNumberOfHashes) + (4 * totalNumberOfHashes) + (4 * indexForHash);
                fileStream.Seek(indexInto4ByteOffsets, SeekOrigin.Begin);
                fileStream.Read(fourByteBuffer, 0, 4);
                bool use8ByteOffsets = (fourByteBuffer[0] & 0b1000_0000) != 0;
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
                return -1;
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

        internal string GetObjectFilePath(string hash)
        {
            string folderName = hash.Substring(0, 2);
            string fileName = hash.Substring(2);
            return Path.Combine(repoPath, "objects", folderName, fileName);
        }

        internal Commit ReadCommit(string hash)
        {
            string filePath = GetObjectFilePath(hash);
            if (File.Exists(filePath))
            {
                return ReadCommitFromFile(filePath, hash);
            }
            else
            {
                return ReadObjectFromPack(hash, (FileStream f, ulong _) => ReadCommitFromStream(f, hash));
            }
        }

        private T ReadObjectFromPack<T>(string hash, Func<FileStream, ulong, T> extractFunc)
        {
            foreach(string indexFile in Directory.EnumerateFiles(Path.Combine(repoPath, "objects", "pack"), "*.idx"))
            {
                string packName = Path.GetFileNameWithoutExtension(indexFile);
                long offset = ReadIndex(packName, hash);
                if(offset != -1)
                {
                    using (FileStream fileStream = File.OpenRead(Path.Combine(repoPath, "objects", "pack", packName + ".pack")))
                    {
                        return ReadPackFile(fileStream, hash, offset, extractFunc);
                    }
                }
            }
            return default(T);
        }

        internal Commit ReadCommitFromFile(string filePath, string hash)
        { 
            using (FileStream fileStream = File.OpenRead(filePath))
            using (DeflateStream deflateStream = GetDeflateStreamForZlibData(fileStream))
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
                return ReadCommitCore(reader, hash);
            }
        }

        private static Commit ReadCommitCore(StreamReader reader, string hash)
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
            return new Commit(hash, tree, parents, author, message);
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
        
        private DeflateStream GetDeflateStreamForZlibData(FileStream fileStream)
        {
            fileStream.Seek(2, SeekOrigin.Current);
            return new DeflateStream(fileStream, CompressionMode.Decompress);
        }

        private enum ComparisonResult
        {
            Less,
            Equal,
            Greater
        }
    }
}