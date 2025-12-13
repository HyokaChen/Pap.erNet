#! /bin/bash
dotnet publish -c Release -r linux-%1 -p:PublishSingleFile=true