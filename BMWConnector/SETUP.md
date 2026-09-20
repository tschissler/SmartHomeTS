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
cd BMWConnector/BMWConnector
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
- `[BMW] Output topic: 'daten/Fahrzeug/BMW/Status' (no BMW_OUTPUT_TOPIC set, using the convention).`
- `[BMW] Published to daten/Fahrzeug/BMW/Status`

Verify data arrives on Mosquitto:

```fish
mosquitto_sub -h mosquitto.intern -t "daten/Fahrzeug/+/Status" -v
```

### Raw message debugging

Set `BMW_DEBUG_RAW=true` to forward the unprocessed BMW streaming payloads to `debug/{vehicle}/raw` on Mosquitto. Useful for inspecting field names, types, and values directly from the BMW broker.

```fish
# Locally
BMW_DEBUG_RAW=true dotnet run
```

> **Do not set this via `kubectl set env` on the cluster.** Like `rollout restart`, it patches
> the Deployment template, and the ArgoCD application runs with `selfHeal: true` — ArgoCD
> reverts the change (and may revert it mid-debugging). To enable raw debugging in the cluster,
> change the value in the service's `values.yaml` in the deployments repo and let ArgoCD sync it,
> or just run the connector locally with `BMW_DEBUG_RAW=true` as shown above.

Subscribe while the car is charging:

```fish
mosquitto_sub -h mosquitto.intern -t "debug/bmw/raw" -v
mosquitto_sub -h mosquitto.intern -t "debug/mini/raw" -v
```

---

## BMW CarData Developer Portal — required data points

