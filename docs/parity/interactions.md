# Parité Interactions

<!-- Baseline 2026-11-25 : discord-api-docs@62ab2d50 (developers/interactions/*.mdx, developers/components/reference.mdx) et NetCord@bb5f4e13 (InteractionType.cs, ApplicationCommandType.cs, ApplicationCommandOptionType.cs, Rest/InteractionCallbackType.cs, Components/ComponentType.cs, *Interaction.cs, NetCord.Services). Maintenue par les agents (skill discord-parity). -->

Progression `Fake` (hors ➖) : ✅ **0** · 🟡 **0** · ⬜ **81** · ⛔ **0** — ➖ **2**

Légende — `NetCord` : type/classe NetCord correspondant (vide ⇒ non supporté). `Fake` : ⬜ · 🟡 · ✅ · ➖ · ⛔. `Tests` : ⬜ / ✅ (`[Parity("<clé>")]`).

## Types d'interaction

| Clé | Discord | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `interaction:1` | PING | `PingInteraction` | ⬜ | ⬜ | HTTP uniquement (Hosting) |
| `interaction:2/chat_input` | APPLICATION_COMMAND · CHAT_INPUT (1) | `SlashCommandInteraction` | ⬜ | ⬜ | |
| `interaction:2/user` | APPLICATION_COMMAND · USER (2) | `UserCommandInteraction` | ⬜ | ⬜ | |
| `interaction:2/message` | APPLICATION_COMMAND · MESSAGE (3) | `MessageCommandInteraction` | ⬜ | ⬜ | |
| `interaction:2/primary_entry_point` | APPLICATION_COMMAND · PRIMARY_ENTRY_POINT (4) | `EntryPointCommandInteraction` | ⬜ | ⬜ | Activities hors vision : builder minimal seulement |
| `interaction:3/button` | MESSAGE_COMPONENT · Button | `ButtonInteraction` | ⬜ | ⬜ | |
| `interaction:3/string_select` | MESSAGE_COMPONENT · String Select | `StringMenuInteraction` | ⬜ | ⬜ | |
| `interaction:3/user_select` | MESSAGE_COMPONENT · User Select | `UserMenuInteraction` | ⬜ | ⬜ | `resolved.users/members` |
| `interaction:3/role_select` | MESSAGE_COMPONENT · Role Select | `RoleMenuInteraction` | ⬜ | ⬜ | `resolved.roles` |
| `interaction:3/mentionable_select` | MESSAGE_COMPONENT · Mentionable Select | `MentionableMenuInteraction` | ⬜ | ⬜ | |
| `interaction:3/channel_select` | MESSAGE_COMPONENT · Channel Select | `ChannelMenuInteraction` | ⬜ | ⬜ | `resolved.channels` |
| `interaction:4` | APPLICATION_COMMAND_AUTOCOMPLETE | `AutocompleteInteraction` | ⬜ | ⬜ | option `focused` |
| `interaction:5` | MODAL_SUBMIT | `ModalInteraction` | ⬜ | ⬜ | composants V2 de modal (Label, FileUpload, Radio/Checkbox) |

## Champs de l'objet interaction

| Clé | Champ | Fake | Tests | Notes |
| --- | --- | --- | --- | --- |
| `field:guild-context` | `guild_id`, `guild` (partiel), `member` (+ `permissions`) | ⬜ | ⬜ | |
| `field:dm-context` | `user` (DM / bot DM), `context` = 1/2 | ⬜ | ⬜ | |
| `field:channel` | `channel` partiel + `channel_id` | ⬜ | ⬜ | |
| `field:app_permissions` | permissions calculées de l'app dans le salon | ⬜ | ⬜ | dépend de M4-REST-05 |
| `field:locale` | `locale`, `guild_locale` | ⬜ | ⬜ | |
| `field:entitlements` | `entitlements` (SKUs de test) | ⬜ | ⬜ | |
| `field:authorizing_integration_owners` | guild install / user install | ⬜ | ⬜ | |
| `field:attachment_size_limit` | limite d'upload | ⬜ | ⬜ | |
| `field:resolved` | users/members/roles/channels/messages/attachments | ⬜ | ⬜ | |
| `field:message` | message source (composants) | ⬜ | ⬜ | doit exister dans l'état |

## Types de commande & options

| Clé | Discord | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `option:1` | SUB_COMMAND | `SubCommand` | ⬜ | ⬜ | |
| `option:2` | SUB_COMMAND_GROUP | `SubCommandGroup` | ⬜ | ⬜ | |
| `option:3` | STRING | `String` | ⬜ | ⬜ | min/max length, choices |
| `option:4` | INTEGER | `Integer` | ⬜ | ⬜ | min/max value, bornes ±2^53 |
| `option:5` | BOOLEAN | `Boolean` | ⬜ | ⬜ | |
| `option:6` | USER | `User` | ⬜ | ⬜ | + `resolved` |
| `option:7` | CHANNEL | `Channel` | ⬜ | ⬜ | `channel_types` |
| `option:8` | ROLE | `Role` | ⬜ | ⬜ | |
| `option:9` | MENTIONABLE | `Mentionable` | ⬜ | ⬜ | |
| `option:10` | NUMBER | `Double` | ⬜ | ⬜ | |
| `option:11` | ATTACHMENT | `Attachment` | ⬜ | ⬜ | `resolved.attachments` + CDN fake |

## Callbacks

| Clé | Discord | NetCord (`InteractionCallbackType`) | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `callback:1` | PONG | `Pong` | ⬜ | ⬜ | |
| `callback:4` | CHANNEL_MESSAGE_WITH_SOURCE | `Message` | ⬜ | ⬜ | crée le message (flags EPHEMERAL, IS_COMPONENTS_V2) |
| `callback:5` | DEFERRED_CHANNEL_MESSAGE_WITH_SOURCE | `DeferredMessage` | ⬜ | ⬜ | |
| `callback:6` | DEFERRED_UPDATE_MESSAGE | `DeferredModifyMessage` | ⬜ | ⬜ | composants uniquement |
| `callback:7` | UPDATE_MESSAGE | `ModifyMessage` | ⬜ | ⬜ | composants uniquement |
| `callback:8` | APPLICATION_COMMAND_AUTOCOMPLETE_RESULT | `Autocomplete` | ⬜ | ⬜ | ≤ 25 choix |
| `callback:9` | MODAL | `Modal` | ⬜ | ⬜ | interdit en réponse à modal/autocomplete |
| `callback:10` | PREMIUM_REQUIRED (déprécié) | — | ➖ | ➖ | Déprécié côté Discord, absent de NetCord @bb5f4e13 |
| `callback:12` | LAUNCH_ACTIVITY | `LaunchActivity` | ⬜ | ⬜ | enregistrement seulement |
| `callback:with_response` | `?with_response=true` → `InteractionCallbackResponse` | `SendResponseAsync(withResponse: true)` | ⬜ | ⬜ | |

## Règles de cycle de vie

| Clé | Règle | Fake | Tests | Notes |
| --- | --- | --- | --- | --- |
| `rule:single-ack` | 2e callback initial ⇒ `40060 Interaction has already been acknowledged` | ⬜ | ⬜ | |
| `rule:token-15min` | token webhook valable 15 min ⇒ `50027 Invalid Webhook Token` | ⬜ | ⬜ | `TimeProvider` |
| `rule:3s-deadline` | callback après 3 s ⇒ `10062 Unknown interaction` (opt-in) | ⬜ | ⬜ | désactivé par défaut |
| `rule:original-response` | GET/PATCH/DELETE `@original` | ⬜ | ⬜ | cf. rest.md `/webhooks/{application_id}/{interaction_token}/messages/@original` |
| `rule:followups` | followups `POST /webhooks/{application_id}/{token}` + edit/delete | ⬜ | ⬜ | |
| `rule:ephemeral` | flag 64 : invisible pour les autres, non relisible par REST bot | ⬜ | ⬜ | |
| `rule:modal-origin` | modal seulement depuis commande/composant | ⬜ | ⬜ | |

## Composants (`ComponentType`)

| Clé | Discord | NetCord | Fake | Tests | Notes |
| --- | --- | --- | --- | --- | --- |
| `component:1` | Action Row | `ActionRow` | ⬜ | ⬜ | |
| `component:2` | Button | `Button` | ⬜ | ⬜ | styles, link/premium |
| `component:3` | String Select | `StringMenu` | ⬜ | ⬜ | |
| `component:4` | Text Input | `TextInput` | ⬜ | ⬜ | modal |
| `component:5` | User Select | `UserMenu` | ⬜ | ⬜ | |
| `component:6` | Role Select | `RoleMenu` | ⬜ | ⬜ | |
| `component:7` | Mentionable Select | `MentionableMenu` | ⬜ | ⬜ | |
| `component:8` | Channel Select | `ChannelMenu` | ⬜ | ⬜ | |
| `component:9` | Section | `Section` | ⬜ | ⬜ | V2 |
| `component:10` | Text Display | `TextDisplay` | ⬜ | ⬜ | V2 |
| `component:11` | Thumbnail | `Thumbnail` | ⬜ | ⬜ | V2 |
| `component:12` | Media Gallery | `MediaGallery` | ⬜ | ⬜ | V2 |
| `component:13` | File | `File` | ⬜ | ⬜ | V2, `attachment://` |
| `component:14` | Separator | `Separator` | ⬜ | ⬜ | V2 |
| `component:16` | Content Inventory Entry (non documenté) | `ContentInventoryEntry` | ➖ | ➖ | Absent de discord-api-docs @62ab2d50 |
| `component:17` | Container | `Container` | ⬜ | ⬜ | V2 |
| `component:18` | Label | `Label` | ⬜ | ⬜ | modal |
| `component:19` | File Upload | `FileUpload` | ⬜ | ⬜ | modal |
| `component:21` | Radio Group | `RadioGroup` | ⬜ | ⬜ | modal |
| `component:22` | Checkbox Group | `CheckboxGroup` | ⬜ | ⬜ | modal |
| `component:23` | Checkbox | `Checkbox` | ⬜ | ⬜ | modal |
| `component:validation` | limites (5 rows, 5 boutons/row, 40 composants V2, `custom_id` ≤ 100…) ⇒ `50035` | — | ⬜ | ⬜ | |

## Harnais NetCord.Services / Hosting

| Clé | Cible | Fake | Tests | Notes |
| --- | --- | --- | --- | --- |
| `services:application-commands` | `ApplicationCommandService<TContext>` (Gateway et Http contexts) | ⬜ | ⬜ | |
| `services:register-commands` | `CreateCommandsAsync` ⇒ `PUT /applications/{id}/commands` cohérent | ⬜ | ⬜ | |
| `services:autocomplete` | `IAutocompleteProvider` via `AutocompleteInteraction` | ⬜ | ⬜ | |
| `services:component-interactions` | `ComponentInteractionService<TContext>` (boutons, menus, modals) | ⬜ | ⬜ | |
| `services:text-commands` | `CommandService<TContext>` via `MESSAGE_CREATE` | ⬜ | ⬜ | |
| `services:minimal-api` | `host.AddSlashCommand(...)` (NetCord.Hosting.Services) | ⬜ | ⬜ | |
| `hosting:gateway` | `UseDiscordGateway()` branché sur le fake | ⬜ | ⬜ | |
| `hosting:sharded` | `UseDiscordShards()` | ⬜ | ⬜ | |
| `hosting:http-interactions` | `UseHttpInteractions()` + POST signé Ed25519 | ⬜ | ⬜ | |
| `hosting:webhook-events` | Webhook events HTTP (`APPLICATION_AUTHORIZED`, `ENTITLEMENT_CREATE`…) | ⬜ | ⬜ | `IWebhookHandler` |
