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

S = P + N + B                    Summe der drei Toepfe
p = P/S     n = N/S     b = B/S          (p + n + b = 1)

je Wallbox:  PV = p · P_Box    Netz = n · P_Box    Batterie = b · P_Box
```

**Normiert wird über `S`, nicht über `V`.** Ohne Messwert-Schieflage ist `S = V`, das
Ergebnis also identisch. Weichen die Envoy-Kanäle aber kurzzeitig voneinander ab — etwa
`V = 3000`, `N = 2000`, `B = 2000` — dann wird `P = 0`, und mit `V` als Nenner ergäbe
`n + b = 1,33`: dem Auto würden 133 % seiner Ladeleistung zugerechnet und die Zähler
liefen dauerhaft zu hoch. Mit `S` als Nenner ist `p + n + b = 1` konstruktiv garantiert.

**Der Mix ist der von M3.** Der ChargingController abonniert ausschließlich
`data/electricity/envoym3` (`Program.cs:283`), und dort hängen auch beide Wallboxen.
M1-Werte gehen in die Zurechnung nicht ein.

Vorzeichen laut `docs/influxdb-reference.md` im Repo `forgejo.intern/thomas/Grafana`:
`PowerFromGrid` positiv =
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
- Ist `S ≤ 0` oder fehlt ein Eingangswert, wird das Intervall übersprungen. Es erscheint
  dann als nicht zugeordnete Energie — das ist gewollt.
- `ladezeit_s` zählt Intervalle mit einer Ladeleistung **über 100 W**, nicht „> 0" — die
  Wallbox meldet im Leerlauf kleine Messrauschwerte, die sonst als Ladezeit gälten.
- Auch die momentanen Aufteilungsleistungen gehören nach `power_values`
  (`LadeleistungPv`/`-Batterie`/`-Netz` je Gerät), damit das Verlaufsdiagramm ohne
  Ableitung auskommt.

### Stand der Umsetzung (Punkt 12)

Der ChargingController publiziert je Box retained auf
`daten/Laden/M3/<Box>/Energieaufteilung` (`LadeTopics.Energieaufteilung`), Payload
`SharedContracts.Energieaufteilung` mit den drei Zählerständen, den drei momentanen
Aufteilungsleistungen und `LadezeitSekunden`. Er **abonniert dieses Topic selbst** — der
retained Payload ist die Sicherung der Zählerstände über einen Neustart hinweg.

Drei Schwellen, die in der Regel nicht stehen, aber die Buchführung tragen:

| Schwelle | Wert | Wofür |
|---|---|---|
| Wiederherstellungsfrist | 15 s | So lange zählt der Controller nach dem Start **nicht**, sondern wartet auf seinen eigenen retained Stand. Ohne sie publiziert ein Rollout eine frische Null, bevor der Broker den alten Wert nachgeliefert hat |
| Maximales Intervall | 30 s | Δt darüber wird nicht zugerechnet. Ein längeres Intervall ist ein Controller-Ausfall, und ihn mit der jetzt gemessenen Leistung zu verbuchen erfände Energie |
| Alter der Envoy-Werte | 30 s | Ältere Messwerte gelten als fehlend, das Intervall wird übersprungen. Der Regelkreis arbeitet davon unberührt weiter wie bisher |

`HouseConsumptionPower` in `ChargingSituation` **ist** der Envoy-Kanal `PowerToHouse` von
M3, nur in Watt statt Milliwatt (`ChargingController/Program.cs`, Zweig
`data/electricity/envoym3`). Die beiden Namen in dieser Regel meinen also dieselbe Größe.

Die Ladezeit wird auch dann gezählt, wenn der Mix unbekannt ist: die Ladeleistung ist
gemessen, die Zurechnung gerechnet. Ein gemessenes Faktum wegzulassen, weil ein
gerechnetes fehlt, machte die Ladezeit still zu kurz.

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
| `energie_unzugeordnet_kwh` | Feld | Boxenergie minus Summe der drei; **darf leicht negativ werden** (Rundung, Zählerskew) und wird nicht geklemmt — ein kleiner negativer Wert ist ehrlicher als eine stille Korrektur |

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

### Stand der Umsetzung (Punkt 13a)

Der ChargingController publiziert je Box retained auf
`daten/Laden/M3/<Box>/Ladesitzung` (`LadeTopics.Ladesitzung`), Payload
`SharedContracts.Ladesitzung`. Er **abonniert dieses Topic selbst** — aus demselben Grund
wie bei der Energieaufteilung: ohne den retained Stand zerschnitte jeder Rollout eine
laufende Ladung in zwei Sitzungen. Dieselbe Wiederherstellungsfrist von 15 s gilt.

**Der Payload trägt Sitzungswerte, keine Zählerstände.** Die drei virtuellen Zähler laufen
unverändert in der `Energieaufteilung`; was eine Sitzung verbraucht hat, ist die Differenz
zwischen zwei ihrer Stände. Der Controller bildet sie selbst, weil er der einzige Dienst
ist, der die Sitzungsgrenzen im 5-Sekunden-Takt sieht. Ein Leser des Topics braucht damit
kein zweites Topic und keine Fensterrechnung.

**Woher der Beginn kommt — in dieser Reihenfolge:**

| Quelle | Bedingung | `BeginnGeschaetzt` |
|---|---|---|
| Die vom KebaConnector beobachtete Steckflanke | `SitzungsBeginnAusBoxZeit == false` | `false` |
| Die Boxuhr | nur bei einer geerbten Sitzung, und nur wenn der Wert plausibel ist (nicht in der Zukunft, nicht älter als 14 Tage) | `true` |
| Die erste eigene Sichtung des Controllers | sonst | `false`, wenn er den Übergang selbst gesehen hat; `true`, wenn er die Sitzung geerbt hat |

`WallboxStatus.SitzungsBeginnAusBoxZeit` heißt das Gegenteil dessen, was der Name
nahelegt: `true` bedeutet, der Wert **stammt** aus der Boxuhr — und die meldet `"timeQ": 0`
und darf beliebig falsch sein. Das neue Feld `BeginnGeschaetzt` sagt stattdessen das, was
ein Konsument braucht: ob die Startzeit gemessen ist oder nicht.

**Sitzungsende.** Der Controller schließt die Sitzung in dem Zyklus, in dem die Box keine
oder eine andere `SitzungsId` meldet; das Intervall, das gerade vergangen ist, gehört noch
der endenden Sitzung. Hat er das Ende verschlafen (Ausfall über die Steckdauer hinaus),
wird als Ende der `Zeitpunkt` des retained Payloads eingetragen — der letzte Moment, in
dem überhaupt jemand von der Sitzung wusste. „Jetzt" einzutragen erfände eine Steckdauer,
die niemand beobachtet hat.

**Die Boxenergie wird im letzten laufenden Zyklus festgehalten**, nicht aus der Meldung
gelesen, die das Ende anzeigt: Die Keba zählt `E pres` je Sitzung und setzt den Wert für
die nächste zurück.

Der DataHub abonniert `daten/Laden/+/+/Ladesitzung` und `daten/Laden/+/+/Zuordnung`,
führt sie in `SmartHome.DataHub.Ladesitzungen` zusammen und schreibt die Tabelle. Beide
Topics sind retained und kommen in beliebiger Reihenfolge, deshalb ist **jede** eingehende
Nachricht ein neuer Versuch — von welcher Seite sie auch kommt.

**Drei Festlegungen, die die Spaltenliste oben nicht trifft:**

1. **`ende` ist ein String-Feld im Format ISO 8601 (UTC).** InfluxDB hat genau eine
   Zeitspalte, und die trägt den Sitzungsbeginn. Ein zweiter Zeitstempel kann nur Feld
   sein, und ein Feld ist entweder Zahl oder Text; der lesbare Text gewinnt, weil das
   Rechnen ohnehin `dauer_s` daneben erledigt.
2. **`BeginnGeschaetzt` wird nicht geschrieben.** Es wäre eine ehrliche Spalte, steht aber
   nicht in der Liste, und die Modellierungsregel sagt: nicht selbst entscheiden.
   Nachträglich ein Feld zu ergänzen ist billig — **offene Frage an Thomas.**
3. **Nur Sitzungen aus `M3` werden geschrieben.** Die Tabelle hat `wallbox` als einzigen
   Tag und keine Ortsspalte; eine gleichnamige Box in einem anderen Gebäude fiele sonst
   still in dieselbe Reihe. Heute hängen beide Boxen in M3, es wird also nichts
   abgewiesen, was es gibt.

**Der Zyklus-Versatz aus Punkt 12 spielt für die Sitzungsenergie keine Rolle.** Die
Zurechnung zum Zeitpunkt `T` verbucht die Ladeleistung von `T − 5 s`, der Zählerstand ist
also der Stand von `T − 5 s`. Die Sitzungsenergie ist eine *Differenz* zweier solcher
Stände, und der Versatz steckt in beiden Enden gleich:

```
Sitzung = M(t_ende) − M(t_beginn) ≈ E(t_ende − 5 s) − E(t_beginn − 5 s)
        = Energie über [t_beginn − 5 s, t_ende − 5 s]
