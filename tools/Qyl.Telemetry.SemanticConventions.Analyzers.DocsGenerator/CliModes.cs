// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.Telemetry.SemanticConventions.Analyzers.DocsGenerator;

/// <summary>
///   The generator's operating modes. Each maps to a Nuke target in <c>eng/build/Build.cs</c>
///   (<c>GenerateDocs</c>, <c>CheckDocs</c>, <c>AuditDocs</c>, <c>EnforceIds</c>,
///   <c>EnforceIdsApply</c>) plus a <c>--rewrite-shipped</c> ad-hoc fixup.
/// </summary>
internal enum Mode
{
    Generate,
    Check,
    Audit,
    EnforceIdsCheck,
    EnforceIdsApply,
    RewriteShipped,
}

internal static class CliModes
{
    /// <summary>
    ///   Nuke's <c>DotNetRunSettings.SetApplicationArguments</c> passes a single quoted
    ///   string, so <c>"--enforce-ids --apply"</c> arrives as <c>args[0]</c> instead of
    ///   two separate args. Flatten on whitespace so both invocation shapes work.
    /// </summary>
    public static Mode Parse(string[] args)
    {
        var flat = args
            .SelectMany(a => a.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        var enforce = flat.Any(a => IsFlag(a, "enforce-ids"));
        var apply = flat.Any(a => IsFlag(a, "apply"));
        if (enforce) return apply ? Mode.EnforceIdsApply : Mode.EnforceIdsCheck;

        if (flat.Any(a => IsFlag(a, "rewrite-shipped"))) return Mode.RewriteShipped;

        foreach (var arg in flat)
        {
            if (IsFlag(arg, "audit")) return Mode.Audit;
            if (IsFlag(arg, "check") || Eq(arg, "validate")) return Mode.Check;
        }
        return Mode.Generate;

        static bool IsFlag(string arg, string name) => Eq(arg, name) || Eq(arg, "--" + name);
        static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
