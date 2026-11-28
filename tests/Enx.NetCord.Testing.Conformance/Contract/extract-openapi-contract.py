#!/usr/bin/env python3
"""Extracts the response contract of the operations covered by the conformance tests from the pinned Discord OpenAPI spec.

Usage:
    python3 -I tests/Enx.NetCord.Testing.Conformance/Contract/extract-openapi-contract.py \
        <path/to/discord-api-spec/specs/openapi.json> <spec-commit-sha> \
        [tests/Enx.NetCord.Testing.Conformance/Contract/contract-operations.txt] \
        [tests/Enx.NetCord.Testing.Conformance/Contract/openapi-contract.json]

The tool runs offline, outside the build, and its output is checked in and embedded in the test assembly: CI never
needs a checkout of discord-api-spec. For every operation listed in contract-operations.txt ("METHOD /template", one
per line, '#' comments allowed) it keeps the 2xx response schemas (null when the response has no JSON body), plus the
transitive closure of the "#/components/schemas/*" references and the Discord error envelope (ErrorResponse).

Known inaccuracies of the spec are corrected by explicit, guarded errata (see apply_errata); each one is also listed in
the report of the task that introduced it and must be dropped once the spec is fixed.

The SHA must be the one pinned in docs/parity/sources.md; the ContractSourceTests test fails otherwise. Re-run this tool
during /parity/sync (after advancing the pin) and review the diff: it is the contract drift. Adding an operation to the
conformance tests means adding its key to contract-operations.txt and re-running this tool.
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


def read_operation_keys(path):
    keys = []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            line = line.split("#", 1)[0].strip()
            if line:
                keys.append(line)
    return keys


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)

    here = os.path.dirname(os.path.abspath(__file__))
    spec_path, commit = sys.argv[1], sys.argv[2]
    operations_path = (
        sys.argv[3]
        if len(sys.argv) > 3
        else os.path.join(here, "contract-operations.txt")
    )
    output_path = (
        sys.argv[4]
        if len(sys.argv) > 4
        else os.path.join(here, "openapi-contract.json")
    )

    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise SystemExit("the commit must be a full lower-case 40 character SHA")

    with open(spec_path, encoding="utf-8") as handle:
        spec = json.load(handle)

    schemas = spec["components"]["schemas"]
    operations = {}
    roots = set(ALWAYS_INCLUDED)

    for key in read_operation_keys(operations_path):
        method, _, template = key.partition(" ")
        item = spec["paths"].get(template)
        if item is None or method.lower() not in item or method.lower() not in METHODS:
            raise SystemExit(f"{key}: not an operation of the spec")
        responses = {}
        for status, response in item[method.lower()]["responses"].items():
            if not status.startswith("2"):
                continue
            content = response.get("content", {})
            if not content:
                responses[status] = None
                continue
            if list(content) != ["application/json"]:
                raise SystemExit(
                    f"{key} {status}: unexpected media types {list(content)}"
                )
            schema = content["application/json"]["schema"]
            collect_refs(schema, roots)
            responses[status] = schema
        if not responses:
            raise SystemExit(f"{key}: no 2xx response")
        operations[key] = {
            "operationId": item[method.lower()]["operationId"],
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
