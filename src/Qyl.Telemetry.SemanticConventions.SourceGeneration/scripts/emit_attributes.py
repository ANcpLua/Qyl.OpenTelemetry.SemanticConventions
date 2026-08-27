#!/usr/bin/env python3
"""Deterministic emitter for the shipped C# attribute-constant surface.

Reads the embedded resolved-registry.json (core + genai + qyl merge) and regenerates, per root namespace, `{Root}Attributes.g.cs` for both the stable
and the incubating NuGet packages, `SchemaUrl.g.cs` for the stable package, plus
`Names/QylTelemetryNames.g.cs` for the qyl-owned scope and bridge-event names.

The qyl-owned vocabulary is merged into the same catalog as the upstream
registries (architecture loop 1: `qyl.*` entries are first-class next to
upstream ones), so it flows through the same stability filter — today every
`qyl.*` entry is development-stability and therefore lands in the incubating
package only. An entry promoted to `stable` would move to the stable package
with no change here.

The output shape is reverse-engineered from the checked-in reference file
`…Incubating/Attributes/GenAi/GenAiAttributes.g.cs`, which was produced by the
project's (uncommitted) ad-hoc emitter from the genai registry. See the module
docstring notes at the bottom for the rules and for the caveat that the
currently-committed json has DRIFTED away from the json that produced that
reference (so byte-reproduction of it is not possible from this json — the
formatting logic here is validated on the attributes whose json content survived
unchanged). Ongoing drift is guarded by VerifyAttributesHash + the
ByteIdentity snapshot tests, not by this reference.

CLI:
    emit_attributes.py --stdout {root} {stable|incubating}
    emit_attributes.py --write
    emit_attributes.py --check    (snapshot gate: committed tree == emitter output)
"""
from __future__ import annotations

import json
import os
import re
import sys
from collections import Counter

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.abspath(os.path.join(HERE, "..", ".."))  # …/src
JSON_PATH = os.path.join(
    SRC,
    "Qyl.Telemetry.SemanticConventions.SourceGeneration",
    "Resources",
    "resolved-registry.json",
)
QYL_JSON_PATH = os.path.join(
    SRC,
    "Qyl.Telemetry.SemanticConventions.SourceGeneration",
    "Resources",
    "qyl-registry.json",
)
STABLE_PROJECT_ROOT = os.path.join(SRC, "Qyl.Telemetry.SemanticConventions")
STABLE_ROOT = os.path.join(STABLE_PROJECT_ROOT, "Attributes")
SCHEMA_URL_PATH = os.path.join(STABLE_PROJECT_ROOT, "SchemaUrl.g.cs")
INCUBATING_ROOT = os.path.join(
    SRC, "Qyl.Telemetry.SemanticConventions.Incubating", "Attributes"
)
INCUBATING_NAMES_ROOT = os.path.join(
    SRC, "Qyl.Telemetry.SemanticConventions.Incubating", "Names"
)
QYL_SOURCE_REGISTRY = "qyl"

def csharp_string(value: str) -> str:
    return '"' + value.replace("\\", "\\\\").replace('"', '\\"').replace("\r", "\\r").replace("\n", "\\n") + '"'


REPO_SLUG = {
    "core": "open-telemetry/semantic-conventions",
    "genai": "open-telemetry/semantic-conventions-genai",
}


# --------------------------------------------------------------------------- #
# String helpers (ported from Emitters/SourceWriter.cs)                        #
# --------------------------------------------------------------------------- #
def to_pascal(value: str) -> str:
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


def escape_attribute(text: str) -> str:
    """Escape for a C# string-literal attribute argument (SourceWriter.EscapeAttribute):
    backslash, double-quote, newlines->space, drop CR. Backticks are preserved."""
    return (
        text.replace("\\", "\\\\")
        .replace('"', '\\"')
        .replace("\n", " ")
        .replace("\r", "")
    )


