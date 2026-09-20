# Ladeprotokoll und Energieaufteilung

Wann hat welches Fahrzeug wie viel geladen, und woher kam die Energie.
Ergänzt `Fahrzeug-Wallbox-Zuordnung.md` (wer lädt) um die Frage, womit geladen wurde.

## Die Rückschau lebt in Grafana

Die Web-PWA zeigt den **aktuellen** Zustand und verlinkt auf das Dashboard. Das tote
Sitzungs-Grid in `ChargingOverview.razor` entfällt ersatzlos — samt `CarSelection`-Filter,
Excel- und PDF-Export. Zeitraumwahl, Aggregation, Tabellen und Export kann Grafana ohne
eigenen Code, und dort liegt bereits die Auswertungsebene des Systems.

## Zurechnungsregel: anteilig am Quellenmix

Jeder Verbraucher bezieht zu jedem Zeitpunkt aus PV, Batterie und Netz im selben
Verhältnis wie das gesamte Haus. Die Anteile aller Verbraucher addieren sich damit exakt
auf den Gesamtverbrauch.

**Der Nenner ist der Verbrauch, nicht die Erzeugung.** Das ist die entscheidende Feinheit:
Sobald eingespeist wird oder die Batterie lädt, ist ein Teil der PV-Erzeugung gar nicht im
Haus. Mit roher PV-Leistung im Nenner summierten sich die Anteile nicht auf 100 %. Der
PV-Anteil wird deshalb als **Rest aus der Energiebilanz** gezogen:

```
N = max(0, PowerFromGrid)        Netzbezug
B = max(0, PowerFromBattery)     Batterieentladung
V = PowerToHouse                 Gesamtverbrauch inkl. Wallboxen
P = max(0, V − N − B)            PV-Anteil am Verbrauch

p = P/V     n = N/V     b = B/V          (p + n + b = 1)

je Wallbox:  PV = p · P_Box    Netz = n · P_Box    Batterie = b · P_Box
```

Vorzeichen laut `grafana-dashboards/influxdb-reference.md`: `PowerFromGrid` positiv =
Bezug, `PowerFromBattery` positiv = Entladen. Alle vier Größen liegen im
`ChargingSituation`-Objekt nebeneinander.

Laden beide Boxen gleichzeitig, bekommt jede denselben Mix — bei dieser Regel genau richtig.

**Achtung Einheiten.** Die XML-Kommentare in `ChargingSituation` behaupten bei
`PowerFromPV`, `PowerFromGrid` und `PowerFromBattery` „in mW", ebenso
`ChargingGetData.CurrentChargingPower`. Tatsächlich führen die Felder **Watt** — der
Enphase-Connector liefert Milliwatt, und `Program.cs` teilt beim Einlesen durch 1000. Da
die Zurechnung alle vier Größen miteinander verrechnet, sind diese Kommentare vor der
Umsetzung zu korrigieren.

### Verworfene Alternativen

| Regel | Warum nicht |
|---|---|
| **Auto ist Grenzverbraucher** (Netzbezug zuerst dem Auto zurechnen) | Rechnet dem Auto Netzbezug zu, der nachweislich von anderen Verbrauchern stammt, und ist gegenüber der Grundlast willkürlich. |
| **Regler-Überschuss als Maßstab** (`CalculateRawAvailablePower`) | Rechnet die Batterieladung zurück und schreibt dem Auto PV gut, die tatsächlich in die Hausbatterie ging. Für die Regelentscheidung richtig, für die Bilanz zu großzügig. |
| **Batterie pauschal als PV zählen** | Stille Näherung, im Winter spürbar daneben, nachträglich nicht mehr auftrennbar. |
| **Batterie rekursiv auflösen** | Physikalisch am ehrlichsten, braucht aber einen zweiten Zustandszähler mit eigener Neustart-Problematik und eine Annahme zur Entladereihenfolge. Aus drei getrennten Töpfen später jederzeit nachrüstbar. |

