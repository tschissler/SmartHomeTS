# Pi-Worker von SD-Karte auf USB-SSD umstellen

Erprobt am 2026-09-26 an **k3snode5** (Acer FA200 1 TB NVMe im AXAGON-Gehäuse, Bridge
Realtek RTL9210 `0bda:9210`). Vollständiger Bericht mit Messwerten: SmartHomeDeployments
Issue #35, Kommentare 728, 729, 732.

Keine Playbooks, sondern Skripte, die man Schritt für Schritt von Hand startet: Der Umbau
passiert einmal pro Node, braucht zwischendurch Hände an der Hardware, und jeder Schritt
soll geprüft sein, bevor der nächste läuft.

Die Skripte 1–4 nehmen den **by-id-Pfad der SSD** als einziges Argument und brechen ab, wenn
er nicht auf eine USB-Platte zeigt. **Nie `/dev/sdX` verwenden:** Auf den Pi-Nodes liegen
die Longhorn-iSCSI-Volumes als `sda`, `sdb`, … daneben — vor dem Umbau hieß die SSD auf
node5 `sdd`, danach `sda`.

## Vorbereitung

```fish
scp -r scripts/ssd-umbau <user>@k3snodeN.intern:/tmp/
ssh k3snodeN.intern 'ls -l /dev/disk/by-id/ | grep usb-'   # Pfad ohne -partN notieren
```

EEPROM prüfen: `sudo rpi-eeprom-config` muss `BOOT_ORDER=0xf41` (SD, dann USB) zeigen —
dann bootet der Pi ohne SD-Karte von USB. Ein angebotenes Bootloader-Update ist nicht nötig
(USB-Boot kann der Pi 4 seit 2020) und wird besser getrennt eingespielt, nicht zusammen
mit dem ersten SSD-Boot.

## Ablauf

| # | Schritt | Ausfall | Wer |
|---|---------|---------|-----|
| 1 | `sudo /tmp/ssd-umbau/1-abnahme.sh <dev>` — Lesetest unter Last, Unterspannung | nein | Hand |
| 2 | udev-Regel installieren (s. u.) | nein | Hand |
| 3 | `sudo /tmp/ssd-umbau/2-vorbereiten.sh <dev>` — ⚠ löscht die SSD | nein | Hand |
| 4 | `sudo systemd-run --unit=ssd-clone /tmp/ssd-umbau/3-klonen.sh <dev>` — Grobkopie, ~30 min | nein | Hand |
| 5 | `kubectl drain k3snodeN --ignore-daemonsets --delete-emptydir-data` | ja | kubectl |
| 6 | `sudo /tmp/ssd-umbau/4-wartungsfenster.sh <dev>` — fährt am Ende herunter | ja | Hand |
| 7 | LED aus → Strom ab → **SD ziehen** → Strom an | ja | Hardware |
| 8 | Prüfen (s. u.), dann `kubectl uncordon k3snodeN` | – | kubectl |
| 9 | `sudo /tmp/ssd-umbau/5-runner-cache.sh`, dann `kubectl label node k3snodeN smarthome/runner-cache=ssd` — Runner-Cache (#45) | nein | Hand + kubectl |

Fortschritt von Schritt 4: `journalctl -u ssd-clone -f`. Bei node5 dauerte das
Wartungsfenster (5–8) rund 10 Minuten.

**Rückweg in jeder Phase:** Strom ab, SD-Karte wieder rein, SSD ab, Strom an. Die SD wird
nie beschrieben und bleibt deshalb nach dem Umbau unverändert liegen.

### udev-Regel für TRIM (Schritt 2)

```fish
sudo install -m 644 /tmp/ssd-umbau/60-usb-ssd-trim.rules /etc/udev/rules.d/
sudo udevadm control --reload
sudo udevadm trigger --action=change (readlink -f <dev>)
cat /sys/block/(basename (readlink -f <dev>))/device/scsi_disk/*/provisioning_mode   # → unmap
```

### Prüfen nach dem Boot (Schritt 8)

```fish
od -An -tu4 --endian=big /proc/device-tree/chosen/bootloader/boot-mode   # 4 = USB
findmnt -no SOURCE /                                                     # SSD, nicht mmcblk
cat /sys/block/sda/device/scsi_disk/*/provisioning_mode                  # unmap
cat /sys/class/hwmon/hwmon*/in0_lcrit_alarm                              # 0, 24–48 h beobachten
```

### Runner-Cache einhängen (Schritt 9)

Gehört zu SmartHomeDeployments **#45**, nicht zum eigentlichen Umbau, und kann jederzeit
nach Schritt 8 laufen. Das Skript braucht kein Argument: Es findet die Partition über ihr
Label `act-runner-cache` (von `2-vorbereiten.sh` angelegt) und prüft, dass sie eindeutig
ist und auf derselben USB-SSD liegt wie `/`. Es trägt sie mit `nofail` in die fstab ein,
hängt sie unter `/var/lib/act-runner-cache` ein und legt darin `docker/` an. Nur dieses
Verzeichnis verwenden die arm64-Runner als `hostPath` (`type: Directory`): Fehlt der Mount,
startet der Runner nicht, statt den Cache auf `/` zu schreiben.

Erst **nach** dem Skript das Node-Label setzen, denn das Label lässt die Runner auf den Node.
Beim ersten Node mit Label kommt dazu der einmalige Umstieg des StatefulSets
(Ablauf im Issue).

## Fallen

- **`provisioning_mode` fällt auf `full` zurück**, sobald der Kernel die Platte neu prüft
  (Probe beim Anstecken/Boot, Neueinlesen der Partitionstabelle). Eine Regel auf
  `SUBSYSTEM=="scsi_disk"` kommt *vor* dieser Prüfung und wird überschrieben. Die Regel
  hängt deshalb am Blockgerät, dessen `add`/`change` erst danach kommt.
- **`dd` ist auf Ubuntu 26.04 uutils (Rust)** und scheitert mit `iflag=direct` am
  Blockgerät sofort mit `IO error: Invalid input` — ohne Kernel-Meldung. `gnudd` nehmen.
- **Doppelte Labels:** Nach dem Klonen tragen SD und SSD dieselben Labels
  (`writable`/`system-boot`). Deshalb bekommt die SSD bis zum Wartungsfenster vorläufige
  Labels, und beim ersten SSD-Boot darf keine SD-Karte stecken.
- **`k3s-agent` stoppen reicht nicht:** Die Container laufen weiter und schreiben während
  des Abgleichs. `k3s-killall.sh` beendet sie.
- **Lasttest, nicht nur Identifikation:** Das erste Gehäuse (UGREEN, ebenfalls RTL9210)
  meldete sich einwandfrei als `uas` mit SMART und hing unter Last trotzdem im
  31-s-SCSI-Timeout. `1-abnahme.sh` vor jedem Einbau laufen lassen.
- **Lesetest ≠ Stromtest:** Beim Schreiben zieht eine NVMe mehr als beim Lesen.
  `in0_lcrit_alarm` nach dem Umbau 24–48 h beobachten; springt er auf 1, braucht es einen
  aktiven USB-Hub.
- **AdGuard** (einziger LAN-DNS, 1 Replica) fällt beim Drain des Nodes, auf dem er läuft,
  für 1–2 Minuten aus.
