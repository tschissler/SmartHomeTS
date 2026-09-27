#!/bin/bash
# Runner-Cache-Partition einhängen (SmartHomeDeployments #45): LABEL=act-runner-cache
# -> /var/lib/act-runner-cache, darin docker/ als hostPath der arm64-Runner.
# Nicht destruktiv, idempotent — erneutes Starten prüft nur und ändert nichts.
# DANACH: kubectl label node <node> smarthome/runner-cache=ssd
#
# Per Label statt per by-id-Pfad, anders als die Skripte 1–4: Dort geht es um das
# rohe Gerät, das gelöscht oder beschrieben wird. Hier wird nur ein Dateisystem
# eingehängt, und fstab spricht es ohnehin per LABEL an (wie / und /boot/firmware).
# Das Skript prüft also genau das, worauf sich der Boot später verlässt. Statt der
# by-id-Prüfung: Das Label muss eindeutig sein, ext4, und auf derselben USB-SSD
# liegen, die / trägt — eine fremde, gleich beschriftete Platte fällt damit raus.
set -euo pipefail
export LC_ALL=C
LABEL=act-runner-cache
MNT=/var/lib/act-runner-cache
DATA=$MNT/docker      # muss zum hostPath in forgejo/runners.yaml passen
# nofail: Fehlt die Partition beim Boot, landet der Node sonst im Emergency-Mode —
# headless, also ohne Hände am Gerät nicht mehr erreichbar, und mit ihm alles
# andere, was dort läuft. Mit nofail bootet er normal, und die Runner starten
# nicht, weil der hostPath type: Directory auf $DATA zeigt, das nur auf der
# Partition existiert (kein stilles Schreiben auf /). device-timeout: nicht 90 s
# auf eine fehlende Platte warten. nofail ordnet den Mount nicht vor
# local-fs.target ein; startet k3s zuerst, wiederholt der Kubelet den Pod-Start,
# bis der Mount da ist.
OPTS=noatime,nofail,x-systemd.device-timeout=10s
LINE="LABEL=$LABEL	$MNT	ext4	$OPTS	0	2"

[ "$(id -u)" = 0 ] || { echo "ABBRUCH: als root starten (sudo)"; exit 1; }

echo "== Partition suchen (LABEL=$LABEL)"
DEVS=$(blkid -c /dev/null -t LABEL=$LABEL -o device || true)   # -c /dev/null: frisch prüfen, kein Cache
N=$(printf '%s\n' "$DEVS" | grep -c . || true)
[ "$N" = 1 ] || { echo "ABBRUCH: $N Dateisysteme mit LABEL=$LABEL, erwartet genau 1:"; echo "$DEVS"; exit 1; }
PART=$DEVS
DISK=$(lsblk -no PKNAME "$PART")
ROOTDISK=$(lsblk -no PKNAME "$(findmnt -no SOURCE /)")
[ "$(blkid -s TYPE -o value "$PART")" = ext4 ] || { echo "ABBRUCH: $PART ist kein ext4"; exit 1; }
[ "$(lsblk -dno TRAN "/dev/$DISK")" = usb ] || { echo "ABBRUCH: /dev/$DISK hängt nicht an USB"; exit 1; }
[ "$DISK" = "$ROOTDISK" ] || { echo "ABBRUCH: $PART liegt auf /dev/$DISK, / aber auf /dev/$ROOTDISK"; exit 1; }
echo "$PART auf /dev/$DISK ($(cat /sys/block/$DISK/device/model)), $(lsblk -dno SIZE "$PART")"

echo "== fstab"
mkdir -p "$MNT"   # vor findmnt --verify: ein fehlendes Ziel wertet es als Fehler
# Vor dem Eintragen, und nur die eigene Zeile: Ein Befund an fremden Einträgen
# soll hier nicht abbrechen.
TAB=$(mktemp); printf '%s\n' "$LINE" > "$TAB"
findmnt --verify --tab-file "$TAB" || { rm -f "$TAB"; exit 1; }
rm -f "$TAB"
if grep -qE "^[^#]*[[:space:]]$MNT[[:space:]]" /etc/fstab || grep -q "^LABEL=$LABEL[[:space:]]" /etc/fstab; then
  CUR=$(grep -E "^LABEL=$LABEL[[:space:]]|^[^#]*[[:space:]]$MNT[[:space:]]" /etc/fstab)
  if [ "$CUR" = "$LINE" ]; then echo "Eintrag vorhanden"
  else echo "ABBRUCH: abweichender Eintrag, von Hand klären:"; echo "  ist:  $CUR"; echo "  soll: $LINE"; exit 1; fi
else
  cp -a /etc/fstab "/etc/fstab.vor-runner-cache.$(date +%Y%m%d-%H%M%S)"
  printf '%s\n' "$LINE" >> /etc/fstab
  echo "eingetragen: $LINE"
fi
systemctl daemon-reload

echo "== Einhängen"
if findmnt -n "$MNT" >/dev/null; then
  [ "$(findmnt -no SOURCE "$MNT")" = "$PART" ] || { echo "ABBRUCH: $MNT trägt $(findmnt -no SOURCE "$MNT")"; exit 1; }
  echo "bereits eingehängt"
else
  mount "$MNT"      # über fstab, damit der Eintrag selbst geprüft ist
fi

echo "== Reservierte Blöcke"
# ext4 hält 5 % (25 GiB) für root zurück. Das ist für / gedacht, nicht für einen
# Cache; dockerd läuft ohnehin als root und würde sie mitbenutzen, df zeigte also
# weniger frei, als die BuildKit-GC tatsächlich sieht.
[ "$(tune2fs -l "$PART" | awk -F: '/^Reserved block count/{gsub(/ /,"",$2); print $2}')" = 0 ] \
  && echo "bereits 0" || tune2fs -m 0 "$PART"

echo "== Datenverzeichnis"
# Existiert nur auf der Partition. Die Unterverzeichnisse je Pod legt der Kubelet
# über subPathExpr selbst an.
install -d -m 711 -o root -g root "$DATA"

echo "== Ergebnis"
findmnt "$MNT"
df -h "$MNT"
ls -la "$MNT"
echo "fstrim.timer: $(systemctl is-enabled fstrim.timer 2>/dev/null || true)   (enabled = wöchentlicher TRIM)"
echo "== Nächster Schritt: kubectl label node $(hostname -s) smarthome/runner-cache=ssd"
