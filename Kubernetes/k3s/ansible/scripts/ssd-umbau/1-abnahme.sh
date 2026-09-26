#!/bin/bash
# Abnahme einer USB-SSD am Pi 4 unter Last — NUR LESEND, schreibt nichts auf die Platte.
# Bestanden: kein Durchgang > 5 s, keine uas_eh/reset-Meldungen, in0_lcrit_alarm immer 0.
# Hintergrund: Ein Gehäuse kann sich einwandfrei anmelden (uas, SMART) und trotzdem
# unter Last im 31-s-SCSI-Timeout hängen — lsusb/smartctl allein reichen nicht.
DEV=${1:-}
source "$(dirname "$0")/common.sh"
OUT=/tmp/ssd-abnahme.log
exec > >(tee "$OUT") 2>&1

lsusb -t | grep -i 'Mass Storage'
MARK=$(date '+%Y-%m-%d %H:%M:%S')
HW=$(grep -l rpi_volt /sys/class/hwmon/hwmon*/name | xargs dirname)
echo "== Strom vorher: lcrit=$(cat $HW/in0_lcrit_alarm) $(vcgencmd get_throttled)"

SAMPLES=/tmp/ssd-abnahme.power
: > $SAMPLES
( while :; do echo "$(date +%T) lcrit=$(cat $HW/in0_lcrit_alarm) $(vcgencmd get_throttled)" >> $SAMPLES; sleep 1; done ) &
SAMPLER=$!

# gnudd statt dd: dd ist auf Ubuntu 26.04 uutils (Rust) und scheitert mit
# iflag=direct am Blockgerät sofort mit "Invalid input" — sieht aus wie ein Defekt.
echo "== 10 x 256 MiB O_DIRECT an verteilten Offsets"
for i in 0 1 2 3 4 5 6 7 8 9; do
  skip=$(( i * 90000 ))
  t0=$(date +%s.%N)
  gnudd if=$DEV of=/dev/null bs=1M count=256 skip=$skip iflag=direct status=none
  rc=$?
  t1=$(date +%s.%N)
  printf 'Lauf %d  Offset %4d GiB  %6.2f s  rc=%d\n' $i $((skip/1024)) "$(awk "BEGIN{print $t1-$t0}")" $rc
  sleep 3                        # Leerlauf provoziert U1-Übergänge
done

echo "== 60 s Dauerlesen am Stück"
timeout 60 gnudd if=$DEV of=/dev/null bs=1M iflag=direct status=progress 2>&1 | tr '\r' '\n' | tail -1

echo "== hdparm -t"
hdparm -t $DEV | tail -1

kill $SAMPLER
echo "== Strom während der Last ($(wc -l < $SAMPLES) Samples)"
echo "lcrit=1 gesehen: $(grep -c 'lcrit=1' $SAMPLES)x"
echo "throttled != 0x0: $(grep -vc 'throttled=0x0$' $SAMPLES)x"

echo "== Kernel-Log seit Teststart"
journalctl -k --since "$MARK" --no-pager | grep -Ei 'uas_eh|reset|offlined|I/O error|timeout|voltage' \
  || echo "keine uas_eh/reset/Fehler-Meldungen"
echo "== Log: $OUT"
