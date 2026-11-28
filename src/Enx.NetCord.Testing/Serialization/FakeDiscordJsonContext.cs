using System.Text.Json.Serialization;

namespace Enx.NetCord.Testing.Serialization;

/// <summary>Provides generated metadata for Discord wire payloads emitted by the fake (ADR 0006).</summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(RateLimitedResponse))]
[JsonSerializable(typeof(UserResponse))]
[JsonSerializable(typeof(CurrentUserResponse))]
[JsonSerializable(typeof(PrivateChannelResponse))]
[JsonSerializable(typeof(TextChannelResponse))]
[JsonSerializable(typeof(VoiceChannelResponse))]
[JsonSerializable(typeof(CategoryChannelResponse))]
[JsonSerializable(typeof(ForumChannelResponse))]
[JsonSerializable(typeof(GuildResponse))]
[JsonSerializable(typeof(GuildPreviewResponse))]
[JsonSerializable(typeof(List<GuildChannelUnion>))]
[JsonSerializable(typeof(MessageResponse))]
[JsonSerializable(typeof(List<MessageResponse>))]
[JsonSerializable(typeof(List<UserResponse>))]
[JsonSerializable(typeof(GuildMemberResponse))]
[JsonSerializable(typeof(List<GuildMemberResponse>))]
[JsonSerializable(typeof(PinnedMessagesResponse))]
internal sealed partial class FakeDiscordJsonContext : JsonSerializerContext;
