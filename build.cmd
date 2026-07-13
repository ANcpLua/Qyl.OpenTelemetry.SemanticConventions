@echo off
:: Repository-local Nuke build entry point.
setlocal
set "SCRIPT_DIR=%~dp0"
dotnet run --project "%SCRIPT_DIR%eng\build\_build.csproj" -- %*
endlocal
