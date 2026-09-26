#!/bin/bash
# SD -> SSD kopieren. Wiederholbar: Lauf 1 im Betrieb (Grobkopie, bei SD-Leserate
# ~27 MB/s etwa 30 min für 46 GB), Lauf 2 aus 4-wartungsfenster.sh mit gestopptem k3s.
# Liest nur von der SD, schreibt nur auf die Partitionen mit den vorläufigen Labels.
# Im Betrieb als Unit starten, damit es eine getrennte SSH-Sitzung überlebt:
#   sudo systemd-run --unit=ssd-clone <pfad>/3-klonen.sh /dev/disk/by-id/usb-…-0:0
set -euo pipefail
DEV=${1:-}
source "$(dirname "$0")/common.sh"
M=/mnt/ssd-clone

mkdir -p $M/root $M/boot
mountpoint -q $M/root || mount "$DEV"-part2 $M/root
mountpoint -q $M/boot || mount "$DEV"-part1 $M/boot
trap 'umount $M/boot $M/root' EXIT

echo "== $(date +%T) Root: / -> SSD p2"
# -x: bleibt auf dem Root-Dateisystem (kein /proc, /sys, /run, /boot/firmware,
#     keine iSCSI-/Overlay-/Snap-Mounts) — die Mountpunkte selbst werden angelegt.
# rc 24 = Dateien während der Kopie verschwunden (Logs, Sockets) — unkritisch
ionice -c3 rsync -aHAXx --numeric-ids --delete --info=stats1 / $M/root/ || [ $? -eq 24 ]

echo "== $(date +%T) Boot: /boot/firmware -> SSD p1"
rsync -rt --modify-window=1 --delete --info=stats1 /boot/firmware/ $M/boot/

echo "== $(date +%T) Ergebnis"
df -h / $M/root /boot/firmware $M/boot
grep -v '^#' $M/root/etc/fstab
cat $M/boot/current/cmdline.txt
