# 0003 — Modèle d'état et concurrence

- **Statut** : Acceptée
- **Date** : 2026-11-26
- **Auteur** : agent `architect`
- **Tâches liées** : M1-ADR-03, M1-01, M1-ADR-04, M1-ADR-05, M1-ADR-06, M3-ADR-09, M3-03, M3-05

## Contexte

Un `POST` de message doit produire un seul message : le `GET` suivant le retrouve et le
dispatch `MESSAGE_CREATE` décrit **la création**, même si le bot a déjà édité ou supprimé le
message quand la Gateway traite le dispatch. Seed, REST et interactions doivent donc écrire
dans le même état. Le cache NetCord et le journal des requêtes ne sont pas cet état.

Points de jonction publics vérifiés dans `netcord`, tag `1.0.0-beta.30` = `main` @ `bb5f4e13` :

- REST : `NetCord.Rest.RestClientConfiguration.RequestHandler` (`IRestRequestHandler?`, `init`) ;
  `Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)`
  (`NetCord/Rest/{RestClientConfiguration,IRestRequestHandler}.cs`). Le handler reçoit une requête, pas des entités NetCord à stocker.
- Gateway : `NetCord.Gateway.GatewayClientConfiguration.WebSocketConnectionProvider`
  (`IWebSocketConnectionProvider?`, `init`) ; `NetCord.Gateway.WebSockets.IWebSocketConnectionProvider.CreateConnection()`
  renvoie `IWebSocketConnection` (`NetCord/Gateway/{GatewayClientConfiguration,WebSockets/IWebSocketConnectionProvider}.cs`).
  Le vrai client traite `MESSAGE_CREATE` dans `NetCord/Gateway/GatewayClient.cs`.
- Interactions : `public static Interaction CreateFromJson(JsonModels.JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client)`
  sur `NetCord.Interaction` (`NetCord/Interaction.cs`). Les `NetCord.JsonModels` sont des objets de
  transport mutables (`NetCord/JsonModels/{JsonEntity,JsonMessage}.cs`), pas des instantanés.
  En particulier `JsonMessage` ne porte pas `timestamp` comme propriété active ; le fake doit
  néanmoins produire ce champ de `MessageResponse` (spec `65423001`, `specs/openapi.json`).

## Options

### Option A — Dictionnaires mutables et verrou unique par fake

`lock (_gate) { … }` protège l'opération métier entière (ex. guilde **et** rôle `@everyone`).
Une lecture copie seulement les valeurs demandées. Pas de boucle de retry, de tâche de fond
obligatoire ni de version complète à conserver. Deux lecteurs ne travaillent pas simultanément
dans l'état ; une grande liste peut donc retarder un écrivain.

### Option B — Racine immuable et remplacement atomique

`ImmutableInterlocked.Update(ref root, …)` rend les lectures et la capture d'une version peu
coûteuses. Mais les écritures modifient plusieurs index persistants ; sur contention, la
transformation peut être répétée. Il faut sortir génération d'IDs et publication de cette
transformation, puis garantir leur ordre. Garder les racines pour RESUME retiendrait aussi des
entités sans rapport avec les dispatchs de la session.

### Option C — Acteur propriétaire de l'état

Un `Channel<Command>` à lecteur unique sérialise les opérations sans verrou métier. En
contrepartie, même un `GET` ou un seed doit attendre son tour par message/réponse ; annulation,
arrêt et fautes du lecteur deviennent des cas de test du noyau. Comme le verrou, la file ne
détermine pas lequel de deux producteurs concurrents arrive en premier.

## Décision

**Option A**, avec un canal de **notification** interne, pas un acteur. Pour la taille d'un
scénario de bot, copier le message concerné vaut mieux que versionner tout le serveur. Aucune
des trois options n'exige de réflexion ; la première a le moins de mécanismes à maintenir.

