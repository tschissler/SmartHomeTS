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
4. **CoreDNS forwarding** - Internal `.intern` zone resolution for pods
5. **Node split-DNS** - `.intern` queries routed to AdGuard on each node (Step 2c)
6. **Cluster CA trust** - SmartHome CA installed in node OS trust store (Step 3, requires cert-manager)

```bash
ansible-playbook configure-cluster.yml -i inventory.ini --limit prodservers
```

> **Note – Step 3 prerequisite:** `trust-cluster-ca.yml` requires ArgoCD and cert-manager to be running and the `ClusterCA` ArgoCD application to be healthy before it can be executed. If running `configure-cluster.yml` end-to-end on a fresh cluster, Step 3 will fail and must be re-run separately once cert-manager is ready:
> ```bash
> ./run-addons-prod.sh plays/trust-cluster-ca.yml
> ```

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
- `configure-adguard-rewrites.yml` - AdGuard DNS rewrites for `.intern` hostnames
- `configure-coredns-intern-forward.yml` - Pod DNS zone forwarding
- `configure-node-dns.yml` - Split-DNS drop-in on each node: routes `*.intern` queries to AdGuard Home, all other traffic stays on Fritz!Box (192.168.178.1). Does not change Netplan/network config.
- `configure-k3s-resolv.yml` - Upstream resolver for pods and CoreDNS: writes `/etc/resolv.conf.k3s` (AdGuard as the single upstream -- CoreDNS load-balances `random` across every nameserver listed, so a second entry would bypass filtering for half the queries, not act as failover) and sets `resolv-conf` in `config.yaml` on every node. Until 2026-09-19 that file was hand-written, unversioned and missing on k3snode5/6, so pod DNS differed by node (SmartHomeDeployments issue #40). Restarts k3s `serial: 1`; skip with `-e resolv_restart=false`.
- `configure-node-nameservers.yml` - Removes the public resolver (`1.1.1.1`) from each node's netplan file. Edits only that one line on purpose: the files on the nodes were hand-maintained and carry a `match`/`set-name` block the template does not have, so re-rendering them via `network-config.yml` would drop it -- and the node could come up without a network after the next reboot. Validates with `netplan generate` before applying, `serial: 1`.
- `trust-cluster-ca.yml` - Installs SmartHome Cluster CA (from `cluster-ca-secret` in `cert-manager`) into the OS trust store on every node and restarts k3s/k3s-agent. Required for image pulls from `forgejo.intern` and TLS connections to internal services. Runs `serial: 1` to avoid simultaneous control-plane restarts.
- `configure-ingress-hostnames.yml` - Host-based ingress routes
- `configure-traefik-mqtt.yml` - MQTT service routing
- `install-argocd.yml` - GitOps CD (optional)
- `disable-kubevip.yml` - Remove kube-vip (troubleshooting)

Backup infrastructure:

- `setup-backup-rpi.yml` - Setup Raspberry Pi as MinIO backup server (Velero S3 backend)

Diagnostics:

- `setup-diagnostic-readonly-user.yml` - Creates the `claude-ro` account on every prod node
  for log and state inspection: member of `adm` and `systemd-journal`, **no sudo**, key
  restricted to `no-agent-forwarding,no-port-forwarding,no-X11-forwarding`. Reading the
  journal (including previous boots) needs no root, so the account stays powerless while
  covering node-level diagnosis. The play asserts both halves: the journal must be readable
  and `sudo -n` must fail. Requires `~/.ssh/claude_ro.pub` on the control host:

  ```bash
  ssh-keygen -t ed25519 -f ~/.ssh/claude_ro -C claude-ro -N ""
  ansible-playbook plays/setup-diagnostic-readonly-user.yml -K --limit k3snode5.intern
  ```

  Rationale: the alternative path -- a throwaway pod with `hostPath: /` and `journalctl -D`
  -- works, but needs a running kubelet, which is exactly what is gone when a node goes
  NotReady. Fixing anything still goes through Git -> ArgoCD or the admin account.

- `expose-etcd-metrics.yml` - Sets `etcd-expose-metrics: true` in `/etc/rancher/k3s/config.yaml`
  on the control-plane nodes, so etcd additionally binds its metrics port to the node IP
  (verified on k3snode3: `192.168.178.233:2381` plus the existing `127.0.0.1:2381`, not
  `0.0.0.0`). Without this, Prometheus (a pod on some other node) cannot reach the
  endpoint at all, which is why `kubeEtcd` is disabled in the kube-prometheus-stack values
  (SmartHomeDeployments `Monitoring.yaml`). Port 2381 serves only `/metrics` and `/health`
  -- no etcd keys, no write path -- so exposing it on the LAN is the same trade-off already
  accepted for node-exporter on 9100. Merges the single key into the existing config and
  restarts k3s only if the file actually changed; `serial: 1` keeps etcd quorum at 2 of 3.
  The play verifies `etcd_server_has_leader` is served on the node IP before moving on.

  ```bash
  ansible-playbook plays/expose-etcd-metrics.yml -K --limit k3snode3.intern   # canary
  ansible-playbook plays/expose-etcd-metrics.yml -K                           # all servers
  ```

  After the play, flip `kubeEtcd.enabled` to `true` in SmartHomeDeployments -- in that
  order, otherwise the ServiceMonitor scrapes a target that does not answer yet.

## Wrapper Scripts

```bash
./run-on-prod.sh -K -k     # Full bootstrap for production
./run-addons-prod.sh -K -k # Configuration only for production
```

Test cluster variants available (`testservers` inventory group).
