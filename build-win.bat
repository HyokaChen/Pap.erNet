@echo off
dotnet publish -c Release -r win-%1 -p:PublishSingleFile=true