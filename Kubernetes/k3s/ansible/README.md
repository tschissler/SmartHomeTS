# Setting up a k3s cluster

## Expected setup for nodes
- Installed Ubuntu Server (minimized version is OK).
- Node is connected via Wifi (will be changed during the setup to ethernet)
- SSH is enabled

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
./run-on-test.sh -K
./run-on-prod.sh -K
```