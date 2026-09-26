#!/bin/bash
# USB-SSD löschen, partitionieren, formatieren — ⚠ DESTRUKTIV für die SSD.
# Layout für 1 TB (MBR wie SD-Karte und k3snode3):
#   p1 512 MiB vfat (bootbar) | p2 200 GiB ext4 / | p3 500 GiB ext4 Runner-Cache | Rest frei
# Vorläufige Labels ssd-boot/ssd-root: Solange die SD das System trägt, dürfen nie
# zwei Dateisysteme "writable" heißen. Umbenannt wird erst in 4-wartungsfenster.sh.
set -euo pipefail
DEV=${1:-}
source "$(dirname "$0")/common.sh"
if grep -q "^$REAL" /proc/mounts; then echo "ABBRUCH: $REAL ist eingehängt"; exit 1; fi
OUT=/tmp/ssd-vorbereiten.log
exec > >(tee -a "$OUT") 2>&1

echo "== Signaturen entfernen"
wipefs -a "$DEV"-part* 2>/dev/null || true
wipefs -a "$DEV"

echo "== blkdiscard (TRIM-Nachweis über die ganze Platte)"
# Das Neueinlesen der Partitionstabelle (wipefs) setzt provisioning_mode auf 'full'
# zurück. Die udev-Regel fängt das ab, hier trotzdem explizit setzen.
PM=$(ls -d /sys/block/$KNAME/device/scsi_disk/*/provisioning_mode)
echo unmap > "$PM"
echo "provisioning_mode=$(cat "$PM") discard_max=$(cat /sys/block/$KNAME/queue/discard_max_bytes)"
if blkdiscard -f -v "$DEV"; then echo "TRIM: OK"; else echo "TRIM: FEHLGESCHLAGEN (rc=$?) — weiter ohne TRIM"; fi

echo "== Partitionieren"
sfdisk "$DEV" <<'EOF'
label: dos
start=2048, size=512MiB, type=c, bootable
size=200GiB, type=83
size=500GiB, type=83
EOF
udevadm settle
sleep 2

echo "== Formatieren (vorläufige Labels)"
mkfs.vfat -F 32 -n ssd-boot "$DEV"-part1
mkfs.ext4 -F -L ssd-root "$DEV"-part2
mkfs.ext4 -F -L act-runner-cache "$DEV"-part3
udevadm settle

echo "== Ergebnis"
lsblk -o NAME,PTTYPE,PARTTYPE,PARTFLAGS,START,SIZE,LABEL,FSTYPE "$REAL"
lsblk -D "$REAL"
echo "provisioning_mode nach Partitionieren: $(cat "$PM")   (muss unmap sein, sonst udev-Regel prüfen)"
echo "== Log: $OUT"
