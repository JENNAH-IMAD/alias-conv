[CmdletBinding()]
param(
    [ValidateSet('Local','Server','Desktop')][string]$Mode='Local',
    [ValidateRange(1024,65535)][int]$Port=3002,
    [string]$ConfigFile='',
    [switch]$Prepare,
    [switch]$Open
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskProject=Join-Path $taskRoot 'backend\BankStatementConverter.API\BankStatementConverter.API.csproj'
$taskSources=Test-Path -LiteralPath $taskProject
$taskRuntime=if($taskSources){Join-Path $taskRoot 'output\runtime'}else{$taskRoot}
$taskLogs=Join-Path $taskRoot '.local\launch'
New-Item -ItemType Directory -Path $taskLogs -Force | Out-Null
Get-Command dotnet.exe,node.exe -ErrorAction Stop | Out-Null
function Test-AliasApi {
    try { return (Invoke-RestMethod -Uri 'http://127.0.0.1:5081/health' -TimeoutSec 2).status -eq 'ok' } catch { return $false }
}
function Test-AliasUi {
    try { $taskSession=Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/auth/session" -TimeoutSec 3; return $taskSession.PSObject.Properties.Name -contains 'setupRequired' } catch { return $false }
}
function Test-AliasPort([int]$Number) {
    $taskSocket=New-Object Net.Sockets.TcpClient
    try { $taskSocket.Connect('127.0.0.1',$Number); return $true } catch { return $false } finally { $taskSocket.Dispose() }
}
function Start-AliasProcess([string]$Executable,[string[]]$Arguments,[string]$Directory,[hashtable]$Variables,[string]$Name) {
    $taskOriginal=@{}
    foreach($taskKey in $Variables.Keys) { $taskOriginal[$taskKey]=[Environment]::GetEnvironmentVariable($taskKey,'Process'); [Environment]::SetEnvironmentVariable($taskKey,$Variables[$taskKey],'Process') }
    try {
        $taskQuoted=($Arguments | ForEach-Object { '"'+$_+'"' }) -join ' '
        return Start-Process -FilePath $Executable -ArgumentList $taskQuoted -WorkingDirectory $Directory -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskLogs "$Name.out.log") -RedirectStandardError (Join-Path $taskLogs "$Name.err.log")
    } finally { foreach($taskKey in $taskOriginal.Keys) { [Environment]::SetEnvironmentVariable($taskKey,$taskOriginal[$taskKey],'Process') } }
}
if($Mode -ne 'Local') {
    if($Prepare -or -not(Test-Path -LiteralPath (Join-Path $taskRuntime 'frontend\server.js'))) {
        if(-not $taskSources){throw 'Le dossier publié est incomplet. Relancer Prepare-Alias.ps1 depuis les sources.'}
        & (Join-Path $PSScriptRoot 'Prepare-Alias.ps1')
    }
    if(-not $ConfigFile) {
        if($Mode -eq 'Desktop' -and $taskSources){$ConfigFile=Join-Path $taskRoot 'backend\BankStatementConverter.API\appsettings.Development.json'}
        else {$ConfigFile=Join-Path $taskRuntime 'Alias.Runtime.json'}
    }
    $ConfigFile=[IO.Path]::GetFullPath($ConfigFile)
    if(-not(Test-Path -LiteralPath $ConfigFile)){throw "Configurer $ConfigFile à partir de Alias.Runtime.json.example avant ce lancement."}
}
if(-not(Test-AliasApi)) {
    if(Test-AliasPort 5081){throw 'Le port API 5081 est occupé ou PostgreSQL est indisponible. Vérifier /health avant de relancer.'}
    $taskVariables=@{ASPNETCORE_ENVIRONMENT=if($Mode -eq 'Local'){'Development'}else{'Production'}}
    if($ConfigFile){$taskVariables['ALIAS_CONFIG_FILE']=[IO.Path]::GetFullPath($ConfigFile)}
    if($Mode -eq 'Local') {
        if(-not $taskSources){throw 'Le mode Local nécessite les sources. Utiliser Desktop ou Server avec une version publiée.'}
        & dotnet build $taskProject
        if($LASTEXITCODE -ne 0){throw 'La compilation du backend a échoué.'}
        $taskBackend=Join-Path $taskRoot 'backend\BankStatementConverter.API'
        $taskArgs=@('run','--no-build','--no-launch-profile','--project',$taskProject,'--urls','http://127.0.0.1:5081')
    } else {
        $taskBackend=Join-Path $taskRuntime 'backend'
        $taskArgs=@((Join-Path $taskBackend 'BankStatementConverter.API.dll'),'--urls','http://127.0.0.1:5081')
        if($Mode -eq 'Desktop' -and $taskSources -and $ConfigFile -eq (Join-Path $taskRoot 'backend\BankStatementConverter.API\appsettings.Development.json')) {
            $taskSettings=Get-Content -LiteralPath $ConfigFile -Raw | ConvertFrom-Json
            $taskStorage=if($taskSettings.Storage.Root){$taskSettings.Storage.Root}else{'uploads'}
            if(-not[IO.Path]::IsPathRooted($taskStorage)){$taskStorage=Join-Path (Join-Path $taskRoot 'backend\BankStatementConverter.API') $taskStorage}
            $taskVariables['Storage__Root']=[IO.Path]::GetFullPath($taskStorage)
        }
    }
    $taskApiProcess=Start-AliasProcess (Get-Command dotnet.exe).Source $taskArgs $taskBackend $taskVariables 'api'
    for($taskAttempt=0;$taskAttempt -lt 45;$taskAttempt++) { if(Test-AliasApi){break}; if($taskApiProcess.HasExited){throw "L'API s'est arrêtée. Consulter $taskLogs\api.err.log"}; Start-Sleep -Milliseconds 1000 }
    if(-not(Test-AliasApi)){throw "API non prête. Vérifier PostgreSQL et $taskLogs\api.out.log"}
} else { Write-Output 'API déjà disponible sur le port 5081.' }
if(-not(Test-AliasUi)) {
    if(Test-AliasPort $Port){throw "Le port $Port est occupé par un autre service. Choisir -Port 3003, par exemple."}
    $taskHost=if($Mode -eq 'Server'){'0.0.0.0'}else{'127.0.0.1'}
    if($Mode -eq 'Local') {
        $taskUi=Join-Path $taskRoot 'frontend'
        if(-not(Test-Path -LiteralPath (Join-Path $taskUi 'node_modules\next\dist\bin\next'))) {
            Push-Location $taskUi
            try { & npm.cmd ci; if($LASTEXITCODE -ne 0){throw 'Installation frontend échouée.'} } finally { Pop-Location }
        }
        $taskArgs=@((Join-Path $taskUi 'node_modules\next\dist\bin\next'),'dev','--port',[string]$Port,'--hostname',$taskHost)
    } else { $taskUi=Join-Path $taskRuntime 'frontend';$taskArgs=@((Join-Path $taskUi 'server.js')) }
    $taskUiProcess=Start-AliasProcess (Get-Command node.exe).Source $taskArgs $taskUi @{PORT=[string]$Port;HOSTNAME=$taskHost;API_PROXY_URL='http://127.0.0.1:5081';NEXT_PUBLIC_API_URL='/api'} 'frontend'
    for($taskAttempt=0;$taskAttempt -lt 60;$taskAttempt++){if(Test-AliasUi){break};if($taskUiProcess.HasExited){throw "Le frontend s'est arrêté. Consulter $taskLogs\frontend.err.log"};Start-Sleep -Milliseconds 1000}
    if(-not(Test-AliasUi)){throw "Interface non prête. Consulter $taskLogs\frontend.out.log"}
} else {
    if($Mode -eq 'Server') {
        $taskListeners=Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction Stop
        if(-not($taskListeners | Where-Object { $_.LocalAddress -notin @('127.0.0.1','::1') })) {
            throw "L'interface existante écoute seulement sur ce PC. Choisir un port libre avec -Port 3003 pour le mode Server."
        }
    }
    Write-Output "Interface déjà disponible sur le port $Port."
}
$taskUrl="http://localhost:$Port"
Write-Output "Alias est prêt : $taskUrl"
if($Mode -eq 'Server'){Write-Output "Accès des autres PC : http://ADRESSE_IP_DU_SERVEUR:$Port (autoriser ce port dans le pare-feu du serveur)."}
if($Open) {
    $taskEdge=Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'
    if($Mode -eq 'Desktop' -and (Test-Path -LiteralPath $taskEdge)){Start-Process -FilePath $taskEdge -ArgumentList "--app=$taskUrl"}
    else {Start-Process $taskUrl}
}