# --------------------------------------------------------------------------- #
# Markdown -> XML-doc conversion                                              #
# --------------------------------------------------------------------------- #
# The registry `brief`/`note` fields are GitHub-flavoured markdown. They are
# rendered into C# XML doc comments, which the compiler parses as XML — so the
# output MUST be well-formed (unclosed/mismatched tags make the compiler SILENTLY
# DROP the whole doc comment, which is worse than an error under the NoWarn band).
# Every construct below therefore emits balanced/self-closing XML only:
#   * paragraph break        -> a self-closing `<para/>` separator line (valid XML
#                               and an IntelliSense paragraph break)
#   * inline code `x`        -> <c>x</c>
#   * fenced code ```...```  -> <code> … </code> block
#   * link [t](u)            -> <a href="u">t</a>, with BALANCED-parenthesis URL
#                               parsing and newline-spanning link text collapsed
#   * image ![alt](u)        -> the alt text only (image link dropped)
#   * GitHub alert [!WARNING]-> "Warning:" (case-insensitive, any word)
#   * blockquote `> …`       -> markers stripped; a LEADING quote block is wrapped
#                               in <blockquote> … </blockquote>, else emitted bare
#   * table separator row    -> dropped (cells kept as plain text)
# All literal text is XML-escaped (& < >). A well-formedness self-check
# (`assert_docs_well_formed`) parses every emitted doc block and fails the run if
# anything is malformed.

_ALERT_RE = re.compile(r"\[!(\w+)\]", re.IGNORECASE)
_TABLE_SEP_RE = re.compile(r"^\s*\|?[\s:|-]*-[\s:|-]*\|?\s*$")


def _esc(s: str) -> str:
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def _collapse_ws(s: str) -> str:
    return re.sub(r"\s+", " ", s).strip()


def _parse_link(text: str, start: int):
    """Parse a markdown link/image body starting at the '[' at ``start``.

    Returns (link_text, url, end_index) or None. Bracketed text may nest and span
    newlines; the URL is read with balanced parentheses so inner/trailing ')' are
    kept in the href instead of truncating the link."""
    n = len(text)
    if start >= n or text[start] != "[":
        return None
    i = start + 1
    depth = 1
    txt = []
    while i < n:
        c = text[i]
        if c == "[":
            depth += 1
            txt.append(c)
        elif c == "]":
            depth -= 1
            if depth == 0:
                i += 1
                break
            txt.append(c)
        else:
            txt.append(c)
        i += 1
    else:
        return None
    if i >= n or text[i] != "(":
        return None
    i += 1
    pdepth = 1
    url = []
    while i < n:
        c = text[i]
        if c == "(":
            pdepth += 1
            url.append(c)
        elif c == ")":
            pdepth -= 1
            if pdepth == 0:
                i += 1
                break
            url.append(c)
        else:
            url.append(c)
        i += 1
    else:
        return None
    return "".join(txt), "".join(url), i


def render_inline(text: str) -> str:
    """Convert inline markdown in ``text`` (which may span newlines) to well-formed
    XML-doc markup with all literal text escaped."""
    out = []
    i = 0
    n = len(text)
    while i < n:
        ch = text[i]
        if ch == "`":
            j = i + 1
            while j < n and text[j] == "`":
                j += 1
            ticks = text[i:j]
            close = text.find(ticks, j)
            if close == -1:
                out.append(_esc(ch))
                i += 1
                continue
            out.append("<c>" + _esc(text[j:close]) + "</c>")
            i = close + len(ticks)
            continue
        if ch == "!" and i + 1 < n and text[i + 1] == "[":
            parsed = _parse_link(text, i + 1)
            if parsed:
                alt, _url, end = parsed
                out.append(_esc(_collapse_ws(alt)))
                i = end
                continue
            out.append("!")
            i += 1
            continue
        if ch == "[":
            m = _ALERT_RE.match(text, i)
            if m:
                out.append(_esc(m.group(1).capitalize() + ":"))
                i = m.end()
                continue
            parsed = _parse_link(text, i)
            if parsed:
                txt, url, end = parsed
                out.append(
                    '<a href="' + _esc(url.strip()) + '">' + _esc(_collapse_ws(txt)) + "</a>"
                )
                i = end
                continue
            out.append(_esc(ch))
            i += 1
            continue
        out.append(_esc(ch))
        i += 1
    return "".join(out)


