# 0008 — Harnais NetCord.Services : composition, DI, enregistrement et exécution

- **Statut** : Proposée
- **Date** : 2026-11-28
- **Auteur** : agent `architect`
- **Tâches liées** : M2-ADR-08, M2-07, M2-08, M2-09, M2-10, M2-11, M2-T-01, M3-ADR-09, M3-08, M5-ADR-10

## Contexte

L'[ADR 0007](0007-interaction-model.md) livre le cœur : une requête (`discord.Interactions.SlashCommand(…)`),
son exécution par un handler `Func<Interaction, Task>` et un `InteractionResult`. Un bot écrit
avec `NetCord.Services` n'a pas de handler de cette forme : il a des **modules**, qu'un service
NetCord instancie, alimente par un `IServiceProvider` et appelle avec un **contexte**. Entre la
requête et le module, chaque test devrait donc construire un client, enregistrer les commandes,
caster l'interaction, créer le contexte et un scope, puis lire un `IExecutionResult` qui a pu
avaler une exception. Le harnais écrit ce chemin une fois.

Seams **publics** vérifiés dans `netcord`, tag `1.0.0-beta.30` = `main` @ `bb5f4e13` :

- **Services.** `ApplicationCommandService<TContext>` et sa variante avec autocomplétion
  `ApplicationCommandService<TContext, TAutocompleteContext>`
  (`NetCord.Services/ApplicationCommands/ApplicationCommandService.cs:51,15`),
  `ComponentInteractionService<TContext>` (`ComponentInteractions/ComponentInteractionService.cs:15`),
  `CommandService<TContext>` (`Commands/CommandService.cs:17`). Leur constructeur public prend
  une configuration facultative.
- **Modules et délégués.** `AddModule<T>()` exige
  `DynamicallyAccessedMembers(PublicConstructors | PublicMethods | PublicNestedTypes)`
  (`ApplicationCommandService.cs:95,103` ; sans `PublicNestedTypes` pour les composants,
  `ComponentInteractionService.cs:38,46`). `AddModules(Assembly)` porte
  `RequiresUnreferencedCode` (`IService.cs:20`). Les délégués « minimal API » passent par des
  builders publics : `AddSlashCommand(SlashCommandBuilder)`, `AddUserCommand`, `AddMessageCommand`
  (`ApplicationCommandService.cs:160-183`), `AddComponentInteraction(ComponentInteractionBuilder)`.
  `host.AddSlashCommand(…)` appartient à `NetCord.Hosting.Services`.
- **Enregistrement.** La méthode s'appelle **`RegisterCommandsAsync`**
  (`RestClient client, ulong applicationId, ulong? guildId = null, …`, `ApplicationCommandService.cs:196`) ;
  `CreateCommandsAsync`, cité par `interactions.md`, `architecture.md` et la skill
  `fake-interaction`, n'existe pas à ce SHA. `ApplicationCommandServiceManager`
  (`ApplicationCommandServiceManager.cs:13,33,47`) regroupe plusieurs services en **un** appel :
  il écarte les commandes `Register = false` (l. 57) puis fait un seul
  `BulkOverwriteGlobalApplicationCommandsAsync` ou `BulkOverwriteGuildApplicationCommandsAsync`
  (l. 77-79), soit `PUT /applications/{application_id}/commands` ou
  `PUT /applications/{application_id}/guilds/{guild_id}/commands`. Un service enregistre tout
  globalement ou tout dans **une** guilde ; aucune commande ne porte sa propre guilde.
- **Recherche de la commande.** Par nom et type par défaut
  (`NameAndTypeApplicationCommandServiceStorage`, `IApplicationCommandServiceStorage.cs:48-69`),
  ou par `data.id` (`IdApplicationCommandServiceStorage`, l. 16-46), qui n'accepte **qu'un**
  enregistrement (l. 27-33).
- **Exécution.** `ExecuteAsync(TContext context, IServiceProvider? serviceProvider = null)` rend
  un `ValueTask<IExecutionResult>` et **attrape toute exception** en `ExecutionExceptionResult`
  (`ApplicationCommandService.cs:207-220`, `ComponentInteractionService.cs:87-97`).
  `ExecuteAutocompleteAsync` n'existe que sur la variante à deux paramètres (l. 23).
  `IExecutionResult`, `IFailResult` et `IExceptionResult` sont publics (`IExecutionResult.cs`) ;
  `SuccessResult` est interne : un succès se reconnaît à `is not IFailResult`. Un module qui
  rend `string` répond par `context.Interaction.SendResponseAsync(Message(…))`
  (`InteractionResultResolverProviderHelper.cs`).
- **Contextes.** Chaque contexte a un constructeur `(interaction, client)` : `GatewayClient`
  pour `ApplicationCommandContext`, `SlashCommandContext`, `ButtonInteractionContext`,
  `ModalInteractionContext`… ; `RestClient` pour leurs jumeaux `Http…`
  (`ApplicationCommands/ApplicationCommandContexts.cs:20,44,75,99`,
  `AutocompleteInteractionContexts.cs:20,43`, `ComponentInteractions/ComponentInteractionContexts.cs`).
  `CommandContext(Message, GatewayClient)` n'a pas de jumeau HTTP (`Commands/CommandContexts.cs:19`).
