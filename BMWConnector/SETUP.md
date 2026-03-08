# BMWConnector Setup Guide

This guide covers everything needed to authenticate the BMWConnector with BMW's CarData streaming API,
both for local development and for the Kubernetes deployment.

The connector uses the **Kubernetes API** as the single source of truth for all configuration and tokens.
No local files, no kubectl binary required at runtime — just `~/.kube/config` on your dev machine.

---

## Prerequisites

- .NET 10 SDK
- `kubectl` configured to reach your k3s cluster (for one-time Secret creation)
- `~/.kube/config` accessible on your dev machine (used by the connector at runtime to read Secrets)
- A BMW Connected Drive account (for the BMW) and a MINI Connected account (for the Mini)
- Access to the [BMW CarData Developer Portal](https://developer.bmw.com/products/cardata-streaming)

---

## Step 1: Obtain your CLIENT_ID and GCID

These two values are required per vehicle account. You only configure them once, in the Kubernetes Secret.

### CLIENT_ID

The CLIENT_ID is the OAuth 2.0 client ID issued by BMW for the CarData streaming scope.

1. Log in at the [BMW CarData Developer Portal](https://developer.bmw.com/products/cardata-streaming)
https://www.bmw.de/de-de/mybmw
https://www.mini.de/de-de/mymini/vehicle-overview
2. Navigate to your application / subscription
3. Copy the **Client ID** shown there

You need one CLIENT_ID **per account** — BMW account and MINI account are separate.

### GCID (Global Customer ID)

The GCID is your BMW account UUID used as the MQTT username on the CarData broker.

- It appears in the verification URL printed during bootstrap (look for the UUID component)
- Or decode the `id_token` JWT at [jwt.io](https://jwt.io) and look for the `sub` claim

---

## Step 2: Create the Kubernetes credentials Secret (one-time)

This is the **only manual configuration step**. Both local development and the Kubernetes pod
read CLIENT_ID and GCID from this Secret via the Kubernetes API.

```bash
kubectl -n smarthome create secret generic bmwconnector-credentials \
  --from-literal=BMW_CLIENT_ID="your-bmw-client-id-here" \
  --from-literal=BMW_GCID="your-bmw-gcid-here" \
  --from-literal=Mini_CLIENT_ID="your-mini-client-id-here" \
  --from-literal=Mini_GCID="your-mini-gcid-here"
```

To update later:

```bash
kubectl -n smarthome create secret generic bmwconnector-credentials \
  --from-literal=BMW_CLIENT_ID="..." ... \
  --dry-run=client -o yaml | kubectl apply -f -
```

---

## Step 3: Authenticate (bootstrap)

Run the connector from your local machine. If tokens are missing it automatically starts the auth flow:

```fish
cd BMWConnector
dotnet run
```

Or explicitly for one vehicle at a time (bootstrap + immediately start that vehicle's service):

```fish
dotnet run -- --bootstrap BMW
dotnet run -- --bootstrap Mini
```

The bootstrap flow will:
1. Read CLIENT_ID and GCID from the `bmwconnector-credentials` Kubernetes Secret (prompts interactively if missing)
2. Request a device code from BMW's OAuth endpoint
3. Print a URL — open it in your browser and log in with the vehicle's BMW account
4. Press Enter once you see "Anmeldung erfolgreich / Login successful"
5. Save tokens directly to the Kubernetes Secret (`bmwconnector-bmw-tokens` / `bmwconnector-mini-tokens`)
6. Start the vehicle's service immediately — no restart needed

Expected output:
```
Tokens saved to Kubernetes Secret 'bmwconnector-bmw-tokens'.
Bootstrap complete. You can now start the service normally.
```

No local files are created. Tokens live exclusively in the Kubernetes Secrets.

### Running a single vehicle (without bootstrap)

To run only one vehicle's service — useful for testing or when only one is authenticated:

```fish
dotnet run -- --vehicle BMW
dotnet run -- --vehicle Mini
```

This skips the other vehicle entirely. Without `--vehicle`, both BMW and Mini are started.

---

## Step 4: Verify locally

Start the connector:

```fish
dotnet run
```

Expected log lines:
- `[BMW] Loaded tokens from Kubernetes Secret.`
- `[BMW] Connecting to BMW CarData broker...`
- `[BMW] Connected. Subscribing to {GCID}/+`
- `[BMW] Published to data/charging/BMW`

Verify data arrives on Mosquitto:

```fish
mosquitto_sub -h mosquitto.intern -t "data/charging/BMW" -v
mosquitto_sub -h mosquitto.intern -t "data/charging/Mini" -v
```

---

## BMW CarData Developer Portal — required data points

When setting up your application in the [BMW CarData Developer Portal](https://developer.bmw.com/products/cardata-streaming),
enable the following data points for the CarData Streaming subscription.
The connector subscribes to the wildcard topic `{GCID}/+` and extracts exactly these fields:

| Data point (field name) | Description | Used for |
|---|---|---|
| `vehicle.drivetrain.batteryManagement.header` | State of charge (%) | Battery level |
| `vehicle.drivetrain.electricEngine.charging.status` | Charging status string | Charging status |
| `vehicle.powertrain.electric.battery.stateOfCharge.target` | Target SoC (%) | Charging target |
| `vehicle.drivetrain.electricEngine.charging.timeRemaining` | Minutes until charge complete | Estimated end time |
| `vehicle.body.chargingPort.status` | Port status (`CONNECTED` / other) | Charger connected flag |
| `vehicle.drivetrain.electricEngine.kombiRemainingElectricRange` | Remaining range (km) | Range display |
| `vehicle.trip.segment.end.travelledDistance` | Odometer / trip distance (km) | Mileage |
| `vehicle.cabin.infotainment.navigation.currentLocation.latitude` | GPS latitude | Position |
| `vehicle.cabin.infotainment.navigation.currentLocation.longitude` | GPS longitude | Position |
| `vehicle.isMoving` | Boolean — vehicle in motion | Moving state |
| `vehicle.powertrain.electric.battery.charging.power` | Charging power (kW) | Live charging power |

Any other fields pushed by BMW are silently ignored. You can enable additional fields in the portal
without code changes — add the corresponding `case` in `VehicleState.Apply()` to consume them.

---

## Token lifecycle

| Token | Expires | Managed by |
|---|---|---|
| `id_token` | 60 min | Service refreshes every 50 min automatically |
| `access_token` | 60 min | Refreshed alongside id_token |
| `refresh_token` | ~90 days | Must re-run bootstrap when expired |

On each 50-minute refresh, the service writes all three updated tokens back to the Kubernetes Secret.
The service logs `[Vehicle] Proactive token refresh, reconnecting...` — this is normal.

---

## Re-authentication checklist (~every 90 days)

When auth starts failing (refresh_token expired):

1. Run bootstrap again — reads credentials from k8s Secret automatically:
   ```fish
   dotnet run -- --bootstrap BMW
   dotnet run -- --bootstrap Mini
   ```
2. Bootstrap updates the Kubernetes token Secrets immediately
3. Restart the pod to reload:
   ```bash
   kubectl -n smarthome rollout restart deployment/bmwconnector
   ```

---

## Kubernetes deployment

The connector uses the Kubernetes API directly — no Secret volume mounts or kubectl binary needed in the pod.
The pod requires a ServiceAccount with permission to read and update the token Secrets.

### Required Secrets

| Secret | Contents | Created by |
|---|---|---|
| `bmwconnector-credentials` | CLIENT_ID + GCID for both vehicles | Step 2 (manual, once) |
| `bmwconnector-bmw-tokens` | BMW OAuth tokens | Bootstrap (Step 3) |
| `bmwconnector-mini-tokens` | Mini OAuth tokens | Bootstrap (Step 3) |

### Required RBAC

The pod's ServiceAccount needs the following Role (included in the Helm chart):

```yaml
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: bmwconnector
  namespace: smarthome
rules:
- apiGroups: [""]
  resources: ["secrets"]
  resourceNames:
    - bmwconnector-credentials
    - bmwconnector-bmw-tokens
    - bmwconnector-mini-tokens
  verbs: ["get", "update", "replace", "create"]
```
