---
name: Fake Gateway event
description: Recette pour ajouter un opcode ou un event dispatch Gateway au faux Discord (payload fidèle, intents, shard, séquence, traduction depuis les changements d'état, API de test, tests avec le vrai GatewayClient NetCord, parité).
---

# Ajouter un event Gateway

## 1. Sources

- Payload : `discord-api-docs/developers/events/gateway-events.mdx` (section de l'event).
- Intent requis : `discord-api-docs/developers/events/gateway.mdx` § « List of Intents ».
- NetCord : `NetCord/Gateway/GatewayClient.cs` (`case "EVENT_NAME":` → quel `JsonModel`, quel
  event C# — ex. `MessageCreate`), `NetCord/Gateway/JsonModels/**`, `NetCord/Gateway/EventArgs/**`.
- fauxcord : `src/gateway/dispatch.ts`, `src/gateway/intents.ts`.

## 2. Implémentation (`src/Enx.NetCord.Testing/Gateway/`)

- `Dispatch/<Domaine>Dispatches.cs` : méthode qui construit le payload `{ op: 0, t, s, d }`
  à partir d'un `StateChange` (ou d'un appel explicite du test).
- Déclarer l'intent requis dans la table `EventIntents` (un seul endroit) ; les events
  `GUILD_MEMBER_*`/`PRESENCE_UPDATE`/`MESSAGE_CONTENT` (champ `content` vidé sans l'intent) sont
  **privilégiés**.
- Routage shard : `(guild_id >> 22) % shard_count` ; events sans guilde → shard 0.
- `s` (séquence) incrémentée par session ; stocker pour `RESUME`.
- Exposer côté test : `discord.Gateway.DispatchAsync(new MessageCreateEvent(...))` (forme exacte
  selon ADR) + barrière `await discord.Gateway.FlushAsync()` qui garantit que NetCord a traité
  tous les messages en file (sans `Task.Delay`).

## 3. Tests (`tests/Enx.NetCord.Testing.Tests/Gateway/`)

```csharp
[Fact, Parity("MESSAGE_CREATE")]
public async Task MessageCreate_AfterRestSend_IsReceivedByGatewayClient()
{
    await using var discord = FakeDiscord.Create();
    var channel = discord.Seed.Guild("G").TextChannel("general");
    await using var client = discord.CreateGatewayClient(GatewayIntents.GuildMessages | GatewayIntents.MessageContent);
    var received = new List<Message>();
    client.MessageCreate += m => { received.Add(m); return default; };
    await client.StartAsync();
    await discord.Gateway.WaitForReadyAsync(client);

    await client.Rest.SendMessageAsync(channel.Id, "hello");
    await discord.Gateway.FlushAsync();

    Assert.Single(received);
}
```

Couvrir aussi : event **non** reçu sans l'intent ; `content` vide sans `MessageContent` ;
mauvais shard ⇒ non reçu.

## 4. Parité

`docs/parity/gateway.md` : ligne de l'event → `Fake`, `Tests`, `Notes` (champs non modélisés).
