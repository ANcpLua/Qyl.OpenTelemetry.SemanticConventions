// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

internal static class DiagnosticDescriptors
{
    private const string Category = "OpenTelemetry.SemanticConventions";

    // Each rule anchors into the single generated docs file:
    //   docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#qyl0003
    // tools/Qyl.OpenTelemetry.SemanticConventions.Analyzers.DocsGenerator emits a
    // "### QYL0003" sub-section per descriptor; GitHub renders that as the lowercase
    // anchor #qyl0003. Run `./build.sh CheckDocs` to verify the committed markdown
    // still matches what the descriptors would emit; drift fails CI.
    private const string HelpLinkBase =
        "https://github.com/ANcpLua/Qyl.OpenTelemetry.SemanticConventions"
        + "/blob/main/docs/Qyl.OpenTelemetry.SemanticConventions.Analyzers.md#";

    public static readonly DiagnosticDescriptor DeprecatedSemconvConstant = new(
        id: "QYL0003",
        title: "Deprecated semantic-convention constant",
        messageFormat: "Semantic-convention constant '{0}' is deprecated: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "References to constants in OpenTelemetry.SemanticConventions.Attributes.* that carry [Obsolete]. Migrate to the replacement attribute named in the deprecation message.",
        helpLinkUri: HelpLinkBase + "qyl0003");

    public static readonly DiagnosticDescriptor RpcServerHasClientAddressAttribute = new(
        id: "QYL0002",
        title: "RPC server span must not include client.address / client.port",
        messageFormat: "RPC server span sets '{0}'; v1.41.0 removed client.* attributes from RPC server span definitions",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "From v1.41.0, RPC server spans extend the rpc base group directly and no longer include client.address or client.port. Use server.address / server.port instead.",
        helpLinkUri: HelpLinkBase + "qyl0002");

    public static readonly DiagnosticDescriptor GenAiExecuteToolMissingToolName = new(
        id: "QYL0400",
        title: "gen_ai.execute_tool span requires gen_ai.tool.name",
        messageFormat: "Method sets gen_ai.operation.name=\"execute_tool\" but does not set gen_ai.tool.name; the tool name is required for span naming as of v1.41.0",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "v1.41.0 made gen_ai.tool.name a required attribute on the gen_ai.execute_tool internal span; the canonical span name is 'execute_tool {gen_ai.tool.name}'.",
        helpLinkUri: HelpLinkBase + "qyl0400");

    public static readonly DiagnosticDescriptor GraphqlDocumentIsOptIn = new(
        id: "QYL0001",
        title: "graphql.document is opt-in",
        messageFormat: "Setting graphql.document captures user-supplied data; v1.41.0 demoted it from recommended to opt_in — verify explicit enablement and sanitization",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "graphql.document carries user-inputted, potentially sensitive, high-cardinality content. v1.41.0 moved its requirement level from recommended to opt_in. Capture only behind an explicit opt-in flag with sanitization.",
        helpLinkUri: HelpLinkBase + "qyl0001");

    public static readonly DiagnosticDescriptor PreferSemconvConstant = new(
        id: "QYL0004",
        title: "Prefer typed semantic-convention constant over string literal",
        messageFormat: "String literal \"{0}\" matches the semantic-convention constant '{1}' — use the typed constant",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "When a telemetry attribute key literal matches a known semantic-convention attribute name from OpenTelemetry.SemanticConventions.Attributes.*, prefer the typed constant for refactor-safety and discoverability.",
        helpLinkUri: HelpLinkBase + "qyl0004");

    public static readonly DiagnosticDescriptor LiteralMatchesDeprecatedSemconv = new(
        id: "QYL0005",
        title: "String literal matches a deprecated semantic-convention name",
        messageFormat: "Literal \"{0}\" matches a deprecated semantic-convention attribute: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "When a telemetry attribute key literal matches a semantic-convention attribute that is marked [Obsolete] in the consumer's referenced OpenTelemetry.SemanticConventions package, the call site needs migration regardless of whether a typed constant is being used.",
        helpLinkUri: HelpLinkBase + "qyl0005");

    public static readonly DiagnosticDescriptor DeprecatedSemconvValue = new(
        id: "QYL0007",
        title: "Deprecated semantic-convention value",
        messageFormat: "Value \"{0}\" of attribute '{1}' is deprecated: {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A constant string used as the value of a known semantic-convention telemetry attribute matches a value member that is marked [Obsolete] in the consumer's referenced *Values enum class.",
        helpLinkUri: HelpLinkBase + "qyl0007");

    public static readonly DiagnosticDescriptor IncubatingSemconvInLibrary = new(
        id: "QYL0008",
        title: "Incubating semantic-convention member used in a library",
        messageFormat: "Member '{0}' from an Incubating namespace forces every consumer onto its exact package version; copy the constant locally in libraries",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Members under any *.SemanticConventions.Incubating namespace may rename or change values across minor package releases. Library projects (non-exe, non-test) baking direct references push that volatility onto every downstream consumer.",
        helpLinkUri: HelpLinkBase + "qyl0008");

    public static readonly DiagnosticDescriptor SupplementalExactSemconvMigration = new(
        id: "QYL0009",
        title: "Obsolete semantic convention has an exact replacement",
        messageFormat: "Semantic convention '{0}' is obsolete in production telemetry emission; use '{1}' ({2})",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A hard-coded semantic-convention name or value matches the supplemental OpenTelemetry migration catalog and has a one-to-one replacement. This supplements, but does not replace, [Obsolete] metadata from OpenTelemetry.SemanticConventions.",
        helpLinkUri: HelpLinkBase + "qyl0009");

    public static readonly DiagnosticDescriptor SupplementalManualSemconvMigration = new(
        id: "QYL0010",
        title: "Semantic convention migration needs review",
        messageFormat: "Semantic convention '{0}' needs semantic-convention migration review: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A hard-coded semantic-convention name or value matches the supplemental OpenTelemetry migration catalog, but the migration is context-sensitive or has no safe automatic replacement.",
        helpLinkUri: HelpLinkBase + "qyl0010");

    public static readonly DiagnosticDescriptor SupplementalCompatibilitySemconvMigration = new(
        id: "QYL0011",
        title: "Legacy semantic convention appears in compatibility or test code",
        messageFormat: "Semantic convention '{0}' is legacy compatibility/test/migration data: {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "A hard-coded semantic-convention name or value appears in test, fixture, compatibility, translator, generated, or catalog code. Keep it only when the old schema is intentionally modeled.",
        helpLinkUri: HelpLinkBase + "qyl0011");
}
