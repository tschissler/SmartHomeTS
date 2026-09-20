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

| Punkt | Branch | seit | Stand |
|---|---|---|---|
| 1 BMW-Token | `worktree-laden-01-bmw-token` | 2026-09-20 | **blockiert** — Doku fertig und committet; `bmwconnector-credentials` beim Bootstrap aus der Shell-Umgebung mit Platzhaltern überschrieben (`BMW_CLIENT_ID`/`BMW_GCID` je 10 Bytes, `Mini_*` intakt). GCID **nicht** aus dem Pod-Log rekonstruierbar (BMW war seit Pod-Start nie verbunden) — Reparatur braucht die Portal-Werte oder den `sub`-Claim aus `bmwconnector-bmw-tokens`. Bootstrap nur mit `env -u BMW_GCID -u BMW_CLIENT_ID`, sonst werden die Platzhalter erneut zurückgeschrieben |

Merges nach `main` gibt ausschließlich Thomas frei: jeder Merge ist über den ArgoCD Image
Updater binnen ~2 min ein Deployment ins laufende System.

---

## Parallelisierung in Wellen

Die Punkte 7→8→9→10→11→13→14 hängen in einer Kette; echte Parallelität gibt es nur an zwei
Stellen. Sinnvoll sind 2–3 gleichzeitige Sessions, jede in einem eigenen git worktree, mit
einem Branch pro Punkt.

