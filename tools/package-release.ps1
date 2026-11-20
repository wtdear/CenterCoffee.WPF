# Сборка релиза «Центр кофе»: приложение, установщик, exe в корне и ярлык.
# Использование: powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 [-Version 1.0.0]
param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$msbuild = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
if (-not (Test-Path $msbuild)) {
    # Найти MSBuild последней установленной Visual Studio через vswhere.
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild `
            -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
    }
}
if (-not (Test-Path $msbuild)) {
    throw "MSBuild не найден. Установите Visual Studio с workload «Разработка классических приложений .NET»."
}

Write-Host "==> Сборка приложения (Release)..." -ForegroundColor Cyan
& $msbuild "$root\src\CenterCoffee.WPF\CenterCoffee.WPF.csproj" `
    /p:Configuration=Release /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw "Сборка приложения завершилась с ошибкой." }

Write-Host "==> Сборка установщика..." -ForegroundColor Cyan
& $msbuild "$root\installer\CenterCoffee.Installer.wixproj" `
    /p:Configuration=Release /p:Version=$Version /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw "Сборка установщика завершилась с ошибкой." }

$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
Copy-Item "$root\installer\bin\Release\CenterCoffee.Setup.msi" $dist -Force
Write-Host "    Установщик: dist\CenterCoffee.Setup.msi (версия $Version)"

# Копия запускаемого набора в корень решения: exe и dll рядом, ярлык рядом.
# Это рабочий экземпляр без установки: его можно запускать прямо из репозитория.
Write-Host "==> Копирование exe и dll в корень решения..." -ForegroundColor Cyan
$release = "$root\src\CenterCoffee.WPF\bin\Release"
Copy-Item "$release\CenterCoffee.WPF.exe" $root -Force
Copy-Item "$release\CenterCoffee.WPF.exe.config" $root -Force
Copy-Item "$release\CenterCoffee.*.dll" $root -Force
Write-Host "    Запуск: CenterCoffee.WPF.exe в корне решения"

# Ярлык на рабочий стол к exe в корне решения.
$shell = New-Object -ComObject WScript.Shell
$shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Центр кофе.lnk"
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = "$root\CenterCoffee.WPF.exe"
$shortcut.WorkingDirectory = $root
$shortcut.Description = "Учётное приложение кофейни «Центр кофе»"
$shortcut.Save()
Write-Host "    Ярлык: $shortcutPath"

Write-Host "==> Готово." -ForegroundColor Green
