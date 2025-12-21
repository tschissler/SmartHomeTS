## Check data partition
lsblk


helm repo add longhorn https://charts.longhorn.io
helm repo update
helm upgrade --install longhorn longhorn/longhorn `
  --namespace longhorn-system `
  --create-namespace `
  -f longhorn-values.yaml

# Für Clusternode 1
kubectl annotate node clusternode1 node.longhorn.io/default-disks-config='[{"path":"/var/lib/longhorn-data","allowScheduling":true}]' --overwrite

# Für Clusternode 2
kubectl annotate node clusternode2 node.longhorn.io/default-disks-config='[{"path":"/var/lib/longhorn-data","allowScheduling":true}]' --overwrite