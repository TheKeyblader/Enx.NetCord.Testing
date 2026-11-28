# 0012 — Assertions agnostiques sur le journal REST

- **Statut** : Proposée
- **Date** : 2026-11-28
- **Auteur** : agent `architect`
- **Tâches liées** : M1-ADR-12, M1-04, M1-04B, M2-03, M2-T-01, M3-ADR-09, M6-01 ; à créer :
  M2-14, M3-10

> Les numéros 0009 à 0011 sont réservés par `M3-ADR-09`, `M5-ADR-10` et `M7-ADR-11`
> ([ADR 0004](0004-fake-discord-public-api.md), amendement 04B) : le saut de 0008 à 0012 est voulu.

## Contexte

L'[ADR 0002](0002-test-framework-agnostic.md) promet que les assertions de `src/` lèvent
`FakeDiscordAssertionException`, sans dire ce qu'est ce type ni quelles assertions existent.
L'[ADR 0004](0004-fake-discord-public-api.md) livre le journal (`discord.Recorded.Requests`) et
écarte les assertions. Aujourd'hui, vérifier « le bot a envoyé un message dans ce salon »
s'écrit :

```csharp
var post = Assert.Single(discord.Recorded.Requests,
    r => r.Method == "POST" && r.RouteTemplate == "/channels/{channel_id}/messages"
      && r.RouteParameters["channel_id"] == welcome.Id.ToString(CultureInfo.InvariantCulture));
```

Trois défauts. Une faute de frappe dans le template ne lève rien : le filtre ne trouve rien, et
un `Assert.DoesNotContain` passe à tort. En cas d'échec, le runner dit « la collection ne
contient pas d'élément », pas ce que le bot a réellement appelé. Et le test dépend d'un
framework pour une question qui est propre à Discord.

Ce que fournit le code réel (`main` @ `59a1246`) :

- `DiscordRecording.Requests` rend une **nouvelle liste** à chaque lecture, sur des
  enregistrements immuables **partagés** entre les lectures (`Recording/DiscordRecording.cs:44`,
  test `Requests_ConsecutiveReads_ReturnNewListsOverTheSameImmutableRecords`). Après
  `DisposeAsync`, la lecture lève `ObjectDisposedException` ; une liste déjà prise reste lisible.
- `RecordedRestRequest` porte `Method`, `Path`, `RouteTemplate`, `RouteParameters`, `Query`,
  `RequestBody`, `StatusCode`, `ResponseBody`. Les segments et paramètres de jeton valent
  `[redacted]` (`Rest/RestRedaction.cs:7,16`). `RequestBody` est `null` quand la tentative s'est
  terminée avant la lecture du contenu (route inconnue ou non implémentée, 405, 401).
- Une tentative est journalisée quelle que soit son issue. Un 405 garde le template du chemin
  avec une méthode **absente de l'inventaire** (`Rest/RestPipeline.cs:67-70,83`) ; un chemin
  inconnu n'a pas de template.
- La clé `METHOD /template` est celle de la `RouteTable` : `TryGetEntry(key)` dit si
  l'opération est inventoriée, `Resolve(method, segments)` applique la règle « le littéral prime
  sur le paramètre » (`Rest/RouteTable.cs:143,159`). `Rest.Intercept` exige en plus un handler
  (`Rest/RestInterceptionRegistry.cs:41`).
- La table d'un fake créé par l'API publique est toujours `RouteTable.Default`. La surcharge
  `FakeDiscord.Create(options, routes)` est interne (`FakeDiscord.cs:66`) ; les tests du dépôt
  qui créent ainsi un `FakeDiscord` construisent tous leur table sur `RouteInventory.All`.

Aucun seam NetCord n'est en jeu : les assertions lisent le journal du fake et ne prennent ni ne
rendent de type NetCord.

