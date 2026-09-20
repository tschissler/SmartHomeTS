# RulesEngine

Zentraler Service für Steuerregeln des SmartHome-Systems. Regeln sind bewusst
simple, pure C#-Klassen (`Rules/`), vollständig durch Unit-Tests abgedeckt —
kein Framework, keine dynamische Registrierung. Das MQTT-Handling liegt einmal
zentral in `Program.cs`, außerhalb der Regeln.

**Arbeitsweise:**
- Regel ändern: Test anpassen → rot → Code anpassen → grün → Deployment.
- Neue Regel: neue Klasse mit Tests → Aufruf in `Program.cs` → Deployment.

Mittelfristig ziehen weitere Regeln (aus Node-RED und dem Thermostat-Service)
hierher um — jeweils dann, wenn sie ohnehin angefasst werden.

## Regeln

### MixerPositionRule

Steuert die Zielposition der Heizkreis-Mischer (M1) abhängig vom
Betriebsmodus der Hoval-Wärmepumpe:

- FA_Status = `4` (Warmwasserladung, als Konstante in `Program.cs`) und
  Status frisch → `close`
- alles andere (unbekannte Werte, veralteter Status) → `open`
  (Failsafe: offen kostet nur Effizienz, nie Komfort)

### VehicleAssignmentRule

Ermittelt, **welches Fahrzeug an welcher Wallbox lädt**. Konzept und
Begründungen: `Docs/Fahrzeug-Wallbox-Zuordnung.md`.

Tragender Satz: **Die Wallbox ist die Wahrheit, und aus dem Schweigen eines
Fahrzeugs folgt nichts.** Ein Auto, das nichts meldet, ist kein Auto, das
anderswo steht — es hat womöglich nur nichts zu sagen. Keine Stufe schließt
aus einer fehlenden oder negativen Fahrzeugmeldung.

Evidenz, absteigend:

1. **Manueller Override** aus der Weboberfläche, gültig nur für die laufende
   `SitzungsId` → `bestaetigt`
2. **Positive Fahrzeugmeldung**, während genau eine belegte Box noch nicht
   sicher vergeben ist → `erkannt`
3. Dieselbe Bedingung bei zwei belegten Boxen, von denen eine sicher vergeben
   ist. Das ist ein Ausschluss über die **gemessene Belegung der Boxen**, nicht
   über Fahrzeugschweigen, und damit zulässig
4. **Historie** — das Fahrzeug, das zuletzt an dieser Box lud → `vermutet`
5. nichts davon → `unbekannt`

Eigenschaften:

- **Klebend je Sitzung, nur Aufwertung** (`vermutet` → `erkannt` →
  `bestaetigt`), nie ein stillschweigendes Umwerfen auf gleicher Stufe.
- **Der Override verfällt mit dem Stecker**, weil die Regel ihn nur auswertet,
  wenn seine `SitzungsId` zur laufenden passt. Niemand muss ihn zurücknehmen.
- **Der Zustand wird allein aus retained Topics wiederhergestellt** — kein
  Datenbankzugriff, keine eigene Persistenz. Deshalb publiziert der Service
  in den ersten 15 s nach dem Start nichts: sonst schriebe er ein frisches
  `unbekannt` über genau das Topic, aus dem er sich erholen soll.
- **Jedes Alter kommt aus dem `Zeitpunkt` im Payload**, nie aus der
  Empfangszeit. Beim Start kommen alle Eingänge als retained Schwall herein;
  keiner davon ist ein frisches Ereignis. Für die Fahrzeugmeldung ist die
  Messzeit `lastUpdate` maßgeblich, nicht die Veröffentlichungszeit des
  Connectors.
- **Mehrdeutigkeit erzeugt nie eine Aussage.** Zwei meldende Fahrzeuge oder
  zwei unvergebene belegte Boxen führen auf die Historie zurück — sichtbar
  als Vermutung, statt als selbstbewusster Fehlgriff.

### CoolingFlowTemperatureRule

