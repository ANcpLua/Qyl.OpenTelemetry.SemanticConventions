#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 || $# -gt 3 ]]; then
  echo "usage: $0 <version> <package-source> [package-directory]" >&2
  exit 2
fi

version="$1"
package_source="$2"
package_directory="${3:-}"
stable_id="Qyl.Telemetry.SemanticConventions"
incubating_id="${stable_id}.Incubating"
source_generation_id="${stable_id}.SourceGeneration"
package_ids=("${stable_id}" "${incubating_id}" "${source_generation_id}")

require_archive_entry() {
  local package="$1"
  local entry="$2"
  if ! unzip -Z1 "${package}" | grep -Fx "${entry}" >/dev/null; then
    echo "error: ${package} is missing ${entry}" >&2
    exit 1
  fi
}

if [[ -n "${package_directory}" ]]; then
  if [[ ! -d "${package_directory}" ]]; then
    echo "error: package directory does not exist: ${package_directory}" >&2
    exit 1
  fi

  archives=()
  while IFS= read -r archive; do
    archives+=("${archive}")
  done < <(find "${package_directory}" -maxdepth 1 -type f -name '*.nupkg' | sort)
  if [[ ${#archives[@]} -ne ${#package_ids[@]} ]]; then
    echo "error: expected ${#package_ids[@]} supported nupkg files, found ${#archives[@]}" >&2
    printf '  %s\n' "${archives[@]}" >&2
    exit 1
  fi

  stable_package="${package_directory}/${stable_id}.${version}.nupkg"
  incubating_package="${package_directory}/${incubating_id}.${version}.nupkg"
  source_generation_package="${package_directory}/${source_generation_id}.${version}.nupkg"

  for package in "${stable_package}" "${incubating_package}" "${source_generation_package}"; do
    if [[ ! -f "${package}" ]]; then
      echo "error: expected package was not produced: ${package}" >&2
      exit 1
    fi
  done

  require_archive_entry "${stable_package}" "lib/net10.0/${stable_id}.dll"
  require_archive_entry "${stable_package}" "lib/netstandard2.0/${stable_id}.dll"
  require_archive_entry "${incubating_package}" "lib/net10.0/${incubating_id}.dll"
  require_archive_entry "${incubating_package}" "lib/netstandard2.0/${incubating_id}.dll"
  require_archive_entry "${incubating_package}" "registry/resolved-registry.json"
  for schema in \
    gen-ai-input-messages.json \
    gen-ai-memory-records.json \
    gen-ai-output-messages.json \
    gen-ai-retrieval-documents.json \
    gen-ai-system-instructions.json \
    gen-ai-tool-call-arguments.json \
    gen-ai-tool-call-result.json \
    gen-ai-tool-definitions.json; do
    require_archive_entry "${incubating_package}" "registry/schemas/gen-ai/${schema}"
  done
  require_archive_entry \
    "${source_generation_package}" \
    "analyzers/dotnet/cs/${source_generation_id}.Generator.dll"
  require_archive_entry \
    "${source_generation_package}" \
    "build/${source_generation_id}.props"
fi

work_directory="$(mktemp -d)"
trap 'rm -rf "${work_directory}"' EXIT INT TERM HUP
consumer_directory="${work_directory}/consumer"
mkdir -p "${consumer_directory}"

cat > "${work_directory}/NuGet.Config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="qyl-release" value="${package_source}" />
EOF

if [[ "${package_source}" != "https://api.nuget.org/v3/index.json" ]]; then
  cat >> "${work_directory}/NuGet.Config" <<'EOF'
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
EOF
fi

cat >> "${work_directory}/NuGet.Config" <<'EOF'
  </packageSources>
</configuration>
EOF

cat > "${consumer_directory}/ReleaseSmoke.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="${stable_id}" Version="${version}" />
    <PackageReference Include="${incubating_id}" Version="${version}" />
    <PackageReference Include="${source_generation_id}" Version="${version}"
                      OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
EOF

cat > "${consumer_directory}/Program.cs" <<'EOF'
using Qyl.Telemetry.SemanticConventions.Attributes.Http;
using Qyl.Telemetry.SemanticConventions.Incubating.Attributes.GenAi;
using Qyl.Telemetry.SemanticConventions.Incubating.Registry;
using Qyl.Telemetry.SemanticConventions.SourceGeneration;
using System.Text.Json;

namespace ReleaseSmoke;

[SemanticConventionAttributes("http")]
internal static partial class GeneratedHttp;

internal static class Program
{
    public static int Main()
    {
        string[] actual =
        [
            HttpAttributes.RequestMethod,
            GenAiAttributes.OperationName,
            GeneratedHttp.AttributeHttpRequestMethod,
        ];
        string[] expected =
        [
            "http.request.method",
            "gen_ai.operation.name",
            "http.request.method",
        ];

        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            return 1;
        }

        using var registryStream = SemanticConventionRegistry.OpenResolvedRegistry();
        using var registry = JsonDocument.Parse(registryStream);
        if (registry.RootElement.GetProperty("sources").GetArrayLength() != 2
            || registry.RootElement.GetProperty("json_schemas").GetArrayLength() != 8)
        {
            return 2;
        }

        if (!SemanticConventionRegistry.TryOpenPayloadSchema("gen_ai.input.messages", out var schemaStream)
            || schemaStream is null)
        {
            return 3;
        }

        using (schemaStream)
        using (var schema = JsonDocument.Parse(schemaStream))
        {
            if (schema.RootElement.ValueKind != JsonValueKind.Object)
            {
                return 4;
            }
        }

        Console.WriteLine("semantic-conventions release smoke passed");
        return 0;
    }
}
EOF

restore_attempts=1
if [[ "${package_source}" == "https://api.nuget.org/v3/index.json" ]]; then
  restore_attempts=60
fi

restore_log="${work_directory}/restore.log"
restored=false
for attempt in $(seq 1 "${restore_attempts}"); do
  rm -rf "${consumer_directory}/obj" "${work_directory}/packages"
  if dotnet restore "${consumer_directory}/ReleaseSmoke.csproj" \
    --configfile "${work_directory}/NuGet.Config" \
    --packages "${work_directory}/packages" \
    --no-cache >"${restore_log}" 2>&1; then
    restored=true
    break
  fi

  if [[ "${attempt}" == "${restore_attempts}" ]] \
    || ! grep -Eq 'NU110[12].*Qyl\.Telemetry\.SemanticConventions' "${restore_log}"; then
    cat "${restore_log}" >&2
    exit 1
  fi

  echo "Waiting for the published packages to become restorable (${attempt}/${restore_attempts})"
  sleep 10
done

if [[ "${restored}" != true ]]; then
  cat "${restore_log}" >&2
  exit 1
fi

dotnet run --project "${consumer_directory}/ReleaseSmoke.csproj" \
  --configuration Release \
  --no-restore
