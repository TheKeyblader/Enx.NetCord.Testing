# Enx.NetCord.Testing.Conformance

Conformance tests: the **real NetCord clients** (`RestClient`, …) talk to the fake, and every response the fake
produced is validated against the **Discord OpenAPI spec** pinned in `docs/parity/sources.md`.

## Layout

| Path | Role |
| --- | --- |
| `Rest/*ConformanceTests.cs` | One scenario per operation (and its main `Unknown *` error), through the NetCord API. |
| `Contract/openapi-contract.json` | Checked-in extract of the pinned spec: 2xx response schemas of **every** operation + the `$ref` closure + the error envelopes (`ErrorResponse`, and `RatelimitedResponse` for 429). Embedded in the assembly; CI needs no checkout of `discord-api-spec`. |
| `Contract/extract-openapi-contract.py` | Generates the extract (offline, reproducible; the spec SHA is in the extract's `source` object). |
| `Contract/JsonSchemaValidator.cs` | Small **strict** validator for the subset of JSON Schema the spec uses (undeclared properties are rejected). It **throws** on an unsupported keyword. |
| `Contract/ContractExceptions.cs` + `ContractException.cs` | The single list of justified undeclared properties (empty today) and its static audit. |
| `Contract/ContractAssert.cs` | `ContractAssert.Conforms(discord, "METHOD /template"…)`. |
| `Contract/ContractInfrastructureTests.cs` | Guards: pinned commit, inventory ↔ extract in sync, supported keywords, one scenario per implemented operation, validator oracle. |
| `Contract/StrictContractTests.cs` | Strict mode: `$ref`, `allOf`, `oneOf`/`anyOf`, nested, arrays, `additionalProperties`, exceptions. |
| `Contract/ContractExceptionsTests.cs` | Exception list soundness and dead-exception detection (runs after the parallel collections). |

## Adding a REST handler (no change to `Contract/` needed)

The extract covers all spec operations, so a new handler only needs a scenario:

1. In `Rest/`, add a test per nominal case and for the main error (`Unknown *`) that calls the operation **through
   NetCord** (`discord.CreateRestClient()`), asserts the shape of the returned NetCord object, and ends with
   `ContractAssert.Conforms(discord, "METHOD /template")`. `Conforms` validates *every* attempt in the journal (setup
   calls included) and fails if the named route was never reached.
2. Tag it with `[Parity("METHOD /template")]` (the exact route key, e.g. `[Parity("GET /channels/{channel_id}")]`);
   error scenarios also carry `[Parity("<code>")]`.
3. `ConformanceTests_EveryImplementedOperation_HasAScenarioTaggedWithItsKey` fails until step 2 is done.

Handlers of interactions and webhooks (`POST /interactions/…/callback`, `/webhooks/…`) get their scenarios in
`Interactions/` instead of `Rest/`, still `[Parity]`-tagged and ending with `ContractAssert.Conforms`.
A skipped test (`Skip` set) does not count for the guard: it must not stand in for a missing scenario.

A violation is a bug in the fake (fix it, or `Skip` the test with the reason and a task ID, and downgrade the parity row to 🟡).
A route NetCord calls but the spec does not document (`InSpecification: false`) has no contract: `Conforms` only
checks the others.

## Regenerating after `/parity/sync`

After advancing the `discord-api-spec` SHA in `docs/parity/sources.md` and regenerating `RouteInventory.g.cs`
(`tools/generate-route-inventory.py`):

```bash
python3 -I tests/Enx.NetCord.Testing.Conformance/Contract/extract-openapi-contract.py \
    <path>/discord-api-spec/specs/openapi.json <full-spec-sha>
```

Review the diff of `openapi-contract.json`: it is the contract drift. Then run `dotnet test`:

- `Contract_Extract_IsProducedFromThePinnedSpecCommit` / `…DescribeTheSameSpecOperations` fail when the extract and the inventory disagree.
- `…UsesOnlyKeywordsTheValidatorSupports` fails when the spec introduced a keyword to support (extend `JsonSchemaValidator`).
- The script's guarded errata fail loudly when the spec fixed what they correct. Current erratum **E-001**: the spec types
  the field-error `code` of a `50035` as an integer, whereas Discord (docs `reference.mdx`) and the fake send a string.

## Strict mode and justified exceptions

`ContractAssert.Conforms` is **strict**: a property of a response that the spec schema does not declare is a violation,
like `unevaluatedProperties: false` (message: JSON path, property name, spec schema). A property is declared by
`properties` of the schema, of its `$ref` target, of any `allOf` branch (union), or of a `oneOf`/`anyOf` branch **that
validates**; `additionalProperties: true` or a schema accepts the others, `false` rejects them. It applies through nested
objects and arrays, and to the error envelopes.

When the fake sends an undeclared property, decide:

- the official docs (`discord-api-docs`) do **not** declare it → bug of the fake (fix `src/`, or `Skip` the test with the
  reason and a task ID, and downgrade the parity row to 🟡);
- the docs declare it and the spec is incomplete → add **one** entry to `Contract/ContractExceptions.cs`: `Schema`,
  `Property`, `Justification`, `Reference` (`developers/…mdx:line` of `discord-api-docs`), `Operation` (the route whose
  success response carries it) and `Task` (roadmap task or `UR-NNN`). Never register an exception to silence a bug.

`ContractExceptionsTests` keeps the list honest: it fails for a malformed entry, for a property the spec now declares
(after a `/parity/sync`), and for an **unused** exception (its operation's success response was validated in the run and
no response needed it). A filtered `dotnet test` does not judge exceptions whose operation did not run.
The list is empty today: no response of the fake carries an undeclared property.

## Limits

Only response *schemas* are checked, strictly; request validation and behaviour stay in `Enx.NetCord.Testing.Tests`.
Required properties are checked, but a property the spec declares optional is not required to be present.
