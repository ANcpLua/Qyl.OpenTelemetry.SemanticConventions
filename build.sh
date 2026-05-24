#!/usr/bin/env bash
# Nuke build entry point. Delegates to eng/build/_build.csproj which dogfoods
# Qyl.OpenTelemetry.SemanticConventions.Nuke as a ProjectReference.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
dotnet run --project "$SCRIPT_DIR/eng/build/_build.csproj" -- "$@"
