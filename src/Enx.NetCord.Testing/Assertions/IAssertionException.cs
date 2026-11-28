namespace Enx.NetCord.Testing;

/// <summary>
/// Marks <see cref="FakeDiscordAssertionException"/> as an assertion failure for xUnit v3, which recognizes
/// any interface named <c>IAssertionException</c> by its simple name (ADR 0012). The name must not change.
/// </summary>
internal interface IAssertionException
{
}
