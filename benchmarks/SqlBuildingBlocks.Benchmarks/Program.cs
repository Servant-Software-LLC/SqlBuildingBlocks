using BenchmarkDotNet.Running;

namespace SqlBuildingBlocks.Benchmarks;

/// <summary>
/// Entry point for the SqlBuildingBlocks benchmark suite. Use BenchmarkSwitcher so the
/// runner can pick a single class via --filter or run everything by default.
///
/// Examples:
///   dotnet run --configuration Release --project benchmarks/SqlBuildingBlocks.Benchmarks -- --filter "*"
///   dotnet run --configuration Release --project benchmarks/SqlBuildingBlocks.Benchmarks -- --filter "*ParseBenchmarks*"
///   dotnet run --configuration Release --project benchmarks/SqlBuildingBlocks.Benchmarks -- --job short --filter "*"
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
