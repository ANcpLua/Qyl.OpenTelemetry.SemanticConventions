// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

extern alias analyzers;

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using DiagnosticDescriptors = analyzers::Qyl.OpenTelemetry.SemanticConventions.Analyzers.DiagnosticDescriptors;

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator;

/// <summary>
///   Reflects the analyzer assembly to enumerate everything the renderers + rewriters need:
///   the <see cref="DiagnosticDescriptor"/> catalog, the set of fixable IDs, and the
///   <c>Id → ClassName</c> map (covering multi-id analyzers by pointing every id at the
///   same class). The runtime descriptor remains the source of truth; this class is the
///   one place that depends on <see cref="Activator.CreateInstance(Type)"/> over analyzer
///   types — keeping that fragility contained.
/// </summary>
internal static class DescriptorCatalog
{
    /// <summary>
    ///   Returns every distinct <see cref="DiagnosticDescriptor"/> reported by the analyzer
    ///   assembly, ordered by id for deterministic output. Walks two sources because the
    ///   project mixes a central <c>DiagnosticDescriptors</c> file with analyzers that
    ///   declare descriptors privately.
    /// </summary>
    public static IReadOnlyList<DiagnosticDescriptor> GetDescriptors()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var descriptors = new List<DiagnosticDescriptor>();

        // (1) Centralised DiagnosticDescriptors.cs fields.
        foreach (var field in typeof(DiagnosticDescriptors)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(DiagnosticDescriptor)))
        {
            if (field.GetValue(null) is DiagnosticDescriptor d && seen.Add(d.Id))
                descriptors.Add(d);
        }

        // (2) Per-analyzer SupportedDiagnostics for QYL00XX-named analyzers that define
        //     their descriptor locally (no entry in DiagnosticDescriptors.cs).
        foreach (var type in typeof(DiagnosticDescriptors).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
                continue;

            DiagnosticAnalyzer? analyzer;
            try { analyzer = (DiagnosticAnalyzer?)Activator.CreateInstance(type); }
            catch { continue; }
            if (analyzer is null) continue;

            foreach (var d in analyzer.SupportedDiagnostics)
            {
                if (seen.Add(d.Id))
                    descriptors.Add(d);
            }
        }

        return descriptors
            .OrderBy(d => d.Id, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    ///   Returns the set of <c>QYL*</c> diagnostic ids any <see cref="CodeFixProvider"/>
    ///   advertises through <see cref="CodeFixProvider.FixableDiagnosticIds"/>. Filters
    ///   to the <c>QYL</c> band so a code-fix that happens to advertise a non-package id
    ///   (e.g., a CS***) doesn't leak into per-rule "Code fix: Yes" labels.
    /// </summary>
    public static HashSet<string> GetFixableDiagnosticIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in typeof(DiagnosticDescriptors).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CodeFixProvider).IsAssignableFrom(type))
                continue;

            CodeFixProvider? provider;
            try { provider = (CodeFixProvider?)Activator.CreateInstance(type); }
            catch { continue; }
            if (provider is null) continue;

            foreach (var id in provider.FixableDiagnosticIds)
            {
                if (id.StartsWith("QYL", StringComparison.Ordinal))
                    ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>
    ///   Walks every concrete <see cref="DiagnosticAnalyzer"/> in the analyzer assembly
    ///   and builds <c>Id → ClassName</c>. Analyzers that register multiple ids point
    ///   all of those ids at the same class — matching the
    ///   <c>AL1003ToAl1004SpanComparisonAnalyzer</c> shape in ANcpLua.Analyzers' Shipped.md.
    /// </summary>
    public static Dictionary<string, string> BuildIdToClassMap()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in typeof(DiagnosticDescriptors).Assembly.GetTypes())
        {
            if (type.IsAbstract) continue;
            if (!typeof(DiagnosticAnalyzer).IsAssignableFrom(type)) continue;
            {
                if (Activator.CreateInstance(type) is DiagnosticAnalyzer a)
                {
                    foreach (var d in a.SupportedDiagnostics)
                        map[d.Id] = type.Name;
                }
            }
        }
        return map;
    }

    /// <summary>
    ///   Variant used by <see cref="EnforceIdsRewriter"/>: emits <c>ClassName → realId</c>
    ///   pairs separately for analyzers and code-fix providers (where "realId" is the
    ///   smallest descriptor id, ordinal-sorted, the type advertises). Multi-id analyzers
    ///   still collapse to one anchor id so the source rewriter has one target per class.
    /// </summary>
    public static (Dictionary<string, string> AnalyzerIds, Dictionary<string, string> CodeFixIds) BuildClassMaps()
    {
        var analyzers = new Dictionary<string, string>(StringComparer.Ordinal);
        var codeFixes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var type in typeof(DiagnosticDescriptors).Assembly.GetTypes())
        {
            if (type.IsAbstract) continue;

            if (typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            {
                try
                {
                    if (Activator.CreateInstance(type) is DiagnosticAnalyzer a && a.SupportedDiagnostics.Length > 0)
                        analyzers[type.Name] = a.SupportedDiagnostics
                            .OrderBy(d => d.Id, StringComparer.Ordinal).First().Id;
                }
                catch { /* analyzers with non-default ctors are skipped */ }
            }
            else if (typeof(CodeFixProvider).IsAssignableFrom(type))
            {
                {
                    if (Activator.CreateInstance(type) is CodeFixProvider p && p.FixableDiagnosticIds.Length > 0)
                        codeFixes[type.Name] = p.FixableDiagnosticIds
                            .OrderBy(s => s, StringComparer.Ordinal).First();
                }
            }
        }
        return (analyzers, codeFixes);
    }
}
