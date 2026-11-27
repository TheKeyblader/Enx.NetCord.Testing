# 0007 — Modèle d'interaction : requêtes, exécution, capture et cycle de vie

- **Statut** : Proposée
- **Date** : 2026-11-27
- **Auteur** : agent `architect`
- **Tâches liées** : M2-ADR-07, M2-01 à M2-07, M2-ADR-08, M3-ADR-09, M3-08, M5-ADR-10, M5-02

## Contexte

Un test doit pouvoir dire « Alice lance `/greet user:bob` dans #general », exécuter le code du
bot et lire ce que Discord aurait affiché : la réponse, son édition après un `defer`, un
followup, un modal. Trois chemins mènent une interaction au bot (appel direct, Gateway, HTTP
signé) et deux ramènent sa réponse (REST, ou corps de la réponse HTTP). Sans modèle commun,
chaque chemin aurait sa propre capture et ses propres règles (`40060`, 15 minutes).

Seams **publics** vérifiés dans `netcord`, tag `1.0.0-beta.30` = `main` @ `bb5f4e13` :

- `public static Interaction CreateFromJson(JsonModels.JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client)`
  (`NetCord/Interaction.cs`). `InteractionResponseDelegate` n'est **pas** un type public : c'est
  un alias `global using` de
  `Func<IInteraction, InteractionCallbackProperties, bool, RestRequestProperties?, CancellationToken, Task<InteractionCallbackResponse?>>`
  (`NetCord/IInteraction.cs:1`). Le fake fournit donc un `Func<…>`.
- Le constructeur déréférence sans garde `Channel!`, `Entitlements`, `GuildUser!` si `guild_id`
  est présent et `User!` sinon ; `AuthorizingIntegrationOwners!` et `UserLocale!` à la lecture ;
  `Message!` pour un composant (`NetCord/{Interaction,MessageComponentInteraction}.cs`).
  Un payload minimal à la shardix fait donc lever NetCord avant le code du bot.
- La surcharge Gateway `CreateFromJson(JsonInteraction, IGatewayClientCache, RestClient)` prend la
  guilde dans le cache et répond par `client.SendInteractionResponseAsync(interaction.Id, interaction.Token, …)`.
  `GatewayClient.cs:1404` l'appelle pour `INTERACTION_CREATE`. `HandleMessageReceived` est
  `async void` (`NetCord/Gateway/WebSocketClient.cs:375`) : la lecture d'une frame ne prouve pas
  que le handler du bot a fini.
- REST (`NetCord/Rest/RestClient.Interactions.ReceivingAndResponding.cs`) :
  `POST /interactions/{id}/{token}/callback` avec la query `?with_response=True` si demandée ;
  réponse originale et followups sur `/webhooks/{applicationId}/{interactionToken}…`.
  `InteractionCallbackProperties.Serialize()` est public et produit du JSON ou du multipart
  (`NetCord/Rest/InteractionCallback.cs`).
- HTTP (`Hosting/NetCord.Hosting.AspNetCore/{IHttpInteractionParser,IHttpInteractionProcessor}.cs`) :
  le delegate de `HttpInteractionParser` **écrit le callback dans le corps de la réponse HTTP**
  et renvoie `null` même si `withResponse` est vrai ; aucun `POST …/callback` ne part.
  `HttpInteractionProcessor` répond lui-même `Pong` à un `PingInteraction`, passe `guild: null`
  et attend les handlers avant de rendre la main.
- Modèles de lecture constructibles : `RestMessage(JsonMessage, RestClient)`
  (`NetCord/Rest/RestMessage.cs`), `ApplicationCommandOptionChoice(JsonApplicationCommandOptionChoice)`
  (`NetCord/Rest/ApplicationCommandOptionChoice.cs`),
  `Guild(JsonGuild, ulong clientId, RestClient, IDictionaryProvider)` (`NetCord/Gateway/Guild.cs`).
- Contextes Services : `SlashCommandContext(SlashCommandInteraction, GatewayClient)` et
  `HttpSlashCommandContext(SlashCommandInteraction, RestClient)`
  (`NetCord.Services/ApplicationCommands/ApplicationCommandContexts.cs`).

Discord, `discord-api-docs` @ `62ab2d50` et `discord-api-spec` @ `65423001` :

- `developers/interactions/receiving-and-responding.mdx` : structure de l'interaction (l. 20-51),
  matrice des types de callback (l. 266-282), jeton valable 15 minutes et réponse initiale sous
  3 secondes, sinon « the token will be invalidated » (l. 351, 432), réponse inline en HTTP
  (l. 354-376), `with_response` (l. 443-452), premier followup après un `defer` (l. 479).
- `specs/openapi.json` : `create_interaction_response` et les opérations
  `/webhooks/{webhook_id}/{webhook_token}…` ont `security: [{}, {BotToken}]`. Réponse originale
  et followups **partagent les templates des webhooks** : il n'existe pas de route
  `/webhooks/{application_id}/{interaction_token}`.
