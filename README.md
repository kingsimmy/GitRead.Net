# GitRead.Net
[![NuGet](https://img.shields.io/badge/nuget-v1.0.0-green.svg)](https://www.nuget.org/packages/GitRead.Net/1.0.0)

.Net Standard library for reading Git repository data

This is a C# library which can be used to explore the commits and files which exist in a local Git repository. This library works with .NET Framework 4.6.1 or later and .NET Core 2.0 or later

Example
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
