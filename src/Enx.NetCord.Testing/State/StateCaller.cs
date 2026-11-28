namespace Enx.NetCord.Testing.State;

/// <summary>
/// Who an access-controlled state operation acts for (ADR 0004, amendment 04B, § 2). Every operation that receives a
/// guild ID or a channel ID from its caller takes one, with no default, so a handler cannot forget the access check.
/// The zero value is deliberately undefined: a defaulted caller is rejected rather than treated as either.
/// </summary>
internal enum StateCaller
{
    /// <summary>
    /// What a test stages: the seed, and later the Gateway test commands. No access control: the fake's bot need
    /// not be a member of the guild.
    /// </summary>
    Fixture = 1,

    /// <summary>
    /// The bot, as seen through REST. Applies the visibility table of <see cref="GuildAccess"/>. This is the only value
    /// a handler of <c>Rest/</c> passes.
    /// </summary>
    Bot = 2,
}
