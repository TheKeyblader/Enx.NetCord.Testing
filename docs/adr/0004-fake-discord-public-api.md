# 0004 — Façade FakeDiscord, seed, journal et création des clients

- **Statut** : Acceptée
- **Date** : 2026-11-26
- **Auteur** : agent `architect`
- **Tâches liées** : M1-ADR-04, M1-ADR-03, M1-02, M1-04, M2-ADR-07, M3-ADR-09, M5-ADR-10 ;
  amendement 04B : M1-ADR-04B, M1-REST-02 à M1-REST-09, M2-01, M2-07, M2-08, M2-10, M2-11,
  M3-ADR-09, M3-02, M3-03, M3-06 ; proposées : M1-05, M1-REST-10, M2-13, PAR-ERR-02, PAR-GW-01
- **Amendements** : [04B — Adhésion du bot aux guildes seedées](#amendement-04b--adhésion-du-bot-aux-guildes-seedées)
  (**Proposé**, 2026-11-28)

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

> **Amendée par l'[ADR 0008](0008-services-harness.md), § 2 (2026-11-28).** Le harnais Services
> a besoin d'un `GatewayClient` dès M2 : `FakeDiscord.CreateGatewayClient()` existe sans
> paramètre, avec le branchement REST décrit ci-dessus et un WebSocket qui refuse de se
> connecter jusqu'à M3-01. Intents, shards et démarrage restent à M3-ADR-09.

**Seed immédiat.** Un appel réussi vaut un commit dans `FakeDiscordState` (ADR 0003), avec
les mêmes règles de notification qu'une mutation d'état, sans requête REST fictive. `Guild`
crée aussi `@everyone`. Seules la guilde et le salon ont besoin de méthodes enfants : leurs
poignées gardent le fake et un ID, pas une ligne d'état mutable. Les feuilles renvoient un
`ulong` directement utilisable par NetCord. Le membre n'a pas d'ID propre : `Member("alice")`
crée l'utilisatrice et son adhésion atomiquement, `Member(userId)` inscrit une utilisatrice
existante. Les références numériques sont résolues dans **ce** fake ; un nombre seul ne
prouve pas sa provenance. Parent supprimé, référence absente, double adhésion : échec avant
commit (`InvalidOperationException`), sans modification ni notification.

> **Amendement 04B (proposé, 2026-11-28).** `Guild` inscrit aussi le bot du fake, dans le même
> commit, sauf `withBot: false`. Voir [la section en fin de document](#amendement-04b--adhésion-du-bot-aux-guildes-seedées).

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

---

## Amendement 04B — Adhésion du bot aux guildes seedées

- **Statut** : Proposé
- **Date** : 2026-11-28
- **Auteur** : agent `architect`
- **Tâche** : M1-ADR-04B

**Pourquoi une section et pas une ADR.** La décision change une signature de cette ADR
(`DiscordSeed.Guild`) et le lecteur du seed doit la trouver ici. Les numéros 0009 à 0012 sont
par ailleurs réservés par les tâches `M3-ADR-09`, `M5-ADR-10`, `M7-ADR-11` et `M1-ADR-12`. La
section porte son propre statut : le reste de l'ADR 0004 demeure « Acceptée ».

### Contexte

`Seed.Guild("Test")` crée aujourd'hui une guilde et son rôle `@everyone`, sans y inscrire le bot
(`State/FakeDiscordState.cs`, `CreateGuild`). Aucune route de guilde n'existe encore, donc rien
ne l'a montré. Avec M1-REST-06 la question devient visible : le bot peut-il lire une guilde dont
il n'est pas membre ? La même réponse décide de `READY.guilds` (M3-02) et du `PUT` de commandes
de guilde que l'[ADR 0008](0008-services-harness.md), § 3, fait déjà dépendre de cet amendement.

Ce que disent les sources épinglées (`discord-api-docs` @ `62ab2d50`, `discord-api-spec`
@ `65423001`) :

- **Gateway.** « `guilds` are the guilds of which your bot is a member. They start out as
  unavailable » ; puis un `GUILD_CREATE` par guilde (`developers/events/gateway-events.mdx:468,477,736`).
  Un `GUILD_CREATE` est aussi envoyé « when the current user joins a new Guild » (l. 738), et un
  `GUILD_DELETE` « when the user leaves or is removed from a guild » ; `unavailable: true` y
  signale une panne, pas un départ (l. 777-779). `Leave Guild` déclenche ce `GUILD_DELETE`
  (`developers/resources/user.mdx:322-325`).
- **REST.** `GET /users/@me/guilds` rend les guildes « the current user is a member of »
  (`user.mdx:275-278`). `GET /guilds/{guild.id}/preview` exige une guilde *discoverable* si
  l'appelant n'en est pas membre (`developers/resources/guild.mdx:839`).
- **Commandes.** « To create commands in a guild, your app must be authorized with the
  `applications.commands` scope », inclus dans le scope `bot`
  (`developers/interactions/application-commands.mdx:171-175`).
- **Codes.** `10004 Unknown guild` et `50001 Missing access` sont listés
  (`developers/topics/opcodes-and-status-codes.mdx:145,263`), mais **aucune page ne dit quelle
  route rend lequel** pour un bot non membre. La spec ne déclare que `4XX` pour ces opérations
  (`get_guild`, `list_guild_channels`, `get_guild_member`, `list_guild_roles`, `get_channel`,
  `bulk_set_guild_application_commands`). Seule occurrence documentée : un 403 `50001` sur le
  déplacement d'un salon que le bot ne voit pas (`guild.mdx:967`).

Sources non épinglées, consultées le 2026-11-28 :

- [discord-api-docs#5840](https://github.com/discord/discord-api-docs/issues/5840) (janvier
  2023) : `GET /guilds/{id}` d'une guilde dont le bot n'est pas membre rend désormais
  **`10004`**, et plus `50001`. Réponse de l'équipe Discord : « We have always inconsistently
  returned either of those two errors to represent unauthorized access to guild resources. »
- Le 403 `50001` du `PUT /applications/{id}/guilds/{guild_id}/commands` hors d'une guilde où
  l'application est installée est largement rapporté par les utilisateurs de bibliothèques
  (questions publiques de 2021 à 2023, API v9 et v10). Le même 403 `50001` est rapporté pour
  `GET /channels/{id}` et ses messages quand le bot ne voit pas le salon. Ce sont des
  observations de tiers, pas des engagements de Discord.

`fauxcord` @ `70563d51` rattache chaque guilde à **un** bot (`src/db.ts:104`). Ses routes
récentes distinguent guilde inexistante ⇒ 404 `10004` et guilde d'un autre bot ⇒ 403 `50001`
(`src/routes/guild-advanced.ts:202-218`, `stage-instances.ts:22-34`, `soundboard.ts:52-68`) ;
ses routes de base ne vérifient rien (`guilds.ts:36`). Le `PUT` de commandes de guilde n'y
exige que l'existence de la guilde (`application-commands.ts:213-222`). `READY.guilds` y liste
les guildes du bot en `{id, unavailable: true}`, quel que soit l'intent, puis un `GUILD_CREATE`
par guilde si `GUILDS` est demandé (`src/gateway/server.ts:271-301,340-354`).

NetCord, tag `1.0.0-beta.30` @ `bb5f4e13` : `READY` synchronise le cache avec les IDs reçus
(`NetCord/Gateway/GatewayClient.cs:1123-1130`, `SyncGuilds`), `GUILD_CREATE` y ajoute la guilde
(l. 1251-1262), `GUILD_DELETE` l'en retire (l. 1271-1275). Routes concernées :
`GetCurrentUserGuildsAsync` et `LeaveGuildAsync` (`NetCord/Rest/RestClient.User.cs:25,53`).

### Options

**A — Adhésion explicite (état actuel).** Le test écrit `guild.Member(botId)` pour chaque
guilde. Fidèle à la lettre, mais tous les exemples de `vision.md` et des ADR 0007 et 0008
gagnent une ligne, et l'oublier produit des `50001` sans rapport avec ce que le test vérifie.

**B — Adhésion implicite, retrait à la demande.** `Seed.Guild("Test")` inscrit le bot ;
`Seed.Guild("Elsewhere", withBot: false)` ne l'inscrit pas. Le cas courant ne coûte rien, le
cas rare se lit à l'appel.

**C — Pas d'adhésion du bot : il voit tout.** C'est le comportement des routes de base de
fauxcord. Rien à modéliser, mais `50001` et `10004` ne se testent plus, `READY.guilds` liste des
guildes où le bot n'est pas, et la liste des membres ne contient jamais le bot.

### Décision

**Option B.**

#### 1. Seed

- `Seed.Guild(name, id, withBot: true)` crée la guilde, son rôle `@everyone` **et l'adhésion du
  bot** en **un** commit de l'[ADR 0003](0003-state-model-and-concurrency.md) : aucun lecteur ni
  abonné ne voit la guilde sans son bot. Le fait `GuildCreated` porte cette adhésion.
- L'adhésion est celle d'un membre ordinaire : clé `(guildId, botId)`, `joined_at` lu sur
  l'horloge (jamais déduit de l'ID de la guilde), `roles` vide, `@everyone` implicite. Elle
  **n'alloue aucun ID** : `Seed.Guild` consomme les mêmes allocations qu'avant, et les IDs des
  fixtures existantes ne bougent pas. Le bot n'est pas propriétaire de la guilde, et aucun rôle
  géré d'intégration n'est créé (M4-REST-05 le décidera avec le calcul des permissions).
- `withBot: false` donne la guilde d'aujourd'hui. Le seed y reste libre : salons, rôles,
  membres et messages s'y créent sans contrôle d'accès, car le seed n'est pas un appelant REST.
- **Rejoindre plus tard.** Le bot est un utilisateur du fake : `guild.Member(discord.ApplicationId)`
  l'inscrit, avec les règles existantes de `Member(ulong)`. Sur une guilde créée avec le bot,
  ce même appel lève `InvalidOperationException` (double adhésion), comme pour tout membre.
- `FakeDiscord.ApplicationId` expose l'ID que se partagent le bot et son application. Jusqu'ici
  un test ne l'obtenait qu'en le fixant dans les options ou par `GetCurrentUserAsync`.

**Rejeté :** `Seed.GuildWithoutBot(…)` (une méthode de plus pour un booléen) ; `guild.AddBot()`
(double emploi avec `Member(ulong)`) ; créer la guilde avec le bot puis l'en retirer (deux faits,
donc un `GUILD_CREATE` suivi d'un `GUILD_DELETE` sur un client connecté) ; un troisième état
« application installée sans utilisateur bot » (scope `applications.commands` seul). Ce dernier
est réel, surtout pour un bot HTTP, mais il demande de modéliser les installations ; il est
renvoyé au § 4.

#### 2. Visibilité REST

Une guilde dont le bot n'est pas membre n'est pas une guilde absente de l'état : elle existe,
le seed y écrit, et le bot ne la voit pas. La règle tient en deux profils, choisis par la forme
de la route et non par le handler :

| Famille de routes | La guilde n'existe pas | Elle existe, le bot n'en est pas membre | Source |
| --- | --- | --- | --- |
| La guilde elle-même : `/guilds/{guild_id}`, `/guilds/{guild_id}/preview` | 404 `10004` | **404 `10004`** | `GET` : discord-api-docs#5840 ; autres méthodes et `preview` (pas de guilde *discoverable* dans le fake) : hypothèse |
| Sous-ressources : `/guilds/{guild_id}/…` (salons, membres, rôles, bans…) | 404 `10004` | **403 `50001`** | observé, non documenté ; même partage que fauxcord (`guild-advanced.ts:202-218`) |
| Salon de guilde : `/channels/{channel_id}` et tout ce qui en dépend (messages, réactions, pins, typing) | salon inexistant : 404 `10003` | **403 `50001`** | observé ; fauxcord `soundboard.ts:52-68` |
| Commandes de guilde : `/applications/{application_id}/guilds/{guild_id}/commands…` | 404 `10004` | **403 `50001`** | précondition documentée : scope `applications.commands` (`application-commands.mdx:171`), assimilé en v1 à l'adhésion du bot (§ 4, Conséquences) ; code observé ; guilde inexistante : fauxcord, hypothèse |
| `GET /users/@me/guilds` | — | la guilde n'est pas listée | `user.mdx:275-278` |
| `DELETE /users/@me/guilds/{guild_id}` | 404 `10004` | 404 `10004` | hypothèse |
| Routes à jeton : callback d'interaction, `/webhooks/{webhook_id}/{webhook_token}…` | — | **aucun contrôle d'adhésion** | le jeton fait foi ([ADR 0007](0007-interaction-model.md)) |
| `GET /users/{user_id}`, salons de DM | — | aucun contrôle d'adhésion | un utilisateur n'appartient pas à une guilde |

- **Profil « masquée »** (la guilde elle-même) : le bot ne peut pas distinguer une guilde où il
  n'est pas d'une guilde qui n'existe pas. **Profil « refusée »** (tout le reste) : la ressource
  existe, l'accès manque.
- **Helper central** (nom indicatif `GuildAccess`, dans `State/`). Toute opération d'état à
  portée de guilde ou de salon commence par lui **sous le verrou** : il résout la guilde, ou le
  salon puis sa guilde, applique le profil et rend l'entité ou un échec `UnknownGuild`,
  `UnknownChannel` ou `MissingAccess` (nouveau `StateFailureKind`). La traduction en
  `DiscordError` est faite une fois pour toutes les routes : fabriques
  `DiscordError.UnknownGuild()` (404, `10004`) et `DiscordError.MissingAccess()` (403, `50001`),
  à ajouter au catalogue avec leurs lignes de `errors.md`. Un handler ne teste jamais l'adhésion
  du bot lui-même.
- **Seed ou REST : un argument d'appelant, obligatoire.** Les opérations d'état restent
  **communes** au seed et au REST ; chacune reçoit un appelant interne (nom indicatif
  `StateCaller`) qu'elle transmet au helper. Deux valeurs : `Seed`, sans contrôle d'accès, pour
  le seed et la création d'une interaction (geste d'un utilisateur, contrôlé par le § 4) ; `Bot`,
  qui applique le tableau, pour tout handler REST authentifié par le jeton du bot. Le paramètre
  n'a **pas de valeur par défaut** : un handler qui l'oublie ne compile pas, au lieu de
  contourner le contrôle en silence. Aucun fichier de `Rest/` ne passe `Seed`. Une identité
  `Bearer` (ADR 0005) ajoutera une valeur sans changer les signatures.
  *Rejeté :* des opérations d'état propres au REST. Elles doubleraient chaque mutation, avec
  deux chemins à garder alignés pour un même fait (ADR 0003) ; et un appelant facultatif, que
  le seed ne passerait pas, car l'oubli et l'intention s'y écrivent de la même façon.
