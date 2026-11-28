# 0002 — Bibliothèque agnostique du framework de test ; xUnit v3 pour les tests du dépôt

- **Statut** : Acceptée
- **Date** : 2026-11-25
- **Auteur** : initialisation du projet
- **Tâches liées** : M0

## Contexte

Les utilisateurs de NetCord utilisent xUnit, NUnit, MSTest (NetCord lui-même : MSTest) ou TUnit.
Imposer un framework limiterait l'adoption.

## Options

- **A** — Lib liée à xUnit (fixtures, `IAsyncLifetime`) : ergonomie maximale pour xUnit, exclut
  les autres.
- **B** — Lib agnostique + packages d'intégration par framework : souple, plus de packages.
- **C** — Lib agnostique seule (retenue pour v1) : `FakeDiscord` est `IAsyncDisposable`, les
  assertions lèvent `FakeDiscordAssertionException` (reconnue par tous les runners comme un
  échec), compatible avec n'importe quelle lib d'assertions (FluentAssertions, Shouldly…).

## Décision

**C** pour `src/`. Les tests **du dépôt** utilisent **xUnit v3** + `FakeTimeProvider`
(`Microsoft.Extensions.TimeProvider.Testing`). Des packages d'intégration (B) pourront être
ajoutés plus tard si le besoin apparaît (ADR dédiée).

## Conséquences

- Aucune référence à un framework de test dans `src/` (vérifié par le reviewer).
- Le contrat de `FakeDiscordAssertionException` et les assertions de `src/` sont fixés par
  l'[ADR 0012](0012-agnostic-assertions.md).
- Le sample montre l'usage avec xUnit ; les guides donnent un équivalent NUnit/MSTest.
