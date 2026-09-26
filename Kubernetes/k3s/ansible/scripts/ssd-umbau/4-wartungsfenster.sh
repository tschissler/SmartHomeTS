#!/bin/bash
# Wartungsfenster: k3s anhalten, SD -> SSD abgleichen, Labels tauschen, herunterfahren.
# VORHER: kubectl drain <node> --ignore-daemonsets --delete-emptydir-data
# DANACH: grüne LED aus → Strom ab → SD ziehen (unverändert aufheben = Rückweg) → Strom an
#         → kubectl uncordon <node>
set -euo pipefail
DEV=${1:-}
DIR=$(dirname "$(readlink -f "$0")")
source "$DIR/common.sh"
OUT=/var/log/ssd-wartungsfenster.log   # wird am Ende auch auf die SSD kopiert
exec > >(tee "$OUT") 2>&1

echo "== $(date +%T) k3s anhalten"
# stop allein lässt die Container weiterlaufen — k3s-killall.sh beendet sie
systemctl stop k3s-agent
/usr/local/bin/k3s-killall.sh > /var/log/k3s-killall.log 2>&1
pgrep -a containerd-shim && { echo "ABBRUCH: Container laufen noch"; exit 1; } || echo "keine Container mehr"

echo "== $(date +%T) Abgleich SD -> SSD"
"$DIR/3-klonen.sh" "$DEV"

echo "== $(date +%T) Labels tauschen (ab hier tragen SD und SSD dieselben Labels)"
# fstab und cmdline referenzieren per LABEL → an der Boot-Konfiguration ist nichts anzupassen
e2label "$DEV"-part2 writable
fatlabel "$DEV"-part1 system-boot
udevadm settle
lsblk -o NAME,SIZE,LABEL,FSTYPE,MOUNTPOINT /dev/mmcblk0 "$REAL"
# Log auf die SSD, damit es nach dem Boot lesbar ist
mount "$DEV"-part2 /mnt/ssd-clone/root
cp "$OUT" /var/log/k3s-killall.log /mnt/ssd-clone/root/var/log/
umount /mnt/ssd-clone/root

echo "== $(date +%T) Herunterfahren in 10 s — danach Strom ab, SD ziehen, Strom an"
sync
sleep 10
systemctl poweroff