- **Ordre**, dans l'étape 4 de l'[ADR 0005](0005-rest-routing-and-pipeline.md) : accès à la
  guilde ou au salon (cette règle), puis existence de la ressource visée (`10007`, `10011`,
  `10008`), puis permissions (`50013`, M4-REST-05), puis règles métier. Un message inexistant
  dans un salon que le bot ne voit pas rend donc `50001`, pas `10008`. La validation
  structurelle reste avant, à l'étape 3 : un corps mal formé rend `50035` même hors de la guilde.
  Cet ordre est celui du pipeline de l'ADR 0005, pas un comportement confirmé de Discord
  (hypothèse, `PAR-ERR-02`).
- **`50001` et `50013`.** `50001` dit « le bot ne voit pas cette ressource » ; `50013` dit « il la
  voit, mais n'a pas la permission ». M4-REST-05 réutilisera `MissingAccess` pour un salon dont
  `VIEW_CHANNEL` est refusé au bot, par le même helper.
- **Interception.** Une fois dans le catalogue, les couples 403/`50001` et 404/`10004` sont
  acceptés par `RestEffect.Error` (ADR 0005), pour simuler ces refus sans seed particulier.

Chaque ligne « hypothèse » ou « observé » est à confirmer contre Discord (`PAR-ERR-02`,
ci-dessous). Corriger une ligne revient à changer le profil d'une famille dans le helper, sans
toucher aux handlers.

