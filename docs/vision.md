# Vision — Enx.NetCord.Testing

## Problème

Tester un bot NetCord aujourd'hui, c'est soit des tests manuels sur un vrai serveur Discord,
soit mocker à la main des types NetCord (souvent sans constructeur public pratique) et perdre
toute fidélité. NetCord teste ses propres services avec un `GatewayClient` doté d'un faux token
(`Tests/ServicesTest`), mais n'offre pas de kit de test aux utilisateurs.

## Proposition

Un package NuGet qui fournit un **faux Discord en mémoire** :

- branché sur les **seams publics** de NetCord (REST `IRestRequestHandler`, Gateway
  `IWebSocketConnectionProvider`, `Interaction.CreateFromJson`) : le code du bot utilise
  les **vrais** `RestClient` / `GatewayClient` / services NetCord ;
- **stateful** et cohérent comme fauxcord : créer un message par REST le rend lisible par GET,
  déclenche `MESSAGE_CREATE` sur la Gateway et alimente le cache ;
- **ergonomique** comme `@shardix/testing` : un test d'une slash command tient en ~10 lignes et
  expose une réponse inspectable ;
- **déterministe et rapide** : zéro réseau, zéro attente réelle, horloge contrôlée ;
- **traçable** : la couverture de l'API Discord est mesurée en continu (`docs/parity/`).

## Expérience cible (indicative — l'API exacte est fixée par les ADR)

```csharp
[Fact]
public async Task Ping_RepliesPong()
{
    await using var discord = FakeDiscord.Create();
    var guild = discord.Seed.Guild("Test");
    var alice = guild.Member("alice");

    await using var bot = await discord.Services()
        .AddApplicationCommandModule<PingModule>()
        .BuildAsync();

    var result = await bot.ExecuteSlashCommandAsync("ping", from: alice, @in: guild.TextChannel("general"));

    result.Callback.Type.Should().Be(InteractionCallbackType.Message);
    result.Response.Content.Should().Be("Pong!");
}
```

```csharp
[Fact]
public async Task Welcome_SendsMessageOnMemberJoin()
{
    await using var discord = FakeDiscord.Create();
    var guild = discord.Seed.Guild("Test").TextChannel("welcome", out var welcome);
    using var host = MyBot.CreateHost().UseFakeDiscord(discord).Build();
    await host.StartAsync();

    await discord.Gateway.GuildMemberAddAsync(guild, discord.Seed.User("bob"));
    await discord.Gateway.FlushAsync();

    discord.State.Messages(welcome).Should().ContainSingle(m => m.Content.Contains("bob"));
}
```

## Publics

1. Développeurs de bots NetCord (tests unitaires de modules, tests d'intégration du host).
2. Auteurs de bibliothèques au-dessus de NetCord (frameworks, extensions).

## Objectifs (par priorité)

1. Interactions (slash/user/message commands, autocomplete, composants, modals) via NetCord.Services.
2. REST stateful pour les ressources courantes : channels, messages, réactions, threads,
   guilds, members, roles, bans, webhooks, application commands, interactions/callbacks.
3. Gateway : cycle de connexion complet, dispatch des events cohérents avec l'état, intents,
   sharding, reconnexion/resume.
4. Hosting : `IHost` / ASP.NET Core (HTTP interactions signées).
5. Assertions/inspections : requêtes enregistrées, réponses d'interaction, état.
6. Parité mesurée et à jour avec la spec Discord et NetCord.

## Non-objectifs (v1)

- Voix (UDP, chiffrement, Opus) — ➖ dans les matrices.
- OAuth2 utilisateur complet, Activities/Embedded App SDK, lobbies, partner SDK, monétisation
  au-delà des entitlements de test.
- Être un serveur HTTP/WebSocket autonome multi-langages (c'est fauxcord). Un mode
  « serveur » hors processus pourra être étudié plus tard (M7) via ADR.
- Simuler les permissions Discord exhaustivement dès v1 (modèle simplifié, enrichi par jalons).
- Modéliser les limites de débit réelles de Discord (en-têtes oui, throttling optionnel).

## Critères de succès

- Le `samples/SampleBot.Tests` couvre un bot réaliste sans aucun mock manuel.
- ≥ 80 % des lignes REST « exposées par NetCord » des domaines prioritaires en ✅ à la v1.0.
- Suite de tests de la lib < 30 s, 0 test flaky sur 50 exécutions.
- Synchro de parité ≤ 14 jours de retard sur la spec/NetCord.
