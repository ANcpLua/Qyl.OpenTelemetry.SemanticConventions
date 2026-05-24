@echo off
:: Nuke build entry point. Delegates to eng/build/_build.csproj which dogfoods
:: Qyl.OpenTelemetry.SemanticConventions.Nuke as a ProjectReference.
setlocal
set "SCRIPT_DIR=%~dp0"
dotnet run --project "%SCRIPT_DIR%eng\build\_build.csproj" -- %*
endlocal
