``` ini

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.26200.8328)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
|              Method |       Mean |      Error |    StdDev |     Gen0 |    Gen1 |  Allocated |
|-------------------- |-----------:|-----------:|----------:|---------:|--------:|-----------:|
| ExecuteSimpleSelect |   815.0 μs |   446.2 μs |  24.46 μs |   9.7656 |       - |  207.34 KB |
| ExecuteJoinedSelect | 7,461.6 μs | 2,029.4 μs | 111.24 μs | 343.7500 | 15.6250 | 7113.93 KB |