Côté runners, toute exception non gérée fait échouer le test. xUnit v3 (`xunit.v3` 4.0.3 dans
`Directory.Packages.props`) distingue en plus la **cause** : une exception qui implémente une
interface nommée `IAssertionException`, dans n'importe quel namespace, est rapportée comme un
échec d'assertion, sans dépendance vers xUnit
([What's New in v3](https://xunit.net/docs/getting-started/v3/whats-new), consulté le
2026-11-28, non épinglé). NUnit classe une exception qui n'est pas la sienne en « Error » plutôt
qu'en « Failure » : le test échoue dans les deux cas.

Inspiration : `shardix/packages/testing` (@ `main`) n'a aucune assertion propre. Son README
rend une réponse inspectable et laisse `expect(response.data.content).toContain('Pong')` au
framework. La leçon retenue : rendre des valeurs que le test affirme avec ses outils, et ne
fournir une assertion que là où le fake sait mieux expliquer l'échec.

## Options

### Option A — Pas d'assertion : des filtres, et la bibliothèque du test

`discord.Recorded.RequestsTo("POST /channels/{channel_id}/messages")` rend une liste, vide ou
non ; le test écrit `Assert.Single(…)` ou `.Should().ContainSingle()`. Surface minimale, aucune
exception à définir. Mais le message d'échec reste celui d'une collection quelconque, et
l'ADR 0002 comme `architecture.md` annoncent des assertions.

### Option B — Chaîne fluide ou vérification à la Moq

`discord.Recorded.Should().HaveRequested(route).WithBody(…).Once()`, ou
`Verify(r => r.Method == "POST", Times.Once())` avec un `Expression<Func<…>>`. Lecture agréable,
mais :

- une chaîne a besoin d'un terminal ; un terminal oublié est une assertion qui ne vérifie rien ;
- chaque filtre devient un type ou une méthode publics (`Times`, `WithBody`, `WithStatus`…) ;
- un arbre d'expression doit être compilé ou interprété à l'exécution, ce que Native AOT ne
  garantit pas ;
- `Should()` existe déjà dans FluentAssertions sur `object` : deux sens pour le même mot dans
  un même fichier de test.

### Option C — Méthodes d'extension `Should…` terminales, qui rendent ce qu'elles ont trouvé

`var post = discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{welcome.Id}/messages");`
Chaque appel vérifie tout de suite, lève en cas d'échec, et rend les enregistrements trouvés.
La suite (corps, statut) s'affirme avec la bibliothèque du test.

## Décision

**Option C.** Le fake répond à la question qu'il est seul à bien poser (« cette opération
a-t-elle été tentée, combien de fois ? ») et explique l'échec avec le journal. Le reste revient
au test, sur les valeurs rendues.

### 1. `FakeDiscordAssertionException`

- Namespace racine `Enx.NetCord.Testing`, comme les autres types publics. `sealed`, dérive de
  `Exception` : ni type d'un framework de test, ni `InvalidOperationException` (un `catch` du
  bot ou du test pourrait l'avaler).
- Immuable : rien d'autre que `Message` et `InnerException`. Pas de propriété structurée
  (requêtes attendues ou trouvées) : aucun runner ne l'afficherait, et le test qui veut des
  données les a déjà par `Recorded.Requests`.
- Deux constructeurs **publics**, `(string message)` et `(string message, Exception? innerException)`.
  Un test ou une bibliothèque bâtie sur NetCord peut ainsi écrire ses propres assertions et
  échouer de la même façon. Pas de constructeur sans message ni de constructeur de
  sérialisation (obsolète depuis .NET 8).
- **Frontière.** Seules les méthodes `Should…` la lèvent, et seulement quand ce qui est observé
  diffère de ce qui est attendu. Un mauvais usage lève autre chose (§ 4). Le fake ne la
  rattrape jamais.
- **Indice pour xUnit v3.** Elle implémente une interface **interne**, vide, nommée
  `IAssertionException`. xUnit v3 la reconnaît par son nom et rapporte un échec d'assertion ;
  les autres runners l'ignorent. C'est un indice, pas un contrat : si un trimmer retire
  l'interface, le test échoue quand même, avec la cause « exception ».

### 2. Forme des assertions

Une classe statique `RecordedRequestAssertions`, namespace racine : `using Enx.NetCord.Testing;`
suffit. Trois méthodes, sur deux récepteurs.

| Méthode | Réussit si | Rend |
| --- | --- | --- |
| `ShouldHaveRequested` | au moins une tentative correspond | toutes les correspondances, dans l'ordre du journal (liste non vide) |
| `ShouldHaveRequestedOnce` | exactement une tentative correspond | cette tentative |
| `ShouldNotHaveRequested` | aucune tentative ne correspond | rien |

