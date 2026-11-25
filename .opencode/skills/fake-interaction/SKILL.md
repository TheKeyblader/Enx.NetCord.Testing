---
name: Fake interaction
description: Recette pour ajouter un type d'interaction (slash/user/message command, autocomplete, bouton, menus, modal, entry point) ou un type de callback au faux Discord, avec builder, capture des réponses, règles Discord, harnais NetCord.Services et tests.
---

# Ajouter une interaction

## 1. Sources

- `discord-api-docs/developers/interactions/receiving-and-responding.mdx` (objet interaction,
  types, callbacks, followups, 3 s / 15 min), `application-commands.mdx` (options, sous-commandes,
  autocomplete, `resolved`), `developers/components/reference.mdx` (types, `custom_id`, modals,
  Components V2 avec flag `IS_COMPONENTS_V2`).
- NetCord : `NetCord/Interaction.cs` (`CreateFromJson` → classe concrète selon `type` /
  `data.type` / `data.component_type`), `NetCord/JsonModels/JsonInteraction*.cs`,
  `NetCord/Rest/InteractionCallback*.cs`, `NetCord.Services/**/…Contexts.cs`.
- shardix : `packages/testing/src/index.ts` (`mockInteraction`, `executeInteraction`).

## 2. Builder (`src/Enx.NetCord.Testing/Interactions/Builders/`)

- Un builder fluent par famille (`SlashCommand("ping").WithOption("user", user)`,
  `Button("confirm:42")`, `StringMenu("pick", "a", "b")`, `ModalSubmit("form").WithText("name","Bob")`).
- Remplit automatiquement depuis l'état : `id` (Snowflake), `application_id`, `token` unique,
  `guild_id`, `channel` (objet partiel), `member` **ou** `user` (DM), `app_permissions`
  calculées, `locale`/`guild_locale`, `entitlements`, `authorizing_integration_owners`,
  `context`, `attachment_size_limit`, `resolved` pour les options user/role/channel/attachment,
  `message` pour les composants (message existant dans l'état).
- Valide comme Discord : options requises, types, ≤ 25 choix, `custom_id` ≤ 100.

## 3. Exécution & capture (`Interactions/`)

- `discord.Interactions.ExecuteAsync(builder, handler)` ou via un `GatewayClient` (dispatch
  `INTERACTION_CREATE`) ou via HTTP (Hosting).
- Résultat `InteractionResult` : `Callback` (type + données typées), `Followups`,
  `OriginalResponse` (message dans l'état), `Edits`, `Deleted`, `Modal`, `AutocompleteChoices`,
  `IsDeferred`, `IsEphemeral`.
- Règles : un seul callback initial → sinon `40060`; followup/edit avant callback → `10015 Unknown Webhook`;
  token expiré (15 min via `TimeProvider`) → `50027 Invalid Webhook Token`; modal en réponse
  à un modal/autocomplete → `50035`.

## 4. Harnais Services (`src/Enx.NetCord.Testing.Services/`)

- Construire `ApplicationCommandService<TContext>` / `ComponentInteractionService<TContext>` /
  `CommandService<TContext>`, ajouter modules, `CreateCommandsAsync` contre le fake (enregistre
  les commandes dans l'état ⇒ `/applications/{id}/commands` cohérent), puis `ExecuteAsync`
  avec un contexte NetCord réel (`SlashCommandContext`, `HttpSlashCommandContext`…).
- Retourner à la fois `IExecutionResult` et `InteractionResult`.

## 5. Tests & parité

Tests dans `tests/Enx.NetCord.Testing.Tests/Interactions/` et `/Services/`, attribut
`[Parity("interaction:2/chat_input")]`, `[Parity("callback:4")]`, `[Parity("component:2")]`.
Mettre à jour `docs/parity/interactions.md`.