Die Batterie bleibt ein **eigener dritter Topf**. Aus drei Töpfen lässt sich jede spätere
Deutung noch im Dashboard herstellen; umgekehrt geht es nicht.

## Drei virtuelle Zähler je Wallbox

Die Aufteilung wird **nicht** nachträglich aus der Datenbank berechnet. Sie ist eine
zeitgewichtete Zurechnung über den gesamten Ladeverlauf; eine Query, die
5-Sekunden-Leistungswerte gegen den Quellenmix integriert, wird fragil und bei jeder
Datenlücke still falsch.

Stattdessen behandelt der ChargingController die Aufteilung wie **drei virtuelle Zähler je
Wallbox** — fortlaufend, nie zurückgesetzt, genau wie ein Shelly oder der Envoy:

```
energy_values,  sensor_type = Wallbox

  device      measurement
  ──────────────────────────────────────
  Garage      EnergieLadungPv
  Garage      EnergieLadungBatterie
  Garage      EnergieLadungNetz
  Garage      EnergieGesamt          (bereits vorhanden, von der Box)
  Stellplatz  … dito
```

Das hat vier Vorteile:

- Es gilt das etablierte Abfragemuster `MAX(value_cumulated_kwh) − MIN(...)`, das die
  Schema-Referenz ausdrücklich empfiehlt.
- **Zeitraumsummen kommen nie aus der Sitzungstabelle.** „Wie viel PV-Ladung im Februar"
  ist ein MAX-minus-MIN über den Februar — unabhängig davon, wie Sitzungen über die
  Monatsgrenze laufen. Die Monatsgrenzen-Frage stellt sich damit gar nicht.
- **Ausfallzeiten sind sichtbar statt kaschiert.** Läuft der Controller nicht, während die
  Wallbox autonom weiterlädt, zählen die Zähler nicht mit. Die Differenz zur Boxenergie
  ist echte Information über die Datenqualität, kein zu versteckender Fehler.
- Ein Neustart stellt den Stand aus dem eigenen retained Topic wieder her.

**Umsetzungshinweise**
- Δt ist die tatsächlich vergangene Zeit seit dem letzten Zyklus, nicht pauschal 5 s.
- Ist `V ≤ 0` oder fehlt ein Eingangswert, wird das Intervall übersprungen. Es erscheint
  dann als nicht zugeordnete Energie — das ist gewollt.
- Auch die momentanen Aufteilungsleistungen gehören nach `power_values`
  (`LadeleistungPv`/`-Batterie`/`-Netz` je Gerät), damit das Verlaufsdiagramm ohne
  Ableitung auskommt.

## Tabelle `ladesitzungen`

Eine Sitzung ist ein Datensatz mit Attributen, keine Messung. Sie bekommt deshalb eine
eigene Tabelle; `WritePointDataToInfluxDb` im `Influx3Connector` ist bereits generisch.

| Spalte | Art | Inhalt |
|---|---|---|
| `time` | Zeitstempel | Sitzungsbeginn |
| `wallbox` | **Tag** | `Garage`, `Stellplatz` |
| `sitzungs_id` | Feld | ID der Keba-Sitzung |
| `fahrzeug` | Feld | `BMW`, `Mini`, `VW`, `-` |
| `vertrauen` | Feld | `bestaetigt`, `erkannt`, `vermutet`, `unbekannt` |
| `ende` | Feld | Zeitstempel des Aussteckens |
| `dauer_s` | Feld | angesteckt |
| `ladezeit_s` | Feld | tatsächlich geladen (Leistung > 0) |
| `energie_kwh` | Feld | Gesamtenergie laut Box |
| `energie_pv_kwh` | Feld | aus Zählerdifferenz |
| `energie_batterie_kwh` | Feld | aus Zählerdifferenz |
| `energie_netz_kwh` | Feld | aus Zählerdifferenz |
| `energie_unzugeordnet_kwh` | Feld | Boxenergie minus Summe der drei |