#### 3. Gateway

- `READY.guilds` contient un objet `{ "id": …, "unavailable": true }` par guilde **dont le bot
  est membre** à l'instant de la projection de session, quels que soient les intents, filtré par
  shard (M3-06). Avec l'intent `GUILDS`, un `GUILD_CREATE` complet suit pour chacune (M3-02) ;
  le bot figure dans ses `members` : même réduite, cette liste « will only contain your bot and
  users in voice channels » (`gateway-events.mdx:768`).
- Une guilde sans le bot n'apparaît ni dans `READY` ni dans un `GUILD_CREATE`, et **aucun** de
  ses faits (messages, membres, rôles, salons) n'est dispatché : M3-03 filtre par adhésion
  avant de filtrer par intents.
- **Le bot rejoint** (`guild.Member(discord.ApplicationId)`) : le fait d'adhésion du bot se
  traduit en `GUILD_CREATE` disponible et complet (`gateway-events.mdx:738`). Le fake n'y
  ajoute pas de `GUILD_MEMBER_ADD` pour le bot lui-même : **hypothèse**, aucune source ne dit si
  Discord en envoie un (`PAR-GW-01`).
- **Le bot part ou est retiré** (`LeaveGuildAsync`, expulsion, bannissement) : `GUILD_DELETE`
  avec le seul `id`, sans `unavailable` : **hypothèse**, lue dans `gateway-events.mdx:777-779`
  (`PAR-GW-01`).
  Les faits suivants de cette guilde ne sont plus dispatchés ; le REST suit aussitôt le § 2.
