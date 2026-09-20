# Fahrzeugdaten in InfluxDB

Was der DataHub aus `daten/Fahrzeug/<Auto>/Status` in die Datenbank schreibt, in welche
Tabelle, unter welchen Tags — und warum der Zeitstempel nicht der Empfangszeitpunkt ist.

Umgesetzt mit Punkt 17 des Lade-Vorhabens (`Archiv/Backlog-Laden-2026-09.md`). Der Payload selbst ist
`SharedContracts/CarStatusData.cs`, die Topics stehen in `SharedContracts/FahrzeugTopics.cs`,
die Umsetzung in `SmartHome.DataHub/SmartHome.DataHub/Fahrzeugdaten.cs`.

---

## Die wichtigste Regel: der Zeitstempel ist `lastUpdate`

Ein Fahrzeug-Payload trägt **zwei** Zeiten, und sie bedeuten Verschiedenes:

| Feld | Bedeutung |
|---|---|
| `Zeitpunkt` | Wann der **Connector gesendet** hat. Pflichtfeld der Topic-Konvention. Sagt, ob der Connector lebt |
| `lastUpdate` | Wann das **Fahrzeug gemessen** hat. Das ist das Alter, auf das es ankommt |

Geschrieben wird mit `lastUpdate`. Der Grund ist nicht Pedanterie: CarData schiebt nur bei
Fahrzeugereignissen, ein **kerngesunder** Connector publiziert deshalb wochenalte Werte. Am
2026-09-20 trug der BMW einen `lastUpdate` vom 2026-08-16 — 35 Tage alt, ohne dass irgendetwas
kaputt war.

Mit `DateTimeOffset.UtcNow` als Zeitstempel würde dieser 35 Tage alte Wert bei **jedem**
Neustart des DataHubs als aktuelle Messung eingetragen. In der Historie entstünde eine Linie
frisch aussehender Punkte, die alle denselben uralten Wert tragen — schlimmer als keine Daten,
weil sie glaubwürdig aussehen.

`Zeitpunkt` ist nur die Rückfallebene, wenn ein Payload gar kein `lastUpdate` trägt. Trägt er
beides nicht, wird **nichts** geschrieben und eine Warnung geloggt. Die Ankunftszeit ist kein
Ersatz.

**Plausibilitätsfenster.** Verworfen wird eine Messzeit vor dem 01.01.2020 (ein
`default(DateTime)` oder eine Epoch-Null kommt als wohlgeformter Zeitstempel an und landet
sonst Jahrzehnte in der Vergangenheit) und eine mehr als eine Stunde in der Zukunft (eine
vorgehende Fahrzeuguhr ist normal, ein Punkt in der Zukunft bleibt sonst der „letzte Wert"
seiner Serie, bis die Zukunft ihn eingeholt hat).

### Die Entdopplung folgt daraus

Ein retained Payload wird vom Broker bei **jedem** Subscribe erneut zugestellt. Mit der
richtigen Zeitquelle erzeugt er denselben Punkt: gleiche Tabelle, gleiche Tags, gleiche Zeit.
Genau das ist in InfluxDB 3 der Primärschlüssel — solche Zeilen werden dedupliziert, der
zuletzt geschriebene Wert gewinnt. Es entsteht **keine** zweite Messung.

Zusätzlich vergleicht der DataHub den Payload je Fahrzeug mit dem zuletzt gesehenen und
überspringt ihn, wenn er identisch ist. Das ist keine zweite Absicherung derselben Sache,
sondern Arbeitsvermeidung: Die Korrektheit kommt aus dem Zeitstempel, das Überspringen spart
den Weg durch Batch und Netz.

Publiziert ein Connector denselben Wert mit **neuer** `lastUpdate` (der VWConnector trägt bei
einer Teilantwort des Portals den letzten bekannten Wert weiter), entsteht ein neuer Punkt —
und das ist richtig so: Das Portal sagt, es habe erneut gemessen.

