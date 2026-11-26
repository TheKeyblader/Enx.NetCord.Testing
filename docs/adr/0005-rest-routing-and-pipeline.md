# 0005 — Table de routes, pipeline REST et interception

- **Statut** : Acceptée
- **Date** : 2026-11-26
- **Auteur** : agent `architect`
- **Tâches liées** : M1-ADR-03, M1-ADR-04, M1-ADR-05, M1-ADR-06, M1-03, M1-04, M1-REST-01 à M1-REST-09, M6-02

## Contexte

`GET /channels/123` peut échouer parce que le salon n'existe pas, parce que le bot n'y a pas
accès, ou parce que le fake ne sait pas traiter la route. Le test doit pouvoir distinguer ces
cas sans remplacer le `RestClient` du bot. Un défaut injecté ne doit pas créer de message
« fantôme » dans [l'état partagé](0003-state-model-and-concurrency.md).

Références vérifiées :

- NetCord `1.0.0-beta.30` @ `bb5f4e13`, `NetCord/Rest/{RestClientConfiguration,IRestRequestHandler}.cs` :
  `public IRestRequestHandler? RequestHandler { get; init; }` dans `NetCord.Rest.RestClientConfiguration`.
  `NetCord.Rest.IRestRequestHandler : IDisposable` expose `void AddDefaultHeader(string name, IEnumerable<string> values)`
  et `Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)`.
  `RestClient.cs:33–64,106–153` construit l'URI **absolue** `https://{Hostname}/api/v{N}{route}{query}` ;
  `User-Agent` et `Authorization` passent par `AddDefaultHeader`, pas nécessairement par `request.Headers`.
- `RestClient.cs:185–292,360–424` : NetCord lit les en-têtes de bucket, détecte un 429 via
  **la présence de `X-RateLimit-Scope`**, et lit les autres erreurs JSON si le type de média
  est `application/json`. `RestException.Error` expose alors `RestError` (`code` entier,
  `message`, `errors` facultatif ; `NetCord/Rest/{RestException,RestError}.cs`).
- Discord v10 : `discord-api-spec` @ `65423001`, `specs/openapi.json` (`paths`, `security`
  par opération, `ErrorResponse`, `RatelimitedResponse`, `components.headers`) ; `discord-api-docs`
  @ `62ab2d50`, `developers/reference.mdx` (authentification, exemples d'erreurs),
  `developers/topics/{opcodes-and-status-codes,rate-limits}.mdx`. La spec nomme les opérations ;
  les exemples Discord précisent le format sur le fil, même si NetCord tolère une autre forme.

## Options

### A — Table limitée aux handlers écrits

Une ligne `Map("GET", "/users/{user_id}", …)` par handler, sans autre inventaire.
Peu de code, mais si `/users/@me` n'est pas encore implémentée, `@me` peut tomber dans le
handler d'ID. Une méthode manquante dans le fake est indiscernable d'une méthode interdite.

### B — Routeur OpenAPI dynamique et middleware HTTP public

Charger la spec au démarrage ; permettre au test de remplacer librement la réponse ou
d'appeler le handler suivant. Flexible, mais la spec ne sait ni créer un message ni produire
son `StateChange`. Le bot pourrait recevoir un succès sans commit, ou deux commits pour
un seul POST. Le coût du parseur et l'API de middleware seraient à maintenir même en AOT.

### C — Inventaire statique, handlers explicites, effets de test bornés

Séparer « Discord connaît cette opération » de « le fake l'implémente ». Utiliser la même
clé pour le routage, la matrice et l'interception ; n'exposer ni le routeur ni le transport.
C'est l'option retenue.

## Décision

### Table et résolution

`RouteTable` est interne et immuable. M1-03 embarque un petit inventaire C# **dérivé hors ligne**
des opérations de la spec épinglée : méthode, template exact, `operationId`, alternatives
d'authentification. Les handlers, enregistrés explicitement par clé, reçoivent les paramètres
nommés (ex. `channel_id`) ; l'absence de handler est un état normal. Ni téléchargement,
ni lecture de Markdown/OpenAPI au runtime, ni découverte de handlers par réflexion.
Les routes NetCord hors spec ont une liste supplémentaire explicite : template nommé, source
NetCord et justification dans [la matrice REST](../parity/rest.md). Pas de joker `{}` au runtime.
Lors du `/parity/sync`, régénérer et comparer le jeu `(méthode, template, operationId, sécurité)` ;
contrôler séparément les exceptions hors spec et leurs clés de matrice. Un binding sans entrée,
un doublon ou une ambiguïté fait échouer le contrôle de la table, pas le premier appel du bot.

1. Sur l'URI de l'hôte sentinelle, isoler le chemin échappé, vérifier puis retirer **un seul**
   préfixe `/api/v{N}` (v10 pour M1). Séparer la query avant tout décodage ; conserver
   les valeurs répétées. Découper sur les `/` réels **puis** décoder chaque segment une fois :
   `%40me` = `@me`, `%2F` reste une donnée d'un seul paramètre et `%2540me` ne devient pas
   `@me`. Pas de normalisation de casse ni de suppression de segments vides. Un autre hôte
   est une erreur de configuration locale ; jamais de repli sur un `HttpClient` réseau.
2. Chercher les templates qui correspondent **au chemin entier** : littéral ordinal exact ou
   `{paramètre}` pour un segment non vide. À la première position qui les distingue, le littéral
   l'emporte. À spécificité égale, deux formes qui se recouvrent mais ne diffèrent que par
   le nom des paramètres sont interdites. Aucun départage par ordre d'inscription.
3. Sur le template gagnant seulement, chercher la méthode. Pas de repli sur `{user_id}` si
   `/users/@me` gagne ; pas de GET implicite pour HEAD. Chemin connu mais méthode absente de
   l'inventaire : **405**, `code: 0`, dans les deux modes (`Allow` liste les méthodes
   inventoriées du chemin). Aucun chemin correspondant ou
   opération inventoriée sans handler : **non implémentée**. Une valeur d'ID invalide est
   une question de validation de l'opération, pas une raison d'essayer une autre route.

### Pipeline d'une tentative de transport

1. **Authentification.** Fusionner les valeurs par défaut propres au handler de **ce client**
   avec celles de la requête (ces dernières priment, noms insensibles à la casse). Copier
   les valeurs de `AddDefaultHeader` ; libérer un client ne détruit ni les autres ni l'état.
   Identifier l'appelant (`Bot`, `Bearer`, absent ou invalide). Ne pas exiger globalement un
   header : c'est la politique de l'opération résolue qui décide si l'anonyme est autorisé.
2. **Résolution**, puis application des alternatives d'auth de l'entrée, avant validation ou
   interception. Route protégée, token absent/inconnu : `401`, code `0`. N'accepter qu'une
   identité connue du fake, et seulement pour le schéma permis ; le préfixe `Bearer` ne
   transforme pas le token du bot en jeton OAuth2. M1 fournit l'identité Bot ; une identité
   Bearer et ses scopes doivent être explicitement modélisés avant d'accorder l'accès.
   Le callback d'interaction accepte l'absence de header (`{}` dans `security`) : le token
   **de chemin** relève des règles du handler. Une permission métier manquante n'est pas un 401.
   L'absence de chemin ou de méthode se traite à la résolution, sans inventer de politique
   pour l'opération manquante. Pour une opération inventoriée, appliquer sa politique d'auth
   même si le handler manque ; une fois l'appel autorisé, traiter l'absence de handler.
3. **Validation structurelle** de la requête résolue et implémentée : paramètres, query,
   type de contenu attendu par l'opération, JSON ou multipart (`payload_json`, `files[n]`).
   Lire les flux une seule fois, hors verrou. Appliquer les contraintes de l'opération ;
   produire les détails de champ quand Discord le fait, pas une exception STJ exposée au bot.
4. **Interception**, puis **handler** si l'effet ne termine pas la tentative. Le handler demande
   à `FakeDiscordState` une seule opération cohérente. Sous son verrou : résolution des
   références et contrôles dépendant de l'état (existence, accès, permissions, limites),
   lecture détachée ou commit et mise en file des faits métier, selon l'ADR 0003. Une
   validation préliminaire hors verrou ne dispense jamais de ces contrôles. L'état renvoie
   une valeur ou un échec métier ; il ne construit ni réponse HTTP ni dispatch Gateway.
5. **Sérialisation**, hors verrou : statut de succès de l'opération (pas de `201` universel
   pour POST), JSON Discord ou aucun corps pour `204`. Pour un échec, appliquer le catalogue
   ci-dessous. L'[ADR 0006](0006-wire-serialization.md) fixe les DTO, lecteurs et contextes source-generated.
