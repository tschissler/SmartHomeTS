# smarthome_app

A new Flutter project.

## Getting Started

This project is a starting point for a Flutter application.

A few resources to get you started if this is your first Flutter project:

- [Lab: Write your first Flutter app](https://docs.flutter.dev/get-started/codelab)
- [Cookbook: Useful Flutter samples](https://docs.flutter.dev/cookbook)

For help getting started with Flutter development, view the
[online documentation](https://docs.flutter.dev/), which offers tutorials,
samples, guidance on mobile development, and a full API reference.


# Deployment to Google Play Store
## Create a service account
Create a new service account here https://console.cloud.google.com/projectselector2/iam-admin/serviceaccounts
More information: https://damienaicheh.github.io/azure/devops/2021/10/25/configure-azure-devops-google-play-en.html

## Provide the service account JSON
``` Powershell
 [convert]::ToBase64String((Get-Content -path "smarthome-app-16d1b-cb8873d5fc10.json" -Encoding byte)) > service-account.json.base64  
```

Use the JSON in the workflow
```YAML
    - name: Decode Google Play Service Account JSON
      run: |
        echo "${{ secrets.GOOGLE_PLAY_SERVICE_ACCOUNT_JSON }}" | base64 -d > service-account.json
        
    - name: Deploy to Google Play
      uses: r0adkll/upload-google-play@v1
      with:
        serviceAccountJson: service-account.json
        packageName: "dev.tschissler.smarthome_app"
        releaseFiles: ./${{ env.PROJECT_NAME }}/build/app/outputs/bundle/release/app-release.aab
        track: internal
```
