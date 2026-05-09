``` ini

BenchmarkDotNet=v0.13.4, OS=Windows 11 (10.0.26200.8328)
11th Gen Intel Core i7-1185G7 3.00GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=10.0.201
  [Host]   : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.5 (10.0.526.15411), X64 RyuJIT AVX2

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
|                            Method |       Mean |       Error |     StdDev |   Gen0 |   Gen1 |   Gen2 | Allocated |
|---------------------------------- |-----------:|------------:|-----------:|-------:|-------:|-------:|----------:|
|            ParseSimpleSelect_Ansi |   4.404 μs |   0.9842 μs |  0.0539 μs | 0.2747 | 0.0076 | 0.0076 |         - |
|           ParseSimpleSelect_MySql |   4.343 μs |   3.2337 μs |  0.1773 μs | 0.2136 |      - |      - |    7808 B |
|           ParseComplexSelect_Ansi |  59.083 μs |   8.7234 μs |  0.4782 μs | 3.1128 | 0.3052 |      - |   66688 B |
|          ParseComplexSelect_MySql |  62.011 μs |  21.6666 μs |  1.1876 μs | 3.1738 | 0.3662 |      - |   67344 B |
|  ParseDeeplyNestedExpression_Ansi |   3.706 μs |   1.2512 μs |  0.0686 μs | 0.4044 | 0.0076 | 0.0038 |         - |
|                     ParseCte_Ansi |  13.154 μs |  13.7396 μs |  0.7531 μs | 1.2360 | 0.0458 |      - |   18536 B |
| ParseAndCreate_ComplexSelect_Ansi | 166.765 μs | 331.2801 μs | 18.1586 μs | 3.9063 |      - |      - |   84555 B |
