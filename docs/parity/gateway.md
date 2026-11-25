# Parité Gateway

<!-- Baseline générée le 2026-11-25 depuis discord-api-docs@62ab2d50 (developers/events/gateway*.mdx, topics/opcodes-and-status-codes.mdx) et NetCord@bb5f4e13 (Gateway/GatewayClient.cs, GatewayOpcode.cs). Maintenue ensuite par les agents (skill discord-parity). -->

Légende — `NetCord` : ✔ géré par `GatewayClient` (opcode envoyé/traité, ou `case "EVENT"`), vide sinon. `Fake` : ⬜ à faire · 🟡 partiel · ✅ fait · ➖ hors périmètre · ⛔ bloqué. `Tests` : ⬜ / ✅ (`[Parity("<clé>")]`).

Progression `Fake` (toutes sections, hors ➖) : ✅ **0** · 🟡 **0** · ⬜ **113** · ⛔ **0** — ➖ **12**

Sémantique transverse à couvrir (lignes de suivi) :

| Clé | Description | Fake | Tests | Notes |
| --- | --- | --- | --- | --- |
| `intents-filter` | Events filtrés selon les intents d'IDENTIFY | ⬜ | ⬜ | |
| `message-content` | `content`/`embeds`/`attachments`/`components`/`poll` vidés sans MESSAGE_CONTENT (sauf DM, mention, auteur = bot) | ⬜ | ⬜ | |
| `privileged-4014` | Intent privilégié non autorisé ⇒ close 4014 | ⬜ | ⬜ | Autorisations configurables dans `FakeDiscordOptions` |
| `shard-routing` | `(guild_id >> 22) % num_shards`, DM ⇒ shard 0 | ⬜ | ⬜ | |
| `resume-replay` | RESUME rejoue les dispatchs manqués depuis `seq` | ⬜ | ⬜ | |
| `heartbeat-timeout` | Pas d'ACK ⇒ NetCord reconnecte (piloté par `TimeProvider`) | ⬜ | ⬜ | |
| `send-rate-limit` | 120 envois / 60 s ⇒ close 4008 | ⬜ | ⬜ | |
| `ready-guild-create` | READY (guildes indisponibles) puis GUILD_CREATE complet par guilde | ⬜ | ⬜ | |

## Opcodes

| Clé | Nom | Client action | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `op 0` | Dispatch | Receive | ⬜ | ⬜ | An event was dispatched. |
| `op 1` | Heartbeat | Send/Receive | ⬜ | ⬜ | Fired periodically by the client to keep the connection alive. |
| `op 2` | Identify | Send | ⬜ | ⬜ | Starts a new session during the initial handshake. |
| `op 3` | Presence Update | Send | ⬜ | ⬜ | Update the client's presence. |
| `op 4` | Voice State Update | Send | ⬜ | ⬜ | Used to join/leave or move between voice channels. |
| `op 6` | Resume | Send | ⬜ | ⬜ | Resume a previous session that was disconnected. |
| `op 7` | Reconnect | Receive | ⬜ | ⬜ | You should attempt to reconnect and resume immediately. |
| `op 8` | Request Guild Members | Send | ⬜ | ⬜ | Request information about offline guild members in a large guild. |
| `op 9` | Invalid Session | Receive | ⬜ | ⬜ | The session has been invalidated. You should reconnect and identify/resume accordingly. |
| `op 10` | Hello | Receive | ⬜ | ⬜ | Sent immediately after connecting, contains the `heartbeat_interval` to use. |
| `op 11` | Heartbeat ACK | Receive | ⬜ | ⬜ | Sent in response to receiving a heartbeat to acknowledge that it has been received. |
| `op 31` | Request Soundboard Sounds | Send | ➖ | ➖ | Request information about soundboard sounds in a set of guilds. Absent de `GatewayOpcode` NetCord @bb5f4e13. |
| `op 43` | Request Channel Info | Send | ➖ | ➖ | Request ephemeral channel data for channels in a guild. Absent de `GatewayOpcode` NetCord @bb5f4e13. |

## Close codes

