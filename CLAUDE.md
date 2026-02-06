# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

SmartHomeTS is a production smart home platform running on a self-hosted Kubernetes cluster (k3s on Raspberry Pi nodes). It uses an **event-driven microservices architecture with MQTT as the central message bus**. The system monitors and controls solar energy, EV charging, heating, temperature sensors, and vehicle integrations.

## Architecture

**Data flow**: Physical devices → ESP32 firmware → MQTT (Mosquitto) → Connector services → InfluxDB 3 → Grafana/Blazor Web/Flutter App

**Key layers**:
- **ESP32 firmware** (C++/Arduino via PlatformIO): 18 sensor/controller projects in `ESP32Firmwares/`, sharing libraries from `ESP32Firmwares/SharedLibs/`
- **Connector services** (.NET/Python): Bridge external APIs to MQTT — `BMWConnector`, `VWConnector`, `EnphaseConnector`, `ShellyConnector`, `KebaConnector`
- **Business logic services** (.NET): `ChargingController` (EV charging optimization), `Thermostat` (climate control), `SmartHome.DataHub` (central data processing)
- **Presentation**: `SmartHome.Web` (Blazor Server with Syncfusion), `smarthome_app` (Flutter), Grafana dashboards
- **AI tooling**: `MCPServer` exposes InfluxDB schema/data via Model Context Protocol

**Data storage**: InfluxDB 3 with primary table `energy_values` using tags (category, sub_category, device, location, measurement, sensor_type) and fields (value_kwh, value_cumulated_kwh).

**Infrastructure**: Kubernetes manifests in `Kubernetes/`, Ansible playbooks in `ansible/`, 21 GitHub Actions workflows in `.github/workflows/`.

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

Shared libraries are referenced via `lib_dir = ../SharedLibs` in each project's `platformio.ini`.

### Python services
```bash
pip install -r BMWConnector/requirements.txt
pip install -r VWConnector/requirements.txt
```

### Flutter app
```bash
cd smarthome_app
flutter pub get
flutter run
```

## CI/CD

Every push to `main` triggers deployment. Workflows follow a two-stage pattern:
1. **Build** (GitHub-hosted runner): Multi-arch Docker build (amd64+arm64), push to Docker Hub (`tschissler/*`)
2. **Deploy** (self-hosted runner): Apply K8s manifests, update deployment image

ESP32 firmware CI uploads `.bin` to Azure Blob Storage, then publishes an MQTT message so devices auto-update via OTA.

Docker image versions use format `1.0.{github.run_number}`.

## Key Patterns

- **MQTT is the integration backbone**: All services communicate via MQTT topics through Mosquitto broker
- **Each .NET service has its own solution file** — there is no monolithic solution
- **Secrets are managed via** GitHub Secrets (CI) and Kubernetes Secrets (runtime). `Secrets.cs` files are gitignored
- **.NET target frameworks vary**: .NET 10.0 (DataHub, Web), .NET 9.0 (MCPServer), .NET 8.0 (ChargingController, connectors)
- **Shared .NET libraries** in `Libs/` (ShellyLib, MQTTControllerLib, HelpersLib) and `SharedContracts/`
- **`Depricated/` folder** contains legacy/replaced projects — avoid modifying these

## Copilot Instructions

When working with Azure resources, use Azure best practices.
