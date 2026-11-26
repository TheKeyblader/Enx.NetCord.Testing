# 0006 — Sérialiser le contrat Discord, lire avec les vrais clients NetCord

- **Statut** : Acceptée
- **Date** : 2026-11-26
- **Auteur** : agent `architect`
- **Tâches liées** : M1-ADR-06, M1-ADR-03, M1-01, M1-03, M1-ADR-05, M1-REST-03, M1-T-01, M2-ADR-07

## Contexte

Un `RestMessage` lisible ne prouve pas que le JSON est fidèle. Ainsi, `MessageResponse`
exige `timestamp` (chaîne `date-time`) **et** `edited_timestamp` (chaîne `date-time` ou `null`).
Le premier est commenté dans `NetCord/JsonModels/JsonMessage.cs`. Inversement, `webhook_id`
est facultatif, mais n'admet pas `null`. Sources : `discord-api-spec` @ `65423001`,
`specs/openapi.json`, schémas `MessageResponse` et `SnowflakeType` ; `discord-api-docs`
@ `62ab2d50`, `developers/resources/message.mdx` et `developers/reference.mdx`.
L'[ADR 0003](0003-state-model-and-concurrency.md) fournit des valeurs détachées au commit :
ni le JSON ni les `JsonModels` mutables ne sont l'état du serveur.

Parcours vérifié dans `netcord`, tag `1.0.0-beta.30` @ `bb5f4e13` :

- `NetCord.Rest.RestClient.SendMessageAsync(ulong channelId, MessageProperties message, RestRequestProperties? properties = null, CancellationToken cancellationToken = default)`
  renvoie `Task<RestMessage>`. `MessageProperties.Serialize()` utilise
  `Serialization.Default.MessageProperties` ; `IMessageProperties.Serialize` construit un
  `JsonContent<T>` muni d'un `JsonTypeInfo<T>`, ou un `MultipartFormDataContent` avec ce
  JSON sous `payload_json` et les fichiers sous `files[n]`
  (`NetCord/Rest/{RestClient.Channel,MessageProperties,IMessageProperties,JsonContent}.cs`).
- Le seam public `NetCord.Rest.IRestRequestHandler.SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)`
  renvoie `Task<HttpResponseMessage>`. Au retour, `SendMessageAsync` et `GetMessageAsync`
  appellent `ToObjectAsync(Serialization.Default.JsonMessage)` puis construisent `RestMessage`.
  NetCord fait donc **sa propre** désérialisation ; le fake n'installe rien côté client
  (`NetCord/Rest/{IRestRequestHandler,RestClient.Channel}.cs`, `NetCord/Utils/SerializationUtils.cs`).
- Le contexte source-generated est **`internal`**, `NetCord.Serialization`, pas un contexte
  public dans `NetCord.JsonModels`. `public static JsonSerializerOptions NetCord.Discord.SerializerOptions`
  expose ses options, pas ses propriétés typées (`NetCord/{Serialization,Discord}.cs`).
  Les noms du protocole sont explicites (`[JsonPropertyName("channel_id")]`), sans politique
  globale camelCase. Le `UInt64Converter` de ce contexte lit les chiffres d'un ID chaîne ou
  nombre, mais **écrit un nombre**. De plus, `JsonComponent.JsonComponentConverter.Write`
  lève `NotImplementedException` (`NetCord/JsonConverters/UInt64Converter.cs`,
  `NetCord/JsonModels/JsonComponent.cs`). Ces métadonnées sont adaptées au **client**, pas
  un contrat serveur bidirectionnel.

## Options

### Option A — Sérialiser les `NetCord.JsonModels` en réponse

Peu de types à définir. Mais `JsonMessage` perd `timestamp` et le convertisseur des IDs écrit
le mauvais type JSON pour `SnowflakeType`. Un contexte maison autour de `JsonMessage` ne crée
pas la propriété manquante ; le sous-classer ne corrige ni les messages imbriqués, ni les
convertisseurs à sens unique. Une mise à jour du client changerait le serveur implicitement.

### Option B — Types propres partout, même à la frontière du client

Des DTO par forme Discord donnent la maîtrise de chaque champ. Refaire aussi la lecture en
objets d'interaction ou les modèles du client dupliquerait cependant une partie de NetCord et
ne testerait plus son interprétation réelle.

### Option C — DTO propres sur le fil, `JsonModels` aux points d'entrée NetCord

Le fake écrit le contrat du serveur ; NetCord lit ce contrat. On maintient les formes que le
fake produit, **pas** un miroir de tout OpenAPI, ni deux sérialiseurs interchangeables.

