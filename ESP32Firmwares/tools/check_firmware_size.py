#!/usr/bin/env python3
"""Fails the build when a firmware image does not fit its OTA partition.

PlatformIO's own "Flash: xx%" line measures the ELF program size, not the padded
.bin that is actually written. The difference is easily 40 KB, so a green build can
still produce an image the device refuses to install - it downloads the update,
esp_ota_begin rejects it, and the device silently keeps running the old firmware.

The partition sizes are read from the partitions.bin that PlatformIO generated for
this build, so custom tables are handled without any configuration here.
"""

import struct
import sys
from pathlib import Path

ENTRY_SIZE = 32
ENTRY_MAGIC = 0x50AA
TYPE_APP = 0x00
SUBTYPE_FACTORY = 0x00
SUBTYPE_OTA_MIN, SUBTYPE_OTA_MAX = 0x10, 0x1F

# Leaves room for the next dependency bump instead of only reporting the crash after it
WARN_THRESHOLD_PERCENT = 90.0


def app_slots(partitions_bin: Path):
    """Returns [(label, size)] for every partition an OTA update can be written to."""
    blob = partitions_bin.read_bytes()
    slots = []
    for offset in range(0, len(blob) - ENTRY_SIZE + 1, ENTRY_SIZE):
        entry = blob[offset:offset + ENTRY_SIZE]
        magic, ptype, subtype, _pos, size = struct.unpack("<HBBII", entry[:12])
        if magic != ENTRY_MAGIC:
            break
        if ptype != TYPE_APP:
            continue
        if subtype == SUBTYPE_FACTORY or SUBTYPE_OTA_MIN <= subtype <= SUBTYPE_OTA_MAX:
            label = entry[12:28].rstrip(b"\x00").decode("utf-8", "replace")
            slots.append((label, size))
    return slots


def check(build_dir: Path) -> bool:
    firmware = build_dir / "firmware.bin"
    partitions = build_dir / "partitions.bin"

    if not firmware.exists():
        print(f"  {build_dir.name}: no firmware.bin, skipped")
        return True
    if not partitions.exists():
        # Without the table there is nothing to compare against; do not block the build
        print(f"  {build_dir.name}: no partitions.bin, size not verified")
        return True

    slots = app_slots(partitions)
    if not slots:
        print(f"  {build_dir.name}: no app partition found, size not verified")
        return True

    size = firmware.stat().st_size
    label, capacity = min(slots, key=lambda s: s[1])
    percent = size / capacity * 100

    detail = f"{size:,} B of {capacity:,} B ({percent:.1f}% of '{label}')"
    if size > capacity:
        print(f"  {build_dir.name}: FAIL - image does not fit: {detail}")
        return False
    if percent >= WARN_THRESHOLD_PERCENT:
        print(f"  {build_dir.name}: WARNING - little headroom left: {detail}")
        return True
    print(f"  {build_dir.name}: ok - {detail}")
    return True


def main() -> int:
    root = Path(sys.argv[1] if len(sys.argv) > 1 else ".pio/build")
    if not root.is_dir():
        print(f"{root} does not exist - run the build first")
        return 1

    build_dirs = sorted(d for d in root.iterdir() if d.is_dir())
    if not build_dirs:
        print(f"no build output below {root}")
        return 1

    print("Checking firmware images against their OTA partition:")
    if all([check(d) for d in build_dirs]):
        return 0

    print("\nThe image is larger than the partition it has to be flashed into.")
    print("The device would download the update and then refuse to install it.")
    print("Shrink the firmware (for example -DCORE_DEBUG_LEVEL=0) or give the board")
    print("a partition table with larger app slots - note that OTA cannot change the")
    print("table itself, so that requires flashing the devices over USB once.")
    return 1


if __name__ == "__main__":
    sys.exit(main())
