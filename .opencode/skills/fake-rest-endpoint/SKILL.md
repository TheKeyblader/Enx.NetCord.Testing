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

- Ajouter/compléter l'entité (record immuable ou classe interne) et ses index.
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
- Sérialiser avec les `JsonModels` attendus par NetCord (même noms/champs que l'API Discord).
- Erreurs : `DiscordError.*` (404 `10003 Unknown Channel`, 400 `50035 Invalid Form Body`
  avec arbre `errors`, 403 `50013 Missing Permissions`, 401 `0 401: Unauthorized`…). Ajouter
  le code à `docs/parity/errors.md` s'il est nouveau.
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

Minimum : nominal, relecture d'état, `Unknown *` 404, validation 50035, (401 si token
invalide modélisé), event Gateway émis (ou non). Les API exactes (`Seed`, `CreateRestClient`,
assertions) sont celles fixées par les ADR — adapter l'exemple.

## 5. Parité

Dans `docs/parity/rest.md`, ligne `POST` `/channels/{channel_id}/messages` : `Fake` ✅ ou 🟡
(+ Note : ce qui manque, ex. « attachments multipart non gérés »), `Tests` ✅. Recalculer
les compteurs d'en-tête.