## Décision

**Option C.** La direction des données décide du type à employer :

| Frontière | Contrat |
| --- | --- |
| État → réponse REST / dispatch Gateway / requête HTTP d'interaction | Projeter hors verrou la valeur détachée (ou le fait de commit) vers un DTO interne, puis vers des octets JSON UTF-8. Partager les formes communes, pas les enveloppes : les ajouts propres à un dispatch ne deviennent pas des champs de la réponse REST. |
| JSON Discord → client | Laisser le vrai client utiliser son contexte. Pour la voie directe sans transport, le seam public `NetCord.Interaction.CreateFromJson(JsonInteraction jsonModel, Guild? guild, InteractionResponseDelegate sendResponseAsync, RestClient client)` attend justement un `NetCord.JsonModels.JsonInteraction` (`NetCord/Interaction.cs`). Il peut être construit à la frontière ou lu depuis le payload commun avec le `JsonTypeInfo<JsonInteraction>` obtenu via `Discord.SerializerOptions.GetTypeInfo(typeof(JsonInteraction))` (cast typé, racine enregistrée dans `NetCord/Serialization.cs`). M2-ADR-07 fixe le parcours ; aucune modification des options NetCord. |
| Requête NetCord → état | Lire les **octets réellement envoyés**, pas l'objet `MessageProperties`. Le décodeur de la route produit un input métier ou un échec de forme avant toute mutation. |

**Règles d'écriture.**

