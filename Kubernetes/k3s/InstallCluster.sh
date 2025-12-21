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



sudo mkdir -p /etc/rancher/k3s
# copy config.yaml into /etc/rancer/k3s/config.yaml
sudo nano /etc/rancher/k3s/config.yaml
curl -sfL https://get.k3s.io | sh -

sudo cat /var/lib/rancher/k3s/server/node-token













## Fix when kubectl does not work correctly
cat /etc/rancher/k3s/k3s.yaml
mkdir -p $HOME/.kube
sudo cp /etc/rancher/k3s/k3s.yaml $HOME/.kube/config
sudo chown $(id -u):$(id -g) $HOME/.kube/config
if ! grep -q "KUBECONFIG" ~/.bashrc; then
  echo 'export KUBECONFIG=$HOME/.kube/config' >> ~/.bashrc
fi
chmod 600 ~/.kube/config
export KUBECONFIG=$HOME/.kube/config


kubectl label node clusternode1 node-role.kubernetes.io/storage=true capability/architecture=x86
kubectl label node clusternode2 node-role.kubernetes.io/storage=true capability/architecture=x86
kubectl label node clusternode3 capability/architecture=arm64 node-role.kubernetes.io/worker=worker


## Health check
sudo systemctl status k3s
sudo k3s etcd-snapshot list
sudo k3s kubectl get componentstatuses
sudo k3s kubectl get nodes -o custom-columns=NAME:.metadata.name,IP:.status.addresses[0].address




## Traefik
helm repo add traefik https://traefik.github.io/charts
helm repo update
helm install traefik traefik/traefik --namespace kube-system --create-namespace -f traefik-values.yaml