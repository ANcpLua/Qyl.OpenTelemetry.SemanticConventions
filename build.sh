#!/usr/bin/env bash
# Repository-local Fallout build entry point.
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" &>/dev/null && pwd)"

# Quiet unless --verbose or QYL_VERBOSE=1: scripts/quiet.sh prints the target table, PASS or FAIL
# and the failing target's last lines, and keeps the whole output in
# artifacts/logs/build-<targets>.log. --help and AuditDocs, whose output is the point, print it.
verbose=${QYL_VERBOSE:-0}
arguments=()
name=build
for argument in "$@"; do
  case $argument in
    --verbose) verbose=1; continue ;;
    --help | -h | AuditDocs) verbose=1 ;;
    -*) parameters=1 ;;
    *) [[ -n ${parameters:-} ]] || name+="-$argument" ;;
  esac
  arguments+=("$argument")
done
if [[ $verbose != 1 ]]; then
  exec "$SCRIPT_DIR/scripts/quiet.sh" "${name//[^A-Za-z0-9._-]/_}" -- "$SCRIPT_DIR/build.sh" ${arguments[@]+"${arguments[@]}"}
fi
set -- ${arguments[@]+"${arguments[@]}"}
dotnet run --project "$SCRIPT_DIR/eng/build/_build.csproj" -- "$@"