---

## Tags

Für jede Zeile gleich:

| Tag | Wert | Warum |
|---|---|---|
| `category` | `Fahrzeug` | neuer Wert in `MeasurementCategory` |
| `sensor_type` | `Fahrzeug` | die Quelle. Käme irgendwann der geplante WiCAN-Dongle am VW dazu, bekäme der seinen eigenen `sensor_type` beim selben `device` |
| `location` | `-` | Ein Fahrzeug bewegt sich. Es hat keinen Ort |
| `device` | `BMW`, `Mini`, `VW` | **aus dem Topic-Pfad**, nicht aus dem Payload. Dieselbe Entscheidung wie beim Wallbox-Zweig: sie macht den Konverter mechanisch und hält einen umbenannten Nickname aus dem Tag-Satz |
| `measurement_id` | `Fahrzeug_<Auto>_<Messwert>` | ohne Ort — das „-" im Namen würde ihn nur schwerer lesbar machen |
| `sub_category` | siehe Tabelle unten | |

---

## Welcher Wert in welche Tabelle

| Feld im Payload | Tabelle | `measurement` | `sub_category` | Einheit |
|---|---|---|---|---|
| `battery` | `percent_values` | `Ladestand` | `Ist` | % |
| `chargingTarget` | `percent_values` | `Ladeziel` | `Soll` | % |
| `remainingRange` | `distance_values` | `Reichweite` | `Ist` | km |
| `predictedRange` | `distance_values` | `ReichweiteNachLadung` | `Prognose` | km |
| `mileage` | `distance_values` | `Kilometerstand` | `Ist` | km |
| `chargingPower` | `power_values` | `Ladeleistung` | `Ladeleistung` | W |
| `maxEnergy` | `energy_values` | `Batteriekapazitaet` | `Other` | kWh |
| `chargerConnected` | `status_values` | `SteckerVerbunden` | `Verbindungsstatus` | 1 / 0 |
| `chargingStatus` | `status_values` | `Ladestatus` | `Ist` | Code, siehe unten |
| `moving` | `status_values` | `Faehrt` | `Ist` | 1 / 0 |
| `position` | `position_values` | `Position` | `-` | Grad |

**`distance_values` und `position_values` sind neu.** Die Referenz im Grafana-Repo
(`docs/influxdb-reference.md`) kannte für eine Entfernung nichts — im Haus gab es bis jetzt
keine. Das Haus-Muster ist aber eindeutig: **eine Tabelle je physikalischer Größe**
(`temperature_values`, `volume_values`, `voltage_values`, …). Zwei weitere fügen sich ein,
statt eine Größe in eine fremde Tabelle zu zwängen.

Für Reichweite und Kilometerstand war das keine Geschmacksfrage: `counter_values` und
`status_values` speichern ihr Feld als **Int16**. Ein Kilometerstand jenseits von 32 767 km
würde dort beim Schreiben scheitern — jedes unserer Fahrzeuge erreicht das.

Die **Position** ist ein Punkt mit zwei Feldern (`value_latitude`, `value_longitude`), keine
zwei Messwerte: Eine Breite ohne ihre Länge ist keine halbe Position, sondern keine. Eine
Position 0/0 wird verworfen — `SharedContracts.GeoPosition` hat nicht-nullbare `double`, ein
leeres Positions-Objekt käme sonst als Golf von Guinea in der Datenbank an.

Die **Batteriekapazität** liegt in `energy_values`, obwohl sie kein Zähler ist. `value_delta_kwh`
bleibt deshalb 0, und `MAX - MIN` über einen Zeitraum ergibt 0 — was stimmt: Es ist keine
Energie geflossen. Gelesen wird hier der Wert selbst, über Jahre: die Degradationskurve der
Batterie.

### Codes des Ladestatus

Beide Hersteller formulieren denselben Vorgang anders. Die Codes bilden das Vokabular ab, das
auch die Web-Oberfläche rendert (`CarStatusDataVisualizer.LadestatusText`) — ein Vokabular,
zwei Leser.