6. **En-têtes de rate limit**, puis **enregistrement** avant retour ou propagation. Une issue
   HTTP anticipée saute au point 5, sans exécuter les étapes suivantes du métier. Une faute
   locale (dont le mode Strict) ou une annulation saute au journal, **sans réponse HTTP fictive**.
   Pas d'attente, de sérialisation ni de journalisation sous le verrou de l'état. Vérifier
   l'annulation avant commit ; une annulation après commit ne l'annule pas.

**Contrat producteur de `Recorded` (M1-04).** Une entrée terminale détachée **par `SendAsync`**,
retries compris : méthode, chemin expurgé, template résolu et paramètres si connus, query, corps de requête
lu, issue (réponse / non implémentée / faute / annulation), indication d'effet appliqué,
statut, en-têtes et corps de réponse s'il y en a. Distinguer « non lu » de « vide » ; ne
pas consommer un flux uniquement pour journaliser un rejet d'auth. Le 404 Lenient garde le
marqueur non implémenté. Ni `HttpContent`, ni `JsonElement` lié à un document libéré, ni
référence à l'état. Masquer `Authorization` et les tokens de chemin/query dans le journal
et les exceptions ; si le chemin est inconnu, ne pas supposer ses segments publiables.
L'[ADR 0004](0004-fake-discord-public-api.md) fixe la projection publique et son ordre,
pas l'ordre de commit. Le marqueur d'issue, la présence d'une interception et les en-têtes
de réponse restent des informations internes ; le contrat producteur n'ajoute pas de membres
à `RecordedRestRequest`.

