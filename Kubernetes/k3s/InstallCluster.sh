## Install k3s on clusternode1 as master

## Enable kube-vip (Virtual IP) 
## See folder VirtualIp

## Install clusternode2 as master
## on clusternode1

sudo nano /etc/systemd/system/k3s.service

        [Unit]
        Description=Lightweight Kubernetes
        Documentation=https://k3s.io
        Wants=network-online.target
        After=network-online.target

        [Install]
        WantedBy=multi-user.target

        [Service]
        Type=notify
        EnvironmentFile=-/etc/default/%N
        EnvironmentFile=-/etc/sysconfig/%N
        EnvironmentFile=-/etc/systemd/system/k3s.service.env
        KillMode=process
        Delegate=yes
        User=root
        # Having non-zero Limit*s causes performance problems due to accounting overhead
        # in the kernel. We recommend using cgroups to do container-local accounting.
        LimitNOFILE=1048576
        LimitNPROC=infinity
        LimitCORE=infinity
        TasksMax=infinity
        TimeoutStartSec=0
        Restart=always
        RestartSec=5s
        ExecStartPre=-/sbin/modprobe br_netfilter
        ExecStartPre=-/sbin/modprobe overlay
        ExecStart=/usr/local/bin/k3s \
            server \
            '--cluster-init' \
            '--tls-san' '192.168.178.220' \
            '--disable' 'traefik' \
            '--disable' 'local-storage' \
            '--write-kubeconfig-mode' '644'

sudo systemctl stop k3s
sudo rm -rf /var/lib/rancher/k3s/server/db
sudo systemctl daemon-reload
sudo systemctl start k3s

sudo cat /var/lib/rancher/k3s/server/node-token

## on clusternode2

sudo mkdir -p /etc/rancher/k3s
# copy config.yaml into /etc/rancer/k3s/config.yaml
sudo systemctl restart k3s

curl -sfL https://get.k3s.io | K3S_TOKEN="<DEIN_TOKEN_VON_NODE1>" sh -s - server \
  --server https://192.168.178.126:6443 \
  --tls-san 192.168.178.220 \
  --etcd-expose-metrics true \
  --write-kubeconfig-mode 644

# clusternode3
curl -sfL https://get.k3s.io | K3S_TOKEN="<TOKEN>" sh -s - server \
  --server https://192.168.178.126:6443 \
  --tls-san 192.168.178.220 \
  --node-ip 192.168.178.75 \
  --advertise-address 192.168.178.75 \
  --node-external-ip 192.168.178.75 \
  --disable traefik \
  --disable local-storage \
  --write-kubeconfig-mode 644

## Fix when kubectl does not work correctly
# 1. Sicherstellen, dass das Verzeichnis existiert
mkdir -p $HOME/.kube

# 2. Die k3s Konfiguration in dein Home-Verzeichnis kopieren
sudo cp /etc/rancher/k3s/k3s.yaml $HOME/.kube/config

# 3. Rechte anpassen, damit dein User darauf zugreifen darf
sudo chown $(id -u):$(id -g) $HOME/.kube/config

# 4. (Optional) In der Datei localhost durch die echte IP ersetzen, 
# falls du Kube-VIP auch lokal ansprechen willst:
sed -i 's/127.0.0.1/192.168.178.220/g' $HOME/.kube/config

# 5. KUBECONFIG permanent in die .bashrc eintragen
if ! grep -q "KUBECONFIG" ~/.bashrc; then
  echo 'export KUBECONFIG=$HOME/.kube/config' >> ~/.bashrc
fi

# 6. Die aktuelle Session aktualisieren
export KUBECONFIG=$HOME/.kube/config