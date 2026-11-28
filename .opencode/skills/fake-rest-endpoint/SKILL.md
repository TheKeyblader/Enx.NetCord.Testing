---
name: Fake REST endpoint
description: Recette pas à pas pour ajouter ou compléter un endpoint REST du faux Discord (état, handler, validation, erreurs Discord, rate-limit headers, dispatch Gateway, tests via RestClient NetCord, mise à jour de parité).
---

# Ajouter un endpoint REST fake

Prérequis : avoir lu `docs/architecture.md` §REST et l'ADR sur la table de routes.

## 1. Comprendre la cible

- Spec : opération dans `discord-api-spec/specs/openapi.json` (`paths["/channels/{channel_id}/messages"].post`) :
  paramètres, `requestBody` (JSON et/ou multipart), réponses `200/201/204`, erreurs.
- Docs : page `discord-api-docs/developers/resources/<resource>.mdx` (limites, permissions, events émis).
- NetCord : méthode dans `NetCord/Rest/RestClient.<Domaine>.cs` (route, query, type de corps,
  type de retour attendu → quel `JsonModel` il désérialise).
- fauxcord : `src/routes/<x>.ts`, `src/services/<x>.ts`, `src/validators/<x>.ts` pour les
  cas limites (ex. `embeds: null`, `?wait=1`, pins nouvelle/ancienne API, bulk-delete 2–100).

## 2. État (`src/Enx.NetCord.Testing/State/`)

- Ajouter/compléter l'entité (classe interne mutable dans `State/`, ADR 0003 ; seules des valeurs détachées en sortent) et ses index.
- Toute mutation passe par une méthode de `FakeDiscordState` qui :
  1. valide les invariants (existence, appartenance guilde/canal) ;
  2. applique la mutation sous verrou ;
  3. publie un **événement de domaine** (`StateChange`) que la Gateway traduit en dispatch.
- IDs : `ISnowflakeGenerator` injecté ; dates : `TimeProvider`.

## 3. Handler (`src/Enx.NetCord.Testing/Rest/Routes/<Domaine>/`)

- Un handler par opération, enregistré dans la table de routes avec le template de la spec
  (`POST /channels/{channel_id}/messages`). Routes littérales avant paramétrées.
- Entrée : `FakeRestRequest` (méthode, template, paramètres, query, corps JSON ou multipart,
  `Authorization`, `X-Audit-Log-Reason`). Sortie : `FakeRestResponse` (statut, JSON, en-têtes).
