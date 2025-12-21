winget install Helm.Helm



sudo apt-get update && sudo apt-get install -y iputils-ping


 # Execute check on all nodes
curl -sSfL https://raw.githubusercontent.com/longhorn/longhorn/v1.7.2/scripts/environment_check.sh | bash


#!/bin/bash
# prepare-nodes.sh - Installiert Abhängigkeiten für Longhorn auf allen Nodes
NODES=("clusternode1" "clusternode2" "clusternode3" )

for NODE in "${NODES[@]}"; do
  echo "--- Preparing $NODE ---"
  # 1. Abhängigkeiten installieren
  ssh $NODE "sudo apt-get update && sudo apt-get install -y jq open-iscsi nfs-common util-linux"
  
  # 2. iSCSI Modul laden und für Reboot vormerken
  ssh $NODE "sudo modprobe iscsi_tcp && echo 'iscsi_tcp' | sudo tee -a /etc/modules"
  
  # 3. iSCSI Dienst starten
  ssh $NODE "sudo systemctl enable --now iscsid"
done

echo "--- Alle Nodes vorbereitet. Starte Environment Check erneut... ---"


# Markiere die Mini-PCs als exklusive Storage-Nodes
kubectl label node clusternode1 node.longhorn.io/create-default-disk=true
kubectl label node clusternode2 node.longhorn.io/create-default-disk=true

# Markiere die Pis explizit (optional, zur besseren Übersicht)
kubectl label node clusternode3 node.longhorn.io/create-default-disk=false