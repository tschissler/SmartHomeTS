# Backlog: Laden, Fahrzeugzuordnung und Topic-Aufräumung

Abzuarbeiten in dieser Reihenfolge. Jeder Punkt ist für sich abgeschlossen und lässt das
System lauffähig zurück. Erledigte Punkte werden abgehakt, nicht gelöscht — die
Reihenfolge soll nachvollziehbar bleiben.

Konzept und Begründungen: `Fahrzeug-Wallbox-Zuordnung.md`, `Ladeprotokoll.md`,
`MQTT-Topic-Konvention.md`.

---

## Bearbeitungsstand

Wer arbeitet gerade woran. **Nur auf `main` pflegen, nie im Feature-Branch** — sonst wird
diese Tabelle selbst zum Merge-Konflikt. Arbeits-Sessions fassen sie nicht an; die
Integrator-Session trägt ein und aus.

**Name.** Session, Worktree und Branch eines Punktes tragen denselben Namen
`laden-<NN>-<kurz>`. Der Branch heißt `worktree-<Name>`, weil `EnterWorktree` dieses
Präfix setzt.

| Punkt | Name | seit | Stand |
|---|---|---|---|
| 4 Flutter stilllegen | `laden-04-flutter` | 2026-09-20 | **erledigt und gemergt** (`a12202b`), kein Rollout |
| 16 Ladestrom-Retain | `laden-16-ladestrom` | 2026-09-20 | **erledigt, ausgerollt und verifiziert** (`c46f54f`) |
| 2 + 18 Health & Secret | `laden-02-health` | 2026-09-20 | **erledigt, ausgerollt und verifiziert** (`1c8ff95`). Im neuen Pod: `Loaded tokens … (issued …)`, `Stored token refreshed 0.9 h ago, well inside the 7 day limit`, sofortiger Refresh mit erfolgreicher Persistierung, `Connected to the BMW broker — vehicle is ready.` je Fahrzeug, Readiness `True` |
| 19 CI-Tests | `laden-19-ci-tests` | 2026-09-20 | **erledigt und gemergt** (`ef244fe`). Ab jetzt läuft `dotnet test` vor jedem ChargingController-Build; ein roter Test erzeugt kein Image und damit kein Deployment |
| 7 + 8 Topic-Schnitt | `laden-0708-schnitt` | 2026-09-20 | **erledigt und gemergt** (`04cc432`). 26 Dateien, ChargingControllerTests 46/46 und KebaConnectorTests 17/17 grün ohne einen geänderten Erwartungswert. Löst **sechs** Rollouts aus, nicht vier: Enphase und Shelly bauen wegen `SharedContracts/**` mit — der transitive Pfadfilter aus Punkt 0 wirkt wie vorgesehen. Rollout-Handgriffe siehe unten |
| 9 Wallbox-Kacheln | `laden-09-kacheln` | 2026-09-20 | **erledigt, ausgerollt und verifiziert** (`5c23df9`, Image `1.0.172`). Nachkontrolle am Broker: sechs Klicks → sechs retained Nachrichten, eins zu eins. Der Doppelklick erzeugt zwei (zwei Klicks = zweimal umschalten, beide Zustände gewollt); die frühere dritte Phantomnachricht mit `Outside=true` **und** `Prefered=2`, die niemand angeklickt hatte, ist weg. Die Regelung lief während des Tests unbeirrt weiter (Stellplatz 8932 → 10294 → 10907 mA, PV-geführt) |
| 12 Energieaufteilung | `laden-12-energieaufteilung` | 2026-09-20 | **erledigt, ausgerollt und verifiziert** (`44c0e80`, ChargingController `1.0.50`). Die Zurechnung selbst trifft auf 0,1 W, über alle drei Mischungsarten. **Aber: ein Zyklus Versatz**, siehe den Befund unten — meine erste Messung hatte ihn unbemerkt herausgekürzt |
| 10 Fahrzeugdaten | `laden-10-fahrzeugdaten` | 2026-09-20 | **erledigt und gemergt** (`0206430`). 18 Dateien, BMWConnector 56/56 und ChargingController 94/94 grün (selbst nachgelaufen). Neun bisher verworfene Felder kommen an, **alle** Werte nullbar statt nur `Battery`, Altersanzeige aus `lastUpdate` statt `Zeitpunkt`. `BMW_OUTPUT_TOPIC` im Deployment nicht gesetzt — der Connector meldet sein Ausgabetopic jetzt bei jedem Start und warnt bei einem Override. Entsperrt 11 und 17 |
| 11 Zuordnung | `laden-11-zuordnung` | 2026-09-20 | **erledigt und gemergt** (`3e6bd68`). RulesEngineTests 64/64, ChargingControllerTests 94/94 (selbst nachgelaufen). Zwei Präzisierungen über den Auftrag hinaus: Eine Fahrzeugmeldung zählt nur, wenn ihre **Messzeit** zur Sitzung passen kann (sonst wäre die 35 Tage alte BMW-Meldung Evidenz über jetzt), und der Dienst schweigt 15 s nach dem Start, statt sich mit einem frischen `unbekannt` das eigene Gedächtnis zu überschreiben. **Entsperrt 13**. Nachkontrolle 13:02: RulesEngine `1.0.10` läuft, Konfigurationszeile bestätigt die Parameter (Boxzustand 5 min, Fahrzeugmeldung 6 h, Wiederanlauf-Fenster 15 s). **Funktional noch nicht beweisbar**: Beide Boxen stehen auf `SitzungsId: null`, kein Fahrzeug steckt. Die Regel lässt eine Box ohne Sitzung bewusst aus dem Ergebnis fallen (`Lage()` gibt `null` zurück), also steht auf `daten/Laden/+/+/Zuordnung` korrekt nichts. Der Beweis kommt beim nächsten Einstecken — **offen** |
| 17 Fahrzeugdaten persistieren | `laden-17-fahrzeugdaten-persistieren` | 2026-09-20 | **erledigt und gemergt** (`352fc13`). 36/36 Tests grün — **der DataHub hatte bisher gar keine**. Zeitstempel aus `lastUpdate`; die Entdopplung fällt daraus ab, weil der Primärschlüssel Tabelle + Tags + Zeit ist. Zwei neue Tabellen `distance_values` und `position_values`, weil `counter_values` über `Convert.ToInt16` schreibt und der Mini bei 38954 km steht — Int16 endet bei 32767, das hätte eine `OverflowException` geworfen. **13 kann jetzt folgen**. **Nachkontrolle 13:05 am lebenden Objekt bestanden** — siehe unten |
| 9b Steckerzustand | `laden-09b-steckerzustand` | 2026-09-20 | **erledigt und gemergt** (`b560217`). Der gemeldete Fehler war der Text; die Ursache lag tiefer — `disconnected.svg` ist fest `#d4aa00`, die freie Box trug also dauerhaft eine Aufmerksamkeitsfarbe, und `connected.svg`/`connectednotready.svg` sind geometrisch identisch und unterscheiden sich nur im Strich. Symbol jetzt inline in `currentColor`: Geometrie sagt Fahrzeug ja/nein, Farbe kommt aus dem Zustand |
| 20 Quellenmix-Verlauf | `grafana-wallbox-verlauf` | 2026-09-20 | **erledigt und live** — Dashboard `laden-quellenmix` im Grafana-Repo, importiert und visuell geprüft. Lieferte nebenbei den Versatz-Befund zu Punkt 12 und `import_dashboards.py`. Ernstester Anzeigefehler war die **leere Box**: Sie skalierte automatisch auf 0…100 W, wodurch Rauschen wie Ladung aussah, und das Gegenprobe-Panel hatte die Nulllinie am unteren Rand — ein Ausschlag nach unten wäre unsichtbar gewesen. Behoben |
| 13a Ladesitzungen | `laden-13a-ladesitzungen` | 2026-09-20 | **erledigt und gemergt** (`a6d9c30`). ChargingController 115/115, DataHub 65/65 (selbst nachgelaufen), kein bestehender Erwartungswert angefasst. Statt eines booleschen `beginn_geschaetzt` die Spalte **`beginn_quelle`** mit vier Werten: `steckflanke` und `regelzyklus` sind gemessen, `boxuhr` hat einen Fehler ohne Vorzeichen, `dienstanlauf` einen mit — dann ist `dauer_s` zu kurz. Ein Ja/Nein hätte die letzten beiden ununterscheidbar gemacht. **Zwei** Int16-Verengungen entfernt; die zweite (`Int16.Parse` beim Einlesen der CanGateway-Werte) wird tatsächlich erreicht. **Acht** Builds, nicht sechs — meine Zahl war falsch, die Session hat die `paths:`-Blöcke durchgezählt. **Entsperrt 14** |
| 21 Ladeseite oben | `laden-21-ladeseite-oben` | 2026-09-20 | **erledigt und gemergt** (`22d4240`). Build 0 Fehler, 44 Warnungen alle vorbestehend (selbst geprüft). Der einzige echte Fehler war der Zyklus-Versatz: Die abgelesene Ladeleistung kommt jetzt aus `WallboxStatus`, identisch mit der Kachel. **Den Hausverbrauch hat die Session bewusst nicht umgestellt** und meinen Vorschlag widerlegt — gemischt stünde beim Anfahren auf 7 kW kurzzeitig „Haus: −6.000 W“ da. Ihre Regel: Eine abgelesene Zahl kommt aus der frischesten Quelle, ein Balken, der eine Summe aufteilt, vollständig aus einem Schnappschuss. Farben als CSS-Variablen an einer Stelle, Boxnamen aus `LadeTopics`, Navigation heißt jetzt „Laden“ |
| 13b Ladeliste entfernen | `laden-13b-ladeliste-entfernen` | 2026-09-20 | **läuft** — Abriss: Sitzungs-Grid, Monatswähler, Fahrzeugfilter und Export aus `ChargingOverview.razor`, `ChargingSessionService.cs` (enthält nur auskommentierten Code) und die DI-Zeile. Der `ChargingSession`-Record wird gelöscht, **wenn** die Session selbst bestätigt, dass er frei ist — das kostet acht Builds. **Kein Link aufs Dashboard**, das gibt es erst mit Punkt 14. Disjunkt zu Punkt 3, der im Web nur `Devices.razor` anfasst. **Zusatzauftrag am 2026-09-20:** Quellen und Verbräuche zurück zur dichten Anordnung, siehe die Nachbesserung zu Punkt 21 unten |
| 3 Service-Heartbeat | `laden-03-heartbeat` | 2026-09-20 | **läuft** — fünf Dienste publizieren auf `status/Cluster/<Typ>/<Name>`, `Devices.razor` zeigt sie ohne Sonderbehandlung. Gefahrenstellen im Auftrag genannt: Der Heartbeat ist **retained** und `DeviceStatus.Age` misst die Empfangszeit (bewusst, wegen der NTP-Offsets der Firmwares) — für Dienste mit korrekter Uhr gilt der Grund nicht. Ein Helfer in `Libs/` baut ohne Eintrag in die `paths:`-Blöcke nichts neu. Und der **VWConnector ist Python**, den erreicht kein .NET-Helfer |
| Grafana-Konvention | `grafana-konvention` | 2026-09-20 | **erledigt und abgenommen** — sieben weitere Commits bis `1cb4386`. Endstand **28 Dashboards** (`energiefluss-copy` gelöscht), Abgleich zweimal hintereinander 0 Unterschiede. Ordnerverteilung von mir unabhängig aus den `folderUid`-Feldern nachgerechnet: Infrastruktur 9, Energie 7, Wärme 5, Klima 2, Laden 2, Wasser 1, Provisioned 2 — deckungsgleich. Der kritische Umbenenn-Push (`6f889aa`) war **vorher gemessen, nicht angenommen**: 29 zu 29, null inhaltliche Abweichung, Trockenlauf 0 Ordnerwechsel. `docs/influxdb-reference.md` kennt jetzt die vier Fahrzeugtabellen — inklusive `sub_category` je Measurement, ein Fund der Session in meinen eigenen Messdaten: `Ladeziel` steht auf `Soll`, `SteckerVerbunden` auf `Verbindungsstatus`, wer das nicht weiß verliert still zwei von sechs Reihen. Endstand `fa682ed`, aus einem eigenen Klon gegen den Server geprüft |
| Grafana-Action | `grafana-action` | 2026-09-20 | **erledigt** — zwei Forgejo Workflows live. Ein Push auf `dashboards/` schreibt ~20 s später nach Grafana, nur die geänderten Dateien. Abgleich doppelt geprüft (lokal und aus dem Runner): 29 Dashboards, 0 Unterschiede. **Der Editor-Test wurde von Thomas abgelehnt**, siehe unten |
| Grafana-Repo | `grafana-dashboards` | 2026-09-20 | erledigt — `forgejo.intern/thomas/Grafana`, Export-Skript über die API, 28 Dashboards statt 6. Siehe unten |
| 0 CI-Trigger | `laden-00-ci-trigger` | 2026-09-20 | **erledigt und gemergt** (`d95d3f5`); sieben Rollouts ausgelöst |
| 1 BMW-Token | `laden-01-bmw-token` | 2026-09-20 | **erledigt, ausgerollt und vollständig verifiziert** (`d5b829b`). Nach Thomas' Fahrt am 2026-09-20 lieferte der BMW frisch auf `daten/Fahrzeug/BMW/Status`: `lastUpdate` 12:50:42, `Zeitpunkt` 12:50:44 — **zwei Sekunden Verzug**. Kilometerstand 1958 → 2086, Ladestand 83 → 79. Damit ist die Anbindung Ende zu Ende bewiesen, nicht nur verbunden. Zum Vergleich im selben Moment der VW: 30 Minuten zwischen Mess- und Sendezeit — beide gesund, und genau deshalb führt Punkt 10 beide Zeiten. Von den neun neuen Feldern kommt beim BMW nur `maxEnergy` an; die übrigen sind überwiegend ladebezogen und beim nächsten Ladevorgang nachzuprüfen, bevor man sie als „liefert er nicht" abhakt |

**Reihenfolge entschieden und umgesetzt: 19 lag vor 7/8.** Der Schnitt aus Punkt 7/8
muss damit durch die 46 Szenariotests des ChargingControllers, bevor er ein Image
erzeugt. Das ist beabsichtigt: Bei einer Suite, die auf 1 Watt Abweichung mit 27 roten
Tests reagiert, wird der Umbau Erwartungswerte anfassen müssen — jetzt fällt das vor dem
Rollout auf und nicht danach. **Für `laden-0708-schnitt` heißt das: Die Tests sind ab
sofort Teil der Abnahme.** Geänderte Erwartungswerte gehören begründet in den Commit,
nicht stillschweigend angepasst.

**Entschieden (2026-09-20): drei Grafana-Dashboards werden vom Schnitt blind, die Session
repariert sie im selben Commit.**
Der DataHub zieht `Device` jetzt aus dem Topic-Pfad und schreibt damit `Garage` /
`Stellplatz` statt `KebaGarage` / `KebaOutside`. `Location` bleibt `M3`, `sensor_type`
bleibt `Wallbox` — es bricht ausschließlich der `device`-Filter. Betroffen sind
`wallbox-charging-dashboard.json` (4× je Name, wird von Punkt 14 ohnehin abgelöst),
`energy-overview-dashboard.json` (3× je Name) und `energy-sankey-dashboard.json`
(2× je Name). Die beiden letzten bleiben dauerhaft kaputt, wenn sie niemand anfasst.

Umgesetzt wird: in den Queries auf beide Werte filtern
(`device IN ('KebaGarage','Garage')`). Das überbrückt den Tag-Wechsel in der Abfrage,
hält die Historie sichtbar und braucht keine Datenmigration. Für die Sankey-Kennzahl
`MAX(value_cumulated_kwh) - MIN(...)` ist es sogar die einzig richtige Variante: Der
Zählerstand läuft über den Umbenennungszeitpunkt hinweg durch, die Differenz stimmt nur,
wenn beide Tag-Werte in derselben Abfrage liegen.