- Validation structurelle (étape 3 du pipeline, sans lire l'état) : un validateur par opération,
  lié par `RouteBinding.Of(clé, handler, validateur)`. **Réutiliser** `Rest/FormValidation.cs`
  (ne pas recopier dans la famille) : `ValidateRouteSnowflake(request, "channel_id")`,
  `TryReadJsonObject(request, out doc, out error)` (`50109` / `50035` `MODEL_TYPE_CONVERT`),
  `ReadOptionalString`, `CheckLength`, `TryReadSnowflake` (chaîne ou nombre), `AddBadType`,
  `AddRequired`, `TryParseSnowflake` / `NotSnowflakeMessage`. Accumuler les erreurs dans un
  `FormErrors` puis `DiscordError.InvalidFormBody(errors)`, ou rendre
  `RestValidation.Valid(recordDétaché)`, lu par `request.GetPayload<T>()`. Ne garder dans
  `Routes/<Domaine>/` que les règles propres au domaine ; une règle qui sert à deux familles
  remonte dans `FormValidation`.
- Pour une réponse JSON, projeter le résultat dans un DTO interne conforme à la spec Discord et le sérialiser avec `FakeDiscordJsonContext` (source-gen STJ). Le vrai client désérialise la réponse ; réserver les `NetCord.JsonModels` aux API NetCord qui les exigent, comme `Interaction.CreateFromJson`.
- Erreurs : `DiscordError.*` (404 `10003 Unknown Channel`, 400 `50035 Invalid Form Body`
  avec arbre `errors`, 403 `50013 Missing Permissions`, 401 `0 401: Unauthorized`…). Ajouter
  le code à `docs/parity/errors.md` s'il est nouveau.
- Visibilité (amendement 04B de l'ADR 0004) : toute opération d'état qui reçoit un ID de guilde
  ou de salon prend `StateCaller.Bot` depuis `Rest/` (jamais `StateCaller.Fixture`, réservé au
  seed et aux commandes de test). Résoudre la guilde ou le salon par `GetGuild(caller, id)` /
  `GetChannel(caller, id)` (contrôle `GuildAccess` : `10004` masquée pour `/guilds/{id}` et
  `preview`, `50001` refusée ailleurs) et traduire tout échec par
  `DiscordError.FromStateFailure(failure)`. Ne jamais appeler `FindGuild`, `FindChannel`,
  `FindMember` ni `FindMessage` depuis `Rest/` (lectures non contrôlées). Tester « guilde
  inconnue » **et** « bot non membre ». Les routes à jeton (interaction, webhook) ne passent pas
  par ce contrôle.
- En-têtes rate limit (`X-RateLimit-Limit/Remaining/Reset/Reset-After/Bucket`) via le
  middleware commun ; ne pas les écrire dans le handler.
- `204 No Content` quand Discord le fait (DELETE, PUT réaction…).

## 4. Tests (`tests/Enx.NetCord.Testing.Tests/Rest/<Domaine>/`)

```csharp
[Fact, Parity("POST /channels/{channel_id}/messages")]
public async Task SendMessageAsync_ValidContent_CreatesMessageAndDispatchesMessageCreate()
{
    await using var discord = FakeDiscord.Create();
    var channel = discord.Seed.Guild("Test").TextChannel("general");
    var rest = discord.CreateRestClient();          // vrai NetCord.Rest.RestClient

    var message = await rest.SendMessageAsync(channel.Id, new() { Content = "hi" });

    Assert.Equal("hi", message.Content);
    Assert.Equal("hi", (await rest.GetMessageAsync(channel.Id, message.Id)).Content);
    discord.Gateway.Dispatched.Should().Contain("MESSAGE_CREATE"); // API d'assertion du fake
}
```

Pour affirmer qu'une requête a (ou n'a pas) été tentée, utiliser les assertions de l'ADR 0012 sur
le journal plutôt que du LINQ sur `Recorded.Requests` : `discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{channel.Id}/messages")`
(aussi `ShouldHaveRequested`, `ShouldNotHaveRequested`, et sur une suite : `Requests.Skip(n).Should…`).
Le motif est la clé `METHOD /template` où un paramètre peut être une valeur **décodée** ; interpoler
l'`Id` (jamais la poignée du seed). Un motif inconnu lève `ArgumentException`. Elles comptent des
tentatives, erreurs comprises. Pour émettre une requête de test avec une route qu'aucun
endpoint n'implémente encore, `RestClient.SendRequestAsync(method, content, $"/channels/{id}/…")` prend un
`FormattableString` (les arguments sont encodés par NetCord) : écrire un littéral interpolé, pas une variable `string`.
Si la spec change (`/parity/sync`), régénérer l'inventaire : la liste `RouteInventory.SnowflakeParameters`
(paramètres typés `SnowflakeType`) en fait partie.

Minimum : nominal, relecture d'état, `Unknown *` 404, validation 50035, (401 si token
invalide modélisé), event Gateway émis (ou non). Les API exactes (`Seed`, `CreateRestClient`,
assertions) sont celles fixées par les ADR — adapter l'exemple.

## 5. Parité

Dans `docs/parity/rest.md`, ligne `POST` `/channels/{channel_id}/messages` : `Fake` ✅ ou 🟡
(+ Note : ce qui manque, ex. « attachments multipart non gérés »), `Tests` ✅. Recalculer
les compteurs d'en-tête.
