# MQTT-Topic-Konvention

Verbindliche Regel für alle neuen Topics im SmartHome-System. Bestehende Topics werden
migriert, wenn ihr Bereich ohnehin angefasst wird — nicht auf Vorrat.

## Die Regel

```
art / Kategorie / [Ort /] Geraet / Aspekt
```

| Ebene | Schreibweise | Inhalt | InfluxDB-Tag |
|---|---|---|---|
| `art` | klein, geschlossener Satz | `daten`, `befehle`, `konfiguration`, `status` | — |
| `Kategorie` | groß | fachliche Domäne: `Laden`, `Fahrzeug`, `Heizung`, `Temperatur` | `category` |
| `Ort` | groß | Gebäude: `M1`, `M3` — **entfällt bei ortslosen Geräten** | `location` |
| `Geraet` | groß | das konkrete Objekt: `Garage`, `Stellplatz`, `BMW`, `Keller` | `device` |
| `Aspekt` | groß | was von diesem Objekt: `Status`, `Ladesitzung`, `Wert` | `measurement` |

Beispiele:

```
daten/Laden/M3/Garage/Status
daten/Temperatur/M1/Keller/Wert
daten/Fahrzeug/BMW/Status
befehle/Laden/M3/Garage/Ladestrom
konfiguration/Laden/M3/Regelung/Einstellungen
```

**Ausnahme `status/`.** Der bestehende Geräte-Heartbeat lautet
`status/<Ort>/<Geraetetyp>/<Name>` (z. B. `status/M1/TemperaturSensor/Wohnzimmer`) und
folgt der Regel **nicht** — ihm fehlen Kategorie- und Aspekt-Ebene. Er bleibt vorerst so,
weil er von 17 ESP32-Geräten bedient wird und eine Umstellung einen OTA-Rollout auf die
gesamte Flotte bedeutet. **Neue Teilnehmer übernehmen dieses Bestandsformat**, damit im
`status`-Namensraum nicht zwei Formate nebeneinanderstehen; ortslose Teilnehmer wie die
.NET-Dienste verwenden `Cluster` als Ort. Die Migration des ganzen Namensraums ist ein
eigener, späterer Schritt für alle Teilnehmer gemeinsam.

## Warum diese Reihenfolge

Die Ebenen bilden die Tags der InfluxDB-Tabelle `energy_values` ab
(`category`, `location`, `device`, `measurement`). Dadurch wird der Converter im
DataHub mechanisch, statt für jeden Bereich eine eigene Zerlegung zu brauchen — heute
steht etwa die Location der Wallboxen als Literal `"M3"` im Code.

Der zweite Grund sind brauchbare Wildcards. Weil gleichartige Objekte auf gleicher
Tiefe liegen, reicht ein Abonnement statt einer Fallliste:

```
daten/Laden/+/+/Status      alle Wallboxen
daten/Fahrzeug/+/Status     alle Fahrzeuge
daten/Temperatur/M1/+/Wert  alle Temperaturen in M1
```

## Die Ort-Ebene

Der Ort ist **Pflicht für ortsgebundene Geräte** und **entfällt für ortslose**.
Entschieden wird das **einmal je Kategorie**, nie je Nachricht — nur so bleibt die
Tiefe innerhalb einer Kategorie konstant und `+`-Wildcards funktionieren.

Ein Fahrzeug ist ortslos: es bewegt sich und lädt manchmal auswärts. Kategorie
`Fahrzeug` hat deshalb vier Ebenen, Kategorie `Laden` fünf. In InfluxDB bekommt
`location` bei ortslosen Geräten `-`, analog zur bestehenden Handhabung von
`sub_category`.

## Schreibregeln

- **Sprache: Deutsch** ab der Kategorie. Die Domänensprache des Systems ist Deutsch,
  ebenso das InfluxDB-Datenmodell (`Laden`, `AktuelleLadeleistung`, `Verbindungsstatus`).
- **Art-Ebene klein.** Sie ist Syntax, kein Domänenvokabular, und bleibt bei
  `daten` / `befehle` / `konfiguration` / `status`. Dadurch bleiben die bestehenden
  `daten/#`- und `status/#`-Abonnements gültig.