| Welle | parallel | Anmerkung |
|---|---|---|
| A | 0, 1, 2, 4 | 1 braucht Thomas interaktiv (BMW-Login); 1 und 2 berühren beide den BMWConnector, aber disjunkte Dateien |
| B | 3 | braucht 2 (Begriff von „gesund") und 0 (sonst baut der Helfer in `Libs/` nichts neu) |
| C | 7 + 8 zusammen in **einer** Session | harter Schnitt, muss ein Release sein — nicht aufteilen |
| D | 9 ∥ 12 | Web gegen ChargingController, saubere Trennung — sobald die Payload-Verträge aus C stehen |
| E | 10 → 11 → 13 → 14 seriell; 15 optional nach 10 | |

Innerhalb einer Welle gilt: kein Punkt wird von zwei Sessions angefasst, und
`Docs/Backlog-Laden.md` bearbeitet ausschließlich die Integrator-Session.

---

## Parallel arbeiten — Kollisionen

Die Abhängigkeiten unten sagen, was fachlich aufeinander aufbaut. Wer mehrere Punkte
gleichzeitig bearbeitet, braucht zusätzlich die **Datei-Kollisionen**:

| Bereich | angefasst von Punkt |
|---|---|
| `SharedContracts` | 7, 10, 12, 13 |
| `ChargingController` | 8, 12 |
| `KebaConnector` | 7, 16 |
| `SmartHome.DataHub` | 8, 13, 17 |
| `SmartHome.Web` | 9, 10, 11, 13 |
| alle fünf Dienste + `Devices.razor` | 3 |

Daraus folgt:

- **Gefahrlos gleichzeitig:** 1, 2, 4 — und 14 lässt sich jederzeit vorbereiten.
- **Punkt 3 kollidiert mit fast allem.** Entweder vorziehen und zügig abschließen, oder
  bis nach 13 zurückstellen. Nicht mittendrin einschieben.
- **7 → 8 → 9 ist eine echte Kette**, nicht parallelisierbar: 7 definiert den Contract,
  den 8 konsumiert und auf dem 9 aufsetzt.
- **12 erst nach 8**, sonst treffen sich zwei Sessions im `ChargingController`.
- **Nummern bleiben stabil.** Neue Erkenntnisse werden hinten angehängt (16, 17), nie
  eingeschoben — laufende Sessions verweisen auf diese Nummern.

---

## Unabhängige Vorarbeiten

### 0. CI-Trigger für geteilten Code

**Problem.** Kein Service-Workflow hat einen Pfadfilter auf den geteilten Code, den er
referenziert. Eine Änderung an `SharedContracts` (von 7 Projekten referenziert),
`MQTTClient` oder `SmartHomeHelpers` (je 4) baut **kein** Image neu — es gibt keinen
Fehler, nur ein stilles Nicht-Deployment. Einzige Ausnahme ist `RulesEngine.yml`, und
selbst dort fehlt `SharedContracts`.

| Service | referenziert | Filter heute |
|---|---|---|
| ChargingController | SharedContracts | keiner |
| KebaConnector | SharedContracts, MQTTClient, Libs/HelpersLib | keiner |
| ShellyConnector | SharedContracts, MQTTClient, SmartHomeHelpers | keiner |
| EnphaseConnector | SharedContracts, MQTTClient, SmartHomeHelpers | keiner |
| SmartHome.DataHub | SharedContracts, MQTTClient | keiner |
| SmartHome.Web | SharedContracts, SmartHomeHelpers | keiner |
| RulesEngine | MQTTClient | MQTTClient, SmartHomeHelpers |

**Warum zuerst.** Punkt 7/8 legt die neuen Payload-Typen nach `SharedContracts`, Punkt 3
den Heartbeat-Helfer nach `Libs/`, Punkt 12 ändert die Einheitenkommentare in
`ChargingSituation`. In allen drei Fällen wäre der harte Schnitt halb ausgerollt, ohne
dass es auffällt.

**Umfang**
- [ ] Je Service-Workflow die tatsächlich referenzierten Pfade in `paths:` ergänzen
      (Quelle: die `ProjectReference`-Einträge der `.csproj`)
- [ ] `SmartHomeHelpers` aus `RulesEngine.yml` entfernen, wenn es dort nicht referenziert ist
- [ ] `.claude/worktrees/` in `.gitignore` aufnehmen — Worktrees liegen im Repo und
      erscheinen sonst als untracked

**Fertig, wenn** eine Teständerung an `SharedContracts` die Builds aller sieben Services
auslöst.

**Blockiert** 3, 7, 8, 12.

---

### 1. BMW-Token erneuern und Doku korrigieren

**Problem.** Der BMWConnector liefert seit 2026-08-16 nichts. Der Pod läuft
(`1/1 Running`, 0 Restarts), aber der `refresh_token` ist abgelaufen:
`Token refresh failed (HTTP 400): invalid_request`. Laut `SETUP.md` ist das ein von BMW
vorgegebener ~90-Tage-Zyklus, der ein interaktives Re-Bootstrap erfordert.

**Umfang**
- [ ] `dotnet run -- --bootstrap BMW` lokal ausführen (interaktiver BMW-Login, nur Thomas)
- [ ] Pod erneuern mit `kubectl -n smarthome delete pod <bmwconnector-pod>` —
      **nicht** `rollout restart`, das patcht das Deployment-Template und gilt ArgoCD
      mit `selfHeal: true` als Drift
- [ ] `DATAPOINTS.md`: Die Aussage „`header` — **Mini only**" und „Not available for BMW
      via streaming API" ist falsch. Der BMW liefert `battery` (gemessen: 83 %)
- [ ] `SETUP.md`: `rollout restart` durch `delete pod` ersetzen, mit Begründung ArgoCD

**Fertig, wenn** `daten/Fahrzeug/BMW/Status` einen `Zeitpunkt` von heute trägt.

---

### 2. Health-Semantik des BMWConnectors korrigieren

**Problem.** Der Ausfall blieb 35 Tage unsichtbar. `HealthRegistry.GetResult()` liefert
bei Teilausfall `Degraded`, und `app.MapHealthChecks("/healthz/ready")` gibt für
`Degraded` genau wie für `Healthy` HTTP 200 zurück. Die Probe war zufrieden, Kubernetes
tat nichts. Umgekehrt sind Liveness und Readiness auf **denselben** Checksatz gemappt —
fielen beide Fahrzeuge aus, würde der Pod neu starten, was einen abgelaufenen Token
nicht repariert.

**Umfang**
- [ ] Liveness von der Fahrzeugverbindung entkoppeln: sie prüft nur, ob der Prozess
      bedienbar ist. Ein Neustart repariert keinen Token
- [ ] Readiness so mappen, dass `Degraded` nicht als betriebsbereit durchgeht
- [ ] Proaktive Warnung: Token-Alter aus dem Kubernetes-Secret bewerten und ab
      > 80 Tagen warnen, bevor der Ausfall eintritt
- [ ] Prüfen, ob dieselbe Probe-Verwechslung in den anderen Services steckt
      (KebaConnector, ChargingController, EnphaseConnector, ShellyConnector, DataHub)

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
- [ ] Harter Schnitt, gemeinsam mit 7 ausrollen — kein Parallelbetrieb
- [ ] Telegraf-Altlast prüfen und entfernen: `Kubernetes/microk8s/InfluxDB/` schreibt noch
      gegen InfluxDB 2 und ist im k3s-Cluster nicht mehr deployt

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
      `PowerFromBattery` und `ChargingGetData.CurrentChargingPower` behaupten „mW",
      führen aber **Watt** (`Program.cs:206-212` teilt durch 1000). Muss vor der
      Zurechnung stimmen
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

**Umfang**
- [ ] ChargingController publiziert `daten/Laden/M3/<Box>/Ladesitzung` mit `SitzungsId`,
      Beginn, Ende, `Zustand: laufend | beendet`, Zählerständen und Ladezeit — er ist der
      **einzige** Autor dieses Topics (der KebaConnector publiziert nur `Status`, Punkt 7)
- [ ] RulesEngine publiziert `daten/Laden/M3/<Box>/Zuordnung` mit derselben `SitzungsId`
- [ ] DataHub führt beide zusammen und schreibt die Tabelle `ladesitzungen` — **nur bei
      übereinstimmender `SitzungsId`**, sonst nichts schreiben und protokollieren
- [ ] Nur `wallbox` als Tag; `fahrzeug` und `vertrauen` als Felder (Idempotenz bei
      Retain-Wiedergabe), plus Wächter auf bereits geschriebene `SitzungsId`
- [ ] Sitzungen mit 0 kWh werden geschrieben, aber im Dashboard ausgeblendet
- [ ] Sitzungs-Grid, `CarSelection`-Filter und Excel-/PDF-Export aus
      `ChargingOverview.razor` entfernen, stattdessen Verweis auf das Dashboard
- [ ] `ChargingSessionService.cs` ersatzlos löschen

**Abhängig von** 11, 12.

---

### 14. Grafana-Dashboard „Laden"

Löst `wallbox-charging-dashboard.json` ab; dessen Leistungskurven wandern als Verlaufsteil
hinein.

**Umfang**
- [ ] Kennzahlen: kWh gesamt im Zeitraum, PV-Anteil in Prozent, Anzahl Sitzungen
- [ ] Protokolltabelle: Beginn, Wallbox, Fahrzeug, Vertrauen, Steckdauer, Ladezeit, kWh,
      davon PV/Batterie/Netz, PV-Anteil
- [ ] Balken kWh je Fahrzeug und Monat, gestapelt nach Quelle
- [ ] Verlauf: Ladeleistung gestapelt nach Quelle
- [ ] Variablen: Fahrzeug, Wallbox, Schalter „nur sichere Zuordnungen"
- [ ] Zeitraumsummen **aus den Zählern** (`MAX − MIN`), nicht aus der Sitzungstabelle —
      dann sind Monatsgrenzen kein Thema
- [ ] Altes Dashboard entfernen, `grafana-dashboards/README.md` und
      `influxdb-reference.md` um `ladesitzungen` und die neuen Measurements ergänzen

**Abhängig von** 13.

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

## Nachträglich ergänzt

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
- [ ] Sofortmaßnahme ohne Contract-Änderung: MQTTnet liefert bei einer Retain-Zustellung
      das Retain-Flag mit. Eine so gekennzeichnete Nachricht darf den Sollwert zwar
      **setzen**, aber den Frischezähler **nicht zurücksetzen**
- [ ] Dauerhaft: `Zeitpunkt` im Kommando-Payload, Alter daraus statt aus der Empfangszeit
      — fällt mit Punkt 7/8 ohnehin an
- [ ] Testfall: Controller schweigt, Connector startet neu → Freigabe muss nach
      `StaleReleaseAfter` erfolgen
- [ ] Prüfen, ob dieselbe Verwechslung anderswo steckt — die `RulesEngine` wertet
      `MaxStatusAge` ebenfalls gegen die Empfangszeit aus

**Abhängig von** nichts.

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

**Abhängig von** 8 — vorher gäbe es das Topic `daten/Fahrzeug/+/Status` noch nicht, und
es müsste zweimal gegen zwei Schemata gebaut werden.

**Welle E, seriell nach 13.** Kollidiert mit 8 und 13 im DataHub, deshalb nicht parallel zu
diesen. Punkt 14 hängt nicht davon ab und kann davor oder danach laufen; Punkt 15 dagegen
setzt 17 zwingend voraus.

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

**Umfang**
- [ ] Vorrang umkehren oder absichern: im Cluster-Betrieb gewinnt das Secret. Eine
      Env-Var darf lokal überschreiben, aber nicht zurückschreiben
- [ ] Rückschreiben nur mit Plausibilitätsprüfung (GCID und CLIENT_ID sind UUIDs, also
      Format und Länge prüfbar) und explizitem Opt-in, nicht als Nebenwirkung
- [ ] Beim Überschreiben den ersetzten Wert maskiert protokollieren — der Vorfall war nur
      an den Byte-Längen im Secret erkennbar
- [ ] Dieselbe Rückschreib-Logik in den anderen Connectoren prüfen (VW, Keba, Shelly,
      Enphase)
- [ ] Erwägen, `bmwconnector-credentials` versioniert zu hinterlegen (SealedSecret im
      Deployments-Repo), damit es überhaupt eine Wiederherstellungsquelle gibt

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

## Offene Punkte außerhalb dieses Vorhabens

- **Benachrichtigungen.** `Nachrichten/#` wurde nur von der Flutter-App gelesen. Ein
  Meldeweg für die Web-PWA fehlt danach — eigenes Thema.
- **WiCAN.** Für den VW alternativlos, sobald das Portal wegfällt: Nur ein
  fahrzeugseitiger Sensor kann zeitnah melden, dass angesteckt wurde. Beim BMW vorher
  die OBD-Sperre neuerer Modelle prüfen; CarData liefert dort bereits alles Nötige.