- **Renvoyé à M3-ADR-09 :** la forme des faits d'arrivée et de départ du bot (un `GUILD_CREATE`
  tardif a besoin de la projection complète de la guilde, prise au commit), la barrière de
  livraison, les events annexes, et la commande de test qui retire le bot d'une guilde : le seed
  ne supprime rien aujourd'hui.

#### 4. Interactions

- Une requête de l'ADR 0007 dont le salon appartient à une guilde **sans le bot** est refusée à
  l'exécution par `InvalidOperationException`, avec les autres contrôles d'état de son § 1. Le
  seul payload que l'ADR 0007 sait produire en guilde annonce une installation de guilde
  (`authorizing_integration_owners` = `{"0": guild_id}`, `guild`, `app_permissions` du bot) :
  il serait faux ici.
- Discord livre pourtant des interactions dans ces guildes, dans deux cas que le fake ne
  modélise pas encore : l'application installée **par l'utilisateur** (clé `"1"`, limite de cinq
  followups, `receiving-and-responding.mdx:78-90,474`) et l'application installée dans la guilde
  **sans utilisateur bot** (`application-commands.mdx:171-175`). Les deux demandent un modèle
  d'installation (`integration_types` et `contexts` des commandes, donc le registre de M2-07).
  Ils sont renvoyés à une tâche dédiée, `M2-13`, proposée ci-dessous.