Zwei Ergänzungen aus der Nachprüfung, die im Vorschlag der Session fehlten und
mit beauftragt sind:
1. **Der Sankey wird generiert.** `gen_sankey.py` erzeugt `energy-sankey-dashboard.json`;
   die Gerätenamen stehen in Zeile 536/537/545/546. Eine Korrektur nur im JSON wäre beim
   nächsten Lauf des Skripts wieder weg — sie muss ins Skript.
2. **Dashboards werden manuell importiert**, sie liegen nicht im ArgoCD-Pfad
   (`grafana-dashboards/README.md`). Ein Merge repariert Grafana also nicht; der Import
   ist ein eigener Handgriff nach dem Rollout. Umgekehrt löst ein Merge dieser Dateien
   auch keinen Build aus — `grafana-dashboards/` steht in keinem Pfadfilter.

Die Delta-Berechnung im DataHub ist vom Namenswechsel **nicht** betroffen: `previousValues`
ist ein In-Memory-Dictionary, das jeder Pod-Neustart ohnehin leert. Die erste Messung unter
dem neuen `MeasurementId` liefert Delta 0, genau wie nach jedem Neustart — kein Sprung.

**Rollout-Ablauf für Punkt 7/8, wenn der Merge freigegeben wird.** Der Schnitt ist hart:
Zwischen dem Merge und dem Ende der drei Rollouts sprechen alte und neue Dienste
verschiedene Topics. Reihenfolge und Handgriffe:

1. **Merge.** Löst Builds für KebaConnector, ChargingController, DataHub und Web aus,
   danach je ein ArgoCD-Rollout binnen ~2 min. `grafana-dashboards/` steht in keinem
   Pfadfilter und baut nichts.
2. **Während der Rollouts regelt der Controller nicht.** Das ist die gewollte
   Rückfallebene: Der KebaConnector gibt die Boxen nach `StaleReleaseAfter` auf vollen
   Strom frei. Gemessen ist dieses Fenster etwa eine Minute — nicht zehn, wie eine frühere
   Annahme in diesem Dokument behauptete.
3. **Einstellungen anstoßen: Ladestufe in der Oberfläche einmal neu klicken.**
   So entschieden am 2026-09-20 — kein Broker-Eingriff. Web publiziert die Einstellungen
   dann selbst retained auf `konfiguration/Laden/M3/Regelung/Einstellungen`, inklusive
   `Zeitpunkt`; der Controller nimmt sie auf, sobald er sie sieht.

   **Der Klick zählt erst, wenn der Web-Pod das neue Image fährt.** Klickt man früher,
   publiziert das alte Web auf das alte Topic und es passiert nichts Sichtbares — der
   Controller regelt weiter nicht, und die Ursache sieht aus wie ein Fehler im Schnitt.
   Erkennbar am `build: automatic update of smarthomeweb`-Commit im Deployments-Repo
   oder schlicht daran, dass die Oberfläche einmal neu geladen hat.

   (Die verworfene Alternative wäre gewesen, den retained Payload von
   `config/charging/settings` direkt auf das neue Topic umzukopieren. Er wäre unverändert
   gültig — das neue Pflichtfeld `Zeitpunkt` fehlt darin, was der Controller verträgt.)
4. **Alte retained Topics leeren** (leere retained Nachricht): `commands/charging/KebaGarage`,
   `commands/charging/KebaOutside`, `data/charging/situation`, `config/charging/settings`.
   Das ist der einzige verbliebene Broker-Eingriff und **nicht eilig** — solange der neue
   Stand läuft, abonniert diese Topics niemand mehr. Er schützt allein den Rollback-Fall:
   Fällt man auf die alten Images zurück, würde sonst ein uraltes Kommando wiederbelebt.
   Wer ihn aufschiebt, sollte das wissen, bevor er zurückrollt.
5. **Dashboards importieren.** Manuell in Grafana, sie liegen nicht im ArgoCD-Pfad.
6. **Verifikation im Connector-Log:** Bei jedem Kommando muss das Alter aus dem
   `Zeitpunkt` im Payload gezogen werden. Das löst zugleich das Prüfkriterium aus Punkt 16
   ab — `Received retained message` taugt nicht mehr, weil das Retain-Flag für die
   Altersbestimmung keine Rolle mehr spielt.

**Achtung beim Dashboard-Import: Die JSONs im Repo sind womöglich nicht das, was in
Grafana läuft.** Grafana speichert Dashboards in seiner PVC; im Repo liegen manuelle
Exporte, deren letzter Commit und deren Dateidaten von April bis Juli 2026 stammen. Wer
die korrigierten Dateien importiert, überschreibt damit möglicherweise Monate an
UI-Änderungen mit einem Aprilstand. **Empfehlung für diesen Rollout: die Filterstellen
stattdessen im Grafana-UI von Hand nachtragen** — drei Dashboards, `device = 'X'` wird zu
`device IN ('X','Y')`. Der Commit der Session bleibt trotzdem wertvoll: Er hält fest,
welche Stellen es sind.

**Generator nachgezogen und verifiziert (2026-09-20).** `grafana-dashboards/gen_sankey.py`
ist gitignored und lag deshalb nicht im Worktree der Session; der Patch ist in der
Hauptarbeitskopie angewendet. Neu sind `WALLBOX_ALTNAMEN` und `device_filter()`, die den
IN-Filter an einer Stelle erzeugen statt an vier Aufrufstellen. Zusätzlich war der
Ausgabepfad tot: Das Skript schrieb nach `/home/thomas/grafana-dashboards/`, ein Ordner,
den es nicht mehr gibt — es lief also in einen `FileNotFoundError`, statt die Datei zu
erzeugen, die es erzeugen soll. Der Pfad zeigt jetzt neben das Skript. Gegenprobe: Der
gepatchte Generator erzeugt das gemergte `energy-sankey-dashboard.json` byteidentisch
(`git status` nach dem Lauf sauber).

**Nachkontrolle zu Punkt 7/8 bestanden (2026-09-20, nach dem Klick auf die Ladestufe).**
Gemessen am produktiven Broker mit einem frischen Client und in InfluxDB:

- Alle vier neuen Topics sind da und retained — sie kamen beim Subscribe sofort.
  Jeder Payload trägt `Zeitpunkt`, wie die Konvention es verlangt. In
  `daten/Laden/M3/<Box>/Status` stehen `SollstromMa` und `SitzungsBeginnAusBoxZeit`,
  und Letzteres trägt tatsächlich Information: Stellplatz meldet `true` bei gesetztem
  `SitzungsBeginn`, Garage `false` bei `null`.
- Die Regelung läuft: `befehle/Laden/M3/<Box>/Ladestrom` wird publiziert,
  `daten/Laden/M3/Regelung/Situation` im Sekundentakt fortgeschrieben.
- **Der Tag-Wechsel in InfluxDB ist sauber geschnitten.** `KebaGarage`/`KebaOutside`
  enden um 09:44:49 UTC, `Garage`/`Stellplatz` setzen dort ein — das ist exakt der
  DataHub-Rollout. Keine Lücke, keine Überlappung.
- **Der IN-Filter ist messbar nötig, nicht nur theoretisch.** Für den Stellplatz über
  die letzten drei Stunden: `KebaOutside` läuft von 16824,164 auf 16827,895 kWh,
  `Stellplatz` steht seitdem konstant bei 16827,895. Die Sankey-Kennzahl mit
  IN-Filter liefert **3,731 kWh**; nur auf den neuen Namen gefiltert wären es
  **0,000 kWh**. Genau die 3,73 kWh wären verloren gewesen.
- **Die vier alten retained Topics sind geleert** (2026-09-20, auf Freigabe von Thomas):
  `commands/charging/KebaGarage`, `commands/charging/KebaOutside`,
  `data/charging/situation`, `config/charging/settings`. Vorher gesichert;
  `commands/charging/KebaOutside` trug bis zuletzt `{"ChargingCurrent":6000}` — ein
  Rückfall auf die alten Images hätte die Box sofort auf 6 A gezogen. Gegenprobe: 20 s
  beobachtet, keines der Topics kehrt zurück, es publiziert dort also niemand mehr.
  **Damit ist der Rollback auf die Stände vor `04cc432` keine Rückfallebene mehr** — die
  alten Dienste fänden ihre Konfiguration nicht vor. Vorwärts reparieren, nicht zurück.
- Der Burst von drei `Einstellungen`-Publikationen innerhalb von 124 ms war **kein
  Fehler**, sondern Thomas beim Klicken. Gegenprobe: 30 s ohne Bedienung, keine
  einzige weitere Publikation.

Merges nach `main` gibt ausschließlich Thomas frei: jeder Merge ist über den ArgoCD Image
Updater binnen ~2 min ein Deployment ins laufende System.

---

## Parallel arbeiten

Die Punkte 7→8→9 und 10→11→13 hängen in Ketten; echte Parallelität gibt es nur an wenigen
Stellen. Sinnvoll sind 2–3 gleichzeitige Sessions, jede in einem eigenen git worktree, mit
einem Branch pro Punkt.

### Wellen

