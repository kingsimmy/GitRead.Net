# GitRead.Net
[![NuGet](https://img.shields.io/badge/nuget-v1.2.0-green.svg)](https://www.nuget.org/packages/GitRead.Net/1.2.0)

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
> Commit commit = repositoryAnalyzer.Commits.First();
> Console.WriteLine(commit.Hash);
a8245a5b9e63bdabfb9dcb90a6cff761cac3af58
> Console.WriteLine(commit.Author);
Jon Hanna
> Console.WriteLine(commit.Timestamp);
16/12/2017 19:25:43
```

Getting line count of all files as they existed at a specific commit
--------------------
```
> RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(@"C:\src\csharplang\.git");
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

Getting names of files added, modified or deleted by a specific commit
--------------------
```
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
> FileChange change = changes.Modified[0];
> Console.WriteLine($"{change.Path} ({change.NumberOfLinesAdded} lines added) ({change.NumberOfLinesDeleted} lines deleted)");
meetings\2017\LDM-2017-05-17.md (32 lines added) (28 lines deleted)
```
