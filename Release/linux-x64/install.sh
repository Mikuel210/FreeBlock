#!/bin/bash

# ---------------------------------------------------------------------------------------------
# FreeBlock install script for Linux (systemd)
# ---------------------------------------------------------------------------------------------

RELEASE_VERSION="0.7.2"

# Check installation status
has_binary=false
has_daemon=false
has_service=false
updated=false

[ -f /usr/bin/freeblock ]                      && has_binary=true
[ -f /usr/bin/freeblockd ]                     && has_daemon=true
[ -f /etc/systemd/system/freeblock.service ]   && has_service=true

$has_binary || $has_daemon || $has_service && updated=true

# Semantic version comparison
version_lt() {
    [ "$(printf '%s\n' "$1" "$2" | sort -V | head -1)" = "$1" ] && [ "$1" != "$2" ]
}

# Remove existing installation
if [ "$updated" = true ]; then
    echo "Existing installation detected"

    if $has_binary && $has_daemon && $has_service; then
        # Healthy installation: check version to decide removal strategy
        current_version=$(freeblock -v 2>/dev/null | grep -oP '\d+\.\d+\.\d+' | head -1)

        if [ -n "$current_version" ] && version_lt "$RELEASE_VERSION" "$current_version"; then
            # Downgrade: use freeblock --uninstall to safely clean up config/state
            freeblock --uninstall
            if [ -f /usr/bin/freeblock ] || [ -f /usr/bin/freeblockd ]; then
                echo "Uninstall failed, aborting downgrade"
                exit 1
            fi
        else
            # Upgrade (or same version reinstall): remove files directly
            sudo systemctl stop freeblock
            sudo rm /usr/bin/freeblock
            sudo rm /usr/bin/freeblockd
            sudo rm /etc/systemd/system/freeblock.service
        fi
    else
        # Corrupted installation: remove manually
        $has_binary  && sudo rm /usr/bin/freeblock
        $has_daemon  && sudo rm /usr/bin/freeblockd
        $has_service && sudo systemctl stop freeblock 2>/dev/null
        $has_service && sudo rm /etc/systemd/system/freeblock.service
    fi
fi

# Migrate v0.6.X config
if sudo test -f "/root/.config/freeblock/state.json"; then
    echo "Migrating user data..."
    sudo python3 ./migrate.py > /dev/null 2>&1
fi

# Install new version
sudo cp ./freeblock /usr/bin/freeblock
sudo cp ./freeblockd /usr/bin/freeblockd
sudo cp ./freeblock.service /etc/systemd/system/freeblock.service
sudo systemctl daemon-reload
sudo systemctl enable --now freeblock

if [ "$updated" = true ]; then
    echo "Update complete!"
else
    echo "Installation complete!"
fi

echo "Run: freeblock --help"
