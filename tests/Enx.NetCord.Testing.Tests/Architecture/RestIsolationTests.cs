using System.Reflection;
using Enx.NetCord.Testing.State;
using Xunit;

namespace Enx.NetCord.Testing.Tests.Architecture;

/// <summary>
/// ADR 0004, amendment 04B: <c>Rest/</c> resolves guilds and channels through <c>GuildAccess</c> as
/// <see cref="StateCaller.Bot"/>; <see cref="StateCaller.Fixture"/> and the state reads that skip the access check are
/// reserved to the seed and the test commands. Cross-cutting test: it covers no row of <c>docs/parity</c>.
/// </summary>
public sealed class RestIsolationTests
{
    /// <summary>
    /// State members with no <see cref="StateCaller"/> parameter that <c>Rest/</c> may still call: none takes a guild ID or
    /// a channel ID, or the channel is a direct message, which has no guild and is never access-controlled. Adding a name
    /// here is a decision to review, not a way to silence the test.
    /// </summary>
    /// <remarks>
    /// The token-scoped operations of M2 (interaction callback, followups, webhooks) are expected to be added here
    /// explicitly: by design they take no <see cref="StateCaller"/> because the token resolves their scope and they do
    /// not check the bot's membership (ADR 0004, amendment 04B). The rule covers only the members of
    /// <see cref="FakeDiscordState"/> (a partial class: every state operation must remain a member of that type, whatever
    /// the file). State exposed through another type is not seen by this test. The scan is by member name, so an
    /// indexer (<c>state[id]</c>, never spelled <c>.Item</c>) would escape it: <see cref="FakeDiscordState"/> must
    /// have none, which <c>FakeDiscordState_Indexers_DoNotExist</c> asserts.
    /// </remarks>
    private static readonly string[] AllowedUnguardedMembers =
    [
        nameof(FakeDiscordState.FindUser),
        nameof(FakeDiscordState.FindApplication),
        nameof(FakeDiscordState.FindBotIdentity),
        nameof(FakeDiscordState.UpdateUser),
        nameof(FakeDiscordState.GetOrCreateDmChannel),
        nameof(FakeDiscordState.Clock),
    ];

    private const BindingFlags InstanceMembers =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Every instance member of <see cref="FakeDiscordState"/> (method, property, indexer or field, public or internal)
    /// that does not take a <see cref="StateCaller"/> cannot apply the visibility table, so it is forbidden to
    /// <c>Rest/</c> unless it is in <see cref="AllowedUnguardedMembers"/>. A new state member is therefore forbidden by
    /// default, which is the safe side.
    /// </summary>
    private static HashSet<string> ForbiddenMembers()
    {
        var type = typeof(FakeDiscordState);
        var methods = type.GetMethods(InstanceMembers)
            .Where(method => !method.IsSpecialName && IsVisible(method.IsPublic, method.IsAssembly, method.IsFamilyOrAssembly))
            .Where(method => method.GetParameters().All(parameter => parameter.ParameterType != typeof(StateCaller)))
            .Select(method => method.Name);
        var properties = type.GetProperties(InstanceMembers)
            .Where(property => property.GetMethod is { } getter && IsVisible(getter.IsPublic, getter.IsAssembly, getter.IsFamilyOrAssembly)
                || property.SetMethod is { } setter && IsVisible(setter.IsPublic, setter.IsAssembly, setter.IsFamilyOrAssembly))
            .Where(property => property.GetIndexParameters().All(parameter => parameter.ParameterType != typeof(StateCaller)))
            .Select(property => property.Name);
        var fields = type.GetFields(InstanceMembers)
            .Where(field => IsVisible(field.IsPublic, field.IsAssembly, field.IsFamilyOrAssembly))
            .Select(field => field.Name);

        return methods.Concat(properties).Concat(fields).Except(AllowedUnguardedMembers).ToHashSet(StringComparer.Ordinal);
    }

    private static bool IsVisible(bool isPublic, bool isAssembly, bool isFamilyOrAssembly) =>
        isPublic || isAssembly || isFamilyOrAssembly;

