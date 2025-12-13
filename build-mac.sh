#! /bin/bash
dotnet publish -c Release -r osx-%1 /p:PublishSingleFile=true