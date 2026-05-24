// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// Reserves diagnostic ID slots in the <c>QYL</c> range for the 156-entry supplemental
/// OpenTelemetry semantic-conventions migration catalog. Each reserved descriptor is
/// <c>Hidden</c>, <c>isEnabledByDefault: false</c>, and has no <c>Initialize</c> action —
/// it exists purely so <c>AnalyzerReleases.Shipped.md</c> can list a stable 1:1 mapping
/// between every catalog entry and a public diagnostic ID without minting hundreds of
/// IDE-visible rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReservedCatalogStubsAnalyzer : DiagnosticAnalyzer
{
    private const string Category = "OpenTelemetry.SemanticConventions";
    private const string Description = "Reserved ID slot for the OpenTelemetry semantic-conventions migration catalog. Runtime reports surface via QYL0030/QYL0031/QYL0032.";

    private static readonly DiagnosticDescriptor s_qyl0003 = new(
        id: "QYL0003",
        title: "Reserved for catalog-derived diagnostic QYL0003",
        messageFormat: "Reserved QYL0003",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0004 = new(
        id: "QYL0004",
        title: "Reserved for catalog-derived diagnostic QYL0004",
        messageFormat: "Reserved QYL0004",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0006 = new(
        id: "QYL0006",
        title: "Reserved for catalog-derived diagnostic QYL0006",
        messageFormat: "Reserved QYL0006",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0007 = new(
        id: "QYL0007",
        title: "Reserved for catalog-derived diagnostic QYL0007",
        messageFormat: "Reserved QYL0007",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0008 = new(
        id: "QYL0008",
        title: "Reserved for catalog-derived diagnostic QYL0008",
        messageFormat: "Reserved QYL0008",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0009 = new(
        id: "QYL0009",
        title: "Reserved for catalog-derived diagnostic QYL0009",
        messageFormat: "Reserved QYL0009",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0015 = new(
        id: "QYL0015",
        title: "Reserved for catalog-derived diagnostic QYL0015",
        messageFormat: "Reserved QYL0015",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0016 = new(
        id: "QYL0016",
        title: "Reserved for catalog-derived diagnostic QYL0016",
        messageFormat: "Reserved QYL0016",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0017 = new(
        id: "QYL0017",
        title: "Reserved for catalog-derived diagnostic QYL0017",
        messageFormat: "Reserved QYL0017",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0018 = new(
        id: "QYL0018",
        title: "Reserved for catalog-derived diagnostic QYL0018",
        messageFormat: "Reserved QYL0018",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0019 = new(
        id: "QYL0019",
        title: "Reserved for catalog-derived diagnostic QYL0019",
        messageFormat: "Reserved QYL0019",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0020 = new(
        id: "QYL0020",
        title: "Reserved for catalog-derived diagnostic QYL0020",
        messageFormat: "Reserved QYL0020",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0022 = new(
        id: "QYL0022",
        title: "Reserved for catalog-derived diagnostic QYL0022",
        messageFormat: "Reserved QYL0022",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0023 = new(
        id: "QYL0023",
        title: "Reserved for catalog-derived diagnostic QYL0023",
        messageFormat: "Reserved QYL0023",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0024 = new(
        id: "QYL0024",
        title: "Reserved for catalog-derived diagnostic QYL0024",
        messageFormat: "Reserved QYL0024",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0025 = new(
        id: "QYL0025",
        title: "Reserved for catalog-derived diagnostic QYL0025",
        messageFormat: "Reserved QYL0025",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0026 = new(
        id: "QYL0026",
        title: "Reserved for catalog-derived diagnostic QYL0026",
        messageFormat: "Reserved QYL0026",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0027 = new(
        id: "QYL0027",
        title: "Reserved for catalog-derived diagnostic QYL0027",
        messageFormat: "Reserved QYL0027",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0028 = new(
        id: "QYL0028",
        title: "Reserved for catalog-derived diagnostic QYL0028",
        messageFormat: "Reserved QYL0028",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0029 = new(
        id: "QYL0029",
        title: "Reserved for catalog-derived diagnostic QYL0029",
        messageFormat: "Reserved QYL0029",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0033 = new(
        id: "QYL0033",
        title: "Reserved for catalog-derived diagnostic QYL0033",
        messageFormat: "Reserved QYL0033",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0034 = new(
        id: "QYL0034",
        title: "Reserved for catalog-derived diagnostic QYL0034",
        messageFormat: "Reserved QYL0034",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0035 = new(
        id: "QYL0035",
        title: "Reserved for catalog-derived diagnostic QYL0035",
        messageFormat: "Reserved QYL0035",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0036 = new(
        id: "QYL0036",
        title: "Reserved for catalog-derived diagnostic QYL0036",
        messageFormat: "Reserved QYL0036",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0037 = new(
        id: "QYL0037",
        title: "Reserved for catalog-derived diagnostic QYL0037",
        messageFormat: "Reserved QYL0037",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0038 = new(
        id: "QYL0038",
        title: "Reserved for catalog-derived diagnostic QYL0038",
        messageFormat: "Reserved QYL0038",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0039 = new(
        id: "QYL0039",
        title: "Reserved for catalog-derived diagnostic QYL0039",
        messageFormat: "Reserved QYL0039",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0040 = new(
        id: "QYL0040",
        title: "Reserved for catalog-derived diagnostic QYL0040",
        messageFormat: "Reserved QYL0040",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0041 = new(
        id: "QYL0041",
        title: "Reserved for catalog-derived diagnostic QYL0041",
        messageFormat: "Reserved QYL0041",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0042 = new(
        id: "QYL0042",
        title: "Reserved for catalog-derived diagnostic QYL0042",
        messageFormat: "Reserved QYL0042",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0043 = new(
        id: "QYL0043",
        title: "Reserved for catalog-derived diagnostic QYL0043",
        messageFormat: "Reserved QYL0043",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0044 = new(
        id: "QYL0044",
        title: "Reserved for catalog-derived diagnostic QYL0044",
        messageFormat: "Reserved QYL0044",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0045 = new(
        id: "QYL0045",
        title: "Reserved for catalog-derived diagnostic QYL0045",
        messageFormat: "Reserved QYL0045",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0046 = new(
        id: "QYL0046",
        title: "Reserved for catalog-derived diagnostic QYL0046",
        messageFormat: "Reserved QYL0046",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0047 = new(
        id: "QYL0047",
        title: "Reserved for catalog-derived diagnostic QYL0047",
        messageFormat: "Reserved QYL0047",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0048 = new(
        id: "QYL0048",
        title: "Reserved for catalog-derived diagnostic QYL0048",
        messageFormat: "Reserved QYL0048",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0049 = new(
        id: "QYL0049",
        title: "Reserved for catalog-derived diagnostic QYL0049",
        messageFormat: "Reserved QYL0049",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0050 = new(
        id: "QYL0050",
        title: "Reserved for catalog-derived diagnostic QYL0050",
        messageFormat: "Reserved QYL0050",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0051 = new(
        id: "QYL0051",
        title: "Reserved for catalog-derived diagnostic QYL0051",
        messageFormat: "Reserved QYL0051",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0052 = new(
        id: "QYL0052",
        title: "Reserved for catalog-derived diagnostic QYL0052",
        messageFormat: "Reserved QYL0052",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0053 = new(
        id: "QYL0053",
        title: "Reserved for catalog-derived diagnostic QYL0053",
        messageFormat: "Reserved QYL0053",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0054 = new(
        id: "QYL0054",
        title: "Reserved for catalog-derived diagnostic QYL0054",
        messageFormat: "Reserved QYL0054",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0055 = new(
        id: "QYL0055",
        title: "Reserved for catalog-derived diagnostic QYL0055",
        messageFormat: "Reserved QYL0055",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0056 = new(
        id: "QYL0056",
        title: "Reserved for catalog-derived diagnostic QYL0056",
        messageFormat: "Reserved QYL0056",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0057 = new(
        id: "QYL0057",
        title: "Reserved for catalog-derived diagnostic QYL0057",
        messageFormat: "Reserved QYL0057",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0058 = new(
        id: "QYL0058",
        title: "Reserved for catalog-derived diagnostic QYL0058",
        messageFormat: "Reserved QYL0058",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0059 = new(
        id: "QYL0059",
        title: "Reserved for catalog-derived diagnostic QYL0059",
        messageFormat: "Reserved QYL0059",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0060 = new(
        id: "QYL0060",
        title: "Reserved for catalog-derived diagnostic QYL0060",
        messageFormat: "Reserved QYL0060",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0062 = new(
        id: "QYL0062",
        title: "Reserved for catalog-derived diagnostic QYL0062",
        messageFormat: "Reserved QYL0062",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0080 = new(
        id: "QYL0080",
        title: "Reserved for catalog-derived diagnostic QYL0080",
        messageFormat: "Reserved QYL0080",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0081 = new(
        id: "QYL0081",
        title: "Reserved for catalog-derived diagnostic QYL0081",
        messageFormat: "Reserved QYL0081",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0082 = new(
        id: "QYL0082",
        title: "Reserved for catalog-derived diagnostic QYL0082",
        messageFormat: "Reserved QYL0082",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0084 = new(
        id: "QYL0084",
        title: "Reserved for catalog-derived diagnostic QYL0084",
        messageFormat: "Reserved QYL0084",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0087 = new(
        id: "QYL0087",
        title: "Reserved for catalog-derived diagnostic QYL0087",
        messageFormat: "Reserved QYL0087",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0094 = new(
        id: "QYL0094",
        title: "Reserved for catalog-derived diagnostic QYL0094",
        messageFormat: "Reserved QYL0094",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0095 = new(
        id: "QYL0095",
        title: "Reserved for catalog-derived diagnostic QYL0095",
        messageFormat: "Reserved QYL0095",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0097 = new(
        id: "QYL0097",
        title: "Reserved for catalog-derived diagnostic QYL0097",
        messageFormat: "Reserved QYL0097",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0098 = new(
        id: "QYL0098",
        title: "Reserved for catalog-derived diagnostic QYL0098",
        messageFormat: "Reserved QYL0098",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0099 = new(
        id: "QYL0099",
        title: "Reserved for catalog-derived diagnostic QYL0099",
        messageFormat: "Reserved QYL0099",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0100 = new(
        id: "QYL0100",
        title: "Reserved for catalog-derived diagnostic QYL0100",
        messageFormat: "Reserved QYL0100",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0101 = new(
        id: "QYL0101",
        title: "Reserved for catalog-derived diagnostic QYL0101",
        messageFormat: "Reserved QYL0101",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0102 = new(
        id: "QYL0102",
        title: "Reserved for catalog-derived diagnostic QYL0102",
        messageFormat: "Reserved QYL0102",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0103 = new(
        id: "QYL0103",
        title: "Reserved for catalog-derived diagnostic QYL0103",
        messageFormat: "Reserved QYL0103",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0104 = new(
        id: "QYL0104",
        title: "Reserved for catalog-derived diagnostic QYL0104",
        messageFormat: "Reserved QYL0104",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0105 = new(
        id: "QYL0105",
        title: "Reserved for catalog-derived diagnostic QYL0105",
        messageFormat: "Reserved QYL0105",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0106 = new(
        id: "QYL0106",
        title: "Reserved for catalog-derived diagnostic QYL0106",
        messageFormat: "Reserved QYL0106",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0111 = new(
        id: "QYL0111",
        title: "Reserved for catalog-derived diagnostic QYL0111",
        messageFormat: "Reserved QYL0111",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0112 = new(
        id: "QYL0112",
        title: "Reserved for catalog-derived diagnostic QYL0112",
        messageFormat: "Reserved QYL0112",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0114 = new(
        id: "QYL0114",
        title: "Reserved for catalog-derived diagnostic QYL0114",
        messageFormat: "Reserved QYL0114",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0115 = new(
        id: "QYL0115",
        title: "Reserved for catalog-derived diagnostic QYL0115",
        messageFormat: "Reserved QYL0115",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0116 = new(
        id: "QYL0116",
        title: "Reserved for catalog-derived diagnostic QYL0116",
        messageFormat: "Reserved QYL0116",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0117 = new(
        id: "QYL0117",
        title: "Reserved for catalog-derived diagnostic QYL0117",
        messageFormat: "Reserved QYL0117",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0118 = new(
        id: "QYL0118",
        title: "Reserved for catalog-derived diagnostic QYL0118",
        messageFormat: "Reserved QYL0118",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0119 = new(
        id: "QYL0119",
        title: "Reserved for catalog-derived diagnostic QYL0119",
        messageFormat: "Reserved QYL0119",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0120 = new(
        id: "QYL0120",
        title: "Reserved for catalog-derived diagnostic QYL0120",
        messageFormat: "Reserved QYL0120",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0121 = new(
        id: "QYL0121",
        title: "Reserved for catalog-derived diagnostic QYL0121",
        messageFormat: "Reserved QYL0121",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0122 = new(
        id: "QYL0122",
        title: "Reserved for catalog-derived diagnostic QYL0122",
        messageFormat: "Reserved QYL0122",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0123 = new(
        id: "QYL0123",
        title: "Reserved for catalog-derived diagnostic QYL0123",
        messageFormat: "Reserved QYL0123",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0125 = new(
        id: "QYL0125",
        title: "Reserved for catalog-derived diagnostic QYL0125",
        messageFormat: "Reserved QYL0125",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0126 = new(
        id: "QYL0126",
        title: "Reserved for catalog-derived diagnostic QYL0126",
        messageFormat: "Reserved QYL0126",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0127 = new(
        id: "QYL0127",
        title: "Reserved for catalog-derived diagnostic QYL0127",
        messageFormat: "Reserved QYL0127",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0132 = new(
        id: "QYL0132",
        title: "Reserved for catalog-derived diagnostic QYL0132",
        messageFormat: "Reserved QYL0132",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0133 = new(
        id: "QYL0133",
        title: "Reserved for catalog-derived diagnostic QYL0133",
        messageFormat: "Reserved QYL0133",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0134 = new(
        id: "QYL0134",
        title: "Reserved for catalog-derived diagnostic QYL0134",
        messageFormat: "Reserved QYL0134",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0136 = new(
        id: "QYL0136",
        title: "Reserved for catalog-derived diagnostic QYL0136",
        messageFormat: "Reserved QYL0136",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0137 = new(
        id: "QYL0137",
        title: "Reserved for catalog-derived diagnostic QYL0137",
        messageFormat: "Reserved QYL0137",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0138 = new(
        id: "QYL0138",
        title: "Reserved for catalog-derived diagnostic QYL0138",
        messageFormat: "Reserved QYL0138",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0139 = new(
        id: "QYL0139",
        title: "Reserved for catalog-derived diagnostic QYL0139",
        messageFormat: "Reserved QYL0139",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0140 = new(
        id: "QYL0140",
        title: "Reserved for catalog-derived diagnostic QYL0140",
        messageFormat: "Reserved QYL0140",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0141 = new(
        id: "QYL0141",
        title: "Reserved for catalog-derived diagnostic QYL0141",
        messageFormat: "Reserved QYL0141",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0142 = new(
        id: "QYL0142",
        title: "Reserved for catalog-derived diagnostic QYL0142",
        messageFormat: "Reserved QYL0142",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0143 = new(
        id: "QYL0143",
        title: "Reserved for catalog-derived diagnostic QYL0143",
        messageFormat: "Reserved QYL0143",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0144 = new(
        id: "QYL0144",
        title: "Reserved for catalog-derived diagnostic QYL0144",
        messageFormat: "Reserved QYL0144",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0145 = new(
        id: "QYL0145",
        title: "Reserved for catalog-derived diagnostic QYL0145",
        messageFormat: "Reserved QYL0145",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0146 = new(
        id: "QYL0146",
        title: "Reserved for catalog-derived diagnostic QYL0146",
        messageFormat: "Reserved QYL0146",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0147 = new(
        id: "QYL0147",
        title: "Reserved for catalog-derived diagnostic QYL0147",
        messageFormat: "Reserved QYL0147",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0148 = new(
        id: "QYL0148",
        title: "Reserved for catalog-derived diagnostic QYL0148",
        messageFormat: "Reserved QYL0148",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0149 = new(
        id: "QYL0149",
        title: "Reserved for catalog-derived diagnostic QYL0149",
        messageFormat: "Reserved QYL0149",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0150 = new(
        id: "QYL0150",
        title: "Reserved for catalog-derived diagnostic QYL0150",
        messageFormat: "Reserved QYL0150",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0151 = new(
        id: "QYL0151",
        title: "Reserved for catalog-derived diagnostic QYL0151",
        messageFormat: "Reserved QYL0151",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0152 = new(
        id: "QYL0152",
        title: "Reserved for catalog-derived diagnostic QYL0152",
        messageFormat: "Reserved QYL0152",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0153 = new(
        id: "QYL0153",
        title: "Reserved for catalog-derived diagnostic QYL0153",
        messageFormat: "Reserved QYL0153",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0154 = new(
        id: "QYL0154",
        title: "Reserved for catalog-derived diagnostic QYL0154",
        messageFormat: "Reserved QYL0154",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0155 = new(
        id: "QYL0155",
        title: "Reserved for catalog-derived diagnostic QYL0155",
        messageFormat: "Reserved QYL0155",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    private static readonly DiagnosticDescriptor s_qyl0156 = new(
        id: "QYL0156",
        title: "Reserved for catalog-derived diagnostic QYL0156",
        messageFormat: "Reserved QYL0156",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Hidden,
        isEnabledByDefault: false,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            s_qyl0003,
            s_qyl0004,
            s_qyl0006,
            s_qyl0007,
            s_qyl0008,
            s_qyl0009,
            s_qyl0015,
            s_qyl0016,
            s_qyl0017,
            s_qyl0018,
            s_qyl0019,
            s_qyl0020,
            s_qyl0022,
            s_qyl0023,
            s_qyl0024,
            s_qyl0025,
            s_qyl0026,
            s_qyl0027,
            s_qyl0028,
            s_qyl0029,
            s_qyl0033,
            s_qyl0034,
            s_qyl0035,
            s_qyl0036,
            s_qyl0037,
            s_qyl0038,
            s_qyl0039,
            s_qyl0040,
            s_qyl0041,
            s_qyl0042,
            s_qyl0043,
            s_qyl0044,
            s_qyl0045,
            s_qyl0046,
            s_qyl0047,
            s_qyl0048,
            s_qyl0049,
            s_qyl0050,
            s_qyl0051,
            s_qyl0052,
            s_qyl0053,
            s_qyl0054,
            s_qyl0055,
            s_qyl0056,
            s_qyl0057,
            s_qyl0058,
            s_qyl0059,
            s_qyl0060,
            s_qyl0062,
            s_qyl0080,
            s_qyl0081,
            s_qyl0082,
            s_qyl0084,
            s_qyl0087,
            s_qyl0094,
            s_qyl0095,
            s_qyl0097,
            s_qyl0098,
            s_qyl0099,
            s_qyl0100,
            s_qyl0101,
            s_qyl0102,
            s_qyl0103,
            s_qyl0104,
            s_qyl0105,
            s_qyl0106,
            s_qyl0111,
            s_qyl0112,
            s_qyl0114,
            s_qyl0115,
            s_qyl0116,
            s_qyl0117,
            s_qyl0118,
            s_qyl0119,
            s_qyl0120,
            s_qyl0121,
            s_qyl0122,
            s_qyl0123,
            s_qyl0125,
            s_qyl0126,
            s_qyl0127,
            s_qyl0132,
            s_qyl0133,
            s_qyl0134,
            s_qyl0136,
            s_qyl0137,
            s_qyl0138,
            s_qyl0139,
            s_qyl0140,
            s_qyl0141,
            s_qyl0142,
            s_qyl0143,
            s_qyl0144,
            s_qyl0145,
            s_qyl0146,
            s_qyl0147,
            s_qyl0148,
            s_qyl0149,
            s_qyl0150,
            s_qyl0151,
            s_qyl0152,
            s_qyl0153,
            s_qyl0154,
            s_qyl0155,
            s_qyl0156);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        // Intentionally no actions registered — these descriptors are ID reservations only.
    }
}
