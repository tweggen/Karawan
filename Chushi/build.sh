#!/bin/bash  
dotnet build -c Debug -f net9.0 --sc
dotnet publish -c Debug -f net9.0 --sc