### `DiscordError` et mode des routes non implémentées

`DiscordError` est un catalogue **interne** de fabriques, pas une hiérarchie d'exceptions.
Chaque résultat fixe le statut HTTP et le corps à sérialiser en `application/json`. Exemples :

| Cause | HTTP | Corps JSON |
| --- | --- | --- |
| Route non implémentée, mode Lenient | 404 | `{"message":"404: Not Found","code":0}` |
| Méthode absente sur un chemin connu (`Allow` provient des méthodes inventoriées pour ce chemin) | 405 | `{"message":"405: Method Not Allowed","code":0}` |
| Auth requise non satisfaite | 401 | `{"message":"401: Unauthorized","code":0}` |
| `UnknownChannel()` / `UnknownMessage()` | 404 | `{"message":"Unknown Channel","code":10003}` / `{"message":"Unknown Message","code":10008}` |
| Permission manquante | 403 | `{"message":"Missing Permissions","code":50013}` |
| JSON syntaxiquement invalide | 400 | `{"message":"The request body contains invalid JSON.","code":50109}` |
| Champ invalide dans un JSON lisible | 400 | `{"message":"Invalid Form Body","code":50035,"errors":{"content":{"_errors":[{"code":"BASE_TYPE_MAX_LENGTH","message":"Must be 2000 or fewer in length."}]}}}` |

Construire `errors` à partir des chemins de validation : objets par nom de propriété ou indice
décimal (`"0"`), liste `_errors` de couples code **texte** / message à la feuille, éventuellement
à la racine. Pas d'`errors: null` pour une erreur simple. Les exemples Discord priment sur la
référence à `Error` (code entier) de `InnerErrors.items` dans la spec épinglée. Un handler ne
compose pas du JSON à la main. Ajouter la fabrique, sa ligne dans [errors.md](../parity/errors.md)
si absente et le test correspondant ensemble. Le 429 a son propre format, ci-dessous.

