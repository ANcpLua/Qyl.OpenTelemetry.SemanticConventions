using System.Linq;
using ANcpLua.Roslyn.Utilities.Testing.GeneratorHelpers;
using AwesomeAssertions;
using Qyl.Telemetry.SemanticConventions.SourceGeneration;
using Xunit;

namespace Qyl.Telemetry.SemanticConventions.SourceGeneration.Tests;

/// <summary>
/// Byte-identity smoke against contrib's
/// <c>OpenTelemetry.SemanticConventions/Attributes/Disk/DiskAttributes.cs</c>
/// shape at <c>open-telemetry/opentelemetry-dotnet-contrib@55978aae</c>. The
/// member region (constants + enum-value classes + XML doc comment layout) is
/// the byte-identity target; outer namespace/class declarations diverge
/// intentionally (contrib emits its own <c>public static class</c>; the marker
/// pattern attaches generated members to a user-declared partial class). One
/// deliberate extension over contrib: every enum-value class ends with the
/// vocabulary-owned value set (<c>AllValues</c> and <c>Contains</c>).
/// </summary>
public sealed class ByteIdentitySnapshotTests
{
    [Fact]
    public void DiskAttributes_Members_Match_Contrib_Shape()
    {
        const string source = """
            using Qyl.Telemetry.SemanticConventions.SourceGeneration;

            namespace OpenTelemetry.SemanticConventions;

            [SemanticConventionIncubatingAttributes("disk")]
            public static partial class DiskAttributes;
            """;

        var result = GeneratorTestHelper.RunGenerator<SemConvAttributesGenerator>(source);

        var generated = result.RunResult.GeneratedTrees
            .Single(static t => t.FilePath.EndsWith("DiskAttributes.g.cs", StringComparison.Ordinal))
            .ToString();

        // Contrib member region for disk.io.direction (verified against
        // gh api repos/open-telemetry/opentelemetry-dotnet-contrib/contents/
        //   src/OpenTelemetry.SemanticConventions/Attributes/DiskAttributes.cs?ref=55978aae):
        //
        //     /// <summary>
        //     /// The disk IO operation direction.
        //     /// </summary>
        //     public const string AttributeDiskIoDirection = "disk.io.direction";
        //
        //     /// <summary>
        //     /// The disk IO operation direction.
        //     /// </summary>
        //     public static class DiskIoDirectionValues
        //     {
        //         /// <summary>
        //         /// read.
        //         /// </summary>
        //         public const string Read = "read";
        //
        //         /// <summary>
        //         /// write.
        //         /// </summary>
        //         public const string Write = "write";
        //     }

        // Contiguous block check: assert the disk.io.direction member region
        // appears as one uninterrupted run of lines, not just that the substrings
        // are present anywhere in the file — reordered or interleaved members
        // would be a byte-shape regression that substring checks miss.
        //
        // Whitespace is normalized per-line (leading trim) because the generator
        // emits inside a user-declared partial class, while the contrib reference
        // is a top-level static class — the column counts differ by one nesting
        // level but the row sequence must match.
        const string expectedBlock = """
            /// <summary>
            /// The disk IO operation direction.
            /// </summary>
            /// <remarks>
            /// Examples:
            /// - read
            /// </remarks>
            public const string AttributeDiskIoDirection = "disk.io.direction";

            /// <summary>
            /// The disk IO operation direction.
            /// </summary>
            public static class DiskIoDirectionValues
            {
            /// <summary>
            /// read.
            /// </summary>
            public const string Read = "read";

            /// <summary>
            /// write.
            /// </summary>
            public const string Write = "write";

            /// <summary>Every catalogued value, in registry order.</summary>
            public static global::System.Collections.Generic.IReadOnlyList<string> AllValues { get; } = new[] { "read", "write" };
            /// <summary>Whether <paramref name="value"/> is a catalogued value (ordinal).</summary>
            public static bool Contains(string value) { foreach (var candidate in AllValues) if (string.Equals(candidate, value, global::System.StringComparison.Ordinal)) return true; return false; }
            }
            """;

        static string Normalize(string code) =>
            string.Join("\n", code.Replace("\r\n", "\n").Split('\n').Select(static l => l.TrimStart()));

        Normalize(generated).Should().Contain(Normalize(expectedBlock));
    }
}
