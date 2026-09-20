# Fahrzeug-Wallbox-Zuordnung

Wie das System ermittelt und anzeigt, welches Fahrzeug an welcher Wallbox lädt.

## Ausgangslage

Früher lud der BMW immer in der Garage, Mini und VW teilten sich die Wallbox außen.
Diese Zuordnung war an sieben Stellen hart verdrahtet — unter anderem als drei feste
Kacheln in `ChargingOverview.razor` und als `switch` in `MQTTService`. Sie stimmt nicht
mehr, und die Anzeige war dadurch systematisch falsch, ohne es zu zeigen.

Zwei weitere Fehler kamen dazu:

- Der Verbindungsstatus wurde vom **Fahrzeug** gelesen (`CarStatusData.ChargerConnected`)
  statt von der Wallbox — also aus der langsamsten verfügbaren Quelle.
- Mini und VW zeigten beide `OutsideChargingCurrentSessionWh` und hingen an derselben
  Einstellung `OutsideChargingEnabled` — zwei Bedienelemente für einen Schalter, zwei
  Kacheln für eine Zahl.

## Leitprinzip

> **Die Wallbox ist die Wahrheit über den Ladevorgang. Das Fahrzeug liefert nur
> Identität und Fahrzeugdaten.**

Steckerzustand, Ladeleistung, Phasenzahl, Energie der Sitzung, Sitzungsbeginn und Dauer
kommen ausschließlich aus der Wallbox und sind damit in 5 Sekunden aktuell. Vom Fahrzeug
kommt nur, was die Box nicht wissen kann: Ladestand, Ziel-Ladestand, Reichweite,
Kilometerstand. Diese Werte dürfen alt sein — sie werden mit ihrem Alter angezeigt.

Die Folge: **Die Anzeige funktioniert vollständig, auch wenn kein Fahrzeug erkannt ist.**
Die Zuordnung ist eine Beschriftung, keine Voraussetzung.

## Warum das Ausschlussverfahren nicht funktioniert

Naheliegend wäre: Wenn eine Box belegt ist und zwei von drei Fahrzeugen melden
„nicht verbunden", muss es das dritte sein. Das ist falsch, und der Grund ist wichtig
genug, um ihn festzuhalten.

**Gemessener Fall vom 2026-09-20, 07:02 UTC:**

```
KebaOutside:  CarIsPlugedIn = true,  4044 W,  31.935 Wh in dieser Sitzung
BMW   chargerConnected = false   lastUpdate 2026-08-16  (Connector-Token abgelaufen)
Mini  chargerConnected = false   lastUpdate 2026-09-19  (100 %, NOCHARGING)
VW    chargerConnected = false   lastUpdate 2026-09-20 06:32  (off, parked)
```

Ausschluss hätte mit Überzeugung den BMW benannt. **Tatsächlich lud der VW.**

Der VW-Datensatz war nicht *alt* — er war 30 Minuten alt bei einem 900-Sekunden-Poll,
also nach jedem vernünftigen Maßstab frisch. Er war **inhaltlich überholt, obwohl
frisch zugestellt**. Eine Frischeprüfung hätte das nicht verhindert, weil sie das
Zustellalter misst und nicht das Inhaltsalter.

> **Regel: Das „nicht verbunden" eines pollenden Fahrzeugs ist keine Negativevidenz.**
> Ausschluss setzt einen Kanal voraus, der Zustandswechsel zeitnah meldet. Ein
> Portal-Poll ist das nicht, und keine Konfiguration kann das heilen.

Beim VW ist das keine Latenz, sondern die Natur der Quelle (EU-Data-Act-Portal,
`VW_POLL_INTERVAL` = 900 s). Nur ein fahrzeugseitiger Sensor — etwa WiCAN am OBD-Port —
könnte das an der Wurzel lösen.

## Verworfene Ansätze

| Ansatz | Warum verworfen |
|---|---|
| **RFID-Karte je Fahrzeug** | Die Keba kann es, aber es verlangt bei jedem Laden eine Karte. Bewusst abgelehnt. |
| **Leistungssignatur** (Phasenzahl, maximale Ladeleistung) | Die Fahrzeuge unterscheiden sich darin nicht. |
| **Ausschlussverfahren** | Siehe oben — liefert falsche Ergebnisse mit hoher Bestimmtheit. |
| **GPS-Geofence Garage/Stellplatz** | Die Stellplätze liegen innerhalb der GPS-Streuung; in der Garage gibt es keinen Fix, die gemeldete Position ist die letzte vor der Einfahrt. Der VW liefert überhaupt kein GPS. Für „ist zuhause" wäre es tauglich, für „welche Box" nicht. Die Position wird mitgeschrieben, um das später empirisch zu prüfen. |

## Zuordnungslogik

Reine Funktion in der `RulesEngine` (`Rules/VehicleAssignmentRule.cs`), unit-getestet
nach dem Muster der Mischerregeln:

```
(Wallbox-Zustände, Fahrzeug-Zustände, manueller Override, letzte Zuordnung)
    -> Zuordnung je Wallbox
```

**Evidenz, in dieser Reihenfolge:**

1. **Manueller Override** — in der Web-Oberfläche gesetzt, gilt für die laufende
   `SitzungsId`. Vertrauensgrad `bestätigt`.
