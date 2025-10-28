#!/bin/bash

dotnet build-server shutdown
dotnet build -c Debug -f net9.0 --sc
dotnet build -c Debug -f netstandard2.0 --sc
dotnet publish -c Debug -f net9.0 --sc
dotnet build-server shutdown