- **DI.** `NetCord.Services` ne référence que `NetCord` (`NetCord.Services.csproj`) et ne connaît
  que `System.IServiceProvider`. Le module est instancié à chaque exécution par un délégué
  compilé qui résout ses paramètres de constructeur dans le provider reçu ; il n'a pas à y être
  enregistré (`Helpers/TypeHelper.cs:9`).
- **Hosting**, pour comparaison : un scope par interaction par défaut
  (`Hosting/NetCord.Hosting.Services/ApplicationCommands/ApplicationCommandInteractionHandler.cs:42-48`),
  un contexte construit par réflexion sur ses constructeurs (`ContextHelper.cs`), puis un
  *result handler* qui **répond à l'utilisateur** avec le message de tout `IFailResult`
  (`ApplicationCommandResultHandler.cs:33-50`). L'ID d'application vient de
  `(IEntityToken)client.Token` (`ApplicationCommandServiceHostedService.cs:32-35`).
- **Clients.** `RestClient.Token` est public (`NetCord/Rest/RestClient.cs:31`), mais son
  handler est un champ privé (l. 15, 39). Un `GatewayClient` ne se construit qu'avec
  `(IEntityToken, GatewayClientConfiguration?)` et crée son propre client REST
  (`NetCord/Gateway/GatewayClient.cs:854`) : sans le `RequestHandler` interne du fake, **aucun
  code hors du package cœur ne peut créer un `GatewayClient` dont `Rest` vise le fake**. Un
  client jamais démarré est utilisable ; NetCord teste ainsi ses services
  (`Tests/ServicesTest/ServiceTester.cs:8`).
- **AOT.** NetCord déclare `IsAotCompatible` pour tous ses projets (`Directory.Build.props`).
  `NetCord.Services` découvre les commandes par réflexion annotée et compile ses appels avec
  `Expression.Lambda(…).Compile()` (`Helpers/InvocationHelper.cs:41,62`).

Ergonomie de référence, `shardix` @ `e6851f10`, `packages/testing/src/index.ts` :
`createTestingApplication({ controllers })` puis `app.executeInteraction(payload)`. Une fabrique,
une méthode d'exécution.

## Options

### Option A — Harnais miroir de `NetCord.Hosting.Services`

Le builder possède tout : un `IServiceCollection` interne, des méthodes
`AddApplicationCommandModule<T>()` et `AddSlashCommand(name, description, handler)`, un contexte
créé par réflexion, un *result handler*. C'est l'exemple de `vision.md` mot pour mot. Mais il
faut recopier les annotations de trimming de NetCord, déduire `TContext` du type de base du
module, dépendre du conteneur Microsoft, et simuler à la main un pipeline que M5 testera avec
le **vrai** host.

### Option B — Harnais de composition

Le test construit ses services NetCord comme son bot le fait, avec leur configuration. Le
harnais ajoute ce qui manque entre ces services et les requêtes de l'ADR 0007 : clients,
enregistrement, routage par type d'interaction, contexte, scope, résultat.

### Option C — Pas de harnais, des recettes

`request.ExecuteAsync(async i => await service.ExecuteAsync(new SlashCommandContext((SlashCommandInteraction)i, client)))`.
Rien à maintenir, mais chaque test répète l'enregistrement, le cast, le scope et la lecture de
l'`IExecutionResult`, et doit obtenir un `GatewayClient` branché, ce qui est impossible
aujourd'hui.

## Décision

**Option B.** Le harnais ne possède ni les modules, ni le conteneur, ni un second modèle
d'interaction. Les quatre arbitrages qui dépendaient des préférences de l'utilisateur ont été
tranchés par lui le 2026-11-28 : fabrique Gateway minimale dans le cœur (§ 2), provider fourni
par le test (§ 2), exception du module relancée (§ 4), une seule méthode d'exécution (§ 4).

### 1. Point d'entrée et cycle de vie

```csharp
var commands = new ApplicationCommandService<ApplicationCommandContext>();
commands.AddModule<PingModule>();

await using var bot = await discord.Services().AddApplicationCommands(commands).BuildAsync();
var result = await bot.ExecuteAsync(discord.Interactions.SlashCommand("ping", alice, general.Id));
```

- `discord.Services()` est une méthode d'extension du package Services. Elle rend un builder
  neuf et ne touche pas au fake.
- Le test passe **l'instance** du service. `AddModule<T>`, `AddModules(assembly)`, les builders
  de délégués, les type readers, le stockage par ID ou la localisation restent des appels
  NetCord, écrits dans le test : le harnais n'en recopie aucun.
- `BuildAsync()` crée le client, enregistre les commandes (§ 3) et rend le harnais. Le builder
  ne sert qu'une fois ; sans aucun service ajouté, `BuildAsync` lève `InvalidOperationException`.
