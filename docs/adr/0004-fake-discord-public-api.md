# 0004 — Façade FakeDiscord, seed, journal et création des clients

- **Statut** : Acceptée
- **Date** : 2026-11-26
- **Auteur** : agent `architect`
- **Tâches liées** : M1-ADR-04, M1-ADR-03, M1-02, M1-04, M2-ADR-07, M3-ADR-09, M5-ADR-10

## Contexte

Un test doit pouvoir créer un salon, donner son ID au bot et relire ce que le bot a envoyé,
sans connaître le routeur ni construire un `JsonModel`. Deux exemples du dépôt divergent :
dans `vision.md`, `TextChannel("welcome", out var welcome)` renvoie la guilde ; dans
l'[ADR 0003](0003-state-model-and-concurrency.md), `TextChannel("general")` renvoie le salon.
Il faut aussi distinguer deux questions : « Ce message existe-t-il ? » (état, lisible par
REST) et « Le bot a-t-il tenté ce POST ? » (journal, même en cas d'échec).

Seams **publics** vérifiés dans `netcord`, tag `1.0.0-beta.30` @ `bb5f4e13` :

- `NetCord.Rest.RestClient(IToken token, RestClientConfiguration? configuration = null)`,
  dans `NetCord/Rest/RestClient.cs`. `RestClientConfiguration` a
  `IRestRequestHandler? RequestHandler { get; init; }` et `string? Hostname { get; init; }`,
  **pas de propriété `Token`** (`NetCord/Rest/RestClientConfiguration.cs`).
- `NetCord.BotToken(string token)` parse l'ID du premier segment et fournit `Id` et
  `HttpHeaderValue` (`NetCord/IToken.cs`). Le client appelle `AddDefaultHeader` pour
  `User-Agent` et `Authorization`. `NetCord.Rest.IRestRequestHandler : IDisposable` expose
  `void AddDefaultHeader(string name, IEnumerable<string> values)` et
  `Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)`
  (`NetCord/Rest/IRestRequestHandler.cs`). `RestClient.Dispose()` dispose son handler.
- `NetCord.Gateway.GatewayClient(IEntityToken token, GatewayClientConfiguration? configuration = null)`
  crée son propre client REST. `GatewayClientConfiguration` expose en `init`
  `RestClientConfiguration? RestClientConfiguration`, `IWebSocketConnectionProvider? WebSocketConnectionProvider`,
  `IGatewayCompression? Compression` et `string? Hostname`
  (`NetCord/Gateway/{GatewayClient,GatewayClientConfiguration}.cs`).

Le temps d'un Snowflake se déduit de ses 42 bits hauts (`discord-api-docs` @ `62ab2d50`,
`developers/reference.mdx`). C'est observable : la liste accepte `before`
(`discord-api-spec` @ `65423001`, `specs/openapi.json`) et la suppression multiple refuse
un message de plus de deux semaines (`developers/resources/message.mdx`, erreur `50034`).

## Options

### Option A — Fabrique et seed qui renvoie la ressource créée

`var channel = discord.Seed.Guild("Test").TextChannel("general");` La lecture va du parent
vers l'enfant ; pour créer deux salons frères, on nomme la guilde une fois. Le test conserve
des IDs ou des poignées vers le fake, jamais ses entités. Le client est un vrai `RestClient`.

### Option B — Builder de scénario qui conserve le parent

`var guild = discord.Seed.Guild("Test").TextChannel("general", out var general);`
Les fratries se déclarent en une chaîne. Mais pour envoyer un message, il faut changer de
variable ; ajouter les deux formes pérenniserait deux conventions pour chaque relation.
Un builder différé avec `Build()` ajouterait de plus un instant où l'ID existe sans que le
salon soit lisible par le bot.

## Décision

**Option A.** `Create` crée immédiatement l'état de l'ADR 0003, l'identité du bot et son
application. Chaque fake possède ses réservations d'IDs, ses transports et son journal ;
deux fakes peuvent produire les mêmes IDs sans jamais partager les objets correspondants.
Les dépendances injectées restent la propriété du test. Pas de singleton, de conteneur DI
ni de dépendance à un framework d'assertion.

**Identité et clients.** L'application et l'utilisateur bot partagent **un** ID.
Si le test fournit un token brut de test, `new BotToken(raw)` doit l'accepter et son ID non
nul fait foi ; `ApplicationId`, si renseigné, doit lui être égal. Un ID nul ou un désaccord
est une erreur d'argument de `Create`. Sans token, prendre `ApplicationId` ou allouer un ID
selon l'ADR 0003, puis construire un token synthétique : base64 sans padding des chiffres
ASCII invariants de l'ID, suivi de `.fake.test`. Pas de secret nécessaire, pas de hasard.
L'ID fourni est réservé comme un ID explicite, une seule fois pour le couple bot/application.

