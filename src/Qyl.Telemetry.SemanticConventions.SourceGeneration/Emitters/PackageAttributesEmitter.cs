using Qyl.Telemetry.SemanticConventions.SourceGeneration.Extractors;
using Qyl.Telemetry.SemanticConventions.SourceGeneration.Models;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Emitters;

/// <summary>
/// Emits the compiled-package projection of the attribute registry for one stability tier:
/// one <c>public static class {Root}Attributes</c> per registry root namespace under
/// <c>{package root}.Attributes.{Root}</c>, in the layout the
/// <c>Qyl.Telemetry.SemanticConventions</c> and <c>.Incubating</c> packages ship. The stable
/// tier also emits <c>{package root}.SchemaUrl</c>, the pinned schema URL of that tier.
/// </summary>
/// <remarks>
/// Shape rules:
/// <list type="bullet">
///   <item>Member names are the PascalCase key without its root segment (<c>http.route</c> →
///   <c>Route</c>); a dot-less key is rooted in its own namespace with the constant named after
///   the root.</item>
///   <item>PascalCase treats <c>.</c> and <c>_</c> identically, so distinct keys or enum ids can
///   collapse to one identifier. Within each class and each <c>…Values</c> class exactly one
///   entry survives: the non-deprecated one, then the ordinally-first key or value.</item>
///   <item>Stable tier: stable rows plus deprecated migration symbols; incubating tier: every
///   row. Enum members follow their own stability and deprecation.</item>
///   <item>Each file's header cites the majority source registry of its root (ties prefer
///   core); qyl-owned roots cite the repository's own registry input.</item>
/// </list>
/// </remarks>
internal static class PackageAttributesEmitter
{
    private const string Pad = "    ";
    private const string QylSourceRegistry = "qyl";

    public static EquatableArray<FileWithName> Generate(SemConvPackageMarkerModel marker, RegistryModel registry)
    {
        var stable = marker.Filter == StabilityFilter.StableOnly;
        var byRoot = new Dictionary<string, List<AttributeModel>>(StringComparer.Ordinal);
        foreach (var attribute in registry.Catalog)
        {
            if (!StabilityFiltering.IsIncludedOrDeprecated(attribute.Stability, attribute.Deprecated, marker.Filter))
                continue;

            var root = RootOf(attribute.Key);
            if (!byRoot.TryGetValue(root, out var list))
            {
                list = new List<AttributeModel>();
                byRoot.Add(root, list);
            }
            list.Add(attribute);
        }

        var roots = new List<string>(byRoot.Keys);
        roots.Sort(StringComparer.Ordinal);

        var files = new List<FileWithName>(roots.Count + 1);
        foreach (var root in roots)
            files.Add(EmitRoot(marker.RootNamespace, root, byRoot[root], stable));

        if (stable)
            files.Add(EmitSchemaUrl(marker.RootNamespace, registry.Pin));

        return files.ToEquatableArray();
    }

    private static string RootOf(string key)
    {
        var dot = key.IndexOf('.');
        return dot < 0 ? key : key.Substring(0, dot);
    }

