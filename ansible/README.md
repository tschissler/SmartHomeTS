# k3s Cluster Bootstrap (Ansible)

Ziel: den Cluster von 0 auf den aktuellen Stand aufbauen:

- k3s HA via kube-vip (`192.168.178.220`)
- Traefik als LoadBalancer auf eigener VIP (`192.168.178.221`)
- Longhorn (Storage-Nodes + Default Disk Path)
- Nextcloud + Collabora (funktionierende /cool WebSockets)
- CoreDNS NodeHosts für `nextcloud.local`, `office.local`, `longhorn.local`
- Traefik encoded-path Keeper (damit Updates das Collabora-Fix nicht entfernen)

## Voraussetzungen

- Ansible Controller: Linux/WSL (unter Windows nativ ist Ansible unüblich).
- SSH Zugriff auf die Nodes (User in `inventories/home/hosts.ini`).
- Nodes: Ubuntu (getestet mit 24.04; ein Pi/ARM64 ist ok).
- Internetzugang (k3s Install Script + Helm Charts).

## Node-Vorbereitung (ab hier kann das Playbook starten)

Ziel dieser Vorbereitung: die Nodes sind frisch installiert, haben **stabile Hostnames + IPs**, einen **SSH-User für Ansible** und sind per SSH erreichbar. Ab dann übernimmt `ansible/site.yml` den Rest (k3s, kube-vip, Traefik, Longhorn, Nextcloud/Collabora, DNS, Postflight-Checks).

### Gemeinsame Anforderungen (alle Nodes)

Die folgenden Commands sind für Ubuntu Server (x86_64 und ARM64) gleich.

- Hostname setzen (Beispiel für `clusternode1`):

```bash
sudo hostnamectl set-hostname clusternode1
hostnamectl
```

Optional (hilft bei lokaler Auflösung):

```bash
echo "127.0.1.1 clusternode1" | sudo tee -a /etc/hosts
```

- Statische IP konfigurieren (Alternative: DHCP-Reservation am Router)

1) Interface-Namen herausfinden (z.B. `enp1s0`, `eth0`):

```bash
ip -br link
ip route
```

1) Netplan-Datei anlegen/anpassen (Beispiel; IP/Gateway/Interface anpassen):

```bash
sudo nano /etc/netplan/01-netcfg.yaml
```

```yaml
# /etc/netplan/01-netcfg.yaml
network:
  version: 2
  renderer: networkd
  ethernets:
    enp1s0:
      dhcp4: no
      addresses:
        - 192.168.178.211/24
      routes:
        - to: default
          via: 192.168.178.1
      nameservers:
        addresses: [192.168.178.1, 1.1.1.1]
```

1) Anwenden + prüfen:

```bash
sudo netplan apply
ip -br addr
ping -c 2 192.168.178.1
```

Danach die IPs in `inventories/home/hosts.ini` eintragen.

- SSH aktivieren (falls nicht schon aktiv):

```bash
sudo apt-get update
sudo apt-get install -y openssh-server
sudo systemctl enable --now ssh
sudo systemctl status ssh --no-pager
```

- Ansible-User anlegen (Standard im Inventory: `thomas`) und `sudo` erlauben:

```bash
sudo adduser thomas
sudo usermod -aG sudo thomas
getent group sudo | grep -q thomas && echo "thomas is in sudo group"
```

- SSH-Key hinterlegen (empfohlen)

Vom **Ansible-Controller** (WSL/Linux) aus:

```bash
ssh-copy-id thomas@192.168.178.211
```

Alternativ (Key manuell eintragen) auf dem Node:

```bash
sudo -u thomas mkdir -p /home/thomas/.ssh
sudo -u thomas chmod 700 /home/thomas/.ssh
sudo -u thomas nano /home/thomas/.ssh/authorized_keys
sudo -u thomas chmod 600 /home/thomas/.ssh/authorized_keys
```

- System updaten:

```bash
sudo apt-get update
sudo apt-get -y upgrade
sudo reboot
```

- Python muss vorhanden sein (für Ansible auf dem Node):

```bash
command -v python3 || (sudo apt-get update && sudo apt-get install -y python3)
python3 --version
```

Hinweis: Swap muss für Kubernetes aus sein. Das Playbook macht `swapoff -a` und kommentiert Swap in `/etc/fstab`, aber wenn du ein völlig minimales System installierst, ist Swap oft ohnehin aus.