2. **Positive Fahrzeugmeldung** — ein Fahrzeug meldet `chargerConnected = true`,
   während genau eine Box belegt ist und noch kein Fahrzeug hat.
   Vertrauensgrad `erkannt`.
3. **Historie** — das Fahrzeug, das zuletzt an dieser Box lud.
   Vertrauensgrad `vermutet`.
4. Nichts davon: `unbekannt`.

**Eigenschaften:**

- **Klebend.** Eine Zuordnung gilt bis zum Ende der Wallbox-Sitzung (Stecker gezogen).
  Sie wird nur *aufgewertet* (`vermutet` → `erkannt` → `bestätigt`), nie stillschweigend
  umgeworfen.
- **Override endet mit der Sitzung.** Die nächste Sitzung beginnt wieder mit Automatik
  plus Historie-Vermutung. Kein vergessener Dauerzustand, der einen Fahrzeugwechsel
  überlebt.
- **Vermutungen sind als solche sichtbar** — grau mit Fragezeichen, ein Klick genügt zur
  Korrektur.

## Topics

Nach `MQTT-Topic-Konvention.md`:

```
daten/Laden/M3/Garage/Status              Ist-Zustand der Box      KebaConnector
daten/Laden/M3/Garage/Ladesitzung         Sitzung + Zaehlerstaende ChargingController
daten/Laden/M3/Garage/Zuordnung           Fahrzeug + Vertrauen     RulesEngine
daten/Laden/M3/Stellplatz/…               dito
daten/Laden/M3/Regelung/Situation         Reglerbild               ChargingController
daten/Fahrzeug/BMW/Status                 Fahrzeugdaten            BMWConnector
daten/Fahrzeug/Mini/Status
daten/Fahrzeug/VW/Status
befehle/Laden/M3/Garage/Ladestrom         Sollstrom                ChargingController
konfiguration/Laden/M3/Regelung/Einstellungen
konfiguration/Laden/M3/Garage/Zuordnung   manueller Override       Web
```

Alle Topics sind retained und tragen einen `Zeitpunkt`.

Zwei Entwurfsentscheidungen:

- **Drei Topics, verbunden über die `SitzungsId`.** Der Sitzungsdatensatz hat drei
  Beitragende — die Box liefert Grenzen und Gesamtenergie, der ChargingController die
  Energieaufteilung, die RulesEngine das Fahrzeug. Ein Topic mit drei Autoren geht nicht,
  deshalb publiziert jeder Dienst nur, was er selbst weiß. Der DataHub führt beim
  Schreiben zusammen und **schreibt nur bei übereinstimmender `SitzungsId`**; so ist die
  Konsistenz geprüft statt bloß gehofft. Einzelheiten in `Ladeprotokoll.md`.
- **Kein Ereignis-Topic für das Sitzungsende.** Der `Ladesitzung`-Payload trägt ein Feld
  `Zustand: laufend | beendet`. Heute wird `…_ChargingSessionEnded` mit QoS 0 und ohne
  Retain verschickt — geht die Nachricht verloren, ist die Sitzung für immer weg.

## Anzeige

Die beiden Wallboxen sind die Hauptobjekte und immer sichtbar, auch wenn frei:

```
┌─ GARAGE ──────────● lädt ─┐  ┌─ STELLPLATZ ─────○ frei ─┐
│ [Bild]  BMW  ✓ bestätigt  │  │                          │
│         64 % → 80 %       │  │    kein Fahrzeug         │
│  4,1 kW · 3-phasig · 6 A  │  │    angeschlossen         │
│  12,4 kWh · seit 18:42    │  │                          │
│  Dauer 3 h 12 min         │  │                          │
│  [Freigabe ✓] [Prio ★]    │  │  [Freigabe ✓] [Prio ☆]   │
└───────────────────────────┘  └──────────────────────────┘
```

- Boxstatus aus `DeviceState` der Keba: frei / Stecker drin, wartet / lädt / Fehler.
- Fahrzeugname mit Vertrauensgrad, klickbar zur Korrektur.
- **Freigabe und Priorität gehören zur Box**, nicht zum Fahrzeug.

Darunter ein **Fahrzeugbereich** mit einer vollen Karte je Fahrzeug: Ladestand und Ziel,
Reichweite und Prognose, Kilometerstand, Durchschnittsverbrauch, Ladeleistung, Spannung
und Strom laut Fahrzeug, Lademodus, Fahrzeugzustand, Position und **Alter der Daten**.
Veraltete Werte werden ausgegraut und mit ihrem Alter beschriftet, statt sie zu
verstecken oder als frisch auszugeben.

## Historie und Ladeprotokoll

Beendete Sitzungen schreibt der DataHub nach InfluxDB 3 — mit **Fahrzeug und
Vertrauensgrad** als getrennte Felder, sodass eine Kilowattstunden-Bilanz je Fahrzeug auf
`bestaetigt` und `erkannt` eingeschränkt werden kann.

Die Rückschau lebt in **Grafana**, nicht in der Web-UI. Zurechnungsregel für die
Aufteilung PV / Batterie / Netz, das Zählermodell, die Tabelle `ladesitzungen` und der
Dashboard-Zuschnitt stehen in **`Ladeprotokoll.md`**.