Dreipunkt-Schrittregler für den `Mischer_FBHZ`: hält die
FBHZ-Vorlauftemperatur nahe am Sollwert (Laufzeit-Config, s. u.), damit der
Taupunktwächter nicht anspricht (zu kalt → Kondenswasser → WP-Blockierung
B:65535) und trotzdem Kühlleistung ankommt (zu warm).

- Regelt **immer**, außer die Warmwasser-Regel sagt `close` (bei WW-Bereitung
  ist der WP-Vorlauf heiß — Mischer auf würde wärmen statt kühlen).
  Zusätzliche Gates: FBHZ-Pumpe läuft (ohne Umwälzung misst der Fühler
  stehendes Wasser) und alle Eingänge frisch.
- Die Wirkrichtung (auf = kälter) gilt für die Kühlsaison. Im Heizbetrieb
  konvergiert dieselbe Logik automatisch auf „ganz auf" (warmer Vorlauf →
  Auf-Pulse) = bisheriges Verhalten; echte Heizbetriebs-Regelung ist ein
  späterer Schritt.
- Regelabweichung > Totband → kurzer Fahrpuls (`close:N` bei zu kalt,
  `open:N` bei zu warm), Pulslänge proportional zur Abweichung
  (`PulseSecondsPerKelvin`, geclampt auf Min/Max).
- Pulse werden **nur im Zyklustakt** ausgewertet (nicht pro Message) und
  **nicht retained** publiziert — ein Puls ist eine Relativbewegung, ein
  Replay nach Reconnect würde den Mischer grundlos verfahren.
- Die Warmwasser-Regel hat Vorrang: solange sie `close` sagt, gibt es keine
  Pulse. Nach jedem retained `open` (z. B. Ende der WW-Bereitung oder
  ESP32-Reconnect) macht der Mischer eine Referenzfahrt auf und der Regler
  trimmt danach wieder ein — selbstkorrigierend.

## MQTT

| Topic | Richtung | Inhalt |
|---|---|---|
| `cangateway/M1/WEZ/Status/FA_Status` | in | Betriebsmodus der WP (CAN-Gateway) |
| `cangateway/M1/FBHZ/Temperatur/Vorlauf_Ist` | in | FBHZ-Vorlauftemperatur (Regelgröße) |
| `cangateway/M1/FBHZ/Status/Pumpe` | in | FBHZ-Umwälzpumpe (1 = läuft) |
| `config/RulesEngine` | in (retained) | Laufzeit-Konfiguration als JSON, z. B. `{"CoolingFlowTargetTemperature": 18.0}` — überschreibt den Env-Default ohne Redeploy |
| `commands/MixerController/M1/Mischer_FBHZ` + `Mischer_HK` | out (retained) | Zielposition `open` / `close` |
| `commands/MixerController/M1/Mischer_FBHZ` | out (nicht retained) | Fahrpulse `open:N` / `close:N` (Sekunden) |
| `daten/Laden/+/+/Status` | in (retained) | Zustand der Wallboxen (`SitzungsId`, Steckerzustand) |
| `daten/Fahrzeug/+/Status` | in (retained) | Fahrzeugdaten (`chargerConnected`, `lastUpdate`) |
| `konfiguration/Laden/+/+/Zuordnung` | in (retained) | manuelle Korrektur aus der Weboberfläche |
| `daten/Laden/M3/<Box>/Zuordnung` | **in und out** (retained) | eigene Aussage: `SitzungsId`, `Fahrzeug`, `Vertrauen`, `Zeitpunkt`. Wird zurückgelesen, weil sie nach einem Neustart die laufende Zuordnung **und** die Historie ist |
| `status/Cluster/Dienst/RulesEngine` | out (retained, 60 s) | Service-Heartbeat: Version, Laufzeit, letzte Regelauswertung, Zustand der Health-Checks. Erscheint auf der Geräteseite der Weboberfläche — siehe `Docs/Service-Heartbeat.md` |
| `meta/RulesEngine/version` | — | **entfällt.** Ging im Heartbeat auf; der Service löscht das retained Topic beim Start mit leerem Payload |

