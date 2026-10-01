#!/usr/bin/env python3
"""
FreeBlock config migration: v0.6 -> v0.7

Adds an "Options" object to every entry under "Locks" and "Schedules":
  - Locks:     Options = {}
  - Schedules: Options = {"WarningTime": "00:15:00"}
"""

from __future__ import annotations

import argparse
import json
import os
import platform
import shutil
import stat
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Tuple

DEFAULT_WARNING_TIME = "00:15:00"
REQUIRED_TOP_LEVEL_KEYS = ("Block", "Locks", "Lists", "Schedules")

# Default values for config.json (a separate file, sibling to state.json).
# This step is independent of the Locks/Schedules "Options" migration
# above: it always runs and only fills in whichever of these keys are
# missing, regardless of whether that migration was needed/applied.
DEFAULT_CONFIG_JSON_VALUES = {
    "defaultWarningTime": "00:15:00",
    "showAllSchedules": False,
    "showAllLists": False,
    "showWarningPeriodSchedules": True,
}


class MigrationError(Exception):
    """Raised for any condition that should stop the migration."""


def get_config_path() -> Path:
    """
    Resolve FreeBlock's state.json path for the current OS, relative to the
    home directory of the user running this script.
    """
    home = Path.home()
    system = platform.system()

    if system == "Darwin":
        return home / "Library" / "Preferences" / "FreeBlock" / "state.json"
    elif system == "Linux":
        return home / ".config" / "freeblock" / "state.json"
    elif system == "Windows":
        return home / "AppData" / "Roaming" / "FreeBlock" / "state.json"
    else:
        raise MigrationError(f"Unsupported platform: {system!r}")


def get_secondary_config_path(state_path: Path) -> Path:
    """config.json lives alongside state.json in the same directory."""
    return state_path.parent / "config.json"


def load_config(path: Path) -> dict:
    if not path.exists():
        raise MigrationError(f"Config file not found: {path}")

    try:
        with path.open("r", encoding="utf-8") as f:
            return json.load(f)
    except json.JSONDecodeError as e:
        raise MigrationError(f"Config file is not valid JSON: {path} ({e})")


def validate_structure(config: dict) -> None:
    """
    Sanity-check that this looks like a FreeBlock config at all (v0.6 or
    v0.7), so we don't blindly inject Options into an unrelated or older/
    unrecognized format.
    """
    if not isinstance(config, dict):
        raise MigrationError("Top-level config is not a JSON object.")

    missing = [k for k in REQUIRED_TOP_LEVEL_KEYS if k not in config]
    if missing:
        raise MigrationError(
            f"Config is missing expected top-level key(s): {', '.join(missing)}. "
            "This doesn't look like a v0.6 or v0.7 FreeBlock config -- refusing "
            "to guess at a migration."
        )

    if not isinstance(config["Locks"], list):
        raise MigrationError('"Locks" is present but is not a list.')
    if not isinstance(config["Schedules"], list):
        raise MigrationError('"Schedules" is present but is not a list.')

    for i, lock in enumerate(config["Locks"]):
        if not isinstance(lock, dict):
            raise MigrationError(f"Locks[{i}] is not a JSON object.")
    for i, sched in enumerate(config["Schedules"]):
        if not isinstance(sched, dict):
            raise MigrationError(f"Schedules[{i}] is not a JSON object.")


def needs_migration(config: dict) -> bool:
    """
    A config is v0.6 (pre-Options) if any Lock or Schedule entry lacks the
    "Options" key. If Locks and Schedules are both empty (or all entries
    already have "Options"), there's nothing to migrate.
    """
    for lock in config["Locks"]:
        if "Options" not in lock:
            return True
    for sched in config["Schedules"]:
        if "Options" not in sched:
            return True
    return False


def migrate(config: dict) -> Tuple[dict, int, int]:
    """
    Adds "Options" to every Lock/Schedule entry that doesn't already have
    one. Only touches entries that are actually missing it, so this is
    safe to run against a partially-migrated file too.
    """
    locks_changed = 0
    for lock in config["Locks"]:
        if "Options" not in lock:
            lock["Options"] = {}
            locks_changed += 1

    schedules_changed = 0
    for sched in config["Schedules"]:
        if "Options" not in sched:
            sched["Options"] = {"WarningTime": DEFAULT_WARNING_TIME}
            schedules_changed += 1

    return config, locks_changed, schedules_changed


def backup_path_for(path: Path) -> Path:
    timestamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    return path.with_name(f"{path.name}.v0.6.bak.{timestamp}")


def write_config_atomic(path: Path, config: dict) -> None:
    """
    Writes the migrated config to a temp file in the same directory, then
    atomically replaces the original. Preserves the original file's
    permission bits and ownership where possible.
    """
    original_stat = path.stat()

    fd, tmp_name = tempfile.mkstemp(
        dir=str(path.parent), prefix=f".{path.name}.", suffix=".tmp"
    )
    tmp_path = Path(tmp_name)
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as f:
            json.dump(config, f, indent=2)
            f.write("\n")

        os.chmod(tmp_path, stat.S_IMODE(original_stat.st_mode))
        try:
            os.chown(tmp_path, original_stat.st_uid, original_stat.st_gid)
        except (AttributeError, PermissionError, OSError):
            # os.chown doesn't exist on Windows, and may fail even as root
            # in some sandboxed/container setups -- not fatal.
            pass

        os.replace(tmp_path, path)
    except Exception:
        tmp_path.unlink(missing_ok=True)
        raise


def migrate_config_json(path: Path) -> None:
    """
    Best-effort: make sure config.json has the default keys in
    DEFAULT_CONFIG_JSON_VALUES, adding whichever ones are missing.
    Existing values are never overwritten.

    This is intentionally silent and swallows every failure (file
    missing, unreadable, not valid JSON, not an object, already fully
    migrated, permission errors, etc.) -- by design, per the caller,
    nothing is ever printed and nothing here should ever affect the
    process's exit code.
    """
    try:
        if not path.exists():
            return

        with path.open("r", encoding="utf-8") as f:
            config = json.load(f)

        if not isinstance(config, dict):
            return

        changed = False
        for key, default in DEFAULT_CONFIG_JSON_VALUES.items():
            if key not in config:
                config[key] = default
                changed = True

        if not changed:
            return

        write_config_atomic(path, config)
    except Exception:
        pass


def main() -> int:
    # Independent step: always attempt this, regardless of whether the
    # Locks/Schedules Options migration below is needed, already done,
    # or state.json itself fails to load/validate. Silent by design.
    try:
        migrate_config_json(get_secondary_config_path(get_config_path()))
    except Exception:
        pass

    try:
        path = get_config_path()
        config = load_config(path)
        validate_structure(config)

        if not needs_migration(config):
            return 0

        migrated, locks_changed, schedules_changed = migrate(config)
        backup_path = backup_path_for(path)
        shutil.copy2(path, backup_path)
        write_config_atomic(path, migrated)

        return 0

    except MigrationError as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1
    except OSError as e:
        print(f"Filesystem error: {e}", file=sys.stderr)
        return 1

if __name__ == "__main__":
    sys.exit(main())