def _segment_blocks(text: str):
    """Split note text into ('text', lines) / ('code', lines) blocks. Fenced code
    blocks are atomic; other blocks are separated by blank lines."""
    blocks = []
    buf = []
    fbuf = []
    in_fence = False
    for raw in text.split("\n"):
        line = raw.rstrip("\r")
        if line.strip().startswith("```"):
            if not in_fence:
                if buf:
                    blocks.append(("text", buf))
                    buf = []
                in_fence = True
                fbuf = []
            else:
                blocks.append(("code", fbuf))
                fbuf = []
                in_fence = False
            continue
        if in_fence:
            fbuf.append(line)
            continue
        if line.strip() == "":
            if buf:
                blocks.append(("text", buf))
                buf = []
            continue
        buf.append(line)
    if in_fence and fbuf:
        blocks.append(("code", fbuf))
    if buf:
        blocks.append(("text", buf))
    return blocks


def render_markdown(text: str):
    """Convert a markdown brief/note into a list of well-formed XML-doc content
    lines. Paragraphs are separated by a self-closing `<para/>` line."""
    text = text.rstrip("\n")
    if text == "":
        return []
    out = []
    first = True
    for kind, lines in _segment_blocks(text):
        if kind == "code":
            block_lines = ["<code>"] + [_esc(ln) for ln in lines] + ["</code>"]
        else:
            # drop GFM table separator rows; keep cell text
            lines = [ln for ln in lines if not _TABLE_SEP_RE.match(ln)]
            if not lines:
                continue
            nonempty = [ln for ln in lines if ln.strip() != ""]
            is_bq = bool(nonempty) and all(ln.lstrip().startswith(">") for ln in nonempty)
            if is_bq:
                stripped = [re.sub(r"^\s*>\s?", "", ln) for ln in lines]
                rendered = render_inline("\n".join(stripped)).split("\n")
                if first:
                    block_lines = ["<blockquote>"] + rendered
                    block_lines[-1] = block_lines[-1] + "</blockquote>"
                else:
                    block_lines = rendered
            else:
                block_lines = render_inline("\n".join(lines)).split("\n")
        if not first:
            out.append("<para/>")
        out.extend(block_lines)
        first = False
    return out


def assert_docs_well_formed(text: str, class_name: str):
    """Parse every contiguous XML doc-comment block in ``text`` and raise if any is
    not well-formed XML (wrapped in a dummy root so summary+remarks siblings are
    allowed)."""
    import xml.etree.ElementTree as ET

    block = []

    def flush():
        if not block:
            return
        frag = "\n".join(block)
        try:
            ET.fromstring("<root>" + frag + "</root>")
        except ET.ParseError as exc:
            raise AssertionError(
                f"malformed doc XML in {class_name}: {exc}\n---\n{frag}\n---"
            )

    for line in text.split("\n"):
        stripped = line.lstrip()
        if stripped.startswith("///"):
            content = stripped[3:]
            if content.startswith(" "):
                content = content[1:]
            block.append(content)
        else:
            flush()
            block = []
    flush()


def deprecated_message(dep: dict) -> str:
    """Attribute-level obsolete message (SourceWriter.DeprecatedMessage)."""
    reason = dep.get("reason")
    if reason == "renamed":
        return f"Replaced by {dep.get('renamed_to')}."
    if reason == "obsoleted":
        return "Removed, no replacement."
    note = dep.get("note") or ""
    return note if note else "Deprecated."


# --------------------------------------------------------------------------- #
# Emit primitives                                                             #
# --------------------------------------------------------------------------- #
class Writer:
    def __init__(self):
        self.parts = []

    def line(self, s=""):
        self.parts.append(s)

    def doc_line(self, pad, line):
        self.parts.append(pad + "///" + (" " + line if line else ""))

    def summary(self, pad, text_lines):
        self.doc_line_raw(pad, "<summary>")
        for ln in text_lines:
            self.doc_line(pad, ln)
        self.doc_line_raw(pad, "</summary>")

    def remarks(self, pad, text_lines):
        self.doc_line_raw(pad, "<remarks>")
        for ln in text_lines:
            self.doc_line(pad, ln)
        self.doc_line_raw(pad, "</remarks>")

    def doc_line_raw(self, pad, tag):
        self.parts.append(pad + "/// " + tag)

    def text(self):
        return "\n".join(self.parts) + "\n"


