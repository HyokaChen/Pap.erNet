#! /bin/bash
ARCH=${1:-x64}

echo "Building for osx-$ARCH..."
dotnet publish -c Release -r osx-$ARCH --self-contained true -p:PublishSingleFile=true
echo "Build complete."