| Clé | Description | Reconnect | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `4000` | Unknown error | true | ⬜ | ⬜ | |
| `4001` | Unknown opcode | true | ⬜ | ⬜ | |
| `4002` | Decode error | true | ⬜ | ⬜ | |
| `4003` | Not authenticated | true | ⬜ | ⬜ | |
| `4004` | Authentication failed | false | ⬜ | ⬜ | |
| `4005` | Already authenticated | true | ⬜ | ⬜ | |
| `4007` | Invalid `seq` | true | ⬜ | ⬜ | |
| `4008` | Rate limited | true | ⬜ | ⬜ | |
| `4009` | Session timed out | true | ⬜ | ⬜ | |
| `4010` | Invalid shard | false | ⬜ | ⬜ | |
| `4011` | Sharding required | false | ⬜ | ⬜ | |
| `4012` | Invalid API version | false | ⬜ | ⬜ | |
| `4013` | Invalid intent(s) | false | ⬜ | ⬜ | |
| `4014` | Disallowed intent(s) | false | ⬜ | ⬜ | |

## Send events (client → Discord)

| Clé | Description | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `IDENTIFY` | Triggers the initial handshake with the gateway | ✔ | ⬜ | ⬜ |  |
| `RESUME` | Resumes a dropped gateway connection | ✔ | ⬜ | ⬜ |  |
| `HEARTBEAT` | Maintains an active gateway connection | ✔ | ⬜ | ⬜ |  |
| `REQUEST_GUILD_MEMBERS` | Requests members for a guild | ✔ | ⬜ | ⬜ |  |
| `REQUEST_SOUNDBOARD_SOUNDS` | Requests soundboard sounds in a set of guilds |  | ➖ | ➖ | Opcode absent de `GatewayOpcode` NetCord @bb5f4e13 |
| `REQUEST_CHANNEL_INFO` | Requests ephemeral channel data for channels in a guild |  | ➖ | ➖ | Opcode absent de `GatewayOpcode` NetCord @bb5f4e13 |
| `UPDATE_VOICE_STATE` | Joins, moves, or disconnects the app from a voice channel | ✔ | ⬜ | ⬜ |  |
| `UPDATE_PRESENCE` | Updates an app's presence | ✔ | ⬜ | ⬜ |  |

## Receive events (Discord → client)

Events documentés : **82** · gérés par NetCord : **71**

