``` ini

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.26200.8328)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
|              Method |         Mean |        Error |      StdDev |   Gen0 |  Allocated |
|-------------------- |-------------:|-------------:|------------:|-------:|-----------:|
| ExecuteSimpleSelect |     808.8 μs |     414.3 μs |    22.71 μs | 9.7656 |  207.34 KB |
| ExecuteJoinedSelect | 240,427.0 μs | 129,592.8 μs | 7,103.42 μs |      - | 2634.52 KB |