1. Un contexte interne dans `Serialization/` référence statiquement les racines effectivement
   utilisées (DTO, tableaux/listes renvoyés comme racines, variantes d'unions). Propriétés C#
   en PascalCase ; `[JsonPropertyName]` fixe chaque nom JSON à l'orthographe Discord, en général
   `snake_case` (`channel_id`, et non `channelId`). Pas de transformation des clés de
   dictionnaires. Les enums numériques du protocole restent des nombres.
2. Un Snowflake reste `ulong` dans l'état ; **dans le DTO de sortie**, projeter vers `string`
   par `ToString(CultureInfo.InvariantCulture)`, y compris pour les tableaux et les clés de
   dictionnaires. Aucun passage par `double`, aucun `WriteAsString` global qui changerait aussi
   `type`, `flags` ou les compteurs. Les autres types suivent leur propre schéma.
3. Une date `date-time` est un `DateTimeOffset` UTC, écrit par STJ en ISO 8601 avec décalage
   explicite. `MessageResponse` **propre au fake** projette `timestamp` depuis `CreatedAt`
   capturé à la création, `edited_timestamp` depuis la dernière édition ou `null`.
   À la sérialisation, ne pas lire l'horloge ni recalculer la date depuis l'ID. Pas de sous-classe de `JsonMessage`.
   Les champs explicitement définis en temps Unix ne passent pas par cette règle `date-time`.
4. Respecter deux axes indépendants : champ requis ou facultatif, valeur nullable ou non.
   Pas d'omission globale des valeurs nulles ou par défaut. Pour un message neuf, émettre
   `"edited_timestamp":null`, `"tts":false` et `"attachments":[]` ; omettre `webhook_id`
   en l'absence de webhook (`WhenWritingNull` sur **ce** champ). Si les trois états ont un
   sens, conserver la présence séparément et écrire la propriété seulement si présente
   (ex. `referenced_message` absent, nul ou message). Une propriété C# nullable seule ne suffit pas.
5. Une union `oneOf` conserve la variante du contrat, pas le type CLR découvert à l'exécution.
   Sélection explicite par discriminant Discord (`type` pour un composant) ou par nature du
   jeton (`nonce` entier ou chaîne), puis lecture/écriture d'une variante connue. Pas de
   `$type` ajouté. Pour les cas non représentables par attributs, utiliser un
   `JsonConverter<T>` fermé, référencé statiquement, avec `Utf8JsonReader`/`Utf8JsonWriter`
   ou délégation aux `JsonTypeInfo<T>` générés des variantes ; jamais de repli sur `object`.

**Règles de lecture REST.** Le handler extrait selon le `Content-Type` le corps JSON ou,
pour les uploads de messages, les parties du `multipart/form-data`. Décoder `payload_json`
avec le **même** lecteur de route que le JSON direct ; distinguer une partie absente d'un
`null` JSON. Identifier `files[n]` par le `name` de `Content-Disposition`, pas par son ordre ;
conserver nom de fichier et contenu binaire séparément. Rapprocher l'indice `n` de
`attachments[].id` lorsqu'il est fourni. Ce placeholder d'upload peut être l'entier `0`
dans la requête (exemple Discord, `developers/reference.mdx`, et
`NetCord/Rest/AttachmentProperties.cs`) ; ce n'est pas l'ID attribué en réponse.
Ne pas généraliser cette tolérance numérique à tout le schéma. Les formulaires sans
`payload_json` et les autres noms de parties se traitent selon la route qui les expose.

Lire la forme avec `JsonDocument`/`JsonElement` (`TryGetProperty` puis `ValueKind`, notamment
pour PATCH) ou un DTO d'entrée à métadonnées générées si la présence reste distinguable.
Ne pas conserver de `JsonElement` lié à un document libéré. JSON mal formé, type incorrect,
variante invalide ou parties incohérentes donnent un échec interne portant un chemin de champ
(ou la racine) et une raison stable. M1-ADR-05 possède la traduction en erreur HTTP Discord
(dont `50035`), la validation métier et l'enregistrement ; pas de réponse HTTP ni de fuite de
`JsonException.Message` depuis le décodeur. Une annulation se propage ; un défaut de
métadonnées en sortie est un bug du fake, pas une faute de requête du bot.

## API publique proposée

**Aucune.** Les DTO, le contexte et les convertisseurs sont internes ; ni la façade
(M1-ADR-04) ni le bot n'ont à fournir des `JsonSerializerOptions`. Squelette interne
illustratif, sans implémentation ; `MessageResponse` désigne le DTO complet du fake :

```csharp
namespace Enx.NetCord.Testing.Serialization;

/// <summary>Provides generated metadata for Discord wire payloads emitted by the fake.</summary>
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(MessageResponse))]
[JsonSerializable(typeof(MessageResponse[]))]
internal sealed partial class FakeDiscordJsonContext : JsonSerializerContext;
```

L'écriture utilise par exemple
`JsonSerializer.SerializeToUtf8Bytes(dto, FakeDiscordJsonContext.Default.MessageResponse)`.
Pas de `Serialize(value)` sans métadonnées, de resolver réflexif, de construction de génériques
à l'exécution ni d'accès aux membres internes de NetCord. `IsAotCompatible=true` et
`TreatWarningsAsErrors=true` sont déjà activés pour `src/` dans `Directory.Build.props`.

Exemple de test de contrat (pseudo-code M1-T-01) : le journal de l'ADR 0004 expose la
réponse JSON détachée, y compris les champs que `RestMessage` ne permet pas de vérifier.

```csharp
var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
await using var discord = FakeDiscord.Create(new FakeDiscordOptions { TimeProvider = clock });
var channelId = discord.Seed.Guild("Test").TextChannel("general").Id;
using var rest = discord.CreateRestClient();
var sent = await rest.SendMessageAsync(channelId, new MessageProperties { Content = "hello" });
var json = discord.Recorded.Requests.Single().ResponseBody!.Value;
Assert.Equal(sent.Id.ToString(CultureInfo.InvariantCulture), json.GetProperty("id").GetString());
Assert.Equal(clock.GetUtcNow(), json.GetProperty("timestamp").GetDateTimeOffset());
Assert.Equal(JsonValueKind.Null, json.GetProperty("edited_timestamp").ValueKind);
Assert.False(json.TryGetProperty("webhook_id", out _));
var read = await rest.GetMessageAsync(channelId, sent.Id);
Assert.Equal("hello", read.Content);
```

## Conséquences

- On maintient la projection des champs côté serveur. En échange, une évolution de `JsonMessage`
  ne modifie pas à elle seule le JSON du fake. Lors d'une synchro, comparer le schéma épinglé
  et les octets émis, puis vérifier la lecture par le client de la version NuGet ciblée.
- M1-03 et M1-REST-03 fournissent le contexte et les premières formes ; M1-T-01 vérifie
  JSON **et** lecture par le vrai `RestClient`, avec ID supérieur à `2^53`, null/absent,
  édition, JSON/multipart et corps invalide sans mutation. Vérifier les analyseurs trimming/AOT
  sans suppression de diagnostic et exécuter ce parcours avec réflexion STJ désactivée dans
  un hôte de test publié Native AOT : une compilation ordinaire ne prouve pas ce dernier point.
- **Parité :** prépare `POST /channels/{channel_id}/messages`,
  `GET /channels/{channel_id}/messages/{message_id}`, `50035` puis `MESSAGE_CREATE` et
  `INTERACTION_CREATE`. Aucune ligne promue ni exclue par cette ADR ; seules une implémentation
  et ses preuves `[Parity]` justifient de changer les matrices. Aucune nouvelle tâche de roadmap.
