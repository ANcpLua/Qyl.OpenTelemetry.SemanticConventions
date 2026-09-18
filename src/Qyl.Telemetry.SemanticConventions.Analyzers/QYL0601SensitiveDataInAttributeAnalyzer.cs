namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
///     QYL0601: Detects potential PII or credential data in telemetry attributes.
/// </summary>
/// <remarks>
///     <para>
///         Attributes containing sensitive data (passwords, secrets, tokens, API keys,
///         SSNs, credit card numbers) can leak sensitive information to telemetry backends
///         where it may be stored, logged, or exposed to unauthorized users.
///     </para>
///     <para>
///         A key is inspected only when it provably reaches telemetry: a tag setter, a
///         baggage entry, a metric measurement, <c>StartActivity</c> tags, a logger scope
///         or state, resource attributes, or an event or link, including a dictionary or
///         <c>TagList</c> built up in a local and handed to one of those. The detection is
///         the shared <see cref="TelemetryAttributePayloadDetection"/>, not a guess from
///         the receiver's variable name.
///     </para>
///     <para>
///         Sensitive words match on segment boundaries, so <c>auth.token</c> is flagged and
///         <c>gen_ai.usage.input_tokens</c> is not. A key the pinned registry defines
///         outright is never flagged: the registry has already decided that
///         <c>aws.secretsmanager.secret.arn</c> or <c>aspnetcore.authorization.policy</c>
///         is safe to emit. A key that only extends a registry template, such as
///         <c>http.request.header.authorization</c>, is still flagged.
///     </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0601SensitiveDataInAttributeAnalyzer : AlAnalyzer {
    /// <summary>
    ///     Words and word runs that indicate sensitive data. Separator and case variants of the
    ///     same words are covered by segment matching; the joined spellings stay listed because
    ///     an all-lowercase <c>apikey</c> is a single segment.
    /// </summary>
    private static readonly AttributeKeyPatternSet s_sensitivePatterns = new(
        // Credentials
        "password",
        "passwd",
        "pwd",
        "secret",
        "credential",
        "credentials",
        "auth",
        "authorization",
        "bearer",

        // Tokens and keys
        "token",
        "api_key",
        "apikey",
        "private_key",
        "privatekey",
        "access_key",
        "accesskey",
        "secret_key",
        "secretkey",
        "encryption_key",
        "encryptionkey",

        // PII
        "ssn",
        "social_security",
        "socialsecurity",
        "credit_card",
        "creditcard",
        "card_number",
        "cardnumber",
        "cvv",
        "pin",

        // Connection strings
        "connection_string",
        "connectionstring",
        "conn_str");

    /// <summary>The diagnostic identifier for QYL0601.</summary>
    private const string DiagnosticId = "QYL0601";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.OpenTelemetry,
        DiagnosticSeverity.Warning);

    /// <summary>Gets the diagnostic descriptors for the supported diagnostics.</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <summary>Routes every telemetry payload literal in the compilation through the sensitive-name check.</summary>
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterCompilationStartAction(static start =>
            TelemetryAttributePayloadDetection.RegisterPayloadAnalysis(start, ReportIfSensitive));

    private static void ReportIfSensitive(OperationAnalysisContext context, TelemetryAttributePayloadLiteral payload) {
        if (payload.Sink == TelemetryPayloadSink.None
            || SemconvRegistryFacts.IsRegistryAttributeKey(payload.Key)
            || !s_sensitivePatterns.Matches(payload.Key)) {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(s_rule, payload.KeySyntax.GetLocation(), payload.Key));
    }
}
