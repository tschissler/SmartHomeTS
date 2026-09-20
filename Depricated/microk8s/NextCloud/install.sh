helm repo add nextcloud https://nextcloud.github.io/helm/
helm repo update
kubectl create namespace nextcloud --dry-run=client -o yaml | kubectl apply -f -
helm upgrade --install nextcloud nextcloud/nextcloud `
  --namespace nextcloud `
  -f nextcloud-values.yaml

## Install Collabora Online Helm Chart for NextOffice
helm repo add collabora https://collaboraonline.github.io/online/
helm repo update
helm upgrade --install collabora collabora/collabora-online `
  --namespace nextcloud `
  -f collabora-values.yaml

# 1. Sicherstellen, dass die App auch aktiv ist
kubectl exec -it -n nextcloud deployment/nextcloud -- php occ app:enable richdocuments

# 2. Die Brücke zu Collabora schlagen
kubectl exec -it -n nextcloud deployment/nextcloud -- php occ config:app:set richdocuments wopi_url --value="http://collabora-collabora-online:9980"
kubectl exec -it -n nextcloud deployment/nextcloud -- php occ config:app:set richdocuments wopi_public_url --value="http://office.local"




