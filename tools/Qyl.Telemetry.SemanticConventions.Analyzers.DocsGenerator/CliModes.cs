// Copyright (c) Alexander Nachtmann
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.Telemetry.SemanticConventions.Analyzers.DocsGenerator;

/// <summary>
///   The generator's operating modes. <c>eng/build/Build.cs</c> runs Generate
///   (<c>GenerateDocs</c>), Audit (<c>AuditDocs</c>) and EnforceIdsApply
///   (<c>EnforceIdsApply</c>); the generator's own build runs Check and EnforceIdsCheck;
///   <c>--rewrite-shipped</c> is an ad-hoc fixup.
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
    public static Mode Parse(string[] args)
    {
        var enforce = args.Any(a => IsFlag(a, "enforce-ids"));
        var apply = args.Any(a => IsFlag(a, "apply"));
        if (enforce) return apply ? Mode.EnforceIdsApply : Mode.EnforceIdsCheck;

        if (args.Any(a => IsFlag(a, "rewrite-shipped"))) return Mode.RewriteShipped;

        foreach (var arg in args)
        {
            if (IsFlag(arg, "audit")) return Mode.Audit;
            if (IsFlag(arg, "check") || Eq(arg, "validate")) return Mode.Check;
        }
        return Mode.Generate;

        static bool IsFlag(string arg, string name) => Eq(arg, name) || Eq(arg, "--" + name);
        static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