| Code | Bedeutung | BMW / Mini | VW |
|---|---|---|---|
| 0 | lädt nicht | `NOCHARGING` | `off` |
| 1 | lädt | `CHARGINGACTIVE` | `charging` |
| 2 | pausiert | `CHARGINGPAUSED` | |
| 3 | beendet | `CHARGINGENDED` | |
| 4 | Fehler | `CHARGINGERROR` | `error` |
| 5 | bereit | | `ready_for_charging` |
| 6 | Erhaltungsladung | | `conservation` |
| 7 | entlädt | | `discharging` |

Ein **unbekannter** Zustand bekommt keinen Code und wird nicht geschrieben; der DataHub loggt
ihn als Warnung. Eine erfundene Zahl wäre ein Wert in der Historie, der nichts bedeutet, und
eine 0 würde als „lädt nicht" gelesen — eine Aussage über das Fahrzeug, die niemand gemacht
hat.

---

## Was nicht geschrieben wird

Nicht alles, was ankommt, muss aufgehoben werden.

| Feld | Warum nicht |
|---|---|
| `plugEventId` | Ein Zähler für Steckvorgänge, reines Diagnosemittel. Sobald die Ladesitzungen aus Punkt 13 existieren, ist die Frage „ein langer Ladevorgang oder zwei" dort beantwortet |
| `chargingMode` | Eine Einstellung, keine Messung. Ändert sich fast nie und sagt über das Fahrzeug nichts, was die Leistung nicht zeigt |
| `acVoltage`, `acAmpere` | Fahrzeugseitige Diagnose. Die tatsächliche Leistung steht in `chargingPower`, und über den Ladestrom ist die **Wallbox** die Wahrheit — siehe `Fahrzeug-Wallbox-Zuordnung.md` |
| `avgConsumption` | Verdient eine Historie, hat aber keine Tabelle (kWh/100 km). Aus `Kilometerstand` und geladener Energie ohnehin ableitbar, sobald Punkt 13 die Ladesitzungen führt. Dann neu entscheiden |
| `hvChargingStatus` | Das Wertevokabular ist nirgends dokumentiert (belegt ist nur `HV_CHARGING`). Ein Code-Mapping auf Verdacht wäre geraten, nicht gemessen |
| `state` | Dasselbe Problem, und der nützliche Teil — fährt / fährt nicht — steht in `moving`, das geschrieben wird |
| `chargingEndTime` | Ein Zeitpunkt in der Zukunft, keine Messung |
| `Zeitpunkt` | Sendezeit des Connectors. Gehört in die Diagnose, nicht in die Messreihe |

---

## Beim Rollout

Die Historie beginnt bei null — seit der Telegraf-Abschaltung hat niemand Fahrzeugdaten
geschrieben, und die alten Reihen lagen in InfluxDB 2.

Beim Start abonniert der DataHub `daten/Fahrzeug/+/Status` und bekommt sofort die drei retained
Nachrichten. Die werden mit ihrer **eigenen, alten Messzeit** geschrieben. In der Datenbank
entstehen dadurch **rückwirkend einzelne Punkte** — pro Fahrzeug einer, an dem Tag, an dem das
Fahrzeug zuletzt gemessen hat. Für den BMW kann das Wochen zurückliegen.

Das ist gewollt und sieht in Grafana entsprechend aus: ein einzelner Punkt weit links, dann
eine Lücke, dann ab dem ersten Fahrzeugereignis die eigentliche Reihe. Ein Liniendiagramm
verbindet die beiden und suggeriert einen Verlauf, den es nicht gibt — für Fahrzeugreihen
deshalb Punkte statt Linien anzeigen, oder das Zeitfenster eng genug wählen.

Ein Neustart des DataHubs wiederholt das **nicht**: Dieselben retained Nachrichten ergeben
dieselben Punkte.
