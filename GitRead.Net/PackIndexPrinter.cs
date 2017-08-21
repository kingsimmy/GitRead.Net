using System;
using System.IO;
using System.Linq;

namespace GitRead.Net
{
    public class PackIndexPrinter
    {
        private readonly string repoPath;

        public PackIndexPrinter(string repoPath)
        {
            this.repoPath = repoPath.EndsWith(".git") ? repoPath : Path.Combine(repoPath, ".git");
        }

        public void PrintIndexFile(string name, bool sorted)
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
                uint lastFanoutPos = 4 + 4 + (255 * 4);
                uint totalNumberOfHashes = ReadUInt32(fileStream, lastFanoutPos);
                string[] hashes = new string[totalNumberOfHashes];
                for(uint i = 0; i < totalNumberOfHashes; i++)
                {
                    hashes[i] = ReadHash(fileStream);
                }
                fileStream.Seek(totalNumberOfHashes * 4, SeekOrigin.Current); //Skip past CRC32 values
                uint[] offsets = new uint[totalNumberOfHashes];
                for (uint i = 0; i < totalNumberOfHashes; i++)
                {
                    offsets[i] = ReadUInt32(fileStream);
                }
                var zip = hashes.Zip(offsets, (x, y) => new {Hash = x, Offset = y });
                if (sorted)
                {
                    zip = zip.OrderBy(x => x.Offset);
                }
                zip.ToList().ForEach(x => Console.WriteLine(x.Hash + " " + x.Offset));
            }
        }

        private string ReadHash(FileStream fileStream)
        {
            byte[] buffer = new byte[20];
            fileStream.Read(buffer, 0, 20);
            return String.Concat(buffer.Select(x => x.ToString("X2")));
        }

        private uint ReadUInt32(FileStream fileStream, uint? pos = null)
        {
            byte[] buffer = new byte[4];
            if (pos != null)
            {
                fileStream.Seek(pos.Value, SeekOrigin.Begin);
            }
            fileStream.Read(buffer, 0, 4);
            Array.Reverse(buffer);
            return BitConverter.ToUInt32(buffer, 0);
        }
    }
}
