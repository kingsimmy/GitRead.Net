using System.Text;
using System.IO;
using System.IO.Compression;
using GitRead.Net.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GitRead.Net
{
    public class RepositoryReader
    {
        private const int whiteSpace = ' ';
        private const int nullChar = '\0';
        private readonly byte[] oneByteBuffer = new byte[1];
        private readonly string repoPath;
        private readonly PackIndexReader indexReader;

        public RepositoryReader(string repoPath)
        {
            this.repoPath = repoPath.EndsWith(".git") ? repoPath : Path.Combine(repoPath, ".git");
            indexReader = new PackIndexReader(repoPath);
        }

        public string ReadHead()
        {
            string[] lines = File.ReadAllLines(Path.Combine(repoPath, "HEAD"));            
            return lines[0].Split('/').Last();
        }

        public string ReadBranch(string branchName)
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

        internal string ReadBlob(string hash)
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
                if(gitFileType.ToString() != "blob")
                {
                    throw new Exception($"Object with hash {hash} is not a blob. It is a {gitFileType.ToString()}.");
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
            string filePath = GetObjectFilePath(hash);
            if (File.Exists(filePath))
            {
                return ReadTreeFromFile(filePath, hash);
            }
            else
            {
                return ReadObjectFromPack(hash, (Stream s, long length, bool useZlib) => ReadTreeFromStream(s, length, useZlib));
            }
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
                return ReadObjectFromPack(hash, (Stream s, long _, bool useZlib) => ReadCommitFromStream(s, hash, useZlib));
            }
        }

        private T ReadPackFile<T>(FileStream fileStream, string hash, long offset, Func<Stream, long, bool, T> extractFunc)
        {
            byte[] lengthBuffer = new byte[1];
            fileStream.Seek(offset, SeekOrigin.Begin);
            fileStream.Read(lengthBuffer, 0, 1);            
            PackFileObjectType packFileObjectType = (PackFileObjectType)((lengthBuffer[0] & 0b0111_0000) >> 4);
            long length = lengthBuffer[0] & 0b0000_1111; //First four bits are dropped as they are they are readNextByte indicator and packFileObjectType
            int counter = 0;
            while ((lengthBuffer[0] & 0b1000_0000) != 0)
            {
                counter++;
                fileStream.Read(lengthBuffer, 0, 1);
                length = length + ((lengthBuffer[0] & 0b0111_1111) << (4 + (7 * (counter - 1)))); //First bit is dropped as it is the readNextByte indicator
            }
            switch (packFileObjectType)
            {
                case PackFileObjectType.Commit:
                    return extractFunc(fileStream, length, true);
                case PackFileObjectType.Blob:
                    return extractFunc(fileStream, length, true);
                case PackFileObjectType.Tree:
                    return extractFunc(fileStream, length, true);
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
                    byte[] baseBytes = ReadPackFile(fileStream, null, offset - baseObjOffset, (Stream f, long l, bool _) => ReadZlibBytes(f, l));
                    if (baseBytes.Length != sourceDataTuple.length)
                    {
                        throw new Exception("Base object did not match expected length");
                    }
                    byte[] undeltifiedData = Undeltify(baseBytes, deltaBytes, targetLengthTuple.length);
                    return extractFunc(new MemoryStream(undeltifiedData), undeltifiedData.Length, false);
            }
            return default(T);
        }

        private byte[] Undeltify(byte[] baseBytes, byte[] deltaBytes, long targetLength)
        {
            byte[] targetBuffer = new byte[targetLength];
            int deltaIndex = 0;
            int targetIndex = 0;
            while (deltaIndex < deltaBytes.Length)
            {
                byte deltaByte = deltaBytes[deltaIndex];
                deltaIndex++;
                if ((deltaByte & 0b1000_0000) != 0) //copy
                {
                    int offset = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        if ((deltaByte & (1 << i)) != 0)
                        {
                            offset = offset + (deltaBytes[deltaIndex] << (8 * i));
                            deltaIndex++;
                        }
                    }
                    int bytesToCopy = 0;
                    for (int i = 4; i < 7; i++)
                    {
                        if ((deltaByte & (1 << i)) != 0)
                        {
                            bytesToCopy = bytesToCopy + (deltaBytes[deltaIndex] << (8 * i));
                            deltaIndex++;
                        }
                    }
                    for (int i = 0; i < bytesToCopy; i++)
                    {
                        targetBuffer[targetIndex] = baseBytes[offset + i];
                        targetIndex++;
                    }
                }
                else //insert
                {
                    int bytesToInsert = deltaByte;
                    for (int i = 0; i < bytesToInsert; i++)
                    {
                        targetBuffer[targetIndex] = deltaBytes[deltaIndex];
                        targetIndex++;
                        deltaIndex++;
                    }
                }
            }
            return targetBuffer;
        }

        private byte[] ReadZlibBytes(Stream stream, long length)
        {
            byte[] result = new byte[length];
            using (DeflateStream deflateStream = GetDeflateStreamForZlibData(stream))
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

        private T ReadObjectFromPack<T>(string hash, Func<Stream, long, bool, T> extractFunc)
        {
            foreach(string indexFile in Directory.EnumerateFiles(Path.Combine(repoPath, "objects", "pack"), "*.idx"))
            {
                string packName = Path.GetFileNameWithoutExtension(indexFile);
                long offset = indexReader.ReadIndex(packName, hash);
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
        
        private IReadOnlyList<TreeEntry> ReadTreeFromFile(string filePath, string hash)
        {
            List<TreeEntry> entries = new List<TreeEntry>();
            using (FileStream fileStream = File.OpenRead(filePath))
            using (DeflateStream deflateStream = GetDeflateStreamForZlibData(fileStream))
            {
                string gitFileType = ReadString(deflateStream, whiteSpace);
                if (gitFileType.ToString() != "tree")
                {
                    throw new Exception($"Object with hash {hash} is not a tree. It is a {gitFileType}.");
                }
                string gitFileSize = ReadString(deflateStream, nullChar);
                int length = int.Parse(gitFileSize);
                return ReadTreeCore(deflateStream, length);
            }
        }

        private IReadOnlyList<TreeEntry> ReadTreeCore(Stream stream, int length)
        {
            List<TreeEntry> entries = new List<TreeEntry>();
            while (length > 0)
            {
                string mode = ReadString(stream, whiteSpace);
                string name = ReadString(stream, nullChar);
                byte[] buffer = new byte[20];
                stream.Read(buffer, 0, 20);
                string itemHash = String.Concat(buffer.Select(x => x.ToString("X2")));
                length -= (mode.Length + 1 + name.Length + 1 + 20);
                entries.Add(new TreeEntry(name, itemHash, mode));
            }
            return entries;
        }

        private Commit ReadCommitFromFile(string filePath, string hash)
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

        private IReadOnlyList<TreeEntry> ReadTreeFromStream(Stream stream, long length, bool useZlib)
        {
            if (useZlib)
            {
                using (DeflateStream deflateStream = GetDeflateStreamForZlibData(stream))
                {
                    return ReadTreeCore(deflateStream, (int)length);
                }
            }
            else
            {
                return ReadTreeCore(stream, (int)length);
            }
        }

        private Commit ReadCommitFromStream(Stream stream, string hash, bool useZlib)
        {
            if (useZlib)
            {
                using (DeflateStream deflateStream = GetDeflateStreamForZlibData(stream))
                using (StreamReader reader = new StreamReader(deflateStream, Encoding.UTF8))
                {
                    return ReadCommitCore(reader, hash);
                }
            }
            else
            {
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return ReadCommitCore(reader, hash);
                }
            }
        }

        private string GetObjectFilePath(string hash)
        {
            string folderName = hash.Substring(0, 2);
            string fileName = hash.Substring(2);
            return Path.Combine(repoPath, "objects", folderName, fileName);
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
        
        private DeflateStream GetDeflateStreamForZlibData(Stream stream)
        {
            stream.Seek(2, SeekOrigin.Current);
            return new DeflateStream(stream, CompressionMode.Decompress);
        }
    }
}