| Clé | Description | Intent(s) | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| `HELLO` | Defines the heartbeat interval | — | ✔ | ⬜ | ⬜ |  |
| `READY` | Contains the initial state information | — | ✔ | ⬜ | ⬜ |  |
| `RESUMED` | Response to [Resume](/developers/events/gateway-events#resume) | — | ✔ | ⬜ | ⬜ |  |
| `RECONNECT` | Server is going away, client should reconnect to gateway and resume | — | ✔ | ⬜ | ⬜ |  |
| `RATE_LIMITED` | Application was rate limited for a gateway opcode | — | ✔ | ⬜ | ⬜ |  |
| `INVALID_SESSION` | Failure response to [Identify](/developers/events/gateway-events#identify) or [Resume](/developers/events/gateway-events#resume) or invalid active session | — | ✔ | ⬜ | ⬜ |  |
| `APPLICATION_COMMAND_PERMISSIONS_UPDATE` | Application command permission was updated |  | ✔ | ⬜ | ⬜ |  |
| `AUTO_MODERATION_RULE_CREATE` | Auto Moderation rule was created | AUTO_MODERATION_CONFIGURATION (1<<20) | ✔ | ⬜ | ⬜ |  |
| `AUTO_MODERATION_RULE_UPDATE` | Auto Moderation rule was updated | AUTO_MODERATION_CONFIGURATION (1<<20) | ✔ | ⬜ | ⬜ |  |
| `AUTO_MODERATION_RULE_DELETE` | Auto Moderation rule was deleted | AUTO_MODERATION_CONFIGURATION (1<<20) | ✔ | ⬜ | ⬜ |  |
| `AUTO_MODERATION_ACTION_EXECUTION` | Auto Moderation rule was triggered and an action was executed (e.g. a message was blocked) | AUTO_MODERATION_EXECUTION (1<<21) | ✔ | ⬜ | ⬜ |  |
| `CHANNEL_CREATE` | New guild channel created | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `CHANNEL_UPDATE` | Channel was updated | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `CHANNEL_DELETE` | Channel was deleted | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `CHANNEL_INFO` | Response to [Request Channel Info](/developers/events/gateway-events#request-channel-info) |  |  | ➖ | ➖ | Non géré par `GatewayClient` NetCord @bb5f4e13 |
| `CHANNEL_PINS_UPDATE` | Message was pinned or unpinned | GUILDS (1<<0), DIRECT_MESSAGES (1<<12) | ✔ | ⬜ | ⬜ |  |
| `THREAD_CREATE` | Thread created, also sent when being added to a private thread | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `THREAD_UPDATE` | Thread was updated | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `THREAD_DELETE` | Thread was deleted | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `THREAD_LIST_SYNC` | Sent when gaining access to a channel, contains all active threads in that channel | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `THREAD_MEMBER_UPDATE` | [Thread member](/developers/resources/channel#thread-member-object) for the current user was updated | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `THREAD_MEMBERS_UPDATE` | Some user(s) were added to or removed from a thread | GUILDS (1<<0), GUILD_MEMBERS (1<<1) privilégié | ✔ | ⬜ | ⬜ |  |
| `ENTITLEMENT_CREATE` | Entitlement was created |  | ✔ | ⬜ | ⬜ |  |
| `ENTITLEMENT_UPDATE` | Entitlement was updated or renewed |  | ✔ | ⬜ | ⬜ |  |
| `ENTITLEMENT_DELETE` | Entitlement was deleted |  | ✔ | ⬜ | ⬜ |  |
| `GUILD_CREATE` | Lazy-load for unavailable guild, guild became available, or user joined a new guild | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `GUILD_UPDATE` | Guild was updated | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `GUILD_DELETE` | Guild became unavailable, or user left/was removed from a guild | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `GUILD_AUDIT_LOG_ENTRY_CREATE` | A guild audit log entry was created | GUILD_MODERATION (1<<2) | ✔ | ⬜ | ⬜ |  |
| `GUILD_BAN_ADD` | User was banned from a guild | GUILD_MODERATION (1<<2) | ✔ | ⬜ | ⬜ |  |
| `GUILD_BAN_REMOVE` | User was unbanned from a guild | GUILD_MODERATION (1<<2) | ✔ | ⬜ | ⬜ |  |
| `GUILD_EMOJIS_UPDATE` | Guild emojis were updated | GUILD_EXPRESSIONS (1<<3) | ✔ | ⬜ | ⬜ |  |
| `GUILD_STICKERS_UPDATE` | Guild stickers were updated | GUILD_EXPRESSIONS (1<<3) | ✔ | ⬜ | ⬜ |  |
| `GUILD_INTEGRATIONS_UPDATE` | Guild integration was updated | GUILD_INTEGRATIONS (1<<4) | ✔ | ⬜ | ⬜ |  |
| `GUILD_MEMBER_ADD` | New user joined a guild | GUILD_MEMBERS (1<<1) privilégié | ✔ | ⬜ | ⬜ |  |
| `GUILD_MEMBER_REMOVE` | User was removed from a guild | GUILD_MEMBERS (1<<1) privilégié | ✔ | ⬜ | ⬜ |  |
| `GUILD_MEMBER_UPDATE` | Guild member was updated | GUILD_MEMBERS (1<<1) privilégié | ✔ | ⬜ | ⬜ |  |
| `GUILD_MEMBERS_CHUNK` | Response to [Request Guild Members](/developers/events/gateway-events#request-guild-members) |  | ✔ | ⬜ | ⬜ |  |
| `GUILD_ROLE_CREATE` | Guild role was created | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `GUILD_ROLE_UPDATE` | Guild role was updated | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `GUILD_ROLE_DELETE` | Guild role was deleted | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `GUILD_SCHEDULED_EVENT_CREATE` | Guild scheduled event was created | GUILD_SCHEDULED_EVENTS (1<<16) | ✔ | ⬜ | ⬜ |  |
| `GUILD_SCHEDULED_EVENT_UPDATE` | Guild scheduled event was updated | GUILD_SCHEDULED_EVENTS (1<<16) | ✔ | ⬜ | ⬜ |  |
| `GUILD_SCHEDULED_EVENT_DELETE` | Guild scheduled event was deleted | GUILD_SCHEDULED_EVENTS (1<<16) | ✔ | ⬜ | ⬜ |  |
| `GUILD_SCHEDULED_EVENT_USER_ADD` | User subscribed to a guild scheduled event | GUILD_SCHEDULED_EVENTS (1<<16) | ✔ | ⬜ | ⬜ |  |
| `GUILD_SCHEDULED_EVENT_USER_REMOVE` | User unsubscribed from a guild scheduled event | GUILD_SCHEDULED_EVENTS (1<<16) | ✔ | ⬜ | ⬜ |  |
| `GUILD_SOUNDBOARD_SOUND_CREATE` | Guild soundboard sound was created | GUILD_EXPRESSIONS (1<<3) |  | ➖ | ➖ | Non géré par `GatewayClient` NetCord @bb5f4e13 |
| `GUILD_SOUNDBOARD_SOUND_UPDATE` | Guild soundboard sound was updated | GUILD_EXPRESSIONS (1<<3) |  | ➖ | ➖ | Non géré par `GatewayClient` NetCord @bb5f4e13 |
| `GUILD_SOUNDBOARD_SOUND_DELETE` | Guild soundboard sound was deleted | GUILD_EXPRESSIONS (1<<3) |  | ➖ | ➖ | Non géré par `GatewayClient` NetCord @bb5f4e13 |
| `GUILD_SOUNDBOARD_SOUNDS_UPDATE` | Guild soundboard sounds were updated | GUILD_EXPRESSIONS (1<<3) |  | ➖ | ➖ | Non géré par `GatewayClient` NetCord @bb5f4e13 |
| `SOUNDBOARD_SOUNDS` | Response to [Request Soundboard Sounds](/developers/events/gateway-events#request-soundboard-sounds) |  |  | ➖ | ➖ | Non géré par `GatewayClient` NetCord @bb5f4e13 |
| `INTEGRATION_CREATE` | Guild integration was created | GUILD_INTEGRATIONS (1<<4) | ✔ | ⬜ | ⬜ |  |
| `INTEGRATION_UPDATE` | Guild integration was updated | GUILD_INTEGRATIONS (1<<4) | ✔ | ⬜ | ⬜ |  |
| `INTEGRATION_DELETE` | Guild integration was deleted | GUILD_INTEGRATIONS (1<<4) | ✔ | ⬜ | ⬜ |  |
| `INTERACTION_CREATE` | User used an interaction, such as an [Application Command](/developers/interactions/application-commands) | — | ✔ | ⬜ | ⬜ |  |
| `INVITE_CREATE` | Invite to a channel was created | GUILD_INVITES (1<<6) | ✔ | ⬜ | ⬜ |  |
| `INVITE_DELETE` | Invite to a channel was deleted | GUILD_INVITES (1<<6) | ✔ | ⬜ | ⬜ |  |
| `MESSAGE_CREATE` | Message was created | GUILD_MESSAGES (1<<9), DIRECT_MESSAGES (1<<12) | ✔ | ⬜ | ⬜ |  |
| `MESSAGE_UPDATE` | Message was edited | GUILD_MESSAGES (1<<9), DIRECT_MESSAGES (1<<12) | ✔ | ⬜ | ⬜ |  |
| `MESSAGE_DELETE` | Message was deleted | GUILD_MESSAGES (1<<9), DIRECT_MESSAGES (1<<12) | ✔ | ⬜ | ⬜ |  |
| `MESSAGE_DELETE_BULK` | Multiple messages were deleted at once | GUILD_MESSAGES (1<<9) | ✔ | ⬜ | ⬜ |  |
| `MESSAGE_REACTION_ADD` | User reacted to a message | GUILD_MESSAGE_REACTIONS (1<<10), DIRECT_MESSAGE_REACTIONS (1<<13) | ✔ | ⬜ | ⬜ |  |
| `MESSAGE_REACTION_REMOVE` | User removed a reaction from a message | GUILD_MESSAGE_REACTIONS (1<<10), DIRECT_MESSAGE_REACTIONS (1<<13) | ✔ | ⬜ | ⬜ |  |
| `MESSAGE_REACTION_REMOVE_ALL` | All reactions were explicitly removed from a message | GUILD_MESSAGE_REACTIONS (1<<10), DIRECT_MESSAGE_REACTIONS (1<<13) | ✔ | ⬜ | ⬜ |  |
| `MESSAGE_REACTION_REMOVE_EMOJI` | All reactions for a given emoji were explicitly removed from a message | GUILD_MESSAGE_REACTIONS (1<<10), DIRECT_MESSAGE_REACTIONS (1<<13) | ✔ | ⬜ | ⬜ |  |
| `PRESENCE_UPDATE` | User was updated | GUILD_PRESENCES (1<<8) privilégié | ✔ | ⬜ | ⬜ |  |
| `STAGE_INSTANCE_CREATE` | Stage instance was created | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `STAGE_INSTANCE_UPDATE` | Stage instance was updated | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `STAGE_INSTANCE_DELETE` | Stage instance was deleted or closed | GUILDS (1<<0) | ✔ | ⬜ | ⬜ |  |
| `SUBSCRIPTION_CREATE` | Premium App Subscription was created |  | ✔ | ⬜ | ⬜ |  |
| `SUBSCRIPTION_UPDATE` | Premium App Subscription was updated |  | ✔ | ⬜ | ⬜ |  |
| `SUBSCRIPTION_DELETE` | Premium App Subscription was deleted |  | ✔ | ⬜ | ⬜ |  |
| `TYPING_START` | User started typing in a channel | GUILD_MESSAGE_TYPING (1<<11), DIRECT_MESSAGE_TYPING (1<<14) | ✔ | ⬜ | ⬜ |  |
| `USER_UPDATE` | Properties about the user changed |  | ✔ | ⬜ | ⬜ |  |
| `VOICE_CHANNEL_EFFECT_SEND` | Someone sent an effect in a voice channel the current user is connected to | GUILD_VOICE_STATES (1<<7) | ✔ | ⬜ | ⬜ | voix : payload seulement (pas d’UDP) |
| `VOICE_CHANNEL_START_TIME_UPDATE` | Voice channel start time was updated | GUILDS (1<<0) |  | ➖ | ➖ | Non géré par `GatewayClient` NetCord @bb5f4e13 |
| `VOICE_CHANNEL_STATUS_UPDATE` | Voice channel status was updated | GUILDS (1<<0) |  | ➖ | ➖ | Non géré par `GatewayClient` NetCord @bb5f4e13 |
| `VOICE_STATE_UPDATE` | Someone joined, left, or moved a voice channel | GUILD_VOICE_STATES (1<<7) | ✔ | ⬜ | ⬜ |  |
| `VOICE_SERVER_UPDATE` | Guild's voice server was updated |  | ✔ | ⬜ | ⬜ | voix : payload seulement (pas d’UDP) |
| `WEBHOOKS_UPDATE` | Guild channel webhook was created, update, or deleted | GUILD_WEBHOOKS (1<<5) | ✔ | ⬜ | ⬜ |  |
| `MESSAGE_POLL_VOTE_ADD` | User voted on a poll | GUILD_MESSAGE_POLLS (1<<24), DIRECT_MESSAGE_POLLS (1<<25) | ✔ | ⬜ | ⬜ |  |
| `MESSAGE_POLL_VOTE_REMOVE` | User removed a vote on a poll | GUILD_MESSAGE_POLLS (1<<24), DIRECT_MESSAGE_POLLS (1<<25) | ✔ | ⬜ | ⬜ |  |