- **Récepteur `DiscordRecording`** : `discord.Recorded.ShouldHaveRequested(…)` examine une
  photographie du journal entier, prise une fois par appel.
- **Récepteur `IEnumerable<RecordedRestRequest>`** : mêmes méthodes sur une suite déjà prise,
  énumérée une fois. Il sert à borner l'examen (`discord.Recorded.Requests.Skip(before)`), et
  en M2 aux requêtes d'une interaction (`result.Requests`, [ADR 0007](0007-interaction-model.md))
  sans nouvelle API. Une liste détachée reste examinable après `DisposeAsync`.
- **Tentatives, pas succès.** Le journal compte les tentatives, erreurs comprises (ADR 0004) :
  un POST refusé par un 403 correspond. Un retry de NetCord après un 429 fait **deux**
  tentatives, donc `ShouldHaveRequestedOnce` échoue. Le statut se filtre par le prédicat ou se
  lit sur l'enregistrement rendu. « Ce message existe-t-il ? » reste une question d'état, posée
  par le vrai client (`rest.GetMessageAsync`).
- **Instantané, sans attente.** Aucune assertion n'attend ni ne réessaie. Si le bot travaille
  encore, le test franchit d'abord une barrière (`await` de l'appel, `WaitForAsync` de
  l'ADR 0007, barrières Gateway de M3-ADR-09).
- **Identité.** Les enregistrements rendus sont les instances du journal, pas des copies :
  l'égalité de référence situe une requête dans `Recorded.Requests`.

**Comptage.** « Exactement une » a sa méthode, parce que c'est le cas courant et qu'elle rend un
enregistrement et non une liste. « Exactement N » s'écrit sur la liste rendue, avec la
bibliothèque du test : `Assert.Equal(3, sent.Count)`. *Rejeté :* un paramètre `times` (exactement
ou au moins ? il faudrait un type `Times` public pour lever le doute).

**Ordre.** Pas d'assertion dédiée. Deux appels concurrents n'ont pas d'ordre prédictible
(ADR 0004) ; pour des appels séquentiels, l'identité ci-dessus suffit (exemple plus bas).

**Prédicat.** Un `Func<RecordedRestRequest, bool>` facultatif couvre statut, query et corps sans
ajouter un paramètre par critère. Il n'est appelé que sur les tentatives qui correspondent déjà
au motif de route, une fois chacune, dans l'ordre. Une exception du prédicat est propagée telle
quelle : c'est une erreur du test, pas un échec d'assertion. Il doit donc tolérer un
`RequestBody` `null`. Le texte source du prédicat est capté à la compilation par
`[CallerArgumentExpression]` et repris dans le message d'échec : ni réflexion ni arbre
d'expression. L'usage conseillé reste de filtrer par la route, puis d'affirmer sur
l'enregistrement rendu ; le prédicat sert à départager plusieurs tentatives.

### 3. Motif de route

L'argument `route` est un **motif** : `METHOD`, une espace, un chemin dont chaque segment
paramétré est soit son nom entre accolades, soit une valeur concrète.

| Motif | Correspond à |
| --- | --- |
| `"POST /channels/{channel_id}/messages"` | tout POST de message, quel que soit le salon |
| `$"POST /channels/{welcome.Id}/messages"` | les POST de message dans ce salon |
| `$"DELETE /channels/{channel.Id}/messages/{{message_id}}"` | toute suppression de message dans ce salon |
| `"GET /users/@me"` | l'opération littérale, jamais `user_id = "@me"` |

- Un motif sans valeur concrète est exactement la clé `METHOD /template` de
  l'[ADR 0005](0005-rest-routing-and-pipeline.md) : la même chaîne sert à `Rest.Intercept`, à la
  matrice de parité et à l'assertion.
- Le motif est résolu par la table de routes, avec la règle du routeur (le littéral prime sur
  le paramètre). `RouteTable.Resolve` suffit : un segment `{nom}` occupe une position de
  paramètre, puis chaque valeur extraite est lue comme joker si elle vaut `{nom du paramètre}`,
  comme contrainte sinon.
