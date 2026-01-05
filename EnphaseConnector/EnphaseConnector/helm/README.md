# EnphaseConnector Helm Chart

A Helm chart for deploying EnphaseConnector - an MQTT-based Enphase solar data connector for Kubernetes.

## Prerequisites

- Kubernetes 1.19+
- Helm 3.0+
- Existing secret with Enphase credentials (see below)

## Installing the Chart

### 1. Create the namespace

```bash
kubectl create namespace smarthome
```

### 2. Create the secrets

Create a Kubernetes secret with your Enphase credentials:

```bash
kubectl create secret generic enphaseconnector-secrets \
  --namespace smarthome \
  --from-literal=EnphaseSettings__EnphaseUserName="your-username" \
  --from-literal=EnphaseSettings__EnphasePassword="your-password" \
  --from-literal=EnphaseSettings__EnvoyM1Serial="your-m1-serial" \
  --from-literal=EnphaseSettings__EnvoyM3Serial="your-m3-serial"
```

### 3. Install the chart

```bash
helm install enphaseconnector ./helm/enphaseconnector \
  --namespace smarthome \
  --create-namespace
```

Or with custom values:

```bash
helm install enphaseconnector ./helm/enphaseconnector \
  --namespace smarthome \
  --create-namespace \
  --set config.mqttBroker="your-mqtt-broker" \
  --set config.mqttPort="1883"
```

## Upgrading the Chart

```bash
helm upgrade enphaseconnector ./helm/enphaseconnector \
  --namespace smarthome
```

## Uninstalling the Chart

```bash
helm uninstall enphaseconnector --namespace smarthome
```

## Configuration

The following table lists the configurable parameters and their default values.

| Parameter | Description | Default |
|-----------|-------------|---------|
| `replicaCount` | Number of replicas | `1` |
| `image.repository` | Image repository | `tschissler/enphaseconnector` |
| `image.tag` | Image tag | `latest` |
| `image.pullPolicy` | Image pull policy | `IfNotPresent` |
| `service.type` | Kubernetes service type | `ClusterIP` |
| `service.port` | Service port | `8080` |
| `config.mqttBroker` | MQTT broker hostname | `mosquitto.intern` |
| `config.mqttPort` | MQTT broker port | `1883` |
| `config.readIntervalMs` | Read interval in milliseconds | `1000` |
| `config.healthCheckPort` | Health check HTTP server port | `8080` |
| `secrets.existingSecret` | Name of existing secret | `enphaseconnector-secrets` |
| `resources.limits.cpu` | CPU limit | `200m` |
| `resources.limits.memory` | Memory limit | `256Mi` |
| `resources.requests.cpu` | CPU request | `100m` |
| `resources.requests.memory` | Memory request | `128Mi` |

## Health Checks

The chart configures three types of probes:

- **Liveness Probe** (`/healthz`): Checks if the application is alive
- **Readiness Probe** (`/ready`): Checks if the application is ready to serve traffic
- **Startup Probe** (`/healthz`): Gives the app time to start before other probes kick in

## Using with ArgoCD

See the ArgoCD application example in the repository.

## Values File Example

Create a custom `my-values.yaml`:

```yaml
image:
  tag: "1.0.0"

config:
  mqttBroker: "my-mqtt-broker.local"
  mqttPort: "1883"
  readIntervalMs: "5000"

resources:
  limits:
    cpu: 500m
    memory: 512Mi
  requests:
    cpu: 200m
    memory: 256Mi
```

Then install with:

```bash
helm install enphaseconnector ./helm/enphaseconnector \
  --namespace smarthome \
  --values my-values.yaml
```
