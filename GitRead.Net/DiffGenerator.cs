using System;
using System.Collections.Generic;
using System.Linq;

namespace GitRead.Net
{
    internal static class DiffGenerator
    {
        private static readonly int BlankLineHash = "\n".GetHashCode();

        internal static (int added, int deleted) GetLinesChanged(string contentBefore, string contentNow)
        {
            int prefixLength = FindLengthOfCommonPrefix(contentBefore, contentNow);
            int suffixLength = FindLengthOfCommonSuffix(contentBefore, contentNow);
            if(prefixLength + suffixLength >= Math.Max(contentBefore.Length, contentNow.Length) && prefixLength < suffixLength)
            {
                prefixLength = 0;
            }
            else if (prefixLength + suffixLength >= Math.Max(contentBefore.Length, contentNow.Length) && suffixLength < prefixLength)
            {
                suffixLength = 0;
            }
            Dictionary<int, List<string>> allLines1 = Split(contentBefore, prefixLength, suffixLength).GroupBy(x => x.GetHashCode())
                .ToDictionary(x => x.Key, x => x.ToList());
            Dictionary<int, List<string>> allLines2 = Split(contentNow, prefixLength, suffixLength).GroupBy(x => x.GetHashCode())
                .ToDictionary(x => x.Key, x => x.ToList());
            int added = 0;
            int deleted = 0;
            foreach (int hashCode in Enumerable.Concat(allLines1.Keys, allLines2.Keys).Distinct())
            {
                bool existed1 = allLines1.TryGetValue(hashCode, out List<string> lines1);
                bool existed2 = allLines2.TryGetValue(hashCode, out List<string> lines2);
                if (existed1 && existed2 && hashCode == BlankLineHash)
                {
                    int diff = lines1.Count - lines2.Count;
                    if (diff > 0)
                    {
                        deleted += diff;
                    }
                    if(diff < 0)
                    {
                        added += (diff * -1);
                    }
                }
                else if (existed1 && existed2)
                {
                    List<string> lines2Copy = new List<string>(lines2);
                    foreach (string line in lines1)
                    {
                        if (lines2Copy.Contains(line))
                        {
                            lines2Copy.Remove(line);
                        }
                        else
                        {
                            deleted++;
                        }
                    }
                    foreach (string line in lines2)
                    {
                        if (lines1.Contains(line))
                        {
                            lines1.Remove(line);
                        }
                        else
                        {
                            added++;
                        }
                    }
                }
                else if (existed1)
                {
                    deleted += lines1.Count;
                }
                else if (existed2)
                {
                    added += lines2.Count;
                }
            }
            return (added, deleted);
        }

        internal static (int added, int deleted) GetLinesChanged(string contentBefore1, string contentBefore2, string contentNow)
        {
            Dictionary<int, List<string>> allLinesBefore1 = contentBefore1.Split('\n').GroupBy(x => x.GetHashCode())
                .ToDictionary(x => x.Key, x => x.ToList());
            Dictionary<int, List<string>> allLinesBefore2 = contentBefore2.Split('\n').GroupBy(x => x.GetHashCode())
                .ToDictionary(x => x.Key, x => x.ToList());
            Dictionary<int, List<string>> allLinesNow = contentNow.Split('\n').GroupBy(x => x.GetHashCode())
                .ToDictionary(x => x.Key, x => x.ToList());
            int added = 0;
            int deleted = 0;
            foreach (KeyValuePair<int, List<string>> before in allLinesBefore1)
            {
                if (allLinesNow.TryGetValue(before.Key, out List<string> linesNow))
                {
                    foreach (string line in before.Value)
                    {
                        if (!linesNow.Contains(line))
                        {
                            deleted++;
                        }
                    }
                }
                else
                {
                    deleted += before.Value.Count;
                }
            }
            foreach (KeyValuePair<int, List<string>> before in allLinesBefore2)
            {
                if (allLinesNow.TryGetValue(before.Key, out List<string> linesNow))
                {
                    foreach (string line in before.Value)
                    {
                        if (!linesNow.Contains(line))
                        {
                            deleted++;
                        }
                    }
                }
                else
                {
                    deleted += before.Value.Count;
                }
            }
            foreach (KeyValuePair<int, List<string>> now in allLinesNow)
            {
                List<string> linesBefore1 = allLinesBefore1.ContainsKey(now.Key) ? allLinesBefore1[now.Key] : new List<string>();
                List<string> linesBefore2 = allLinesBefore2.ContainsKey(now.Key) ? allLinesBefore2[now.Key] : new List<string>();
                foreach (string line in now.Value)
                {
                    if (linesBefore1.Contains(line))
                    {
                        linesBefore1.Remove(line);
                        continue;
                    }
                    else if (linesBefore2.Contains(line))
                    {
                        linesBefore2.Remove(line);
                        continue;
                    }
                    else
                    {
                        added++;
                    }
                }
            }
            return (added, deleted);
        }

        private static int FindLengthOfCommonPrefix(string input1, string input2)
        {
            int lastNewlineIndex = -1;
            int i = 0;
            while(i < input1.Length && i < input2.Length)
            {
                if(input1[i] != input2[i])
                {
                    return lastNewlineIndex + 1;
                }
                if(input1[i] == '\n')
                {
                    lastNewlineIndex = i;
                }
                i++;
            }
            return i + 1;
        }

        private static int FindLengthOfCommonSuffix(string input1, string input2)
        {
            int suffixLength = 1;
            int i = 0;
            while (i < input1.Length - 1 && i < input2.Length - 1)
            {
                i++;
                if (input1[input1.Length - i] != input2[input2.Length - i])
                {
                    return suffixLength - 1;
                }
                if (input1[input1.Length - i] == '\n')
                {
                    suffixLength = i;
                }
            }
            return i - 1;
        }

        private static IEnumerable<string> Split(string input, int start, int suffixLength)
        {
            for (int i = start; i < input.Length - suffixLength; i++)
            {
                if(input[i] == '\n')
                {
                    yield return input.Substring(start, i - start + 1);
                    start = i + 1;
                }
            }
            if(start < input.Length - suffixLength)
            {
                yield return input.Substring(start, input.Length - suffixLength - start);
            }
        }
    }
}