- Une tentative correspond si sa méthode et son template sont ceux de l'opération résolue et si
  chaque contrainte égale, en comparaison ordinale, la valeur **décodée** de
  `RouteParameters`. Le motif n'est pas décodé : on y écrit `👍`, pas `%F0%9F%91%8D`.
- Un `ulong` interpolé donne ses chiffres ASCII quelle que soit la culture courante.
- **Table utilisée.** Récepteur `DiscordRecording` : la table de son fake, celle du pipeline et
  de `Intercept`. Récepteur suite : `RouteTable.Default`. Pour tout fake créé par l'API
  publique, c'est la même.

Un motif que la table ne sait pas résoudre est une **erreur de programmation** :
`ArgumentException` (paramètre `route`), jamais un échec d'assertion ni un succès silencieux.
C'est ce qui empêche `ShouldNotHaveRequested` de réussir sur une faute de frappe.

| Motif refusé | Raison donnée dans le message |
| --- | --- |
| `"post /users/@me"`, `"GET  /users/@me"`, `"/users/@me"` | forme attendue : `METHOD /path`, méthode en majuscules, une seule espace |
| `"GET /api/v10/users/@me"`, `"GET /users/@me?x=1"`, `"GET /users//x"` | pas de préfixe d'API, de query, de fragment ni de segment vide |
| `"GET /nowhere"` | aucune opération connue pour ce chemin |
| `"PUT /users/@me"` | méthode absente pour ce chemin ; le message liste celles de l'inventaire |
| `"GET /channels/{id}"`, `"GET /channels/{channel.Id}"` | `{…}` n'est pas le paramètre attendu `{channel_id}` ; rappel du `$` oublié |
| `$"POST /webhooks/{id}/{token}"` | un paramètre de jeton est masqué dans le journal : écrire `{webhook_token}` |

- L'opération doit être **inventoriée**, pas implémentée : en mode `Lenient`, une tentative sur
  une route sans handler est journalisée avec son template, et affirmer que le bot l'a tentée,
  ou ne l'a pas tentée, est légitime. `Intercept` reste plus strict, car il doit agir.
- Deux cas restent hors des assertions et se lisent par LINQ sur `Recorded.Requests` : une
  tentative sans template (chemin inconnu) et un 405, dont la méthode n'est pas inventoriée.
  Une valeur de paramètre qui contient `/` (segment `%2F`) se filtre par le prédicat.

*Rejeté :* la seule clé de template, les valeurs passant par le prédicat. Le cas le plus courant
(« dans **ce** salon ») s'écrirait
`r => r.RouteParameters["channel_id"] == id.ToString(CultureInfo.InvariantCulture)`.
*Rejeté aussi :* des paramètres nommés (`channelId:`, `status:`, `body:`), soit un par critère et
par famille de routes.

### 4. Erreurs et ordre des contrôles

| Situation | Exception |
| --- | --- |
| Ce qui est observé diffère de ce qui est attendu | `FakeDiscordAssertionException` |
| Récepteur ou `route` `null` | `ArgumentNullException` |
| Fake fermé (récepteur `DiscordRecording`) | `ObjectDisposedException` |
| Motif refusé (§ 3), élément `null` dans la suite | `ArgumentException` |
| Le prédicat lève | son exception, non enveloppée |

Dans cet ordre, qui est celui de `Rest.Intercept` : arguments nuls, fake fermé, motif, puis
examen.

### 5. Message d'échec

En anglais, lignes séparées par `\n`, nombres en culture invariante. Première ligne : la phrase
d'attente, avec le motif **tel qu'écrit** et le texte du prédicat s'il y en a un. Viennent
ensuite les tentatives sur la **même opération**, puis les autres :

```text
Expected exactly one request matching POST /channels/100/messages, but found 2.

Requests to POST /channels/{channel_id}/messages (3 of 5 examined; * = match):
* #2 POST /channels/100/messages -> 200  {"content":"Welcome bob!"}
  #3 POST /channels/200/messages -> 403 (50001)  {"content":"Welcome bob!"}
* #5 POST /channels/100/messages -> 200  {"content":"Welcome bob!"}

Other requests (2):
  #1 GET /users/@me -> 200
  #4 PUT /channels/100/messages/300/reactions/👍/@me -> 204
```

