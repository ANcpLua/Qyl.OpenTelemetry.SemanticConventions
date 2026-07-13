#!/usr/bin/env bash
# Repository-local Nuke build entry point.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"
dotnet run --project "$SCRIPT_DIR/eng/build/_build.csproj" -- "$@"
