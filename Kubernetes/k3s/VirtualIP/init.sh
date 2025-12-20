## on clusternode1
sudo mkdir -p /etc/rancher/k3s

## Copy config.yaml into this folder
sudo systemctl restart k3s

kubectl apply -f https://kube-vip.io/manifests/rbac.yaml
kubectl apply -f ./kube-vip.yaml

## on external machine
ping 192.168.178.220
kubectl get nodes --server=https://192.168.178.220:6443

