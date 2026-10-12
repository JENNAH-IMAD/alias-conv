param([Parameter(Mandatory)][string]$AppRoot)
$ErrorActionPreference='Stop'
foreach($taskName in @('AliasFrontend','AliasBackend','AliasPostgreSQL')) {
    if(Get-Service $taskName -ErrorAction SilentlyContinue) {
        Stop-Service $taskName -Force -ErrorAction Stop
        & (Join-Path $AppRoot "services\$taskName.exe") uninstall
        if($LASTEXITCODE -ne 0){throw "Impossible de desinstaller $taskName"}
    }
}
Get-NetFirewallRule -Name 'AliasFrontend' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
# PostgreSQL and the external data/configuration directory are deliberately retained.