def is_enum(attr) -> bool:
    t = attr.get("type")
    return isinstance(t, dict) and "members" in t


# stability filter --------------------------------------------------------- #
def attr_included(attr, stable: bool) -> bool:
    if not stable:
        return True
    stab = attr.get("stability")
    return stab == "stable" or stab == "deprecated" or attr.get("deprecated") is not None


def member_included(member, stable: bool) -> bool:
    if not stable:
        return True
    stab = member.get("stability")
    return stab == "stable" or stab == "deprecated" or member.get("deprecated") is not None


def member_name(member) -> str:
    return to_pascal(member["id"])


# --------------------------------------------------------------------------- #
# Collision resolution                                                        #
# --------------------------------------------------------------------------- #
# PascalCase treats '.' and '_' identically, so distinct dotted keys / enum ids
# can collapse to the same C# identifier and produce CS0102 duplicate-member
# errors. This is new in the 1.43 surface: several attributes/enum members gained
# a deprecated '.'<->'_' alias of a canonical form (e.g. messaging.client.id vs
# the deprecated messaging.client_id; cloud.platform azure.vm vs deprecated
# azure_vm). Both spellings map to one identifier (ClientId / AzureVm), so only
# one const/member can be emitted.
#
# Rule (applied AFTER stability filtering, independently within each attributes
# class and each …Values enum, for BOTH tiers):
#   When >=1 entry share a C# identifier, keep exactly ONE:
#     1. Prefer the entry that is NOT deprecated (no `deprecated` block AND
#        stability != "deprecated").
#     2. If still tied, keep the one whose key/value sorts first ordinally.
#   Drop the rest. This keeps the canonical const/member (e.g.
#   ClientId = "messaging.client.id", AzureVm = "azure.vm") and drops the
#   deprecated exact-duplicate that cannot coexist.
def _is_deprecated_entry(entry, stability_key="stability") -> bool:
    return entry.get("deprecated") is not None or entry.get(stability_key) == "deprecated"


def resolve_collisions(entries, ident_fn, sort_key_fn):
    """Return (kept_entries, drops). ``drops`` is a list of dicts describing every
    dropped duplicate for auditing."""
    groups = {}
    for e in entries:
        groups.setdefault(ident_fn(e), []).append(e)
    kept = []
    drops = []
    for ident, group in groups.items():
        if len(group) == 1:
            kept.append(group[0])
            continue
        # 1. prefer non-deprecated; 2. tie-break ordinal on sort key
        ranked = sorted(
            group, key=lambda e: (_is_deprecated_entry(e), sort_key_fn(e))
        )
        winner = ranked[0]
        kept.append(winner)
        for loser in ranked[1:]:
            drops.append(
                {
                    "identifier": ident,
                    "kept": sort_key_fn(winner),
                    "dropped": sort_key_fn(loser),
                }
            )
    return kept, drops


def emit_member_summary(brief: str, member_id: str):
    if brief and brief.strip():
        s = render_inline(brief.rstrip("\n"))
        if not s.endswith("."):
            s += "."
        return s.split("\n")
    return [f"{member_id}."]