- Le harnais de l'ADR 0008 n'a rien à ajouter : `RegisterCommandsInGuild(guildId)` sur une guilde
  sans le bot fait échouer `BuildAsync` par la `RestException` 403 `50001` du § 2.
- Les routes à jeton ne contrôlent pas l'adhésion (§ 2) : rien ne change pour les réponses et
  les followups.

### API publique proposée

Deux ajouts. `Guild` garde ses deux premiers paramètres : le changement est compatible source ;
la rupture binaire (la surcharge à deux paramètres disparaît) est sans effet avant la première
publication (M6-05).

```csharp
// DiscordSeed — replaces Guild(string name, ulong? id = null)
/// <summary>Creates a guild, its everyone role and, by default, the bot's membership; returns the guild handle.</summary>
/// <param name="name">The guild name.</param>
/// <param name="id">An explicit nonzero ID, or <see langword="null"/> to allocate one.</param>
/// <param name="withBot">
/// <see langword="true"/> to make the fake's bot a member of the guild in the same commit;
/// <see langword="false"/> to create a guild the bot cannot see until it is added with
/// <see cref="SeededGuild.Member(ulong)"/>.
/// </param>
public SeededGuild Guild(string name, ulong? id = null, bool withBot = true);

// FakeDiscord
/// <summary>Gets the ID shared by the bot user and its application.</summary>
/// <remarks>Remains readable after the fake has been disposed.</remarks>
public ulong ApplicationId { get; }
```

