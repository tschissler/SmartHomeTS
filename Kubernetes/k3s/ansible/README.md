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

| Component | Endpoint | Purpose |
|-----------|----------|---------|
| k3s API | `192.168.178.222:6443` | HA control plane via kube-vip |
| Longhorn UI | `http://longhorn.intern/` | Distributed storage management |
| AdGuard Home | `http://adguard.intern/` | DNS + network filtering |
| Traefik Ingress | `192.168.178.223:80/443` | HTTP service ingress |

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

## Wrapper Scripts

```bash
./run-on-prod.sh -K -k     # Full bootstrap for production
./run-addons-prod.sh -K -k # Configuration only for production
```

Test cluster variants available (`testservers` inventory group).