# --------------------------------------------------------------------------- #
# File generation                                                             #
# --------------------------------------------------------------------------- #
def provenance_lines(attrs):
    """The `<auto-generated/>` header block for one emitted file.

    Upstream roots name the pinned repo + commit they were projected from. The
    qyl-owned root has no upstream commit to cite — its provenance is this
    repository's own registry input, which the same one command regenerates
    from.
    """
    registry, schema_url, commit = choose_source(attrs)
    if registry == QYL_SOURCE_REGISTRY:
        return [
            "// <auto-generated/>",
            "// Generated by qyl's emitter from the qyl-owned registry",
            "// Source: SourceGeneration/Resources/qyl-registry.json",
            "// Licensed under Apache-2.0",
            "// </auto-generated>",
        ]
    slug = REPO_SLUG.get(registry, registry)
    return [
        "// <auto-generated/>",
        f"// Generated by qyl's Weaver pipeline from {slug}@{commit}",
        f"// Schema: {schema_url}",
        "// Licensed under Apache-2.0 (inherited from OpenTelemetry upstream)",
        "// </auto-generated>",
    ]


def choose_source(attrs):
    """Pick header provenance for a root: majority source_registry, tie-break core."""
    counts = Counter(a.get("source_registry") for a in attrs)
    # majority; tie-break prefers 'core'
    best = sorted(counts.items(), key=lambda kv: (-kv[1], kv[0] != "core"))[0][0]
    for a in attrs:
        if a.get("source_registry") == best:
            return best, a.get("schema_url"), a.get("source_commit")
    a = attrs[0]
    return a.get("source_registry"), a.get("schema_url"), a.get("source_commit")


def emit_file(root: str, attrs, stable: bool) -> str:
    pas_root = to_pascal(root)
    class_name = pas_root + "Attributes"
    if stable:
        ns = f"Qyl.Telemetry.SemanticConventions.Attributes.{pas_root}"
        suffix = ""
    else:
        ns = f"Qyl.Telemetry.SemanticConventions.Incubating.Attributes.{pas_root}"
        suffix = " (incubating)"

    w = Writer()
    for line in provenance_lines(attrs):
        w.line(line)
    w.line()
    w.line("// Copyright (c) 2025-2026 ancplua")
    w.line()
    w.line("#nullable enable")
    w.line()
    w.line(f"namespace {ns};")
    w.line()
    w.line(f"/// <summary>{pas_root} Attributes{suffix}.</summary>")
    w.line(f"public static class {class_name}")
    w.line("{")

    pad = "    "
    drops = []

    def attr_ident(a):
        k = a["key"]
        rel = k[len(root) + 1:] if k.startswith(root + ".") else k
        return to_pascal(rel)

    attrs, adrops = resolve_collisions(attrs, attr_ident, lambda a: a["key"])
    for d in adrops:
        d["scope"] = f"{class_name}"
        drops.append(d)

    # self-check: emitted attribute identifiers must be unique within the class
    seen_idents = set()
    for a in attrs:
        i = attr_ident(a)
        if i in seen_idents:
            raise AssertionError(f"duplicate identifier {i!r} in class {class_name}")
        seen_idents.add(i)

    ordered = sorted(attrs, key=lambda a: a["key"])
    first = True
    for attr in ordered:
        key = attr["key"]
        rel = key[len(root) + 1:] if key.startswith(root + ".") else key
        mname = to_pascal(rel)

        if not first:
            w.line()
        first = False

        # summary (brief)
        brief = attr.get("brief") or ""
        brief = brief.rstrip("\n")
        w.summary(pad, render_inline(brief).split("\n") if brief else [])

        # remarks (note)
        note = attr.get("note") or ""
        note_lines = render_markdown(note)
        if note_lines:
            w.remarks(pad, note_lines)

        # obsolete
        dep = attr.get("deprecated")
        if dep is not None:
            msg = escape_attribute(deprecated_message(dep))
            w.line(f'{pad}[global::System.Obsolete("{msg}", false)]')
        elif attr.get("stability") == "deprecated":
            w.line(f'{pad}[global::System.Obsolete("Deprecated.", false)]')

        w.line(f'{pad}public const string {mname} = "{key}";')

        # enum Values nested class
        if is_enum(attr):
            members = [m for m in attr["type"]["members"] if member_included(m, stable)]
            members, mdrops = resolve_collisions(
                members, member_name, lambda m: m["value"]
            )
            for d in mdrops:
                d["scope"] = f"{class_name}.{mname}Values"
                drops.append(d)
            # self-check: emitted member identifiers unique within the enum class
            _seen_m = set()
            for m in members:
                mi = member_name(m)
                if mi in _seen_m:
                    raise AssertionError(
                        f"duplicate identifier {mi!r} in enum {class_name}.{mname}Values"
                    )
                _seen_m.add(mi)
            if members:
                members = sorted(members, key=member_name)
                w.line()
                w.summary(pad, [f"Values for the <c>{mname}</c> attribute."])
                w.line(f"{pad}public static class {mname}Values")
                w.line(f"{pad}{{")
                ipad = pad + pad
                mfirst = True
                for m in members:
                    if not mfirst:
                        w.line()
                    mfirst = False
                    w.summary(ipad, emit_member_summary(m.get("brief", ""), m["id"]))
                    mdep = m.get("deprecated")
                    if mdep is not None:
                        msg = escape_attribute(deprecated_message(mdep))
                        w.line(f'{ipad}[global::System.Obsolete("{msg}", false)]')
                    elif m.get("stability") == "deprecated":
                        w.line(f'{ipad}[global::System.Obsolete("Deprecated.", false)]')
                    w.line(f'{ipad}public const string {member_name(m)} = "{m["value"]}";')
                w.line(f"{pad}}}")

    w.line("}")
    text = w.text()
    assert_docs_well_formed(text, class_name)
    return text, drops


