using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

/// <summary>
/// Disables parallel execution for <see cref="Utils.CompiledPredicateCacheTests"/>.
/// <see cref="QueryProcessing.CompiledPredicateCache"/> is a process-static cache; tests that
/// assert delta-from-baseline cache counts would race when run in parallel with other tests
/// that populate the same cache. Serializing the collection eliminates the race.
/// </summary>
[CollectionDefinition("CompiledPredicateCache", DisableParallelization = true)]
public class CompiledPredicateCacheCollection { }