- Phrases : `Expected at least one request matching <motif>[ where <prédicat>], but found none.`
  `Expected exactly one request matching …, but found none.` ou `…, but found <n>.`
  `Expected no request matching …, but found <n>.`
- Une ligne par tentative : rang (à partir de 1) dans la suite examinée, méthode, chemin du
  journal (jetons déjà masqués), query s'il y en a une, statut numérique ou `no response`, et
  le `code` JSON de Discord si la réponse d'erreur en porte un.
- Seules les tentatives sur la même opération montrent leur corps de requête, par
  `JsonElement.GetRawText()`, coupé à 200 caractères. `*` marque celles qui satisfont motif et
  prédicat.
- Plafonds fixes : 10 lignes par section, puis `… and <n> more`. Journal vide :
  `No request was recorded.` `ShouldNotHaveRequested` n'affiche pas la section des autres
  requêtes.
- Le message reprend des corps tels qu'envoyés : comme le journal, ce n'est pas un export
  expurgé (ADR 0004).
- **Le texte exact n'est pas un contrat de compatibilité.** Les tests du dépôt vérifient la
  présence des éléments (phrase, motif, lignes, marque, plafonds), pas le message entier.

### 6. Périmètre

**Lot 1 (M1-04B)** : `FakeDiscordAssertionException` et les six méthodes de
`RecordedRequestAssertions`. Rien d'autre.

**Plus tard**, chaque lot par un amendement de cette ADR avant d'être implémenté :

- **M2 — `InteractionResult`.** L'ADR 0007 rend déjà des `RestMessage` NetCord : la plupart des
  vérifications s'écrivent avec la bibliothèque du test. Les assertions utiles sont celles dont
  l'échec demande le contexte de l'interaction, par exemple « a répondu », qui cite
  `RejectedCallbacks` (`10062` après 3,2 s) quand il n'y a pas de réponse. Tâche `M2-14`.
- **M3 — Gateway.** Dépend du journal des frames et des dispatchs, à définir par M3-ADR-09.
  Tâche `M3-10`.

**Règles communes aux lots futurs.** Nom en `Should…` ; une classe statique par type observé
(`InteractionResultAssertions`…) dans `Assertions/`, namespace racine ; vérification immédiate
sur une photographie, sans attente ; valeur observée rendue, non nulle, pour enchaîner ;
`FakeDiscordAssertionException` pour l'échec et `Argument*Exception` pour le mauvais usage ;
message qui cite les observations utiles ; délégués, jamais d'arbres d'expression.

*Rejeté pour le lot 1 :*

- Des assertions de domaine sur le journal (`ShouldHaveSentMessage(channel, "hello")`) : une par
  ressource, et l'existence d'un message se lit dans l'état par le vrai client.
- Des aides de lecture JSON sur `RequestBody` : `JsonElement` suffit, et un test les écrit en
  trois lignes (exemple plus bas).
- `ShouldEventuallyHaveRequested` : attendre sans barrière, c'est du polling.
- Une remise à zéro du journal : l'ADR 0004 l'exclut ; une tranche de `Requests` la remplace.

### 7. Composition, AOT et surface publique

- Le fake ne dépend d'aucune bibliothèque d'assertions, et n'en imite aucune. Les noms sont
  propres à Discord (`…Requested`) : pas de collision avec `ShouldBe` (Shouldly) ni avec
  `Should()` (FluentAssertions, AwesomeAssertions).
