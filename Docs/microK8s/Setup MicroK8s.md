# Install cluster

## Prepare raspberry
``` bash
sudo nano /boot/firmware/cmdline.txt
```

and add
``` bash
cgroup_enable=memory cgroup_memory=1
```

## Install microk8s

``` bash
sudo apt install snapd

sudo snap install microk8s --classic
sudo reboot 

sudo usermod -a -G microk8s thomasschissler
newgrp microk8s

microk8s status --wait-ready
```

⚠️ Important: On Ubuntu OS install this package, otherwise calisto will not run successfully
``` bash
sudo apt install linux-modules-extra-raspi
```


## Uninstall microk8s
``` bash
sudo snap remove microk8s
sudo snap saved
sudo snap forget <Id>
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

Access dashboard via https://smarthomepi2:10443/.
To get the token, use
``` bash
kubectl describe secret -n kube-system microk8s-dashboard-token
```

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
copy kube.config ~/.kube/kube.config
kubectl get nodes
```

# Deploy Services
## Mosquitto

``` bash
kubectl apply -f .\mosquitto-configmap.yaml
kubectl apply -f .\mosquitto-deployment.yaml
kubectl apply -f .\mosquitto-service.yaml
```

## StorageConnector
``` pwsh
(Get-Content .\kube.yaml) | Foreach-Object {
     $_ -replace 'env:SMARTHOMESTORAGEKEY', $env:SMARTHOMESTORAGEKEY `
        -replace 'env:SMARTHOMESTORAGEURI', $env:SMARTHOMESTORAGEURI
 } | kubectl apply -f -
```

## MongoDB
``` bash
TMPFILE=$(mktemp)
/usr/bin/openssl rand -base64 741 > $TMPFILE
kubectl create secret generic shared-bootstrap-data --from-file=internal-auth-mongodb-keyfile=$TMPFILE
rm $TMPFILE

kubectl apply -f .\mongodb-volume.yaml
kubectl apply -f .\mongodb-service.yaml
kubectl apply -f .\mongodb-statefulset.yaml
```

Delete StatefulSet before re-deploying it
``` bash
kubectl delete -n default persistentvolumeclaim mongodb-persistent-storage-claim-mongod-0
kubectl delete -n default statefulset mongod
```