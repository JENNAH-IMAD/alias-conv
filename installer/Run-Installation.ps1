[CmdletBinding()]
param([Parameter(Mandatory)][string]$AppRoot,[Parameter(Mandatory)][string]$SettingsFile)
$ErrorActionPreference='Stop'
$taskLog=Join-Path $AppRoot 'installer\installation.log'
function Hide-InstallationSecrets([string]$Message) {
    $Message=[regex]::Replace($Message,'(?i)password\s*=\s*("[^"]*"|''[^'']*''|[^;\r\n]+)','Password=***')
    $Message=[regex]::Replace($Message,'(?i)password\s+''[^'']*''',"PASSWORD '***'")
    return [regex]::Replace($Message,'\b[a-fA-F0-9]{64}\b','***')
}
try {
    "Installation commencee : $([DateTime]::UtcNow.ToString('u'))" | Set-Content -LiteralPath $taskLog -Encoding UTF8
    & (Join-Path $PSScriptRoot 'Configure-Server.ps1') -AppRoot $AppRoot -SettingsFile $SettingsFile |
        ForEach-Object { Hide-InstallationSecrets ([string]$_) | Add-Content -LiteralPath $taskLog -Encoding UTF8 }
    'Installation terminee.' | Add-Content -LiteralPath $taskLog -Encoding UTF8
} catch {
    Hide-InstallationSecrets $_.Exception.Message | Add-Content -LiteralPath $taskLog -Encoding UTF8
    exit 1
}
