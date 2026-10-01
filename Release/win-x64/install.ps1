#Requires -RunAsAdministrator

# ---------------------------------------------------------------------------------------------
# FreeBlock install script for Windows
# ---------------------------------------------------------------------------------------------

$RELEASE_VERSION = [System.Version]"0.7.2"
$INSTALL_DIR     = "C:\Program Files\FreeBlock"
$SERVICE_NAME    = "freeblockd"

# Check installation status
$hasBinary  = Test-Path "$INSTALL_DIR\freeblock.exe"
$hasDaemon  = Test-Path "$INSTALL_DIR\freeblockd.exe"
$hasService = [bool](Get-Service -Name $SERVICE_NAME -ErrorAction SilentlyContinue)
$updated    = $hasBinary -or $hasDaemon -or $hasService

# Remove existing installation
if ($updated) {
    Write-Host "Existing installation detected"

    if ($hasBinary -and $hasDaemon -and $hasService) {
        # Healthy installation: check version to decide removal strategy
        $versionOutput  = & "$INSTALL_DIR\freeblock.exe" -v 2>$null
        $currentVersion = if ($versionOutput -match '(\d+\.\d+\.\d+)') {
            [System.Version]$Matches[1]
        }

        if ($currentVersion -and $RELEASE_VERSION -lt $currentVersion) {
            # Downgrade: use freeblock --uninstall to safely clean up config/state
            & "$INSTALL_DIR\freeblock.exe" --uninstall
            if ((Test-Path "$INSTALL_DIR\freeblock.exe") -or (Test-Path "$INSTALL_DIR\freeblockd.exe")) {
                Write-Host "Uninstall failed, aborting downgrade"
                exit 1
            }
        } else {
            # Upgrade (or same version reinstall): remove files directly
            Stop-Service -Name $SERVICE_NAME -Force -ErrorAction SilentlyContinue
            sc.exe delete $SERVICE_NAME | Out-Null
            Remove-Item "$INSTALL_DIR\freeblock.exe"
            Remove-Item "$INSTALL_DIR\freeblockd.exe"
        }
    } else {
        # Corrupted installation: remove manually
        if ($hasBinary)  { Remove-Item "$INSTALL_DIR\freeblock.exe"  -ErrorAction SilentlyContinue }
        if ($hasDaemon)  { Remove-Item "$INSTALL_DIR\freeblockd.exe" -ErrorAction SilentlyContinue }
        if ($hasService) {
            Stop-Service -Name $SERVICE_NAME -Force -ErrorAction SilentlyContinue
            sc.exe delete $SERVICE_NAME | Out-Null
        }
    }
}

# Migrate v0.6.X config
$stateJsonPath = Join-Path $env:USERPROFILE "AppData\Roaming\FreeBlock\state.json"
if (Test-Path $stateJsonPath) {
    Write-Host "Migrating user data..."
    python .\migrate.py *> $null
}

# Install new version
New-Item -ItemType Directory -Path $INSTALL_DIR -Force | Out-Null
Copy-Item ".\freeblock.exe"  "$INSTALL_DIR\freeblock.exe"
Copy-Item ".\freeblockd.exe" "$INSTALL_DIR\freeblockd.exe"

# Create service
sc.exe create $SERVICE_NAME binPath= "$INSTALL_DIR\freeblockd.exe" start= auto DisplayName= "FreeBlock" obj= "LocalSystem" | Out-Null
sc.exe start $SERVICE_NAME | Out-Null

# Add to PATH
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";$INSTALL_DIR", [EnvironmentVariableTarget]::Machine)

if ($updated) {
    Write-Host "Update complete!"
} else {
    Write-Host "Installation complete!"
}

Write-Host "Run: freeblock --help"

# SIG # Begin signature block
# SIG # End signature block
