$templatePath = "SecretsTemplate.cs"
$outputPath = "Secrets.cs"

$content = Get-Content $templatePath
$content = $content -replace "<The Uri to the Azure Table Storage>", $env:STORAGE_URI
$content = $content -replace "<The key to access the Azure Table Storage>", $env:STORAGE_KEY
$content | Set-Content $outputPath