### Sanity Checks (vor dem Playbook)

Diese Checks machst du idealerweise vom **Ansible-Controller** (WSL/Linux) aus.

- SSH erreichbar?

```bash
ssh thomas@192.168.178.211 "hostnamectl ; uptime"
```

- Passwordloses sudo ok?

```bash
ssh thomas@192.168.178.211 "sudo -n true && echo 'sudo ok' || echo 'sudo needs password'"
```

- DNS & Internet ok?

```bash
ssh thomas@192.168.178.211 "getent hosts github.com ; curl -fsSL https://get.k3s.io >/dev/null && echo 'internet ok'"
```

- Python3 ok?

```bash
ssh thomas@192.168.178.211 "python3 --version"
```

### clusternode1 & clusternode2 (x86_64): Minimal Ubuntu Server

Empfehlung: Ubuntu Server 24.04 LTS minimal, ohne Desktop.

1. Ubuntu installieren (z.B. per ISO/USB).
2. Während/ nach Installation:
   - Hostname: `clusternode1` bzw. `clusternode2`
   - User: `thomas` (oder passe `ansible_user` im Inventory an)
   - OpenSSH Server aktivieren
3. Statische IP setzen (Netplan). Beispiel (bitte an dein Interface anpassen, häufig `enp1s0` o.ä.):

```yaml
# /etc/netplan/01-netcfg.yaml
network:
  version: 2
  renderer: networkd
  ethernets:
    enp1s0:
      dhcp4: no
      addresses:
        - 192.168.178.211/24   # clusternode1 (Beispiel)
      routes:
        - to: default
          via: 192.168.178.1
      nameservers:
        addresses: [192.168.178.1, 1.1.1.1]
```

Dann: `sudo netplan apply`

1. SSH-Key (vom Ansible-Controller) auf den Node kopieren:

```bash
ssh-copy-id thomas@192.168.178.211
```

1. (Optional) Longhorn-Datenpartition vorbereiten:
   - Wenn du `longhorn_manage_disk: true` nutzen willst, stelle sicher, dass das Ziel-Device/Partition eindeutig ist (z.B. `/dev/sda3`) und trage es in `group_vars/all.yml` unter `longhorn_disk_device_by_host` ein.

### clusternode3 & zukünftige Raspberry Pi Worker (ARM64): Ubuntu Server via Raspberry Pi Imager

Empfehlung: mit **Raspberry Pi Imager** das **offizielle Ubuntu Server (64-bit)** Image schreiben.

1. Raspberry Pi Imager → Ubuntu Server (64-bit) auswählen → auf SD/SSD flashen.
2. Imager-„OS Customisation“ (Zahnrad) nutzen:
   - Hostname: `clusternode3` (bzw. später `workerX`)
   - SSH aktivieren
   - User/Pass setzen (z.B. `thomas`) oder zumindest einen User, der per SSH erreichbar ist
   - Optional: Public SSH Key direkt hinterlegen
   - Optional: WLAN (falls nicht per LAN)
3. Nach dem ersten Boot:
   - `sudo apt-get update ; sudo apt-get -y upgrade`
   - Prüfen, dass `python3` verfügbar ist: `command -v python3`
   - Statische IP setzen (Netplan), analog zu oben.

Hinweis: Diese Pi-Nodes sind in deinem aktuellen Setup **keine Storage-Nodes**. In `inventories/home/hosts.ini` sind nur `clusternode1`/`clusternode2` unter `[storage_nodes]` gelistet.

## Quickstart

1. Inventory anpassen:

- `inventories/home/hosts.ini` (IPs, SSH-User)

1. Cluster-Variablen prüfen:

- `group_vars/all.yml`

1. Collabora Admin Passwort setzen:

- In `group_vars/all.yml` steht `collabora_admin_password: CHANGE_ME_IN_VAULT`.
- Empfehlung: als Vault ablegen (z.B. `group_vars/vault.yml` + `ansible-vault encrypt`).

1. Ausführen:

```bash
cd ansible
ansible-playbook -i inventories/home/hosts.ini site.yml
```

## Einen neuen Node zum bestehenden Cluster hinzufügen (Join)

Ja – du kannst mit diesem Repo auch **einen neuen Node nur joinen**, ohne den ganzen Cluster/Apps neu zu installieren.

