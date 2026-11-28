namespace Enx.NetCord.Testing;

/// <summary>The exception thrown when an assertion of the fake fails.</summary>
/// <remarks>
/// Only <c>Should…</c> methods throw it, and only when what was observed differs from what was expected;
/// a misuse of the API throws an argument exception instead. Tests may throw it from their own assertions.
/// </remarks>
public sealed class FakeDiscordAssertionException : Exception, IAssertionException
{
    /// <summary>Initializes a new instance with the failure message.</summary>
    /// <param name="message">The failure message.</param>
    public FakeDiscordAssertionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance with the failure message and the exception that caused the failure.</summary>
    /// <param name="message">The failure message.</param>
    /// <param name="innerException">The exception that caused the failure, if any.</param>
    public FakeDiscordAssertionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
