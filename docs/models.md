# Modèles par agent

Choix révisé le 2026-11-26, à partir des modèles disponibles dans opencode pour ce projet et de
benchmarks publiés (sources en bas de page).

## Fournisseurs disponibles

| Provider opencode | Type | Modèles pertinents |
| --- | --- | --- |
| `claude-agent` | abonnement Claude (quota) | Fable 5.2 (`fable`), Opus 5.5 (`opus`), Sonnet 5.5 (`sonnet`), Haiku 5.5 (`haiku`) |
| ~~`openai`~~ | ~~abonnement ChatGPT/Codex~~ — **plus disponible depuis le 2026-11-28** | (GPT-6.2 Astra, GPT-6.2 Sol, GPT-6.1 Terra, GPT-6 Luna : chiffres gardés pour comparaison) |
| `opencode` (Zen) | **payé au token — non utilisé** | Gemini 4 Neon ($3/$15), et les mêmes Claude/GPT au prix API |
| `opencode-go` | abonnement Go (modèles ouverts surtout) | Gemini 4 Argon, Kimi K3.1 Pro, Qwen4 Max, DeepSeek V4.1 Flash… |

## Comparatif (novembre 2026)

| Modèle | FrontierCode 2.0 (modifs merge-ready) | Terminal-Bench 4.1 (agent terminal) | SWE-Atlas-QnA (questions sur une codebase) | AA Coding Agent Index | BenchLM coding | Coût/tâche AA | Vitesse |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Claude Fable 5.2 | **49.1 %** | – | – | 58.24 (high) / 58.47 (xhigh) | **92.3** | $7.51 (high) – $26.54 (max) | 112–127 tok/s |
| Claude Opus 5.5 | 47.3 % | **65.8 %** | – | 58.16 (max) | 88.3 | $10.04 | **117 tok/s** |
| Gemini 4 Neon | 46.2 % | 65.2 % | **67.7** | **58.64** (xhigh) | 90.3 | $6.44–6.86 | 110 tok/s |
| GPT-6.2 Astra | 44.2 % | 63.6 % | 55.1 | 53.47 (max) / 52.28 (high) | 91.8 | $4.00–5.12 | 66–88 tok/s |
| GPT-6.2 Sol | 43.8 % | 61.5 % | – | – | 83.6 | **$1.01** | 85–145 tok/s |
| Claude Sonnet 5.5 | – | – | – | – | 86.5 | – | – |
| Gemini 4 Argon (Go) | – | – | – | – | 78.9 | – | – |
| GPT-6 Luna | – | – | – | – | 61.2 | très faible | – |

Ce que disent les sources, au-delà des chiffres :

- Le haut du classement tient en moins d'un point : à ce niveau, **le niveau d'effort compte plus
  que le modèle** (Fable 5.2 : 47.96 en `low`, 58.24 en `high`, 55.74 en `max` ; Astra : 35.58 en
  `low`, 53.47 en `max`). `high` est le bon réglage pour Fable : quasi le score de `xhigh`, à
  moitié prix, et `max` fait moins bien.
- **Astra** est bon en revue de code mais signalé comme s'arrêtant sur les tâches longues.
- **Sol** : à 0.4 point d'Astra sur FrontierCode pour environ un quart du coût, mais plus faible en
  terminal ; OpenAI note que les deux 6.2 « oublient » plus souvent de signaler un blocage.
- **Neon** est nettement en tête sur les questions posées à une codebase (+12.6 points sur Astra),
  ne refuse jamais, mais consomme beaucoup de tokens et de pas d'outils.

## Affectation

Choix initial du 2026-11-26, révisé par l'utilisateur le 2026-11-27 (commit `38d1dfe`, « update
models ») : tout passe sur l'abonnement Claude, sauf deux commandes. **Le champ `model:` des
fichiers `.opencode/agents/*.md` et `.opencode/commands/**.md` fait foi** ; ce tableau le recopie
(réaligné le 2026-11-28 après l'analyse des sessions exportées).

| Agent / commande | Modèle | Raison |
| --- | --- | --- |
| `orchestrator` | `claude-agent/opus#high` | pilotage : planifier, déléguer, intégrer, tenir la roadmap |
| `architect` | `claude-agent/fable#high` | meilleur score FrontierCode ; peu d'appels, décisions à fort impact |
| `rest-implementer` | `claude-agent/sonnet#high` | implémentation guidée par une recette détaillée (`fake-rest-endpoint`) |
| `gateway-implementer` | `claude-agent/opus#high` | concurrence, reconnexion, séquences : plus risqué que REST |
| `interactions-implementer` | `claude-agent/opus#high` | règles d'état (délais, jetons) et harnais Services |
| `reviewer` | `claude-agent/opus#high` | filet de sécurité : relit tout le code produit |
| `test-engineer` | `claude-agent/sonnet#high` | tests transverses, samples |
| `parity-auditor` | `claude-agent/sonnet#medium` | lecture/comparaison de corpus, requêtes `jq`/`rg` |
| `parity-status` (agent de `/parity/status`) | `claude-agent/haiku#low` | simple comptage |
| `docs-writer` | `claude-agent/sonnet#medium` | rédaction |
| `/bootstrap` | `claude-agent/sonnet#medium` | scaffolding .NET simple (M0 terminé, rarement relancé) |
| défaut du projet | `claude-agent/opus` | sessions sans agent explicite |

Répartition : tous les agents et commandes sur Claude ; `opencode-go` sert de repli. Aucun modèle
facturé au token. Un sous-agent lancé par l'outil `subagent` prend le modèle de **son fichier
d'agent**, pas celui d'une commande : c'est pourquoi `/parity/status` a son propre agent.

## Coût et alternatives

- Règle du projet : ne jamais utiliser le provider `opencode` (Zen, payé au token).
  Gemini 4 Neon serait le meilleur auditeur (SWE-Atlas-QnA 67.7) mais n'est disponible que sur Zen.
- L'auditeur interroge la spec avec `jq`/`rg` plutôt que de la lire en entier (≈ 300 k tokens),
  pour ménager le quota.
- Quota Claude atteint (repli sur `opencode-go`) : reviewer et architecte →
  `opencode-go/gemini-4-argon#high`, implementers → `opencode-go/deepseek-v4.1-flash#high`,
  orchestrateur → `opencode-go/glm-5.3#high`, `parity-status` → `opencode-go/deepseek-v4.1-flash#low`.
  Vérifier les ID exacts avec la liste des modèles d'opencode avant de basculer.
- À réévaluer à chaque sortie de modèle : le classement bouge environ tous les dix jours.

## Modifier

- Agents : champ `model:` en tête de `.opencode/agents/<agent>.md`.
- Commandes : champ `model:` de `.opencode/commands/**.md`.
- Défaut : `model` dans `opencode.jsonc`.

## Sources

- TechVerdict, « Best AI for Coding (November 2026) », 18/11/2026 — FrontierCode 2.0, Terminal-Bench 4.1,
  FrontierSWE v2, coût/tâche et vitesse (Artificial Analysis), chiffres constructeurs.
- OrcaRouter, « Gemini 4 Neon Tops the Coding Agent Index », 19/11/2026 — Artificial Analysis Coding
  Agent Index v2.1 (scores par niveau d'effort, SWE-Atlas-QnA, refus, tokens).
- BenchLM, classement coding, vérifié le 23/11/2026.
- Les chiffres constructeurs sont auto-déclarés et mesurés avec des réglages différents : un ou deux
  points d'écart valent égalité.