- Tout est statique et pur : pas de réflexion, pas de `System.Linq.Expressions`, pas de
  sérialiseur (le corps s'affiche par `GetRawText()`). `IsAotCompatible` reste vrai sans
  annotation.
- Surface publique ajoutée : deux types, six méthodes. Le motif de route, le formateur et
  l'interface marqueur sont internes.

## API publique proposée

```csharp
using System.Runtime.CompilerServices;

namespace Enx.NetCord.Testing;

/// <summary>The exception thrown when an assertion of the fake fails.</summary>
/// <remarks>
/// Only <c>Should…</c> methods throw it, and only when what was observed differs from what was expected;
/// a misuse of the API throws an argument exception instead. Tests may throw it from their own assertions.
/// </remarks>
public sealed class FakeDiscordAssertionException : Exception /* , internal IAssertionException */
{
    /// <summary>Initializes a new instance with the failure message.</summary>
    public FakeDiscordAssertionException(string message);

    /// <summary>Initializes a new instance with the failure message and the exception that caused the failure.</summary>
    public FakeDiscordAssertionException(string message, Exception? innerException);
}

/// <summary>Asserts on the REST attempts recorded by a fake.</summary>
/// <remarks>
/// A route pattern is <c>METHOD /path</c> where each parameter segment is either its placeholder, such as
/// <c>{channel_id}</c>, or a concrete decoded value. Attempts are matched whatever their outcome, error responses included.
/// </remarks>
public static class RecordedRequestAssertions
{
    /// <summary>Asserts that at least one recorded attempt matches the route pattern and the predicate.</summary>
    /// <param name="recording">The journal of the fake.</param>
    /// <param name="route">The route pattern, such as <c>POST /channels/{channel_id}/messages</c>.</param>
    /// <param name="predicate">An optional filter applied to the attempts that match the pattern.</param>
    /// <param name="predicateExpression">The source text of <paramref name="predicate"/>, supplied by the compiler.</param>
    /// <returns>The matching attempts in recording order; never empty.</returns>
    /// <exception cref="FakeDiscordAssertionException">No attempt matches.</exception>
    /// <exception cref="ArgumentException">The pattern does not identify a known REST operation.</exception>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    public static IReadOnlyList<RecordedRestRequest> ShouldHaveRequested(
        this DiscordRecording recording,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null);

    /// <summary>Asserts that exactly one recorded attempt matches the route pattern and the predicate.</summary>
    /// <returns>The matching attempt.</returns>
    /// <exception cref="FakeDiscordAssertionException">No attempt, or more than one, matches.</exception>
    public static RecordedRestRequest ShouldHaveRequestedOnce(
        this DiscordRecording recording,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null);

    /// <summary>Asserts that no recorded attempt matches the route pattern and the predicate.</summary>
    /// <exception cref="FakeDiscordAssertionException">At least one attempt matches.</exception>
    public static void ShouldNotHaveRequested(
        this DiscordRecording recording,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null);

    /// <summary>Asserts that at least one attempt of the sequence matches the route pattern and the predicate.</summary>
    /// <param name="requests">Recorded attempts, such as a slice of <see cref="DiscordRecording.Requests"/>; enumerated once.</param>
    /// <returns>The matching attempts in sequence order; never empty.</returns>
    public static IReadOnlyList<RecordedRestRequest> ShouldHaveRequested(
        this IEnumerable<RecordedRestRequest> requests,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null);

    /// <summary>Asserts that exactly one attempt of the sequence matches the route pattern and the predicate.</summary>
    public static RecordedRestRequest ShouldHaveRequestedOnce(
        this IEnumerable<RecordedRestRequest> requests,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null);

    /// <summary>Asserts that no attempt of the sequence matches the route pattern and the predicate.</summary>
    public static void ShouldNotHaveRequested(
        this IEnumerable<RecordedRestRequest> requests,
        string route,
        Func<RecordedRestRequest, bool>? predicate = null,
        [CallerArgumentExpression(nameof(predicate))] string? predicateExpression = null);
}
```

Les surcharges abrégées portent les mêmes `<param>` et `<exception>` que la première ; les XML
docs complètes sont à écrire par M1-04B (CS1591 en erreur).

Exemples d'usage dans des tests (pseudo-code, xUnit ; les routes de messages arrivent avec
M1-REST-03) :