- `developers/topics/opcodes-and-status-codes.mdx` liste `10008`, `10015`, `10062`, `40060` et
  `50027`, mais **aucune page épinglée ne relie ces codes à un endpoint ni à un statut HTTP**.
  Les associations de cette ADR viennent du comportement observé de l'API ; elles sont marquées
  comme telles dans la table des règles. `fauxcord` @ `70563d51` ne simule pas ces règles.

Ergonomie de référence, `shardix` @ `e6851f10`, `packages/testing/src/index.ts` :
`mockInteraction({ command: 'ping' })` puis `executeInteraction(payload)` renvoie la réponse.
Deux appels lisibles, mais un payload inventé (`mock_guild_123`) et aucune suite possible.

## Options

### Option A — Enregistreur dans le delegate (façon shardix)

Construire le `JsonInteraction` à la main et passer un delegate qui range
l'`InteractionCallbackProperties` reçu dans le résultat. Rien à stocker, M2-01 tient en un
fichier. Mais seule la réponse initiale passe par le delegate : `ModifyResponseAsync` et
`SendFollowupMessageAsync` partent en REST et exigeraient un second mécanisme. Les règles
seraient écrites deux fois, la Gateway et HTTP auraient chacune leur capture, et rien ne
prouverait que le payload est celui que Discord envoie.

### Option B — L'interaction est une ressource de l'état ; la capture est côté serveur

Matérialiser l'interaction dans `FakeDiscordState`, produire **un** payload en octets pour les
trois voies et capturer les réponses là où Discord les reçoit : la route de callback, les
routes à jeton, et le corps inline en HTTP. Le résultat est une lecture de l'état. Coût : la
voie directe dépend des handlers REST d'interaction, et chaque interaction paie une
sérialisation suivie d'une lecture par NetCord.

### Option C — Tout faire passer par la Gateway ou HTTP

Fidèle au transport, sans voie directe. M2 dépendrait de M3 et de M5, un test de module
exigerait un client connecté, et l'objectif n° 1 de `vision.md` arriverait en dernier.

## Décision

**Option B.** Les sept décisions ci-dessous en découlent.

### 1. Requêtes : décrire le geste de l'utilisateur

`discord.Interactions` crée des **requêtes** liées à ce fake. Le contexte obligatoire est
positionnel (qui, où, sur quoi) ; seul le facultatif est fluent. Utilisateur, salon et message
sont des IDs du seed ou de réponses REST : pas d'objet NetCord à fournir.

- Le salon donne le contexte : salon de guilde ⇒ `guild_id`, `guild`, `member` ; DM ⇒ `user`.
  Pour un composant ou une commande de message, le salon vient du message visé.
- `SlashCommand("config roles add", …)` : un à trois segments séparés par une espace désignent
  commande, groupe et sous-commande. Un nom de commande Discord ne contient pas d'espace.
- Une méthode par type d'option (`WithString`, `WithInteger`, `WithUser`…) plutôt qu'un
  `WithOption(name, value)` surchargé : `ulong` désigne aussi bien un utilisateur, un rôle ou
  un salon, et le littéral `3` serait ambigu entre `long`, `double` et `ulong`.
- L'autocomplétion est une commande en cours de saisie : `SlashCommand(…).WithString(…).Autocomplete("query", "har")`.
- `ModalSubmit(opened)` reprend utilisateur, salon, `custom_id`, message source et disposition
  des champs du modal affiché ; `ModalSubmit(customId, user, channel)` sert à tester un
  handler isolé et enveloppe chaque champ dans un `Label` (l'`ActionRow` de modal est
  déprécié, `developers/components/reference.mdx:98`).

**Validation.** Un appel fluent ne vérifie que ses arguments (`ArgumentException`). Tout ce
qui dépend de l'état est vérifié à l'exécution, sous le verrou, avant tout commit : références
absentes, utilisateur non membre de la guilde, message sans ce `custom_id`, composant désactivé
ou d'un autre type, valeur hors des options du menu, option en double, sous-commande
incohérente. Ces fautes sont celles du test : `InvalidOperationException` locale, jamais une
erreur Discord, comme pour le seed de l'[ADR 0004](0004-fake-discord-public-api.md).

Pour une commande, la validation suit ce que l'application a enregistré (M2-07) :

