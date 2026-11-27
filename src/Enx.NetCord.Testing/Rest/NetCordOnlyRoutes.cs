namespace Enx.NetCord.Testing.Rest;

/// <summary>
/// Routes that NetCord calls but the pinned Discord specification does not document
/// (see "Routes NetCord hors spec publique" in <c>docs/parity/rest.md</c>).
/// Templates reuse the parameter names of the specification when a documented path has the same shape.
/// </summary>
internal static class NetCordOnlyRoutes
{
    /// <summary>The NetCord-only operations, sourced from NetCord @ <c>bb5f4e13</c> <c>RestClient.*.cs</c>.</summary>
    internal static readonly RouteOperation[] All =
    [
        // RestClient.Undocumented.cs: GetApplicationAsync.
        new("GET", "/applications/{application_id}/rpc", null, RouteSecurity.Bot, InSpecification: false),

        // RestClient.Monetization.Skus.cs: GetSkusAsync.
        new("GET", "/applications/{application_id}/skus", null, RouteSecurity.Bot, InSpecification: false),

        // RestClient.Undocumented.cs: CreateGoogleCloudPlatformStorageBucketsAsync.
        new("POST", "/channels/{channel_id}/attachments", null, RouteSecurity.Bot, InSpecification: false),

        // RestClient.Guild.cs: CreateGuildAsync.
        new("POST", "/guilds", null, RouteSecurity.Bot, InSpecification: false),

        // RestClient.GuildTemplate.cs: CreateGuildFromGuildTemplateAsync (the specification only documents GET here).
        new("POST", "/guilds/templates/{code}", null, RouteSecurity.Bot, InSpecification: false),

        // RestClient.Guild.cs: DeleteGuildAsync (the specification only documents GET and PATCH here).
        new("DELETE", "/guilds/{guild_id}", null, RouteSecurity.Bot, InSpecification: false),

        // RestClient.Undocumented.cs: SearchGuildUsersAsync.
        new("POST", "/guilds/{guild_id}/members-search", null, RouteSecurity.Bot, InSpecification: false),

        // RestClient.Guild.cs: ModifyGuildMfaLevelAsync.
        new("POST", "/guilds/{guild_id}/mfa", null, RouteSecurity.Bot, InSpecification: false),
    ];
}
