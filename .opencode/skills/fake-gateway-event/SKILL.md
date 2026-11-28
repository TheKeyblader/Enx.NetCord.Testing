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

### Fabrique du client (M2-12, ADR 0008 § 2)

- `discord.CreateGatewayClient()` (sans paramètre) passe par `Gateway/GatewayClientFactory` :
  une `GatewayClientConfiguration` **neuve par client** (`Hostname` sentinelle,
  `UncompressedGatewayCompression`, provider WebSocket du fake, et
  `RestClientConfiguration = RestClientFactory.CreateConfiguration(context)` : ne jamais
  dupliquer le branchement REST). Toute surcharge (intents, shards, M3-ADR-09) part de cette
  configuration ; l'appel sans argument doit rester valide.
- Jusqu'à M3-01, le provider est `UnavailableWebSocketConnectionProvider` : `CreateConnection()`
  lève `ObjectDisposedException` si le fake est fermé, sinon `NotSupportedException`. NetCord
  appelle `CreateConnection()` en tête de `ConnectAsync` et **ne réessaie pas** : l'exception
  sort de `StartAsync`, l'état est remis à zéro (un second `StartAsync` échoue pareil) et
  l'événement `Connecting` n'est pas levé. Le `ReconnectStrategy` ne joue qu'après une
  connexion établie : en M3, injecter une stratégie sans délai réel.
- `GatewayClient` n'expose ni sa compression ni son URI : vérifier la configuration par un test
  interne sur `GatewayClientFactory.CreateConfiguration(...)` (en complément des tests via le
  vrai client).
- `GatewayClient` est `IDisposable` seulement (pas `IAsyncDisposable`) : `using var gateway = …`,
  pas `await using`. Il possède son `Rest` (construit depuis la configuration) et le dispose.

## 3. Tests (`tests/Enx.NetCord.Testing.Tests/Gateway/`)

```csharp
[Fact, Parity("MESSAGE_CREATE")]
public async Task MessageCreate_AfterRestSend_IsReceivedByGatewayClient()
{
    await using var discord = FakeDiscord.Create();
    var channel = discord.Seed.Guild("G").TextChannel("general");
    // Signature indicative : la surcharge avec intents relève de M3-ADR-09.
    using var client = discord.CreateGatewayClient(GatewayIntents.GuildMessages | GatewayIntents.MessageContent);
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
