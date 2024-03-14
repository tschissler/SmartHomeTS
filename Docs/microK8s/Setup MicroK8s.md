# Install cluster
## Install microk8s

``` bash
sudo snap install microk8s --classic
microk8s status --wait-ready
```

## Adding nodes
``` bash
microk8s add-node
microk8s.kubectl get node
```

## Create an alias for kubectl
``` bash
nano ~/.bashrc
```
-> Add to the end of this file: alias kubectl='microk8s kubectl'

## Enabling Dashboard
``` bash
microk8s enable dashboard
microk8s dashboard-proxy
```

Access dashboard via https://smarthomepi2:10443/

To enable the dashboard-proxy to be started automatically as a service, follow these steps:

``` bash
sudo nano /etc/systemd/system/microk8s.dashboard.service
```

``` config
[Unit]
Description=MicroK8s.Dashboard
After=network.target

[Service]
ExecStart=/snap/bin/microk8s dashboard-proxy
Restart=always
User=thomasschissler
Group=adm

[Install]
WantedBy=multi-user.target
```


``` bash
sudo systemctl daemon-reload
sudo systemctl enable microk8s.dashboard
sudo systemctl start microk8s.dashboard
sudo systemctl status microk8s.dashboard
```

# Get kube.config to allow access from a remote computer
On the Raspberry Pi run
``` bash
microk8s config > kube.config
```
Then copy the created config file to your Windows machine and configure kubectl
``` bash
scp thomasschissler@smarthomepi2:~/kube.config .
copy kube.config ~/.kube/config
kubectl get nodes
```