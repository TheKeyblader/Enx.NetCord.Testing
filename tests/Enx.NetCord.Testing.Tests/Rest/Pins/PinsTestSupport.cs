using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Tests.Rest.Pins;

internal static class PinsTestSupport
{
    /// <summary>Seeds a message that is already pinned, as a user other than the bot would have left it (no system message, no change).</summary>
    public static ulong SeedPinned(FakeDiscord discord, ulong channelId, ulong authorId, string content)
        => discord.State.CreateMessage(StateCaller.Fixture, new CreateMessageInput(channelId, authorId, content, Pinned: true)).Value.Id;

    /// <summary>The ID of the guild-less (DM) channel between the bot and a user, opened by the fake.</summary>
    public static ulong OpenDm(FakeDiscord discord, ulong userId)
        => discord.State.GetOrCreateDmChannel(new CreateDmChannelInput(userId)).Value.Channel.Id;
}
