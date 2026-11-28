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
[JsonSerializable(typeof(MessageResponse))]
[JsonSerializable(typeof(List<MessageResponse>))]
internal sealed partial class FakeDiscordJsonContext : JsonSerializerContext;