Vorgehen:

1. Node wie oben unter „Node-Vorbereitung“ installieren (Hostname, IP, SSH, User+sudo, Key).
2. Node im Inventory eintragen: `inventories/home/hosts.ini`

- **Worker/Agent**: unter `[k3s_agents]`
- **Zusätzlicher Control-Plane** (optional): unter `[k3s_servers]` (aber nicht unter `[k3s_init]`)
- **Longhorn Storage Node** (optional): zusätzlich unter `[storage_nodes]`

Dann joinen:

```bash
cd ansible

# Beispiel: neuen Worker joinen
ansible-playbook -i inventories/home/hosts.ini join.yml --limit newworker1

# Beispiel: neuen Control-Plane joinen
ansible-playbook -i inventories/home/hosts.ini join.yml --limit newserver1
```

Wichtig:

- Für Join-Läufe empfehle ich **immer** `--limit <node>`, damit wirklich nur der neue/ersetzte Node angefasst wird.

### Node ersetzen (z.B. wenn ein bestehender Node ausgefallen ist)

Szenario: Ein Node ist tot/neu installiert und soll mit **gleichem Namen** wieder in den Cluster.

1. OS neu installieren und die „Node-Vorbereitung“ ausführen (Hostname/IP/SSH/User/Key).
2. Stelle sicher, dass der Host im Inventory noch genauso heißt (z.B. `clusternode2`).
3. Optional aber empfohlen: alten Node-Record aus dem Cluster löschen (vom Init-Node aus):

```bash
/usr/local/bin/k3s kubectl delete node clusternode2
```

1. Join ausführen:

```bash
cd ansible
ansible-playbook -i inventories/home/hosts.ini join.yml --limit clusternode2
```

Hinweis für Storage-Nodes: wenn der ersetzte Node Longhorn-Replicas hatte, prüfe nach dem Re-Join im Longhorn UI, ob Volumes/Replicas sauber wieder aufgebaut werden.

Hinweise:

- `join.yml` liest das Join-Token automatisch vom Init-Server (`k3s_init` im Inventory). Der Init-Server muss per SSH erreichbar sein.
- Wenn du den neuen Node als Storage-Node verwendest: setze ggf. `longhorn_manage_disk` + `longhorn_disk_device_by_host` in `group_vars/all.yml`, bevor du `join.yml` ausführst.

## Hinweise

- Dieses Setup nutzt **k3s kubectl** auf dem Init-Node (kein lokales `kubectl` nötig).
- In deinem Repo war in `Kubernetes/k3s/config-clusternode2.yaml` ein echter Join-Token enthalten; der ist jetzt durch einen Platzhalter ersetzt. Wenn der Token früher irgendwo genutzt wurde: bitte k3s Token rotieren bzw. Cluster neu erzeugen.

## Longhorn: dedizierte Partition/SSD

Wenn `/var/lib/longhorn-data` auf eine eigene Partition/SSD soll, aktiviere das in `group_vars/all.yml`:

- `longhorn_manage_disk: true`
- `longhorn_disk_device_by_host:` (pro Storage-Node das Device angeben)

Beispiel:

```yaml
longhorn_manage_disk: true
longhorn_disk_device_by_host:
  # Aktueller Stand bei dir: /var/lib/longhorn-data liegt auf /dev/sda3
  clusternode1: /dev/sda3
  clusternode2: /dev/sda3
```

Wichtig: Das ist **destruktiv**, falls das Device schon Daten hat (Partition/Format). Die Rolle verweigert das Überschreiben, wenn bereits ein anderes Filesystem existiert.

Hinweis: Geräte wie `/dev/sdb`/`/dev/sdc`, die unter `/var/lib/kubelet/.../csi/driver.longhorn.io/...` gemountet sind, sind i.d.R. **von Longhorn selbst erzeugte Block-Devices** (replicas/volumes) und dürfen hier nicht als "Longhorn-Disk" konfiguriert werden.

## Was als Nächstes Sinn macht

- Longhorn Disk Mounts: falls `/var/lib/longhorn-data` auf eine eigene Partition soll, ergänzen wir ein Ansible-Task, das z.B. ein Volume formatiert/mounted.
- Nextcloud Credentials/SMTP/OIDC etc.: dafür am besten Secrets/Vault.
