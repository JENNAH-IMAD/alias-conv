[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskRuntime = Join-Path $taskRoot 'output\runtime'
$taskFrontend = Join-Path $taskRoot 'frontend'
$taskOldRouteCache=[IO.Path]::GetFullPath((Join-Path $taskRuntime 'frontend\.next-runtime\server\route-cache'))
if(Test-Path -LiteralPath $taskOldRouteCache) {
    $taskSavedRouteCache=[IO.Path]::GetFullPath((Join-Path $taskRoot ('output\previous-route-cache-'+[Guid]::NewGuid().ToString('N'))))
    if(-not($taskOldRouteCache.StartsWith($taskRuntime+'\') -and $taskSavedRouteCache.StartsWith($taskRoot+'\'))){throw 'Chemin hors projet.'}
    Move-Item -LiteralPath $taskOldRouteCache -Destination $taskSavedRouteCache
}
Get-Command dotnet.exe,node.exe,npm.cmd -ErrorAction Stop | Out-Null
$taskPublishedApi=Join-Path $taskRuntime 'backend\BankStatementConverter.API.dll'
if(Test-Path -LiteralPath $taskPublishedApi) {
    try { $taskProbe=[IO.File]::Open($taskPublishedApi,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None); $taskProbe.Dispose() }
    catch { throw 'Arrêter le backend publié avant de remplacer les fichiers de production.' }
}
New-Item -ItemType Directory -Path $taskRuntime -Force | Out-Null
& dotnet publish (Join-Path $taskRoot 'backend\BankStatementConverter.API\BankStatementConverter.API.csproj') -c Release -o (Join-Path $taskRuntime 'backend')
if ($LASTEXITCODE -ne 0) { throw 'La publication du backend a échoué.' }
$taskOldRuntime = $env:ALIAS_RUNTIME_BUILD
$taskOldProxy = $env:API_PROXY_URL
$taskOldPublicApi = $env:NEXT_PUBLIC_API_URL
Push-Location $taskFrontend
try {
    $env:ALIAS_RUNTIME_BUILD = '1'
    $env:API_PROXY_URL = 'http://127.0.0.1:5081'
    $env:NEXT_PUBLIC_API_URL = '/api'
    if (-not (Test-Path -LiteralPath 'node_modules\next\dist\bin\next')) {
        & npm.cmd ci
        if ($LASTEXITCODE -ne 0) { throw 'Installation des dépendances frontend échouée.' }
    }
    & npm.cmd run build
    if ($LASTEXITCODE -ne 0) { throw 'La compilation du frontend a échoué.' }
    $taskStandalone = Join-Path $taskFrontend '.next-runtime\standalone'
    $taskUi = Join-Path $taskRuntime 'frontend'
    New-Item -ItemType Directory -Path $taskUi -Force | Out-Null
    Get-ChildItem -LiteralPath $taskStandalone -Force | Copy-Item -Destination $taskUi -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $taskFrontend 'public') -Destination $taskUi -Recurse -Force
    New-Item -ItemType Directory -Path (Join-Path $taskUi '.next-runtime') -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $taskFrontend '.next-runtime\static') -Destination (Join-Path $taskUi '.next-runtime') -Recurse -Force
} finally {
    Pop-Location
    $env:ALIAS_RUNTIME_BUILD = $taskOldRuntime
    $env:API_PROXY_URL = $taskOldProxy
    $env:NEXT_PUBLIC_API_URL = $taskOldPublicApi
}
New-Item -ItemType Directory -Path (Join-Path $taskRuntime 'scripts') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Start-Alias.ps1') -Destination (Join-Path $taskRuntime 'scripts') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Creer-raccourci.ps1') -Destination (Join-Path $taskRuntime 'scripts') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Alias.Runtime.json.example') -Destination $taskRuntime -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'Lancer-desktop.cmd'),(Join-Path $taskRoot 'Lancer-serveur.cmd') -Destination $taskRuntime -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\INSTALLATION.md') -Destination (Join-Path $taskRuntime 'LIRE-MOI.md') -Force
Write-Output "Application publiée : $taskRuntime"
Write-Output 'La configuration et les données du poste ne sont pas copiées dans ce dossier.'