**Unité atomique.** Une instance de `FakeDiscordState` par `FakeDiscord`, un verrou synchrone
privé pour toutes ses lectures et écritures. Sous verrou : vérifier les références, préparer
les effets, appliquer la mutation entière, attribuer un numéro de changement croissant et
mettre ses notifications en file dans cet ordre. Un lot (ex. suppression multiple) est validé
avant toute écriture. Échec de validation ou annulation avant commit : ni écriture visible
ni notification ; après commit, l'annulation ne retire pas l'effet. Un ID réservé peut rester
inutilisé. Pas d'`await`, de sérialisation, d'I/O, de handler utilisateur ni de dispatch dans
la section critique. Les seules dépendances injectées consultées sous verrou sont l'horloge
et le générateur, synchrones, non bloquants et sans réentrée dans le fake.

**Entités M1.** Classes internes mutables dans `State/` ; dictionnaires typés par `ulong`
pour utilisateurs, applications, guildes, salons, rôles et messages ; membres par
`(guildId, userId)` (pas d'ID de membre inventé). Les relations sont des IDs, pas des copies :
un salon de guilde référence sa guilde, un message référence son salon et son auteur. On
crée les parents avant leurs dépendants : identité du bot et application, guilde avec son
rôle `@everyone` (même ID, même commit), puis salons/rôles/membres, enfin messages ; un DM
n'exige pas de guilde. Conserver un ordinal interne de création pour départager les créations
au même instant ; appliquer l'ordre demandé par chaque route, jamais l'ordre d'énumération
d'un dictionnaire. Aucune référence mutable, même imbriquée, ne sort du verrou : résultat
de lecture, de mutation ou de seed = valeur détachée ou ID.

**Flux des changements.** Un `StateChange` est un fait métier interne, typé par un record
dérivé `sealed`, avec numéro global et données immuables prises au commit (y compris les
données liées nécessaires au dispatch). Pas de couple `enum`/`object`, de cast dynamique ni
de sérialisation polymorphe de cette hiérarchie. Une mutation effective produit un ou plusieurs
faits ordonnés ; un `GET` ou une opération sans changement n'en produit pas. Un fait peut ne
correspondre à **aucun** event Discord. M3 traduit explicitement les faits, par intents et
shard, sans relire un message devenu différent.

Le serveur Gateway est l'unique abonné interne : en son absence, pas de file ni d'historique
accumulé. À l'abonnement, installer sous verrou un `Channel<StateChange>` FIFO non borné ;
écrire par `TryWrite` sous le même verrou, sans continuation synchrone. Un lecteur unique
redistribue les changements aux sessions **hors** verrou (plusieurs lecteurs d'un canal se
partageraient les événements au lieu de les diffuser). Désabonnement et fermeture du writer
sont sérialisés avec les commits. Pour une nouvelle session, prendre la projection initiale
et son dernier numéro sous le même verrou ; la Gateway doit garder les faits postérieurs à
cette borne pendant l'initialisation, sans rediffuser les faits déjà représentés.

Le numéro global n'est **pas** le `s` de Gateway. Chaque session conserve pour RESUME ses
propres dispatchs déjà filtrés et numérotés, y compris durant une déconnexion résumable.
Elle rejoue ces payloads dans l'ordre après le dernier `s` reçu, et non des vues de l'état
courant (`discord-api-docs` @ `62ab2d50`, `developers/events/gateway.mdx`). Aucun instantané
complet du serveur par event. Une réponse REST réussie garantit le commit, pas l'exécution
du handler Gateway du bot ; les barrières de livraison relèvent de M3-ADR-09.

**Temps et IDs.** Un `TimeProvider` par fake ; par défaut, temps virtuel fixé au
`2020-01-01T00:00:00Z`, sans avance murale. Le test injecte une horloge contrôlable pour
avancer temps et timers. Jamais de `DateTime.Now`, `UtcNow` statique ni de délai réel ; tout
timer passe par ce fournisseur. En l'absence d'instant explicite, lire `GetUtcNow()` une fois
par mutation et utiliser cet instant UTC pour les créations (`CreatedAt` interne), l'adhésion
(`joined_at`), les éditions (`edited_timestamp`) et l'allocation des IDs. N'émettre que les champs Discord prévus, ex.
`timestamp` et `edited_timestamp` (initialement `null`) pour le message, pas de champ JSON
universel `created_timestamp` (`developers/resources/message.mdx`).

Le générateur par défaut est privé, neuf par fake. Il calcule `id = (msDepuis2015 << 22) | compteur`
en `ulong`. Les 42 bits de poids fort codent les millisecondes depuis `2015-01-01T00:00:00Z`.
Les 22 bits du compteur occupent les champs Discord worker (5), process (5) et increment (12),
du poids fort au poids faible (`developers/reference.mdx`). Ces champs ne désignent pas des
processus réels du fake : après 4 095, la retenue passe dans le champ process sans avancer
l'horloge. À chaque nouvelle milliseconde, repartir de 0 (de 1 à l'epoch pour exclure l'ID
zéro). Cela permet 4 194 304 allocations à l'instant fixe par défaut, par exemple pour semer
5 000 membres sans changer leur date.

