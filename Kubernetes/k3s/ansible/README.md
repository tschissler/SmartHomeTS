# k3s Cluster Setup via Ansible

Automated provisioning and configuration of a k3s Kubernetes cluster with HA control plane, distributed storage, DNS, and ingress.

## Purpose
This cluster is designed for home lab use rather than an enterprise environment.
It emphasizes simplicity, cost-effectiveness, and ease of management while providing essential features.
To provide a robust and functional Kubernetes experience, the cluster includes:
- **High Availability (HA)** for the control plane using `kube-vip`
- **Failover Support** across multiple nodes. The scenario assumes, that short downtimes may occur but ideally the cluster will self-heal into a healthy state.
- **Distributed Block Storage** with `Longhorn` on NVMe drives
- **DNS and Network Filtering** via `AdGuard Home`
- **Host-Based Ingress Routing** using `Traefik` with VIP support

The Ansible playbooks automate the entire setup process for the core cluster, from initial node preparation to final service configuration.
Applications and services will then be deployed on top of this foundation using GitOps with ArgoCD.

## Prerequisites

- Ubuntu Server (20.04+) on all nodes
- SSH access enabled
- Passwordless sudo configured for Ansible user
- Copy `inventory.ini.example` to `inventory.ini` and fill in your node IPs and SSH user

## Two-Phase Deployment

### Phase 1: Bootstrap (`cluster.yml`)
Prepares nodes and installs k3s:
- Passwordless sudo setup
- Network configuration (Ethernet, cgroups for ARM)
- k3s installation and stabilization

**Run this first and verify cluster is healthy before proceeding.**

```bash
ansible-playbook cluster.yml -i inventory.ini --limit prodservers
```

### Phase 2: Configure (`configure-cluster.yml`)
Adds cluster services in dependency order:
1. **kube-vip** - HA API endpoint and LoadBalancer VIP allocation
2. **Longhorn** - Replicated block storage on NVMe
3. **AdGuard Home** - DNS and network filtering
4. **CoreDNS forwarding** - Internal `.intern` zone resolution
5. **Traefik ingress** - Host-based routing with VIP

```bash
ansible-playbook configure-cluster.yml -i inventory.ini --limit prodservers
```

## Architecture

| Component | Purpose |
|-----------|---------|
| k3s API (kube-vip VIP) | HA control plane |
| Longhorn UI (`longhorn.intern`) | Distributed storage management |
| AdGuard Home (`adguard.intern`) | DNS + network filtering |
| Traefik Ingress | HTTP/HTTPS service ingress |

> For internal network configuration details (IP addresses, VIPs, DNS mappings), see your private infrastructure repository.

## DNS Zones

The cluster uses three DNS zones for resolving services and devices:

```
Pod DNS query
  └─> CoreDNS
        ├── *.cluster.local          → Kubernetes internal
        ├── *.intern                 → AdGuard Home (custom DNS rewrites)
        ├── *.fritz.box              → Home router via coredns-custom
        └── everything else          → node's /etc/resolv.conf
```

**When to use each DNS zone:**

| Device / Service | Use | Example |
|---|---|---|
| LAN device (DHCP on home router) | `<name>.fritz.box` | `<device>.fritz.box` |
| Cluster-internal K8s service | `<svc>.<ns>.svc.cluster.local` | `mosquitto.mosquitto.svc.cluster.local` |
| Cluster service exposed via ingress | `<name>.intern` (AdGuard rewrite) | `nextcloud.intern` |

All DHCP clients registered in the router are resolvable as `<hostname>.fritz.box` via a CoreDNS server block that forwards the `fritz.box` zone to the router. This is the preferred pattern for reaching LAN devices (wallboxes, inverters, etc.) from within the cluster — no extra CoreDNS config needed per device.

## Individual Playbooks

Core provisioning tasks:
- `bootstrap-passwordless-sudo.yml` - Enable ansible sudo access
- `check-connectivity.yml` - Validate SSH access
- `deploy-ssh-key.yml` - Distribute SSH keys
- `prepare-ethernet.yml` - Configure network interfaces
- `install-nano.yml` - Install text editor
- `network-config.yml` - Advanced networking
- `raspi-cgroups.yml` - ARM-specific kernel tuning
- `install-k3s.yml` - k3s installation

Configuration tasks:
- `configure-kubevip.yml` - HA API and service VIPs
- `prepare-longhorn-nodes.yml` - Node preparation for storage
- `prepare-longhorn-storage.yml` - Partition and prepare storage devices
- `install-longhorn.yml` - Deploy Longhorn
- `expose-longhorn-ui.yml` - Expose UI via ingress
- `prepare-minio-storage.yml` - MinIO object storage (optional)
- `install-adguard-home.yml` - Deploy DNS service
- `configure-coredns-intern-forward.yml` - Pod DNS zone forwarding
- `configure-ingress-hostnames.yml` - Host-based ingress routes
- `configure-traefik-mqtt.yml` - MQTT service routing
- `install-argocd.yml` - GitOps CD (optional)
- `disable-kubevip.yml` - Remove kube-vip (troubleshooting)

Backup infrastructure:

- `setup-backup-rpi.yml` - Setup Raspberry Pi as MinIO backup server (Velero S3 backend)

## Wrapper Scripts

```bash
./run-on-prod.sh -K -k     # Full bootstrap for production
./run-addons-prod.sh -K -k # Configuration only for production
```

Test cluster variants available (`testservers` inventory group).
