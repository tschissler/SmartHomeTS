# EnphaseConnector Kubernetes Deployment

This directory contains plain Kubernetes YAML manifests for deploying EnphaseConnector to the **smarthome** namespace.

## Files

- `namespace.yaml` - Creates the smarthome namespace
- `configmap.yaml` - Non-sensitive configuration (MQTT broker, port, intervals, health check port)
- `secret.yaml` - Sensitive credentials (username, password, device serials)
- `deployment.yaml` - Main deployment configuration with health checks
- `service.yaml` - Service for health check endpoints (optional)
- `argocd-application.yaml` - ArgoCD Application definition

## Health Checks

The application exposes three HTTP endpoints for monitoring:

### Endpoints

- **`/healthz`** - Liveness probe (checks if app is alive)
  - Returns: `200 OK` with `{"status": "alive"}`
  - Used by Kubernetes to restart unhealthy pods

- **`/ready`** - Readiness probe (checks if app is ready to work)
  - Returns: `200 OK` if healthy, `503` if not ready
  - Checks: MQTT connection + recent successful data reads (< 5 minutes)
  - Used by Kubernetes to route traffic only to ready pods

- **`/health`** - Detailed health check (JSON response)
  - Returns detailed status with timing information
  - Example response:
  ```json
  {
    "status": "Healthy",
    "checks": [{
      "name": "enphase_connector",
      "status": "Healthy",
      "description": "Last successful read: 45 seconds ago",
      "duration": 0.123
    }],
    "totalDuration": 0.234
  }
  ```

### Testing Health Endpoints

```bash
# Port-forward to access health endpoints locally
kubectl port-forward -n smarthome deployment/enphaseconnector 8080:8080

# Test liveness
curl http://localhost:8080/healthz

# Test readiness
curl http://localhost:8080/ready

# Get detailed health status
curl http://localhost:8080/health | jq
```

## Kubernetes Probes Configuration

The deployment includes three types of probes:

1. **Startup Probe** - Gives the app 60 seconds to start (12 failures × 5s)
2. **Liveness Probe** - Restarts pod if unhealthy for 30 seconds (3 failures × 10s)
3. **Readiness Probe** - Removes from service if unhealthy for 15 seconds (3 failures × 5s)

## Manual Deployment (kubectl)

### 1. Create the namespace

```bash
kubectl apply -f namespace.yaml
```

Or create manually:
```bash
kubectl create namespace smarthome
```

### 2. Create the secret with your credentials

```bash
kubectl create secret generic enphaseconnector-secrets \
  --namespace smarthome \
  --from-literal=EnphaseSettings__EnphaseUserName="your-username" \
  --from-literal=EnphaseSettings__EnphasePassword="your-password" \
  --from-literal=EnphaseSettings__EnvoyM1Serial="your-m1-serial" \
  --from-literal=EnphaseSettings__EnvoyM3Serial="your-m3-serial"
```

### 3. Apply the manifests

```bash
kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml
kubectl apply -f secret.yaml
kubectl apply -f service.yaml
kubectl apply -f deployment.yaml
```

Or apply all at once:

```bash
kubectl apply -f .
```

## ArgoCD Deployment

### Option 1: Direct Git Repository

Create an ArgoCD Application pointing to this directory:

```bash
kubectl apply -f argocd-application.yaml
```

The ArgoCD application is configured to:
- Deploy to the `smarthome` namespace
- Automatically create the namespace if it doesn't exist (`CreateNamespace=true`)
- Auto-sync on Git changes
- Self-heal if manual changes are made

ArgoCD will automatically use the health endpoints to determine application health status.

### Option 2: With Sealed Secrets (Recommended for GitOps)

1. Install sealed-secrets controller
2. Create a sealed secret:

```bash
kubectl create secret generic enphaseconnector-secrets \
  --namespace smarthome \
  --from-literal=EnphaseSettings__EnphaseUserName="your-username" \
  --from-literal=EnphaseSettings__EnphasePassword="your-password" \
  --from-literal=EnphaseSettings__EnvoyM1Serial="your-m1-serial" \
  --from-literal=EnphaseSettings__EnvoyM3Serial="your-m3-serial" \
  --dry-run=client -o yaml | \
  kubeseal -o yaml > sealed-secret.yaml
```

3. Update the sealed secret to target the smarthome namespace:
```bash
# Edit sealed-secret.yaml and ensure namespace: smarthome is set
```

4. Commit `sealed-secret.yaml` to Git
5. Let ArgoCD sync

## Updating Configuration

### Update ConfigMap
Edit `configmap.yaml` and apply:
```bash
kubectl apply -f configmap.yaml
kubectl rollout restart deployment/enphaseconnector -n smarthome
```

### Update Secrets
```bash
kubectl delete secret enphaseconnector-secrets -n smarthome
kubectl create secret generic enphaseconnector-secrets \
  --namespace smarthome \
  --from-literal=EnphaseSettings__EnphaseUserName="new-username" \
  --from-literal=EnphaseSettings__EnphasePassword="new-password" \
  --from-literal=EnphaseSettings__EnvoyM1Serial="new-m1-serial" \
  --from-literal=EnphaseSettings__EnvoyM3Serial="new-m3-serial"
kubectl rollout restart deployment/enphaseconnector -n smarthome
```

## Environment Variable Mapping

The application uses .NET configuration system where:
- `appsettings.json` provides defaults
- Environment variables override defaults using double underscore notation

Example:
- Config path: `EnphaseSettings:MqttBroker`
- Environment variable: `EnphaseSettings__MqttBroker`

## Monitoring

Check deployment status:
```bash
kubectl get pods -n smarthome -l app=enphaseconnector
kubectl logs -n smarthome -l app=enphaseconnector -f
```

Check all resources in the namespace:
```bash
kubectl get all -n smarthome
```

Check pod health status:
```bash
kubectl describe pod -n smarthome -l app=enphaseconnector | grep -A 10 "Conditions:"
```

## Troubleshooting

If the pod fails to start, check:
1. Namespace exists: `kubectl get namespace smarthome`
2. Secrets are created: `kubectl get secret enphaseconnector-secrets -n smarthome`
3. ConfigMap exists: `kubectl get configmap enphaseconnector-config -n smarthome`
4. Pod logs: `kubectl logs -n smarthome -l app=enphaseconnector`
5. Pod events: `kubectl describe pod -n smarthome -l app=enphaseconnector`
6. Environment variables are loaded: 
```bash
kubectl exec -n smarthome -it deployment/enphaseconnector -- env | grep EnphaseSettings
```

### Health Check Issues

If health checks are failing:

```bash
# Check probe status
kubectl describe pod -n smarthome -l app=enphaseconnector | grep -A 5 "Liveness:\|Readiness:"

# Check health endpoint directly
kubectl port-forward -n smarthome deployment/enphaseconnector 8080:8080
curl http://localhost:8080/health

# Check application logs for health-related messages
kubectl logs -n smarthome -l app=enphaseconnector | grep -i health
```

## Namespace Organization

The `smarthome` namespace is intended to hold all smart home related services:
- EnphaseConnector (solar data)
- MQTT broker (mosquitto)
- Other IoT services

This provides:
- ? Logical separation of concerns
- ? Resource quotas per namespace
- ? Network policies per namespace
- ? Easier RBAC management
- ? Clean service discovery within namespace