```csharp
[Fact]
public async Task Greet_NewMember_SendsOneMessageInWelcomeChannel()
{
    await using var discord = FakeDiscord.Create();
    var guild = discord.Seed.Guild("Test");
    var welcome = guild.TextChannel("welcome");
    var general = guild.TextChannel("general");
    using var rest = discord.CreateRestClient();

    await new WelcomeService(rest).GreetAsync(welcome.Id, "bob");

    var post = discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{welcome.Id}/messages");
    Assert.Equal(HttpStatusCode.OK, post.StatusCode);
    Assert.Equal("Welcome bob!", post.RequestBody!.Value.GetProperty("content").GetString());

    discord.Recorded.ShouldNotHaveRequested($"POST /channels/{general.Id}/messages");
    discord.Recorded.ShouldNotHaveRequested("DELETE /channels/{channel_id}/messages/{message_id}");
}

[Fact]
public async Task Announce_ThreeChannels_SendsThreeAcceptedMessagesAfterSetup()
{
    await using var discord = FakeDiscord.Create();
    var guild = discord.Seed.Guild("Test");
    ulong[] channels = [guild.TextChannel("a").Id, guild.TextChannel("b").Id, guild.TextChannel("c").Id];
    using var rest = discord.CreateRestClient();
    await rest.SendMessageAsync(channels[0], new MessageProperties { Content = "setup" });
    var before = discord.Recorded.Requests.Count;

    await new Announcer(rest).AnnounceAsync(channels, "hello");

    // A slice scopes the assertion; the predicate keeps accepted attempts only.
    var sent = discord.Recorded.Requests.Skip(before).ShouldHaveRequested(
        "POST /channels/{channel_id}/messages", r => r.StatusCode == HttpStatusCode.OK);
    Assert.Equal(3, sent.Count);
}

[Fact]
public async Task SendMessage_RateLimitedOnce_IsTwoAttempts()
{
    await using var discord = FakeDiscord.Create();
    var channel = discord.Seed.Guild("Test").TextChannel("general");
    using var rest = discord.CreateRestClient();

    using (discord.Rest.Intercept("POST /channels/{channel_id}/messages", RestEffect.RateLimited(0)))
        await rest.SendMessageAsync(channel.Id, new MessageProperties { Content = "hi" });

    var attempts = discord.Recorded.ShouldHaveRequested($"POST /channels/{channel.Id}/messages");
    Assert.Equal(2, attempts.Count);
    Assert.Equal(HttpStatusCode.TooManyRequests, attempts[0].StatusCode);
    Assert.Equal(HttpStatusCode.OK, attempts[1].StatusCode);
    Assert.Throws<FakeDiscordAssertionException>(
        () => discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{channel.Id}/messages"));
}

[Fact]
public async Task React_AfterReply_ComesSecond()
{
    // … the bot replies, then reacts to its own message
    var reply = discord.Recorded.ShouldHaveRequestedOnce($"POST /channels/{channel.Id}/messages");
    var react = discord.Recorded.ShouldHaveRequestedOnce(
        $"PUT /channels/{channel.Id}/messages/{{message_id}}/reactions/👍/@me");

    // Returned records are the journal's own instances: reference identity gives their position.
    var journal = discord.Recorded.Requests.ToList();
    Assert.True(journal.IndexOf(reply) < journal.IndexOf(react));
}

[Fact]
public async Task ShouldNotHaveRequested_MisspelledRoute_IsAProgrammingError()
{
    await using var discord = FakeDiscord.Create();

    Assert.Throws<ArgumentException>(() => discord.Recorded.ShouldNotHaveRequested("POST /channel/{channel_id}/messages"));
    Assert.Throws<ArgumentException>(() => discord.Recorded.ShouldNotHaveRequested("POST /channels/{id}/messages"));
    Assert.Throws<ArgumentException>(() => discord.Recorded.ShouldNotHaveRequested("PUT /users/@me"));
}
```

Avec d'autres bibliothèques, et une assertion écrite par le test :

```csharp
// Shouldly
discord.Recorded.ShouldHaveRequestedOnce(route).StatusCode.ShouldBe(HttpStatusCode.OK);
// FluentAssertions / AwesomeAssertions
discord.Recorded.ShouldHaveRequested(route).Should().HaveCount(2);
// NUnit
Assert.That(discord.Recorded.ShouldHaveRequestedOnce(route).StatusCode, Is.EqualTo(HttpStatusCode.OK));

// A test's own Discord-level assertion, failing the same way.
static RecordedRestRequest ShouldHaveSentMessage(this DiscordRecording recorded, ulong channelId, string content)
    => recorded.ShouldHaveRequested($"POST /channels/{channelId}/messages")
        .FirstOrDefault(p => p.RequestBody is { } body
            && body.TryGetProperty("content", out var sent) && sent.GetString() == content)
       ?? throw new FakeDiscordAssertionException($"No message '{content}' was sent to channel {channelId}.");

// M2 (ADR 0007): the same methods on the requests attached to one interaction.
result.Requests.ShouldHaveRequestedOnce("PATCH /webhooks/{webhook_id}/{webhook_token}/messages/@original");
```