- `FakeDiscordServices` est `IAsyncDisposable`. Il libère **son** client, jamais le provider,
  les services NetCord ni le fake. `DisposeAsync` est idempotent ; ensuite `ExecuteAsync` lève
  `ObjectDisposedException`. Le harnais se déclare après le fake et se ferme avant lui.
- Tout passe par l'API publique du cœur, comme l'exige l'ADR 0007, § 7 : pas
  d'`InternalsVisibleTo` vers ce package.

**Rejeté :** `AddApplicationCommandModule<T>()` sur le builder (il faudrait recopier les
annotations `DynamicallyAccessedMembers` de NetCord et suivre leurs changements, puis déduire
`TContext`, ce qui demande `MakeGenericType`) ; un lambda
`AddApplicationCommands<TContext>(s => s.AddModule<T>())`, qui double chaque surcharge pour
gagner deux lignes et ne permet pas de réutiliser la fabrique de services du bot.

### 2. Clients, transport et DI

**Un transport par harnais.** Le type de contexte choisit le transport de l'ADR 0007 : un
contexte à `GatewayClient` s'exécute en `InteractionTransport.Gateway`, un contexte `Http…` en
`InteractionTransport.Http`. Un module écrit pour `HttpApplicationCommandContext` est donc testé
sans guilde et avec une réponse inline, sans option à régler. Le premier service ajouté fixe le
transport ; en ajouter un de l'autre transport lève `InvalidOperationException`. La raison est
mécanique : `request.ExecuteAsync` reçoit le transport **avant** de créer l'interaction, alors
que son type n'est connu qu'après. Un bot est Gateway ou HTTP ; un test qui veut les deux crée
deux harnais.

**Clients.** Le harnais crée et possède un seul client : `discord.CreateGatewayClient()` en
transport `Gateway`, `discord.CreateRestClient()` en transport `Http`. L'interaction reçoit
`InteractionExecutionOptions.Client` = `gateway.Rest` ou ce `RestClient`, comme en production.
Il n'accepte **pas** de client venu du test ou du provider : il ne pourrait pas vérifier qu'il
vise ce fake, et un `RestClient` réel pris dans la racine de composition du bot enverrait les
réponses sur le réseau. Tant que la Gateway n'existe pas, l'identité de l'instance ne change
rien : tous les clients d'un fake voient le même état. Un service du bot qui injecte
`RestClient` reçoit celui que le test enregistre, `discord.CreateRestClient()`. M3-ADR-09 dira
comment un client **connecté**, dont le cache compte, rejoint le harnais.

**Ajout au cœur : `FakeDiscord.CreateGatewayClient()`.** L'ADR 0004 reportait toute fabrique
Gateway à M3-ADR-09. Le besoin arrive en M2 : les contextes Gateway exigent un `GatewayClient`
non nul, que seul le cœur peut brancher. Cette ADR amende donc l'ADR 0004 d'un membre **sans
paramètre** :

- son client REST est branché comme celui de `CreateRestClient()` (handler et gestionnaire sans
  attente neufs, via `GatewayClientConfiguration.RestClientConfiguration`), avec le jeton du bot ;
- compression désactivée, et un `IWebSocketConnectionProvider` du fake. Jusqu'à M3-01, son
  `CreateConnection()` lève `NotSupportedException` : `StartAsync` échoue donc avant toute socket
  (`NetCord/Gateway/WebSocketClient.cs:427`, premier appel de `ConnectAsync`). Aucun réseau ;
- l'appelant possède le client et le dispose ; fermer le fake l'invalide comme un client REST ;
- son cache est vide : `context.Guild` vient de `Interaction.Guild`, donc de M2-10.

M3-ADR-09 garde la main sur les intents, les shards et le démarrage, par surcharge ou paramètre
facultatif ; l'appel sans argument doit rester valide.

