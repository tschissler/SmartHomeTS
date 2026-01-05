# Health Check Implementation Summary

## ? What Was Added

### 1. Health Check Infrastructure
- **`EnphaseConnectorHealthCheck.cs`** - Custom health check implementation
  - Monitors MQTT connection status
  - Tracks last successful data read
  - Reports unhealthy if no data read for > 5 minutes

### 2. HTTP Server for Health Endpoints
- Integrated ASP.NET Core minimal API into console app
- Runs on configurable port (default: 8080)
- Three endpoints:
  - `/healthz` - Liveness probe (simple alive check)
  - `/ready` - Readiness probe (checks MQTT + data freshness)
  - `/health` - Detailed health status (JSON response)

### 3. Kubernetes Integration
- **Liveness Probe** - Restarts pod if `/healthz` fails
- **Readiness Probe** - Removes pod from service if `/ready` fails
- **Startup Probe** - Gives app 60 seconds to start before other probes begin
- **Service** - ClusterIP service exposing port 8080 (optional, for debugging)

## ?? How Health Checks Work

### Health Status Logic

```
Healthy = MQTT Connected + (Starting OR Recent Read < 5min)
Unhealthy = MQTT Disconnected OR No Recent Read > 5min
```

### Kubernetes Behavior

1. **Pod Starting:**
   - Startup probe checks `/healthz` every 5s (up to 12 times = 60s)
   - If startup fails ? pod marked as failed and restarted

2. **Pod Running:**
   - Liveness probe checks `/healthz` every 10s
   - If fails 3 times (30s) ? pod restarted
   - Readiness probe checks `/ready` every 5s
   - If fails 3 times (15s) ? pod removed from service (no traffic)

3. **Pod Recovery:**
   - Once health checks pass ? pod marked ready again
   - Traffic resumes automatically

## ?? ArgoCD Integration

ArgoCD automatically detects the health endpoints and uses them to show application health status in the UI:

- **Green (Healthy)** - All health checks passing
- **Yellow (Progressing)** - Starting up or recovering
- **Red (Unhealthy)** - Health checks failing

ArgoCD checks the `/health` endpoint by default for detailed status.

## ?? Testing Health Checks

### Local Testing (Port Forward)

```bash
# Forward port to local machine
kubectl port-forward -n smarthome deployment/enphaseconnector 8080:8080

# Test liveness
curl http://localhost:8080/healthz
# Expected: {"status":"alive"}

# Test readiness
curl http://localhost:8080/ready
# Expected: {"status":"ready"} (200 OK if healthy, 503 if not)

# Get detailed health
curl http://localhost:8080/health | jq
# Expected: JSON with detailed status
```

### Check Kubernetes Probe Status

```bash
# View probe configuration and status
kubectl describe pod -n smarthome -l app=enphaseconnector

# Watch pod events in real-time
kubectl get events -n smarthome --watch

# Check if probes are passing
kubectl get pods -n smarthome -l app=enphaseconnector -o wide
```

## ??? Configuration

### appsettings.json
```json
{
  "EnphaseSettings": {
    "HealthCheckPort": 8080
  }
}
```

### Environment Variable
```bash
EnphaseSettings__HealthCheckPort=8080
```

### Kubernetes ConfigMap
```yaml
data:
  EnphaseSettings__HealthCheckPort: "8080"
```

## ?? Benefits

### For Kubernetes:
- ? **Automatic Recovery** - Unhealthy pods restarted automatically
- ? **Traffic Management** - Only healthy pods receive traffic
- ? **Graceful Startup** - App gets time to initialize before probes start
- ? **Zero Downtime** - Pods not marked ready until fully functional

### For ArgoCD:
- ? **Visual Health Status** - See app health in UI
- ? **Automated Rollback** - Can rollback on health failures
- ? **Sync Verification** - Confirms deployment successful via health
- ? **Alert Integration** - Can alert on unhealthy status

### For Operations:
- ? **Observability** - Clear health status at any time
- ? **Debugging** - Detailed health info via `/health` endpoint
- ? **Monitoring Integration** - Can scrape health endpoints
- ? **SLA Compliance** - Automated health tracking

## ?? Health Check Thresholds

| Check | Condition | Action |
|-------|-----------|--------|
| MQTT Disconnected | Immediately | Mark unhealthy |
| No Data > 5 min | After 5 minutes | Mark unhealthy |
| Startup Timeout | After 60 seconds | Restart pod |
| Liveness Failure | After 30 seconds (3×10s) | Restart pod |
| Readiness Failure | After 15 seconds (3×5s) | Remove from service |

## ?? Typical Health Flow

```
1. Pod starts
   ? Startup probe checks /healthz every 5s
   ? App initializes (connects to MQTT, gets tokens)
   
2. App becomes healthy
   ? Startup probe passes
   ? Liveness & Readiness probes start
   ? Both pass ? Pod marked READY
   
3. Normal operation
   ? Every data read updates health status
   ? Probes keep passing ? Pod stays healthy
   
4. Issue occurs (e.g., MQTT disconnect)
   ? Health status ? Unhealthy
   ? Readiness probe fails ? Pod removed from service
   ? Liveness still passes ? Pod keeps running
   
5. Issue resolved (MQTT reconnects)
   ? Health status ? Healthy
   ? Readiness probe passes ? Pod added back to service
   
6. Severe issue (pod stuck)
   ? Liveness probe fails 3 times
   ? Kubernetes restarts pod
   ? Back to step 1
```

## ?? Next Steps

1. Deploy and test the health checks
2. Monitor ArgoCD UI for health status
3. Verify probes in Kubernetes:
   ```bash
   kubectl describe pod -n smarthome -l app=enphaseconnector
   ```
4. Simulate failures to test recovery:
   ```bash
   # Simulate MQTT failure by blocking network (if applicable)
   # Watch pod status and ArgoCD UI
   ```

## ?? References

- [Kubernetes Liveness, Readiness and Startup Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/)
- [ArgoCD Health Assessment](https://argo-cd.readthedocs.io/en/stable/operator-manual/health/)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