| État de l'application | Nom demandé | Effet |
| --- | --- | --- |
| Commande enregistrée (guilde d'abord, puis globale) | connu | `data.id` et `data.type` de la commande ; options inconnues, types incohérents, options requises manquantes, choix et bornes ⇒ `InvalidOperationException` |
| Au moins une commande enregistrée | inconnu | `InvalidOperationException` listant les noms connus : Discord n'enverrait pas cette interaction |
| Aucune commande enregistrée | — | commande *ad hoc* : `data.id` alloué, validation structurelle seule |

Le harnais de M2-ADR-08 enregistre les commandes : il obtient la validation complète sans
option. Un test de handler brut reste possible sans rien enregistrer. Une autocomplétion
n'exige pas les options requises (`application-commands.mdx:1085`).

**Rejeté :** un builder par objet JSON (le test réécrirait le protocole) ; un mode
strict/lenient dans `FakeDiscordOptions` (une option de plus pour un cas que l'état distingue
déjà) ; des composants *ad hoc* sans message (NetCord exige `message`, et envoyer le message
par REST tient en une ligne).

### 2. Un seul payload, lu par NetCord

À l'exécution, une mutation d'état crée l'interaction, puis le fake projette hors verrou un DTO
interne `InteractionCreate` et le sérialise avec `FakeDiscordJsonContext`
([ADR 0006](0006-wire-serialization.md)). **Ces octets sont les mêmes pour les trois voies.**
La voie directe les relit avec le `JsonTypeInfo<JsonInteraction>` de `Discord.SerializerOptions`,
comme le prévoit l'ADR 0006, puis appelle `Interaction.CreateFromJson`. Construire le
`JsonInteraction` par ses setters est rejeté : deux projections à garder alignées, et la voie
directe ne prouverait plus la forme JSON.

Tous les champs viennent de l'état, aucun n'est inventé par la requête :

| Champ | Valeur |
| --- | --- |
| `id`, `token` | ID alloué selon l'[ADR 0003](0003-state-model-and-concurrency.md) ; jeton déterministe, unique dans ce fake, limité à `[A-Za-z0-9._-]`. Le fake le retrouve par un index, sans jamais le décoder. |
| `application_id`, `version` | identité du fake ; `1` |
| `guild_id`, `guild`, `guild_locale` | guilde du salon : `id`, `features`, `locale` |
| `channel`, `channel_id` | salon partiel, toujours présent |
| `member` ou `user` | membre avec `user` et `permissions` en guilde ; `user` en DM |
| `app_permissions`, `member.permissions` | la fonction de calcul des permissions de l'état, unique : rôles en M2, overwrites avec M4-REST-05 ; valeurs de DM selon `receiving-and-responding.mdx:49` |
| `locale` | `WithLocale`, sinon `en-US` |
| `entitlements` | `[]` jusqu'à M4-REST-09, toujours présent |
| `authorizing_integration_owners`, `context` | `{"0": guild_id}` et `0` en guilde ; `{"0": "0"}` et `1` en DM avec le bot (`receiving-and-responding.mdx:85-90`) |
| `attachment_size_limit` | constante interne du fake |
| `data.resolved` | utilisateurs, membres, rôles, salons, messages et pièces jointes cités par les options, la cible ou les valeurs d'un menu |
| `message` | message de l'état portant le composant, éphémère compris |

**Journal.** `interaction_token` et `webhook_token` sont des paramètres de route masqués
(ADR 0004 et [0005](0005-rest-routing-and-pipeline.md)). Le jeton n'apparaît pas dans
`InteractionResult` : le bot le lit sur `interaction.Token`. La corrélation entre une requête
REST et son interaction est faite par le handler, qui a résolu le jeton, et non par le test.

### 3. Trois voies, une capture

| Voie | Livraison | Réponse initiale | Jalon |
| --- | --- | --- | --- |
| Directe, transport `Gateway` | `CreateFromJson` puis le handler du test | delegate ⇒ `client.SendInteractionResponseAsync` ⇒ pipeline REST | M2 |
| Directe, transport `Http` | idem, `guild: null` | delegate ⇒ `callback.Serialize()` ⇒ entrée inline ; renvoie `null` | M2 |
| Gateway | dispatch `INTERACTION_CREATE` à un `GatewayClient` connecté | REST, par NetCord | M3-08 |
| HTTP signée | POST des octets, signé, vers l'endpoint du bot | corps de la réponse HTTP ⇒ entrée inline | M5-02 |

Les deux transports de la voie directe reproduisent ce que NetCord construit pour un bot
Gateway (guilde du cache, réponse REST) et pour un bot HTTP (pas de guilde, réponse inline
sans `InteractionCallbackResponse`). Un module écrit pour HTTP qui lirait `interaction.Guild`
passerait sinon ses tests et échouerait en production.

La capture ne connaît pas la voie. Un composant interne unique traite la réponse initiale, avec
deux entrées qui convergent vers **la même mutation d'état** :

- le handler de `POST /interactions/{interaction_id}/{interaction_token}/callback`, après
  authentification (anonyme admis), validation et interception de l'ADR 0005 ;
- l'entrée inline `(interactionId, contentType, octets)`, qui décode le même JSON ou multipart
  avec le même lecteur. Elle n'écrit pas dans `Recorded.Requests` : aucune requête REST n'a eu lieu.

Suites et réponse originale passent toujours par REST, quelle que soit la voie. Les sept
clés `…/webhooks/{webhook_id}/{webhook_token}…` gardent **un handler par clé** : si `webhook_id`
est l'ID de l'application, le jeton est un jeton d'interaction ; sinon c'est un webhook
(M4-REST-02). Les IDs étant uniques dans un fake, les deux cas ne se recouvrent pas.

`PING` n'a pas de requête publique : il n'existe qu'en HTTP et NetCord y répond seul (M5-02).

### 4. `InteractionResult` et barrières

`InteractionResult` est une **photographie détachée** de l'interaction dans l'état, comme les
valeurs des ADR 0003 et 0004. Elle montre ce que voit l'utilisateur qui a déclenché
l'interaction, messages éphémères compris.

- `Callback` : type et `data` JSON de la réponse initiale ; `null` si le bot n'a pas répondu.
- `Response` : la réponse originale **dans son état courant** (après éditions), `null` si elle
  n'existe pas ou a été supprimée. Pour les callbacks 6 et 7, c'est le message du composant.
- `Followups` : les followups existants, par ordre de création.
- `Modal`, `AutocompleteChoices` : contenu des callbacks 9 et 8.
- `Requests` : les entrées de `Recorded.Requests` que le fake a rattachées à cette interaction,
  dans l'ordre du journal. L'historique (éditions successives, suppression) se lit là, avec
  les corps JSON exacts ; pas de second journal.

Les messages sont des `RestMessage` NetCord, construits à partir du JSON que le fake renverrait.
Le test affirme avec les types qu'il connaît (`Content`, `Embeds`, `Components`, `Flags`), et
aucun miroir du modèle de message n'est à maintenir. C'est un point d'entrée NetCord au sens
de l'ADR 0006. Rejeté : un type `InteractionMessage` propre, et le `JsonElement` seul.

**Attendre sans horloge.** Trois barrières, aucune par polling ni délai réel :

1. Voie directe : `ExecuteAsync` attend la tâche du handler. Tout ce que le bot a attendu est
   committé et journalisé (l'ADR 0004 ajoute l'entrée avant de rendre la réponse). Une
   exception du handler est propagée telle quelle.
2. `WaitForAsync(interactionId, condition)` : la condition est évaluée sur une photographie,
   puis réévaluée **à chaque commit** qui touche l'interaction ou un de ses messages. Elle
   sert au travail lancé sans être attendu (« defer, puis édition en tâche de fond ») et aux
   voies Gateway et HTTP. Son seul garde-fou est le `CancellationToken` du test.
3. Voies Gateway et HTTP : barrière propre à la voie, fixée par M3-ADR-09 et M5-ADR-10. À
   défaut, elles rendent la main au premier callback committé, soit
   `WaitForAsync(id, r => r.Callback is not null)`.

`GetResult(interactionId)` reprend une photographie à tout moment.

### 5. Règles de cycle de vie

Chaque règle est un contrôle de la mutation, sous le verrou, à l'instant lu une fois sur le
`TimeProvider`. Les refus passent par `DiscordError`. `Δ` = instant courant − création de
l'interaction.

| Règle | Comportement du fake | Source |
| --- | --- | --- |
| Interaction inconnue, ou jeton qui n'est pas le sien | callback ⇒ 404 `10062` | hypothèse |
| Réponse initiale unique | second callback, REST ou inline ⇒ 400 `40060` | code documenté ; statut et association observés |
| Délai de 3 s | callback avec `Δ ≥ 3 s` ⇒ 404 `10062` | délai : `receiving-and-responding.mdx:351` ; code observé |
| Type de callback interdit | 6 et 7 hors composant et modal ; 8 hors autocomplétion et rien d'autre pour elle ; 9 sur modal ou autocomplétion ; 1 hors `PING` ⇒ 400 `50035` | matrice : l. 266-282 ; code : hypothèse |
| Jeton jamais émis | routes à jeton ⇒ 401 `50027` | hypothèse |
| Jeton de 15 min | routes à jeton avec `Δ ≥ 15 min` ⇒ 401 `50027` | durée : l. 351, 432 ; code observé |
| Pas encore de réponse initiale | routes à jeton ⇒ 404 `10015` | observé |
| Réponse originale absente | supprimée, ou callback 8, 9, 12 ⇒ 404 `10008` | observé |
| `defer` (type 5) | crée le message avec `LOADING` (128), plus `EPHEMERAL` si demandé, seul drapeau admis ; `PATCH @original` le remplit et retire `LOADING` | l. 304 ; `resources/message.mdx:155` |
| Premier followup après un type 5 | édite le message en attente, ne crée rien, ignore le drapeau éphémère | l. 479 |
| Followup | crée un message, `wait` toujours vrai ; `thread_id`, `username`, `avatar_url` refusés | l. 477 |
| Éphémère (64) | message stocké, lisible et modifiable par les routes à jeton ; inconnu (`10008`) des routes de salon ; pas de dispatch Gateway au bot | drapeau : `message.mdx:154` ; reste observé |
| `with_response` | `True`, `true` ou `1` ⇒ 200 et `InteractionCallbackResponse` ; sinon 204 | l. 443-452 ; `reference.mdx:264` |

Ordre des contrôles : existence, puis expiration, puis acquittement, puis type, puis contenu
(`50006`, `50035`). Les bornes sont exclusives : avancer l'horloge de 15 minutes exactement
expire le jeton.

**Le délai de 3 s n'est pas simulé, il est comparé.** Aucune attente, aucune option : avec
l'horloge fixe par défaut, `Δ` vaut toujours zéro. La règle ne joue que si le test avance son
horloge entre la livraison et la réponse, par exemple avec
`RestEffect.Delay(TimeSpan.FromSeconds(4))` sur la route de callback. Cette décision remplace
la note « opt-in, désactivé par défaut » de `rule:3s-deadline`, que `PAR-INT-01` corrigera
dans la matrice. La limite de cinq followups des installations utilisateur seules (l. 474)
n'est pas modélisée.

### 6. État et faits

Extension de l'ADR 0003, même verrou, même règle d'atomicité, pas de second modèle.

- **Interaction** : nouvelle entité interne de `State/`, indexée par ID et par jeton. Elle
  porte l'instant de création, l'utilisateur, le salon, la guilde, les données de la requête,
  la réponse initiale (type, instant, `data` détachée), l'ID de la réponse originale, les IDs
  des followups, le modal affiché et les choix d'autocomplétion. Elle n'est jamais purgée
  pendant la vie du fake : un jeton expiré doit rester reconnu.
- **Messages** : réponse et followups sont de **vrais messages** du dictionnaire de l'ADR 0003,
  avec l'application pour auteur, `webhook_id` et `application_id` égaux à son ID,
  `interaction_metadata` et leurs drapeaux. Le message porte en plus l'ID de son interaction et,
  s'il est éphémère, l'utilisateur qui le voit. La visibilité appartient à la **lecture
  d'état** : les lectures « par salon » ignorent les éphémères, les lectures « par jeton » les
  incluent. Les handlers de M1-REST-03 n'ont donc pas de cas particulier à écrire.
- **Atomicité** : un callback valide commit en une mutation l'acquittement, le message créé ou
  modifié et leurs faits. Un refus ne laisse ni acquittement ni message.
- **Faits** : `InteractionCreated` porte la photographie de l'interaction et sa voie
  (directe, Gateway, HTTP). M3 n'en tire `INTERACTION_CREATE` que pour la voie Gateway : passer
  par le flux ordonné garantit que le bot reçoit d'abord les dispatchs des faits antérieurs.
  Les messages réutilisent `MessageCreated`, `MessageUpdated` et `MessageDeleted` ; la
  photographie porte le drapeau éphémère, que M3-03 filtre. L'acquittement seul ne produit
  aucun fait : aucun event Discord n'y correspond.
- **Barrières** : les attentes de `WaitForAsync` s'inscrivent sous le verrou, dans la même
  section que la photographie évaluée ; un commit qui touche l'interaction détache les
  signaux sous le verrou et les complète **après** l'avoir relâché, sans continuation
  synchrone. La condition du test ne s'exécute jamais sous le verrou.

### 7. Frontière avec M2-ADR-08

Cette ADR livre le cœur : requêtes, exécution d'un `Func<Interaction, ValueTask>`, résultat,
règles. M2-ADR-08 compose par-dessus, sans accès interne : construire les services NetCord et
leur `IServiceProvider`, enregistrer les commandes contre le fake, choisir le contexte
(`SlashCommandContext` ou `HttpSlashCommandContext`, donc le transport), appeler
`service.ExecuteAsync` dans le handler, et offrir le raccourci
`bot.ExecuteSlashCommandAsync("ping", from: alice, @in: general)` de `vision.md` avec
l'`IExecutionResult`. Les commandes texte (`MESSAGE_CREATE`) ne sont pas des interactions et
relèvent de M2-ADR-08.

## API publique proposée

Tout est dans le namespace `Enx.NetCord.Testing` ; les constructeurs sont non publics, sauf
celui d'`InteractionExecutionOptions`. `FakeDiscord` gagne un membre :

```csharp
/// <summary>Gets the interaction controls bound to this fake.</summary>
public FakeDiscordInteractions Interactions { get; }
```

```csharp
/// <summary>Creates user interactions against one fake and reads their outcome.</summary>
public sealed class FakeDiscordInteractions
{
    /// <summary>Describes a slash command; separate command, group and subcommand names with one space.</summary>
    public SlashCommandRequest SlashCommand(string name, ulong user, ulong channel);
    /// <summary>Describes a user context menu command aimed at the specified user.</summary>
    public InteractionRequest UserCommand(string name, ulong user, ulong channel, ulong targetUser);
    /// <summary>Describes a message context menu command aimed at an existing message.</summary>
    public InteractionRequest MessageCommand(string name, ulong user, ulong targetMessage);
    /// <summary>Describes a click on a button of an existing message.</summary>
    public InteractionRequest Button(ulong message, string customId, ulong user);
    /// <summary>Describes a selection in a string select menu of an existing message.</summary>
    public InteractionRequest StringMenu(ulong message, string customId, ulong user, params string[] values);
    /// <summary>Describes a selection of users in a user select menu.</summary>
    public InteractionRequest UserMenu(ulong message, string customId, ulong user, params ulong[] users);
    /// <summary>Describes a selection of roles in a role select menu.</summary>
    public InteractionRequest RoleMenu(ulong message, string customId, ulong user, params ulong[] roles);
    /// <summary>Describes a selection of users and roles in a mentionable select menu.</summary>
    public InteractionRequest MentionableMenu(ulong message, string customId, ulong user, params ulong[] mentionables);
    /// <summary>Describes a selection of channels in a channel select menu.</summary>
    public InteractionRequest ChannelMenu(ulong message, string customId, ulong user, params ulong[] channels);
    /// <summary>Describes the submission of the modal shown by a previous interaction.</summary>
    public ModalSubmitRequest ModalSubmit(InteractionResult opened);
    /// <summary>Describes a modal submission that is not tied to a displayed modal.</summary>
    public ModalSubmitRequest ModalSubmit(string customId, ulong user, ulong channel);

    /// <summary>Returns a fresh detached snapshot of an interaction created by this fake.</summary>
    public InteractionResult GetResult(ulong interactionId);
    /// <summary>Completes when the condition holds; it is reevaluated after each commit touching the interaction.</summary>
    public Task<InteractionResult> WaitForAsync(ulong interactionId, Func<InteractionResult, bool> condition,
        CancellationToken cancellationToken = default);
}

/// <summary>Describes one user interaction; each execution creates a new interaction.</summary>
public abstract class InteractionRequest
{
    /// <summary>Sets the invoking user's locale; defaults to en-US.</summary>
    public InteractionRequest WithLocale(string locale);
    /// <summary>Creates the interaction, runs the handler with the NetCord object and returns the outcome.</summary>
    /// <exception cref="InvalidOperationException">The request does not match the fake's state.</exception>
    public Task<InteractionResult> ExecuteAsync(Func<Interaction, ValueTask> handler,
        InteractionExecutionOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>Describes a slash command invocation and its options.</summary>
public sealed class SlashCommandRequest : InteractionRequest
{
    /// <summary>Sets the invoking user's locale; defaults to en-US.</summary>
    public new SlashCommandRequest WithLocale(string locale);
    /// <summary>Adds a string option.</summary>
    public SlashCommandRequest WithString(string name, string value);
    /// <summary>Adds an integer option.</summary>
    public SlashCommandRequest WithInteger(string name, long value);
    /// <summary>Adds a number option.</summary>
    public SlashCommandRequest WithNumber(string name, double value);
    /// <summary>Adds a boolean option.</summary>
    public SlashCommandRequest WithBoolean(string name, bool value);
    /// <summary>Adds a user option and resolves the user, and the member in a guild.</summary>
    public SlashCommandRequest WithUser(string name, ulong userId);
    /// <summary>Adds a channel option and resolves the channel.</summary>
    public SlashCommandRequest WithChannel(string name, ulong channelId);
    /// <summary>Adds a role option and resolves the role.</summary>
    public SlashCommandRequest WithRole(string name, ulong roleId);
    /// <summary>Adds a mentionable option and resolves it as a user or a role.</summary>
    public SlashCommandRequest WithMentionable(string name, ulong id);
    /// <summary>Adds an attachment option and resolves an attachment holding the content.</summary>
    public SlashCommandRequest WithAttachment(string name, string fileName, ReadOnlyMemory<byte> content,
        string? contentType = null);
    /// <summary>Turns the command into an autocomplete interaction focused on one option.</summary>
    public InteractionRequest Autocomplete(string focusedOption, string input,
        ApplicationCommandOptionType type = ApplicationCommandOptionType.String);
}

/// <summary>Describes a modal submission and its field values.</summary>
public sealed class ModalSubmitRequest : InteractionRequest
{
    /// <summary>Sets the invoking user's locale; defaults to en-US.</summary>
    public new ModalSubmitRequest WithLocale(string locale);
    /// <summary>Sets the value of a text input.</summary>
    public ModalSubmitRequest WithText(string customId, string value);
    /// <summary>Sets the selected values of a string select.</summary>
    public ModalSubmitRequest WithValues(string customId, params string[] values);
}

/// <summary>Selects how the directly executed interaction reaches the bot and how its reply returns.</summary>
public enum InteractionTransport
{
    /// <summary>As a Gateway bot: cached guild, initial response sent through REST.</summary>
    Gateway = 0,
    /// <summary>As an HTTP bot: no guild, initial response returned inline.</summary>
    Http = 1
}

/// <summary>Configures one direct execution.</summary>
public sealed class InteractionExecutionOptions
{
    /// <summary>Gets the emulated transport. Defaults to Gateway.</summary>
    public InteractionTransport Transport { get; init; }
    /// <summary>Gets the client given to the interaction, or null to use one owned by the fake.</summary>
    public RestClient? Client { get; init; }
}

/// <summary>Provides a detached snapshot of what an interaction produced.</summary>
public sealed class InteractionResult
{
    /// <summary>Gets the interaction ID.</summary>
    public ulong InteractionId { get; }
    /// <summary>Gets the initial response, or null if the bot has not responded.</summary>
    public RecordedInteractionCallback? Callback { get; }
    /// <summary>Gets the original response in its current state, or null if none exists.</summary>
    public RestMessage? Response { get; }
    /// <summary>Gets the existing followup messages in creation order.</summary>
    public IReadOnlyList<RestMessage> Followups { get; }
    /// <summary>Gets the modal shown by the initial response, if any.</summary>
    public InteractionModal? Modal { get; }
    /// <summary>Gets the autocomplete choices of the initial response, if any.</summary>
    public IReadOnlyList<ApplicationCommandOptionChoice>? AutocompleteChoices { get; }
    /// <summary>Gets the recorded REST attempts attached to this interaction, in recording order.</summary>
    public IReadOnlyList<RecordedRestRequest> Requests { get; }
}

/// <summary>Describes the initial response to an interaction.</summary>
public sealed class RecordedInteractionCallback
{
    /// <summary>Gets the callback type.</summary>
    public InteractionCallbackType Type { get; }
    /// <summary>Gets the detached callback data, or null if none was sent.</summary>
    public JsonElement? Data { get; }
    /// <summary>Gets whether the response acknowledges now and completes later.</summary>
    public bool IsDeferred { get; }
    /// <summary>Gets whether the response is visible only to the invoking user.</summary>
    public bool IsEphemeral { get; }
}

/// <summary>Describes a modal shown to the user.</summary>
public sealed class InteractionModal
{
    /// <summary>Gets the modal custom ID.</summary>
    public string CustomId { get; }
    /// <summary>Gets the modal title.</summary>
    public string Title { get; }
    /// <summary>Gets the detached JSON array of the modal components.</summary>
    public JsonElement Components { get; }
}
```

Les autres champs de modal (sélecteurs d'entités, `FileUpload`, `RadioGroup`, `CheckboxGroup`,
`Checkbox`) suivront le même schéma, une méthode `With…(customId, …)` par forme de réponse ;
M2-05 propose leurs signatures après vérification de `developers/components/reference.mdx`,
et le reviewer les valide. `Client`, s'il est fourni, doit être branché sur ce fake : le fake
ne peut pas le vérifier.

Exemple de test d'une slash command (pseudo-code ; `GreetHandler` est le code du bot) :

```csharp
async Task Greet_UserOption_RepliesWithMention()
{
    await using var discord = FakeDiscord.Create();
    var guild = discord.Seed.Guild("Test");
    var general = guild.TextChannel("general");
    var alice = guild.Member("alice");
    var bob = guild.Member("bob");

    var result = await discord.Interactions.SlashCommand("greet", alice, general.Id)
        .WithUser("user", bob)
        .ExecuteAsync(i => GreetHandler.HandleAsync((SlashCommandInteraction)i));

    Assert.Equal(InteractionCallbackType.Message, result.Callback!.Type);
    Assert.Equal($"Hello <@{bob}>!", result.Response!.Content);
}
```

Cycle de vie avec l'horloge virtuelle, puis clic sur un bouton de la réponse :

```csharp
var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
await using var discord = FakeDiscord.Create(new FakeDiscordOptions { TimeProvider = clock });
var guild = discord.Seed.Guild("Test");
var general = guild.TextChannel("general");
var alice = guild.Member("alice");

Interaction kept = null!;
var deferred = await discord.Interactions.SlashCommand("report", alice, general.Id).ExecuteAsync(async i =>
{
    kept = i;
    await i.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));
});
Assert.True(deferred.Callback!.IsDeferred);
Assert.True(deferred.Response!.Flags.HasFlag(MessageFlags.Loading));

var twice = await Assert.ThrowsAsync<RestException>(() => kept.SendResponseAsync(InteractionCallback.Message("again")));
Assert.Equal(40060, twice.Error!.Code);

await kept.ModifyResponseAsync(m => m.Components =
    [new ActionRowProperties([new ButtonProperties("confirm", "OK", ButtonStyle.Primary)])]);
var edited = discord.Interactions.GetResult(deferred.InteractionId);
Assert.False(edited.Response!.Flags.HasFlag(MessageFlags.Loading));

var click = await discord.Interactions.Button(edited.Response.Id, "confirm", alice)
    .ExecuteAsync(i => new(i.SendResponseAsync(InteractionCallback.DeferredModifyMessage)));
Assert.Equal(edited.Response.Id, click.Response!.Id);

clock.Advance(TimeSpan.FromMinutes(15));
var expired = await Assert.ThrowsAsync<RestException>(() => kept.SendFollowupMessageAsync("late"));
Assert.Equal(50027, expired.Error!.Code);
```

## Conséquences

- Une règle écrite une fois vaut pour les trois voies, et la voie directe prouve le JSON que
  la Gateway et HTTP enverront. Le prix : l'exécution directe ne répond qu'une fois la route de
  callback livrée, l'interaction reste en mémoire jusqu'à la fin du fake, et chaque interaction
  coûte une sérialisation et deux lectures par NetCord (payload, messages du résultat).
- Le fake ne voit pas le travail que le bot lance sans l'attendre. `WaitForAsync` le rend
  observable, mais un bot qui ne répond jamais bloque le test jusqu'à son annulation.
- Un `RestMessage` du résultat garde le client du fake : ses méthodes d'instance agissent sur
  le fake tant qu'il vit. `timestamp` n'est pas exposé par NetCord ; il se lit dans `Requests`.
- Pas de nouvelle dépendance, pas de réflexion : `JsonInteraction`, `JsonMessage` et `JsonGuild`
  sont des racines du contexte généré de NetCord (`NetCord/Serialization.cs:16,46,61`).
  `JsonApplicationCommandOptionChoice` n'en est pas une : il n'est atteint que par
  `JsonApplicationCommand`. M2-04 vérifie que `Discord.SerializerOptions` en fournit les
  métadonnées ; sinon `AutocompleteChoices` devient un petit type propre (nom, valeur).

**Tâches impactées.**

- **M2-01** : `discord.Interactions`, `SlashCommandRequest` *ad hoc*, entité et jeton, DTO du
  payload, relecture par NetCord, `ExecuteAsync` dans les deux transports, `resolved`. Sans la
  route de callback, son test ne prouve que l'objet reçu par le handler ; `Callback` vaut `null`.
- **M2-02** : la mutation de réponse initiale avec ses deux entrées (route REST de callback,
  entrée inline), tous les types, `with_response`, `InteractionResult`. La route de callback
  **passe donc de M2-03 à M2-02**.
- **M2-03** : les sept routes à jeton, premier followup après `defer`, visibilité des
  éphémères dans les lectures d'état, `WaitForAsync`, `GetResult`, rattachement des requêtes.
- **M2-04** : `UserCommand`, `MessageCommand`, `Autocomplete`. **M2-05** : requêtes de
  composants et de modal, modal affiché dans l'état, signatures des champs restants.
  **M2-06** : la recherche d'un `custom_id` traverse les conteneurs V2.
- **M2-07** : le registre de commandes devient la source de la validation des requêtes.
- **M3-08** : terminal `DispatchAsync` sur `InteractionRequest`, traduction du fait
  `InteractionCreated` de voie Gateway ; M3-ADR-09 fixe la barrière. **M3-03** filtre les
  messages éphémères.
- **M5-02** : POST signé des mêmes octets, entrée inline, `PING`. M5-ADR-10 choisit comment le
  package Hosting atteint les trois opérations internes (matérialiser, entrée inline,
  photographie) : `InternalsVisibleTo` ou petite API publique.

**Tâches à créer** (l'orchestrateur les ajoute à `docs/roadmap.md`) :

- `M2-10` — `Interaction.Guild` en voie directe, transport `Gateway` : projection
  `GUILD_CREATE` minimale partagée avec M3-02, lue en `JsonGuild`, avec un
  `IDictionaryProvider` du fake (celui de NetCord est interne) — interactions-implementer —
  M2-01, M1-REST-06. D'ici là, `guild` vaut `null` dans les deux transports.
- `M2-11` — validation des requêtes de commande contre les commandes enregistrées (schéma,
  portée guilde puis globale, `data.id`) — interactions-implementer — M2-04, M2-07.
- `PAR-INT-01` — dans `interactions.md` : ajouter les lignes `rule:callback-type`,
  `rule:followup-after-defer`, `rule:unacked-token` et `harness:direct-http-transport` ;
  corriger la note de `rule:3s-deadline` (toujours actif, piloté par l'horloge) ; lier
  `rule:token-15min`, `rule:ephemeral` et `rule:followups` à cette ADR — parity-auditor —
  M2-ADR-07.
- `PAR-ERR-01` — confirmer statuts HTTP et libellés exacts de `40060`, `10062`, `10015`,
  `50027` et `10008` sur les routes d'interaction, et le code d'un type de callback interdit ;
  aucune source épinglée ne les donne — parity-auditor — M2-02.

**Parité.** Prépare toutes les lignes de `docs/parity/interactions.md` hors Hosting, les routes
de callback et `/webhooks/{webhook_id}/{webhook_token}…` de `rest.md`, et `10008`, `10015`,
`10062`, `40060`, `50027` de `errors.md`. Aucune ligne promue ni exclue, aucune matrice
modifiée par cette ADR : les notes et lignes à ajuster sont dans `PAR-INT-01` et `PAR-ERR-01`.
