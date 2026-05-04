@echo off
if "%~1"=="" (
    set ARCH=x64
) else (
    set ARCH=%1
)

echo Building for win-%ARCH%...
dotnet publish -c Release -r win-%ARCH% --self-contained true -p:PublishSingleFile=true
echo Build complete.
