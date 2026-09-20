# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SmartHomeTS is a production smart home platform running on a self-hosted Kubernetes cluster (k3s on Raspberry Pi nodes). It uses an **event-driven microservices architecture with MQTT as the central message bus**. The system monitors and controls solar energy, EV charging, heating, temperature sensors, and vehicle integrations.

## Architecture

**Data flow**: Physical devices → ESP32 firmware → MQTT (Mosquitto) → Connector services → InfluxDB 3 → Grafana/Blazor Web

**Key layers**:
- **ESP32 firmware** (C++/Arduino via PlatformIO): 18 sensor/controller projects in `ESP32Firmwares/`, sharing libraries from `ESP32Firmwares/SharedLibs/`
- **Connector services** (.NET/Python): Bridge external APIs to MQTT — `BMWConnector`, `VWConnector`, `EnphaseConnector`, `ShellyConnector`, `KebaConnector`
- **Business logic services** (.NET): `ChargingController` (EV charging optimization), `Thermostat` (climate control), `SmartHome.DataHub` (central data processing)
- **Presentation**: `SmartHome.Web` (Blazor Server with Syncfusion), Grafana dashboards
- **AI tooling**: `MCPServer` exposes InfluxDB schema/data via Model Context Protocol

**Data storage**: InfluxDB 3 with primary table `energy_values` using tags (category, sub_category, device, location, measurement, sensor_type) and fields (value_kwh, value_cumulated_kwh).

**Infrastructure**: k3s cluster setup and Ansible playbooks in `Kubernetes/k3s/`, further Ansible playbooks in `ansible/`, 19 GitHub Actions workflows in `.github/workflows/`. The running services are deployed from a separate repository (`forgejo.intern/thomas/SmartHomeDeployments`), not from here; the superseded MicroK8s manifests sit in `Depricated/`.

## Build Commands

### .NET services (each has its own .sln)
```bash
dotnet build ChargingController/ChargingController.sln
dotnet build SmartHome.DataHub/SmartHome.DataHub.sln
dotnet build SmartHome.Web/SmartHome.Web.sln
# Same pattern for: Thermostat, KebaConnector, ShellyConnector, EnphaseConnector, MCPServer
```

### Run tests
```bash
# All tests in a solution
dotnet test ChargingController/ChargingController.sln

# Single test project
dotnet test ChargingController/ChargingControllerTests/ChargingControllerTests.csproj
dotnet test Libs/ShellyLib/ShellyLibTests/ShellyLibTests.csproj
dotnet test EnphaseConnector/EnphaseLib.Tests/EnphaseLib.Tests.csproj

# Single test
dotnet test ChargingControllerTests/ChargingControllerTests.csproj --filter "FullyQualifiedName~TestMethodName"
```

Test framework: **xUnit** with **FluentAssertions**. ChargingController uses **Excel-based data-driven tests** (ExcelDataReader) with test scenarios defined in `.xlsx` files.

### ESP32 firmware (PlatformIO)
```bash
cd ESP32Firmwares/SMLSensor.Firmware
pio run                    # Build
pio run -t upload          # Build and upload
pio device monitor         # Serial monitor
```

### Python services
```bash
pip install -r BMWConnector/requirements.txt
pip install -r VWConnector/requirements.txt
```

## CI/CD

Every push to `main` triggers a build. **The workflows only build — they do not deploy.**
1. **Build** (GitHub-hosted runner): Multi-arch Docker build (amd64+arm64), push to Docker Hub
   as `tschissler/<image>:1.0.{github.run_number}` plus `latest`
2. **Rollout** (no CI involvement): the ArgoCD Image Updater picks up the new tag within ~2 min,
   commits it into the deployments repo (`forgejo.intern/thomas/SmartHomeDeployments`, the
   service's `values.yaml`), and ArgoCD syncs it. A deploy is therefore visible as a
   `build: automatic update of <service>` commit in that repo, not as a CI step.

Never `kubectl set image` or `kubectl apply` against the cluster: every ArgoCD app runs with
`selfHeal: true` and reverts manual changes. The strategy and its pitfalls are documented in
`docs/update-strategie.md` of the deployments repo.

ESP32 firmware CI uploads `.bin` to Azure Blob Storage, then publishes an MQTT message so devices auto-update via OTA.

Docker image versions use format `1.0.{github.run_number}`.

## Documentation

- `Docs/MQTT-Topic-Konvention.md` — **binding naming rule for all new topics**
  (`art/Kategorie/[Ort/]Geraet/Aspekt`, German from the category level, JSON payload with
  `Zeitpunkt`, state retained / events never). Read before adding any topic
- `Docs/Fahrzeug-Wallbox-Zuordnung.md` — which vehicle charges at which wallbox: the
  wallbox is the source of truth, why elimination does not work, confidence levels,
  UI concept, PV/battery/grid attribution
- `Docs/Ladeprotokoll.md` — charging log: PV/battery/grid attribution rule, the three
  virtual meters per wallbox, the `ladesitzungen` table, who publishes what, Grafana scope
- `Docs/Fahrzeugdaten-in-InfluxDB.md` — which vehicle value goes into which table, the tag
  set, **why the timestamp is `lastUpdate` and not the arrival time**, why that alone makes
  the write idempotent, and what the fields deliberately left out are
- `Docs/Backlog-Laden.md` — ordered backlog for charging, vehicle assignment, charging log
  and the topic cleanup; work it top to bottom
- `Docs/ChargingController-Regelkreis.md` — control loop of the EV charging (smoothing,
  contactor protection delays, tuning parameters, diagnostics)
- `Docs/InfluxDB-Modellierung.md` — **binding rule for every new measurement**: which
  table, field or tag, missing values, timestamps. Read before adding anything to
  InfluxDB. If a rule does not fit your case or does not answer it, discuss it with
  Thomas instead of deciding alone — a table that carries data can only be changed
  at a loss
- `Docs/microK8s/Setup MicroK8s.md` — cluster setup
- **Grafana dashboards no longer live here** — they moved to `forgejo.intern/thomas/Grafana`
  (dashboards, the API export script, the Sankey generator, the InfluxDB field reference).
  The unit is the Grafana instance, not a domain: it also serves climate, heat pump and
  cistern. The move made the knowledge-bearing files versionable — in this public repo
  they had to stay gitignored

## Key Patterns

- **MQTT is the integration backbone**: All services communicate via MQTT topics through Mosquitto broker
- **Each .NET service has its own solution file** — there is no monolithic solution
- **Secrets are managed via** GitHub Secrets (CI) and Kubernetes Secrets (runtime). `Secrets.cs` files are gitignored
- **.NET target frameworks vary**: .NET 10.0 (DataHub, Web, ChargingController), .NET 9.0 (MCPServer), .NET 8.0 (connectors)
- **Shared .NET libraries** in `Libs/` (ShellyLib, MQTTControllerLib, HelpersLib) and `SharedContracts/`
- **`Depricated/` folder** contains legacy/replaced projects — avoid modifying these

## Recommended Skills

Use these skills at the appropriate moments:

- **`/simplify`** — Run after writing new code (connector changes, converters, etc.) to review for quality and efficiency before committing.
- **`/review`** — Run when a PR is open to review the diff in full context before merging.
- **`/security-review`** — Run before merging any changes that touch secrets handling (InfluxDB tokens, connector credentials, `Secrets.cs`).
- **`/less-permission-prompts`** — Run when `dotnet build/test` or `kubectl` approval prompts become frequent during development.
- **`/loop`** — Use to poll after deployment (e.g., query InfluxDB every 60s to verify new measurements are flowing).

