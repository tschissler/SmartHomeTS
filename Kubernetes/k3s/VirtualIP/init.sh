## on clusternode1

kubectl apply -f https://kube-vip.io/manifests/rbac.yaml
kubectl apply -f ./kube-vip.yaml

## on external machine
ping 192.168.178.220
kubectl get nodes --server=https://192.168.178.220:6443

kubectl apply -f kube-vip-cloud-provider.yaml
kubectl apply -f .\VirtualIP\kube-vip-configmap.yaml


## Check
kubectl get svc traefik -n kube-system ## EXTERNE_IP should be set