La politique de routes non implémentées est une option **par `FakeDiscord`**, lue à sa création
depuis `FakeDiscordOptions.UnimplementedRoutes`. **Strict** :
journaliser puis lever `NotSupportedException` avec la méthode et le template s'il est connu.
**Lenient** (défaut) : rendre le 404 code `0`, observable par le code du bot sans activer
de diagnostic propre au fake. Ce choix n'affecte **jamais** le résultat d'un handler :
un salon absent reste un 404 `10003` même en Strict. Ni une exception interne ni une erreur du
générateur d'IDs ne doivent être déguisées en réponse Discord.

### Interception et en-têtes

Une règle vise **la prochaine tentative éligible** d'une opération implémentée, pour ce fake
et tous ses clients. Clé exacte `METHOD /template` de la table, pas d'URL, d'ID concret, de
query, de prédicat ni de joker. Une clé invalide/non liée est rejetée à l'inscription
(`ArgumentException`), une deuxième règle en attente sur la même clé aussi
(`InvalidOperationException`). Après `DisposeAsync` du fake, `Intercept` lève
`ObjectDisposedException`, même via une référence à `discord.Rest` conservée avant la fermeture.
Prendre et retirer la règle atomiquement après validation
structurelle, **avant** toute mutation. `Dispose()` désarme une règle non consommée, de façon
idempotente et sans affecter une règle ultérieure. Une fois prise, elle reste consommée,
même si la tentative est annulée ; entre appels concurrents, le test doit séquencer si le
gagnant importe. Erreur ou 429 : court-circuit sans fait métier. Délai : attendre via le
`TimeProvider` du fake et le `CancellationToken`, puis exécuter le handler normal **une fois**.

**Barrière d'armement du délai.** Pour un délai positif, l'effet consommé crée un timer à
échéance finie via `TimeProvider.CreateTimer`, déjà armé au retour de cet appel. Il n'arme
rien à l'inscription de la règle. Une lecture multipart peut céder la main avant l'interception :
recevoir la tâche NetCord, même incomplète, ne prouve donc pas que le délai a commencé.
Un test peut observer l'armement sur son horloge injectée (exemple ci-dessous), puis avancer
le temps. Cette barrière ne requiert ni polling ni nouvelle API publique du fake.

En M1, **aucun quota**. Sur les réponses des opérations résolues (y compris les erreurs),
émettre les cinq en-têtes de `components.headers` avec des nombres en culture invariante :

| En-tête | Réponse ordinaire | 429 injecté (par utilisateur) |
| --- | --- | --- |
| `X-RateLimit-Bucket` | SHA-256 en hexadécimal minuscule des octets UTF-8 de `METHOD /template`, sans ID ni token | identique |
| `X-RateLimit-Limit` / `X-RateLimit-Remaining` | `5` / `4`, sans décrément | `5` / `0` |
| `X-RateLimit-Reset-After` | `0` | `s` secondes |
| `X-RateLimit-Reset` | `t` | `t + s` |
| `Retry-After` / `X-RateLimit-Scope` | absents | `s` / `user` |

`t` = heure virtuelle de réponse en secondes Unix à la milliseconde ; `s` = entier fourni
au contrôle de test. Ne pas inventer de bucket si aucune opération n'a été résolue. Jamais de
`X-RateLimit-Global` en M1. Corps du 429 : `{"message":"You are being rate limited.","retry_after":2,"global":false}`
pour `s = 2`. `RatelimitedResponse` hérite de `Error` dans la spec épinglée et y rend donc
`code` obligatoire. Sur le fil, c'est l'exemple Discord du quota par utilisateur qui fait foi :
`developers/topics/rate-limits.mdx:59,76–80` réserve `code?` à certaines limites et l'omet
dans cet exemple. Le fake n'en invente pas pour ce 429. Ces valeurs ne sont pas des quotas de production.

