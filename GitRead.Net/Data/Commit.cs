using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace GitRead.Net.Data
{
    public class Commit
    {
        private static readonly Regex AuthorRegex = new Regex(@"^(.*)<(.*)> (\d*) ([+|-]\d{4})$", RegexOptions.Compiled);

        public Commit(string hash, string tree, List<string> parents, string author, string message)
        {
            Hash = hash;
            Tree = tree;            
            Parents = parents;
            Message = message;
            Match authorMatch = AuthorRegex.Match(author);
            if (!authorMatch.Success)
            {
                throw new Exception($"Author string in commit {hash} could not be parsed");
            }
            Author = authorMatch.Groups[1].Value;
            EmailAddress = authorMatch.Groups[2].Value;            
            Timestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(authorMatch.Groups[3].Value)).UtcDateTime;
            TimeZoneOffset = authorMatch.Groups[4].Value;
        }

        public string Hash { get; }

        public string Tree { get; }

        public IReadOnlyCollection<string> Parents { get; }

        public string Author { get; }

        public string EmailAddress { get; }

        public DateTime Timestamp { get; }

        public string TimeZoneOffset { get; }

        public string Message { get; }

        public override string ToString()
        {
            return $"Commit {Hash}";
        }
    }
}