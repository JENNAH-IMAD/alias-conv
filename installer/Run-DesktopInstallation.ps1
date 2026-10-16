param([Parameter(Mandatory)][string]$AppRoot,[Parameter(Mandatory)][string]$SettingsFile)
$ErrorActionPreference='Stop'
$taskLog=Join-Path $AppRoot 'installer\desktop-installation.log'
try {
    'Installation desktop commencee.' | Set-Content -LiteralPath $taskLog -Encoding UTF8
    & (Join-Path $PSScriptRoot 'Configure-Desktop.ps1') -AppRoot $AppRoot -SettingsFile $SettingsFile | ForEach-Object {
        ([regex]::Replace([string]$_,'(?i)(password\s*=\s*)[^;\r\n]+','$1***')) | Add-Content -LiteralPath $taskLog -Encoding UTF8
    }
    'Installation terminee.' | Add-Content -LiteralPath $taskLog -Encoding UTF8
} catch { $_.Exception.Message | Add-Content -LiteralPath $taskLog -Encoding UTF8; exit 1 }