Die Zielposition wird retained publiziert — einmal beim Start und danach nur
bei **Entscheidungsänderungen**. Intern wertet der Service die Regel zyklisch
aus (Default 60 s), damit auch die Veraltet-Prüfung (`MaxStatusAge`) greift,
wenn das CAN-Gateway keine Messages mehr liefert.

**Manueller Eingriff:** Zielposition direkt retained auf das Command-Topic
publizieren — sie bleibt stehen, bis die RulesEngine das nächste Mal anders
entscheidet. Für längere Eingriffe den Service auf 0 Replicas skalieren.

**Monitoring (geplant, siehe MixerController-README):** Soll (`commands/...`)
gegen Ist (`daten/Heizung/.../Mischersteuerung/...` vom ESP32) korrelieren;
Abweichung über Fahrtdauer + Puffer → Alarm.

## Konfiguration (Umgebungsvariablen, Präfix `RulesEngineSettings__`)

| Variable | Default | Bedeutung |
|---|---|---|
| `MqttBroker` / `MqttPort` | `smarthomepi2` / `32004` | Broker-Adresse |
| `HealthCheckPort` | `8080` | HTTP-Port für `/health`, `/healthz`, `/ready` |
| `FaStatusTopic` | `cangateway/M1/WEZ/Status/FA_Status` | Quelle des WP-Status |
| `MixerCommandTopics` | beide M1-Mischer | Ziel-Topics, kommasepariert |
| `MaxStatusAgeMinutes` | `15` | Ältere Eingänge gelten als veraltet → open bzw. keine Pulse |
| `EvaluationIntervalSeconds` | `60` | Intervall der internen Regel-Auswertung (publiziert nur bei Änderung) |
| `FbhzFlowTempTopic` | `cangateway/M1/FBHZ/Temperatur/Vorlauf_Ist` | Quelle der FBHZ-Vorlauftemperatur |
| `FbhzPumpTopic` | `cangateway/M1/FBHZ/Status/Pumpe` | Quelle des FBHZ-Pumpenstatus |
| `FbhzMixerCommandTopic` | `commands/MixerController/M1/Mischer_FBHZ` | Ziel-Topic der Fahrpulse |
| `ConfigTopic` | `config/RulesEngine` | Retained JSON-Konfiguration zur Laufzeit |
| `CoolingFlowTargetTemperature` | `15.0` | Vorlauf-Sollwert im Kühlbetrieb (°C), nur Startup-Default — zur Laufzeit über das Config-Topic setzen (bei Taupunkt-Auslösungen anheben) |
| `CoolingFlowDeadbandKelvin` | `0.5` | Totband um den Sollwert (K) |
| `PulseSecondsPerKelvin` | `5.0` | Pulslänge pro Kelvin Regelabweichung |
| `MinPulseSeconds` / `MaxPulseSeconds` | `2` / `20` | Begrenzung der Pulslänge |
| `MaxWallboxStatusAgeMinutes` | `5` | Ältere Box-Zustände gelten als unbekannt — die Zuordnung dieser Box wird dann **nicht angefasst** |
| `MaxVehicleReportAgeHours` | `6` | Altersfenster für eine positive Fahrzeugmeldung, wenn der Sitzungsbeginn aus der Box-Uhr stammt |
| `AssignmentRestoreSeconds` | `15` | Wartezeit nach dem Start, bevor eine Zuordnung publiziert wird (retained Wiederherstellung) |

## Build & Test

```bash
dotnet build RulesEngine/RulesEngine.sln
dotnet test RulesEngine/RulesEngine.sln
```

## Deployment

CI (`.github/workflows/RulesEngine.yml`): Tests → arm64-Image →
`tschissler/rulesengine` (nur arm64 — läuft auf den Raspberry-Nodes;
amd64-Nodes bleiben für leistungshungrige Anwendungen frei).

Deployment auf den Cluster erfolgt über **ArgoCD**; das Helm-Chart samt
Application-Manifest liegt im GitOps-Repo `SmartHomeDeployments`
(forgejo.intern, `RulesEngine.yaml` + `RulesEngine/`). Der
argocd-image-updater zieht neue SemVer-Tags automatisch nach.
