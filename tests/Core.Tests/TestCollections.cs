using Xunit;

namespace SqlBuildingBlocks.Core.Tests;

/// <summary>
/// Disables parallel execution for tests that assert on static cache counts.
/// <see cref="QueryProcessing.CompiledPredicateCache"/> and <see cref="Utils.CompiledQueryDispatch"/>
/// are process-static caches. Tests that call ClearForTests() and assert absolute counts race
/// when run in parallel with QueryEngineTests, which populates those caches as a side-effect.
/// Serializing all four classes into this collection eliminates the race without affecting
/// other assemblies (IntegrationTests, Grammars tests run in separate processes).
/// </summary>
[CollectionDefinition("CompiledPredicateCache", DisableParallelization = true)]
public class CompiledPredicateCacheCollection { }