Chaque `CreateRestClient()` équivaut, pour le branchement, à
`new RestClient(botToken, new RestClientConfiguration { Hostname = "fake.discord.test", RequestHandler = handler, RateLimitManager = noWaitRateLimitManager })`
— le gestionnaire interne sans attente exigé par l'[ADR 0005](0005-rest-routing-and-pipeline.md),
sans lequel le limiteur global par défaut de NetCord peut dormir en temps réel **avant** l'appel
au handler, hors de l'horloge virtuelle. Le hostname ne coupe **pas** le réseau : c'est le
handler en mémoire, sans repli HTTP, qui le garantit. Le handler **et** le gestionnaire sont
**neufs par client** (`RestClient.Dispose()` libère les deux) ; seuls le routeur, l'état et le
journal sont partagés. `Logger` et `DefaultRequestProperties` sur `CreateRestClient()` restent
des extensions futures possibles, non exposées en M1 pour garder une fabrique sans paramètre.
L'appelant dispose le client, sans fermer le fake ni invalider un autre client. `DisposeAsync`
du fake est idempotent, ferme
son transport et interdit toute nouvelle opération de seed, lecture du journal, création
de client ou requête via un ancien client (`ObjectDisposedException`). Une valeur déjà
détachée reste lisible. Les tests terminent leurs requêtes avant de disposer le fake.

Pas de fabrique Gateway incomplète en M1 : **M3-ADR-09** fixera la signature avec intents,
shards et cycle de vie. Elle devra brancher *aussi* le REST du client Gateway sur ce fake —
donc le handler **et** le gestionnaire sans attente via `RestClientConfiguration` — et
remplacer le WebSocket et la compression ; changer le hostname seul est insuffisant.
M5-ADR-10 réutilisera le même branchement, gestionnaire compris, et la même identité pour
Hosting.

**Seed immédiat.** Un appel réussi vaut un commit dans `FakeDiscordState` (ADR 0003), avec
les mêmes règles de notification qu'une mutation d'état, sans requête REST fictive. `Guild`
crée aussi `@everyone`. Seules la guilde et le salon ont besoin de méthodes enfants : leurs
poignées gardent le fake et un ID, pas une ligne d'état mutable. Les feuilles renvoient un
`ulong` directement utilisable par NetCord. Le membre n'a pas d'ID propre : `Member("alice")`
crée l'utilisatrice et son adhésion atomiquement, `Member(userId)` inscrit une utilisatrice
existante. Les références numériques sont résolues dans **ce** fake ; un nombre seul ne
prouve pas sa provenance. Parent supprimé, référence absente, double adhésion : échec avant
commit (`InvalidOperationException`), sans modification ni notification.

**Fixtures historiques : un ID facultatif, pas deux sources de temps.** Pour toute création
d'entité, `id: null` suit l'allocation normale. Un `id` explicite doit être non nul
(`ArgumentOutOfRangeException` sinon) ; sa milliseconde UTC encodée donne la date de création
de l'entité, sans appel à `Next`, comparaison au dernier ID généré, ni changement de l'horloge
ou du compteur. `joined_at` est l'instant d'adhésion donné par l'horloge, pas la date de
naissance du compte. Pas d'argument `createdAt` indépendant : pour une date historique, le
test compose le Snowflake (exemple ci-dessous). L'ID et le `timestamp` du message ne peuvent
ainsi se contredire. Le seed ne demande pas de créer les fixtures par ordre chronologique.

Vérifier sous le verrou de l'état que chaque nouvel ID, **explicite ou généré**, n'a jamais
été attribué dans ce fake (y compris après suppression). Sur collision, lever
`InvalidOperationException` avant commit ; ne pas écraser, réessayer en boucle ou renvoyer
une erreur REST. Une allocation consommée peut être perdue. Le bot a l'ID de l'application,
le rôle `@everyone` a l'ID de la guilde et la clé d'une adhésion est `(guildId, userId)`.
Aucun de ces cas n'est une seconde attribution libre.

**Journal.** `Recorded.Requests` est une photographie des tentatives arrivées au handler et
terminées, dans l'ordre d'ajout au journal, une entrée par tentative (y compris erreurs HTTP).
Ajouter l'entrée avant de rendre la réponse ou de propager un échec ; si aucune réponse
HTTP n'a été produite, le statut est `null`. Deux appels concurrents n'ont pas d'ordre
prédictible. Copier la projection, y compris les collections imbriquées : aucun flux HTTP,
`JsonDocument` à disposer, objet mutable de l'état, ni liste vivante. Pas de remise à zéro :
le test peut garder une photographie avant l'action. Le journal n'est ni une source d'état
ni une barrière Gateway.

