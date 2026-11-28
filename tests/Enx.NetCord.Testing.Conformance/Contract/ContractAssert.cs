using Xunit;

namespace Enx.NetCord.Testing.Conformance.Contract;

/// <summary>Asserts that what the fake answered to a test conforms to the pinned Discord OpenAPI contract.</summary>
internal static class ContractAssert
{
    /// <summary>
    /// Validates every attempt recorded by the fake against the contract and requires that each operation in
    /// <paramref name="expectedOperations"/> was attempted at least once, so a scenario cannot pass without exercising its route.
    /// </summary>
    /// <param name="discord">The fake whose journal is checked.</param>
    /// <param name="expectedOperations">The <c>METHOD /template</c> keys the scenario must have exercised.</param>
    public static void Conforms(FakeDiscord discord, params string[] expectedOperations)
    {
        ArgumentNullException.ThrowIfNull(discord);

        var requests = discord.Recorded.Requests;
        var attempted = requests.Select(static r => $"{r.Method} {r.RouteTemplate}").ToHashSet(StringComparer.Ordinal);
        foreach (var expected in expectedOperations)
        {
            Assert.True(
                attempted.Contains(expected),
                $"The scenario never reached '{expected}'. Recorded: {string.Join(", ", attempted)}.");
        }

        var violations = requests.SelectMany(OpenApiContract.Current.Validate).ToList();
        Assert.True(violations.Count == 0, "The fake's responses violate the Discord OpenAPI contract:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }
}
