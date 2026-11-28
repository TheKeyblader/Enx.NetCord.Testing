using System.Collections.Immutable;
using System.Globalization;
using Enx.NetCord.Testing.Serialization;
using Enx.NetCord.Testing.State;

namespace Enx.NetCord.Testing.Rest.Routes.Reactions;

/// <summary>Projects detached reactions into the wire DTOs of a message.</summary>
internal static class ReactionWire
{
    /// <summary>Projects the <c>reactions</c> of a message.</summary>
    /// <param name="reactions">The reactions, in order.</param>
    /// <returns>The DTOs, or <see langword="null"/> when there are none: Discord omits the property then.</returns>
    public static List<MessageReactionResponse>? ToResponses(ImmutableArray<ReactionSnapshot> reactions)
        => reactions.IsDefaultOrEmpty ? null : [.. reactions.Select(ToResponse)];

    private static MessageReactionResponse ToResponse(ReactionSnapshot reaction) => new()
    {
        Emoji = new MessageReactionEmojiResponse
        {
            Id = reaction.Emoji.Id?.ToString(CultureInfo.InvariantCulture),
            Name = reaction.Emoji.Name,
        },
        Count = reaction.NormalCount + reaction.BurstCount,
        CountDetails = new MessageReactionCountDetailsResponse { Burst = reaction.BurstCount, Normal = reaction.NormalCount },
        BurstColors = [],
        MeBurst = reaction.MeBurst,
        Me = reaction.Me,
    };
}
