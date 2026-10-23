@echo off
set DOTNET_CLI_HOME=%~dp0.dotnet
set DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
set DOTNET_MULTILEVEL_LOOKUP=0
dotnet build "%~dp0src\LMScreenShot\LMScreenShot.csproj"
