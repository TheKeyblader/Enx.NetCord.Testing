#!/usr/bin/env python3
"""Extracts the response contract of every operation of the pinned Discord OpenAPI spec.

Usage:
    python3 -I tests/Enx.NetCord.Testing.Conformance/Contract/extract-openapi-contract.py \
        <path/to/discord-api-spec/specs/openapi.json> <spec-commit-sha> \
        [tests/Enx.NetCord.Testing.Conformance/Contract/openapi-contract.json]

The tool runs offline, outside the build, and its output is checked in and embedded in the test assembly: CI never
needs a checkout of discord-api-spec. For every operation of the spec ("METHOD /template") it keeps the 2xx response
schemas (null when the response has no body; the media type string when it is not JSON), plus the transitive closure of the "#/components/schemas/*"
references and the Discord error envelope (ErrorResponse). Extracting every operation (not only the implemented ones)
means adding a fake handler never requires regenerating the extract.

Known inaccuracies of the spec are corrected by explicit, guarded errata (see apply_errata); each one must be dropped
once the spec is fixed (the guard fails when the spec no longer has the expected broken shape).

The SHA must be the one pinned in docs/parity/sources.md; ContractInfrastructureTests fails otherwise. Re-run this tool
during /parity/sync (after advancing the pin) and review the diff: it is the contract drift.
"""

import json
import os
import re
import sys

SCHEMA_PREFIX = "#/components/schemas/"
ALWAYS_INCLUDED = ["ErrorResponse"]
METHODS = ("get", "put", "post", "delete", "options", "head", "patch", "trace")


def collect_refs(node, found):
    if isinstance(node, dict):
        for key, value in node.items():
            if key == "$ref":
                if not value.startswith(SCHEMA_PREFIX):
                    raise SystemExit(f"unsupported reference {value}")
                found.add(value[len(SCHEMA_PREFIX) :])
            else:
                collect_refs(value, found)
    elif isinstance(node, list):
        for value in node:
            collect_refs(value, found)


def apply_errata(closure):
    """Corrects the known inaccuracies of the pinned spec, each guarded so that a fixed spec fails loudly here.

    E-001: the spec types the field errors of a 50035 response with the generic `Error` schema, whose `code` is an
    integer. Discord's own documentation (developers/reference.mdx, "Error Messages") and the real API send a string
    there (`"code": "BASE_TYPE_REQUIRED"`). The extract replaces the item schema of `InnerErrors._errors` with a
    `FieldError` (string `code`, string `message`). When the spec stops matching the expected broken shape, this fails:
    drop the erratum then.
    """
    inner = closure.get("InnerErrors")
    if inner is None:
        return
    items = inner["properties"]["_errors"]["items"]
    if items != {"$ref": SCHEMA_PREFIX + "Error"}:
        raise SystemExit(
            "erratum E-001 no longer applies: InnerErrors._errors.items changed; review and drop it"
        )
    closure["FieldError"] = {
        "type": "object",
        "description": "Erratum E-001: a field error, whose code is a string (see developers/reference.mdx)",
        "properties": {"code": {"type": "string"}, "message": {"type": "string"}},
        "required": ["code", "message"],
    }
    inner["properties"]["_errors"]["items"] = {"$ref": SCHEMA_PREFIX + "FieldError"}


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)

    here = os.path.dirname(os.path.abspath(__file__))
    spec_path, commit = sys.argv[1], sys.argv[2]
    output_path = (
        sys.argv[3]
        if len(sys.argv) > 3
        else os.path.join(here, "openapi-contract.json")
    )

    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise SystemExit("the commit must be a full lower-case 40 character SHA")

    with open(spec_path, encoding="utf-8") as handle:
        spec = json.load(handle)

    schemas = spec["components"]["schemas"]
    operations = {}
    roots = set(ALWAYS_INCLUDED)

    for template, item in spec["paths"].items():
        for method in METHODS:
            if method not in item:
                continue
            key = f"{method.upper()} {template}"
            operation = item[method]
            responses = {}
            for status, response in operation["responses"].items():
                if not status.startswith("2"):
                    continue
                content = response.get("content", {})
                if not content:
                    responses[status] = None
                    continue
                if list(content) != ["application/json"]:
                    # A non-JSON body (an image, for instance) is recorded by media type and not validated.
                    if len(content) != 1:
                        raise SystemExit(
                            f"{key} {status}: unexpected media types {list(content)}"
                        )
                    responses[status] = next(iter(content))
                    continue
                schema = content["application/json"]["schema"]
                collect_refs(schema, roots)
                responses[status] = schema
            if not responses:
                raise SystemExit(f"{key}: no 2xx response")
            operations[key] = {
                "operationId": operation["operationId"],
                "responses": responses,
            }

    closure = {}
    pending = sorted(roots)
    while pending:
        name = pending.pop()
        if name in closure:
            continue
        if name not in schemas:
            raise SystemExit(f"unknown schema {name}")
        closure[name] = schemas[name]
        found = set()
        collect_refs(schemas[name], found)
        pending.extend(sorted(found))

    apply_errata(closure)

    contract = {
        "source": {
            "repository": "discord/discord-api-spec",
            "file": "specs/openapi.json",
            "commit": commit,
            "generator": "tests/Enx.NetCord.Testing.Conformance/Contract/extract-openapi-contract.py",
        },
        "operations": operations,
        "schemas": closure,
    }

    with open(output_path, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(
            contract, handle, sort_keys=True, separators=(",", ":"), ensure_ascii=False
        )
        handle.write("\n")

    print(f"{len(operations)} operations, {len(closure)} schemas -> {output_path}")


if __name__ == "__main__":
    main()