Register exactly these fields in the [BMW CarData Developer Portal](https://developer.bmw.com/products/cardata-streaming).
The connector ignores everything else — unrecognised fields are logged as `Unknown field (not mapped)`.

For full field descriptions see [DATAPOINTS.md](DATAPOINTS.md).

```
vehicle.body.chargingPort.status
vehicle.drivetrain.batteryManagement.header
vehicle.drivetrain.batteryManagement.maxEnergy               # BMW only
vehicle.drivetrain.electricEngine.charging.status
vehicle.drivetrain.electricEngine.charging.timeRemaining
vehicle.drivetrain.electricEngine.kombiRemainingElectricRange
vehicle.isMoving
vehicle.cabin.infotainment.navigation.currentLocation.latitude
vehicle.cabin.infotainment.navigation.currentLocation.longitude
vehicle.powertrain.electric.battery.charging.power
vehicle.powertrain.electric.battery.stateOfCharge.target   
vehicle.vehicle.travelledDistance
```

> **Note:** `header` (BMW + Mini) = real-time HV battery SoC (%) — not kWh. `maxEnergy` (BMW only) = battery capacity (kWh). They are different metrics, register both.
> Charging target: `stateOfCharge.target` (BMW) and `stateOfCharge.targetMin` (Mini) — register the one for your vehicle.

### Mapped in the code but not registered above

`VehicleState` maps six further fields, and `CarStatusData` carries all of them through to the
web interface since backlog item 10. They stay empty until they are registered in the portal —
the connector cannot ask for a field the subscription does not deliver:

```
vehicle.drivetrain.electricEngine.remainingElectricRange   -> predictedRange
vehicle.drivetrain.electricEngine.charging.hvStatus        -> hvChargingStatus
vehicle.drivetrain.electricEngine.charging.chargingMode    -> chargingMode
vehicle.drivetrain.electricEngine.charging.acVoltage       -> acVoltage
vehicle.drivetrain.electricEngine.charging.acAmpere        -> acAmpere
vehicle.body.chargingPort.plugEventId                      -> plugEventId
vehicle.drivetrain.avgElectricRangeConsumption             -> avgConsumption
```

Registering one of these needs no code change any more — the field appears on the vehicle card
by itself. That is the whole difference to before item 10, where the connector published them
and `System.Text.Json` dropped them without a word on the reading side.

To add a field that is **not** in that list: register it in the portal, then add a `case` in
`VehicleState.Apply()`, a property to `ToJson()`, and the matching property to
`SharedContracts/CarStatusData.cs`. `VehicleStatePayloadTests` fails if you forget the last one.

## Output topic

The connector publishes to `daten/Fahrzeug/<Vehicle>/Status`, retained, following
`Docs/MQTT-Topic-Konvention.md` — the name is spelled once, in
`SharedContracts/FahrzeugTopics.cs`, and every reader takes it from there.

`<Vehicle>_OUTPUT_TOPIC` still overrides it for local experiments. **Do not set it in the
cluster.** An override fails silently in the worst possible way: the connector keeps publishing
happily, the readers keep subscribing to the conventional topic, and nobody gets an error —
just a dashboard that stops moving. The connector therefore logs its output topic at startup
whether or not the variable is set, and warns loudly when it is in effect.

---

## Token lifecycle

| Token | Expires | Managed by |
|---|---|---|
| `id_token` | 1 hour | Service refreshes every 50 min automatically |
| `access_token` | 1 hour | Refreshed alongside id_token |
| `refresh_token` | 2 weeks **after its last use** | Rotates on every refresh; bootstrap only once it has lapsed |

**The refresh token rotates.** Every refresh returns a new set of all three tokens and restarts
each of their clocks, so a service refreshing every 50 minutes keeps its refresh token
indefinitely young. Two things end that: two weeks without a successful refresh, or the
`client_id` being unsubscribed from its services, which voids the refresh token at once.
Only then is a new device code flow (bootstrap) required.

> **Source.** BMW's own portals (`bmw-cardata.bmwgroup.com`, customer and third-party) are
> JavaScript applications whose text cannot be retrieved mechanically, so the figures above come
> from a community transcript of the CarData documentation:
> <https://github.com/kvanbiesen/bmw-cardata-ha/blob/main/cardata_api_documentation.md>
> (retrieved 2026-09-20) — **not a primary source**, and for the two weeks no independent second
> source was found. The one-hour token lifetime is corroborated by a practitioner report in
> <https://github.com/bimmerconnected/bimmer_connected/discussions/745> ("ID token expires in:
> 0:59:58"), and the rotation itself is confirmed by our own measurement: on 2026-09-20 the
> Mini's stored `id_token` carried an `iat` of that morning alongside an `auth_time` of
> 2026-03-08.
>
> This replaces an earlier "~90 days" that stood here without any source and sent a diagnosis
> in the wrong direction. If you correct these numbers, bring a citation.

On each 50-minute refresh, the service writes all three updated tokens back to the Kubernetes
Secret **before** adopting them. That order matters: once BMW has issued the new set, the token
still sitting in the Secret is already void, so a failed write would leave the only usable
refresh token in the pod's memory. A write failure is retried four times and, if it still fails,
logged as an error naming that risk — never as a passing remark.

The service logs `[Vehicle] Proactive token refresh, reconnecting...` — this is normal.

---

## Credential precedence — why an environment variable cannot overwrite the Secret

`BMW_CLIENT_ID`, `BMW_GCID`, `Mini_CLIENT_ID` and `Mini_GCID` live in the
`bmwconnector-credentials` Secret, and there is no versioned copy of it anywhere: if it is
overwritten, the values have to come back out of KeePass. On 2026-09-20 exactly that happened.
A bootstrap run inherited `REPLACE_ME` placeholders from the user's systemd environment
(`systemctl --user set-environment`, which every new shell inherits and no dotfile grep finds),
preferred them over the Secret, and wrote them into it.

The rules now are:

| Situation | Outcome |
|---|---|
| Value looks like a placeholder (`REPLACE_ME`, `changeme`, `your-…-here`, `<…>`, …) | Refused, with the offending value named in the log |
| Value is not a UUID (8-4-4-4-12) | Refused — both CLIENT_ID and GCID are UUIDs |
| Running in the cluster | The Secret always wins; the environment variable is ignored and logged |
| Running locally | A well-formed environment variable overrides **for that process only** |
| Running locally with `--save-credentials` | ...and is additionally written into the Secret |

An overridden Secret value is logged masked (`1111…5555 (36 bytes)`), which is what makes an
accidental overwrite visible at all — the 2026-09-20 incident was only noticeable by the byte
lengths in the Secret.

None of this applies to the **tokens**: those are written back on every refresh by design, see
the lifecycle section above.

To check what the environment would contribute before running anything:

```fish
env | grep -E '^(BMW|Mini)_'
systemctl --user show-environment | grep -E '^(BMW|Mini)_'
```

---

## Health endpoints

| Endpoint | Probe | Answers 503 when |
|---|---|---|
| `/healthz/live` | liveness, startup | never (the process is serving or it is not answering at all) |
| `/healthz/ready` | readiness | any vehicle has been without a broker connection for longer than `BMW_VEHICLE_STALE_MINUTES` (default 5) |
| `/healthz/tokens` | none — informational | never; reports `Degraded` when a stored token has not been refreshed for `BMW_TOKEN_REFRESH_STALE_DAYS` (default 7) |

Liveness is deliberately independent of the vehicles. A restart cannot renew a token, so letting
a BMW outage restart the pod would replace a visible outage with a CrashLoop. Readiness is the
one that must fail: before this split, both probes ran the same check set and `Degraded` — one of
two vehicles gone — answered 200, which is why the August 2026 outage went unnoticed for 35 days.

Because the token check reads the **Secret** rather than the process's memory, it also catches
the case where refreshes succeed but no longer reach the Secret. The two halves cover each
other: the persistence fix prevents that failure, and this check is what makes it visible if
the fix ever stops working. Neither is worth much alone — a silent persistence failure is
invisible for weeks and only surfaces at the next pod restart.

What healthy looks like in the log: one `Connected to the BMW broker — vehicle is ready.` per
vehicle after each 50-minute refresh cycle, and no `has not been refreshed for` warning.

To confirm it directly rather than waiting for a failure, watch the `iat` of the stored
id_token move every 50 minutes. If it stands still while the service keeps logging refreshes,
the write to the Secret is failing:

```fish
kubectl -n smarthome get secret bmwconnector-bmw-tokens -o jsonpath='{.data.id_token\.txt}' \
  | base64 -d | cut -d. -f2 | base64 -d 2>/dev/null | jq '.iat | todate'
```

---

## Re-authentication checklist

Needed when the refresh token has actually lapsed — two weeks without a successful refresh, or
the `client_id` unsubscribed. `/healthz/tokens` and the `has not been refreshed for` warning in
the log give about a week's notice; a hard failure shows up as
`Token refresh failed (HTTP 400): invalid_request`.

1. Run bootstrap again — reads credentials from k8s Secret automatically:
   ```fish
   cd BMWConnector/BMWConnector
   dotnet run -- --bootstrap BMW
   dotnet run -- --bootstrap Mini
   ```
2. Bootstrap updates the Kubernetes token Secrets immediately
3. Recreate the pod so it reloads the Secret:
   ```bash
   kubectl -n smarthome get pods -l app=bmwconnector
   kubectl -n smarthome delete pod <pod-name>
   ```

> **Do not use `kubectl rollout restart`.** It patches the Deployment template
> (`kubectl.kubernetes.io/restartedAt`), and the ArgoCD application runs with
> `selfHeal: true` — ArgoCD sees the patched template as drift and reverts it.
> Deleting the pod changes nothing ArgoCD manages: the ReplicaSet simply creates
> a replacement, which reads the updated Secret on start.

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

The pod's ServiceAccount needs the following Role (included in the Helm chart). Note that the
write permissions exist for the **token** Secrets; the connector no longer writes credentials
unless asked to with `--save-credentials`, which is a local operation:

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