Exemples d'usage dans des tests (pseudo-code) :

```csharp
async Task SeedGuild_Default_BotIsMember()
{
    await using var discord = FakeDiscord.Create();
    var guild = discord.Seed.Guild("Test");
    using var rest = discord.CreateRestClient();

    var bot = await rest.GetGuildUserAsync(guild.Id, discord.ApplicationId);
    Assert.Equal(discord.ApplicationId, bot.Id);
    Assert.Contains(await rest.GetCurrentUserGuildsAsync().ToListAsync(), g => g.Id == guild.Id);
    Assert.Throws<InvalidOperationException>(() => guild.Member(discord.ApplicationId));
}

async Task GuildRoutes_GuildWithoutBot_AreHiddenOrForbidden()
{
    await using var discord = FakeDiscord.Create();
    var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
    var channel = elsewhere.TextChannel("general");   // the seed is not subject to access checks
    using var rest = discord.CreateRestClient();

    var hidden = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildAsync(elsewhere.Id));
    Assert.Equal((HttpStatusCode.NotFound, 10004), (hidden.StatusCode, hidden.Error!.Code));

    var roles = await Assert.ThrowsAsync<RestException>(() => rest.GetGuildRolesAsync(elsewhere.Id));
    Assert.Equal((HttpStatusCode.Forbidden, 50001), (roles.StatusCode, roles.Error!.Code));

    var send = await Assert.ThrowsAsync<RestException>(
        () => rest.SendMessageAsync(channel.Id, new MessageProperties { Content = "hello" }));
    Assert.Equal(50001, send.Error!.Code);

    var commands = await Assert.ThrowsAsync<RestException>(
        () => rest.BulkOverwriteGuildApplicationCommandsAsync(discord.ApplicationId, elsewhere.Id, []));
    Assert.Equal(50001, commands.Error!.Code);

    Assert.DoesNotContain(await rest.GetCurrentUserGuildsAsync().ToListAsync(), g => g.Id == elsewhere.Id);
}

async Task SeedMember_BotJoinsLater_GuildBecomesVisible()
{
    await using var discord = FakeDiscord.Create();
    var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
    using var rest = discord.CreateRestClient();

    Assert.Equal(discord.ApplicationId, elsewhere.Member(discord.ApplicationId));

    Assert.Equal("Elsewhere", (await rest.GetGuildAsync(elsewhere.Id)).Name);
    // With a connected client (M3), the same call dispatches a full GUILD_CREATE.
}

async Task SlashCommand_GuildWithoutBot_IsRejectedLocally()
{
    await using var discord = FakeDiscord.Create();
    var elsewhere = discord.Seed.Guild("Elsewhere", withBot: false);
    var channel = elsewhere.TextChannel("general");
    var carol = elsewhere.Member("carol");

    await Assert.ThrowsAsync<InvalidOperationException>(() =>
        discord.Interactions.SlashCommand("ping", carol, channel.Id).ExecuteAsync(_ => Task.CompletedTask));
}
```