**Pas de sommeil caché côté client.** `RestClient.cs:400` convertit `Reset-After` de secondes
en millisecondes ; son limiteur global par défaut compte 50 appels par seconde **avant** le
transport, avec `Environment.TickCount64` et des `Task.Delay` non pilotables par l'horloge du
fake. Les clients créés pour le fake reçoivent donc, via
`public IRateLimitManager? RateLimitManager { get; init; }`, un gestionnaire interne qui
accorde toute acquisition et n'arme aucun timer (interfaces publiques
`NetCord.Rest.RateLimits` dans `NetCord/Rest/RateLimits/`). NetCord **continue de lire**
les en-têtes. Avec `RestRateLimitHandling.NoRetry`, le 429 injecté lève
`RestRateLimitedException` ; avec son réglage `Retry` par défaut, le scope `user` permet
une nouvelle tentative immédiate, sans règle restante. Pas de scope `shared` en M1 :
NetCord attendrait lui-même le `Retry-After`. Les quotas stateful, buckets partagés, délais
de retry réalistes et limites globales opt-in relèvent de M6-02.

## API publique proposée

Seuls ces deux membres complètent la façade et les options de l'ADR 0004 (extraits de
signature, pas de nouvelle classe d'options). Pas de delegate utilisateur, de type de
route public ni d'accès au verrou. `FakeDiscordRest` conserve le préfixe de `FakeDiscord`
car il contrôle le comportement du faux transport ; `DiscordSeed` et `DiscordRecording`
servent à préparer et à observer le scénario.

```csharp
// FakeDiscord
/// <summary>Gets the REST controls bound to this fake.</summary>
public FakeDiscordRest Rest { get; }

// FakeDiscordOptions
/// <summary>Gets the policy for unimplemented REST routes. Defaults to Lenient.</summary>
public UnimplementedRouteMode UnimplementedRoutes { get; init; }
```

```csharp
namespace Enx.NetCord.Testing;

/// <summary>Specifies how the fake reports a missing REST operation.</summary>
public enum UnimplementedRouteMode
{
    /// <summary>Returns a Discord 404 with error code zero.</summary>
    Lenient = 0,
    /// <summary>Records the attempt and throws a local NotSupportedException.</summary>
    Strict = 1
}

/// <summary>Controls REST fault injection for one fake Discord instance.</summary>
public sealed class FakeDiscordRest
{
    internal FakeDiscordRest(/* fake-owned registration store */);

    /// <summary>Arms one effect for the next eligible request to an implemented route.
    /// Dispose the returned registration to disarm it before use.</summary>
    /// <exception cref="ObjectDisposedException">The fake has been disposed.</exception>
    public IDisposable Intercept(string route, RestEffect effect);
}

/// <summary>Describes a one-shot REST test effect.</summary>
public sealed class RestEffect
{
    private RestEffect();

    /// <summary>Returns a terminal error from the fake's Discord error catalog.</summary>
    /// <exception cref="ArgumentException">The status and code do not identify a supported simple error.</exception>
    public static RestEffect Error(System.Net.HttpStatusCode status, int code = 0);

    /// <summary>Returns a per-user HTTP 429 with the specified Retry-After in seconds.</summary>
    public static RestEffect RateLimited(int retryAfterSeconds);

    /// <summary>Delays normal handling using the fake's clock.</summary>
    public static RestEffect Delay(TimeSpan duration);
}
```

`Error` accepte les couples statut/code simples du catalogue, dont 500/0 et 502/0 pour
tester une panne serveur. Lever `ArgumentException` dès la construction pour les combinaisons inconnues, `50035`
(détails nécessaires) et tout 429 ; ne pas demander au test d'écrire du JSON. `RateLimited`
accepte de 0 à 2 147 483 secondes incluses (`ResetAfter` de NetCord est un `int` de
millisecondes). `Delay` accepte un délai fini non négatif supporté par le timer du fake.

Exemple de test (pseudo-code). `TimerProbe` est un décorateur **de test** : il délègue
les lectures de temps à l'horloge virtuelle. Ce scénario isolé n'arme qu'un seul timer.

