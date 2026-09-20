# Backlog: Laden, Fahrzeugzuordnung und Topic-Aufräumung

Abzuarbeiten in dieser Reihenfolge. Jeder Punkt ist für sich abgeschlossen und lässt das
System lauffähig zurück. Erledigte Punkte werden abgehakt, nicht gelöscht — die
Reihenfolge soll nachvollziehbar bleiben.

Konzept und Begründungen: `Fahrzeug-Wallbox-Zuordnung.md`, `Ladeprotokoll.md`,
`MQTT-Topic-Konvention.md`.

---

## Unabhängige Vorarbeiten

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
      einen Heartbeat im Format der ESP32-Geräte (Version, Uptime, letzte erfolgreiche
      Aktion, fachlicher Zustand) — sinnvollerweise als gemeinsamer Helfer in `Libs/`
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
- [ ] Neue Topics `daten/Laden/M3/{Garage,Stellplatz}/{Status,Ladesitzung}`, retained
- [ ] Sitzungsende als Feld `Zustand: laufend | beendet` im retained `Ladesitzung`-Payload
      statt als QoS-0-Ereignis auf `…_ChargingSessionEnded`
- [ ] Wallboxen heißen `Garage` und `Stellplatz` — der Hersteller verschwindet aus den
      Bezeichnern

**Achtung.** In den aufgezeichneten Reports steht bei der laufenden Sitzung `TimeQ: 0`
(„not synced time"). Die Uhr der Box ist also möglicherweise nicht synchronisiert. Der
`SitzungsBeginn` sollte deshalb primär aus der selbst beobachteten Steckerflanke
stammen und nur ersatzweise aus der Box-Zeit.

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
- [ ] Zuordnung in den `Ladesitzung`-Payload, nicht in ein eigenes Topic
- [ ] Web: Vertrauensgrad sichtbar (grau + Fragezeichen bei Vermutung), Korrektur per Klick
- [ ] Zustand nach Neustart aus den retained Topics wiederherstellen

**Abhängig von** 6, 10.

---

### 12. Energieaufteilung PV / Batterie / Netz

Konzept und Begründungen: `Ladeprotokoll.md`.

**Regel.** Anteilig am Quellenmix, mit dem **Verbrauch** als Nenner (nicht der Erzeugung):

```
N = max(0, PowerFromGrid)   B = max(0, PowerFromBattery)   V = PowerToHouse
P = max(0, V − N − B)       p = P/V   n = N/V   b = B/V
je Box: PV = p·P_Box,  Netz = n·P_Box,  Batterie = b·P_Box
```

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
- [ ] Ladezeit (Leistung > 0) getrennt von der Steckdauer mitzählen
- [ ] DataHub schreibt die Zähler wie jeden anderen Energiewert nach `energy_values`

**Fertig, wenn** `MAX(value_cumulated_kwh) − MIN(...)` über einen Tag für die drei Zähler
plausibel zur Boxenergie passt.

**Abhängig von** 8.

---

### 13. Ladesitzungs-Tabelle, Grid aus der Web-UI entfernen

**Umfang**
- [ ] ChargingController publiziert `daten/Laden/M3/<Box>/Ladesitzung` mit `SitzungsId`,
      Beginn, Ende, `Zustand: laufend | beendet`, Zählerständen und Ladezeit
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

**Abhängig von** 10.

---

## Offene Punkte außerhalb dieses Vorhabens

- **Benachrichtigungen.** `Nachrichten/#` wurde nur von der Flutter-App gelesen. Ein
  Meldeweg für die Web-PWA fehlt danach — eigenes Thema.
- **WiCAN.** Für den VW alternativlos, sobald das Portal wegfällt: Nur ein
  fahrzeugseitiger Sensor kann zeitnah melden, dass angesteckt wurde. Beim BMW vorher
  die OBD-Sperre neuerer Modelle prüfen; CarData liefert dort bereits alles Nötige.