## Conséquences

- Un test affirme une tentative REST en une ligne, avec l'ID du seed interpolé, et un échec
  montre ce que le bot a réellement appelé. Une route mal écrite est signalée au lieu de
  réussir en silence.
- Le coût : un motif de route à expliquer (accolades simples ou doublées selon que la chaîne est
  interpolée) et un message d'échec à maintenir. Les tests qui veulent un critère que le motif
  ne couvre pas gardent LINQ sur `Recorded.Requests`.
- Les assertions comptent des tentatives : un test qui veut « un message accepté » le dit par le
  prédicat ou par l'état. C'est la distinction de l'ADR 0004 ; elle est rappelée dans la XML doc.
- `DiscordRecording` doit donner à l'assembly un accès **interne** à la table de son fake ;
  aucun membre public n'y est ajouté, et `RecordedRestRequest` ne change pas.
- Le dossier `Assertions/` de `architecture.md` reçoit : l'exception, `RecordedRequestAssertions`,
  le motif de route et le formateur (internes).
- Ni dépendance, ni réflexion, ni type NetCord dans les signatures : une évolution de NetCord ne
  touche pas ces assertions. Une évolution de l'inventaire (`/parity/sync`) peut rendre un motif
  invalide : le test échoue alors par `ArgumentException`, ce qui est le signal voulu.
- **Parité :** aucune ligne de `docs/parity/*` n'est promue, exclue ni modifiée. Les tests des
  assertions ne portent pas `[Parity]` : ils ne prouvent aucun comportement de Discord.

**Tâches impactées.**

- **M1-04B** (à préciser) : `FakeDiscordAssertionException` (+ interface interne
  `IAssertionException`), `RecordedRequestAssertions` (six méthodes), motif de route résolu par
  `RouteTable`, message d'échec du § 5, accès interne de `DiscordRecording` à la table. Tests :
  les trois méthodes sur les deux récepteurs, chaque ligne des tableaux des § 3 et § 4, route
  inventoriée sans handler, prédicat qui lève, fake fermé et liste détachée, retry après 429,
  éléments du message et plafonds. Fichiers : `src/Enx.NetCord.Testing/Assertions/**`,
  `Recording/DiscordRecording.cs`, `tests/Enx.NetCord.Testing.Tests/Assertions/**`. Dépend de
  M1-ADR-12 et M1-04 ; les tests peuvent lier des handlers de test à des clés de l'inventaire
  (comme `RecordedRequestsTests.CreateFake`), sans attendre M1-REST-03.
- **M2-03** : `InteractionResult.Requests` rend les instances du journal, pour que les méthodes
  du récepteur suite et l'identité de référence s'y appliquent.
- **M2-T-01** : le sample utilise ces assertions.
- **M6-01** : le guide des recettes montre la composition avec xUnit, NUnit, MSTest, Shouldly et
  FluentAssertions.

**Tâches à créer** (l'orchestrateur les ajoute à `docs/roadmap.md`) :

- `M2-14` — Assertions sur `InteractionResult` (« a répondu », modal, réponse refusée) :
  amendement de l'ADR 0012 puis implémentation — architect, puis interactions-implementer —
  M2-03, M1-04B.
- `M3-10` — Assertions Gateway sur le journal des frames et des dispatchs : amendement de
  l'ADR 0012 puis implémentation — architect, puis gateway-implementer — M3-ADR-09, M3-03,
  M1-04B.

## Questions ouvertes

Aucune ne bloque M1-04B. Trois choix d'ergonomie sont à confirmer à la revue, avant le passage
en « Acceptée » :

1. Le motif de route accepte des valeurs concrètes (§ 3). L'alternative est la clé de template
   seule, plus simple à expliquer et plus verbeuse à l'usage.
2. `ShouldHaveRequestedOnce` plutôt qu'un paramètre de comptage (§ 2).
3. L'interface interne `IAssertionException` pour xUnit v3 (§ 1) : un indice reconnu par son
   nom, sans dépendance, que l'on peut retirer sans changer l'API.