Le template est le chemin de la spec, sans méthode, query ni `/api/v10` ; s'il n'a pas été
résolu, il est `null` et les paramètres de route sont vides. Garder méthode, chemin, paramètres
de route décodés et paires de query (y compris noms répétés). N'enregistrer aucun en-tête
de requête ; masquer les segments et paramètres identifiés comme tokens de route. Les corps
sont les valeurs JSON détachées ; pour un multipart, garder `payload_json`, pas les octets
des fichiers. Un corps absent, non JSON ou illisible donne `null`, sans changer la réponse.
Le littéral JSON `null` reste un `JsonElement` de type `Null`. Le journal local d'un test
n'est pas un export expurgé du contenu applicatif.

## API publique proposée

Signatures du package cœur (corps omis ; constructeurs non publics sauf celui, sans argument,
de `FakeDiscordOptions`). Le point d'extension Strict/Lenient sera une option de
`FakeDiscordOptions` définie par **M1-ADR-05**, pas un paramètre de `CreateRestClient`.

```csharp
using System.Net;
using System.Text.Json;
using NetCord.Rest;

namespace Enx.NetCord.Testing;

/// <summary>Hosts one isolated in-memory Discord for a test.</summary>
public sealed class FakeDiscord : IAsyncDisposable
{
    /// <summary>Creates a fake with the supplied options or deterministic defaults.</summary>
    public static FakeDiscord Create(FakeDiscordOptions? options = null);
    /// <summary>Gets the fixture builder bound to this fake.</summary>
    public DiscordSeed Seed { get; }
    /// <summary>Gets the observations collected by this fake.</summary>
    public DiscordRecording Recorded { get; }
    /// <summary>Creates a caller-owned NetCord client authenticated against this fake.</summary>
    public RestClient CreateRestClient();
    /// <summary>Closes the fake and invalidates its transports and builders.</summary>
    public ValueTask DisposeAsync();
}

/// <summary>Configures an isolated in-memory Discord instance.</summary>
public sealed class FakeDiscordOptions
{
    /// <summary>Gets the clock to use, or null to use the fake's fixed virtual clock.</summary>
    public TimeProvider? TimeProvider { get; init; }
    /// <summary>Gets the ID source, or null to create a deterministic generator for this fake.</summary>
    public ISnowflakeGenerator? SnowflakeGenerator { get; init; }
    /// <summary>Gets the raw test bot token, or null to synthesize one.</summary>
    public string? BotToken { get; init; }
    /// <summary>Gets the bot and application ID, or null to derive it from the token or allocate it.</summary>
    public ulong? ApplicationId { get; init; }
}

/// <summary>Creates top-level fixtures in one fake.</summary>
public sealed class DiscordSeed
{
    /// <summary>Creates a guild and its everyone role; returns the guild handle.</summary>
    public SeededGuild Guild(string name, ulong? id = null);
    /// <summary>Creates a user and returns the user ID.</summary>
    public ulong User(string username, ulong? id = null);
}

/// <summary>Identifies a seeded guild and creates its child fixtures.</summary>
public sealed class SeededGuild
{
    /// <summary>Gets the guild ID.</summary>
    public ulong Id { get; }
    /// <summary>Creates a guild text channel and returns its handle.</summary>
    public SeededTextChannel TextChannel(string name, ulong? id = null);
    /// <summary>Creates a guild role and returns the role ID.</summary>
    public ulong Role(string name, ulong? id = null);
    /// <summary>Creates a user and guild membership together and returns the user ID.</summary>
    public ulong Member(string username, ulong? id = null);
    /// <summary>Adds an existing user to the guild and returns the same user ID.</summary>
    public ulong Member(ulong userId);
}

/// <summary>Identifies a seeded guild text channel and creates message fixtures.</summary>
public sealed class SeededTextChannel
{
    /// <summary>Gets the text channel ID.</summary>
    public ulong Id { get; }
    /// <summary>Creates a message by the specified user and returns the message ID.</summary>
    public ulong Message(string content, ulong authorId, ulong? id = null);
}

/// <summary>Provides read-only observations from one fake.</summary>
public sealed class DiscordRecording
{
    /// <summary>Gets a detached snapshot of completed REST attempts in recording order.</summary>
    public IReadOnlyList<RecordedRestRequest> Requests { get; }
}

/// <summary>Describes one completed attempt at the in-memory REST transport.</summary>
public sealed class RecordedRestRequest
{
    /// <summary>Gets the HTTP method, such as POST.</summary>
    public string Method { get; }
    /// <summary>Gets the request path without the API prefix or query.</summary>
    public string Path { get; }
    /// <summary>Gets the matched Discord path template, or null if none was resolved.</summary>
    public string? RouteTemplate { get; }
    /// <summary>Gets decoded route parameter values by template parameter name.</summary>
    public IReadOnlyDictionary<string, string> RouteParameters { get; }
    /// <summary>Gets decoded query pairs in wire order, preserving repeated names.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Query { get; }
    /// <summary>Gets the detached JSON request or multipart payload, if available.</summary>
    public JsonElement? RequestBody { get; }
    /// <summary>Gets the HTTP response status, or null if no response was produced.</summary>
    public HttpStatusCode? StatusCode { get; }
    /// <summary>Gets the detached JSON response, if available.</summary>
    public JsonElement? ResponseBody { get; }
}
```