```

Das Fenster ist um 5 s verschoben, nicht verkürzt. In beiden Verschiebungsfenstern lädt
die Box nicht — vor dem Einstecken kann sie nicht, und nach dem Ladeende bis zum
Ausstecken tut sie es nicht mehr —, also ist die Differenz exakt die wahre
Sitzungsenergie. Auch die Ladezeit ist eine Differenz und verhält sich genauso.

Ein messbarer Rest bleibt nur, wenn **unter voller Last ausgesteckt** wird: dann fehlen
bis zu 5 s × P, bei 11 kW also 0,015 kWh. Im selben Zug fehlen sie aber auch der
Boxenergie, die ebenfalls im letzten laufenden Zyklus abgegriffen wird — in
`energie_unzugeordnet_kwh` heben sie sich damit weitgehend auf.

Die −6,1 % aus dem Befund zu Punkt 12 sind kein Widerspruch: Dort endete das
Auswertungsfenster **mitten in einer Ladung**, sodass nur die Anfangsflanke gezählt wurde.
Pro Sitzung heben Anfangs- und Endflanke einander auf. Daraus folgt für Punkt 14: Die
Gegenprobe „Summe der drei = Boxenergie" gehört **je Sitzung** gestellt, nicht über einen
frei gewählten Zeitraum — über einen Zeitraum misst man die Flanken mit.

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