**Contrat d'allocation.** Les IDs que le générateur alloue doivent être non nuls, strictement
croissants par fake et encoder la milliseconde UTC demandée. La réutilisation structurelle
d'un ID (`@everyone`) n'est pas une allocation. Si la milliseconde demandée sort de la plage
des 42 bits ou recule, si le compteur est épuisé ou si un générateur injecté renvoie un ID
qui viole ce contrat, l'allocation lève `InvalidOperationException` avant commit. Pas
d'attente, de passage artificiel à la milliseconde suivante ni de réponse Discord `50035`.

Ces vérifications portent sur les **allocations**, pas sur toute insertion dans l'état :
un ID ou un instant fourni explicitement au seed ne doit pas être comparé au dernier ID
alloué ni à l'horloge courante par cette règle. M1-ADR-04 décidera si le seed accepte ces
valeurs explicites et, si oui, comment les valider et détecter les collisions. Ce choix
laisse possible un message vieux de 15 jours pour tester le bulk-delete (`50034`) ou la
pagination `before`, sans exiger que toutes les fixtures soient créées par ordre d'âge.

Les Snowflakes sont des `ulong` en mémoire et des chaînes décimales invariantes en JSON
(`SnowflakeType` dans `specs/openapi.json`). Le verrou assure un ordre total, pas un vainqueur
reproductible entre appels réellement concurrents ; un test qui dépend de leur ordre doit les séquencer.

## API publique proposée

Seuls les deux points d'injection sont publics ; aucun accesseur à l'état mutable ni bus
d'events public. Voici la partie des options fixée ici ; la façade et le reste des options
relèvent de M1-ADR-04.

```csharp
namespace Enx.NetCord.Testing;

/// <summary>Allocates Discord snowflakes for one fake, in strictly increasing order.</summary>
/// <remarks>
/// Each fake calls Next synchronously under its own lock, one call at a time.
/// Do not block or reenter the fake. An instance shared by multiple fakes must be thread-safe.
/// </remarks>
public interface ISnowflakeGenerator
{
    /// <summary>Returns a unique, nonzero snowflake encoding the supplied UTC millisecond.</summary>
    ulong Next(DateTimeOffset timestamp);
}

/// <summary>Configures an isolated in-memory Discord instance.</summary>
public sealed class FakeDiscordOptions
{
    /// <summary>Gets the clock to use, or null to use the fake's fixed virtual clock.</summary>
    public TimeProvider? TimeProvider { get; init; }

    /// <summary>Gets the ID source, or null to create a deterministic generator for this fake.</summary>
    public ISnowflakeGenerator? SnowflakeGenerator { get; init; }
}
```

