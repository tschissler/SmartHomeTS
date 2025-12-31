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

## Enable SSH access for ansible

- Generate key file
```
ssh-keygen -t rsa -b 4096 -f ~/.ssh/id_rsa -N ""
```

## Enable sudo access for Raspberry Pi OS
Open a root shell (needs a TTY):

``` bash
ssh -t thomasschissler@k3snode3 'sudo -s'
ssh -t thomasschissler@k3snode4 'sudo -s'
```

Create a sudoers drop-in (use visudo so you don’t brick sudo):
``` bash
EDITOR=nano visudo -f /etc/sudoers.d/90-ansible
```
Put exactly this content (adjust username if needed):

```
thomasschissler ALL=(ALL) NOPASSWD: ALL
```

Validate and test:
``` bash
visudo -c
sudo -n true && echo OK
```

# Install k3s

```bash
# Site auf Test-Cluster
ansible-playbook cluster.yml -i inventory.ini --limit testservers

# Oder mit den Wrapper-Skripten
chmod +x *.sh
./run-on-test.sh -K -k
./run-on-prod.sh -K -k

# Add-ons (kube-vip, later more)
./run-addons-test.sh -K -k
./run-addons-prod.sh -K -k
```

# Install kube-VIP
```bash
ansible-playbook plays/configure-kubevip.yml -i inventory.ini --limit k3s_servers -K -k
```

# Fix kube-VIP / API VIP certificate SANs (only)
```bash
ansible-playbook plays/configure-kubevip.yml -i inventory.ini --limit k3s_servers -K -k --tags cert
```

Check kube-VIP
```bash
kubectl get nodes --server=https://192.168.178.222:6443
```