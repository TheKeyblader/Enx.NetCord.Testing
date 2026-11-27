using System.Text.RegularExpressions;
using Enx.NetCord.Testing.Rest;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Rest;

public sealed partial class SourcesPinTests
{
    [GeneratedRegex(@"`discord-api-spec`[^\n]*?`(?<sha>[0-9a-f]{40})`", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SpecRow();

    [Fact]
    public void SpecificationCommit_EmbeddedInventory_MatchesTheShaPinnedInSourcesMd()
    {
        var sources = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "parity", "sources.md"));

        var match = SpecRow().Match(sources);

        Assert.True(match.Success, "docs/parity/sources.md has no pinned discord-api-spec SHA.");
        Assert.Equal(RouteInventory.SpecificationCommit, match.Groups["sha"].Value);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.slnx").Any())
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("No .slnx found above the test output directory.");
    }
}
