// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace Qyl.OpenTelemetry.SemanticConventions.Analyzers;

/// <summary>
/// QYL0005: Flags <c>SetTag("client.address", …)</c> / <c>"client.port"</c> calls inside
/// a method that also contains a <c>SetTag("rpc.system", …)</c>. v1.41.0 removed
/// <c>client.*</c> from RPC server span definitions.
/// </summary>
/// <remarks>
/// Heuristic: scope analysis to the enclosing method. We treat the method as an
/// "RPC server span context" if any sibling tag-setter call uses the literal key
/// <c>"rpc.system"</c>, <c>"rpc.service"</c>, or <c>"rpc.method"</c>. False-positive
/// risk on RPC client spans (which legitimately use <c>server.*</c>, not <c>client.*</c>),
/// so we focus on the literal client.* keys upstream removed.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RpcServerClientAttributeAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> RpcContextKeys = ImmutableHashSet.Create(
        "rpc.system", "rpc.service", "rpc.method", "rpc.grpc.status_code");

    private static readonly ImmutableHashSet<string> ForbiddenClientKeys = ImmutableHashSet.Create(
        "client.address", "client.port");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [DiagnosticDescriptors.RpcServerHasClientAddressAttribute];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationBlockAction(AnalyzeBlock);
    }

    private static void AnalyzeBlock(OperationBlockAnalysisContext context)
    {
        var calls = new List<TagSetterCall>();
        foreach (var blockOperation in context.OperationBlocks)
        {
            TagSetterDetection.CollectTagSetterCalls(blockOperation, calls);
        }

        if (calls.Count == 0)
        {
            return;
        }

        if (!calls.Exists(static call => RpcContextKeys.Contains(call.Key)))
        {
            return;
        }

        foreach (var call in calls)
        {
            if (ForbiddenClientKeys.Contains(call.Key))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.RpcServerHasClientAddressAttribute,
                    call.KeyLocation,
                    call.Key));
            }
        }
    }
}