Exemple d'usage dans un test (pseudo-code). `WorkerOneIds` est propre à ce test : il
n'est pas thread-safe et suppose un seul instant et moins de 4 096 allocations. La façade
et le chaînage du seed sont indicatifs : M1-ADR-04 tranchera si `TextChannel` renvoie le
salon ou la guilde avec un paramètre `out`.

```csharp
sealed class WorkerOneIds : ISnowflakeGenerator
{
    private uint _increment;
    public ulong Next(DateTimeOffset timestamp) =>
        ((ulong)(timestamp.ToUnixTimeMilliseconds() - 1420070400000L) << 22)
        | (1UL << 17) | _increment++;
}

async Task CreateMessage_UsesInjectedClockAndWorker()
{
    var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
    await using var discord = FakeDiscord.Create(new FakeDiscordOptions
    {
        TimeProvider = clock, SnowflakeGenerator = new WorkerOneIds()
    });
    var channel = discord.Seed.Guild("Test").TextChannel("general");
    using var rest = discord.CreateRestClient();
    var sent = await rest.SendMessageAsync(channel.Id, new MessageProperties { Content = "hello" });
    var read = await rest.GetMessageAsync(channel.Id, sent.Id);
    Assert.Equal(sent.Id, read.Id);
    Assert.Equal(1UL, (read.Id >> 17) & 31UL);
    Assert.Equal(clock.GetUtcNow().ToUnixTimeMilliseconds(), (long)(read.Id >> 22) + 1420070400000L);
}
```

Squelette **interne** illustratif (signatures seulement, exemple du message) :

```csharp
internal abstract record StateChange(long Number);
internal sealed record MessageCreated(long Number, MessageSnapshot Message) : StateChange(Number);
internal sealed record MessageDeleted(long Number, ulong? GuildId, ulong ChannelId, ulong MessageId)
    : StateChange(Number);

internal sealed class FakeDiscordState
{
    internal FakeDiscordState(TimeProvider clock, ISnowflakeGenerator ids);
    internal MessageSnapshot CreateMessage(CreateMessageInput input);
    internal MessageSnapshot? FindMessage(ulong channelId, ulong messageId);
    internal ChangeSubscription SubscribeGateway();
}
```

`MessageSnapshot` représente des valeurs détachées ; `ChangeSubscription` possède le
`ChannelReader<StateChange>` et le désabonnement. Les handlers REST traduisent les résultats
en réponses Discord ; l'état ne dépend ni de `HttpResponseMessage`, ni d'un `GatewayClient`.

## Conséquences

- Un test peut vérifier l'état par le vrai `RestClient`. Les assertions futures peuvent
  consommer des projections en lecture seule sans figer les sept classes d'entités en API.
- M1-01 vérifie le contrat du générateur au point d'allocation, pas dans une garde commune
  à toutes les insertions. M1-ADR-04 garde la décision sur les IDs et instants explicites du
  seed, y compris les collisions entre un ID fourni et un ID alloué.
- Coût assumé : contention sur les grandes lectures et mémoire proportionnelle aux changements
  en attente si la Gateway est lente. M1-01 teste notamment l'atomicité guilde/rôle, l'isolation
  entre fakes, les bornes des IDs et l'absence de fuite de données mutables ; il choisit les
  index secondaires et les copies nécessaires, pas un nouveau modèle de concurrence.
- M1-ADR-05/06 fixent la traduction erreurs/JSON. M3-03 devra prouver qu'une création suivie
  d'une édition ne modifie pas le premier dispatch ; M3-ADR-09/M3-05 précisent la barrière
  d'initialisation, la rétention par session et l'échec de RESUME quand l'historique manque.
- **Parité :** prépare `POST /channels/{channel_id}/messages`, `GET /channels/{channel_id}/messages/{message_id}`
  et `MESSAGE_CREATE`, sans les implémenter. Aucune ligne de `docs/parity/*` promue ni exclue ;
  les preuves devront passer par NetCord. Aucune nouvelle tâche de roadmap.
