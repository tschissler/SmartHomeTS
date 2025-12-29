# Setting up a k3s cluster

## Expected setup for nodes
- Installed Ubuntu Server (minimized version is OK).
- Node is connected via Wifi (will be changed during the setup to ethernet). 
  On the Mini-PCs we have to install a driver before Ethernet will work 
- SSH is enabled

## Set console font size

```bash
sudo dpkg-reconfigure console-setup
```

## Enable SSH access for audible

- Generate key file
```
ssh-keygen -t rsa -b 4096 -f ~/.ssh/id_rsa -N ""
```

- Run playbook

```bash
# Site auf Test-Cluster
ansible-playbook cluster.yml -i inventory.ini --limit testservers

# Oder mit den Wrapper-Skripten
chmod +x *.sh
./run-on-test.sh -K -K
./run-on-prod.sh -K -K
```