- **Ab der Kategorie Substantive groß**, nach deutscher Rechtschreibung.
- **Keine Umlaute.** Transliteriert: `ae`, `oe`, `ue`, `ss` — wie bereits gelebt bei
  `Wasserzaehler`, `Gaestezimmer`, `Buero`, `Kueche`, `zisterneFuellstand`.
  MQTT erlaubt UTF-8, aber Shell-Pipelines, Grafana-Queries und Quelltext-Encodings
  nicht zuverlässig.
- **Kein Typ im Namen.** `WallboxGarage` oder `Garage_ChargingSessionEnded` gehören
  in Ebenen aufgelöst, nicht in einen Bezeichner gequetscht.
- **Kein Hersteller im Namen.** Eine Wallbox heißt `Garage`, nicht `KebaGarage` — der
  Hersteller ist austauschbares Implementierungsdetail.
- Topics sind **case-sensitiv**. Ein Schreibfehler in der Groß-/Kleinschreibung
  erzeugt still ein zweites Topic, und der Subscriber bekommt einfach nichts.

## Payload-Regeln

- **Immer JSON**, auch bei einem einzelnen Wert.
- **Pflichtfeld `Zeitpunkt`** (ISO 8601, UTC). Ohne Zeitstempel im Payload ist das
  Alter eines retained Werts nicht feststellbar, und Empfangszeit wird mit Messzeit
  verwechselt. Das hat im Bestand bereits zwei Notlösungen erzwungen:
  `MQTTService.TrackDeviceTopics()` vergleicht Payloads, um ein Retain-Echo von echtem
  Leben zu unterscheiden, und die `RulesEngine` prüft `MaxStatusAge` gegen die
  Empfangszeit — wodurch ein retained Wert nach einem Reconnect fälschlich frisch wirkt.
- **Zustand wird retained publiziert, Ereignisse nie.** Ein Puls oder ein Kommando mit
  Relativwirkung darf sich beim Reconnect nicht wiederholen (so hält es die
  `RulesEngine` bei den Mischerpulsen bereits).
- Zustand, der zusammengehört, gehört in **einen** Payload. Zwei Topics, die
  konsistent sein müssten, laufen irgendwann auseinander. Lässt sich das nicht vermeiden,
  weil mehrere Dienste beitragen, verbindet sie ein gemeinsamer Schlüssel, und der
  Zusammenführende **prüft ihn** (so beim Ladesitzungs-Datensatz über die `SitzungsId`).
- **Auch Befehle tragen einen `Zeitpunkt`, und der Empfänger muss ihn auswerten.** Ein
  retained Kommando wird beim Verbinden sofort zugestellt, unabhängig von seinem Alter.
  Wer stattdessen die Empfangszeit als Alter nimmt, hält ein beliebig altes Kommando für
  frisch. Im Bestand setzt genau das die Notfallfreigabe der Wallbox außer Kraft — siehe
  `Backlog-Laden.md`, Punkt 16.

## Migrationsstand

| Bereich | Stand |
|---|---|
| `daten/Laden/…`, `daten/Fahrzeug/…` | wird mit dem Ladevorhaben umgestellt (siehe `Backlog-Laden.md`) |
| `status/<Ort>/<Geraetetyp>/<Name>` | **dokumentierte Ausnahme** (siehe oben): Kategorie- und Aspekt-Ebene fehlen. Neue Teilnehmer folgen dem Bestandsformat; Migration nur gemeinsam mit der ESP32-Flotte |
| `daten/temperatur/…`, `daten/luftfeuchtigkeit/…` | Kleinschreibung und fehlende Aspekt-Ebene; 18 ESP32-Firmwares, nur bei OTA-Anlass |
| `cangateway/…` | ohne Art-Ebene; Migration offen |
| `meta/…`, `OTAUpdate/…` | sollen langfristig in `status/` bzw. `konfiguration/Ota/` aufgehen |
| `data/…` (englisch) | Altbestand, wird bei Berührung nach `daten/` gezogen |
| `Nachrichten/…` | wurde nur von der Flutter-App gelesen, entfällt mit deren Stilllegung |

Bei Migrationen gilt ein **harter Schnitt**, sobald alle Konsumenten im Repo liegen —
kein Parallelbetrieb. Parallelbetrieb lohnt nur bei Geräten, die nicht gleichzeitig
aktualisiert werden können (ESP32-Flotte).