```csharp
sealed class TimerProbe(TimeProvider clock) : TimeProvider
{
    private readonly TaskCompletionSource _armed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Armed => _armed.Task;
    public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();
    public override long GetTimestamp() => clock.GetTimestamp();
    public override long TimestampFrequency => clock.TimestampFrequency;
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = clock.CreateTimer(callback, state, dueTime, period);
        if (dueTime != Timeout.InfiniteTimeSpan) _armed.TrySetResult();
        return timer;
    }
}
```

```csharp
var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
var probe = new TimerProbe(clock);
await using var discord = FakeDiscord.Create(new FakeDiscordOptions
{
    TimeProvider = probe, UnimplementedRoutes = UnimplementedRouteMode.Strict
});
using var rest = discord.CreateRestClient();
await Assert.ThrowsAsync<NotSupportedException>(
    () => rest.SendRequestAsync(HttpMethod.Get, $"/this-route-does-not-exist"));
Assert.Null(discord.Recorded.Requests.Last().StatusCode);

const string route = "GET /users/@me";

using (discord.Rest.Intercept(route, RestEffect.Error(HttpStatusCode.Forbidden, 50013)))
{
    var denied = await Assert.ThrowsAsync<RestException>(() => rest.GetCurrentUserAsync());
    Assert.Equal(50013, denied.Error!.Code);
}

using (discord.Rest.Intercept(route, RestEffect.RateLimited(2)))
{
    var once = new RestRequestProperties { RateLimitHandling = RestRateLimitHandling.NoRetry };
    var limited = await Assert.ThrowsAsync<RestRateLimitedException>(() => rest.GetCurrentUserAsync(once));
    Assert.Equal(2000, limited.ResetAfter);
}

using (discord.Rest.Intercept(route, RestEffect.Delay(TimeSpan.FromSeconds(3))))
{
    Assert.False(probe.Armed.IsCompleted);
    var pending = rest.GetCurrentUserAsync();
    Assert.Same(probe.Armed, await Task.WhenAny(probe.Armed, pending));
    Assert.False(pending.IsCompleted);
    clock.Advance(TimeSpan.FromSeconds(3));
    Assert.NotNull(await pending);
}
```

Le même GET sur un fake créé avec `UnimplementedRoutes = UnimplementedRouteMode.Lenient`
lève, côté client NetCord, `RestException` avec `StatusCode == HttpStatusCode.NotFound`
et `Error.Code == 0` ; le statut enregistré est alors `404`, pas `null`.

## Conséquences

- La même clé explique un échec, nomme le test de conformité et cible une panne. Le prix
  est un inventaire à synchroniser et une interception volontairement incapable d'inventer
  un succès, de masquer une route absente ou de cibler un seul salon.
- M1-03 vérifie notamment `%40me`, la non-redécoupe de `%2F`, les conflits, 405 vs route
  non implémentée, auth Bot vs anonyme, l'indépendance des clients et l'absence de délai
  global en rafale. Pour le délai, M1-03 suspend la lecture d'un corps valide avant interception,
  vérifie qu'aucun timer n'est armé, libère la lecture puis attend la barrière d'armement.
  Une avance inférieure à l'échéance ne doit pas lancer le handler ; atteindre l'échéance
  doit permettre son unique exécution, sans attente murale.
  M1-04 vérifie la publication (y compris Strict, rejet et annulation)
  et la distinction entre tentative interceptée et retry normal. Chaque famille M1-REST
  valide ses succès, refus et erreurs de champ via le vrai client NetCord.
- **Parité :** prépare les clés `METHOD /template` de `rest.md` et `0`, `10003`, `10008`,
  `50035`, `http:401`, `http:403`, `http:404`, `http:405`, `http:429` et `http:5xx` de
  `errors.md` ; y ajouter `50109` quand le décodage de JSON mal formé sera testé. Aucune ligne promue
  ni exclue. Un `Intercept` qui force `50013` ne prouve pas que le fake calcule une permission ;
  un 429 injecté ne prouve pas un quota M6-02.
- Pas de nouvelle tâche : inventaire et branchement du gestionnaire font partie du noyau
  M1-03 et de la création des clients ; les implémentations M1-04, M1-REST et M6-02 restent
  distinctes. Aucune dépendance réseau, runtime ou à un framework de test ajoutée.