### Conséquences

- Les exemples de `vision.md` et des ADR 0003, 0004, 0007 et 0008 restent valables tels quels :
  la guilde seedée contient le bot, et `RegisterCommandsInGuild` y réussit.
- **La liste des membres compte le bot.** `GET /guilds/{guild_id}/members`, `member_count` et le
  `GUILD_CREATE` rendent un membre de plus que ceux que le test a seedés. C'est ce que fait
  Discord (`gateway-events.mdx:768`) ; un test qui compte les membres doit en tenir compte.
  Trois tests existants passent par `discord.Seed.Guild` et sont à ajuster avec M1-05 :
  `tests/Enx.NetCord.Testing.Tests/Facade/DiscordSeedTests.cs:202, 214, 364` attendent 1, 0 et
  500 membres et en verront 2, 1 et 501.
- **Au niveau de l'état, l'adhésion est une donnée de l'input, fausse par défaut.**
  `CreateGuildInput` gagne `WithBot = false` ; `DiscordSeed.Guild` y passe son `withBot`, seul
  endroit où le défaut vaut `true`. `WithBot: true` sans identité de bot lève
  `InvalidOperationException` avant tout commit, comme un second `CreateBotIdentity` : par l'API
  publique le cas n'arrive pas, `FakeDiscord.Create` créant l'identité avant tout seed. Les
  tests qui créent un état nu (`StateChangeFlowTests.cs:141`, `FakeDiscordStateTests.cs`,
  `StateTestSupport.cs:30`) restent donc valables sans modification.
  *Rejeté :* ignorer l'adhésion quand l'identité manque (l'état tairait une demande non
  satisfaite) ; la refuser toujours sans identité, ou la rendre vraie par défaut dans l'input
  (tous les tests d'état nu devraient créer un bot qui n'est pas leur sujet).
- Le paramètre d'appelant obligatoire du § 2 touche la signature des opérations d'état à portée
  de guilde ou de salon, donc leurs appels dans les tests de `State/` : coût mécanique de M1-05.
- Un contrôle de plus par opération de guilde ou de salon, sous le verrou, et un seul endroit où
  le corriger. Coût : la plupart des lignes du tableau reposent sur l'observation ou sur une
  hypothèse, faute de documentation.
- Limite assumée en v1 : deux états seulement (bot membre ou non). « Application installée sans
  bot » et « installée par l'utilisateur » ne sont pas représentables ; un bot HTTP sans
  utilisateur bot se teste avec la guilde par défaut, ou en injectant le refus par
  `RestEffect.Error(HttpStatusCode.Forbidden, 50001)`.
- `withBot` reste juste si un modèle d'installation arrive : il décrit l'adhésion de
  l'utilisateur bot, pas l'installation de l'application.
- Ni dépendance, ni réflexion, ni changement de l'allocation des IDs.

**Tâches impactées.**

- **M1-REST-02** (et, par le salon, **M1-REST-03 à 05**) : résolution du salon par le helper ;
  M1-REST-02 devrait dépendre de cet amendement, comme M1-REST-06.
- **M1-REST-06, 07, 08, 09** : résolution de la guilde par le helper, profil « masquée » pour
  `/guilds/{guild_id}` et `preview`, « refusée » ailleurs. Chaque famille teste le cas « bot non
  membre » en plus du cas « guilde inconnue ».
- **M2-07** : le registre de guilde suit la ligne « commandes de guilde » ; `GET`, `POST`,
  `PUT`, `PATCH`, `DELETE` et permissions rendent 403 `50001` hors d'une guilde du bot.