Exemples d'usage dans des tests (pseudo-code ; `WorkerOneIds` est le générateur de l'ADR 0003) :

```csharp
async Task SeedMessage_ExplicitSnowflake_PreservesHistory()
{
    var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    await using var discord = FakeDiscord.Create(new FakeDiscordOptions
    {
        ApplicationId = 42, BotToken = "NDI.fake.test", TimeProvider = clock,
        SnowflakeGenerator = new WorkerOneIds()
    });
    var guild = discord.Seed.Guild("Test");
    var channel = guild.TextChannel("general");
    var role = guild.Role("reader");
    var alice = guild.Member("alice");
    var bob = discord.Seed.User("bob");
    Assert.Equal(bob, guild.Member(bob));

    var oldMs = clock.GetUtcNow().AddDays(-15).ToUnixTimeMilliseconds() - 1420070400000L;
    var oldId = ((ulong)oldMs << 22) | 1UL;
    Assert.Equal(oldId, channel.Message("archive", alice, id: oldId));
    using var reader = discord.CreateRestClient();
    Assert.Equal(42UL, (await reader.GetCurrentUserAsync()).Id);
    Assert.Contains(await reader.GetGuildRolesAsync(guild.Id), r => r.Id == role);
    var message = await reader.GetMessageAsync(channel.Id, oldId);
    Assert.Equal(alice, message.Author.Id);
    var body = discord.Recorded.Requests.Last().ResponseBody!.Value;
    Assert.Equal(clock.GetUtcNow().AddDays(-15), body.GetProperty("timestamp").GetDateTimeOffset());
}

async Task SendMessage_TwoClients_ShareStateAndRecording()
{
    await using var discord = FakeDiscord.Create();
    var channel = discord.Seed.Guild("Test").TextChannel("general");
    var before = discord.Recorded.Requests;

    using (var sender = discord.CreateRestClient())
        await sender.SendMessageAsync(channel.Id, new MessageProperties { Content = "hello" });
    var post = discord.Recorded.Requests.Last();
    Assert.Empty(before);
    var channelId = channel.Id.ToString(CultureInfo.InvariantCulture);
    Assert.Equal("POST", post.Method);
    Assert.Equal($"/channels/{channelId}/messages", post.Path);
    Assert.Equal("/channels/{channel_id}/messages", post.RouteTemplate);
    Assert.Equal(channelId, post.RouteParameters["channel_id"]);
    Assert.Empty(post.Query);
    Assert.Equal("hello", post.RequestBody!.Value.GetProperty("content").GetString());
    Assert.Equal(HttpStatusCode.OK, post.StatusCode);
    var sentId = ulong.Parse(post.ResponseBody!.Value.GetProperty("id").GetString()!,
                             CultureInfo.InvariantCulture);
    using var reader = discord.CreateRestClient();
    Assert.Equal("hello", (await reader.GetMessageAsync(channel.Id, sentId)).Content);
}
```

## Conséquences

- M1-02 peut livrer le scénario sans exposer l'état. Le coût du choix est une variable pour
  garder le parent, un petit registre des IDs attribués par fake et des copies à la lecture du
  journal. M1-02 testera la collision dans les deux sens (seed puis allocation, et inversement),
  l'absence de réutilisation après suppression, l'isolation et la disposition indépendante.
- M1-04 s'appuie sur le journal, pas sur des mocks de `RestClient` ; les assertions restent
  hors de cette ADR. M1-ADR-05 fixe routes, erreurs, interception et strictness, M1-ADR-06
  le traitement JSON compatible AOT. Ni code généré par réflexion ni nouveau package requis.
- M2-ADR-07 pourra utiliser les mêmes IDs pour les interactions ; enregistrements de callbacks
  et de frames Gateway seront des observations distinctes, pas des requêtes REST inventées.
- **Parité :** prépare les tests via NetCord de `GET /users/@me`, `POST /channels/{channel_id}/messages`,
  `GET /channels/{channel_id}/messages/{message_id}` et du refus `50034`. Cette dernière clé
  n'existe pas encore dans `docs/parity/errors.md` : **M1-REST-03** l'ajoutera avec sa fabrique
  `DiscordError` et son test de suppression multiple (règle de l'ADR 0005), pas avant.
  Un seed réussi n'est pas une preuve de parité d'un endpoint. Aucune ligne promue ni exclue dans `docs/parity/*` ;
  aucune nouvelle tâche de roadmap.
