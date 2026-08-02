# MixerController.Firmware

Bewusst „dummer" Aktor-/Sensor-Knoten: Fährt die Mischer von zwei Heizkreisen
als Umschaltventile auf die per MQTT empfangene Zielposition und misst
Temperaturen über bis zu drei DS18B20-OneWire-Busse (Vor-/Rücklauf, Puffer,
Warmwasser).

**Die Entscheidungslogik (wann auf/zu) liegt nicht hier**, sondern in der
[RulesEngine](../../RulesEngine/README.md) (`MixerPositionRule`). Die Firmware
folgt schlicht dem letzten empfangenen Kommando (retained → nach Reboot sofort
bekannt):

- Nach jedem Boot: Referenzfahrt „auf" (Position ist ohne Rückmeldung
  unbekannt); das retained Kommando der RulesEngine übernimmt direkt danach.
- Es gibt nur Vollfahrten (115 % der konfigurierten Laufzeit, die
  Endlagenabschaltung des Antriebs fängt den Überlauf ab). Jede Fahrt ist
  damit zugleich eine Referenzfahrt.
- **Kein lokaler Watchdog** (bewusste Entscheidung): Absicherung erfolgt über
  Monitoring — das Status-JSON enthält Soll (`target`) und Ist (`position`);
  Abweichung über Fahrtdauer + Puffer bzw. ausbleibende Ist-Meldung nach einem
  Soll-Wechsel → Alarm (Umsetzung im Monitoring, noch offen). Letzte
  Rückfallebene ist der Handhebel am Antrieb. Elektrisch bleibt failsafe:
  stromlos = Antrieb steht, Richtung „Auf" vorgewählt.

## Hardware

- ESP32 DevKitC V4 (WROOM-32)
- 16-Kanal-Relaisboard (JQC-3FF-S-Z, 12-V-Spulen), Versorgung 12 V,
  Buck-Converter 12 V → 5 V für den ESP32
- **Ansteuerung im Open-Drain-Modus:** Die 5-V-referenzierten
  Optokoppler-Eingänge des Boards lassen sich mit aktiv getriebenen 3,3 V
  nicht ausschalten. Deshalb `OUTPUT_OPEN_DRAIN`: LOW = Eingang auf GND
  gezogen (Relais an), HIGH = Pin hochohmig, Stromkreis offen (Relais sicher
  aus — derselbe Zustand wie im Reset).
- Pro Mischer zwei Relais:
  - **Richtungs-Relais als Wechsler**: COM = Phase zum Antrieb, NC = Ader „Auf",
    NO = Ader „Zu". Stromlos = Richtung „Auf" (Failsafe).
  - **Fahrt-Relais**: schaltet die Phase zum Richtungs-Relais. Stromlos = Antrieb steht.
  - Durch diese Verdrahtung können „Auf" und „Zu" nie gleichzeitig anliegen.

### Pin-Belegung

| Funktion | GPIO |
|---|---|
| Mischer 1 Fahrt / Richtung | 16 / 17 |
| Mischer 2 Fahrt / Richtung | 18 / 19 |
| Reserve-Relais 1–5 | 21, 22, 23, 32, 33 |
| OneWire Bus 1 / 2 / 3 | 25 / 26 / 27 |

Nicht verwenden: 0, 2, 5, 12, 15 (Strapping), 1, 3 (UART), 6–11 (Flash),
34–39 (nur Eingänge).

## MQTT

| Topic | Richtung | Inhalt |
|---|---|---|
| `commands/MixerController/{location}/Mischer1\|Mischer2` | in (retained) | Zielposition `open` / `close` von der RulesEngine (publiziert bei Entscheidungsänderung) |
| `config/MixerController/{chipID}` | in (retained) | Konfiguration, siehe unten |
| `daten/Heizung/{location}/Mischersteuerung/{Mischer}` | out (retained) | Zustand als JSON (position = Ist, target = Soll, moving, timestamp) — publiziert bei Fahrtbeginn und -ende |
| `daten/temperatur/{location}/{sensorName}` | out (retained) | Temperaturwert in °C |
| `meta/MixerController/{location}/sensors` | out (retained) | Gefundene OneWire-Adressen je Bus (für die Zuordnung) |
| `meta/MixerController/{location}/version` | out (retained) | Firmware-Version |
| `OTAUpdate/MixerController` | in (retained) | OTA-Update-URL (CI-Pipeline) |

### Konfiguration (retained publizieren)

```json
{
  "Location": "M1",
  "TravelTimeSeconds": 140,
  "TemperatureIntervalSeconds": 60,
  "Sensors": [
    { "Address": "28-FF-64-1E-11-22-33-44", "Name": "HK1_Vorlauf" },
    { "Address": "28-FF-64-1E-55-66-77-88", "Name": "HK1_Ruecklauf" }
  ]
}
```

Sensoren ohne Mapping-Eintrag werden unter ihrer ROM-Adresse publiziert — so
lassen sich neue Fühler über das `meta/...`-Topic identifizieren und dann in der
Konfiguration benennen.

## Inbetriebnahme (stufenweise)

Das Projekt enthält drei PlatformIO-Environments (`platformio.ini`):

| Environment | Zweck |
|---|---|
| `relaytest` | Schaltet alle 9 Relais-Kanäle der Reihe nach (Verdrahtungstest) ✓ bestanden |
| `mixertest` | Mischer auf/zu per Tastendruck im seriellen Monitor |
| `esp32dev` | Voll integrierte Firmware mit MQTT/OTA (Default, wird von der CI gebaut) |

1. **Relais-Verdrahtung testen:** `pio run -e relaytest -t upload`, dann
   `pio device monitor`.
2. **Mischer testen:** `pio run -e mixertest -t upload`. Im seriellen Monitor:
   `1`/`2` = Mischer 1 auf/zu, `3`/`4` = Mischer 2 auf/zu, `0` = Stopp,
   `h` = Hilfe. Dabei die tatsächliche Laufzeit der Antriebe messen und
   prüfen, dass „auf"/„zu" richtig herum wirken (sonst Auf-/Zu-Adern am
   Richtungs-Relais tauschen).
3. **Voll integrierte Firmware deployen** (`esp32dev`): gemessene Laufzeit in
   der Firmware-Konfiguration hinterlegen, WW-Statuswerte in der
   RulesEngine-Konfiguration (`kube.yaml`) eintragen, RulesEngine deployen.
   Manuelle Tests: Zielposition direkt retained auf das Command-Topic
   publizieren — sie bleibt stehen, bis die RulesEngine das nächste Mal anders
   entscheidet. Für längere manuelle Eingriffe die RulesEngine auf 0 Replicas
   skalieren.
