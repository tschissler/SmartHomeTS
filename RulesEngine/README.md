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

## MQTT

| Topic | Richtung | Inhalt |
|---|---|---|
| `cangateway/M1/WEZ/Status/FA_Status` | in | Betriebsmodus der WP (CAN-Gateway) |
| `commands/MixerController/M1/Mischer_FBHZ` + `Mischer_HK` | out (retained) | Zielposition `open` / `close` |
| `meta/RulesEngine/version` | out (retained) | Service-Version |

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
| `MaxStatusAgeMinutes` | `15` | Älterer FA_Status gilt als veraltet → open |
| `EvaluationIntervalSeconds` | `60` | Intervall der internen Regel-Auswertung (publiziert nur bei Änderung) |

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
