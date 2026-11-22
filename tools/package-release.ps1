# Сборка релиза «Центр кофе»: приложение и установщик.
# Использование: powershell -ExecutionPolicy Bypass -File tools\package-release.ps1 [-Version 0.1.0] [-Portable]
#   -Version   версия продукта в MSI (по умолчанию 0.1.0)
#   -Portable  дополнительно положить exe и dll в корень решения и создать ярлык
#              на рабочем столе (запуск без установки)
param(
    [string]$Version = "0.1.0",
    [switch]$Portable
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
# /t:Rebuild обязателен: смена -Version не считается изменением входных данных,
# и без пересборки в release/ попадёт MSI от предыдущей версии.
& $msbuild "$root\installer\CenterCoffee.Installer.wixproj" `
    /t:Rebuild /p:Configuration=Release /p:Version=$Version /v:m /nologo
if ($LASTEXITCODE -ne 0) { throw "Сборка установщика завершилась с ошибкой." }

# Установщик хранится в репозитории: на него ведёт ссылка в README.
$release = Join-Path $root "release"
New-Item -ItemType Directory -Force -Path $release | Out-Null
$msi = Join-Path $release "CenterCoffee.Setup.msi"
Copy-Item "$root\installer\bin\Release\CenterCoffee.Setup.msi" $msi -Force
$hash = (Get-FileHash $msi -Algorithm SHA256).Hash
Write-Host "    Установщик: release\CenterCoffee.Setup.msi (версия $Version)"
Write-Host "    SHA-256:    $hash"

if ($Portable) {
    # Рабочий экземпляр без установки: exe и dll рядом, ярлык на рабочем столе.
    # В git эти файлы не попадают (см. .gitignore) — это только локальное удобство.
    Write-Host "==> Копирование exe и dll в корень решения..." -ForegroundColor Cyan
    $bin = "$root\src\CenterCoffee.WPF\bin\Release"
    Copy-Item "$bin\CenterCoffee.WPF.exe" $root -Force
    Copy-Item "$bin\CenterCoffee.WPF.exe.config" $root -Force
    Copy-Item "$bin\CenterCoffee.*.dll" $root -Force

    $shell = New-Object -ComObject WScript.Shell
    $shortcutPath = Join-Path ([Environment]::GetFolderPath("Desktop")) "Центр кофе.lnk"
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = "$root\CenterCoffee.WPF.exe"
    $shortcut.WorkingDirectory = $root
    $shortcut.Description = "Учётное приложение кофейни «Центр кофе»"
    $shortcut.Save()
    Write-Host "    Ярлык: $shortcutPath"
}

Write-Host "==> Готово." -ForegroundColor Green
