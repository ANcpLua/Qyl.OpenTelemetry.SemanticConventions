#!/usr/bin/env python3
"""Cross-verify OpenTelemetryDeprecatedSemconvCatalog.cs against resolved-registry.json.

The analyzer catalog is deliberately hand-curated (it carries since-versions,
guidance prose, and mappings for keys upstream deleted outright — none of which
the resolved registry contains), so it cannot be fully generated. What CAN rot
silently is the part the registry does know:

  1. every registry attribute with deprecated.reason == "renamed" must appear in
     the catalog with EXACTLY the registry's renamed_to as its replacement;
  2. every registry attribute deprecated without a rename (obsoleted/uncategorized)
     must appear somewhere in the catalog (exact table, context-sensitive table,
     or prefix rule);
  3. every deprecated ENUM MEMBER of a registry attribute must appear in the
     catalog's attribute-value table under the same attribute.

Any catalog claim the registry contradicts, or any registry deprecation the
catalog misses, fails this check. Catalog-only entries (deleted GenAI keys,
changelog guidance) are fine — the registry cannot vouch either way.

CLI: verify_deprecated_catalog.py   (exit 0 = in sync; exit 1 = drift, printed)
"""
from __future__ import annotations

import json
import os
import re
import sys

from emit_attributes import JSON_PATH

HERE = os.path.dirname(os.path.abspath(__file__))
CATALOG = os.path.abspath(os.path.join(
    HERE, "..", "..",
    "Qyl.Telemetry.SemanticConventions.Analyzers",
    "OpenTelemetryDeprecatedSemconvCatalog.cs",
))


def parse_catalog(text: str):
    """Extract the five data tables from the C# source by their literal shapes."""
    def section(name: str) -> str:
        start = text.index(name)
        end = text.index("};", start)
        return text[start:end]

    exact = {}  # old -> replacement ('' = none)
    for m in re.finditer(r'\["([^"]+)"\] = \("([^"]*)", "[^"]*"\)', section("s_deprecatedAttributes")):
        exact[m.group(1)] = m.group(2)

    prefixes = {}
    for m in re.finditer(r'\["([^"]+)"\] = \("([^"]*)", "[^"]*"\)', section("s_deprecatedAttributePrefixes")):
        prefixes[m.group(1)] = m.group(2)

    genai = {}
    for m in re.finditer(r'\["([^"]+)"\] = \("([^"]+)", "[^"]*"\)', section("s_deprecatedGenAiAttributes")):
        genai[m.group(1)] = m.group(2)

    values = {}  # (attr, member-value) -> guidance
    vsec = section("s_deprecatedAttributeValues")
    for attr_m in re.finditer(r'\["([^"]+)"\] = new\(StringComparer\.OrdinalIgnoreCase\) \{([^}]*)\}', vsec, re.DOTALL):
        attr = attr_m.group(1)
        for v_m in re.finditer(r'\["([^"]+)"\] = "([^"]+)"', attr_m.group(2)):
            values[(attr, v_m.group(1))] = v_m.group(2)

    context = {}
    for m in re.finditer(r'\["([^"]+)"\] = "((?:[^"\\]|\\.)+)"', section("s_contextSensitiveDeprecatedNames")):
        context[m.group(1)] = m.group(2)

    return exact, prefixes, genai, values, context


def main() -> int:
    registry = json.load(open(JSON_PATH))
    exact, prefixes, genai, values, context = parse_catalog(open(CATALOG).read())

    def catalog_replacement(old: str):
        """Return (known, replacement-or-None) for an old attribute name."""
        if old in exact:
            return True, exact[old] or None
        if old in genai:
            return True, genai[old]
        if old in context:
            return True, None  # guidance-only, no machine replacement claimed
        for prefix, repl_prefix in prefixes.items():
            if old.startswith(prefix):
                return True, repl_prefix + old[len(prefix):]
        return False, None

    problems: list[str] = []

    for attr in registry["catalog"]:
        dep = attr.get("deprecated")
        if not isinstance(dep, dict):
            continue
        key = attr["key"]
        known, replacement = catalog_replacement(key)
        if dep.get("reason") == "renamed":
            renamed_to = dep.get("renamed_to", "")
            if not known:
                problems.append(f"MISSING rename: registry deprecates '{key}' -> '{renamed_to}', catalog has no entry")
            elif replacement is not None and replacement != renamed_to:
                problems.append(
                    f"MISMATCH: catalog maps '{key}' -> '{replacement}' but registry says '{renamed_to}'")
        else:
            if not known:
                note = (dep.get("note") or "").split("\n")[0][:80]
                problems.append(f"MISSING deprecation: registry deprecates '{key}' ({dep.get('reason')}: {note}), catalog has no entry")

    # Enum-member deprecations: attribute type members with a deprecated block.
    for attr in registry["catalog"]:
        typ = attr.get("type")
        if not isinstance(typ, dict):
            continue
        for member in typ.get("members", []):
            if member.get("deprecated") is None:
                continue
            key = attr["key"]
            # The catalog files enum-value deprecations under the CURRENT attribute
            # name; if the attribute itself was renamed, entries may live under
            # either name.
            renamed = attr.get("deprecated", {}).get("renamed_to") if isinstance(attr.get("deprecated"), dict) else None
            if (key, member["value"]) not in values and (renamed, member["value"]) not in values:
                problems.append(
                    f"MISSING enum-value deprecation: '{key}' member '{member['value']}' is deprecated in the registry, catalog value-table has no entry")

    if problems:
        print(f"verify_deprecated_catalog: {len(problems)} drift(s) between the analyzer catalog and resolved-registry.json:\n")
        for p in sorted(problems):
            print(f"  - {p}")
        return 1

    print("verify_deprecated_catalog: analyzer catalog is consistent with resolved-registry.json.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