    private static FileWithName EmitRoot(string rootNamespace, string root, List<AttributeModel> attributes, bool stable)
    {
        var pascalRoot = SourceWriter.ToPascalCase(root);
        var className = pascalRoot + "Attributes";
        var ns = rootNamespace + ".Attributes." + pascalRoot;
        var suffix = stable ? string.Empty : " (incubating)";
        var filter = stable ? StabilityFilter.StableOnly : StabilityFilter.AllStabilities;

        var w = new PackageSourceWriter();
        foreach (var line in ProvenanceLines(attributes))
            w.Line(line);
        w.Line();
        w.Line("// Copyright (c) 2025-2026 ancplua");
        w.Line();
        w.Line("#nullable enable");
        w.Line();
        w.Line("namespace " + ns + ";");
        w.Line();
        w.Line("/// <summary>" + pascalRoot + " Attributes" + suffix + ".</summary>");
        w.Line("public static class " + className);
        w.Line("{");

        var kept = ResolveCollisions(
            attributes,
            a => MemberName(root, a.Key),
            static a => a.Key,
            static a => a.Deprecated is not null || a.Stability == StabilityModel.Deprecated);
        kept.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));

        var first = true;
        foreach (var attribute in kept)
        {
            if (!first)
                w.Line();
            first = false;

            var memberName = MemberName(root, attribute.Key);

            var brief = attribute.Brief.TrimEnd('\n');
            w.Summary(Pad, brief.Length > 0 ? PackageDocComments.RenderInline(brief).Split('\n') : Array.Empty<string>());

            var noteLines = PackageDocComments.RenderMarkdown(attribute.Note);
            if (noteLines.Count > 0)
                w.Remarks(Pad, noteLines);

            WriteObsolete(w, Pad, attribute.Deprecated, attribute.Stability);
            w.Line(Pad + "public const string " + memberName + " = \"" + attribute.Key + "\";");

            if (attribute.Type is AttributeTypeModel.EnumType enumType)
                WriteEnumValues(w, memberName, enumType, filter);
        }

        w.Line("}");

        var hintName = GeneratedSourceNames.ForPartialType(ns, className);
        return new FileWithName(hintName, w.ToString());
    }

    private static void WriteEnumValues(PackageSourceWriter w, string memberName, AttributeTypeModel.EnumType enumType, StabilityFilter filter)
    {
        var included = new List<EnumMemberModel>();
        foreach (var member in enumType.Members)
        {
            if (StabilityFiltering.IsIncludedOrDeprecated(member.Stability, member.Deprecated, filter))
                included.Add(member);
        }

        var members = ResolveCollisions(
            included,
            static m => SourceWriter.ToPascalCase(m.Id),
            static m => m.Value,
            static m => m.Deprecated is not null || m.Stability == StabilityModel.Deprecated);
        if (members.Count == 0)
            return;

        members.Sort(static (a, b) => StringComparer.Ordinal.Compare(
            SourceWriter.ToPascalCase(a.Id), SourceWriter.ToPascalCase(b.Id)));

        var innerPad = Pad + Pad;
        w.Line();
        w.Summary(Pad, new[] { "Values for the <c>" + memberName + "</c> attribute." });
        w.Line(Pad + "public static class " + memberName + "Values");
        w.Line(Pad + "{");
        var first = true;
        foreach (var member in members)
        {
            if (!first)
                w.Line();
            first = false;

            w.Summary(innerPad, MemberSummary(member.Brief, member.Id));
            WriteObsolete(w, innerPad, member.Deprecated, member.Stability);
            w.Line(innerPad + "public const string " + SourceWriter.ToPascalCase(member.Id) + " = \"" + member.Value + "\";");
        }

        var identifiers = new List<string>(members.Count);
        var values = new List<string>(members.Count);
        foreach (var member in members)
        {
            identifiers.Add(SourceWriter.ToPascalCase(member.Id));
            values.Add(member.Value);
        }
        w.Line();
        foreach (var line in EnumValueSet.Lines(identifiers, values, memberName + "Values", EnumValueSet.DeclarationOrderSummary))
            w.Line(innerPad + line);

        w.Line(Pad + "}");
    }

    private static string[] MemberSummary(string brief, string memberId)
    {
        if (!string.IsNullOrWhiteSpace(brief))
        {
            var rendered = PackageDocComments.RenderInline(brief.TrimEnd('\n'));
            if (!rendered.EndsWith(".", StringComparison.Ordinal))
                rendered += ".";
            return rendered.Split('\n');
        }

        return new[] { memberId + "." };
    }

    private static void WriteObsolete(PackageSourceWriter w, string pad, DeprecatedModel? deprecated, StabilityModel stability)
    {
        if (deprecated is not null)
        {
            var message = SourceWriter.EscapeAttribute(SourceWriter.DeprecatedMessage(deprecated));
            w.Line(pad + "[global::System.Obsolete(\"" + message + "\", false)]");
        }
        else if (stability == StabilityModel.Deprecated)
        {
            w.Line(pad + "[global::System.Obsolete(\"Deprecated.\", false)]");
        }
    }

    private static string MemberName(string root, string key)
    {
        var relative = key.StartsWith(root + ".", StringComparison.Ordinal)
            ? key.Substring(root.Length + 1)
            : key;
        return SourceWriter.ToPascalCase(relative);
    }

    /// <summary>
    /// Keeps exactly one entry per C# identifier: the non-deprecated one first, then the
    /// ordinally-first sort key. Entry order is otherwise preserved for the survivors.
    /// </summary>
    private static List<T> ResolveCollisions<T>(
        List<T> entries,
        Func<T, string> identifier,
        Func<T, string> sortKey,
        Func<T, bool> isDeprecated)
    {
        var groups = new Dictionary<string, List<T>>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var entry in entries)
        {
            var id = identifier(entry);
            if (!groups.TryGetValue(id, out var group))
            {
                group = new List<T>();
                groups.Add(id, group);
                order.Add(id);
            }
            group.Add(entry);
        }

        var kept = new List<T>(order.Count);
        foreach (var id in order)
        {
            var group = groups[id];
            if (group.Count == 1)
            {
                kept.Add(group[0]);
                continue;
            }

            kept.Add(group
                .OrderBy(isDeprecated)
                .ThenBy(sortKey, StringComparer.Ordinal)
                .First());
        }
        return kept;
    }

    /// <summary>The <c>&lt;auto-generated/&gt;</c> header: majority source registry of the root, ties preferring core.</summary>
    private static string[] ProvenanceLines(List<AttributeModel> attributes)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var attribute in attributes)
        {
            var registry = attribute.Source.Registry;
            if (counts.TryGetValue(registry, out var count))
            {
                counts[registry] = count + 1;
            }
            else
            {
                counts.Add(registry, 1);
                order.Add(registry);
            }
        }

        var best = order
            .OrderByDescending(r => counts[r])
            .ThenBy(r => !r.EqualsOrdinal("core"))
            .First();

        if (best.EqualsOrdinal(QylSourceRegistry))
        {
            return new[]
            {
                "// <auto-generated/>",
                "// Generated by qyl's emitter from the qyl-owned registry",
                "// Source: SourceGeneration/Resources/qyl-registry.json",
                "// Licensed under Apache-2.0",
                "// </auto-generated>",
            };
        }

        var source = attributes.First(a => a.Source.Registry.EqualsOrdinal(best)).Source;
        return new[]
        {
            "// <auto-generated/>",
            "// Generated by qyl's Weaver pipeline from " + RepositorySlug(best) + "@" + source.Commit,
            "// Schema: " + source.SchemaUrl,
            "// Licensed under Apache-2.0 (inherited from OpenTelemetry upstream)",
            "// </auto-generated>",
        };
    }

    private static string RepositorySlug(string registry) => registry switch
    {
        "core" => "open-telemetry/semantic-conventions",
        "genai" => "open-telemetry/semantic-conventions-genai",
        _ => registry,
    };

    private static FileWithName EmitSchemaUrl(string rootNamespace, RegistryPinModel pin)
    {
        var w = new PackageSourceWriter();
        w.Line("// <auto-generated/>");
        w.Line("// Generated by qyl's Weaver pipeline from open-telemetry/semantic-conventions@" + pin.CoreCommit);
        w.Line("// Schema: " + pin.SchemaUrl);
        w.Line("// Licensed under Apache-2.0 (inherited from OpenTelemetry upstream)");
        w.Line("// </auto-generated>");
        w.Line();
        w.Line("// Copyright (c) 2025-2026 ancplua");
        w.Line();
        w.Line("namespace " + rootNamespace + ";");
        w.Line();
        w.Line("/// <summary>Schema URL for OpenTelemetry Semantic Conventions " + pin.SchemaVersion + ".</summary>");
        w.Line("public static partial class SchemaUrl");
        w.Line("{");
        w.Line(Pad + "/// <summary>The schema URL for OTel semconv " + pin.SchemaVersion + ".</summary>");
        w.Line(Pad + "public const string Current = " + PackageSourceWriter.CSharpString(pin.SchemaUrl) + ";");
        w.Line("}");

        return new FileWithName(GeneratedSourceNames.ForPartialType(rootNamespace, "SchemaUrl"), w.ToString());
    }
}