| Welle | gleichzeitig möglich | Anmerkung |
|---|---|---|
| A | 0 ∥ 4 ∥ 16 ∥ (1 → 2+18) | 1 braucht Thomas interaktiv (BMW-Login). 1, 2 und 18 liegen alle im `BMWConnector` und sind **nicht** parallel: 2 und 18 fassen beide den Secret-Store an und gehören in **eine** Session, nach 1 |
| B | 3 | braucht 2 (gemeinsamer Begriff von „gesund") und 0 (sonst baut der Helfer in `Libs/` nichts neu). Kollidiert mit fast allem — vorziehen und zügig abschließen oder bis nach 13 zurückstellen, nicht mittendrin einschieben |
| C | 7 + 8 zusammen in **einer** Session | harter Schnitt, muss ein Release sein: 7 definiert den Contract, den 8 konsumiert |
| D | 9 ∥ 12 | Web gegen ChargingController, disjunkt — sobald die Payload-Verträge aus C stehen. 12 erst nach 8, sonst treffen sich zwei Sessions im ChargingController |
| E | 10 → 11 → 13a → 17 seriell | 14 hängt nicht an 17 und kann davor oder danach; 15 setzt 17 zwingend voraus |
| F | **13a ∥ 21** | disjunkt seit der Teilung von 13: 13a fasst kein Web an, 21 nur das Web. Danach 14, erst dann 13b |

### Datei-Kollisionen

Die Abhängigkeiten bei den Punkten sagen, was fachlich aufeinander aufbaut. Wer mehrere
Punkte gleichzeitig bearbeitet, braucht zusätzlich diese Tabelle:

| Bereich | angefasst von Punkt |
|---|---|
| `SharedContracts` | 7, 10, 12, 13a |
| `BMWConnector` | 1, 2, 18 — bei 2 und 18 dieselbe Secret-Store-Klasse |
| `ChargingController` | 8, 12 |
| `KebaConnector` | 7, 16 |
| `SmartHome.DataHub` | 8, 13a, 17 |
| `SmartHome.Web` | 9, 10, 11, 13b, 21, 23 — und 3 (`Devices.razor`) |
| alle fünf Dienste + `Devices.razor` | 3 |
| `.github/workflows/` | 0, 4 — disjunkte Dateien: 0 ändert die Service-Workflows, 4 löscht die beiden App-Workflows |

### Regeln

- **Ein Punkt, eine Session.** Kein Punkt wird von zwei Sessions gleichzeitig angefasst.
- **Ein Name für alles.** Ein Punkt bekommt einen Namen `laden-<NN>-<kurz>` und trägt ihn
  überall: die Session wird mit `claude -n <Name>` gestartet, `EnterWorktree` legt den
  Worktree unter diesem Namen an, der Branch heißt `worktree-<Name>`. Ohne `-n` vergibt der
  CLI eine Nummer, die niemandem sagt, woran die Session arbeitet. Die Integrator-Session
  heißt `laden-integrator`.
- **Diese Datei** bearbeitet ausschließlich die Integrator-Session.
- **Kein Merge nach `main` ohne Freigabe von Thomas.** Jeder Merge ist über den ArgoCD
  Image Updater binnen ~2 min ein Deployment ins laufende System.
- **Geteilter Code baut nichts neu**, solange Punkt 0 offen ist: Änderungen an
  `SharedContracts`, `MQTTClient`, `SmartHomeHelpers` oder `Libs/` lösen keinen
  Service-Build aus — ohne Fehler, nur ohne Deployment.
- **Nummern bleiben stabil.** Neue Erkenntnisse werden hinten angehängt, nie eingeschoben —
  laufende Sessions verweisen auf diese Nummern. Die Nummerierung zeigt deshalb die
  Entstehungsreihenfolge, die Arbeitsreihenfolge steht in der Wellentabelle.
- **Im Zweifel gewinnen die Konzeptdokumente** (`Ladeprotokoll.md`,
  `Fahrzeug-Wallbox-Zuordnung.md`, `MQTT-Topic-Konvention.md`) gegenüber diesem Backlog:
  der Backlog wurde nicht immer nachgezogen, wenn ein Konzept präzisiert wurde.

---

## Unabhängige Vorarbeiten

### 0. CI-Trigger für geteilten Code

**Problem.** Kein Service-Workflow hat einen Pfadfilter auf den geteilten Code, den er
referenziert. Eine Änderung an `SharedContracts` (von 7 Projekten referenziert),
`MQTTClient` oder `SmartHomeHelpers` (je 4) baut **kein** Image neu — es gibt keinen
Fehler, nur ein stilles Nicht-Deployment. Einzige Ausnahme ist `RulesEngine.yml`.
`SharedContracts` fehlt dort zu Recht: RulesEngine referenziert es weder direkt noch
transitiv.

`MQTTClient` referenziert selbst `SmartHomeHelpers` — wer `MQTTClient` nutzt, hängt
deshalb auch daran. Die Spalte nennt den **transitiv aufgelösten** Stand:

| Service | referenziert | Filter vor Punkt 0 |
|---|---|---|
| ChargingController | SharedContracts | keiner |
| KebaConnector | SharedContracts, MQTTClient, Libs/HelpersLib, SmartHomeHelpers¹ | keiner |
| ShellyConnector | SharedContracts, MQTTClient, SmartHomeHelpers | keiner |
| EnphaseConnector | SharedContracts, MQTTClient, SmartHomeHelpers | keiner |
| SmartHome.DataHub | SharedContracts, MQTTClient, SmartHomeHelpers¹ | keiner |
| SmartHome.Web | SharedContracts, SmartHomeHelpers | keiner |
| RulesEngine | MQTTClient, SmartHomeHelpers¹ | MQTTClient, SmartHomeHelpers |

¹ transitiv über `MQTTClient`. `BMWConnector` hat gar keine `ProjectReference`,
`VWConnector` ist Python — beide brauchen keinen Filter.

**Warum zuerst.** Punkt 7/8 legt die neuen Payload-Typen nach `SharedContracts`, Punkt 3
den Heartbeat-Helfer nach `Libs/`, Punkt 12 ändert die Einheitenkommentare in
`ChargingSituation`. In allen drei Fällen wäre der harte Schnitt halb ausgerollt, ohne
dass es auffällt.

**Umfang**
- [x] Je Service-Workflow die tatsächlich referenzierten Pfade in `paths:` ergänzen
      (Quelle: die `ProjectReference`-Einträge der `.csproj`)
- [x] ~~`SmartHomeHelpers` aus `RulesEngine.yml` entfernen~~ — **gegenstandslos, nicht
      aufräumen:** RulesEngine referenziert es transitiv über `MQTTClient`
- [x] `.claude/worktrees/` in `.gitignore` aufnehmen — Worktrees liegen im Repo und
      erscheinen sonst als untracked

**Erledigt am 2026-09-20**, gemergt als `d95d3f5`. Nachgewiesen über das Matching der
`paths:`-Muster gegen Testpfade in `SharedContracts`, `MQTTClient`, `SmartHomeHelpers` und
`Libs/HelpersLib`; der Ende-zu-Ende-Beweis war ohne Merge nicht führbar. Eine Änderung an
`SharedContracts` erreicht sechs Dienste, eine an `MQTTClient` alle sieben.

**Blockiert** 3, 7, 8, 12.

---

### 1. BMW-Token erneuern und Doku korrigieren

**Problem.** Der BMWConnector liefert seit 2026-08-16 nichts. Der Pod läuft
(`1/1 Running`, 0 Restarts), aber der `refresh_token` ist abgelaufen:
`Token refresh failed (HTTP 400): invalid_request`. Laut `SETUP.md` ist das ein von BMW
vorgegebener ~90-Tage-Zyklus, der ein interaktives Re-Bootstrap erfordert.

**Umfang**
- [x] `dotnet run -- --bootstrap BMW` lokal ausführen (interaktiver BMW-Login, nur Thomas)
- [x] Pod erneuern mit `kubectl -n smarthome delete pod <bmwconnector-pod>` —
      **nicht** `rollout restart`, das patcht das Deployment-Template und gilt ArgoCD
      mit `selfHeal: true` als Drift
- [x] `DATAPOINTS.md`: Die Aussage „`header` — **Mini only**" und „Not available for BMW
      via streaming API" ist falsch. Der BMW liefert `battery` (gemessen: 83 %)
- [x] `SETUP.md`: `rollout restart` durch `delete pod` ersetzen, mit Begründung ArgoCD

**Erledigt am 2026-09-20**, gemergt als `d5b829b`. Belegt: Subscribing-Zeile für BMW im
neuen Pod, keine Auth-Fehler, keine `using environment variable`-Zeile, Secret durchgehend
bei 36 Bytes, `battery: 83` im Payload. **Offen geblieben:** der Nachweis frischer
Publikation — der retained Payload trägt weiterhin `lastUpdate: 2026-08-16`. Der Mini mit
nie defektem Token schweigt identisch, es ist also der ereignisbasierte Stream bei zwei
geparkten Fahrzeugen und kein Token-Symptom.

**Prüfhinweis für künftige Ausfälle.** Ein positives `connected=Mini BMW` gibt es nicht:
der HealthCheck loggt nur auf warn-Level, also ausschließlich bei `Degraded`/`Unhealthy`.
Der Beweis ist das **Ausbleiben** der Degraded-Zeile zusammen mit einer
`Subscribing`-Zeile je Fahrzeug.

**Nicht das Zielbild.** Der Connector publiziert heute `data/charging/<Auto>`
(`VehicleConfig.cs:27`, Default `data/charging/{prefix}`; im Pod ist kein
`BMW_OUTPUT_TOPIC` gesetzt). Das konventionsgerechte `daten/Fahrzeug/BMW/Status`
existiert noch nicht und ist nicht Teil dieses Punktes — siehe den Klärungsvermerk bei
Punkt 17.

---

### 2. Health-Semantik des BMWConnectors korrigieren

**Problem.** Der Ausfall blieb 35 Tage unsichtbar. `HealthRegistry.GetResult()` liefert
bei Teilausfall `Degraded`, und `app.MapHealthChecks("/healthz/ready")` gibt für
`Degraded` genau wie für `Healthy` HTTP 200 zurück. Die Probe war zufrieden, Kubernetes
tat nichts. Umgekehrt sind Liveness und Readiness auf **denselben** Checksatz gemappt —
fielen beide Fahrzeuge aus, würde der Pod neu starten, was einen abgelaufenen Token
nicht repariert.

**Umfang**
- [x] Liveness von der Fahrzeugverbindung entkoppeln: sie prüft nur, ob der Prozess
      bedienbar ist. Ein Neustart repariert keinen Token
- [x] Readiness so mappen, dass `Degraded` nicht als betriebsbereit durchgeht
- [x] Proaktive Warnung, **bevor** der Ausfall eintritt. Maßgeblich ist die Zeit seit dem
      letzten **erfolgreichen Refresh**, nicht das Alter des Logins: Quelle ist der
      `iat`-Claim des aktuellen `id_token`, ersatzweise ein eigener Zeitstempel beim
      Schreiben. Schwelle 7 Tage — die Hälfte der Frist. Konfigurierbar
- [x] Die Alters-Warnung geht **nicht** in die Readiness ein — ein gewarnter, aber
      funktionierender Connector muss betriebsbereit bleiben
- [x] ~~Prüfen, ob dieselbe Probe-Verwechslung in den anderen Services steckt~~ —
      **geprüft, kein Befund.** Die Probe-Pfade aller zehn Deployments wurden gegen den
      Code gehalten: DataHub und Web trennen sauber über Tags (`live`/`ready`);
      ChargingController, Keba, Shelly, Enphase, RulesEngine und VW liefern auf `/healthz`
      ein statisches „alive" und auf `/ready` 503, sobald nicht `Healthy` — `Degraded`
      fällt dort korrekt durch. Der BMWConnector war der einzige mit beiden Proben auf
      demselben Checksatz

**Das Token-Modell, recherchiert am 2026-09-20 in der BMW-CarData-Dokumentation.**
`access_token` und `id_token` gelten je **1 Stunde**, der `refresh_token` **zwei Wochen**
(1 209 600 s). Entscheidend: **der refresh_token rotiert** — jeder Refresh erzeugt ein
neues Set aus allen drei Tokens und setzt deren Frist zurück. Läuft er ab, ist ein neuer
Device-Code-Flow nötig; wird die client_id von ihren Services abgemeldet, verfällt er
sofort. Die „~90 Tage" in `SETUP.md` waren unbelegt und falsch — sie haben die Diagnose in
die Irre geführt. Dass der Mini bei einem 195 Tage alten Login läuft, ist kein
Widerspruch, sondern der Beweis der Rotation.

**Folge für Punkt 18.** Weil der alte refresh_token nach einem Refresh sofort ungültig
ist, ist das Speichern des neuen Tokens der kritische Schritt: Schlägt es fehl, ist der
Zugang unwiederbringlich verloren — ohne sichtbaren Fehler beim Abruf. Das ist die
plausibelste Erklärung für den Ausfall am 2026-08-16.

**Fertig, wenn** ein simulierter Ausfall eines von zwei Fahrzeugen nach außen sichtbar ist.

---

### 3. Service-Heartbeat auf `status/`

**Problem.** Auf `status/#` liegen 17 ESP32-Geräte. Die fünf .NET-Services publizieren
nichts — ein toter Connector ist auf MQTT unsichtbar, und die vorhandene
Geräteüberwachung sieht ihn nicht.

**Umfang**
- [ ] BMWConnector, VWConnector, KebaConnector, ChargingController, DataHub publizieren
      einen Heartbeat im **Bestandsformat der ESP32-Geräte**:
      `status/<Ort>/<Geraetetyp>/<Name>` mit `Ort = Cluster`, weil die Dienste ortslos
      sind (Version, Uptime, letzte erfolgreiche Aktion, fachlicher Zustand) —
      sinnvollerweise als gemeinsamer Helfer in `Libs/`. Begründung der Ausnahme in
      `MQTT-Topic-Konvention.md`
- [ ] `Devices.razor` zeigt sie ohne Sonderbehandlung mit an
- [ ] `meta/<Service>/version` geht darin auf (heute nur `meta/RulesEngine/version`)

**Fertig, wenn** ein gestoppter Connector auf der Geräteseite als stumm erscheint.

**Abhängig von** 2 (gemeinsame Vorstellung davon, was „gesund" heißt).

---

### 4. Flutter-App stilllegen

Die App wird nicht mehr aktiv entwickelt; gepflegt wird die Web-PWA.

**Umfang**
- [ ] `smarthome_app/` nach `Depricated/` verschieben
- [ ] Workflows `Smarthome_app.yml` und `SmartHomeBlazorAppiOS.yml` entfernen
- [ ] `CLAUDE.md`: Flutter-Abschnitt aus Build-Befehlen und Architektur entfernen
- [ ] Notieren, dass `Nachrichten/#` damit keinen Konsumenten mehr hat

**Fertig, wenn** der Topic-Schnitt in Punkt 7/8 nur noch drei Konsumenten im Repo betrifft.

---

### 16. Retained Ladestrom-Kommando hebelt die Notfallfreigabe aus

**Problem.** `PublishChargingCommand` publiziert `befehle/…/Ladestrom` mit
`WithRetainFlag()` (`ChargingController/Program.cs:180`), und der KebaConnector setzt bei
jedem Empfang `desiredCurrentReceivedAt = DateTimeOffset.UtcNow`
(`KebaDeviceConnector.cs:57`). Startet der KebaConnector neu, während der
ChargingController tot ist, stellt der Broker sofort das alte retained Kommando zu — und
der Connector hält es für taufrisch.

Damit greift die eingebaute Notfallfreigabe **nie**: `StaleReleaseAfter` soll die Box nach
10 Minuten Funkstille auf vollen Strom freigeben, damit ohne Regelung weitergeladen werden
kann. Nach einem Connector-Neustart läuft der Alterszähler wieder bei null los, und die
Box bleibt dauerhaft auf einem beliebig alten Sollwert stehen.

**Wie schwer wiegt das?** Kein Gefahrenrisiko, sondern ein Verfügbarkeitsrisiko: Die
Notfallfreigabe existiert, damit ohne Regelung weitergeladen werden kann. Fällt sie aus und
war das letzte Kommando `0 mA`, bleibt die Wallbox **dauerhaft abgeschaltet** — das Auto
lädt nicht, und niemand sieht warum.

Es braucht allerdings **beides gleichzeitig**: Der ChargingController muss tot sein *und*
der KebaConnector danach neu starten. Stirbt nur der Controller, greift die Freigabe
korrekt, weil der Zeitstempel beim letzten echten Empfang stehen bleibt. Der KebaConnector
startet aber bei jedem Deployment neu, die Kombination ist also durchaus erreichbar.

Dafür ist die Sofortmaßnahme klein und ohne Contract-Änderung — ein gutes
Aufwand-Nutzen-Verhältnis.

**Welle A.** Unabhängig von allem anderen; kollidiert nur mit Punkt 7 im KebaConnector, und
der liegt in Welle C. Von den kleinen Punkten der ersten Welle hat dieser den größten
Nutzen pro Zeile.

**Umfang**
- [x] Sofortmaßnahme ohne Contract-Änderung: MQTTnet liefert bei einer Retain-Zustellung
      das Retain-Flag mit. Eine so gekennzeichnete Nachricht darf den Sollwert zwar
      **setzen**, aber den Frischezähler **nicht zurücksetzen**
- [ ] Dauerhaft: `Zeitpunkt` im Kommando-Payload, Alter daraus statt aus der Empfangszeit
      — fällt mit Punkt 7/8 ohnehin an
- [x] Testfall: Controller schweigt, Connector startet neu → Freigabe muss nach
      `StaleReleaseAfter` erfolgen
- [x] ~~Prüfen, ob dieselbe Verwechslung anderswo steckt~~ — **ja, in der `RulesEngine`**
      (`Program.cs:116/127/139` setzen die Empfangszeit, `MixerPositionRule` und
      `CoolingFlowTemperatureRule` bewerten `MaxStatusAge` dagegen). Aber harmloser und
      nicht belegt: der Fail-Safe öffnet den Mischer, was „nur Effizienz kostet, nie
      Komfort", und ob der CAN-Gateway überhaupt retained publiziert, lässt sich aus
      diesem Repo nicht feststellen. Als offener Befund unten erfasst, hier nicht mit
      umgesetzt

**Nach dem Rollout verifiziert, 2026-09-20 09:03 — bestanden.** Im Log des neuen Pods
stehen beim Verbindungsaufbau genau zwei `Received retained message`-Zeilen, eine je
Wallbox, danach ausschließlich `Received message` ohne das Wort. MQTTnet 5.0.1.1416 setzt
das Retain-Flag also korrekt; der Fehlerbericht dotnet/MQTTnet#482 von 2018 ist für diese
Version gegenstandslos. Das retained Kommando für `KebaGarage` lautete dabei
`{"ChargingCurrent":0}` — genau der Fall, vor dem der Punkt schützt.

**Abhängig von** nichts.

---

### 19. Vorhandene Tests in der CI ausführen

**Problem.** `ChargingControllerTests`, `EnphaseLib.Tests` und `ShellyLibTests` existieren,
werden aber von keinem Workflow ausgeführt. Tests, die nie laufen, sind Selbstbetrug — und
der ChargingController, dessen Excel-basierte Szenariotests das Herz der Ladelogik prüfen,
wird gerade in Punkt 7/8 umgebaut.

**Muster vorhanden.** `RulesEngine.yml`, `bmwconnector.yml` und `KebaConnector.yml` führen
bereits `dotnet test` aus, Letzteres als eigener `test`-Job vor dem Build: „A red test must
never produce an image."

**Umfang**
- [x] **Zuerst prüfen, ob die drei Testsuiten überhaupt grün sind.** Ein roter Test
      blockiert nach dem Einbau jedes künftige Deployment des Dienstes. Sind sie rot:
      melden, nicht reparieren und nicht den Job einbauen
- [x] `ChargingController.yml` bekommt den `test`-Job nach dem Muster von
      `KebaConnector.yml`. **`EnphaseConnector.yml` bewusst nicht:** `EnphaseLib.Tests`
      sind Integrationstests gegen die echte Anlage (Login mit `EnphaseUserName`/
      `Password`), auf einem Runner nicht lauffähig und mit Credentials ein Test gegen
      die Produktion
- [x] `ShellyLibTests` geklärt: **verwaist und rot.** `ShellyLib` wird im ganzen Repo nur
      von seinen eigenen Tests referenziert, der ShellyConnector nutzt es nicht; der Test
      pollt zudem Hardware unter einer festen IP. Keine künstliche Zuordnung gebaut.
      Ursprünglicher Auftrag: Es liegt unter `Libs/ShellyLib/` und ist in **keiner**
      Solution. Prüfen, ob der ShellyConnector `ShellyLib` überhaupt referenziert — wenn
      nicht, ist es ein verwaistes Testprojekt und gehört nicht in den Connector-Workflow
- [x] Pfadfilter geprüft, nichts nachzuziehen — das Testprojekt liegt im
      Dienstverzeichnis. Ursprünglich: Liegt ein Testprojekt außerhalb des Dienstverzeichnisses, muss
      der Workflow auch darauf triggern, sonst laufen die Tests bei einer Teständerung nicht

**Fertig, wenn** jeder Dienst mit Testprojekt seine Tests in der CI ausführt und ein
absichtlich roter Test nachweislich kein Image erzeugt.

**Abhängig von** nichts. Berührt nur `.github/workflows/`.

---

### 18. Umgebungsvariablen dürfen das Produktiv-Secret nicht überschreiben

**Problem.** Beim Re-Bootstrap am 2026-09-20 hat der Connector `BMW_CLIENT_ID` und
`BMW_GCID` aus der lokalen Shell-Umgebung gelesen, als vorrangig behandelt und ungefragt
ins Kubernetes-Secret `bmwconnector-credentials` zurückgeschrieben („using environment
variable, saving to Kubernetes Secret…"). Die Shell-Werte waren Platzhalter — je 10 Bytes
statt der 36 einer UUID. Der laufende Pod hielt die korrekten Werte nur noch im Speicher,
die GCID war nirgends rekonstruierbar (im Pod-Log steht ausschließlich die Mini-GCID, der
BMW-Client war seit dem Pod-Start nie verbunden) und musste aus KeePass geholt werden.
`BMWConnector/templates/role.yaml` im Deployments-Repo gibt dem Pod dafür `get`, `update`
und `replace` auf das Secret; eine versionierte Kopie, die ArgoCD wiederherstellen könnte,
gibt es nicht.

**Warum eigener Punkt.** Der abgelaufene Token war nur der Anlass. Die Fehlerquelle ist,
dass eine Entwicklungs-Umgebungsvariable ohne Rückfrage Produktionszustand überschreibt —
und der Bootstrap-Zyklus wiederholt sich in ~90 Tagen.

**Wie die Variablen dorthin kamen.** Sie standen in keiner Datei, sondern in der
systemd-User-Umgebung (`systemctl --user set-environment`), mit `REPLACE_ME` als Wert —
also aus einer unausgefüllten Vorlage. Von dort erbt sie jeder neu gestartete Prozess,
auch jedes neue Terminal; ein grep durch die Dotfiles findet sie nicht, und nach einem
Logout sind sie verschwunden, was den Vorfall unreproduzierbar macht. Das Muster selbst
ist im Repo etabliert und für Firmware-Builds sinnvoll
(`ESP32Firmwares/SMLSensor.Firmware/set_env.fish` schreibt eine `.env` genau so in die
User-Umgebung). Gefährlich ist erst die Kombination: eine global vererbte
Entwicklungsvariable trifft auf einen Dienst, der Env-Vars ins Produktiv-Secret
zurückschreibt.

**Umfang**
- [x] Platzhalterwerte (`REPLACE_ME` und Ähnliches) beim Start erkennen und ablehnen
      statt sie zu verwenden — sie sind das eigentliche Einfallstor
- [x] Vorrang umkehren oder absichern: im Cluster-Betrieb gewinnt das Secret. Eine
      Env-Var darf lokal überschreiben, aber nicht zurückschreiben
- [x] Rückschreiben nur mit Plausibilitätsprüfung (GCID und CLIENT_ID sind UUIDs, also
      Format und Länge prüfbar) und explizitem Opt-in, nicht als Nebenwirkung
- [x] Beim Überschreiben den ersetzten Wert maskiert protokollieren — der Vorfall war nur
      an den Byte-Längen im Secret erkennbar
- [x] ~~Dieselbe Rückschreib-Logik in den anderen Connectoren prüfen~~ — **geprüft,
      kein Befund.** Der BMWConnector ist der einzige Dienst im Repo mit einem
      Kubernetes-Client; VW, Keba, Shelly und Enphase lesen Zugangsdaten nur aus der
      Umgebung und schreiben nichts zurück. Einzige weitere Fundstelle:
      `Depricated/BMWConnector/k8s_utils.py`
- [x] Erwägen, `bmwconnector-credentials` versioniert zu hinterlegen (SealedSecret im
      Deployments-Repo), damit es überhaupt eine Wiederherstellungsquelle gibt

**Zuschnitt, entschieden am 2026-09-20.** Der Punkt umfasst **zwei Hälften**: den Schutz
vor Umgebungsvariablen (falsche Werte) und die Reparatur der Token-Persistenz (verlorene
Werte). Sie bleiben zusammen — beide beheben dasselbe Grundproblem, und eine Trennung
hätte zwei Rollouts desselben Dienstes bedeutet. Die Commits sind thematisch getrennt, ein
einzelner lässt sich zurücknehmen.

**Die Persistenz-Reparatur.** `TokenService.RefreshAsync` ersetzt die Tokens erst im
Speicher, stellt die 50-Minuten-Uhr und persistiert zuletzt — ungeschützt. Ab der Rotation
ist der alte `refresh_token` tot und der neue existiert nur im Prozessspeicher; schlägt
das Schreiben fehl, fängt der häufigste Aufrufer das als
`LogWarning("will retry with existing token")` ab, ein Satz, der zusätzlich sachlich
falsch ist. Zu ändern: erst persistieren, dann übernehmen; Schreibfehler mit Backoff
wiederholen; nach erschöpften Versuchen laut melden, was auf dem Spiel steht; der falsche
Satz entfällt. **Nicht** in die Readiness — ein Connector, der Daten liefert, ist
betriebsbereit.

**Bedingung:** Der **Fehlerpfad** muss getestet sein, nicht nur der Erfolgsfall — was
passiert, wenn `SaveTokensAsync` wirft. Das Risiko ist asymmetrisch: Ein Fehler trifft
genau den Mechanismus, dessen Versagen unsichtbar ist. Ohne diese Tests wird die
Persistenz-Reparatur abgetrennt.

**Nach dem Rollout messbar**, ohne auf einen Ausfall zu warten: Der `iat` im Token-Secret
muss sich alle 50 Minuten bewegen. Bleibt er stehen, während der Dienst weiterläuft, ist
der Defekt da — und der Stale-Monitor aus Punkt 2 schlägt nach sieben Tagen an. Die beiden
Punkte sichern sich gegenseitig ab.

**Fertig, wenn** ein Bootstrap mit gesetzten, falsch formatierten Env-Vars das Secret
nicht mehr verändert.

**Abhängig von** nichts. Berührt `BMWConnector`, kollidiert mit 1 und 2.

**Welle A, gebündelt mit Punkt 2 in derselben Session** — nicht parallel dazu. Die
Berührung ist größer, als die Kollisionstabelle vermuten lässt: Die proaktive
Token-Alters-Warnung aus Punkt 2 liest ebenfalls das Secret, also denselben
`KubernetesSecretStore`, den dieser Punkt umbaut. Und **nach** Punkt 1, damit das
Re-Bootstrap nicht auf halb geändertem Verhalten läuft.

**Abgrenzung beim Umsetzen — nicht überdehnen.** „Nicht zurückschreiben" gilt für die
**Zugangsdaten** (`CLIENT_ID`, `GCID`), nicht für die **Tokens**. Der Dienst schreibt
`id_token`, `access_token` und `refresh_token` bei jedem 50-Minuten-Refresh planmäßig ins
Secret zurück (`SETUP.md`). Wird diese Schreiboperation mit abgeklemmt, überlebt keine
Token-Erneuerung einen Pod-Neustart — und das ist genau der Ausfall, den Punkt 1 gerade
behoben hat.

---

## Konzeption

### 5. `Docs/MQTT-Topic-Konvention.md`

- [x] Regel, Begründung, Schreibregeln, Payload-Regeln, Migrationsstand festgeschrieben

### 6. `Docs/Fahrzeug-Wallbox-Zuordnung.md`

- [x] Konzept festgeschrieben, inklusive des gemessenen Gegenbeispiels vom 2026-09-20,
      das belegt, warum das Ausschlussverfahren nicht taugt

---

## Umsetzung

### 7. KebaConnector: Wallbox als Wahrheit

**Ziel.** Alles, was die Box weiß, wird publiziert — und zwar retained und nach Konvention.

**Umfang**
- [ ] `ChargingGetData` wird zu `WallboxStatus`: `PlugStatus` (roh), `DeviceState` (0–5),
      `SitzungsId`, `SitzungsBeginn`, `EnergieSitzungWh`, `EnergieGesamtWh`,
      `Ladeleistung`, Phasenströme I1/I2/I3, angebotener Strom, `Zeitpunkt`
- [ ] Laufende Sitzung aus **Report 100** lesen (liefert `SessionID` und Startzeit)
- [ ] Neues Topic `daten/Laden/M3/{Garage,Stellplatz}/Status`, retained — **nur `Status`**,
      nicht `Ladesitzung` (siehe Entscheidung unten)
- [ ] `…_ChargingSessionEnded` ersatzlos einstellen. Das Topic hat heute **keinen
      Konsumenten** — weder ChargingController noch DataHub noch Web lesen es, der
      Sitzungs-Service der Web-App ist auskommentiert. Zwischen Punkt 7 und 13 entsteht
      dadurch keine Lücke
- [ ] Wallboxen heißen `Garage` und `Stellplatz` — der Hersteller verschwindet aus den
      Bezeichnern

**Achtung.** In den aufgezeichneten Reports steht bei der laufenden Sitzung `TimeQ: 0`
(„not synced time"). Die Uhr der Box ist also möglicherweise nicht synchronisiert. Der
`SitzungsBeginn` sollte deshalb primär aus der selbst beobachteten Steckerflanke
stammen und nur ersatzweise aus der Box-Zeit.

**Entschieden: `…/Ladesitzung` gehört dem ChargingController** (Punkt 13), der
KebaConnector publiziert hier ausschließlich `Status`. Der Widerspruch war eine Altlast aus
einem früheren Entwurf; `Ladeprotokoll.md` und die Topic-Tabelle in
`Fahrzeug-Wallbox-Zuordnung.md` nennen längst den ChargingController als Autor.

Gründe: Nur er führt die Zähler aus Punkt 12, die in den Payload gehören. Nur er sieht im
5-Sekunden-Takt gleichzeitig Boxzustand und Leistungsmesswerte. Und weil die Box-Uhr laut
`TimeQ: 0` nicht verlässlich ist, stammt der `SitzungsBeginn` ohnehin aus der selbst
beobachteten Steckerflanke — die beobachtet der Controller.

**Folge für diesen Punkt:** `Status` muss `SitzungsId` und `SitzungsBeginn` mitführen,
damit der Controller die Sitzungsgrenzen erkennen kann. Beides steht bereits oben im
Umfang.

**Abhängig von** 5.

---

### 8. ChargingController und DataHub auf die neuen Topics

**Umfang**
- [ ] ChargingController liest die neuen Wallbox-Topics, publiziert
      `daten/Laden/M3/Regelung/Situation` und `befehle/Laden/M3/<Box>/Ladestrom`
- [ ] `konfiguration/Laden/M3/Regelung/Einstellungen` statt `config/charging/settings`
- [ ] DataHub: `location` aus dem Topic-Pfad statt als Literal `"M3"` (`Program.cs:208`)
- [ ] **SmartHome.Web gehört in denselben Schnitt** *(entschieden am 2026-09-20)*: sechs
      Topic-Literale in `ChargingOverview.razor` und `MQTTService.cs`, keine Logikänderung.
      Ohne Web hätte der Schnitt die Ladesteuerung **dauerhaft** abgeschaltet —
      `config/charging/settings` wird ausschließlich von `ChargingOverview.razor:345`
      publiziert, der Controller liest es nur. Ohne Publisher bliebe
      `currentChargingSettings` auf dem Default (`ChargingLevel = 0`, beide Freigaben
      `false`), und beide Boxen bekämen 0 mA bis Punkt 9. Ebenso liest nur
      `MQTTService.cs:172` die `…/Situation`. Ein zusätzlicher Rollout entsteht nicht: Web
      hängt an `SharedContracts` und wird seit Punkt 0 ohnehin mitgebaut
- [ ] **Der Controller publiziert für eine Box kein Kommando, solange er für sie noch nie
      einen Status gesehen hat.** Das ist kein Parallelbetrieb, sondern eine Eigenschaft
      des Zielzustands — die 30-Sekunden-Grace existierte nur, weil der alte Status nicht
      retained war. Ohne das kostet die ungünstige Rollout-Reihenfolge eine Ladepause von
      35–40 s plus einen zusätzlichen Schützzyklus je Box
- [ ] Harter Schnitt, gemeinsam mit 7 ausrollen — kein Parallelbetrieb
- [ ] Telegraf-Altlast prüfen und entfernen: `Kubernetes/microk8s/InfluxDB/` schreibt noch
      gegen InfluxDB 2 und ist im k3s-Cluster nicht mehr deployt

**Das Rollout-Fenster, gemessen am 2026-09-20.** Vom Merge bis zum neuen Pod vergehen
4:46 bis 5:53; die Dienste einer Welle spreizen sich um rund 1,5 Minuten. Da 7/8
`SharedContracts` anfasst, starten alle Workflows mit demselben Push — das Fenster beträgt
also **etwa eine Minute**, die Reihenfolge ist nicht vorhersagbar. `StaleReleaseAfter` (10
Minuten) greift darin **nicht**; es ist die Rückfallebene für einen gescheiterten oder
hängenden Rollout, nicht für diesen Fall.

**Beim Rollout von Hand zu erledigen.** Die Ladeeinstellungen existieren nur als retained
Nachricht im Broker — es gibt keine persistente Quelle, `CommunicateSettings()` läuft nur
bei einem Klick. Der Wert muss einmalig von `config/charging/settings` auf
`konfiguration/Laden/M3/Regelung/Einstellungen` umkopiert werden (`mosquitto_sub -C 1`,
dann `mosquitto_pub -r`), sonst startet die Regelung ohne Einstellungen. Alternative: die
Ladestufe nach dem Rollout einmal in der Oberfläche neu klicken.

**Abhängig von** 4, 7.

---

### 9. Web: Wallbox-Kacheln

**Ziel.** Nach diesem Punkt sind Verbindung, Leistung, Sitzungsenergie und Dauer
zuverlässig — noch ohne jede Fahrzeugzuordnung.

**Umfang**
- [ ] `ChargingOverview.razor`: zwei Wallbox-Kacheln statt drei Fahrzeugkacheln, beide
      immer sichtbar
- [ ] Boxstatus aus `DeviceState`, nicht mehr aus `CarStatusData.ChargerConnected`
- [ ] Leistung, Phasenzahl, Sitzungsenergie, Sitzungsdauer, Sollstrom je Box
- [ ] Freigabe und Priorität je Box — beseitigt die Doppelbelegung von
      `OutsideChargingEnabled` durch Mini und VW
- [ ] `MQTTService`: Wildcard-Abonnement statt der drei fest verdrahteten Cases

**Abhängig von** 8.

---

### 10. Fahrzeugdaten vollständig

**Problem.** Der BMW-Connector publiziert acht Felder, die `CarStatusData` nicht kennt
und `System.Text.Json` deshalb stillschweigend verwirft: `predictedRange`,
`chargingPower`, `maxEnergy`, `chargingMode`, `plugEventId`, `avgConsumption`,
`acVoltage`/`acAmpere`, `hvChargingStatus`. Drei weitere (`position`, `moving`, `state`)
kommen an, werden aber nirgends angezeigt.

**Umfang**
- [ ] `CarStatusData` um die fehlenden Felder erweitern; `Battery` nullbar machen
- [ ] Prüfen, welche dieser Felder im BMW-CarData-Portal überhaupt registriert sind —
      `SETUP.md` listet zwölf, `DATAPOINTS.md` dokumentiert mehr
- [ ] Fahrzeugbereich unter den Wallboxen: eine Karte je Fahrzeug mit allen verfügbaren
      Werten und **Altersanzeige**; veraltete Werte ausgegraut statt versteckt
- [ ] **Die Fahrzeug-Topics auf die Konvention umstellen:** `data/charging/<Auto>` wird zu
      `daten/Fahrzeug/<Auto>/Status`, retained, mit `Zeitpunkt` im Payload

**Warum hier und nicht als eigener Punkt** *(entschieden am 2026-09-20)*. Die Konvention
sagt: migriert wird, wenn ein Bereich ohnehin angefasst wird, nicht auf Vorrat. Genau das
passiert hier — Punkt 9 ersetzt die drei fest verdrahteten Cases im `MQTTService` durch
ein Wildcard-Abonnement, Punkt 10 fasst ohnehin jedes Feld von `CarStatusData` an. Ein
eigener Punkt wäre ein **zweiter harter Schnitt** mit demselben Rollout-Fenster-Problem
wie 7/8, für einen Umbau, der hier ohnehin anfällt.

**Beteiligte.** Schreiber: `BMWConnector` (BMW und Mini, `VehicleConfig.cs:27`) und
`VWConnector` (`vw_mqtt.py:22`). Leser: `ChargingController` (`Program.cs:281-282` — der
Mini fehlt dort heute), `SmartHome.Web/MQTTService.cs:157-172` und der DataHub über sein
`data/charging/#`-Abonnement.

Der VW **bleibt Teilnehmer** und wird mit umgestellt: Die WeConnect-API ist zwar
abgeschaltet, der Zugang läuft aber seit dem 2026-08-29 über den EU Data Act
(`carconnectivity-connector-vw-eu-data-act`, verifiziert). Zu beachten ist etwas anderes:
Der `ChargingController` **abonniert** `data/charging/VW`, hat aber gar keinen Handler
dafür — die Nachricht landet im „Unknown topic"-Zweig, `VWData.cs` ist eine ungenutzte
Record-Definition, und die Ladeentscheidung stützt sich allein auf Enphase- und
Keba-Daten. Diese Subscription kann bei der Umstellung ersatzlos entfallen, statt sie auf
das neue Topic mitzuziehen.

**Abhängig von** 9.

---

### 11. Zuordnung

**Umfang**
- [ ] `RulesEngine/Rules/VehicleAssignmentRule.cs` als reine Funktion mit Unit-Tests
- [ ] Evidenz: manueller Override > positive Fahrzeugmeldung > Historie > unbekannt
- [ ] Klebend je Sitzung, nur Aufwertung, Override endet mit dem Stecker-Ziehen
- [ ] Zuordnung auf ein **eigenes** Topic `daten/Laden/M3/<Box>/Zuordnung`, retained, mit
      `SitzungsId`, `Fahrzeug`, `Vertrauen`, `Zeitpunkt`
- [ ] Web: Vertrauensgrad sichtbar (grau + Fragezeichen bei Vermutung), Korrektur per Klick
- [ ] Zustand nach Neustart aus den retained Topics wiederherstellen

**Entschieden: eigenes Topic.** Auch das war eine Altlast — der Satz stammt aus dem
Entwurf, bevor feststand, dass der Sitzungsdatensatz drei Beitragende hat (Box, Controller,
RulesEngine). Ein retained Topic verträgt genau einen Autor, also publiziert jeder Dienst
nur das Seine, und der DataHub führt über die `SitzungsId` zusammen und **prüft sie**.

Das retained `Zuordnung`-Topic ist zugleich die Quelle der Historie-Vermutung: Es bleibt
nach dem Sitzungsende stehen, und bei der nächsten Sitzung an derselben Box übernimmt die
Regel das dort genannte Fahrzeug als `vermutet`. Siehe `Fahrzeug-Wallbox-Zuordnung.md`.

**Abhängig von** 6, 10.

---

### 12. Energieaufteilung PV / Batterie / Netz

Konzept und Begründungen: `Ladeprotokoll.md`.

**Regel.** Anteilig am Quellenmix, mit dem **Verbrauch** als Nenner (nicht der Erzeugung):

```
N = max(0, PowerFromGrid)   B = max(0, PowerFromBattery)   V = PowerToHouse
P = max(0, V − N − B)       S = P + N + B
p = P/S   n = N/S   b = B/S
je Box: PV = p·P_Box,  Netz = n·P_Box,  Batterie = b·P_Box
```

Normiert wird über `S`, **nicht** über `V` — sonst können die Anteile bei
Messwert-Schieflage in Summe über 100 % liegen. Quelle des Mix ist `envoym3`.

**Modell.** Drei virtuelle, nie zurückgesetzte Zähler je Wallbox, vom ChargingController
im Regelzyklus fortgeschrieben — behandelt wie ein Shelly oder der Envoy.

**Umfang**
- [ ] Einheitenkommentare korrigieren: `ChargingSituation.PowerFromPV`/`PowerFromGrid`/
      `PowerFromBattery` behaupten „mW", führen aber **Watt**. Muss vor der Zurechnung
      stimmen. *(Der frühere vierte Fall `ChargingGetData.CurrentChargingPower` ist mit
      Punkt 7/8 erledigt — er heißt jetzt `WallboxStatus.Ladeleistung` und ist korrekt
      als W dokumentiert.)*
- [ ] Zurechnung als reine, unit-getestete Funktion — inklusive Einspeise-, Misch- und
      Grenzfällen (`V ≤ 0`, fehlende Eingangswerte → Intervall überspringen)
- [ ] ChargingController führt je Box `EnergieLadungPv`, `EnergieLadungBatterie`,
      `EnergieLadungNetz` als kumulierte Zähler; Δt ist die tatsächlich vergangene Zeit
- [ ] Zählerstände retained publizieren; Wiederherstellung nach Neustart aus dem
      eigenen retained Topic
- [ ] Zusätzlich die momentanen Aufteilungsleistungen nach `power_values`
      (`LadeleistungPv`/`-Batterie`/`-Netz`) für das Verlaufsdiagramm
- [ ] Ladezeit getrennt von der Steckdauer mitzählen, Schwelle **> 100 W** (nicht „> 0",
      sonst zählt Messrauschen im Leerlauf als Ladezeit)
- [ ] DataHub schreibt die Zähler wie jeden anderen Energiewert nach `energy_values`

**Fertig, wenn** `MAX(value_cumulated_kwh) − MIN(...)` über einen Tag für die drei Zähler
plausibel zur Boxenergie passt.

**Abhängig von** 8.

---

### 13. Ladesitzungs-Tabelle, Grid aus der Web-UI entfernen

**Geteilt am 2026-09-20 in 13a und 13b.** Der ursprüngliche Umfang entfernte die
Sitzungsliste aus dem Web und verwies „stattdessen auf das Dashboard" — das Dashboard ist
aber Punkt 14 und hängt seinerseits an 13. So gebaut entstünde ein Fenster ohne jede
Sitzungsübersicht, so lang wie die Bauzeit von 14 plus die Wartezeit, bis genug Sitzungen
aufgelaufen sind, damit das Dashboard überhaupt etwas zeigt. **Erst der Ersatz, dann der
Abriss.** Nebeneffekt: 13a und Punkt 21 haben damit keine einzige gemeinsame Datei mehr
und können parallel laufen.

#### 13a. Ladesitzungs-Datensatz erzeugen und schreiben

**Umfang**
- [ ] ChargingController publiziert `daten/Laden/M3/<Box>/Ladesitzung` mit `SitzungsId`,
      Beginn, Ende, `Zustand: laufend | beendet`, Zählerständen und Ladezeit — er ist der
      **einzige** Autor dieses Topics (der KebaConnector publiziert nur `Status`, Punkt 7)
- [x] RulesEngine publiziert `daten/Laden/M3/<Box>/Zuordnung` mit derselben `SitzungsId`
      — **bereits erledigt in Punkt 11**
- [ ] DataHub führt beide zusammen und schreibt die Tabelle `ladesitzungen` — **nur bei
      übereinstimmender `SitzungsId`**, sonst nichts schreiben und protokollieren
- [ ] Nur `wallbox` als Tag; `fahrzeug` und `vertrauen` als Felder (Idempotenz bei
      Retain-Wiedergabe), plus Wächter auf bereits geschriebene `SitzungsId`
- [ ] Sitzungen mit 0 kWh werden geschrieben, aber im Dashboard ausgeblendet

**Fasst an:** `SharedContracts`, `ChargingController`, `SmartHome.DataHub`. **Nicht** das Web.

**Abhängig von** 11, 12 — beide erledigt.

#### 13b. Sitzungs-Grid aus der Web-UI entfernen

**Umfang**
- [ ] Sitzungs-Grid, `CarSelection`-Filter und Excel-/PDF-Export aus
      `ChargingOverview.razor` entfernen, stattdessen Verweis auf das Dashboard
- [ ] `ChargingSessionService.cs` ersatzlos löschen
- [ ] `ChargingSession`-Record in `SharedContracts` mit entfernen, falls dann ungenutzt

**Abhängig von** 13a. **Die Abhängigkeit von 14 ist am 2026-09-20 entfallen** — sie
beruhte auf einer falschen Annahme von mir.

Begründet hatte ich sie mit „erst der Ersatz, dann der Abriss“: Die Liste nicht
entfernen, solange das Dashboard fehlt. Das setzt voraus, dass es etwas zu erhalten gibt.
Nachgesehen am 2026-09-20 — gibt es nicht:

| Stelle | Zustand |
|---|---|
| `@inject IChargingSessionService` (`ChargingOverview.razor` Z. 22) | auskommentiert |
| `builder.Services.AddScoped<IChargingSessionService, …>` (`Program.cs` Z. 76) | auskommentiert |
| `public class ChargingSessionService` | **die ganze Klasse** auskommentiert |
| `ChargingSessions = await …GetChargingSessionsAsync(…)` (Z. 345) | auskommentiert |

`ChargingSessions` bleibt dauerhaft eine leere Liste, `FilteredChargingSessions` damit
auch. Das Grid rendert Kopfzeilen, einen Monatswähler, drei Fahrzeug-Häkchen und eine
Excel-/PDF-Export-Leiste über **null Zeilen**. Es zeigt eine Bedienung, die
funktionsfähig aussieht und nichts tut — das Entfernen verbessert den Zustand sofort,
statt eine Lücke zu reißen.

Der Verweis auf das Dashboard entfällt damit vorerst: Er kann erst gesetzt werden, wenn
Punkt 14 steht. Ein Link auf etwas Ungebautes wäre schlechter als kein Link.

**Zum `ChargingSession`-Record: Er bleibt.** Er wird vom KebaConnector benutzt.

*Hier stand zuvor das Gegenteil, und das war ein Fehler der Integrator-Session.* Die
Behauptung lautete, die Treffer im KebaConnector seien nur `EnergyCurrentChargingSession`,
ein Feldname. Tatsächlich benutzt `KebaDeviceConnector.cs` den Typ dreimal
(Z. 319 `ReadRunningSession`, Z. 327 `ReadReport`, Z. 342 `new ChargingSession {…}`), dazu
`SessionTrackingTests.cs` sechsmal. `SharedContracts/ChargingSession.cs` ist repoweit die
**einzige** Definition.

**Wie der Fehler entstand:** Die Suche fand sechs Dateien; nachgesehen wurden davon nur
zwei (`KebaData.cs`, `KebaDeviceStatusData.cs`). In beiden stand nur der Feldname — und
dieses Ergebnis wurde auf alle sechs verallgemeinert. `KebaDeviceConnector.cs` stand in
der Trefferliste und wurde nie geöffnet.

**Wie die Session es richtig gemacht hat:** nicht gelesen, sondern nachgestellt. Datei
weggeschoben, `dotnet build KebaConnector/KebaConnector.sln` → zweimal `CS0246`, Datei
zurück, Build grün. **Ein Experiment schlägt eine Stichprobe.**

Damit entfällt auch der Acht-Builds-Anlass: 13b ändert nur `SmartHome.Web/**` und löst
genau **einen** Rollout aus.

Kollidiert in `ChargingOverview.razor` mit 21 (erledigt) und 23. **Kann parallel zu Punkt 3
laufen** — der fasst im Web nur `Devices.razor` an.

---

### 20. Wallbox-Verlauf je Box mit Quellen-Mix

**Ziel.** Je Wallbox ein eigenes Verlaufsdiagramm, in dem die Ladeleistung nach ihrer
Herkunft aufgeteilt sichtbar ist: PV, Batterie, Netz. Damit ist an jeder Box ablesbar,
woher der Strom dieser Ladung kam — nicht nur, wie viel es war.

**Warum ein eigener Punkt und nicht Teil von 14.** Punkt 14 nennt bereits „Verlauf:
Ladeleistung gestapelt nach Quelle", aber für das Gesamtdashboard und erst nach der
Sitzungstabelle — er hängt an 13 und damit an 11 und 12. Dieser Verlauf braucht dagegen
**nur Punkt 12**: Sobald der ChargingController `LadeleistungPv`, `LadeleistungBatterie`
und `LadeleistungNetz` je Box nach `power_values` schreibt, ist alles da. Ihn hinter 13 zu
hängen würde ihn ohne fachlichen Grund um zwei Punkte verzögern.

**Umfang**
- [ ] Je Wallbox ein Zeitreihendiagramm, gestapelt nach Quelle (PV / Batterie / Netz),
      Summe der Stapel entspricht der `Ladeleistung` der Box — **diese Gegenprobe
      ausdrücklich prüfen**, sie ist der beste verfügbare Test der Zurechnung aus Punkt 12
- [ ] Beide Boxen immer dargestellt, auch eine ohne laufende Ladung (dieselbe Begründung
      wie bei den Kacheln in Punkt 9: keine Ladung ist ein Betriebszustand)
- [ ] Farbgebung für die drei Quellen einmal festlegen und im ganzen Grafana durchhalten —
      dieselben Farben wie im Sankey, damit man zwischen den Dashboards nicht umlernt
- [ ] Zeitraum über die Dashboard-Variable, keine feste Spanne

**Zu klären bei der Umsetzung.** Ob das in das bestehende Wallbox-Dashboard kommt oder
gleich in den Verlaufsteil von Punkt 14. Sauberer wäre Letzteres — aber nur, wenn 14 in
absehbarer Zeit kommt. Sonst hier einbauen und bei 14 übernehmen statt neu bauen.

**Abhängig von** 12. Nicht von 11, 13 oder 14.

**Fertig, wenn** über eine reale Ladesitzung die drei gestapelten Flächen in Summe der
gemessenen Ladeleistung der Box entsprechen.

---

### 21. Oberer Teil der Ladeseite an die Kacheln angleichen

**Ziel.** Der Bereich über den Wallbox-Kacheln („Quellen und Verbräuche",
„Lade-Einstellungen") stammt aus der Zeit vor Punkt 9 und passt nicht mehr dazu. Er soll
dieselbe Sprache sprechen wie die Kacheln darunter — in Benennung, Farbe und Datenquelle.

**Aufgekommen am 2026-09-20**, nachdem die neuen Kacheln live waren. Der Auftrag ist
Angleichung, nicht Neubau: Die Inhalte des oberen Teils sind richtig und nützlich.

**Vier Brüche, im Code nachgewiesen** (`ChargingOverview.razor`):

1. **Doppelte Überschrift.** Zeile 25 trägt `<h3>Lade-Einstellungen</h3>` als Titel der
   ganzen Seite, Zeile 77 dann `<h4>Lade-Einstellungen</h4>` für einen Abschnitt darin.
   Die Seite enthält sich also selbst. Der `h3` müsste heißen, was die Seite ist.
2. **Zwei Namen für dieselbe Box.** Oben heißt sie „Außen" (Zeile 58), unten in den
   Kacheln „Stellplatz". Punkt 7/8 hat `Stellplatz` als Namen festgelegt
   (`LadeTopics.Stellplatz`); „Außen" ist ein Rest der alten `Outside`-Benennung.
3. **Zwei Farbwelten für dieselben drei Quellen.** Die Legende benutzt `#0dcaf0` für PV,
   `greenyellow` für Batterie, `#f72585` für Netz. Das Quellenmix-Dashboard in Grafana
   (Punkt 20) benutzt die Sankey-Farben — PV gelb, Batterie grün, Netz rot. Wer zwischen
   Weboberfläche und Grafana wechselt, muss umlernen. Genau das wollte Punkt 20 vermeiden,
   und die Angleichung gehört auf die Web-Seite, nicht ins Dashboard: Dort sind die Farben
   über alle Dashboards hinweg einheitlich.
4. **Zwei Quellen für dieselbe Zahl — und sie können auseinanderlaufen.** Der obere Teil
   liest `ChargingSituation.InsideCurrentChargingPower` / `OutsideCurrentChargingPower`,
   die Kacheln lesen `WallboxStatus.Ladeleistung` direkt von der Box. Die
   `ChargingSituation` **hängt einen Regelzyklus hinterher** (siehe den Versatz-Befund zu
   Punkt 12), also zeigen oberer und unterer Teil bei Laständerungen kurzzeitig
   verschiedene Werte für dieselbe Wallbox. Das ist der einzige der vier Punkte, der nicht
   nur Gestaltung ist.

**Umfang**
- [ ] Überschriften entwirren
- [ ] „Außen" → „Stellplatz", und generell die Boxnamen aus `LadeTopics` beziehen statt
      sie zu schreiben
- [ ] Eine Farbfestlegung für PV / Batterie / Netz, gemeinsam mit Grafana, an einer Stelle
- [ ] Ladeleistung je Box aus `WallboxStatus` beziehen, nicht aus `ChargingSituation` —
      dann stimmen oben und unten immer überein
- [ ] Gestalterisch an die Kacheln angleichen (Abstände, Kartenform, Typografie)

#### Nachbesserung am 2026-09-20: die dichte Anordnung war richtig

**Thomas hat die ausgerollte Seite gesehen und sie ist schlechter als vorher.** Der Umbau
wurde freigegeben, ohne dass ihn jemand angesehen hatte: Die Session bekam keine
Screenshots hin (Renderer-Timeouts), meldete das als Einschränkung — und die
Integrator-Session hat trotzdem gemergt. **Geprüft waren Build, Dateiumfang, Farbwerte und
die Hausverbrauchs-Logik; ungeprüft war das Aussehen, und genau darum ging es bei diesem
Punkt.** Das ist der Fehler, nicht der Umbau selbst.

**Was verlorenging, in Thomas' Worten:** *„früher waren die beiden Balken untereinander,
so konnte man Produktion und Verbrauch gut gegenüberstellen.“*

Die beiden Balken sind ein **Vergleichsinstrument**: unmittelbar übereinander, gleich
breit, gleiche Skala. Der Umbau hat eine Überschrift und eine Legende zwischen sie gesetzt
und den Vergleich damit aufgelöst. Dazu wiederholte sich die Seite erneut — auf die
Überschrift „Quellen und Verbräuche“ folgten in der Box noch einmal „QUELLEN“ und
„VERBRÄUCHE“, eine Ebene tiefer derselbe Fehler, den der Punkt oben beheben sollte.

**Wiederhergestellt wird:** beide Balken unmittelbar übereinander, eine Legende statt
zwei, keine Zwischentitel, insgesamt flacher. **Die Substanz bleibt** — Ladeleistung aus
`WallboxStatus`, der Hausverbrauch vollständig aus der `ChargingSituation`, die
Grafana-Farben, die Boxnamen aus `LadeTopics`, der Null-Schutz. Umgesetzt von
`laden-13b-ladeliste-entfernen`, weil die Session ohnehin in dieser Datei arbeitet.

**Die Lehre, allgemein:** Bei einer Umgestaltung ist das Aussehen der Gegenstand. Ist es
nicht geprüft, ist der Punkt nicht geprüft — gleichgültig, wie grün der Build ist. Wenn
eine Session meldet, dass sie das Ergebnis nicht ansehen konnte, gehört vor den Merge ein
Blick von Thomas, nicht danach.

**Nicht enthalten.** Die Kacheln selbst (Punkt 9/9b, fertig und verifiziert) und der
Fahrzeugbereich darunter (Punkt 10). Die Balken und die Batterieanzeige bleiben inhaltlich
wie sie sind.

**Abhängig von** 9, 10, 20 — alle erledigt. Kann jederzeit laufen, kollidiert aber mit
Punkt 11, solange der an der Kachel arbeitet.

---

### 14. Grafana-Dashboard „Laden"

Löst `wallbox-charging-dashboard.json` ab; dessen Leistungskurven wandern als Verlaufsteil
hinein.

**Umfang**
- [ ] Kennzahlen: kWh gesamt im Zeitraum, PV-Anteil in Prozent, Anzahl Sitzungen
- [ ] Protokolltabelle: Beginn, Wallbox, Fahrzeug, Vertrauen, Steckdauer, Ladezeit, kWh,
      davon PV/Batterie/Netz, PV-Anteil
- [ ] Balken kWh je Fahrzeug und Monat, gestapelt nach Quelle
- [ ] Verlauf: Ladeleistung gestapelt nach Quelle — **je Wallbox, siehe Punkt 20.**
      Wird dort schon gebaut, dann hier übernehmen statt neu bauen
- [ ] Variablen: Fahrzeug, Wallbox, Schalter „nur sichere Zuordnungen"
- [ ] Zeitraumsummen **aus den Zählern** (`MAX − MIN`), nicht aus der Sitzungstabelle —
      dann sind Monatsgrenzen kein Thema
- [ ] Altes Dashboard entfernen, `sankey/README.md` und `docs/influxdb-reference.md` im
      Repo `forgejo.intern/thomas/Grafana` um `ladesitzungen` und die neuen Measurements
      ergänzen

**Abhängig von** 13.

---

### 17. Fahrzeugdaten persistieren

**Problem.** Seit der Telegraf-Abschaltung schreibt **niemand mehr Fahrzeugdaten nach
InfluxDB**. Der DataHub verarbeitet nur `KebaGarage` und `KebaOutside`; für
`data/charging/{BMW,Mini,VW}` gibt es keinen Handler. Es existiert also keine Historie von
Ladestand, Reichweite oder Kilometerstand mehr — und Punkt 15 (Position auswerten) hat
ohne diesen Punkt gar keinen Ort, an den geschrieben werden könnte.

**Umfang**
- [ ] DataHub abonniert `daten/Fahrzeug/+/Status` und schreibt nach InfluxDB 3:
      Ladestand und Ziel als `percent_values`, Reichweite und Kilometerstand als eigene
      Messwerte, Verbindungs- und Ladestatus als `status_values`
- [ ] `location` ist `-` (ortsloses Gerät), `device` der Fahrzeugname
- [ ] **Zeitstempel aus dem `lastUpdate` bzw. `Zeitpunkt` des Payloads**, nicht
      `DateTimeOffset.UtcNow` — sonst wird ein 35 Tage alter retained Wert als aktuelle
      Messung eingetragen
- [ ] Position mitschreiben (Grundlage für Punkt 15)
- [ ] Wiederholte identische Payloads nicht als neue Messung schreiben

**Abhängig von** 10 *(entschieden am 2026-09-20, vorher stand hier 8)*. Die Umstellung
der Fahrzeug-Topics auf `daten/Fahrzeug/<Auto>/Status` liefert Punkt 10 — Punkt 8 migriert
nur Laden- und Konfigurations-Topics und hätte dieses Fundament nie gelegt. Vorher gäbe es
das Topic nicht, und es müsste zweimal gegen zwei Schemata gebaut werden.

**Welle E, seriell nach 13.** Kollidiert mit 8 und 13 im DataHub, deshalb nicht parallel zu
diesen. Punkt 14 hängt nicht davon ab und kann davor oder danach laufen; Punkt 15 dagegen
setzt 17 zwingend voraus.

#### Nachkontrolle am 2026-09-20, 13:05 — bestanden

DataHub `1.0.87`. Der BMW fuhr während der Prüfung, was den Nachweis erst wertvoll macht:
Es sind nicht nur Zeilen da, sie bewegen sich plausibel.

**Die Fahrt steht in `position_values`** — sechs aufeinanderfolgende Punkte, Breite steigend,
Länge fallend, also eine zusammenhängende Strecke und keine springende Punktwolke:

| Zeit | Breite | Länge | km-Stand | Reichweite |
|---|---|---|---|---|
| 12:50:42 | 48,4128 | 9,8753 | | |
| 12:54:23 | 48,4195 | 9,8788 | | |
| 12:58:52 | 48,4240 | 9,8651 | | |
| 13:00:21 | 48,4297 | 9,8602 | 2091 | 347 |
| 13:01:27 | 48,4354 | 9,8486 | 2092 | 340 |
| 13:03:10 | 48,4384 | 9,8265 | 2094 | 332 |

Kilometerstand steigt, Reichweite fällt, Ladestand 76 → 75 %. Die Werte hängen zusammen.

**Der Zeitstempel ist die Messzeit.** Die Zeile 13:01:27 trägt exakt den `lastUpdate` des
MQTT-Payloads, dessen `Zeitpunkt` 13:01:28,6 lautet — die Ankunftszeit steht nirgends.

**Die Entdopplung trägt, und der VW beweist sie am schärfsten.** Über alle vier Tabellen gilt
`COUNT(*) = COUNT(DISTINCT time)`: 17 Zeilen, 17 verschiedene Zeiten, obwohl der DataHub
zwischendurch neu startete und den retained Payload erneut las. Der VW steht seit 12:20 und
sein Payload ändert sich nicht — er hat **genau eine Zeile je Messwert**, nicht eine je
Empfang. Das ist der Primärschlüssel Tabelle + Tags + Zeit bei der Arbeit.

**Fehlende Werte werden nicht geschrieben.** Der VW liefert keine Position und hat deshalb
*keine* Zeile in `position_values` — kein `NULL`, und vor allem keine 0/0, die das Auto in
den Golf von Guinea gesetzt hätte.

**Offener Faden außerhalb dieses Repos:** `docs/influxdb-reference.md` im Grafana-Repo kennt
`distance_values` und `position_values` weiterhin nicht. Wer dort nachschlägt, findet die
beiden neuen Tabellen nicht — nachzutragen im Repo `forgejo.intern/thomas/Grafana`.

---

### 23. Web-UI auf dem Smartphone prüfen und geradeziehen

**Ziel.** Die Oberfläche wird überwiegend am Telefon benutzt, geprüft wurde sie dort nie
systematisch. **Hochformat und Querformat**, beide.

**Aufgekommen am 2026-09-20.** Punkt 21 hat den oberen Teil der Ladeseite umgebaut, und
die Session konnte das Ergebnis **nicht optisch prüfen** — die Screenshots über die
Chrome-Anbindung liefen dreimal in Renderer-Timeouts. Geprüft war nur der Build, der
generierte Razor-Code und die CSS-Regeln. Damit steht eine frisch umgebaute Seite im
Betrieb, die noch niemand auf einem Telefon gesehen hat.

#### Was der Bestand hergibt, nachgesehen am 2026-09-20

Das Fundament stimmt: `App.razor` trägt
`<meta name="viewport" content="width=device-width, initial-scale=1.0">`.

Darüber wird es uneinheitlich. Vier Breakpoints, keine zwei gleich:

| Datei | Breakpoint |
|---|---|
| `MainLayout.razor.css` | `max-width: 1640.98px` / `min-width: 1641px` |
| `NavMenu.razor.css` | `min-width: 1641px` |
| `ChargingOverview.razor.css` | `max-width: 700px`, `max-width: 1000px` |
| `Devices.razor.css` | `max-width: 640px` |

Der Umschaltpunkt des Layouts liegt bei **1641 px**. Alles darunter — Telefon im
Hochformat, Telefon im Querformat, Tablet, halber Bildschirm am Laptop — bekommt dieselbe
Darstellung. Das ist nicht falsch, aber es heißt: Es gibt **kein eigenes Telefon-Layout**,
nur ein Nicht-Breitbild-Layout.

**`Climate.razor` und `Heating.razor` haben überhaupt kein eigenes CSS** — keine
`.razor.css`, also auch keine einzige Medienabfrage. Sie sind auf dem Telefon vollständig
ungeprüft.

#### Ein Befund, der keine Gestaltungsfrage ist

**Die fünf Ladestufen erklären sich ausschließlich über `SfTooltip`.** Was Stufe 3
bedeutet („lädt, wenn PV-Überschuss plus Batteriekapazität die Mindestladeleistung
übersteigt und die Batterie zu mehr als 50 % geladen ist"), steht nirgends sonst.
Ein Tooltip hängt am Hover — **auf einem Touchgerät ist er nicht erreichbar.**

Das heißt: Am Telefon ist die zentrale Bedienung der Ladesteuerung unbeschriftet. Wer die
Stufen nicht auswendig kennt, rät. Das ist der einzige Punkt hier, der nicht Optik ist,
sondern Funktion — und er gehört zuerst behoben.

#### Die Ursache ist gefunden, gemessen am 2026-09-20

`laden-13b-ladeliste-entfernen` hat die Seite in einer laufenden Instanz bei 1400, 820,
450 und 414 px angesehen. Ergebnis: **Bei 414 px scrollt die Ladeseite seitlich**, die
`.uebersichtBox` wird auf etwa 150 px zusammengequetscht, ihr Inhalt läuft rechts hinaus,
die Balken schrumpfen auf Briefmarkengröße. Bei 820 px ist alles heil; die Grenze liegt
bei etwa 800 px.

**Schuld ist `.stepsGrid`** (`ChargingOverview.razor.css:99`), die sechs Ladestufen:

```css
grid-template-columns: 1fr 1fr 1fr 1fr 1fr 1fr;
grid-column-gap: 30px;
margin-left: 40px; margin-right: 40px;
```

Sechs Spalten schrumpfen nur bis zur Breite ihres längsten Wortes („Batterie-Prio"), dazu
5 × 30 px Abstand und 80 px Ränder — rechnerisch rund 800 px Mindestbreite, was die
Messung bestätigt. Die **Seite** ist dann überall so breit, und jeder andere Block bekommt
nur den Rest. Der Balkenbereich ist unschuldig.

**Der Beleg, dass es genau dieser Block ist:** `.wallboxGrid` hat
`@media (max-width: 700px) { 1fr }`, `.carsGrid` hat `@media (max-width: 1000px) { 1fr }` —
**`.stepsGrid` ist der einzige der drei ohne Medienabfrage.** Die Kacheln aus 9/9b und der
Fahrzeugbereich aus 10 wurden fürs Telefon vorbereitet, die Lade-Einstellungen nie.

Ein Umbruch auf zwei Reihen zu drei unterhalb von ~700 px räumt vermutlich die ganze Seite
auf, nicht nur diesen Block. **Das ist der erste Handgriff dieses Punktes.**

**Umfang**
- [ ] `.stepsGrid` umbrechen lassen — siehe oben, vermutlich die Hauptursache
- [ ] Alle sechs Seiten am Telefon durchgehen, **hoch und quer**: `ChargingOverview`,
      `Devices`, `Climate`, `Heating`, `LED`, `Error`
- [ ] Die Ladestufen am Touchgerät erklärbar machen — der Tooltip allein genügt nicht
- [ ] Querformat gesondert: dort ist die Höhe knapp, nicht die Breite. Alles, was auf
      vertikalen Platz baut, ist hier zu prüfen
- [ ] Eine gemeinsame Breakpoint-Skala statt 640 / 700 / 1000 / 1641 — an einer Stelle
      festgelegt, wie es Punkt 21 mit den Farben gemacht hat
- [ ] `Climate` und `Heating` bekommen, was sie brauchen — sie haben heute nichts
- [ ] Syncfusion-Komponenten am schmalen Rand prüfen (`SfGrid`, `SfLinearGauge`,
      `SfCheckBox`): Sie sind für den Bildschirm gebaut, nicht für die Hand
- [ ] Kein horizontales Scrollen auf irgendeiner Seite

**Nicht enthalten.** Das Sitzungs-Grid auf der Ladeseite — es verschwindet mit Punkt 13b,
dort Aufwand hineinzustecken wäre verschwendet.

**Abhängig von** 21 (erledigt). Berührt sich mit **3**, der `Devices.razor` um
Dienst-Karten erweitert, und mit **13b**. Läuft nicht parallel zu einem Punkt, der
`SmartHome.Web` anfasst.

**Wie prüfen.** Ein echtes Telefon schlägt jeden Emulator, und Thomas hat eins. Die
Chrome-Anbindung ist für diesen Punkt unzuverlässig — siehe oben. Realistisch ist eine
Mischung: die Session baut und begründet, Thomas schaut auf dem Gerät nach und meldet
zurück. **Der Bericht der Session muss deshalb sagen, worauf zu achten ist**, nicht nur,
was geändert wurde.

---

### 24. Syncfusion ablösen

**Frage von Thomas am 2026-09-20:** Brauchen wir Syncfusion überhaupt noch?

**Bestandsaufnahme, nachgezählt am 2026-09-20 auf dem Stand nach Punkt 13b:**

| Komponente | Anzahl | wo |
|---|---|---|
| `SfTooltip` | 6 | `ChargingOverview.razor` — die Erklärungen der fünf Ladestufen |
| `SfLinearGauge` | 3 | `LED.razor` |
| `SfSwitch` | 2 | `LED.razor` |

**Das ist alles.** Punkt 13b hat `SfGrid`, `SfDropDownList` und `SfCheckBox` entfernt;
`SfChart` und die Navigations-Komponenten kommen im ganzen Projekt **nicht vor**.

**Vier von sechs Paketverweisen in `SmartHome.Web.csproj` sind danach unbenutzt:**
`Calendars` und `Grid` (mit 13b frei geworden), `Charts` und `Navigations` (waren es
schon vorher). **`UIComponentsLib` referenziert `Charts`, `Navigations` und `Themes` und
benutzt keines davon** — reiner Ballast.

**Warum es sich lohnt.** Syncfusion ist kommerziell lizenziert. Der Schlüssel ist ein
Secret, das durch CI und Laufzeit getragen werden muss; fehlt er, blendet die Anwendung
ein Lizenzbanner ein (`Program.cs`: „Syncfusion license key not configured"). Der
Wegfall spart ein Geheimnis, eine Abhängigkeit und die mitgelieferte JS-/CSS-Fracht.

**Der größte Brocken fällt ohnehin an.** Die sechs Tooltips müssen für **Punkt 23**
sowieso ersetzt werden: Sie hängen am Hover, und am Telefon ist die Bedienung der
Ladestufen damit unbeschriftet. Wer sie touchtauglich macht, wird sie kaum als
`SfTooltip` behalten.

**Umfang**
- [ ] **Sofort und risikolos:** die vier unbenutzten Paketverweise in
      `SmartHome.Web.csproj` und die drei in `UIComponentsLib.csproj` entfernen
- [ ] Die sechs Tooltips ersetzen — zusammen mit Punkt 23, nicht daneben
- [ ] `LED.razor`: drei `SfLinearGauge` und zwei `SfSwitch` ablösen. Die Ladeseite
      zeichnet ihre Balken bereits mit reinem CSS; ein Schieberegler-Anzeiger und ein
      Schalter sind kein Grund für eine kommerzielle Bibliothek
- [ ] Danach: Paket, `AddSyncfusionBlazor()`, die Lizenzregistrierung in `Program.cs`,
      das Secret und das Skript in `App.razor` entfernen
- [ ] Prüfen, ob `Syncfusion.Blazor.Themes` noch etwas beiträgt, das die Seiten benutzen

**Abhängig von** 13b (entfernt die Hälfte der Nutzung) und sinnvoll **mit 23** zusammen
(die Tooltips). Der erste Haken lässt sich unabhängig davon jederzeit ziehen.

**Vorsicht bei `LED.razor`.** Das ist eine Seite, die niemand von uns je angesehen hat,
und die Ablösung eines Anzeigers ist eine sichtbare Änderung. Es gilt dasselbe wie bei
Punkt 21: Ohne einen Blick auf das laufende Ergebnis ist sie nicht abgenommen.

---

## Optional

### 15. Position mitschreiben und auswerten

- [ ] `position` von BMW und Mini nach InfluxDB schreiben
- [ ] Nach einigen Wochen prüfen, ob sich Garage und Stellplatz in den Positionswolken
      trennen lassen. Erwartung: nein (GPS-Streuung größer als der Abstand, in der Garage
      kein Fix). Dann ist die Frage empirisch beantwortet statt vermutet

**Abhängig von** 17 — solange Fahrzeugdaten überhaupt nicht persistiert werden, gibt es
keinen Ort für die Position.

---

## Offene Punkte außerhalb dieses Vorhabens

- ~~**Grafana-Dashboards sauber versionieren — eigenes Repo im internen Forgejo.**~~
  **Erledigt am 2026-09-20.** Das Repo ist `forgejo.intern/thomas/Grafana` (privat
  angelegt); dort liegen Dashboards, das Export-Skript, der Sankey-Generator und die
  InfluxDB-Referenz. Die Sicherungsfrage war der Vorbehalt und ist ausgeräumt: Forgejo
  läuft täglich über Velero nach Garage, und das Ziel steht in einem anderen Gebäude.

  **Die Bestandsaufnahme fand etwas anderes als erwartet.** Nicht Abdrift war das Problem,
  sondern Abdeckung. Von den acht Dateien im alten Ordner waren nur **sechs Dashboards** —
  `Percentage.json` und `temp.json` sind Panel-Listen, keine Dashboards. Live existieren
  **28**. Versioniert war also nicht einmal ein Viertel; nie exportiert waren unter
  anderem Raumklima, Thermostate, Heizkreise, Pufferspeicher, Heizkörperlüfter, PV
  Produktion, beide PV-Prognosen, beide Grundlast-Dashboards und der komplette
  Infrastrukturblock (Longhorn, Velero, Node Exporter, Loki, K8s).

  Die Abdrift der sechs ist dagegen **gering**. Jeder manuelle UI-Export schneidet den
  eingebauten Annotations-Block weg, den die API mitliefert — dieselben acht Felder bei
  jedem Dashboard, ohne Bedeutung. Abzüglich davon: Energieübersicht, Wallbox Charging
  und Wärmepumpe **0** echte Änderungen, Energiefluss 3 (`schemaVersion`, `graphTooltip`,
  `weekStart`), Klima-Dashboard 97 (alle in `panels`), Zisterne 221 und ein Panel weniger.
  Vier von sechs waren seit April bzw. Juli inhaltlich unverändert.

  **Zur Normalisierung, weil die Begründung präziser ist als gedacht:** Ein Doppellauf
  ohne Speichern dazwischen ist auch *roh* diff-frei — dieser Test allein beweist nichts.
  Das Rauschen entsteht beim Speichern. Über zwölf aufeinanderfolgende gespeicherte
  Versionen trägt `version` in allen zwölf die bedeutungslose Änderung, bei `Temp_Test`
  (5 → 6) war sie die einzige überhaupt. `id` fällt aus einem anderen Grund weg: eine
  instanzlokale Zeilennummer, die erst beim Restore stört. `updated`, `updatedBy`,
  `created`, `createdBy`, `orgId` und `expires` stehen in Grafana 13 gar nicht im
  `dashboard`-Objekt, sondern im `meta`-Block, den das Skript ohnehin nicht mitnimmt.

  **Nebenbefund:** Provisioning ist entgegen der Annahme **teilweise doch eingerichtet** —
  `Cluster Errors Overview` und `Backup Overview (Velero + Garage)` sind `provisioned`
  und im UI schreibgeschützt. Für Dashboards im UI-Betrieb bleibt es dabei: kein
  Provisioning.
- **Grafana-Token steht weiter auf Admin — die Bedingung dafür ist inzwischen erfüllt.**
  Thomas hatte das Service-Account-Token am 2026-09-20 „erst mal auf Admin hochgestuft …
  bis der Import-Weg sauber steht". Er steht jetzt: Die Forgejo Action ist live, drei
  Pushes sind durchgelaufen, der Abgleich meldet 29 Dashboards und 0 Unterschiede. Damit
  ist die Rückstufung fällig.

  **Offen ist nur, wie weit.** Die Session `grafana-konvention` hält in der README fest,
  alles Gemessene spreche dafür, dass die Rolle **Editor** für den Import genügt — nennt
  es aber ausdrücklich keinen Beweis, denn gelaufen ist der Import bislang ausschließlich
  mit Admin. Der Prüfstein steht bereit: Dashboard `adbtrcf`, seit dem 2026-09-20 betitelt „Testobjekt Import-Action".

  **Die Messung braucht einen zweiten Token mit Rolle Editor, den nur Thomas anlegen
  kann.** Sie fasst weder das Repo noch die Dashboards an. Danach ist entweder Editor
  belegt oder die Ausnahme begründet — beides besser als ein Admin-Token aus Bequemlichkeit.
- **Arbeitsfehler der Integrator-Session am 2026-09-20, hier festgehalten, damit er sich
  nicht wiederholt:** Um die Abnahme von `grafana-konvention` nachzurechnen, habe ich in
  *ihrem* Arbeitsverzeichnis `~/Repos/Forgejo.intern/Grafana` ein `git checkout origin/main`
  ausgeführt, statt in einem eigenen Klon. Das hat ihr den HEAD losgelöst; ihr nächster
  Commit landete auf dem abgehängten HEAD statt auf `main` und war damit unpushbar, ohne
  dass sie es merken konnte — sie meldete sich als fertig, während die Arbeit lokal
  festhing. Repariert mit `git checkout -B main <commit>`, eine reine Vorwärtsbewegung bei
  sauberem Arbeitsverzeichnis, ohne eine Datei anzufassen.

  **Regel daraus: Nachrechnen in einem Repo, in dem eine andere Session arbeitet, nur über
  einen eigenen Klon.** Lesen ist harmlos, aber `checkout`, `fetch --prune` und alles, was
  HEAD oder Refs bewegt, ist ein Eingriff in fremde Arbeit.

  **Die Session fand die zweite Hälfte des Fehlers, und die ist allgemeiner:** Sie hatte
  nach jedem Push „gepusht" gemeldet, weil `git push -q origin main && echo gepusht`
  erfolgreich war. Auf losgelöstem HEAD pusht dieser Befehl aber die *lokale Referenz*
  `main` — die unverändert auf dem alten Stand stand. Ergebnis: „Everything up-to-date",
  **Exit-Code 0**, Erfolgsmeldung. Sieben Pushes davor ging es gut, hätte es aber nicht
  müssen.

  **Regel daraus: Der Exit-Code eines `git push` ist kein Nachweis.** Nachgewiesen ist ein
  Push erst durch `git fetch` und einen Vergleich von `rev-parse origin/main` mit dem
  erwarteten Commit. Das gilt besonders dort, wo ein Push ein Schreibvorgang im laufenden
  System ist — im Grafana-Repo löst er den Import aus, hier den Rollout.
- **Fremde Dashboard-Titel tragen den Schrägstrich, den die Konvention als schlimmsten
  benennt** — „Kubernetes / Views / Pods", „Logs / App". Die Session hat sie bewusst
  **nicht** angefasst, und die Begründung trägt: Ein Neuladen von grafana.com holte den
  Titel zurück, das wäre Aufräumen mit eingebautem Rückfall. Begründet in `3184b7c` und in
  der Konvention vermerkt. **Entscheidung liegt bei Thomas:** so lassen, oder die fremden
  Dashboards bewusst aus der Konvention ausnehmen.
- **Die drei alten Fahrzeug-Topics liegen noch retained am Broker.**
  `data/charging/BMW`, `data/charging/Mini` und `data/charging/VW` stammen aus der Zeit vor
  Punkt 10. **Im gesamten Code referenziert sie niemand mehr** (geprüft am 2026-09-20 über
  `.cs`, `.razor`, `.py`, `.yaml` außerhalb von `Depricated/`). Ihre Inhalte sind veraltet
  und sehen trotzdem gültig aus — der BMW steht dort auf 83 % Ladestand, tatsächlich sind
  es 75 %. Genau die Klasse Altlast, die am 2026-09-20 schon einmal aufgeräumt wurde.
  **Löschen ist Thomas' Entscheidung**, und seit dem letzten Mal gilt: Ein Rollback über
  retained Nachrichten steht danach nicht mehr zur Verfügung.
- **Der Mini ist angebunden, liefert aber noch keine Daten.** `[Mini] Output topic:
  'daten/Fahrzeug/Mini/Status'`, `Connected to the BMW broker — vehicle is ready` — in
  InfluxDB steht von ihm trotzdem nichts, weil BMW CarData nur bei Fahrzeugereignissen
  sendet und er steht. Kein Defekt, aber bis zur ersten Fahrt auch kein Beweis. Erst dann
  ist Punkt 17 für alle drei Fahrzeuge belegt statt für zwei.
- **Velero ist in Grafana doppelt vorhanden.** `ozk-vlr-mon` und
  `velero-backup-overview`, beide aus grafana.com 23838, eines davon provisioniert.
  Entscheidung liegt bei Thomas: welches bleibt.
- **Die Balken der Ladeseite sind auf 150-W-Stufen gerastert.**
  `ChargingSituationManager.CalculatePowerPercent` rechnet
  `return (power * 100) / PowerMaximum;` — `power` ist `int`, `PowerMaximum` ist
  `const int 15000`, also **Ganzzahldivision**, deren Ergebnis erst danach nach `decimal`
  fließt. Die Nachkommastellen entstehen nie.

  Die Folge ist größer als der zuerst gemeldete Fall: Nicht nur verschwindet alles unter
  150 W aus dem Balken (gesehen: „Batterie laden: 6 W" in der Legende, kein Segment im
  Balken) — **die gesamte Skala kennt nur Vielfache von 150 W.** 1.350 W und 1.499 W
  zeichnen dasselbe Segment. Ein `100m` oder ein `decimal`-Cast behebt es.

  Vorbestehend, weder von Punkt 21 noch von 13b verursacht. Gefunden von
  `laden-13b-ladeliste-entfernen` beim Blick auf die laufende Seite.
- **`MQTTService` ignoriert die konfigurierte Broker-Adresse.**
  `MQTTService.cs:92` verdrahtet `.WithTcpServer("mosquitto.intern", 1883)` fest, während
  `Program.cs:27` `SMARTHOME__MQTT_BROKER` liest und beim Start
  „Using MQTT broker at …" protokolliert. **Das Protokoll behauptet also etwas, das die
  Verbindung nicht tut.** Heute steht in beiden derselbe Host, der Fehler ist deshalb
  unsichtbar — wer die Konfiguration ändert, bekommt eine Startmeldung, die die Änderung
  bestätigt, ohne dass sie wirkt.
- **Eine dritte Wallbox scheitert nicht am Web, sondern an `ChargingSettings`.** Dort gibt
  es nur zwei Freigabe-Booleans (`InsideChargingEnabled`, `OutsideChargingEnabled`). Im
  `MQTTService` und in der Seite kostet eine dritte Box nach Punkt 9 nur ihren Eintrag in
  `LadeTopics.Wallboxen` — einen Freigabeschalter bekäme sie nicht. Zusammen mit der
  Umbenennung `Inside`/`Outside` → Boxnamen ein eigener Punkt; im Web liegt die Abbildung
  danach an genau einer Stelle (`ChargingOverview.Station()`).
- **`MqttService.Wallboxen` ist nach Gerätename verschlüsselt, nicht nach Ort + Gerät.**
  Beide Boxen stehen in M3; käme eine gleichnamige Box an einem zweiten Ort, überschriebe
  sie den Eintrag. Im Code als `remarks` vermerkt, harmlos solange es bei einem Ort bleibt.
- **Latenter NullReferenceException im Web, vorbestehend.**
  `ChargingSettings = JsonSerializer.Deserialize<…>(payload)` kann `null` zuweisen (CS8601
  an vier Stellen im `MQTTService`), und `ChargingOverview` dereferenziert
  `chargingSettings` ungeprüft. Von Punkt 9 weder verursacht noch behoben.
- **Grafana-Dashboards: das Repo wird die Quelle, nicht der Spiegel.**
  *Entschieden am 2026-09-20, nachdem der erste Entwurf umgedreht wurde.* Ursprünglich
  war gedacht: im UI wird gearbeitet, das Repo spiegelt per Export. Daraus folgte ein
  Zwei-Schreiber-Problem — ein automatischer Import hätte stillschweigend UI-Änderungen
  überschrieben, weshalb nur eine *Erkennung* von Abdrift sicher gewesen wäre.

  **Thomas hat die Arbeitsweise anders festgelegt:** Die Dashboards werden überwiegend
  im Repo gebaut (von Claude-Sessions). Will er selbst etwas ändern, macht er in Grafana
  eine **Kopie**, ändert die Kopie und lässt die Änderung von dort ins Repo übernehmen.
  Die Kopie ist ein Wegwerf-Objekt; nur ihr Unterschied wandert zurück. Damit gibt es
  keine zwei konkurrierenden Schreiber mehr auf demselben Dashboard, und die Abdrift, die
  am 2026-09-20 überhaupt erst auffiel, kann strukturell nicht mehr entstehen.

  **Schritt 1 (in Arbeit, Session `grafana-wallbox-verlauf`):** `import_dashboards.py`
  als Gegenstück zum Export, Zuordnung über die `uid`, Trockenlauf und Vorher-Anzeige.
  Braucht keine Änderung am Grafana-Deployment und ist sofort nutzbar.

  **Offen: Der Grafana-Service-Account steht auf Admin — auf unbestimmte Zeit.**
  Am 2026-09-20 bewusst hochgestuft, mit der Bedingung „bis der Import-Weg sauber steht".
  Der Import-Weg steht seitdem. **Zurückgestuft wurde trotzdem nicht:** Thomas hat den
  Editor-Test abgelehnt, auch die Variante ohne Klickaufwand (Service Account per API
  anlegen, testen, löschen). Ohne Test lässt sich nicht belegen, dass Editor genügt, und
  damit ist die Bedingung so, wie sie formuliert war, nicht einlösbar.

  Immerhin ist die **Begründung** für Admin entkräftet: Beide Ordner weisen der Rolle
  Editor „Edit" zu, es gibt keinen Ordner ohne Zuweisung — die Lücke, aus der der 403
  stammen sollte, existiert nicht. Ein Beweis ist das nicht: Die Permissions-API gibt
  geerbte Rechte nicht mit aus, ein leerer Editor-Eintrag am Dashboard sagt nichts.

  **Diese Zeile bleibt stehen, bis das Recht zurückgenommen ist** — temporäre
  Berechtigungen werden nicht durch eine Entscheidung dauerhaft, sondern durch Vergessen.

  **Schritt 2, entschieden am 2026-09-20: eine Forgejo Action, nicht Provisioning.**
  Push ins Dashboard-Repo → Action → Import über die Grafana-API. Die Voraussetzungen
  sind da: drei `act-runner` laufen im Cluster (ein amd64, zwei arm64), das Repo hat noch
  keine Workflows.

  Die Action löst die drei Dinge, die Provisioning schwer machten, indem sie sie
  gar nicht erst hat: keine Änderung am Grafana-Deployment, keine 1-MiB-Grenze
  (die 28 Dashboards sind 1,5 MB, `node-exporter-full.json` allein 464 KB), keine
  Cross-Repo-Konstruktion zwischen Dashboard- und Deployments-Repo. Und sie baut auf dem
  `import_dashboards.py` auf, das ohnehin entsteht.

  **Was sie ausdrücklich NICHT leistet, damit das niemand später für einen Fehler hält:**
  Sie macht die Dashboards nicht schreibgeschützt. Eine versehentliche UI-Änderung am
  Original überlebt bis zum nächsten Push und verschwindet dann stillschweigend. Und sie
  ist push-getrieben, nicht abgleichend — verändert sich etwas in Grafana, korrigiert es
  niemand bis zum nächsten Push. Beides ist bewusst in Kauf genommen: Bei der vereinbarten
  Arbeitsweise (am Original wird nicht im UI gearbeitet, sondern an einer Kopie) ist das
  Risiko ein Versehen, keine Gewohnheit — und dafür einen Eingriff ins laufende
  Grafana-Deployment zu bauen, wäre unverhältnismäßig.

  **Die Lücke schließt eine zweite, zeitgesteuerte Action:** exportieren, mit dem
  Repo-Stand vergleichen, bei Abweichung melden. Das ist genau die Erkennung, die bis zum
  2026-09-20 gefehlt hat, und sie kostet ein paar Zeilen im selben Workflow-Ordner.

  **Nebeneffekt, der den offenen Punkt oben erledigt:** Die Action braucht einen
  Grafana-Token als Forgejo-Secret — mit Editor-Rechten, nicht Admin. Das ist die
  Gelegenheit, den hochgestuften Token wieder zurückzunehmen, statt ihn zu vergessen.

  **Provisioning bleibt die Rückfallebene**, falls sich die Verabredung als nicht
  tragfähig erweist. Die Rahmenbedingungen dafür sind oben festgehalten und gelten
  weiter: ArgoCD-App `grafana` aus `SmartHomeDeployments.git`, Pfad `grafana`, Namespace
  `grafana`; das Deployment ist handgeschrieben und mountet heute nur Storage.

  **Vorsicht bleibt beim ersten vollständigen Import:** Wird ein Dashboard importiert,
  dessen Repo-Stand älter ist als der Live-Stand, fällt es stillschweigend zurück. Vor dem
  ersten Lauf über alle Dashboards also exportieren, vergleichen, und erst laufen lassen,
  wenn die Differenz leer oder erklärt ist.

- **Die Energieaufteilung hängt einen Regelzyklus hinterher.** Gefunden am 2026-09-20 von
  der Session `grafana-wallbox-verlauf`, von mir in den Rohdaten bestätigt. Die
  Aufteilungszeile zum Zeitpunkt T trägt die Ladeleistung von **T − 5 s**:

      11:44:52,612  Quellensumme   19,0 W  =  AktuelleLadeleistung 11:44:47,888
      11:44:57,704  Quellensumme 4103,0 W  =  AktuelleLadeleistung 11:44:52,891
      11:45:02,790  Quellensumme 4161,0 W  =  AktuelleLadeleistung 11:44:57,889

  Die Zurechnung ist davon **unberührt** — die drei Anteile summieren sich auf die
  Leistung, die sie verwendet hat, auf 0,1 W genau. Falsch ist nur der Zeitbezug.
  *(Meine eigene erste Nachkontrolle hatte den Versatz unbemerkt herausgekürzt, weil sie
  beim Eintreffen einer Aufteilung den zuletzt gespeicherten Status verglich — also den
  vorherigen. Deshalb meldete sie „Abweichung exakt 0,0 W". Wer so misst, misst den
  Versatz weg.)*

  **Zwei Folgen, die bei Punkt 13 und 14 bekannt sein müssen:**
  1. Bei feinem Zeitraster schlägt die Gegenprobe **an den Flanken** aus — beim Ladebeginn
     im 10-s-Raster bis −50 %, im Minutenraster −50 % in der Anfangsminute. Das sieht wie
     ein Zurechnungsfehler aus, ist aber der Versatz. Ein Ausschlag **mitten** in einer
     Ladung wäre dagegen ein echter Befund. Steht so in der Panel-Beschreibung des
     Quellenmix-Dashboards.
  2. Über die Energie fehlen die **ersten Sekunden** jeder Ladung: im Fenster 11:43–11:50
     summieren die drei Zähler 0,12391 kWh gegen 0,13200 kWh `EnergieGesamt` der Box,
     also **−6,1 %** bei 110 s Ladedauer — rund 5 s bei 4,2 kW. Der Fehlbetrag ist
     **absolut und wächst nicht** mit der Ladedauer; bei einer mehrstündigen Ladung liegt
     er im Promillebereich. Bei kurzen Sitzungen liegt „davon PV/Batterie/Netz" in einer
     Protokolltabelle deshalb systematisch unter der Boxenergie.

  **Wenn es nachgezogen wird**, ist der Ort die Stelle, an der der ChargingController den
  Leistungswert für den Zyklus liest. Ob es das wert ist, ist offen: Der Fehler ist klein,
  konstant je Sitzung und gut verstanden — und ein Eingriff in den Regelzyklus wiegt
  schwerer als ein dokumentierter Versatz.
- **Namensschema für die Grafana-Dashboards festlegen.** *Aufgenommen am 2026-09-20, als
  das Deployment über eine Forgejo Action entschieden war.* Solange Dashboards von Hand
  im UI entstanden, war der Wildwuchs folgenlos. Sobald sie aus dem Repo kommen, wird der
  Name zur Schnittstelle.

  **Der wichtigste Satz zuerst: Die Identität eines Dashboards ist die `uid`, nicht der
  Dateiname.** Import und Export ordnen darüber zu, der Dateiname wird aus dem Titel
  erzeugt und ist Folge, nicht Ursache. Ein Schema, das nur Dateinamen regelt, verfehlt
  das Problem. **Und eine `uid` umzubenennen ist keine Umbenennung, sondern ein Neuanlegen:**
  Der Import legt ein zweites Dashboard an, das alte bleibt verwaist stehen, und jeder
  Link `/d/<alte-uid>` zeigt weiter dorthin. Eine Migration ist also Handarbeit mit
  Aufräumen — nicht ein Suchen-und-Ersetzen.

  **Befund am Bestand (29 Dashboards, 2026-09-20):**
  - **Zwei Welten bei den `uid`s.** Sprechend: `energy-overview`, `grundlast-m1`,
    `laden-quellenmix`, `zisterne-level`. Zufällig: sechs volle UUIDs
    (`1c0261b1-a2fd-…`), dazu `adsd98g`, `ad2pnr7`, `adbtrcf` und ein 39-Zeichen-Ungetüm
    `KKRmiuzTb2oneWM9jbwRTONWHyTXMFiQKnjzZI5`. Die zufälligen sind die, die Grafana beim
    Anlegen im UI vergibt.
  - **Sprache gemischt, teils innerhalb desselben Dashboards.** „Energieübersicht" trägt
    die `uid` `energy-overview` — Datei deutsch, Identität englisch. Daneben
    `wallbox-charging-energy` ganz englisch und `zisterne-fuellstand` mit `uid`
    `zisterne-level` wieder gemischt.
  - **Umlaut-Transliteration** in den Dateinamen (`heizkoerperluefter`, `waermepumpe`,
    `energieuebersicht`), weil der Name aus dem Titel erzeugt wird.
  - **Arbeitskopien sind bereits im Repo gelandet:** `energiefluss-copy` (Titel
    „Energiefluss Copy", `uid` `adsd98g`) und `temp-test` (Titel `Temp_Test`). Das ist
    genau der Kopie-Weg, den Thomas künftig gehen will — er braucht also eine Regel,
    **wie eine Kopie erkennbar ist und dass sie nicht exportiert wird**, sonst wächst das
    Repo mit Wegwerf-Ständen zu.
  - **Fremd-Dashboards liegen wie eigene daneben:** `node-exporter-full` (`rYdddlPWk`),
    `kubernetes-views-pods` (`k8s_views_pods`), `logs-app`
    (`sadlil-loki-apps-dashboard`). Die stammen von grafana.com und haben einen anderen
    Lebenszyklus — sie werden dort aktualisiert, nicht hier gepflegt. Ohne Trennung
    überschreibt ein Import eine Aktualisierung, oder niemand traut sich zu aktualisieren.
  - **Sonderzeichen in Titeln:** Em-Dash in drei, En-Dash in einem, `&` in zweien,
    Schrägstriche in dreien (`Kubernetes / Views / Pods`). Der Schrägstrich sieht aus wie
    eine Ordnerhierarchie, ist aber keine. Das Em-Dash wird im URL-Slug zu `e28094`
    hexkodiert.
  - **Doppelte Themen** ohne erkennbare Abgrenzung: `longhorn-dashboard` gegen
    `longhorn-monitoring`, zwei PV-Prognosen, zwei Grundlast-Dashboards.
  - **`-dashboard`-Suffix willkürlich:** bei `klima-dashboard` und `k8s-dashboard`, nicht
    bei `raumklima`, `heizkreise`, `thermostate`.

  **Zu regeln sind vier Dinge, in dieser Reihenfolge der Wichtigkeit:**
  1. `uid`-Konvention — sprechend, stabil, kurz. Sie ist die Identität und steht in jedem
     Link.
  2. Trennung eigener von fremden Dashboards, weil sie verschiedene Lebenszyklen haben.
  3. Umgang mit Arbeitskopien, damit der Kopie-Weg das Repo nicht zumüllt.
  4. Titelkonvention (Sprache, Sonderzeichen) — daraus folgt der Dateiname von selbst.

  **Empfehlung:** Die Konvention gilt ab sofort für Neues; der Bestand wird nur dort
  migriert, wo eine zufällige `uid` ohnehin stört. Ein Umbenennen aller 29 auf einen
  Schlag kostet mehr, als es einbringt — siehe oben, warum eine `uid`-Änderung ein
  Neuanlegen ist. `energiefluss-copy` kann dagegen sofort weg. **`temp-test` nicht**: Es hat sich beim
  Ende-zu-Ende-Test der Forgejo Action als Testobjekt bewährt (Tag rein, Import, Tag
  raus) und sollte im Schema als bewusstes Testobjekt gekennzeichnet werden statt als
  Überbleibsel zu gelten. *(Revision meiner eigenen Empfehlung vom selben Tag.)*
- **`Convert.ToInt16` in `InfluxDB3Connector` entfernen — latenter Überlauf für jeden
  Aufrufer.** `WriteCounterValue` (Zeile 271) und `WriteStatusValue` (Zeile 250) verengen
  den Wert in C# auf 16 Bit. **Die Spalten sind längst Int64** — in InfluxDB nachgesehen
  am 2026-09-20: `counter_values.value_counter` und `status_values.value_status` beide
  `Int64`. Das Line Protocol verbreitert ohnehin wieder auf i64; `InfluxCounterRecord.
  Value_Counter` ist schon ein `int`. Die Verengung ist also gegenstandslos und
  ausschließlich schädlich.

  **Es ist keine Schemaänderung nötig**, die Korrektur ist eine Zeile je Stelle. Heute
  trifft es niemanden: In `counter_values` stehen nur `Betriebsstunden_Waermeerzeuger`
  (2579) und `Schaltzyklen_Waermeerzeuger` (1918) der Wärmepumpe. Beide wachsen aber
  monoton, und bei 32767 endet es nicht mit einer falschen Zahl, sondern mit einer
  `OverflowException` im Schreibpfad des DataHubs. Zehn bis zwanzig Jahre Zündschnur.

  *Aufgekommen bei Punkt 17: Die Session hat `counter_values` wegen dieser Grenze gemieden
  und zwei neue Tabellen angelegt. Die Gefahr war real, die Begründung („counter_values
  speichert Int16") aber falsch — es speichert Int64, nur der Konverter verengt. Den
  Defekt zu umgehen statt ihn zu beheben lässt ihn für den nächsten Aufrufer stehen.*

  **Offen und zeitkritisch:** Ob der Kilometerstand in `distance_values` bleibt oder nach
  `counter_values` gehört. Für `distance_values` spricht das Hausmuster „eine Tabelle je
  Größe mit der Einheit im Feldnamen" — `counter_values` hat gar keine Einheit und trüge
  sonst Stunden, Schaltzyklen und Kilometer nebeneinander. Thomas neigt zur Erweiterung
  von `counter_values`. **Solange `distance_values` noch keine Daten hat, ist der Wechsel
  billig; danach ist er eine Migration mit verwaisten Reihen.**
- **InfluxDB-Modellierung** — die Regeln stehen jetzt in `Docs/InfluxDB-Modellierung.md`
  und gelten für alles Neue: Die Einheit trennt die Tabellen, der Datentyp folgt ihr; Feld,
  wenn die Werte ohne einander unvollständig sind, sonst `measurement`-Tag. Offen bleiben
  dort die drei Felder von `energy_values` und die sieben Tags, die jede Tabelle trägt.

- **Benachrichtigungen.** `Nachrichten/#` wurde nur von der Flutter-App gelesen. Ein
  Meldeweg für die Web-PWA fehlt danach — eigenes Thema.
- **`MaxStatusAge` in der RulesEngine — bestätigt real.** Dieselbe Verwechslung wie in
  Punkt 16: Die Regeln bewerten das Alter gegen die Empfangszeit, ein retained Status gilt
  nach einem Neustart als taufrisch und die Stale-Regel feuert nie. **Gemessen am
  2026-09-20:** `cangateway/M1/WEZ/Status/FA_Status` liefert einem frischen Client nach
  0,00 s einen Wert — das Topic **ist** retained. Milder als bei der Wallbox, weil der
  Fail-Safe den Mischer öffnet („kostet nur Effizienz, nie Komfort"), aber die Ursache ist
  dieselbe.
- **`EnphaseLib.Tests` und `ShellyLibTests` sind Integrationstests, keine Unit-Tests.**
  Ersteres meldet sich real bei Enphase an, Letzteres pollt eine feste IP im Heimnetz und
  ist zudem fachlich fragil (erwartet Bezug, schlägt bei Einspeisung fehl). Entweder auf
  gemocktes HTTP umschreiben oder als Integrationstests kennzeichnen und aus der CI
  heraushalten — solange sie so bleiben, können sie dort nicht laufen.
- ~~**`Kubernetes/microk8s/InfluxDB/influxdb3-*.yaml` beschreiben ein Deployment**, während
  im Cluster ein StatefulSet (`influxdb3-enterprise`) läuft.~~ **Erledigt am 2026-09-20:**
  Der ganze Ordner `Kubernetes/microk8s/` liegt jetzt unter `Depricated/microk8s/`. Er war
  durchgängig Altlast — alles darin stammte vom 2026-02-14 oder früher, und der Cluster
  läuft seit Langem auf k3s mit ArgoCD aus `SmartHomeDeployments`.
- **Vier MQTTnet-Versionen im Repo.** `MQTTClient` nutzt 5.0.1.1416, andere Projekte
  4.3.1.873, 4.3.3.952 und 4.3.6.1152. Unterschiedliche Bibliotheksversionen können sich
  bei Randverhalten wie dem Retain-Flag unterschiedlich verhalten.
- **`SmartHome.Web/Dockerfile` kopiert `DataContracts`**, obwohl das Projekt es nirgends
  referenziert, auch nicht in der `.sln`. Sieht nach einem Überbleibsel aus.
- **WiCAN.** Für den VW alternativlos, sobald das Portal wegfällt: Nur ein
  fahrzeugseitiger Sensor kann zeitnah melden, dass angesteckt wurde. Beim BMW vorher
  die OBD-Sperre neuerer Modelle prüfen; CarData liefert dort bereits alles Nötige.
