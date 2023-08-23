#!/bin/bash
git pull
git submodule update
dotnet build -c Release
cd ParallelOriginGameServer/bin/Release/net7.0/
./ParallelOriginGameServer &
disown
