#!/usr/bin/env python3
"""Helpers shared by the registry-derived projections (analyzer facts, registry resources,
TypeSpec keys, deprecated-catalog verification).

The C# constant surface itself is no longer emitted here: the Roslyn generator's
package projection (PackageAttributesEmitter / TelemetryNamesEmitter) builds the
compiled packages from the same resolved-registry.json. The naming and deprecation
rules below mirror that projection so the TypeSpec surface agrees with the C# one.
"""
from __future__ import annotations

import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(HERE, "..", ".."))  # …/src
JSON_PATH = os.path.join(
    SRC,
    "Qyl.Telemetry.SemanticConventions.SourceGeneration",
    "Resources",
    "resolved-registry.json",
)

REPO_SLUG = {
    "core": "open-telemetry/semantic-conventions",
    "genai": "open-telemetry/semantic-conventions-genai",
}


def csharp_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "\\r").replace("\n", "\\n") + '"'


def to_pascal(value: str) -> str:
    """PascalCase on the dotted/underscored form (SourceWriter.ToPascalCase)."""
    out = []
    upper_next = True
    for ch in value:
        if ch in ".-_ ":
            upper_next = True
            continue
        if upper_next:
            out.append(ch.upper())
            upper_next = False
        else:
            out.append(ch)
    return "".join(out)


def collapse_ws(s: str) -> str:
    return re.sub(r"\s+", " ", s).strip()


def deprecated_message(dep: dict) -> str:
    """Deprecation wording shared with the C# surface (SourceWriter.DeprecatedMessage)."""
    reason = dep.get("reason")
    if reason == "renamed":
        return f"Replaced by {dep.get('renamed_to')}."
    if reason == "obsoleted":
        return "Removed, no replacement."
    note = dep.get("note") or ""
    return note if note else "Deprecated."


def _is_deprecated_entry(entry, stability_key="stability") -> bool:
    return entry.get("deprecated") is not None or entry.get(stability_key) == "deprecated"


def resolve_collisions(entries, ident_fn, sort_key_fn):
    """PascalCase treats '.' and '_' identically, so distinct keys can collapse to one
    identifier. Keep exactly one per identifier: the non-deprecated entry, then the
    ordinally-first sort key. Returns (kept_entries, drops)."""
    groups = {}
    for e in entries:
        groups.setdefault(ident_fn(e), []).append(e)
    kept = []
    drops = []
    for ident, group in groups.items():
        if len(group) == 1:
            kept.append(group[0])
            continue
        ranked = sorted(group, key=lambda e: (_is_deprecated_entry(e), sort_key_fn(e)))
        winner = ranked[0]
        kept.append(winner)
        for loser in ranked[1:]:
            drops.append({"identifier": ident, "kept": sort_key_fn(winner), "dropped": sort_key_fn(loser)})
    return kept, drops
