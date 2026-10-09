<#
.SYNOPSIS
  Збирає застосунок у теку publish/: UI (npm run build) → wwwroot, бекенд — dotnet publish.
.EXAMPLE
  ./publish.ps1                       # framework-dependent, поточна ОС
  ./publish.ps1 -Runtime linux-x64    # self-contained під Linux
.NOTES
  Запуск зібраного: publish/TTemplate.Api(.exe) --urls http://0.0.0.0:5180
  Дані (SQLite, ключі, заявки) — publish/data (налаштування "DataDir" в appsettings.json).
#>
param(
    [string]$Runtime,
    [string]$Out = (Join-Path $PSScriptRoot 'publish')
)
$ErrorActionPreference = 'Stop'
$ui = Join-Path $PSScriptRoot 'src/TTemplate.UI'
$api = Join-Path $PSScriptRoot 'src/TTemplate.Api'

npm run build --prefix $ui
if ($LASTEXITCODE -ne 0) { throw 'npm run build не вдався' }

$publishArgs = @('publish', $api, '-c', 'Release', '-o', $Out, '-nologo')
if ($Runtime) { $publishArgs += @('-r', $Runtime, '--self-contained', 'true') }
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish не вдався' }

$www = Join-Path $Out 'wwwroot'
if (Test-Path $www) { Remove-Item $www -Recurse -Force }
Copy-Item (Join-Path $ui 'dist') $www -Recurse
Write-Host "Готово: $Out" -ForegroundColor Green
