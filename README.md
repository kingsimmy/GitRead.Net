# GitRead.Net
[![NuGet](https://img.shields.io/badge/nuget-v1.3.0-green.svg)](https://www.nuget.org/packages/GitRead.Net/1.3.0)

C# library for reading Git repository data

This is a C# library which can be used to explore the commits and files which exist in a local Git repository. This library targets .Net Standard so works with .NET Framework 4.6.1 or later and .NET Core 2.0 or later.

| Windows                 | Linux            | Mac             |
| ----------------------- |:----------------:| ---------------:|
| >= .NET Framework 4.6.1 | >= .NET Core 2.0 | >= .NET Core 2.0|
| >= .NET Core 2.0        |                  |                 |

The library is available via Nuget at https://www.nuget.org/packages/GitRead.Net

Simple Example
--------------------
```
> RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(@"C:\src\Newtonsoft.Json\.git");
> Commit commit = repositoryAnalyzer.GetCommits().Last();
> Console.WriteLine(commit.Hash);
e65bc2becd3ded759b5d5ca42c7d28a631cfc06d
> Console.WriteLine(commit.Author);
JamesNK
> Console.WriteLine(commit.Timestamp);
08/09/2007 08:59:38
```

Getting line count of all files as they existed at a specific commit
--------------------
```
> RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(@"C:\src\csharplang");
> foreach(FileLineCount lineCount in repositoryAnalyzer.GetFileLineCounts("7981ea1fb4d89571fd41e3b75f7c9f7fc178e837"))
. {
.     Console.WriteLine(lineCount);
. }
README.md 10
design-notes\Notes-2016-11-16.md 74
design-notes\csharp-language-design-notes-2017.md 0
proposals\async-streams.md 0
proposals\nullable-reference-types.md 126
spec\spec.md 0
```

Getting the details of files added, modified or deleted by a specific commit
--------------------
```
> RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(@"C:\src\csharplang\.git");
> CommitDelta changes = repositoryAnalyzer.GetChanges("ad9e5bca2e2b9240d21ee4cc3aa184610968e9fb");
> Console.WriteLine(changes.Added.Count);
2
> Console.WriteLine(changes.Modified.Count);
4
> Console.WriteLine(changes.Deleted.Count);
0
> foreach (string added in changes.Added.Select(x => x.Path))
. {
.   Console.WriteLine(added);
. }
meetings\2017\LDM-2017-06-13.md
meetings\2017\LDM-2017-06-14.md
> foreach (FileChange change in changes.Modified)
. {
.     Console.WriteLine($"{change.Path} ({change.NumberOfLinesAdded} lines added) ({change.NumberOfLinesDeleted} lines deleted)");
. }
meetings\2017\LDM-2017-05-17.md (32 lines added) (28 lines deleted)
meetings\2017\LDM-2017-05-26.md (18 lines added) (31 lines deleted)
meetings\2017\LDM-2017-05-31.md (55 lines added) (36 lines deleted)
meetings\2017\README.md (42 lines added) (0 lines deleted)
```

Getting the history of commits for a specific file 
--------------------
```
> RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(@"C:\src\csharplang");
> string filePath = @"spec\variables.md";
> IReadOnlyList<Commit> commits = repositoryAnalyzer.GetCommitsForOneFilePath(filePath);
> Console.WriteLine(commits.Count);
3
> foreach (Commit commit in commits)
. {
.     Console.WriteLine($"On {commit.Timestamp:yyyy-MM-dd} {filePath} was modified by {commit.Author}'s commit with hash {commit.Hash}.");
. }
On 2018-01-25 spec\variables.md was modified by stakx's commit with hash 7f39331672cf8edbda8867de004138e0f711c877.
On 2017-10-13 spec\variables.md was modified by Maira Wenzel's commit with hash 868b881ca5c1f158a91d81f3f73fdb1b729c97bd.
On 2017-02-01 spec\variables.md was modified by Neal Gafter's commit with hash 6027ad5a4ab013f4fb42f5edd2d667d649fe1bd8.
```