    private static IReadOnlySet<string> Forbidden { get; } = ForbiddenMembers();

    private static IReadOnlyList<RestIsolationViolation> ScanSample(string source) =>
        RestIsolationScanner.Scan("Sample.cs", source, Forbidden);

    [Fact]
    public void RestSources_ScannedAgainstTheStateApi_NeverBypassGuildAccess()
    {
        var root = RestIsolationScanner.FindRepositoryRoot();
        var rest = Path.Combine(root, "src", "Enx.NetCord.Testing", "Rest");

        var (fileCount, violations) = RestIsolationScanner.ScanDirectory(root, rest, Forbidden);

        Assert.True(fileCount > 0, $"No source file found under '{rest}': the scan would prove nothing.");
        Assert.True(
            violations.Count == 0,
            "Rest/ must resolve guilds and channels through GuildAccess as StateCaller.Bot (ADR 0004, amendment 04B):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void ForbiddenMembers_ComputedFromTheStateApi_CoverTheReadsThatSkipTheAccessCheck()
    {
        string[] mustBeForbidden =
        [
            nameof(FakeDiscordState.FindGuild),
            nameof(FakeDiscordState.FindChannel),
            nameof(FakeDiscordState.FindDmChannel),
            nameof(FakeDiscordState.FindMember),
            nameof(FakeDiscordState.FindMessage),
            nameof(FakeDiscordState.FindRole),
            nameof(FakeDiscordState.CreateGuild),
            nameof(FakeDiscordState.TakeProjection),
        ];

        Assert.All(mustBeForbidden, name => Assert.Contains(name, Forbidden));
    }

    [Fact]
    public void ForbiddenMembers_AfterTheAllowlist_NoLongerContainGuardedMembers()
    {
        Assert.DoesNotContain(nameof(FakeDiscordState.GetGuild), Forbidden);
        Assert.DoesNotContain(nameof(FakeDiscordState.CreateMessage), Forbidden);
        Assert.All(
            AllowedUnguardedMembers,
            name => Assert.True(
                typeof(FakeDiscordState).GetMember(name, InstanceMembers).Length > 0,
                $"'{name}' is in the allowlist but is no longer a FakeDiscordState member: remove it."));
    }

    [Fact]
    public void FakeDiscordState_Indexers_DoNotExist()
    {
        var indexers = typeof(FakeDiscordState).GetProperties(InstanceMembers)
            .Where(property => property.GetIndexParameters().Length > 0)
            .Where(property => property.GetMethod is { IsPrivate: false } || property.SetMethod is { IsPrivate: false })
            .Select(property => property.Name)
            .ToList();

        Assert.True(
            indexers.Count == 0,
            "An indexer would escape the scanner (state[id] never spells the member name): pass through a method taking a StateCaller instead. Found: "
            + string.Join(", ", indexers));
    }

    [Fact]
    public void ForbiddenMembers_NonMethodMembersOfTheState_AreForbiddenExceptClock()
    {
        var nonMethods = typeof(FakeDiscordState).GetMembers(InstanceMembers)
            .Where(member => member is PropertyInfo or FieldInfo)
            .Where(member => member is not FieldInfo { IsPrivate: true } and not FieldInfo { IsFamily: true })
            .Where(member => member is not PropertyInfo { GetMethod.IsPrivate: true, SetMethod: null or { IsPrivate: true } })
            .Select(member => member.Name)
            .ToList();

        Assert.Contains(nameof(FakeDiscordState.Clock), nonMethods);
        Assert.All(nonMethods.Except([nameof(FakeDiscordState.Clock)]), name => Assert.Contains(name, Forbidden));
        Assert.DoesNotContain(nameof(FakeDiscordState.Clock), Forbidden);
    }

    [Theory]
    [InlineData("var c = StateCaller.Fixture;", "StateCaller.Fixture")]
    [InlineData("var c = Enx.NetCord.Testing.State.StateCaller .Fixture;", "StateCaller.Fixture")]
    [InlineData("var c = StateCaller\n    .Fixture;", "StateCaller.Fixture")]
    [InlineData("var c = StateCaller.Other;", "StateCaller.Other")]
    [InlineData("var c = $\"{StateCaller.Fixture}\";", "StateCaller.Fixture")]
    [InlineData("using static Enx.NetCord.Testing.State.StateCaller;", "using static Enx.NetCord.Testing.State.StateCaller;")]
    [InlineData("using Who = Enx.NetCord.Testing.State.StateCaller;", "using Who = Enx.NetCord.Testing.State.StateCaller;")]
    [InlineData("var c = (StateCaller)1;", "(StateCaller)")]
    [InlineData("var c = (Enx.NetCord.Testing.State.StateCaller)1;", "(Enx.NetCord.Testing.State.StateCaller)")]
    [InlineData("var c = (global::Enx.NetCord.Testing.State.StateCaller)1;", "(global::Enx.NetCord.Testing.State.StateCaller)")]
    [InlineData("var c = (StateCaller?)1;", "(StateCaller?)")]
    [InlineData("var c = default(StateCaller);", "(StateCaller)")]
    [InlineData("using static global::Enx.NetCord.Testing.State.StateCaller;", "using static global::Enx.NetCord.Testing.State.StateCaller;")]
    [InlineData("var g = state.FindGuild(id);", ".FindGuild")]
    [InlineData("var g = state?.FindChannel(id);", ".FindChannel")]
    [InlineData("var g = state\n    .FindMember(a, b);", ".FindMember")]
    [InlineData("var m = request.State.FindMessage(1, 2);", ".FindMessage")]
    [InlineData("var f = state.FindRole;", ".FindRole")]
    [InlineData("var s = state.TakeProjection();", ".TakeProjection")]
    [InlineData("var s = $\"{state.FindGuild(id)}\";", ".FindGuild")]
    [InlineData("var s = $$\"\"\"{{state.FindGuild(id)}}\"\"\";", ".FindGuild")]
    public void Scan_SourceThatBreaksTheRule_ReportsTheViolation(string source, string expectedText)
    {
        var violations = ScanSample($"class Sample {{ void M() {{ {source} }} }}");

        var violation = Assert.Single(violations);
        Assert.Equal(expectedText, violation.Text);
        Assert.Equal("Sample.cs", violation.File);
        Assert.Equal("Sample", violation.Type);
        Assert.True(violation.Line >= 1);
    }

    [Fact]
    public void Scan_ViolationAfterSeveralLines_ReportsTheLineAndTheEnclosingType()
    {
        const string source = """
            namespace N;

            internal sealed class First
            {
            }

            internal sealed class Second
            {
                void M(FakeDiscordState state)
                {
                    var ok = state.FindUser(1);
                    var bad = state.FindMessage(1, 2);
                }
            }
            """;

        var violation = Assert.Single(ScanSample(source));

        Assert.Equal(new RestIsolationViolation("Sample.cs", 12, "Second", "calls a FakeDiscordState member that bypasses GuildAccess", ".FindMessage"), violation);
        Assert.Equal("Sample.cs:12 [Second] calls a FakeDiscordState member that bypasses GuildAccess: .FindMessage", violation.ToString());
    }

    [Theory]
    [InlineData("record struct")]
    [InlineData("record class")]
    [InlineData("readonly record struct")]
    [InlineData("record")]
    [InlineData("struct")]
    public void Scan_ViolationInsideAnyTypeKind_ReportsTheEnclosingType(string kind)
    {
        var violation = Assert.Single(ScanSample($"internal {kind} Holder\n{{\n    void M() => _ = StateCaller.Fixture;\n}}\n"));

        Assert.Equal("Holder", violation.Type);
    }

    [Theory]
    [InlineData("var t = typeof(StateCaller);")]
    [InlineData("var n = nameof(StateCaller);")]
    [InlineData("void M(StateCaller caller) { }")]
    public void Scan_TypeofNameofAndParameterDeclarations_ReportNothing(string source) =>
        Assert.Empty(ScanSample($"class Sample {{ {source} }}"));

    [Fact]
    public void Scan_StateCallerBotAndAllowedMembers_ReportsNothing()
    {
        const string source = """
            using Enx.NetCord.Testing.State;
            internal sealed class Handler
            {
                void M(FakeDiscordState state)
                {
                    var guild = state.GetGuild(StateCaller.Bot, 1);
                    var user = state.FindUser(2);
                    var dm = state.GetOrCreateDmChannel(new CreateDmChannelInput(3));
                    var created = state.CreateMessage(StateCaller.Bot, input);
                }
            }
            """;

        Assert.Empty(ScanSample(source));
    }

    [Fact]
    public void Scan_MentionsInCommentsAndStrings_AreIgnored()
    {
        const string source = """"
            internal sealed class Handler
            {
                // state.FindGuild(1) and StateCaller.Fixture are reserved to the seed.
                /* state.FindChannel(2) */
                /// <summary>Never call <c>state.FindMember</c> with <see cref="StateCaller.Fixture"/>.</summary>
                void M()
                {
                    var a = "state.FindGuild(1) StateCaller.Fixture";
                    var b = @"state.FindMessage(""x"") StateCaller.Fixture";
                    var c = $"{{state.FindRole}} StateCaller.Fixture {a}";
                    var d = """
                        state.FindGuild(StateCaller.Fixture)
                        """;
                    var e = '"'; var f = "\"" + "state.FindGuild(1)";
                }
            }
            """";

        Assert.Empty(ScanSample(source));
    }

    [Fact]
    public void Sanitize_Source_KeepsLengthAndLineBreaks()
    {
        const string source = "a // c\r\n/* x\n y */ \"s\\\"\" 'q' @\"v\"\"\" b";

        var sanitized = RestIsolationScanner.Sanitize(source);

        Assert.Equal(source.Length, sanitized.Length);
        Assert.Equal(source.Count(c => c == '\n'), sanitized.Count(c => c == '\n'));
        Assert.Equal(["a", "@", "b"], sanitized.Split((char[])[' ', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void ScanDirectory_DirectoryWithAViolatingFile_ReportsItWithItsRelativePath()
    {
        var root = Path.Combine(Path.GetTempPath(), "enx-rest-isolation-" + Guid.NewGuid().ToString("N"));
        var rest = Path.Combine(root, "Rest", "Routes");
        Directory.CreateDirectory(rest);
        Directory.CreateDirectory(Path.Combine(root, "Rest", "obj"));
        try
        {
            File.WriteAllText(Path.Combine(rest, "Bad.cs"), "class Bad\n{\n    void M() => _ = StateCaller.Fixture;\n}\n");
            File.WriteAllText(Path.Combine(rest, "Good.cs"), "class Good { void M() => _ = StateCaller.Bot; }");
            File.WriteAllText(Path.Combine(root, "Rest", "obj", "Generated.cs"), "class G { void M() => _ = StateCaller.Fixture; }");

            var (fileCount, violations) = RestIsolationScanner.ScanDirectory(root, Path.Combine(root, "Rest"), Forbidden);

            Assert.Equal(2, fileCount);
            var violation = Assert.Single(violations);
            Assert.Equal("Rest/Routes/Bad.cs", violation.File);
            Assert.Equal(3, violation.Line);
            Assert.Equal("Bad", violation.Type);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ScanDirectory_RootUnderAnObjOrBinFolder_StillScansTheDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "enx-rest-isolation-" + Guid.NewGuid().ToString("N"), "bin", "checkout");
        var rest = Path.Combine(root, "Rest");
        Directory.CreateDirectory(rest);
        try
        {
            File.WriteAllText(Path.Combine(rest, "Bad.cs"), "class Bad { void M() => _ = StateCaller.Fixture; }");

            var (fileCount, violations) = RestIsolationScanner.ScanDirectory(root, rest, Forbidden);

            Assert.Equal(1, fileCount);
            Assert.Single(violations);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(root))!, recursive: true);
        }
    }

    [Fact]
    public void FindRepositoryRoot_FromTheTestOutput_FindsTheSourceTree()
    {
        var root = RestIsolationScanner.FindRepositoryRoot();

        Assert.True(Directory.Exists(Path.Combine(root, "src", "Enx.NetCord.Testing", "Rest")));
    }
}
