@echo off
dotnet publish -c Release -r win-%1 /p:PublishSingleFile=true /p:PublishTrimmed=true /p:TrimMode=link /p:PublishReadyToRun=false /p:PublishReadyToRunShowWarnings=true /p:UseAppHost=true /p:IncludeNativeLibrariesForSelfExtract=true /p:SelfContained=true