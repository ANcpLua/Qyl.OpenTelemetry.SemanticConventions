// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// Reports semantic-convention migrations sourced from the supplemental
/// changelog catalog when live <c>[Obsolete]</c> metadata does not own the finding.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SupplementalSemconvMigrationAnalyzer : DiagnosticAnalyzer
{
    internal const string OldNameProperty = "OldName";
    internal const string ReplacementNameProperty = "ReplacementName";
    internal const string MigrationKindProperty = "MigrationKind";
    internal const string ItemKindProperty = "Kind";
    internal const string SignalProperty = "Signal";
    internal const string DomainProperty = "Domain";
    internal const string ChangelogVersionProperty = "ChangelogVersion";

    private static readonly ImmutableHashSet<string> MetricInstrumentMethodNames = ImmutableHashSet.Create(
        "CreateCounter",
        "CreateHistogram",
        "CreateGauge",
        "CreateObservableCounter",
        "CreateObservableGauge",
        "CreateObservableUpDownCounter",
        "CreateUpDownCounter");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        DiagnosticDescriptors.SupplementalExactSemconvMigration,
        DiagnosticDescriptors.SupplementalManualSemconvMigration,
    ];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var liveObsoleteAttributeNames = BuildLiveObsoleteAttributeNames(context.Compilation);
        var liveObsoleteAttributeValues = BuildLiveObsoleteAttributeValues(context.Compilation);

        context.RegisterOperationAction(
            ctx => AnalyzeInvocation(ctx, liveObsoleteAttributeNames, liveObsoleteAttributeValues),
            OperationKind.Invocation);
        context.RegisterOperationAction(
            ctx => AnalyzeObjectCreation(ctx, liveObsoleteAttributeNames, liveObsoleteAttributeValues),
            OperationKind.ObjectCreation);
        context.RegisterOperationAction(
            ctx => AnalyzeCollectionExpression(ctx, liveObsoleteAttributeNames, liveObsoleteAttributeValues),
            OperationKind.CollectionExpression);
        context.RegisterOperationAction(
            ctx => AnalyzeAssignment(ctx, liveObsoleteAttributeNames, liveObsoleteAttributeValues),
            OperationKind.SimpleAssignment);
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (IsCatalogSource(invocation))
        {
            return;
        }

        AnalyzeMetricInstrumentName(context, invocation);
        AnalyzeActivityOrEventName(context, invocation);
        TelemetryAttributePayloadDetection.AnalyzeInvocation(
            invocation,
            payload => ReportPayloadIfCatalogOnly(
                context,
                payload,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues));
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        var objectCreation = (IObjectCreationOperation)context.Operation;
        if (IsCatalogSource(objectCreation))
        {
            return;
        }

        if (objectCreation.Type?.Name == "ActivityEvent"
            && TelemetryAttributePayloadDetection.TryGetArgumentByOrdinal(
                objectCreation.Arguments,
                extensionMethod: false,
                0,
                out var nameArgument)
            && TelemetryAttributePayloadDetection.TryGetBareStringLiteral(
                nameArgument.Value,
                out var eventName,
                out var eventNameSyntax))
        {
            ReportNameIfCatalogOnly(
                context,
                eventName,
                eventNameSyntax,
                liveObsoleteAttributeNames,
                isProductionEmission: true);
        }

        TelemetryAttributePayloadDetection.AnalyzeObjectCreation(
            objectCreation,
            payload => ReportPayloadIfCatalogOnly(
                context,
                payload,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues));
    }

    private static void AnalyzeCollectionExpression(
        OperationAnalysisContext context,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        if (IsCatalogSource(context.Operation))
        {
            return;
        }

        TelemetryAttributePayloadDetection.AnalyzeCollectionExpression(
            (ICollectionExpressionOperation)context.Operation,
            payload => ReportPayloadIfCatalogOnly(
                context,
                payload,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues));
    }

    private static void AnalyzeAssignment(
        OperationAnalysisContext context,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        if (IsCatalogSource(context.Operation))
        {
            return;
        }

        TelemetryAttributePayloadDetection.AnalyzeAssignment(
            (ISimpleAssignmentOperation)context.Operation,
            payload => ReportPayloadIfCatalogOnly(
                context,
                payload,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues));
    }

    private static void AnalyzeMetricInstrumentName(
        OperationAnalysisContext context,
        IInvocationOperation invocation)
    {
        if (!MetricInstrumentMethodNames.Contains(invocation.TargetMethod.Name)
            || invocation.TargetMethod.ContainingType.Name != "Meter"
            || !TelemetryAttributePayloadDetection.TryGetArgumentByOrdinal(
                invocation.Arguments,
                invocation.TargetMethod.IsExtensionMethod,
                0,
                out var nameArgument)
            || !TelemetryAttributePayloadDetection.TryGetBareStringLiteral(
                nameArgument.Value,
                out var metricName,
                out var metricNameSyntax)
            || !SemconvMigrationCatalog.TryGetMigrationByName(metricName, out var entry)
            || entry.Kind != SemconvMigrationItemKind.MetricName
            || !SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(entry))
        {
            return;
        }

        ReportCatalogDiagnostic(context, entry, metricNameSyntax, isProductionEmission: true);
    }

    private static void AnalyzeActivityOrEventName(
        OperationAnalysisContext context,
        IInvocationOperation invocation)
    {
        if (invocation.TargetMethod.Name == "StartActivity"
            && invocation.TargetMethod.ContainingType.Name == "ActivitySource"
            && TelemetryAttributePayloadDetection.TryGetArgumentByOrdinal(
                invocation.Arguments,
                invocation.TargetMethod.IsExtensionMethod,
                0,
                out var spanArgument)
            && TelemetryAttributePayloadDetection.TryGetBareStringLiteral(
                spanArgument.Value,
                out var spanName,
                out var spanNameSyntax)
            && SemconvMigrationCatalog.TryGetMigrationByName(spanName, out var spanEntry)
            && spanEntry.Kind == SemconvMigrationItemKind.SpanName
            && SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(spanEntry))
        {
            ReportCatalogDiagnostic(context, spanEntry, spanNameSyntax, isProductionEmission: true);
        }

        if (invocation.TargetMethod.Name == "AddEvent"
            && TelemetryAttributePayloadDetection.TryGetArgumentByOrdinal(
                invocation.Arguments,
                invocation.TargetMethod.IsExtensionMethod,
                0,
                out var eventArgument)
            && TelemetryAttributePayloadDetection.TryGetBareStringLiteral(
                eventArgument.Value,
                out var eventName,
                out var eventNameSyntax)
            && SemconvMigrationCatalog.TryGetMigrationByName(eventName, out var eventEntry)
            && eventEntry.Kind == SemconvMigrationItemKind.EventName
            && SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(eventEntry))
        {
            ReportCatalogDiagnostic(context, eventEntry, eventNameSyntax, isProductionEmission: true);
        }
    }

    private static void ReportPayloadIfCatalogOnly(
        OperationAnalysisContext context,
        TelemetryAttributePayloadLiteral payload,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        if (!payload.KeyIsBareLiteral)
        {
            return;
        }

        ReportNameIfCatalogOnly(
            context,
            payload.Key,
            payload.KeySyntax,
            liveObsoleteAttributeNames,
            payload.IsProductionEmission);

        if (payload.ValueIsBareLiteral
            && payload.Value is not null
            && payload.ValueSyntax is not null)
        {
            ReportValueIfCatalogOnly(
                context,
                payload.Key,
                payload.Value,
                payload.ValueSyntax,
                liveObsoleteAttributeValues,
                payload.IsProductionEmission);
        }
    }

    private static void ReportNameIfCatalogOnly(
        OperationAnalysisContext context,
        string name,
        SyntaxNode syntax,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        bool isProductionEmission)
    {
        if (liveObsoleteAttributeNames.Contains(name)
            || !SemconvMigrationCatalog.TryGetMigrationByName(name, out var entry)
            || entry.Kind is SemconvMigrationItemKind.MetricName or SemconvMigrationItemKind.SpanName
            || !SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(entry))
        {
            return;
        }

        ReportCatalogDiagnostic(context, entry, syntax, isProductionEmission);
    }

    private static void ReportValueIfCatalogOnly(
        OperationAnalysisContext context,
        string key,
        string value,
        SyntaxNode syntax,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        if (liveObsoleteAttributeValues.Contains(key + "=" + value)
            || !SemconvMigrationCatalog.TryGetAttributeValueMigration(key, value, out var entry)
            || !SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(entry))
        {
            return;
        }

        ReportCatalogDiagnostic(context, entry, syntax, isProductionEmission);
    }

    private static void ReportCatalogDiagnostic(
        OperationAnalysisContext context,
        SemconvMigrationCatalogEntry entry,
        SyntaxNode syntax,
        bool isProductionEmission)
    {
        var descriptor = entry.HasExactReplacement && isProductionEmission
            ? DiagnosticDescriptors.SupplementalExactSemconvMigration
            : DiagnosticDescriptors.SupplementalManualSemconvMigration;
        var replacement = GetTerminalReplacement(entry);
        var evidence = string.IsNullOrEmpty(entry.ChangelogEvidence)
            ? entry.MigrationKind.ToString()
            : entry.ChangelogEvidence;

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add(OldNameProperty, entry.OldName)
            .Add(ReplacementNameProperty, replacement)
            .Add(MigrationKindProperty, entry.MigrationKind.ToString())
            .Add(ItemKindProperty, entry.Kind.ToString())
            .Add(SignalProperty, entry.Signal)
            .Add(DomainProperty, entry.Domain)
            .Add(ChangelogVersionProperty, entry.Version);

        var args = descriptor.Id == "QYL0009"
            ? new object[] { entry.OldName, replacement, evidence }
            : new object[] { entry.OldName, evidence };

        context.ReportDiagnostic(Diagnostic.Create(
            descriptor,
            syntax.GetLocation(),
            properties,
            args));
    }

    private static string GetTerminalReplacement(SemconvMigrationCatalogEntry entry)
    {
        if (!entry.HasExactReplacement)
        {
            return string.Empty;
        }

        return entry.Kind == SemconvMigrationItemKind.AttributeValue
            ? entry.ReplacementNames[0]
            : SemconvMigrationCatalog.ResolveTerminalReplacement(entry.OldName);
    }

    private static ImmutableHashSet<string> BuildLiveObsoleteAttributeNames(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var type in SemconvNamespace.EnumerateAttributesTypes(compilation))
        {
            foreach (var member in type.GetMembers())
            {
                if (member is IFieldSymbol
                    {
                        IsConst: true,
                        Type.SpecialType: SpecialType.System_String,
                        ConstantValue: string value,
                    } field
                    && !string.IsNullOrEmpty(value)
                    && field.HasAttribute("System.ObsoleteAttribute"))
                {
                    builder.Add(value);
                }
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableHashSet<string> BuildLiveObsoleteAttributeValues(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var type in SemconvNamespace.EnumerateAttributesTypes(compilation))
        {
            foreach (var (attributeName, valueField) in SemconvNamespace.EnumerateAttributeValueConstants(type))
            {
                if (valueField.HasAttribute("System.ObsoleteAttribute"))
                {
                    builder.Add(attributeName + "=" + (string)valueField.ConstantValue!);
                }
            }
        }

        return builder.ToImmutable();
    }

    private static bool IsCatalogSource(IOperation operation)
    {
        var path = operation.Syntax.SyntaxTree.FilePath.Replace('\\', '/');
        return path.EndsWith("/OpenTelemetryDeprecatedSemconvCatalog.cs", StringComparison.Ordinal)
            || path.EndsWith("/SemconvMigrationCatalogEntry.cs", StringComparison.Ordinal);
    }
}
