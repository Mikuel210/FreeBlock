#!/bin/bash

# Remove existing releases
yes | rm ./Release/linux-x64/freeblock    &> /dev/null 
yes | rm ./Release/linux-x64/freeblockd   &> /dev/null
yes | rm ./Release/osx-x64/freeblock      &> /dev/null
yes | rm ./Release/osx-x64/freeblockd     &> /dev/null
yes | rm ./Release/osx-arm64/freeblock    &> /dev/null
yes | rm ./Release/osx-arm64/freeblockd   &> /dev/null
yes | rm ./Release/win-x64/freeblock.exe  &> /dev/null
yes | rm ./Release/win-x64/freeblockd.exe &> /dev/null
yes | rm ./Release/linux-x64.zip          &> /dev/null
yes | rm ./Release/osx-x64.zip            &> /dev/null
yes | rm ./Release/osx-arm64.zip          &> /dev/null
yes | rm ./Release/win-x64.zip            &> /dev/null

# Build and copy new releases
dotnet publish CLI -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish Daemon -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish CLI -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish Daemon -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish CLI -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
dotnet publish Daemon -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true
dotnet publish CLI -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish Daemon -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

cp ./CLI/bin/Release/net10.0/linux-x64/publish/CLI ./Release/linux-x64/freeblock
cp ./Daemon/bin/Release/net10.0/linux-x64/publish/Daemon ./Release/linux-x64/freeblockd
cp ./CLI/bin/Release/net10.0/osx-x64/publish/CLI ./Release/osx-x64/freeblock
cp ./Daemon/bin/Release/net10.0/osx-x64/publish/Daemon ./Release/osx-x64/freeblockd
cp ./CLI/bin/Release/net10.0/osx-arm64/publish/CLI ./Release/osx-arm64/freeblock
cp ./Daemon/bin/Release/net10.0/osx-arm64/publish/Daemon ./Release/osx-arm64/freeblockd
cp ./CLI/bin/Release/net10.0/win-x64/publish/CLI.exe ./Release/win-x64/freeblock.exe
cp ./Daemon/bin/Release/net10.0/win-x64/publish/Daemon.exe ./Release/win-x64/freeblockd.exe

# Zip releases
(cd ./Release && zip -r linux-x64.zip linux-x64)
(cd ./Release && zip -r osx-x64.zip osx-x64)
(cd ./Release && zip -r osx-arm64.zip osx-arm64)
(cd ./Release && zip -r win-x64.zip win-x64)

echo "Release complete!"
