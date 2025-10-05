# Grafana Kubernetes Deployment

This directory contains Kubernetes manifests for deploying Grafana in the cluster.

## Files
- `grafana-pv.yaml`: PersistentVolume for Grafana storage
- `grafana-pvc.yaml`: PersistentVolumeClaim for Grafana storage
- `grafana-secret.yaml`: Kubernetes Secret for Grafana admin credentials (base64 encoded)
- `grafana-datasource-configmap.yaml`: ConfigMap defining the InfluxDB datasource for Grafana
- `grafana-deployment.yaml`: Deployment to run Grafana
- `grafana-service.yaml`: Service exposing Grafana via NodePort

## Usage

1. Create the namespace (if not exists):
```powershell
kubectl create namespace monitoring
```

2. Set the InfluxDB token as secret
```powershell
kubectl -n monitoring create secret generic influxdb-token --from-literal=token=<YOUR_INFLUXDB_TOKEN>
```

3. Apply manifests:
```powershell
kubectl apply -n monitoring -f grafana-pv.yaml
kubectl apply -n monitoring -f grafana-pvc.yaml
kubectl apply -n monitoring -f grafana-secret.yaml
kubectl apply -n monitoring -f grafana-datasource-configmap.yaml
kubectl apply -n monitoring -f grafana-deployment.yaml
kubectl apply -n monitoring -f grafana-service.yaml
```

3. Access Grafana at `http://<NodeIP>:32030`.
