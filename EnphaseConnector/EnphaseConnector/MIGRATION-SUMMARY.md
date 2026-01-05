# EnphaseConnector - Migration Summary

## ? Completed Changes

### 1. Application Configuration (Step 1)
- ? Added `appsettings.json` with default configuration values
- ? Created `EnphaseSettings.cs` class for strongly-typed configuration
- ? Updated `Program.cs` to use .NET Configuration system
- ? Added NuGet packages for configuration support

**How it works:**
- Reads from `appsettings.json` by default
- Environment variables override values using format: `EnphaseSettings__PropertyName`
- Example: `EnphaseSettings__EnphaseUserName=myuser`

### 2. Kubernetes Manifests (Plain YAML - No Helm)
- ? Created `k8s/namespace.yaml` for the smarthome namespace
- ? Created `k8s/configmap.yaml` for non-sensitive config
- ? Created `k8s/secret.yaml` for credentials
- ? Created `k8s/deployment.yaml` for the application
- ? Created `k8s/argocd-application.yaml` for ArgoCD
- ? Created `k8s/README.md` with deployment instructions
- ? All resources deployed to `smarthome` namespace

### 3. GitHub Actions Workflow Update
- ? Updated workflow to support both old and new deployment methods
- ? Old cluster continues to work with existing deployment
- ? New deployments can use ArgoCD

## ?? New File Structure

```
EnphaseConnector/
??? EnphaseConnector/
?   ??? appsettings.json          # NEW: Default configuration
?   ??? EnphaseSettings.cs        # NEW: Configuration class
?   ??? Program.cs                # UPDATED: Uses IConfiguration
?   ??? EnphaseConnector.csproj   # UPDATED: Added config packages
??? k8s/                          # NEW: Plain Kubernetes manifests
?   ??? namespace.yaml            # NEW: Creates smarthome namespace
?   ??? configmap.yaml            # Deployed to smarthome namespace
?   ??? secret.yaml               # Deployed to smarthome namespace
?   ??? deployment.yaml           # Deployed to smarthome namespace
?   ??? argocd-application.yaml   # Targets smarthome namespace
?   ??? README.md
??? helm/                          # OPTIONAL: Can be deleted if not needed
    ??? enphaseconnector/
        ??? ...
```

## ?? Deployment Options

### Option 1: Old Cluster (Existing - No Changes Needed)
Your existing workflow will continue to work:
- Builds Docker image
- Pushes to Docker Hub
- Updates existing deployment via `kubectl set image`

### Option 2: New Cluster with ArgoCD (Deploys to smarthome namespace)

#### Setup (One-time):

1. **Install ArgoCD** (if not already installed):
```bash
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml
```

2. **Create the smarthome namespace** (ArgoCD can also do this automatically):
```bash
kubectl apply -f EnphaseConnector/k8s/namespace.yaml
```

3. **Create the secret with your credentials**:
```bash
kubectl create secret generic enphaseconnector-secrets \
  --namespace smarthome \
  --from-literal=EnphaseSettings__EnphaseUserName="your-username" \
  --from-literal=EnphaseSettings__EnphasePassword="your-password" \
  --from-literal=EnphaseSettings__EnvoyM1Serial="your-m1-serial" \
  --from-literal=EnphaseSettings__EnvoyM3Serial="your-m3-serial"
```

4. **Deploy the ArgoCD Application**:
```bash
kubectl apply -f EnphaseConnector/k8s/argocd-application.yaml
```

#### How it works:
- ArgoCD watches your Git repository
- When you push changes to `main`, ArgoCD automatically syncs
- Docker image updates trigger automatic rollouts
- All resources are deployed to the `smarthome` namespace
- All managed through GitOps

## ?? Secrets Management

### For Manual Deployment (kubectl):
```bash
kubectl create secret generic enphaseconnector-secrets \
  --namespace smarthome \
  --from-literal=EnphaseSettings__EnphaseUserName="..." \
  --from-literal=EnphaseSettings__EnphasePassword="..." \
  --from-literal=EnphaseSettings__EnvoyM1Serial="..." \
  --from-literal=EnphaseSettings__EnvoyM3Serial="..."
```

### For GitOps (Recommended):
Use **Sealed Secrets** or **External Secrets Operator**:

```bash
# Install sealed-secrets controller first
kubectl apply -f https://github.com/bitnami-labs/sealed-secrets/releases/download/v0.24.0/controller.yaml

# Create sealed secret
kubectl create secret generic enphaseconnector-secrets \
  --namespace smarthome \
  --from-literal=EnphaseSettings__EnphaseUserName="..." \
  --from-literal=EnphaseSettings__EnphasePassword="..." \
  --from-literal=EnphaseSettings__EnvoyM1Serial="..." \
  --from-literal=EnphaseSettings__EnvoyM3Serial="..." \
  --dry-run=client -o yaml | \
  kubeseal -o yaml > EnphaseConnector/k8s/sealed-secret.yaml

# Commit sealed-secret.yaml to Git (safe to commit!)
git add EnphaseConnector/k8s/sealed-secret.yaml
git commit -m "Add sealed secrets"
git push
```

## ?? Namespace Organization

All resources are deployed to the `smarthome` namespace for better organization:
- ? Logical separation from other services
- ? Easy to apply resource quotas
- ? Simplified network policies
- ? Clean service discovery (e.g., `mosquitto.smarthome.svc.cluster.local`)
- ? Easier RBAC management

### Verify namespace deployment:
```bash
kubectl get all -n smarthome
```

## ?? Cleanup (Optional)

If you don't want the Helm chart:
```bash
rm -rf EnphaseConnector/helm/
```

The old `kube.yaml` can also be removed once you've migrated:
```bash
rm EnphaseConnector/kube.yaml
```

## ?? Configuration Reference

### Environment Variables
| Variable | Description | Default |
|----------|-------------|---------|
| `EnphaseSettings__EnphaseUserName` | Enphase account username | (required) |
| `EnphaseSettings__EnphasePassword` | Enphase account password | (required) |
| `EnphaseSettings__EnvoyM1Serial` | First Envoy device serial | (required) |
| `EnphaseSettings__EnvoyM3Serial` | Second Envoy device serial | (required) |
| `EnphaseSettings__MqttBroker` | MQTT broker hostname | mosquitto.intern |
| `EnphaseSettings__MqttPort` | MQTT broker port | 1883 |
| `EnphaseSettings__ReadIntervalMs` | Read interval in milliseconds | 1000 |

## ?? Troubleshooting

### Check if secrets are loaded:
```bash
kubectl exec -n smarthome -it deployment/enphaseconnector -- env | grep EnphaseSettings
```

### View logs:
```bash
kubectl logs -n smarthome -l app=enphaseconnector -f
```

### Check ArgoCD sync status:
```bash
kubectl get application -n argocd enphaseconnector
```

### Check all resources in smarthome namespace:
```bash
kubectl get all -n smarthome
```

## ?? Next Steps

1. Test the application with the new configuration locally
2. Deploy to a test environment
3. Set up ArgoCD in your new cluster
4. Create the smarthome namespace
5. Create secrets in the smarthome namespace
6. Apply the ArgoCD application manifest
7. Monitor the deployment

## ?? Benefits of This Approach

- ? Simple plain YAML (no Helm complexity)
- ? GitOps-ready for ArgoCD
- ? Environment variables override config
- ? Old cluster continues to work
- ? Easy to understand and maintain
- ? Secrets separated from code
- ? Standard .NET configuration patterns
- ? **Organized in dedicated smarthome namespace**
