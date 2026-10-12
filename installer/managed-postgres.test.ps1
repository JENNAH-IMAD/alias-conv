[CmdletBinding()]
param([string]$PgRoot='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $PgRoot){$PgRoot=Join-Path $taskRoot 'output\server-package\postgresql'}
. (Join-Path $PSScriptRoot 'Managed-Postgres.ps1')
$taskData=Join-Path $taskRoot ('output\postgres-test-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskData,(Join-Path $taskData 'logs') -Force | Out-Null
$taskProcess=$null
$taskApiProcess=$null
$taskOldPassword=[Environment]::GetEnvironmentVariable('PGPASSWORD','Process')
try {
    $taskPlan=Initialize-AliasPostgres $taskData $PgRoot @(3002,5081) -ProtectCredentials
    if($taskPlan.AdminPassword -eq $taskPlan.AppPassword){throw 'Identifiants non distincts.'}
    $taskRules=(Get-Acl -LiteralPath (Join-Path $taskData 'Postgres.Admin.json')).GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])
    if($taskRules | Where-Object { $_.IdentityReference.Value -eq 'S-1-5-20' -and $_.AccessControlType -eq 'Allow' }){throw 'Identifiants de maintenance accessibles aux services applicatifs.'}
    $taskProcess=Start-Process -FilePath (Join-Path $PgRoot 'bin\postgres.exe') -ArgumentList ('-D "'+(Join-Path $taskData 'postgres')+'"') -WorkingDirectory $PgRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskData 'logs\stdout.log') -RedirectStandardError (Join-Path $taskData 'logs\stderr.log')
    $taskConnection=Complete-AliasPostgres $taskData $PgRoot $taskPlan
    $env:PGPASSWORD=$taskPlan.AppPassword
    $taskArguments=@('-X','-h','127.0.0.1','-p',[string]$taskPlan.Port,'-U','alias_app','-d','alias','-v','ON_ERROR_STOP=1','-Atq')
    $taskResult=& (Join-Path $PgRoot 'bin\psql.exe') @taskArguments '-c' "SELECT current_user, rolsuper FROM pg_roles WHERE rolname = current_user"
    if($LASTEXITCODE -ne 0 -or $taskResult -ne 'alias_app|f'){throw 'Compte applicatif incorrect.'}
    & (Join-Path $PgRoot 'bin\psql.exe') @taskArguments '-c' 'CREATE TABLE install_sentinel (value integer); INSERT INTO install_sentinel VALUES (42)' | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Creation de table par le compte applicatif impossible.'}
    $taskReused=Initialize-AliasPostgres $taskData $PgRoot @(3002,5081) -ProtectCredentials
    $taskReusedConnection=Complete-AliasPostgres $taskData $PgRoot $taskReused
    if($taskReusedConnection -ne $taskConnection){throw 'Les identifiants existants ont ete remplaces.'}
    $taskValue=& (Join-Path $PgRoot 'bin\psql.exe') @taskArguments '-c' 'SELECT value FROM install_sentinel'
    if($LASTEXITCODE -ne 0 -or $taskValue -ne '42'){throw 'Les donnees existantes ont ete perdues.'}
    $taskConfig=Get-Content -LiteralPath (Join-Path $taskData 'postgres\postgresql.auto.conf') -Raw
    if($taskConfig -notmatch "listen_addresses = '127.0.0.1'"){throw 'La base est exposee au reseau.'}
    $taskApi=Join-Path $taskRoot 'output\server-package\backend\BankStatementConverter.API.exe'
    if(-not(Test-Path -LiteralPath $taskApi)){throw 'Backend publie absent pour verifier les migrations.'}
    $taskRuntime=Get-Content -LiteralPath (Join-Path $taskRoot 'scripts\Alias.Runtime.json.example') -Raw | ConvertFrom-Json
    $taskRuntime.ConnectionStrings.Default=$taskConnection
    $taskRuntime.Storage.Root=Join-Path $taskData 'uploads'
    $taskRuntime.Database.AutoMigrate=$true
    $taskRuntimePath=Join-Path $taskData 'Api.Test.json'
    $taskRuntime | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $taskRuntimePath -Encoding UTF8
    $taskReserve=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0)
    $taskReserve.Start(); $taskApiPort=$taskReserve.LocalEndpoint.Port; $taskReserve.Stop()
    $taskPreviousConfig=[Environment]::GetEnvironmentVariable('ALIAS_CONFIG_FILE','Process')
    $taskPreviousEnvironment=[Environment]::GetEnvironmentVariable('ASPNETCORE_ENVIRONMENT','Process')
    try {
        $env:ALIAS_CONFIG_FILE=$taskRuntimePath
        $env:ASPNETCORE_ENVIRONMENT='Production'
        $taskApiProcess=Start-Process -FilePath $taskApi -ArgumentList "--urls http://127.0.0.1:$taskApiPort" -WorkingDirectory (Split-Path $taskApi) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $taskData 'logs\api.out.log') -RedirectStandardError (Join-Path $taskData 'logs\api.err.log')
    } finally {
        [Environment]::SetEnvironmentVariable('ALIAS_CONFIG_FILE',$taskPreviousConfig,'Process')
        [Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT',$taskPreviousEnvironment,'Process')
    }
    $taskApiReady=$false
    for($taskAttempt=0;$taskAttempt -lt 45;$taskAttempt++) {
        try {$taskApiReady=(Invoke-RestMethod "http://127.0.0.1:$taskApiPort/health" -TimeoutSec 2).status -eq 'ok'}catch{}
        if($taskApiReady){break}; if($taskApiProcess.HasExited){throw 'Backend arrete pendant les migrations de test.'};Start-Sleep -Seconds 1
    }
    if(-not $taskApiReady){throw 'Backend indisponible apres creation automatique de la base.'}
    if(-not(Invoke-RestMethod "http://127.0.0.1:$taskApiPort/api/auth/session").setupRequired){throw 'Initialisation administrateur de la nouvelle base incorrecte.'}
    Write-Output 'PASS : base automatique, compte non superutilisateur, conservation des donnees, migrations et API publiee.'
} finally {
    [Environment]::SetEnvironmentVariable('PGPASSWORD',$taskOldPassword,'Process')
    if($taskApiProcess -and -not $taskApiProcess.HasExited){Stop-Process -Id $taskApiProcess.Id -ErrorAction Stop; $taskApiProcess.WaitForExit()}
    if($taskProcess -and -not $taskProcess.HasExited) {
        & (Join-Path $PgRoot 'bin\pg_ctl.exe') stop '-D' (Join-Path $taskData 'postgres') '-m' 'fast' '-w' | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Impossible de fermer le PostgreSQL temporaire de test.'}
        $taskProcess.WaitForExit()
    }
}
