$templatePath = "SecretsTemplate.cs"
$outputPath = "Secrets.cs"

$content = Get-Content $templatePath
$content = $content -replace "<The Uri to the Azure Table Storage>", $env:STORAGE_URI
$content = $content -replace "<The key to access the Azure Table Storage>", $env:STORAGE_KEY
$content = $content -replace "<The License Key for Syncfusion Controls>", $env:SYNCFUSION_LICENSEKEY

$content | Set-Content $outputPath
