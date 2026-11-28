using Xunit;
using Xunit.Sdk;
using Xunit.v3;

[assembly: TestCollectionOrderer(typeof(Enx.NetCord.Testing.Conformance.Contract.ExceptionsLastCollectionOrderer))]

namespace Enx.NetCord.Testing.Conformance.Contract;

/// <summary>
/// Runs <see cref="ContractExceptionsCollection"/> after every other collection, whether the run is parallel (where a collection that
/// disables parallelization already runs last) or sequential, so every scenario has recorded its use of the exceptions first.
/// </summary>
public sealed class ExceptionsLastCollectionOrderer : ITestCollectionOrderer
{
    /// <inheritdoc/>
    public IReadOnlyCollection<TTestCollection> OrderTestCollections<TTestCollection>(IReadOnlyCollection<TTestCollection> testCollections)
        where TTestCollection : ITestCollection
        => [.. testCollections.OrderBy(static c => c.TestCollectionDisplayName == ContractExceptionsCollection.Name ? 1 : 0)];
}
