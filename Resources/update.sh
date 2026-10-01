#!/bin/bash

# Remove previous installation
freeblock --uninstall
sudo systemctl disable --now freeblock
sudo rm /usr/bin/freeblock
sudo rm /usr/bin/freeblockd
sudo rm /etc/systemd/system/freeblock.service

# Build new version
dotnet publish CLI -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish Daemon -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true

# Install new version
sudo cp ./CLI/bin/Release/net10.0/linux-x64/publish/CLI /usr/bin/freeblock
sudo cp ./Daemon/bin/Release/net10.0/linux-x64/publish/Daemon /usr/bin/freeblockd
sudo cp ./Release/linux-x64/freeblock.service /etc/systemd/system/freeblock.service
sudo systemctl daemon-reload
sudo systemctl enable --now freeblock

echo "Update complete!"
echo "Run: freeblock --help"
