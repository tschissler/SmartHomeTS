
Invoke-WebRequest -Uri "https://downloads.rclone.org/rclone-current-linux-amd64.zip" -OutFile "rclone-current-linux-amd64.zip"
Expand-Archive -Path "rclone-current-linux-amd64.zip" -DestinationPath "rclone-linux"

kubectl cp .\rclone nextcloud-5b85ddbc9f-56ssq:/usr/local/bin/rclone -n nextcloud
kubectl exec -it nextcloud-5b85ddbc9f-56ssq -n nextcloud -- chmod +x /usr/local/bin/rclone
kubectl exec -it nextcloud-5b85ddbc9f-56ssq -n nextcloud -- /usr/local/bin/rclone version

rclone copy "OneDrive_Privat:/Shared/Bilder" "/var/www/html/data/__groupfolders/1/files/Bilder" --progress --transfers=8 --checkers=16 --fast-list --create-empty-src-dirs --local-encoding UTF-8 --retries 5 --low-level-retries 10

# Import files into NextCloud
php /var/www/html/occ groupfolders:scan 1