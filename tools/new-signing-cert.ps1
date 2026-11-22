# Создание самоподписанного сертификата для подписи установщика «Центр кофе».
# Использование: powershell -ExecutionPolicy Bypass -File tools\new-signing-cert.ps1
#
# Скрипт создаёт сертификат Code Signing в хранилище текущего пользователя и
# экспортирует ТОЛЬКО открытую часть в release\CenterCoffee.cer.
# Приватный ключ остаётся на этом компьютере и в репозиторий не попадает.
param(
    [string]$Subject = "CN=ИП Миргородский, O=ИП Миргородский, C=RU",
    [int]$Years = 3,
    [string]$OutFile = "release\CenterCoffee.cer"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$outPath = if ([System.IO.Path]::IsPathRooted($OutFile)) { $OutFile } else { Join-Path $root $OutFile }

$existing = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -eq $Subject -and $_.HasPrivateKey } |
    Sort-Object NotAfter -Descending |
    Select-Object -First 1

if ($existing) {
    Write-Host "==> Сертификат уже есть, создаём заново не будем:" -ForegroundColor Yellow
    $cert = $existing
}
else {
    Write-Host "==> Создание сертификата..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -KeyUsage DigitalSignature `
        -FriendlyName "CenterCoffee Code Signing" `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears($Years)
}

$directory = Split-Path -Parent $outPath
New-Item -ItemType Directory -Force -Path $directory | Out-Null
Export-Certificate -Cert $cert -FilePath $outPath -Force | Out-Null

Write-Host ""
Write-Host "Отпечаток (SHA-1): $($cert.Thumbprint)"
Write-Host "Действует до:      $($cert.NotAfter.ToString('yyyy-MM-dd'))"
Write-Host "Открытая часть:    $outPath"
Write-Host ""
Write-Host "Подписать установщик:" -ForegroundColor Green
Write-Host "  powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 -Version <версия> -CertificateThumbprint $($cert.Thumbprint)"
Write-Host ""
Write-Host "На новой машине перед установкой (от администратора):" -ForegroundColor Green
Write-Host "  certutil -addstore -f Root `"CenterCoffee.cer`""
Write-Host "  certutil -addstore -f TrustedPublisher `"CenterCoffee.cer`""