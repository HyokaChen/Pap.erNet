@echo off
dotnet publish -c Release -r linux-%1 /p:PublishSingleFile=true /p:PublishTrimmed=true /p:PublishReadyToRun=true /p:PublishReadyToRunShowWarnings=true /p:UseAppHost=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:SelfContained=true --self-contained true