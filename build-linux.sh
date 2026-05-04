#! /bin/bash
ARCH=${1:-x64}

echo "Building for linux-$ARCH..."
dotnet publish -c Release -r linux-$ARCH --self-contained true -p:PublishSingleFile=true
echo "Build complete."
