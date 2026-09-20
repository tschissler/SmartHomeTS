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
3. **Positive Meldung bei zwei belegten Boxen** — ist der einen Box bereits ein Fahrzeug
   zugeordnet und die andere ebenfalls belegt, geht das neu meldende Fahrzeug an die
   andere Box. Das ist **kein Ausschluss über Fahrzeugschweigen**, sondern über die
   *gemessene* Belegung der Boxen, und damit zulässig. Ist noch keine der beiden Boxen
   vergeben, bleibt es bei Stufe 4. Vertrauensgrad `erkannt`.
4. **Historie** — das Fahrzeug, das zuletzt an dieser Box lud.
   Vertrauensgrad `vermutet`.
5. Nichts davon: `unbekannt`.

**Eigenschaften:**

- **Klebend.** Eine Zuordnung gilt bis zum Ende der Wallbox-Sitzung (Stecker gezogen).
  Sie wird nur *aufgewertet* (`vermutet` → `erkannt` → `bestätigt`), nie stillschweigend
  umgeworfen.
- **Override endet mit der Sitzung.** Die nächste Sitzung beginnt wieder mit Automatik
  plus Historie-Vermutung. Kein vergessener Dauerzustand, der einen Fahrzeugwechsel
  überlebt.
- **Vermutungen sind als solche sichtbar** — grau mit Fragezeichen, ein Klick genügt zur
  Korrektur.

### Präzisierungen aus der Umsetzung (2026-09-20)

Sechs Punkte, die das Konzept offen ließ und die die Regel entscheiden musste. Sie schärfen
das Leitprinzip, sie weichen es nicht auf.

- **Stufe 2 und Stufe 3 sind dieselbe Bedingung.** Genau eine belegte Box ist noch nicht
  sicher vergeben, und genau ein positiv meldendes Fahrzeug ist noch nicht platziert. Bei
  einer belegten Box ist das Stufe 2, bei zwei belegten und einer sicher vergebenen Stufe 3.
  Ein Fall im Code weniger, dieselbe Aussage.
- **„Sicher vergeben" heißt `erkannt` oder `bestätigt`, nie `vermutet`.** Über eine Vermutung
  auf die andere Box zu schließen hieße, aus einer Vermutung ein `erkannt` zu machen — genau
  die stille Falschaussage, gegen die die Stufen gebaut sind.
- **Eine positive Fahrzeugmeldung muss zur laufenden Sitzung passen können.** Ein
  `chargerConnected = true` sagt nichts darüber, *wann*. Ist der `SitzungsBeginn` der Box
  vertrauenswürdig (selbst beobachtete Steckerflanke), muss die Messzeit des Fahrzeugs
  (`lastUpdate`, nicht `Zeitpunkt`) nach dem Sitzungsbeginn liegen, mit 5 Minuten Toleranz
  für Uhrenversatz. Stammt der Beginn aus der Box-Uhr (`timeQ: 0`), tritt ein Altersfenster
  von 6 Stunden an seine Stelle. Beides lässt die späte VW-Meldung durch — 15 Minuten
  Abrufintervall plus rund 60 Minuten Datenalter — und weist die 35 Tage alte BMW-Meldung
  vom 2026-09-20 ab. **Das ist keine Frischeprüfung an der Zustellung**, sondern am
  Messzeitpunkt im Payload; die Zustellzeit ist bei retained Nachrichten bedeutungslos.
- **Mehrdeutigkeit führt nie zu einer Aussage.** Melden zwei nicht platzierte Fahrzeuge
  gleichzeitig, fällt die Regel auf die Historie zurück. Eines der beiden lädt auswärts, und
  welches, ist nicht entscheidbar.
- **Eine Box, deren Status veraltet ist (> 5 min), fällt ganz heraus.** Ihre Zuordnung wird
  nicht angefasst — ein fehlender Connector ist kein gezogener Stecker. Und solange
  *irgendeine* Box stumm ist, unterbleibt der Schluss über die Belegung: ihre Belegung ist
  unbekannt, und „dann muss es die andere sein" wäre wieder ein Ausschluss, nur über eine
  schweigende Box statt über ein schweigendes Auto.