**Nur `wallbox` ist Tag.** `fahrzeug` und `vertrauen` sind Felder — nicht um Korrekturen zu
ermöglichen, sondern wegen **Idempotenz**: Das `Ladesitzung`-Topic ist retained, jeder
DataHub-Neustart liest es erneut. Als Feld überschreibt ein wiederholter Schreibvorgang
denselben Punkt; als Tag entstünde bei abweichendem Inhalt eine zweite Zeile. Zusätzlich
prüft der DataHub, ob die `SitzungsId` bereits geschrieben wurde — analog zu
`LastChargingSessionPublishedViaMQTT` im KebaConnector.

**Steckdauer und Ladezeit werden getrennt geführt.** Ein Fahrzeug hängt über Nacht an der
Box und lädt nur zwei Stunden bei Überschuss; die Keba meldet trotzdem eine einzige
Sitzung über zwölf Stunden. Ohne die Trennung sähe jede Überschussladung nach absurd
niedriger Durchschnittsleistung aus. Sitzungen ohne Ladung (0 kWh) werden geschrieben, im
Dashboard aber standardmäßig ausgeblendet.

## Zuständigkeiten

Der Sitzungsdatensatz hat drei Beitragende, deshalb drei Topics mit der `SitzungsId` als
Verbindungsschlüssel. Jeder Dienst publiziert nur, was er selbst weiß:

```
KebaConnector        daten/Laden/M3/<Box>/Status
                       PlugStatus, DeviceState, SitzungsId, E pres, E total

ChargingController   daten/Laden/M3/<Box>/Ladesitzung
                       SitzungsId, Beginn, Ende, Zustand (laufend|beendet),
                       Zählerstände Pv/Batterie/Netz, Ladezeit

RulesEngine          daten/Laden/M3/<Box>/Zuordnung
                       SitzungsId, Fahrzeug, Vertrauen

DataHub              -> Tabelle ladesitzungen
```

Der ChargingController erkennt Sitzungsbeginn und -ende im 5-Sekunden-Takt und hält die
Zählerstände exakt fest. Der DataHub führt beim Schreiben zusammen und **schreibt nur,
wenn die `SitzungsId` beider Quellen übereinstimmt**; andernfalls wird nichts geschrieben
und der Fall protokolliert. Damit ist die Sorge vor auseinanderlaufenden Topics
ausgeräumt, statt sie durch einen gemeinsamen Autor zu umgehen.

## Dashboard

Ein Dashboard `Laden`, das `wallbox-charging-dashboard.json` **ablöst** — dessen beide
Leistungskurven wandern als Verlaufsteil hinein, damit nicht zwei Wallbox-Dashboards
nebeneinander stehen.

| Zeile | Inhalt |
|---|---|
| Kennzahlen | kWh gesamt im Zeitraum, PV-Anteil in Prozent, Anzahl Sitzungen |
| Protokoll | Tabelle: Beginn, Wallbox, Fahrzeug, Vertrauen, Steckdauer, Ladezeit, kWh, davon PV/Batterie/Netz, PV-Anteil |
| Bilanz | Balken kWh je Fahrzeug und Monat, gestapelt nach Quelle |
| Verlauf | Ladeleistung gestapelt nach Quelle |

Variablen: Fahrzeug, Wallbox, und ein Schalter „nur sichere Zuordnungen"
(`vertrauen IN ('bestaetigt','erkannt')`).

## Bewusste Einschränkungen

- **Keine nachträgliche Korrektur.** Der Datensatz entsteht beim Sitzungsende. Bei jeder
  Ladung über ~15 Minuten hat sich auch der VW bis dahin gemeldet; nur sehr kurze
  VW-Ladungen landen dauerhaft mit Vermutung oder als `unbekannt` im Protokoll.
- **Lücken werden ausgewiesen, nicht hochgerechnet.** `energie_unzugeordnet_kwh` bleibt
  stehen, statt die drei Töpfe auf die Boxenergie hochzuskalieren — Hochrechnen wäre eine
  stille Annahme über einen Zeitraum, in dem nichts gemessen wurde.
- **Die Batterie wird nicht rekursiv aufgelöst.** Ihre Entladung ist ein eigener Topf.
