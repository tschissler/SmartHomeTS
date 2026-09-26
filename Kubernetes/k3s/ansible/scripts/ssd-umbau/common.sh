# Gemeinsame Schutzprüfung, wird von den Skripten per `source` eingebunden.
# Erwartet $DEV (by-id-Pfad der USB-SSD) und bricht ab, wenn er nicht auf eine
# nicht eingehängte USB-Platte zeigt — auf den Pi-Nodes liegen daneben die
# Longhorn-iSCSI-Volumes als sdX, der Kernel-Name allein ist nicht eindeutig.

export LC_ALL=C

[ -n "${DEV:-}" ] || { echo "Aufruf: $0 /dev/disk/by-id/usb-…-0:0"; exit 1; }
case "$DEV" in /dev/disk/by-id/usb-*) ;; *) echo "ABBRUCH: $DEV ist kein by-id-USB-Pfad"; exit 1;; esac
REAL=$(readlink -f "$DEV") || { echo "ABBRUCH: $DEV fehlt"; exit 1; }
KNAME=$(basename "$REAL")
[ "$(lsblk -dno TRAN "$REAL")" = usb ] || { echo "ABBRUCH: $REAL hängt nicht an USB"; exit 1; }
MODEL=$(cat /sys/block/$KNAME/device/model)
echo "== Ziel: $DEV -> $REAL ($MODEL)"