- **Beim Start wird 15 Sekunden lang nichts publiziert.** In dieser Zeit treffen die retained
  Nachrichten ein. Wer früher publiziert, schreibt ein frisches `unbekannt` über genau das
  Topic, aus dem die laufende Zuordnung wiederhergestellt werden soll. Dieselbe Begründung
  wie bei den Zählern der Energieaufteilung.

Der `Zeitpunkt` im Payload steht still, solange die Aussage sich nicht ändert — er liest sich
als „zugeordnet seit", nicht als „zuletzt ausgewertet". Ob die RulesEngine lebt, beantwortet
ihr Heartbeat, nicht dieses Feld.

**Woher die Historie kommt:** aus dem eigenen retained `Zuordnung`-Topic. Es bleibt nach
dem Sitzungsende stehen und trägt die alte `SitzungsId`. Beginnt an derselben Box eine
neue Sitzung, übernimmt die Regel das dort genannte Fahrzeug als `vermutet` und schreibt
die neue `SitzungsId` dazu. Kein Datenbankzugriff, und ein Neustart der RulesEngine stellt
den Zustand allein aus retained Topics wieder her.

**Der manuelle Override** wird von der Web-Oberfläche retained nach
`konfiguration/Laden/M3/<Box>/Zuordnung` publiziert und trägt `SitzungsId`, `Fahrzeug` und
`Zeitpunkt`. Die Regel wertet ihn **nur aus, wenn die `SitzungsId` zur laufenden Sitzung
passt** — dadurch verfällt er beim Stecker-Ziehen von selbst, ohne dass ihn jemand
zurücknehmen müsste.

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

- Boxstatus aus `DeviceState` der Keba: frei / verbunden, wartet / lädt / unterbrochen /
  Fehler. **Der `PlugStatus` allein sagt nicht, ob die Box frei ist:** 0, 1 und 3 heißen
  alle „kein Fahrzeug" — 1 und 3 nur „Kabel steckt in der Box". Die Garage hat ein fest
  angeschlagenes Kabel und meldet deshalb im Leerlauf dauerhaft `PlugStatus` 3. Ein
  Fahrzeug hängt erst ab 5 dran (5 = nicht verriegelt, 7 = verriegelt, nur 7 lädt).
  Text, Farbe und Symbol der Kachel folgen alle dieser einen Lesart; eine
  Aufmerksamkeitsfarbe trägt nur eine Box, an der etwas hängt oder etwas klemmt.
- Fahrzeugname mit Vertrauensgrad, klickbar zur Korrektur. Die Zeile erscheint **nur, solange
  eine Sitzung läuft**: das retained `Zuordnung`-Topic steht nach dem Ausstecken weiter — es
  ist die Historie — und auf einer freien Box läse sich derselbe Name als „dieses Auto lädt
  hier". Die Kachel vergleicht dafür die `SitzungsId` der Zuordnung mit der der Box, genau wie
  die Regel es tut.
- Zeichen, Wortlaut und Farbe der Zeile beantworten **eine** Frage — wie sicher ist das?
  `bestätigt` und `erkannt` bekommen ein `✓` in normaler Schrift, `vermutet` und „kein
  Fahrzeug erkannt" ein `?` in Grau und kursiv. Ein graues Fragezeichen neben einem Namen in
  normaler Schrift wäre der Widerspruch, den Punkt 9b aus der Zustandszeile entfernt hat.
- **Freigabe und Priorität gehören zur Box**, nicht zum Fahrzeug. Achtung bei der
  Umsetzung: `InsideChargingEnabled` und `OutsideChargingEnabled` sind zwei unabhängige
  Schalter, `PreferedChargingStation` dagegen **ein einzelnes Enum** — die beiden
  Prio-Schalter verhalten sich also wie Optionsfelder und schließen einander aus.
- Die Ladestufen 0–5 bleiben als globale Auswahl oberhalb der Kacheln; sie gelten für die
  Anlage, nicht für eine Box.

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
