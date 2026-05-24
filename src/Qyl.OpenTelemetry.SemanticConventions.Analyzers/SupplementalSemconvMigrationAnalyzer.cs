// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

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

    private static readonly ImmutableHashSet<string> BaggageMethodNames = ImmutableHashSet.Create(
        "SetBaggage",
        "AddBaggage");

    private static readonly ImmutableHashSet<string> MetricInstrumentMethodNames = ImmutableHashSet.Create(
        "CreateCounter",
        "CreateHistogram",
        "CreateGauge",
        "CreateObservableCounter",
        "CreateObservableGauge",
        "CreateObservableUpDownCounter",
        "CreateUpDownCounter");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [
        DiagnosticDescriptors.SupplementalExactSemconvMigration,
        DiagnosticDescriptors.SupplementalManualSemconvMigration,
        DiagnosticDescriptors.SupplementalCompatibilitySemconvMigration,
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var legacyMode = SemconvIntentClassifier.GetLegacyMode(context.Options);
        if (legacyMode == SemconvLegacyMode.Off)
        {
            return;
        }

        var liveObsoleteAttributeNames = BuildLiveObsoleteAttributeNames(context.Compilation);
        var liveObsoleteAttributeValues = BuildLiveObsoleteAttributeValues(context.Compilation);

        context.RegisterOperationAction(
            ctx => AnalyzeInvocation(ctx, legacyMode, liveObsoleteAttributeNames, liveObsoleteAttributeValues),
            OperationKind.Invocation);
        context.RegisterOperationAction(
            ctx => AnalyzeObjectCreation(ctx, legacyMode, liveObsoleteAttributeNames, liveObsoleteAttributeValues),
            OperationKind.ObjectCreation);
        context.RegisterOperationAction(
            ctx => AnalyzeCollectionExpression(ctx, legacyMode, liveObsoleteAttributeNames, liveObsoleteAttributeValues),
            OperationKind.CollectionExpression);
        context.RegisterOperationAction(
            ctx => AnalyzeAssignment(ctx, legacyMode, liveObsoleteAttributeNames, liveObsoleteAttributeValues),
            OperationKind.SimpleAssignment);
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (SemconvIntentClassifier.IsCatalogSource(invocation))
        {
            return;
        }

        AnalyzeMetricInstrumentName(context, invocation, legacyMode);
        AnalyzeMetricMeasurementTags(
            context,
            invocation,
            legacyMode,
            liveObsoleteAttributeNames,
            liveObsoleteAttributeValues);
        AnalyzeActivityOrEventName(context, invocation, legacyMode);
        AnalyzeTelemetryAttributePayloadInvocation(
            context,
            invocation,
            legacyMode,
            liveObsoleteAttributeNames,
            liveObsoleteAttributeValues);
        if (!IsInsideKnownTelemetryAttributePayload(invocation))
        {
            AnalyzeKeyValueInvocation(context, invocation, legacyMode, liveObsoleteAttributeNames, liveObsoleteAttributeValues);
        }
    }

    private static void AnalyzeObjectCreation(
        OperationAnalysisContext context,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        var objectCreation = (IObjectCreationOperation)context.Operation;
        if (SemconvIntentClassifier.IsCatalogSource(objectCreation))
        {
            return;
        }

        if (IsKeyValuePairStringObject(objectCreation.Type)
            && !IsInsideKnownTelemetryAttributePayload(objectCreation))
        {
            AnalyzeKeyValuePairCreation(
                context,
                objectCreation,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission: false);
        }

        if (objectCreation.Initializer is not null
            && IsStringKeyDictionary(objectCreation.Type)
            && IsInsideLocalDeclarationInitializerUsedAsTelemetryPayload(objectCreation))
        {
            AnalyzeObjectInitializer(
                context,
                objectCreation.Initializer,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission: true);
            return;
        }

        if (IsActivityEventCreation(objectCreation.Type)
            && TryGetArgumentByOrdinal(objectCreation.Arguments, extensionMethod: false, 0, out var nameArgument)
            && TryGetBareLiteral(nameArgument.Value, out var eventName, out var eventNameSyntax))
        {
            ReportNameIfCatalogOnly(
                context,
                eventName,
                eventNameSyntax,
                legacyMode,
                liveObsoleteAttributeNames,
                isProductionEmission: true);
        }

        if (IsMetricMeasurementCreation(objectCreation.Type))
        {
            AnalyzeArgumentsAfterFirst(
                context,
                objectCreation.Arguments,
                extensionMethod: false,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission: true);
        }

        if (IsActivityEventCreation(objectCreation.Type)
            && TryGetArgumentByNameOrOrdinal(objectCreation.Arguments, "tags", 2, out var tagsArgument))
        {
            AnalyzeTelemetryAttributePayload(
                context,
                tagsArgument.Value,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission: true);
        }

        if (IsActivityLinkCreation(objectCreation.Type)
            && TryGetArgumentByNameOrOrdinal(objectCreation.Arguments, "tags", 1, out var linkTagsArgument))
        {
            AnalyzeTelemetryAttributePayload(
                context,
                linkTagsArgument.Value,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission: true);
        }
    }

    private static void AnalyzeCollectionExpression(
        OperationAnalysisContext context,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        var collectionExpression = (ICollectionExpressionOperation)context.Operation;
        if (SemconvIntentClassifier.IsCatalogSource(collectionExpression)
            || !IsInsideLocalDeclarationInitializerUsedAsTelemetryPayload(collectionExpression))
        {
            return;
        }

        AnalyzeCollectionExpressionPayload(
            context,
            collectionExpression,
            legacyMode,
            liveObsoleteAttributeNames,
            liveObsoleteAttributeValues,
            isProductionEmission: true);
    }

    private static void AnalyzeTelemetryAttributePayloadInvocation(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        if (IsActivitySourceStartActivity(invocation)
            && TryGetArgumentByNameOrOrdinal(invocation.Arguments, "tags", 3, out var startActivityTagsArgument))
        {
            AnalyzeTelemetryAttributePayload(
                context,
                startActivityTagsArgument.Value,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission: true);
            return;
        }

        if (IsLoggerBeginScope(invocation)
            && TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var scopeStateArgument))
        {
            AnalyzeTelemetryAttributePayload(
                context,
                scopeStateArgument.Value,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission: true);
            return;
        }

        if (IsLoggerLog(invocation)
            && TryGetArgumentByNameOrOrdinal(invocation.Arguments, "state", 2, out var logStateArgument))
        {
            AnalyzeTelemetryAttributePayload(
                context,
                logStateArgument.Value,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission: true);
            return;
        }

        if (!IsResourceBuilderAddAttributes(invocation)
            || !TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var attributesArgument))
        {
            return;
        }

        AnalyzeTelemetryAttributePayload(
            context,
            attributesArgument.Value,
            legacyMode,
            liveObsoleteAttributeNames,
            liveObsoleteAttributeValues,
            isProductionEmission: true);
    }

    private static void AnalyzeKeyValueInvocation(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        var isProductionEmission = IsKnownProductionTagSetter(invocation)
            || IsDictionaryAddOnLocalFlowingToTelemetry(invocation);
        var isAmbiguousPayload = isProductionEmission is false && IsAmbiguousAttributePayload(invocation);
        if (!isProductionEmission && !isAmbiguousPayload)
        {
            return;
        }

        if (!TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var keyArgument)
            || !TryGetBareLiteral(keyArgument.Value, out var key, out var keySyntax))
        {
            return;
        }

        ReportNameIfCatalogOnly(
            context,
            key,
            keySyntax,
            legacyMode,
            liveObsoleteAttributeNames,
            isProductionEmission);

        if (!TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 1, out var valueArgument)
            || !TryGetBareLiteral(valueArgument.Value, out var value, out var valueSyntax))
        {
            return;
        }

        ReportValueIfCatalogOnly(
            context,
            key,
            value,
            valueSyntax,
            legacyMode,
            liveObsoleteAttributeValues,
            isProductionEmission);
    }

    private static void AnalyzeAssignment(
        OperationAnalysisContext context,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (SemconvIntentClassifier.IsCatalogSource(assignment)
            || (!IsTelemetryTagCollectionIndexerAssignment(assignment)
                && !IsDictionaryIndexerAssignmentOnLocalFlowingToTelemetry(assignment)))
        {
            return;
        }

        AnalyzeIndexerAssignmentPayload(
            context,
            assignment,
            legacyMode,
            liveObsoleteAttributeNames,
            liveObsoleteAttributeValues,
            isProductionEmission: true);
    }

    private static void AnalyzeMetricInstrumentName(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SemconvLegacyMode legacyMode)
    {
        if (!MetricInstrumentMethodNames.Contains(invocation.TargetMethod.Name)
            || !IsMeterLike(invocation.TargetMethod.ContainingType)
            || !TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var nameArgument)
            || !TryGetBareLiteral(nameArgument.Value, out var metricName, out var metricNameSyntax)
            || !SemconvMigrationCatalog.TryGetMigrationByName(metricName, out var entry)
            || entry.Kind != SemconvMigrationItemKind.MetricName)
        {
            return;
        }

        if (!SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(entry))
        {
            return;
        }

        ReportCatalogDiagnostic(context, entry, metricNameSyntax, legacyMode, isProductionEmission: true);
    }

    private static void AnalyzeMetricMeasurementTags(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues)
    {
        if (invocation.TargetMethod.Name is not ("Add" or "Record")
            || !IsMetricInstrument(invocation.TargetMethod.ContainingType))
        {
            return;
        }

        AnalyzeArgumentsAfterFirst(
            context,
            invocation.Arguments,
            invocation.TargetMethod.IsExtensionMethod,
            legacyMode,
            liveObsoleteAttributeNames,
            liveObsoleteAttributeValues,
            isProductionEmission: true);
    }

    private static void AnalyzeActivityOrEventName(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SemconvLegacyMode legacyMode)
    {
        if (invocation.TargetMethod.Name == "StartActivity"
            && IsActivitySourceLike(invocation.TargetMethod.ContainingType)
            && TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var spanArgument)
            && TryGetBareLiteral(spanArgument.Value, out var spanName, out var spanNameSyntax)
            && SemconvMigrationCatalog.TryGetMigrationByName(spanName, out var spanEntry)
            && spanEntry.Kind == SemconvMigrationItemKind.SpanName
            && SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(spanEntry))
        {
            ReportCatalogDiagnostic(context, spanEntry, spanNameSyntax, legacyMode, isProductionEmission: true);
        }

        if (invocation.TargetMethod.Name == "AddEvent"
            && TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var eventArgument)
            && TryGetBareLiteral(eventArgument.Value, out var eventName, out var eventNameSyntax)
            && SemconvMigrationCatalog.TryGetMigrationByName(eventName, out var eventEntry)
            && eventEntry.Kind == SemconvMigrationItemKind.EventName
            && SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(eventEntry))
        {
            ReportCatalogDiagnostic(context, eventEntry, eventNameSyntax, legacyMode, isProductionEmission: true);
        }
    }

    private static void AnalyzeArgumentsAfterFirst(
        OperationAnalysisContext context,
        ImmutableArray<IArgumentOperation> arguments,
        bool extensionMethod,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        for (var i = 0; i < arguments.Length; i++)
        {
            var argument = arguments[i];
            var logicalOrdinal = argument.Parameter is null
                ? i
                : argument.Parameter.Ordinal - (extensionMethod ? 1 : 0);
            if (logicalOrdinal <= 0 && i == 0)
            {
                continue;
            }

            AnalyzeTelemetryAttributePayload(
                context,
                argument.Value,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission);
        }
    }

    private static void ReportNameIfCatalogOnly(
        OperationAnalysisContext context,
        string key,
        LiteralExpressionSyntax syntax,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        bool isProductionEmission)
    {
        if (liveObsoleteAttributeNames.Contains(key)
            || !SemconvMigrationCatalog.TryGetMigrationByName(key, out var entry))
        {
            return;
        }

        if (entry.Kind == SemconvMigrationItemKind.MetricName
            || entry.Kind == SemconvMigrationItemKind.SpanName
            || !SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(entry))
        {
            return;
        }

        ReportCatalogDiagnostic(context, entry, syntax, legacyMode, isProductionEmission);
    }

    private static void ReportValueIfCatalogOnly(
        OperationAnalysisContext context,
        string key,
        string value,
        LiteralExpressionSyntax syntax,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        var valueKey = key + "=" + value;
        if (liveObsoleteAttributeValues.Contains(valueKey)
            || !TryGetAttributeValueMigration(key, value, out var entry))
        {
            return;
        }

        ReportCatalogDiagnostic(context, entry, syntax, legacyMode, isProductionEmission);
    }

    private static void AnalyzeTelemetryAttributePayload(
        OperationAnalysisContext context,
        IOperation operation,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);

        if (unwrapped is IArrayCreationOperation arrayCreation)
        {
            if (arrayCreation.Initializer is not null)
            {
                AnalyzeArrayInitializer(
                    context,
                    arrayCreation.Initializer,
                    legacyMode,
                    liveObsoleteAttributeNames,
                    liveObsoleteAttributeValues,
                    isProductionEmission);
            }

            return;
        }

        if (unwrapped is IArrayInitializerOperation arrayInitializer)
        {
            AnalyzeArrayInitializer(
                context,
                arrayInitializer,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission);
            return;
        }

        if (unwrapped is ICollectionExpressionOperation collectionExpression)
        {
            AnalyzeCollectionExpressionPayload(
                context,
                collectionExpression,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission);
            return;
        }

        if (unwrapped is IObjectCreationOperation objectCreation)
        {
            if (IsKeyValuePairStringObject(objectCreation.Type))
            {
                AnalyzeKeyValuePairCreation(
                    context,
                    objectCreation,
                    legacyMode,
                    liveObsoleteAttributeNames,
                    liveObsoleteAttributeValues,
                    isProductionEmission);
            }

            if (objectCreation.Initializer is not null)
            {
                AnalyzeObjectInitializer(
                    context,
                    objectCreation.Initializer,
                    legacyMode,
                    liveObsoleteAttributeNames,
                    liveObsoleteAttributeValues,
                    isProductionEmission);
            }

            return;
        }

        if (unwrapped is IInvocationOperation invocation
            && invocation.TargetMethod.Name == "Add")
        {
            AnalyzeAddLikePayloadInvocation(
                context,
                invocation,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission);
            return;
        }

        if (unwrapped is ISimpleAssignmentOperation assignment)
        {
            AnalyzeIndexerAssignmentPayload(
                context,
                assignment,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission);
        }
    }

    private static void AnalyzeArrayInitializer(
        OperationAnalysisContext context,
        IArrayInitializerOperation initializer,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        foreach (var element in initializer.ElementValues)
        {
            AnalyzeTelemetryAttributePayload(
                context,
                element,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission);
        }
    }

    private static void AnalyzeObjectInitializer(
        OperationAnalysisContext context,
        IObjectOrCollectionInitializerOperation initializer,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        foreach (var initializerOperation in initializer.Initializers)
        {
            AnalyzeTelemetryAttributePayload(
                context,
                initializerOperation,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission);
        }
    }

    private static void AnalyzeCollectionExpressionPayload(
        OperationAnalysisContext context,
        ICollectionExpressionOperation collectionExpression,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        foreach (var element in collectionExpression.Elements)
        {
            if (element is ISpreadOperation spread)
            {
                AnalyzeTelemetryAttributePayload(
                    context,
                    spread.Operand,
                    legacyMode,
                    liveObsoleteAttributeNames,
                    liveObsoleteAttributeValues,
                    isProductionEmission);
                continue;
            }

            AnalyzeTelemetryAttributePayload(
                context,
                element,
                legacyMode,
                liveObsoleteAttributeNames,
                liveObsoleteAttributeValues,
                isProductionEmission);
        }
    }

    private static void AnalyzeKeyValuePairCreation(
        OperationAnalysisContext context,
        IObjectCreationOperation objectCreation,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        if (!TryGetArgumentByOrdinal(objectCreation.Arguments, extensionMethod: false, 0, out var keyArgument)
            || !TryGetArgumentByOrdinal(objectCreation.Arguments, extensionMethod: false, 1, out var valueArgument)
            || !TryGetBareLiteral(keyArgument.Value, out var key, out var keySyntax))
        {
            return;
        }

        ReportNameIfCatalogOnly(
            context,
            key,
            keySyntax,
            legacyMode,
            liveObsoleteAttributeNames,
            isProductionEmission);

        if (TryGetBareLiteral(valueArgument.Value, out var value, out var valueSyntax))
        {
            ReportValueIfCatalogOnly(
                context,
                key,
                value,
                valueSyntax,
                legacyMode,
                liveObsoleteAttributeValues,
                isProductionEmission);
        }
    }

    private static void AnalyzeAddLikePayloadInvocation(
        OperationAnalysisContext context,
        IInvocationOperation invocation,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        if (!TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 0, out var keyArgument)
            || !TryGetBareLiteral(keyArgument.Value, out var key, out var keySyntax))
        {
            return;
        }

        ReportNameIfCatalogOnly(
            context,
            key,
            keySyntax,
            legacyMode,
            liveObsoleteAttributeNames,
            isProductionEmission);

        if (TryGetArgumentByOrdinal(invocation.Arguments, invocation.TargetMethod.IsExtensionMethod, 1, out var valueArgument)
            && TryGetBareLiteral(valueArgument.Value, out var value, out var valueSyntax))
        {
            ReportValueIfCatalogOnly(
                context,
                key,
                value,
                valueSyntax,
                legacyMode,
                liveObsoleteAttributeValues,
                isProductionEmission);
        }
    }

    private static void AnalyzeIndexerAssignmentPayload(
        OperationAnalysisContext context,
        ISimpleAssignmentOperation assignment,
        SemconvLegacyMode legacyMode,
        ImmutableHashSet<string> liveObsoleteAttributeNames,
        ImmutableHashSet<string> liveObsoleteAttributeValues,
        bool isProductionEmission)
    {
        if (!TryGetIndexerKey(assignment.Target, out var key, out var keySyntax))
        {
            return;
        }

        ReportNameIfCatalogOnly(
            context,
            key,
            keySyntax,
            legacyMode,
            liveObsoleteAttributeNames,
            isProductionEmission);

        if (TryGetBareLiteral(assignment.Value, out var value, out var valueSyntax))
        {
            ReportValueIfCatalogOnly(
                context,
                key,
                value,
                valueSyntax,
                legacyMode,
                liveObsoleteAttributeValues,
                isProductionEmission);
        }
    }

    private static void ReportCatalogDiagnostic(
        OperationAnalysisContext context,
        SemconvMigrationCatalogEntry entry,
        LiteralExpressionSyntax syntax,
        SemconvLegacyMode legacyMode,
        bool isProductionEmission)
    {
        var compatibilityContext = SemconvIntentClassifier.IsCompatibilityOrTestContext(context)
            || SemconvIntentClassifier.IsGeneratedSource(context.Operation)
            || SemconvIntentClassifier.HasLegacySchemaUrlContext(context.Operation);

        var descriptor = SelectDescriptor(entry, legacyMode, compatibilityContext, isProductionEmission);
        var replacement = entry.ReplacementNames.Length == 1 ? entry.ReplacementNames[0] : "";
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
            .Add(ChangelogVersionProperty, entry.ChangelogVersion);

        var args = descriptor.Id switch
        {
            "QYL0030" => new object[] { entry.OldName, replacement, evidence },
            _ => new object[] { entry.OldName, evidence },
        };

        context.ReportDiagnostic(Diagnostic.Create(
            descriptor,
            syntax.GetLocation(),
            properties,
            args));
    }

    private static DiagnosticDescriptor SelectDescriptor(
        SemconvMigrationCatalogEntry entry,
        SemconvLegacyMode legacyMode,
        bool compatibilityContext,
        bool isProductionEmission)
    {
        if (compatibilityContext)
        {
            return DiagnosticDescriptors.SupplementalCompatibilitySemconvMigration;
        }

        if (legacyMode == SemconvLegacyMode.Compatibility)
        {
            return DiagnosticDescriptors.SupplementalManualSemconvMigration;
        }

        return entry.HasExactReplacement && isProductionEmission
            ? DiagnosticDescriptors.SupplementalExactSemconvMigration
            : DiagnosticDescriptors.SupplementalManualSemconvMigration;
    }

    private static bool IsKnownProductionTagSetter(IInvocationOperation invocation)
    {
        if (TagSetterDetection.IsTagSetterInvocation(invocation)
            || BaggageMethodNames.Contains(invocation.TargetMethod.Name))
        {
            return true;
        }

        return invocation.TargetMethod.Name == "Add"
            && IsTelemetryTagCollection(invocation.Instance?.Type ?? invocation.TargetMethod.ContainingType);
    }

    private static bool IsAmbiguousAttributePayload(IInvocationOperation invocation)
    {
        if (invocation.TargetMethod.Name != "Add")
        {
            return false;
        }

        return IsStringKeyDictionary(invocation.Instance?.Type ?? invocation.TargetMethod.ContainingType);
    }

    private static bool IsTelemetryTagCollection(ITypeSymbol? type) =>
        type?.Name is "TagList" or "ActivityTagsCollection";

    private static bool IsStringKeyDictionary(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol named || named.TypeArguments.Length < 2)
        {
            return false;
        }

        return named.Name is "Dictionary" or "IDictionary" or "IReadOnlyDictionary"
            && named.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static bool IsKeyValuePairStringObject(ITypeSymbol? type)
    {
        if (type is not INamedTypeSymbol { Name: "KeyValuePair", TypeArguments.Length: 2 } named)
        {
            return false;
        }

        return named.TypeArguments[0].SpecialType == SpecialType.System_String;
    }

    private static bool IsMeterLike(INamedTypeSymbol? type) =>
        type?.Name is "Meter";

    private static bool IsMetricInstrument(INamedTypeSymbol? type) =>
        type?.Name is "Counter" or "Histogram" or "UpDownCounter";

    private static bool IsActivitySourceLike(INamedTypeSymbol? type) =>
        type?.Name is "ActivitySource";

    private static bool IsActivityEventCreation(ITypeSymbol? type) =>
        type?.Name is "ActivityEvent";

    private static bool IsActivityLinkCreation(ITypeSymbol? type) =>
        type?.Name is "ActivityLink";

    private static bool IsActivitySourceStartActivity(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "StartActivity"
        && IsActivitySourceLike(invocation.TargetMethod.ContainingType);

    private static bool IsMetricMeasurementCreation(ITypeSymbol? type) =>
        type?.Name is "Measurement";

    private static bool IsResourceBuilderAddAttributes(IInvocationOperation invocation)
    {
        if (invocation.TargetMethod.Name != "AddAttributes")
        {
            return false;
        }

        if (invocation.TargetMethod.ContainingType.Name == "ResourceBuilder")
        {
            return true;
        }

        return invocation.TargetMethod.IsExtensionMethod
            && invocation.TargetMethod.Parameters.Length > 0
            && invocation.TargetMethod.Parameters[0].Type.Name == "ResourceBuilder";
    }

    private static bool IsInsideKnownTelemetryAttributePayload(IOperation operation)
    {
        if (IsInsideLocalDeclarationInitializerUsedAsTelemetryPayload(operation))
        {
            return true;
        }

        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is not IArgumentOperation argument)
            {
                continue;
            }

            if (argument.Parent is IInvocationOperation invocation
                && IsResourceBuilderAddAttributes(invocation)
                && IsLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod, 0))
            {
                return true;
            }

            if (argument.Parent is IInvocationOperation metricInvocation
                && metricInvocation.TargetMethod.Name is "Add" or "Record"
                && IsMetricInstrument(metricInvocation.TargetMethod.ContainingType)
                && IsAfterFirstLogicalArgument(argument, metricInvocation.TargetMethod.IsExtensionMethod))
            {
                return true;
            }

            if (argument.Parent is IObjectCreationOperation metricMeasurement
                && IsMetricMeasurementCreation(metricMeasurement.Type)
                && IsAfterFirstLogicalArgument(argument, extensionMethod: false))
            {
                return true;
            }

            if (argument.Parent is IInvocationOperation startActivityInvocation
                && IsActivitySourceTagsArgument(startActivityInvocation, argument))
            {
                return true;
            }

            if (argument.Parent is IInvocationOperation loggerInvocation
                && IsLoggerPayloadArgument(loggerInvocation, argument))
            {
                return true;
            }

            if (argument.Parent is IObjectCreationOperation objectCreation
                && (IsActivityEventTagsArgument(objectCreation, argument)
                    || IsActivityLinkTagsArgument(objectCreation, argument)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideLocalDeclarationInitializerUsedAsTelemetryPayload(IOperation operation)
    {
        if (!TryGetEnclosingLocalInitializer(operation, out var local))
        {
            return false;
        }

        return LocalFlowsToKnownTelemetryAttributePayload(local, operation);
    }

    private static bool TryGetEnclosingLocalInitializer(
        IOperation operation,
        [NotNullWhen(true)] out ILocalSymbol? local)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IVariableInitializerOperation
                && current.Parent is IVariableDeclaratorOperation { Symbol: ILocalSymbol localSymbol })
            {
                local = localSymbol;
                return true;
            }
        }

        local = null;
        return false;
    }

    private static bool LocalFlowsToKnownTelemetryAttributePayload(
        ILocalSymbol local,
        IOperation operation)
    {
        var root = operation;
        while (root.Parent is not null)
        {
            root = root.Parent;
        }

        foreach (var descendant in Microsoft.CodeAnalysis.Operations.OperationExtensions.DescendantsAndSelf(root))
        {
            if (descendant is not IArgumentOperation argument
                || !IsLocalReference(argument.Value, local))
            {
                continue;
            }

            if (argument.Parent is IInvocationOperation invocation
                && ((IsResourceBuilderAddAttributes(invocation)
                        && IsLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod, 0))
                    || (invocation.TargetMethod.Name is "Add" or "Record"
                        && IsMetricInstrument(invocation.TargetMethod.ContainingType)
                        && IsAfterFirstLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod))
                    || IsActivitySourceTagsArgument(invocation, argument)
                    || IsLoggerPayloadArgument(invocation, argument)))
            {
                return true;
            }

            if (argument.Parent is IObjectCreationOperation objectCreation
                && (IsActivityEventTagsArgument(objectCreation, argument)
                    || IsActivityLinkTagsArgument(objectCreation, argument)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDictionaryAddOnLocalFlowingToTelemetry(IInvocationOperation invocation)
    {
        return invocation.TargetMethod.Name == "Add"
            && IsStringKeyDictionary(invocation.Instance?.Type ?? invocation.TargetMethod.ContainingType)
            && TryGetLocalReference(invocation.Instance, out var local)
            && LocalFlowsToKnownTelemetryAttributePayload(local, invocation);
    }

    private static bool IsDictionaryIndexerAssignmentOnLocalFlowingToTelemetry(ISimpleAssignmentOperation assignment)
    {
        var target = TagSetterDetection.UnwrapConversion(assignment.Target);
        return target is IPropertyReferenceOperation propertyReference
            && IsStringKeyDictionary(propertyReference.Instance?.Type ?? propertyReference.Property.ContainingType)
            && TryGetLocalReference(propertyReference.Instance, out var local)
            && LocalFlowsToKnownTelemetryAttributePayload(local, assignment);
    }

    private static bool IsTelemetryTagCollectionIndexerAssignment(ISimpleAssignmentOperation assignment)
    {
        var target = TagSetterDetection.UnwrapConversion(assignment.Target);
        return target is IPropertyReferenceOperation propertyReference
            && IsTelemetryTagCollection(propertyReference.Instance?.Type ?? propertyReference.Property.ContainingType);
    }

    private static bool TryGetLocalReference(
        IOperation? operation,
        [NotNullWhen(true)] out ILocalSymbol? local)
    {
        if (operation is not null
            && TagSetterDetection.UnwrapConversion(operation) is ILocalReferenceOperation localReference)
        {
            local = localReference.Local;
            return true;
        }

        local = null;
        return false;
    }

    private static bool IsLocalReference(IOperation operation, ILocalSymbol local) =>
        TagSetterDetection.UnwrapConversion(operation) is ILocalReferenceOperation localReference
        && SymbolEqualityComparer.Default.Equals(localReference.Local, local);

    private static bool IsActivityEventTagsArgument(
        IObjectCreationOperation objectCreation,
        IArgumentOperation argument) =>
        IsActivityEventCreation(objectCreation.Type)
        && (string.Equals(argument.Parameter?.Name, "tags", StringComparison.Ordinal)
            || argument.Parameter?.Ordinal == 2);

    private static bool IsActivityLinkTagsArgument(
        IObjectCreationOperation objectCreation,
        IArgumentOperation argument) =>
        IsActivityLinkCreation(objectCreation.Type)
        && (string.Equals(argument.Parameter?.Name, "tags", StringComparison.Ordinal)
            || argument.Parameter?.Ordinal == 1);

    private static bool IsActivitySourceTagsArgument(
        IInvocationOperation invocation,
        IArgumentOperation argument) =>
        IsActivitySourceStartActivity(invocation)
        && (string.Equals(argument.Parameter?.Name, "tags", StringComparison.Ordinal)
            || argument.Parameter?.Ordinal == 3);

    private static bool IsLoggerPayloadArgument(
        IInvocationOperation invocation,
        IArgumentOperation argument)
    {
        if (IsLoggerBeginScope(invocation))
        {
            return IsLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod, 0);
        }

        return IsLoggerLog(invocation)
            && (string.Equals(argument.Parameter?.Name, "state", StringComparison.Ordinal)
                || IsLogicalArgument(argument, invocation.TargetMethod.IsExtensionMethod, 2));
    }

    private static bool IsLoggerBeginScope(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "BeginScope"
        && IsLoggerLike(invocation.TargetMethod.ContainingType);

    private static bool IsLoggerLog(IInvocationOperation invocation) =>
        invocation.TargetMethod.Name == "Log"
        && IsLoggerLike(invocation.TargetMethod.ContainingType);

    private static bool IsLoggerLike(ITypeSymbol? type) =>
        type?.Name is "ILogger";

    private static bool TryGetAttributeValueMigration(
        string key,
        string value,
        out SemconvMigrationCatalogEntry entry)
    {
        return SemconvMigrationCatalog.TryGetAttributeValueMigration(key, value, out entry)
            && SemconvMigrationCatalog.IsSupplementalDiagnosticEntry(entry);
    }

    private static bool TryGetIndexerKey(
        IOperation operation,
        [NotNullWhen(true)] out string? key,
        [NotNullWhen(true)] out LiteralExpressionSyntax? syntax)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);
        if (unwrapped is IPropertyReferenceOperation propertyReference)
        {
            foreach (var argument in propertyReference.Arguments)
            {
                if (IsLogicalArgument(argument, extensionMethod: false, 0)
                    && TryGetBareLiteral(argument.Value, out key, out syntax))
                {
                    return true;
                }
            }
        }

        key = null;
        syntax = null;
        return false;
    }

    private static bool TryGetBareLiteral(
        IOperation operation,
        [NotNullWhen(true)] out string? value,
        [NotNullWhen(true)] out LiteralExpressionSyntax? syntax)
    {
        var unwrapped = TagSetterDetection.UnwrapConversion(operation);
        if (unwrapped.Syntax is LiteralExpressionSyntax literal
            && TagSetterDetection.TryGetNonEmptyStringConstant(unwrapped, out value))
        {
            syntax = literal;
            return true;
        }

        value = null;
        syntax = null;
        return false;
    }

    private static bool TryGetArgumentByOrdinal(
        ImmutableArray<IArgumentOperation> arguments,
        bool extensionMethod,
        int logicalParameterOrdinal,
        [NotNullWhen(true)] out IArgumentOperation? argument)
    {
        var parameterOrdinal = extensionMethod ? logicalParameterOrdinal + 1 : logicalParameterOrdinal;
        foreach (var candidate in arguments)
        {
            if (candidate.Parameter?.Ordinal == parameterOrdinal)
            {
                argument = candidate;
                return true;
            }
        }

        var fallbackIndex = arguments.Length > parameterOrdinal ? parameterOrdinal : logicalParameterOrdinal;
        if (arguments.Length > fallbackIndex)
        {
            argument = arguments[fallbackIndex];
            return true;
        }

        argument = null;
        return false;
    }

    private static bool TryGetArgumentByNameOrOrdinal(
        ImmutableArray<IArgumentOperation> arguments,
        string parameterName,
        int parameterOrdinal,
        [NotNullWhen(true)] out IArgumentOperation? argument)
    {
        foreach (var candidate in arguments)
        {
            if (string.Equals(candidate.Parameter?.Name, parameterName, StringComparison.Ordinal))
            {
                argument = candidate;
                return true;
            }
        }

        return TryGetArgumentByOrdinal(arguments, extensionMethod: false, parameterOrdinal, out argument);
    }

    private static bool IsLogicalArgument(
        IArgumentOperation argument,
        bool extensionMethod,
        int logicalParameterOrdinal)
    {
        var parameterOrdinal = extensionMethod ? logicalParameterOrdinal + 1 : logicalParameterOrdinal;
        return argument.Parameter?.Ordinal == parameterOrdinal;
    }

    private static bool IsAfterFirstLogicalArgument(
        IArgumentOperation argument,
        bool extensionMethod)
    {
        if (argument.Parameter is null)
        {
            return false;
        }

        return argument.Parameter.Ordinal - (extensionMethod ? 1 : 0) > 0;
    }

    private static ImmutableHashSet<string> BuildLiveObsoleteAttributeNames(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        WalkAttributeTypes(compilation.GlobalNamespace, type =>
        {
            foreach (var member in type.GetMembers())
            {
                if (member is not IFieldSymbol
                    {
                        IsConst: true,
                        Type.SpecialType: SpecialType.System_String,
                        ConstantValue: string value,
                    } field
                    || string.IsNullOrEmpty(value)
                    || !HasObsoleteAttribute(field))
                {
                    continue;
                }

                builder.Add(value);
            }
        });

        return builder.ToImmutable();
    }

    private static ImmutableHashSet<string> BuildLiveObsoleteAttributeValues(Compilation compilation)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        WalkAttributeTypes(compilation.GlobalNamespace, type =>
        {
            Dictionary<string, string>? attrConstNameToValue = null;
            foreach (var member in type.GetMembers())
            {
                if (member is IFieldSymbol
                    {
                        IsConst: true,
                        Type.SpecialType: SpecialType.System_String,
                        ConstantValue: string attrName,
                    } field
                    && !string.IsNullOrEmpty(attrName))
                {
                    attrConstNameToValue ??= new Dictionary<string, string>(StringComparer.Ordinal);
                    attrConstNameToValue[field.Name] = attrName;
                }
            }

            if (attrConstNameToValue is null)
            {
                return;
            }

            foreach (var nested in type.GetTypeMembers())
            {
                if (!nested.Name.EndsWith("Values", StringComparison.Ordinal))
                {
                    continue;
                }

                var prefix = nested.Name.Substring(0, nested.Name.Length - "Values".Length);
                if (!attrConstNameToValue.TryGetValue("Attribute" + prefix, out var attrName))
                {
                    continue;
                }

                foreach (var nestedMember in nested.GetMembers())
                {
                    if (nestedMember is IFieldSymbol
                        {
                            IsConst: true,
                            Type.SpecialType: SpecialType.System_String,
                            ConstantValue: string value,
                        } valueField
                        && HasObsoleteAttribute(valueField))
                    {
                        builder.Add(attrName + "=" + value);
                    }
                }
            }
        });

        return builder.ToImmutable();
    }

    private static void WalkAttributeTypes(INamespaceSymbol ns, Action<INamedTypeSymbol> callback)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (SemconvNamespace.IsAttributesType(type))
            {
                callback(type);
            }
        }

        foreach (var nested in ns.GetNamespaceMembers())
        {
            WalkAttributeTypes(nested, callback);
        }
    }

    private static bool HasObsoleteAttribute(ISymbol symbol) =>
        symbol.GetAttributes().Any(static attribute =>
            attribute.AttributeClass is { Name: "ObsoleteAttribute", ContainingNamespace.Name: "System" });
}