# --------------------------------------------------------------------------- #
# qyl-owned scope + telemetry event names                                     #
# --------------------------------------------------------------------------- #
NAMES_NAMESPACE = "Qyl.Telemetry.SemanticConventions.Incubating.Names"
NAMES_CLASS = "QylTelemetryNames"


def _name_members(values, what: str):
    """PascalCase identifier per name, ordinal-sorted, uniqueness enforced."""
    members = []
    seen = {}
    for value in sorted(set(values)):
        ident = to_pascal(value)
        if ident in seen:
            raise AssertionError(
                f"{what} {value!r} and {seen[ident]!r} both map to identifier {ident!r}"
            )
        seen[ident] = value
        members.append((ident, value))
    return members


def emit_names_file(registry) -> str:
    scopes = _name_members(registry.get("scope_names", []), "scope name")
    events = _name_members(registry.get("event_names", []), "event name")

    w = Writer()
    w.line("// <auto-generated/>")
    w.line("// Generated by qyl's emitter from the qyl-owned registry")
    w.line("// Source: SourceGeneration/Resources/qyl-registry.json")
    w.line("// Licensed under Apache-2.0")
    w.line("// </auto-generated>")
    w.line()
    w.line("// Copyright (c) 2025-2026 ancplua")
    w.line()
    w.line("#nullable enable")
    w.line()
    w.line(f"namespace {NAMES_NAMESPACE};")
    w.line()
    w.line("/// <summary>qyl-owned telemetry names (incubating).</summary>")
    w.line("/// <remarks>")
    w.line("/// The same registry input drives the QYL0200 analyzer allowlist, so a name")
    w.line("/// referenced through these constants is a name the analyzer accepts and the")
    w.line("/// collector recognises.")
    w.line("/// </remarks>")
    w.line(f"public static class {NAMES_CLASS}")
    w.line("{")

    pad = "    "
    ipad = pad + pad

    w.summary(pad, ["Names of the <c>ActivitySource</c> and <c>Meter</c> scopes qyl constructs."])
    w.line(f"{pad}public static class Scopes")
    w.line(f"{pad}{{")
    first = True
    for ident, value in scopes:
        if not first:
            w.line()
        first = False
        w.summary(ipad, [f"The <c>{escape_attribute(value)}</c> telemetry scope."])
        w.line(f"{ipad}public const string {ident} = {csharp_string(value)};")
    w.line(f"{pad}}}")
    w.line()

    w.summary(pad, ["Names of qyl-owned <c>Activity</c> and <c>DiagnosticSource</c> events."])
    w.line(f"{pad}public static class Events")
    w.line(f"{pad}{{")
    first = True
    for ident, value in events:
        if not first:
            w.line()
        first = False
        w.summary(ipad, [f"The <c>{escape_attribute(value)}</c> telemetry event."])
        w.line(f"{ipad}public const string {ident} = {csharp_string(value)};")
    w.line(f"{pad}}}")

    w.line("}")
    text = w.text()
    assert_docs_well_formed(text, NAMES_CLASS)
    return text


