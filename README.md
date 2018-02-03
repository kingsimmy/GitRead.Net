# GitRead.Net
[![NuGet](https://img.shields.io/badge/nuget-v1.1.0-green.svg)](https://www.nuget.org/packages/GitRead.Net/1.1.0)

C# library for reading Git repository data

This is a C# library which can be used to explore the commits and files which exist in a local Git repository. This library targets .Net Standard so works with .NET Framework 4.6.1 or later and .NET Core 2.0 or later.

| Windows                 | Linux            | Mac             |
| ----------------------- |:----------------:| ---------------:|
| >= .NET Framework 4.6.1 | >= .NET Core 2.0 | >= .NET Core 2.0|
| >= .NET Core 2.0        |                  |                 |


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
> RepositoryAnalyzer repositoryAnalyzer = new RepositoryAnalyzer(@"C:\src\csharplang\.git");
> CommitDelta changes = repositoryAnalyzer.GetChanges("7981ea1fb4d89571fd41e3b75f7c9f7fc178e837");
> Console.WriteLine(changes.Added.Count);
2
> Console.WriteLine(changes.Deleted.Count);
0
> Console.WriteLine(changes.Modified.Count);
0
> foreach(string added in changes.Added)
. {
.     Console.WriteLine(added);
. }
design-notes\Notes-2016-11-16.md
proposals\nullable-reference-types.md
```