- **M2-01 / M2-11** : contrôle d'état supplémentaire du § 4. **M2-08** : test
  `RegisterCommandsInGuild` avec et sans le bot, déjà prévu par l'ADR 0008.
- **M2-10** : la projection `GUILD_CREATE` partagée contient le membre bot.
- **M3-ADR-09, M3-02, M3-03, M3-06** : § 3.

**Tâches à créer** (l'orchestrateur les ajoute à `docs/roadmap.md`) :

- `M1-05` — Adhésion du bot : `Seed.Guild(…, withBot)`, adhésion dans le commit de création et
  dans `GuildCreated`, `FakeDiscord.ApplicationId`, helper `GuildAccess`,
  `StateFailureKind.MissingAccess`, `DiscordError.UnknownGuild()` et `MissingAccess()` avec
  leurs lignes de `errors.md`, couples acceptés par `RestEffect.Error` — rest-implementer —
  M1-ADR-04B, M1-02, M1-04 (`FakeDiscord.cs`, fusionné). Porte aussi `CreateGuildInput.WithBot`,
  l'appelant `StateCaller` et l'ajustement des trois tests cités. À placer avant M1-REST-02,
  M1-REST-06 et M2-07, et avant **M2-01** et **M2-11**, qui en ont besoin pour le contrôle du § 4.
- `M1-REST-10` — `GET /users/@me/guilds` et `DELETE /users/@me/guilds/{guild_id}`, absentes de
  M1-REST-01 ; le départ produit le fait que M3-03 traduira en `GUILD_DELETE` —
  rest-implementer — M1-05, M1-REST-06.
- `M2-13` — Interactions hors installation de guilde : installation par l'utilisateur
  (`authorizing_integration_owners` `{"1": user_id}`, `context`, `app_permissions`, limite de
  cinq followups) et application installée sans utilisateur bot ; amendement de l'ADR 0007 puis
  implémentation — architect, puis interactions-implementer — M2-07, M2-11.
- `PAR-ERR-02` — Confirmer contre Discord les lignes du tableau du § 2 marquées « hypothèse » ou
  « observé » : sous-ressources de guilde et salons (`50001` ou `10004`), `PATCH` et `preview`
  de la guilde, commandes d'une guilde inexistante (`10004` ici, peut-être `50001`), départ
  d'une guilde dont le bot n'est pas membre ; et l'ordre des refus, `50035` avant `50001` ou
  `10004` pour un corps mal formé hors de la guilde — parity-auditor — M1-REST-06.
- `PAR-GW-01` — Confirmer contre Discord les deux hypothèses du § 3 : pas de `GUILD_MEMBER_ADD`
  pour le bot quand il rejoint une guilde, et absence du champ `unavailable` dans le
  `GUILD_DELETE` d'un départ — parity-auditor — M3-02.

**Parité.** Prépare, sans les promouvoir :

- `errors.md` : `10004`, `50001` (contexte à élargir : guilde ou salon que le bot ne voit pas,
  commandes de guilde), `http:403` (note « `50001` pas encore dans le catalogue »), `http:404`.
- `rest.md` : `GET`/`PATCH /guilds/{guild_id}`, `GET /guilds/{guild_id}/preview`, les lignes
  `/guilds/{guild_id}/channels`, `/members…`, `/roles…`, `/bans…`, `POST /guilds/{guild_id}/members-search`
  (hors spec), `/channels/{channel_id}` et ses messages, les neuf lignes
  `/applications/{application_id}/guilds/{guild_id}/commands…`, `GET /users/@me/guilds`,
  `DELETE /users/@me/guilds/{guild_id}`.
- `gateway.md` : `ready-guild-create`, `READY`, `GUILD_CREATE`, `GUILD_DELETE`.
- `interactions.md` : `field:guild-context`, `field:authorizing_integration_owners` (reste 🟡
  tant que `M2-13` n'est pas livrée).

Aucune ligne promue ni exclue, aucune matrice modifiée par cet amendement.
