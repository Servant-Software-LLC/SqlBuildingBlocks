``` ini

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.26200.8328)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
|              Method |       Mean |      Error |     StdDev |   Gen0 |  Allocated |
|-------------------- |-----------:|-----------:|-----------:|-------:|-----------:|
| ExecuteSimpleSelect |   1.941 ms |   1.563 ms |  0.0857 ms | 7.8125 |  207.54 KB |
| ExecuteJoinedSelect | 359.740 ms | 653.607 ms | 35.8264 ms |      - | 2648.44 KB |
