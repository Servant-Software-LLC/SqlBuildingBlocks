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
|            ParseSimpleSelect_Ansi |  4.289 μs |  0.6623 μs | 0.0363 μs | 0.3815 |      - |      - |    7808 B |
|           ParseSimpleSelect_MySql |  4.364 μs |  0.0779 μs | 0.0043 μs | 0.3815 |      - |      - |    7808 B |
|           ParseComplexSelect_Ansi | 57.391 μs |  6.1168 μs | 0.3353 μs | 3.1738 | 0.3662 |      - |   66696 B |
|          ParseComplexSelect_MySql | 62.597 μs | 30.1407 μs | 1.6521 μs | 3.1738 | 0.3662 |      - |   67352 B |
|  ParseDeeplyNestedExpression_Ansi |  3.723 μs |  1.2760 μs | 0.0699 μs | 0.2899 | 0.0153 | 0.0153 |    8656 B |
|                     ParseCte_Ansi | 12.313 μs |  8.7754 μs | 0.4810 μs | 1.0681 | 0.0610 | 0.0153 |         - |
| ParseAndCreate_ComplexSelect_Ansi | 98.673 μs |  8.1834 μs | 0.4486 μs | 4.0283 | 0.2441 |      - |   84563 B |