**Rejeté :** n'offrir que les contextes `Http…` jusqu'à M3 (le module type de `vision.md` ne
serait pas testable) ; exposer un `RestClientConfiguration` branché (un handler partagé par
deux clients meurt avec le premier disposé) ; `InternalsVisibleTo` (contraire à l'ADR 0007).

**DI.** `UseServices(IServiceProvider)` reçoit le provider du test, qui en reste propriétaire :
conteneur Microsoft, autre conteneur ou implémentation à la main. Sans appel, les services
NetCord reçoivent `null` et seuls les modules à constructeur sans service fonctionnent, comme
avec NetCord nu.

- **Un scope par exécution**, comme le défaut de `NetCord.Hosting.Services` : si le provider
  résout `IServiceScopeFactory`, le harnais crée un scope asynchrone, passe son provider au
  service NetCord et à la fabrique de contexte, puis le dispose avant de rendre la main.
  Sinon il passe le provider tel quel, sans erreur : un provider écrit à la main est légitime.
- Dépendance ajoutée : `Microsoft.Extensions.DependencyInjection.Abstractions`, seule. Le
  harnais ne référence pas le conteneur.
- Le harnais n'enregistre rien dans le provider et n'y lit que la fabrique de scopes.

**Rejeté :** un `IServiceCollection` interne avec `ConfigureServices(…)` (dépendance au
conteneur, un seul conteneur possible, et un second propriétaire pour les services du bot).

### 3. Enregistrement des commandes

`BuildAsync` appelle le vrai NetCord : un `ApplicationCommandServiceManager` reçoit tous les
services de commandes d'application, dans l'ordre d'ajout, puis **un seul**
`RegisterCommandsAsync(client, applicationId, guildId)`. Un appel par service ne conviendrait
pas : chaque `PUT` remplace la liste entière et le dernier effacerait les autres.

- `applicationId` est l'ID du jeton du client, comme le fait NetCord.Hosting.
- **Global par défaut**, comme NetCord.Hosting. `RegisterCommandsInGuild(guildId)` envoie tout
  dans une guilde, seul autre choix de NetCord. Les commandes ne sont alors visibles que là
  (règle des commandes visibles de l'ADR 0007).
- Aucun service de commandes : aucun `PUT`, pour ne pas vider un registre que le test a rempli.
- Le `PUT` est une vraie requête : elle figure dans `Recorded.Requests`, avant celles du test.
  `InteractionResult.Requests`, rattaché à l'interaction, ne la contient pas.
- **Pas de mode dégradé.** Si la route n'a pas de handler (avant M2-07), `BuildAsync` propage
  l'erreur de NetCord : `RestException` 404 en `Lenient`, `NotSupportedException` en `Strict`
  (ADR 0005). M2-08 dépend de M2-07 ; un harnais de composants seuls n'en dépend pas.
- **Lien avec M2-11.** Une fois le registre rempli, les requêtes de commande sont validées
  contre lui et portent le vrai `data.id` : le stockage par ID de NetCord fonctionne, et une
  option mal nommée échoue dans le test avant d'atteindre le module. Entre M2-08 et M2-11, les
  requêtes restent *ad hoc* : seul le stockage par nom et type, celui par défaut, retrouve la
  commande.

Limites assumées : une commande `Register = false` n'entre pas dans le registre, donc M2-11
refuse la requête qui la nomme ; un second harnais global sur le même fake remplace les
commandes du premier ; un service à stockage par ID ne sert qu'à un seul harnais.

### 4. Exécution et résultat

`bot.ExecuteAsync(request)` est la seule méthode. Elle prend n'importe quelle requête de
l'ADR 0007 et compose `request.ExecuteAsync(handler, options)` :

1. le handler reçoit l'`Interaction` et cherche **le** service inscrit dont le type
   d'interaction l'accepte ; une `AutocompleteInteraction` va au service de commandes qui
   accepte les `SlashCommandInteraction` ;
2. il crée le scope, puis le contexte ;
3. il appelle `service.ExecuteAsync(context, services)` ou `ExecuteAutocompleteAsync` ;
4. il rend un `ServiceExecutionResult` : l'`IExecutionResult` de NetCord et
   l'`InteractionResult` de l'ADR 0007.

**Routage.** Chaque service est inscrit avec un type d'interaction : celui du constructeur du
contexte pour les contextes NetCord, `TInteraction` pour un contexte propre. Deux services de
la même famille dont les types se recouvrent (`ApplicationCommandContext` et
`SlashCommandContext`) sont refusés à l'ajout : NetCord.Hosting exécuterait les deux et le
perdant répondrait « not found ». Aucun service pour l'interaction reçue :
`InvalidOperationException`, c'est une erreur du test.

**Contextes.** Pour les contextes fournis par NetCord, le harnais tient une table statique
`typeof(TContext)` → (transport, type d'interaction, constructeur), sans réflexion. Un autre
type passé à la surcharge sans fabrique lève `NotSupportedException` et nomme la surcharge à
utiliser : `AddApplicationCommands(service, createContext)`, ou `AddHttp…` pour le transport
`Http`. Les paramètres du lambda sont typés pour que `TInteraction` soit inféré (dernier
exemple). La fabrique reçoit le provider du scope, comme
`CreateContext` de NetCord.Hosting. L'autocomplétion est détectée sur l'instance :
`ApplicationCommandService<TContext, AutocompleteInteractionContext>` ou
`…<TContext, HttpAutocompleteInteractionContext>`, du même transport que `TContext`. Une
requête d'autocomplétion vers un service qui n'est ni l'un ni l'autre lève
`InvalidOperationException`.

**Ce que le test observe.**

| Situation | `Execution` | `Interaction` | Exception |
| --- | --- | --- | --- |
| Le module se termine | succès (`is not IFailResult`) | ce que le bot a envoyé | — |
| Précondition, type reader, paramètres, « not found » | `IFailResult` | **rien d'ajouté** par le harnais | — |
| Le module lève, y compris une `RestException` de réponse (`40060`…) | — | — | l'exception d'origine, pile conservée |
| Le module ne répond pas | succès | `Callback` vaut `null` | — |
| Transport `Http`, réponse inline refusée | succès | `RejectedCallbacks` | — |
| Aucun service pour ce type d'interaction | — | — | `InvalidOperationException` |

- **L'exception est relancée.** NetCord la range dans un `IExceptionResult` ; le harnais la
  relance telle quelle depuis le handler, et la voie directe la propage (ADR 0007).
  `Execution` n'est donc jamais un `IExceptionResult`. Un bug de module fait échouer le test là
  où il se produit, au lieu d'un `Response` nul trois lignes plus bas ; un test d'erreur
  s'écrit `Assert.ThrowsAsync`. Contrepartie : l'`InteractionResult` d'une exécution qui lève
  n'est pas rendu.
- **Un `IFailResult` est rendu, pas traité.** Le harnais n'envoie pas le message d'échec à
  l'utilisateur : c'est le rôle du *result handler* de `NetCord.Hosting.Services`, ou du code
  du bot. Ce pipeline (pré-exécution, *result handler*, `IContextAccessor`) se teste avec le
  vrai host, en M5.
- **Pas de réponse, pas d'attente.** Le harnais ne juge pas un module muet. Pour un travail que
  le bot n'attend pas, `discord.Interactions.WaitForAsync(result.Interaction.InteractionId, …)`.
- Le `cancellationToken` suit l'ADR 0007 : il n'abandonne que l'attente.

**Rejeté :** des raccourcis `ExecuteSlashCommandAsync("ping", from:, @in:)` comme dans
`vision.md` et l'ADR 0007, § 7. Dès qu'une commande a une option, il leur faut un lambda ou
une copie des `With…`, et il en faudrait un par fabrique de requête. Rejeté aussi : rendre
l'`IExceptionResult` (fidèle à NetCord, mais chaque test devrait le vérifier avant de lire la
réponse).

### 5. Composants, modals et commandes texte

**Composants et modals (M2-09).** Même builder, même `ExecuteAsync`.
`AddComponentInteractions(service)` inscrit un `ComponentInteractionService<TContext>` ; un bot
en a souvent un par type (`ButtonInteractionContext`, `StringMenuInteractionContext`,
`ModalInteractionContext`) et le routage du § 4 les départage. Le découpage du `custom_id` en
arguments reste celui de NetCord. `discord.Interactions.ModalSubmit(opened)` prend
`click.Interaction`, le résultat du clic qui a ouvert le modal.

**Commandes texte : par la Gateway, donc en M3.** Une commande texte est un `MESSAGE_CREATE`,
pas une interaction. Une voie directe avant M3 est possible sur le papier, `Message` ayant un
constructeur public (`NetCord/Gateway/Message.cs:9`), mais elle est rejetée :

- le JSON d'un message lu par REST n'a ni `guild_id` ni `member` ; produire le vrai payload
  demande la projection `MESSAGE_CREATE` de M3-03, interne au cœur ;
- `Message.Guild` et `Message.Channel` viennent du cache du client (`Message.cs:11-43`), vide
  tant qu'il n'est pas connecté ;
- l'intent `MESSAGE_CONTENT` ne serait pas appliqué : un test passerait alors que le bot ne
  reçoit rien en production.

Direction retenue, à fixer par M3-ADR-09 : le test poste un message ordinaire par le seed ; la
Gateway le dispatche au client connecté du harnais ; le harnais, abonné à `MessageCreate`,
applique le préfixe, crée le `CommandContext`, exécute et range l'`IExecutionResult` par ID de
message. Signatures **indicatives** :

```csharp
public FakeDiscordServicesBuilder AddCommands(CommandService<CommandContext> service, string prefix);
public Task<IExecutionResult?> WaitForCommandAsync(ulong messageId, CancellationToken cancellationToken = default);
```

Aucune API de plus dans le cœur. La partie « text commands » de M2-09 devient une tâche de M3.

### 6. AOT, trimming, dépendances

- Le harnais n'ajoute **aucune** réflexion : pas de `MakeGenericType`, pas de recherche de
  constructeur, pas de parcours d'assembly. La table des contextes compare des `typeof` ; les
  contextes propres passent par un délégué.
- Il n'appelle jamais `AddModule` ni `AddModules`. Les exigences de trimming de NetCord
  s'appliquent là où le test écrit ces appels ; le package n'a ni annotation à recopier ni
  avertissement à supprimer, et reste `IsAotCompatible` sous `TreatWarningsAsErrors`.
- Ce que `NetCord.Services` fait lui-même (réflexion annotée, expressions compilées donc
  interprétées en Native AOT) n'est ni aggravé ni corrigé ici.
- Dépendances du package : `Enx.NetCord.Testing`, `NetCord.Services`,
  `Microsoft.Extensions.DependencyInjection.Abstractions`. Aucun framework de test : les
  erreurs sont des exceptions .NET ordinaires ([ADR 0002](0002-test-framework-agnostic.md)).

## API publique proposée

Ajout au package cœur, sur `FakeDiscord` (amende l'[ADR 0004](0004-fake-discord-public-api.md)) :

```csharp
/// <summary>Creates a caller-owned NetCord gateway client whose REST client targets this fake.</summary>
/// <remarks>The client cannot be started until the in-memory Gateway is available.</remarks>
/// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
public GatewayClient CreateGatewayClient();
```

Package `Enx.NetCord.Testing.Services` (corps omis ; constructeurs non publics) :

```csharp
namespace Enx.NetCord.Testing
{
    /// <summary>Adds the NetCord.Services harness to a fake.</summary>
    public static class FakeDiscordServicesExtensions
    {
        /// <summary>Starts describing a harness that runs NetCord services against this fake.</summary>
        public static FakeDiscordServicesBuilder Services(this FakeDiscord discord);
    }
}

namespace Enx.NetCord.Testing.Services
{
    /// <summary>Collects the NetCord services of a bot and builds the harness that runs them.</summary>
    public sealed class FakeDiscordServicesBuilder
    {
        /// <summary>Sets the provider handed to modules and contexts; the caller keeps ownership.</summary>
        public FakeDiscordServicesBuilder UseServices(IServiceProvider services);

        /// <summary>Registers application commands in one guild instead of globally.</summary>
        public FakeDiscordServicesBuilder RegisterCommandsInGuild(ulong guildId);

        /// <summary>Adds an application command service whose context is a NetCord context.</summary>
        /// <exception cref="NotSupportedException">The context is not provided by NetCord; pass a context factory.</exception>
        /// <exception cref="InvalidOperationException">The service overlaps another one or uses the other transport.</exception>
        public FakeDiscordServicesBuilder AddApplicationCommands<TContext>(
            ApplicationCommandService<TContext> service)
            where TContext : IApplicationCommandContext;

        /// <summary>Adds an application command service with a custom context, run as a Gateway bot.</summary>
        /// <exception cref="InvalidOperationException">The service overlaps another one or uses the other transport.</exception>
        public FakeDiscordServicesBuilder AddApplicationCommands<TInteraction, TContext>(
            ApplicationCommandService<TContext> service,
            Func<TInteraction, GatewayClient, IServiceProvider?, TContext> createContext)
            where TInteraction : ApplicationCommandInteraction
            where TContext : IApplicationCommandContext;

        /// <summary>Adds an application command service with a custom context, run as an HTTP bot.</summary>
        /// <exception cref="InvalidOperationException">The service overlaps another one or uses the other transport.</exception>
        public FakeDiscordServicesBuilder AddHttpApplicationCommands<TInteraction, TContext>(
            ApplicationCommandService<TContext> service,
            Func<TInteraction, RestClient, IServiceProvider?, TContext> createContext)
            where TInteraction : ApplicationCommandInteraction
            where TContext : IApplicationCommandContext;

        /// <summary>Adds a component interaction service whose context is a NetCord context.</summary>
        /// <exception cref="NotSupportedException">The context is not provided by NetCord; pass a context factory.</exception>
        /// <exception cref="InvalidOperationException">The service overlaps another one or uses the other transport.</exception>
        public FakeDiscordServicesBuilder AddComponentInteractions<TContext>(
            ComponentInteractionService<TContext> service)
            where TContext : IComponentInteractionContext;

        /// <summary>Adds a component interaction service with a custom context, run as a Gateway bot.</summary>
        /// <exception cref="InvalidOperationException">The service overlaps another one or uses the other transport.</exception>
        public FakeDiscordServicesBuilder AddComponentInteractions<TInteraction, TContext>(
            ComponentInteractionService<TContext> service,
            Func<TInteraction, GatewayClient, IServiceProvider?, TContext> createContext)
            where TInteraction : ComponentInteraction
            where TContext : IComponentInteractionContext;

        /// <summary>Adds a component interaction service with a custom context, run as an HTTP bot.</summary>
        /// <exception cref="InvalidOperationException">The service overlaps another one or uses the other transport.</exception>
        public FakeDiscordServicesBuilder AddHttpComponentInteractions<TInteraction, TContext>(
            ComponentInteractionService<TContext> service,
            Func<TInteraction, RestClient, IServiceProvider?, TContext> createContext)
            where TInteraction : ComponentInteraction
            where TContext : IComponentInteractionContext;

        /// <summary>Creates the client, registers the application commands against the fake and returns the harness.</summary>
        /// <exception cref="InvalidOperationException">No service was added, or the builder was already used.</exception>
        public Task<FakeDiscordServices> BuildAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>Runs interactions of one fake through the NetCord services of a bot.</summary>
    public sealed class FakeDiscordServices : IAsyncDisposable
    {
        /// <summary>Creates the interaction, runs the matching service in a new scope and returns both outcomes.</summary>
        /// <exception cref="InvalidOperationException">
        /// The request does not match the fake's state, or no service accepts this interaction.
        /// </exception>
        /// <exception cref="ObjectDisposedException">The harness or the fake has been disposed.</exception>
        /// <remarks>An exception thrown by the module is rethrown unchanged.</remarks>
        public Task<ServiceExecutionResult> ExecuteAsync(InteractionRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>Disposes the client created by the harness.</summary>
        public ValueTask DisposeAsync();
    }

    /// <summary>Pairs what NetCord reported with what Discord received for one execution.</summary>
    public sealed class ServiceExecutionResult
    {
        /// <summary>Gets the NetCord execution result; a failure implements IFailResult, never IExceptionResult.</summary>
        public IExecutionResult Execution { get; }
        /// <summary>Gets the snapshot of the interaction taken when the service returned.</summary>
        public InteractionResult Interaction { get; }
    }
}
```

Contextes reconnus sans fabrique, chacun avec son jumeau `Http…` : `ApplicationCommandContext`,
`SlashCommandContext`, `UserCommandContext`, `MessageCommandContext`, `EntryPointCommandContext` ;
`ComponentInteractionContext`, `MessageComponentInteractionContext`, `ButtonInteractionContext`,
`StringMenuInteractionContext`, `EntityMenuInteractionContext`, `UserMenuInteractionContext`,
`RoleMenuInteractionContext`, `MentionableMenuInteractionContext`, `ChannelMenuInteractionContext`,
`ModalInteractionContext`. Les contextes `EntryPoint…` servent à l'enregistrement ; aucune
requête ne les atteint (ADR 0007, § 3). Les `Base…Context`, sans client, passent par une fabrique.

Exemple 1 — `/ping` (pseudo-code ; `PingModule` est le code du bot) :

```csharp
public sealed class PingModule : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("ping", "Replies with pong")]
    public string Ping() => "Pong!";
}

async Task Ping_GuildChannel_RepliesPong()
{
    await using var discord = FakeDiscord.Create();
    var guild = discord.Seed.Guild("Test");
    var general = guild.TextChannel("general");
    var alice = guild.Member("alice");

    var commands = new ApplicationCommandService<ApplicationCommandContext>();
    commands.AddModule<PingModule>();
    await using var bot = await discord.Services().AddApplicationCommands(commands).BuildAsync();

    var result = await bot.ExecuteAsync(discord.Interactions.SlashCommand("ping", alice, general.Id));

    Assert.IsNotAssignableFrom<IFailResult>(result.Execution);
    Assert.Equal(InteractionCallbackType.Message, result.Interaction.Callback!.Type);
    Assert.Equal("Pong!", result.Interaction.Response!.Content);
}
```

Exemple 2 — bouton puis modal, avec un service injecté (même seed) :

```csharp
async Task Ticket_ButtonThenModal_StoresTicket()
{
    var tickets = new InMemoryTicketStore();
    await using var provider = new ServiceCollection()
        .AddSingleton<ITicketStore>(tickets)
        .BuildServiceProvider();

    var commands = new ApplicationCommandService<ApplicationCommandContext>();
    commands.AddModule<TicketCommands>();   // "/ticket" replies with a "ticket-open" button
    var buttons = new ComponentInteractionService<ButtonInteractionContext>();
    buttons.AddModule<TicketButtons>();     // "ticket-open" shows the "ticket-form" modal
    var modals = new ComponentInteractionService<ModalInteractionContext>();
    modals.AddModule<TicketModals>();       // "ticket-form" stores the ticket and replies

    await using var bot = await discord.Services()
        .UseServices(provider)
        .AddApplicationCommands(commands)
        .AddComponentInteractions(buttons)
        .AddComponentInteractions(modals)
        .BuildAsync();

    var shown = await bot.ExecuteAsync(discord.Interactions.SlashCommand("ticket", alice, general.Id));
    var click = await bot.ExecuteAsync(
        discord.Interactions.Button("ticket-open", alice, shown.Interaction.Response!.Id));
    Assert.Equal("ticket-form", click.Interaction.Modal!.CustomId);

    var submit = await bot.ExecuteAsync(
        discord.Interactions.ModalSubmit(click.Interaction).WithText("subject", "Login fails"));
    Assert.Equal("Ticket created", submit.Interaction.Response!.Content);
    Assert.Equal("Login fails", Assert.Single(tickets.All).Subject);
}
```

Échec rendu, exception relancée, contexte propre (même harnais ; `/ban` porte une précondition,
`/crash` lève) :

```csharp
var denied = await bot.ExecuteAsync(
    discord.Interactions.SlashCommand("ban", alice, general.Id).WithUser("user", bob));
Assert.IsAssignableFrom<IFailResult>(denied.Execution);
Assert.Null(denied.Interaction.Callback);   // the harness sends no failure message

await Assert.ThrowsAsync<InvalidOperationException>(
    () => bot.ExecuteAsync(discord.Interactions.SlashCommand("crash", alice, general.Id)));

discord.Services().AddHttpApplicationCommands(tenantCommands,
    (SlashCommandInteraction i, RestClient client, IServiceProvider? services) =>
        new TenantContext(i, client, services!.GetRequiredService<ITenant>()));
```

## Conséquences

- Le test écrit deux lignes de plus que l'exemple de `vision.md`, et ce sont celles de son
  bot : aucune API NetCord n'est recopiée, et une évolution de `AddModule` ou de ses
  annotations ne touche pas le harnais. `vision.md` reste indicatif ; son exemple sera réaligné
  avec les guides (M6-01).
- Le harnais teste `NetCord.Services` **nu**. Le message d'échec envoyé par le *result handler*,
  les handlers de pré-exécution et `host.AddSlashCommand` relèvent de `NetCord.Hosting.Services`
  et se testent avec le vrai host (M5), où `ServiceExecutionResult` n'existe pas : le host
  consomme l'`IExecutionResult`.
- La table des contextes suit la liste de NetCord. Un contexte ajouté en amont n'est pas
  reconnu tant qu'elle n'est pas complétée, mais la fabrique sert d'issue : pas de blocage.
- Un contexte d'autocomplétion **propre** n'est pas pris en charge : NetCord ne permet pas de
  le détecter sur l'instance sans réflexion. Le test compose alors à la main avec l'ADR 0007.
- Le cœur gagne `CreateGatewayClient()` avant la Gateway. Ce client ne démarre pas en M2 et
  son cache reste vide : `context.Guild` est `null` jusqu'à M2-10.
- Une exécution qui lève ne rend pas son `InteractionResult`. Le test qui veut les deux
  compose avec `request.ExecuteAsync` de l'ADR 0007.
- L'ADR 0007, § 7, annonçait un raccourci `ExecuteSlashCommandAsync` : il est remplacé par
  `bot.ExecuteAsync(request)`.

**Tâches impactées.**

- **M2-07** : le registre doit accepter le `PUT` global et le `PUT` de guilde tels que
  `RegisterCommandsAsync` les envoie (tous les types de commande, type 4 compris, localisations)
  et rendre les commandes **dans l'ordre reçu** : NetCord associe la réponse aux commandes par
  position (`ApplicationCommandServiceManager.cs:81`).
- **M2-08** : extension `Services()`, builder, table des contextes de commandes et
  d'autocomplétion, fabriques, `ApplicationCommandServiceManager`, `ExecuteAsync`,
  `ServiceExecutionResult`, scope, `PackageVersion` de
  `Microsoft.Extensions.DependencyInjection.Abstractions`. Dépend de M2-07, **M2-02** (sans
  route de callback, aucune réponse n'est captée), **M2-04** (commandes user et message,
  autocomplétion) et de la nouvelle **M2-12**.
- **M2-09** : réduite à `ComponentInteractionService` (table des contextes de composants,
  routage) ; dépend de M2-05 et M2-08. Les commandes texte partent en M3.
- **M2-10** : conditionne `context.Guild` en transport `Gateway`.
- **M2-11** : rend utilisable le stockage par ID ; aucune API de harnais n'en dépend.
- **M2-T-01** : le sample suit les deux exemples ; il ne lit pas `Context.Guild` avant M2-10.
- **M3-ADR-09** : surcharge de `CreateGatewayClient` (intents, shards, démarrage) ; entrée d'un
  client connecté dans le harnais, et exécution par dispatch (M3-08) sans changer
  `ExecuteAsync(request)` ; barrière et signature des commandes texte.
- **M5** : le package Hosting n'utilise pas ce harnais. `services:minimal-api` au sens de
  `host.AddSlashCommand` est une ligne de M5.

**Tâches à créer** (l'orchestrateur les ajoute à `docs/roadmap.md`) :

- `M2-12` — `FakeDiscord.CreateGatewayClient()` : REST branché, compression désactivée,
  provider WebSocket qui refuse la connexion jusqu'à M3-01 ; mise à jour de la couche « Façade »
  d'`architecture.md` — rest-implementer — M1-02.
- `M3-09` — Harnais Services : `CommandService` par `MESSAGE_CREATE` sur un client connecté
  (préfixe, résultat par ID de message) — interactions-implementer — M3-ADR-09, M3-03, M3-04, M2-08.
- `PAR-INT-02` — dans `interactions.md` : `services:register-commands` cite `RegisterCommandsAsync`
  et les deux `PUT` ; `services:minimal-api` distingue les builders de `NetCord.Services` (ce
  harnais) de `host.AddSlashCommand` (M5) ; `services:text-commands` dépend de M3 ; ajouter
  `services:execution-result` (échec rendu, exception relancée), `services:scoped-services` et
  `services:custom-context` — parity-auditor — M2-ADR-08.
- `PAR-SEAM-01` — remplacer `CreateCommandsAsync` par `RegisterCommandsAsync` dans la skill
  `fake-interaction` — orchestrator — M2-ADR-08.

**Parité.** Prépare, dans `docs/parity/interactions.md`, `services:application-commands`,
`services:register-commands`, `services:autocomplete`, `services:component-interactions` et la
ligne `harness:direct-http-transport` que `PAR-INT-01` doit ajouter ; reporte
`services:text-commands` à M3 et `services:minimal-api` (host) à M5. Dans `rest.md`, les deux
opérations `bulk_set_application_commands` et `bulk_set_guild_application_commands` seront
exercées par NetCord lui-même ; dans `gateway.md`, `MESSAGE_CREATE` portera les commandes texte.
Aucune ligne promue ni exclue, aucune matrice modifiée par cette ADR.
