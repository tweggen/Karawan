#!/bin/bash

cd Tooling/Cmdline
bash ./build.sh
cd ../../Chushi
bash ./build.sh
cd ..
dotnet build Karawan/Karawan.csproj
