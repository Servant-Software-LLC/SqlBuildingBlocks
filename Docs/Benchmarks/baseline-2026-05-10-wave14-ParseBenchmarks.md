``` ini

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.26200.8328)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
|                            Method |      Mean |      Error |    StdDev |   Gen0 |   Gen1 |   Gen2 | Allocated |
|---------------------------------- |----------:|-----------:|----------:|-------:|-------:|-------:|----------:|
|            ParseSimpleSelect_Ansi |  4.008 μs |  0.8387 μs | 0.0460 μs | 0.2518 | 0.0076 | 0.0076 |         - |
|           ParseSimpleSelect_MySql |  4.476 μs |  1.8754 μs | 0.1028 μs | 0.2441 | 0.0076 | 0.0076 |         - |
|           ParseComplexSelect_Ansi | 57.263 μs | 14.4882 μs | 0.7941 μs | 1.7700 | 0.1221 |      - |   66696 B |
|          ParseComplexSelect_MySql | 61.367 μs | 15.4507 μs | 0.8469 μs | 3.1738 | 0.2441 |      - |   67352 B |
|  ParseDeeplyNestedExpression_Ansi |  3.662 μs |  2.4882 μs | 0.1364 μs | 0.4196 |      - |      - |    8656 B |
|                     ParseCte_Ansi | 12.819 μs |  1.4234 μs | 0.0780 μs | 0.6409 | 0.0305 | 0.0305 |   18536 B |
| ParseAndCreate_ComplexSelect_Ansi | 98.705 μs | 28.9033 μs | 1.5843 μs | 4.0283 | 0.2441 |      - |   84563 B |
