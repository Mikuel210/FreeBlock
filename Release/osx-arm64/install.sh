#!/bin/bash

# ---------------------------------------------------------------------------------------------
# FreeBlock install script for macOS
# ---------------------------------------------------------------------------------------------

RELEASE_VERSION="0.7.2"
PLIST_LABEL="com.freeblock.daemon"
PLIST_PATH="/Library/LaunchDaemons/${PLIST_LABEL}.plist"

# Check installation status
has_binary=false
has_daemon=false
has_service=false
updated=false

[ -f /usr/local/bin/freeblock ]  && has_binary=true
[ -f /usr/local/bin/freeblockd ] && has_daemon=true
[ -f "$PLIST_PATH" ]             && has_service=true

$has_binary || $has_daemon || $has_service && updated=true

# Semantic version comparison
version_lt() {
    local IFS=.
    local i a=($1) b=($2)
    for (( i=0; i<${#a[@]} || i<${#b[@]}; i++ )); do
        local va=${a[i]:-0} vb=${b[i]:-0}
        (( va < vb )) && return 0
        (( va > vb )) && return 1
    done
    return 1
}

# Remove existing installation
if [ "$updated" = true ]; then
    echo "Existing installation detected"

    if $has_binary && $has_daemon && $has_service; then
        # Healthy installation: check version to decide removal strategy
        current_version=$(freeblock -v 2>/dev/null | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -1)

        if [ -n "$current_version" ] && version_lt "$RELEASE_VERSION" "$current_version"; then
            # Downgrade: use freeblock --uninstall to safely clean up config/state
            freeblock --uninstall
            if [ -f /usr/local/bin/freeblock ] || [ -f /usr/local/bin/freeblockd ]; then
                echo "Uninstall failed, aborting downgrade"
                exit 1
            fi
        else
            # Upgrade (or same version reinstall): remove files directly
            sudo launchctl bootout system "$PLIST_PATH"
            sudo rm /usr/local/bin/freeblock
            sudo rm /usr/local/bin/freeblockd
            sudo rm "$PLIST_PATH"
        fi
    else
        # Corrupted installation: remove manually
        $has_binary && sudo rm /usr/local/bin/freeblock
        $has_daemon && sudo rm /usr/local/bin/freeblockd
        if $has_service; then
            sudo launchctl bootout system "$PLIST_PATH" 2>/dev/null
            sudo rm "$PLIST_PATH"
        fi
    fi
fi

# Migrate v0.6.X config
if sudo test -f "/root/Library/Preferences/FreeBlock/state.json"; then
    echo "Migrating user data..."
    sudo python3 ./migrate.py > /dev/null 2>&1
fi

# Install new version
sudo mkdir -p /usr/local/bin
sudo cp ./freeblock  /usr/local/bin/freeblock
sudo cp ./freeblockd /usr/local/bin/freeblockd
sudo cp ./"${PLIST_LABEL}.plist" "$PLIST_PATH"
sudo chown root:wheel "$PLIST_PATH"
sudo chmod 644 "$PLIST_PATH"
sudo launchctl bootstrap system "$PLIST_PATH"

if [ "$updated" = true ]; then
    echo "Update complete!"
else
    echo "Installation complete!"
fi

echo "Run: freeblock --help"