# --------------------------------------------------------------------------- #
# Registry loading / grouping                                                 #
# --------------------------------------------------------------------------- #
def load_resolved_registry():
    with open(JSON_PATH, encoding="utf-8") as f:
        return json.load(f)


def load_groups():
    data = load_resolved_registry()
    catalog = list(data["catalog"])

    # The qyl-owned vocabulary is catalog content, not a second surface: generate.sh
    # merges it into the resolved registry tagged with its source registry, so it is
    # filtered, grouped and emitted by exactly the same rules as the upstream entries.
    by_root = {}
    for attr in catalog:
        root = attr["key"].split(".")[0]
        by_root.setdefault(root, []).append(attr)
    return by_root


def roots_for_tier(by_root, stable: bool):
    out = {}
    for root, attrs in by_root.items():
        kept = [a for a in attrs if attr_included(a, stable)]
        if kept:
            out[root] = kept
    return out


def emit_schema_url() -> str:
    registry = load_resolved_registry()
    core = next(source for source in registry["sources"] if source["source_registry"] == "core")
    schema_version = registry["schema_version"]
    schema_url = registry["schema_url"]
    source_commit = core["source_commit"]
    return f"""// <auto-generated/>
// Generated by qyl's Weaver pipeline from open-telemetry/semantic-conventions@{source_commit}
// Schema: {schema_url}
// Licensed under Apache-2.0 (inherited from OpenTelemetry upstream)
// </auto-generated>

// Copyright (c) 2025-2026 ancplua

namespace Qyl.Telemetry.SemanticConventions;

/// <summary>Schema URL for OpenTelemetry Semantic Conventions {schema_version}.</summary>
public static partial class SchemaUrl
{{
    /// <summary>The schema URL for OTel semconv {schema_version}.</summary>
    public const string Current = {csharp_string(schema_url)};
}}
"""


# --------------------------------------------------------------------------- #
# CLI                                                                         #
# --------------------------------------------------------------------------- #
def cmd_stdout(root, tier):
    stable = tier == "stable"
    by_root = load_groups()
    tier_roots = roots_for_tier(by_root, stable)
    if root not in tier_roots:
        sys.stderr.write(f"root '{root}' has no {tier} file\n")
        sys.exit(2)
    text, _drops = emit_file(root, tier_roots[root], stable)
    sys.stdout.write(text)


def owned_files():
    """Every file this emitter owns, as {absolute path: expected text}.

    `--write` and `--check` both go through here, so the writer and the verifier
    can never disagree about what the committed tree is supposed to contain —
    a second opinion about that would be the duplicate-owner failure the
    architecture forbids.
    """
    by_root = load_groups()
    expected = {}
    drops = []
    counts = {}
    for stable, base in ((True, STABLE_ROOT), (False, INCUBATING_ROOT)):
        tier_label = "stable" if stable else "incubating"
        tier_roots = roots_for_tier(by_root, stable)
        for root in sorted(tier_roots):
            pas_root = to_pascal(root)
            path = os.path.join(base, pas_root, f"{pas_root}Attributes.g.cs")
            text, file_drops = emit_file(root, tier_roots[root], stable)
            expected[path] = text
            for d in file_drops:
                d["tier"] = tier_label
                drops.append(d)
        counts[tier_label] = len(tier_roots)

    expected[os.path.join(INCUBATING_NAMES_ROOT, f"{NAMES_CLASS}.g.cs")] = emit_names_file(
        load_resolved_registry()
    )
    expected[SCHEMA_URL_PATH] = emit_schema_url()
    return expected, drops, counts, by_root


