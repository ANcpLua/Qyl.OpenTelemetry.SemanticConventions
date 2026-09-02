using MsOperationExtensions = Microsoft.CodeAnalysis.Operations.OperationExtensions;

namespace Qyl.Telemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0401: Validates unconditional required attributes against the exact GenAI or
/// MCP span definition selected by its registry discriminator and <c>ActivityKind</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class Qyl0401GenAiMissingRequiredAttributesAnalyzer : AlAnalyzer
{
    private const string DiagnosticId = "QYL0401";

    private static readonly DiagnosticDescriptor s_rule = CreateRule(
        DiagnosticId,
        DiagnosticCategories.GenAI,
        DiagnosticSeverities.Suggestion);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    /// <inheritdoc />
    protected override void InitializeCore(AnalysisContext context) =>
        context.RegisterOperationBlockAction(AnalyzeBlock);

    private static void AnalyzeBlock(OperationBlockAnalysisContext context)
    {
        var tagCalls = new List<TagSetterCall>();
        foreach (var block in context.OperationBlocks)
        {
            TagSetterDetection.CollectTagSetterCalls(block, tagCalls);
        }

        if (tagCalls.Count == 0)
        {
            return;
        }

        var presentAttributes = new HashSet<string>(tagCalls.Select(call => call.Key), StringComparer.Ordinal);
        var constantAttributes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var call in tagCalls)
        {
            if (call.Value is not null)
            {
                constantAttributes[call.Key] = call.Value;
            }
        }

        foreach (var block in context.OperationBlocks)
        {
            foreach (var operation in MsOperationExtensions.DescendantsAndSelf(block))
            {
                if (operation is not IInvocationOperation { TargetMethod.Name: "StartActivity" } invocation
                    || !SemconvRegistryFacts.TryResolveSpanRule(
                        constantAttributes,
                        GetSpanKind(invocation),
                        out var spanRule)
                    || spanRule is null)
                {
                    continue;
                }

                var activityName = invocation.TryGetConstantArgument<string>("name", out var name) ? name : spanRule.Id;
                foreach (var requiredAttribute in spanRule.RequiredAttributes)
                {
                    if (!presentAttributes.Contains(requiredAttribute))
                    {
                        context.ReportDiagnostic(
                            Diagnostic.Create(
                                s_rule,
                                invocation.Syntax.GetLocation(),
                                activityName,
                                requiredAttribute));
                    }
                }
            }
        }
    }

    private static string GetSpanKind(IInvocationOperation invocation)
    {
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter is not { Name: "kind", Type.Name: "ActivityKind" })
            {
                continue;
            }

            var value = argument.Value.UnwrapAllConversions();
            if (value is IFieldReferenceOperation fieldReference)
            {
                return fieldReference.Field.Name.ToLowerInvariant();
            }

            if (value.TryGetConstantValue<object>(out var constant))
            {
                return constant switch
                {
                    1 or 1u or 1L or 1UL => "server",
                    2 or 2u or 2L or 2UL => "client",
                    3 or 3u or 3L or 3UL => "producer",
                    4 or 4u or 4L or 4UL => "consumer",
                    _ => "internal",
                };
            }
        }

        return "internal";
    }
}