def committed_files():
    """Every generated file currently on disk under the roots this emitter owns."""
    found = set()
    for base in (STABLE_ROOT, INCUBATING_ROOT, INCUBATING_NAMES_ROOT):
        if not os.path.isdir(base):
            continue
        for dirpath, _dirs, files in os.walk(base):
            for fn in files:
                if fn.endswith(".g.cs"):
                    found.add(os.path.join(dirpath, fn))
    if os.path.isfile(SCHEMA_URL_PATH):
        found.add(SCHEMA_URL_PATH)
    return found


def cmd_write():
    expected, all_drops, counts, by_root = owned_files()

    # Delete first, so a root that leaves the registry cannot linger as an orphan.
    for path in committed_files() - set(expected):
        os.remove(path)
    for path, text in expected.items():
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)

    # roots present in incubating but skipped in stable
    inc = roots_for_tier(by_root, False)
    stb = roots_for_tier(by_root, True)
    skipped_stable = sorted(set(inc) - set(stb))
    mixed = [
        r for r, a in by_root.items()
        if len({x.get("source_registry") for x in a}) > 1
    ]
    print(f"stable files:      {counts['stable']}")
    print(f"incubating files:  {counts['incubating']}")
    print(f"names file:        {os.path.join(INCUBATING_NAMES_ROOT, NAMES_CLASS + '.g.cs')}")
    print(f"schema URL file:   {SCHEMA_URL_PATH}")
    print(f"roots with NO stable file ({len(skipped_stable)}): {', '.join(skipped_stable)}")
    print(f"mixed-source roots: {', '.join(sorted(mixed)) or '(none)'}")
    print(f"dropped duplicate identifiers ({len(all_drops)}):")
    for d in sorted(all_drops, key=lambda x: (x["tier"], x["scope"], x["identifier"])):
        print(
            f"  [{d['tier']}] {d['scope']}.{d['identifier']}: "
            f"kept {d['kept']!r}, dropped {d['dropped']!r}"
        )


DIFF_LINE_BUDGET = 40


def cmd_check():
    """Snapshot gate: the committed tree must equal what the emitter produces now.

    Reports a readable per-file diff rather than a single aggregate mismatch —
    the point of a snapshot is to show *what* drifted. VerifyAttributesHash
    still guards the same files against hand-edits; this says why.
    """
    expected, _drops, _counts, _by_root = owned_files()
    failures = []

    for path in sorted(set(expected) | committed_files()):
        want = expected.get(path)
        if want is None:
            failures.append(f"stale generated file (no registry root produces it): {path}")
            continue
        if not os.path.isfile(path):
            failures.append(f"missing generated file: {path}")
            continue
        with open(path, encoding="utf-8", newline="") as f:
            got = f.read()
        if got == want:
            continue
        import difflib

        diff = list(
            difflib.unified_diff(
                got.splitlines(keepends=True),
                want.splitlines(keepends=True),
                fromfile=f"{path} (committed)",
                tofile=f"{path} (emitter)",
                n=1,
            )
        )
        body = "".join(diff[:DIFF_LINE_BUDGET]).rstrip("\n")
        if len(diff) > DIFF_LINE_BUDGET:
            body += f"\n  … {len(diff) - DIFF_LINE_BUDGET} more diff lines"
        failures.append(f"stale generated file: {path}\n{body}")

    if failures:
        sys.stderr.write("\n".join(failures) + "\n")
        sys.stderr.write(
            f"\n{len(failures)} file(s) differ. Regenerate with: "
            "emit_attributes.py --write   (then ./build.sh SeedAttributesHash)\n"
        )
        return 1
    return 0


def main(argv):
    if len(argv) >= 2 and argv[1] == "--stdout":
        if len(argv) != 4:
            sys.stderr.write("usage: emit_attributes.py --stdout {root} {stable|incubating}\n")
            sys.exit(2)
        cmd_stdout(argv[2], argv[3])
    elif len(argv) == 2 and argv[1] == "--write":
        cmd_write()
    elif len(argv) == 2 and argv[1] == "--check":
        sys.exit(cmd_check())
    else:
        sys.stderr.write(__doc__)
        sys.exit(2)


if __name__ == "__main__":
    main(sys.argv)
