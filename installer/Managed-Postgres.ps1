function New-AliasDatabasePassword {
    $taskBytes=New-Object byte[] 32
    $taskRandom=[Security.Cryptography.RandomNumberGenerator]::Create()
    try {$taskRandom.GetBytes($taskBytes)} finally {$taskRandom.Dispose()}
    return ([BitConverter]::ToString($taskBytes)).Replace('-','').ToLowerInvariant()
}
function Initialize-AliasPostgres([string]$DataRoot,[string]$PgRoot,[int[]]$ExcludedPorts=@(),[switch]$ProtectCredentials) {
    $taskMeta=Join-Path $DataRoot 'Postgres.Admin.json'
    $taskCluster=Join-Path $DataRoot 'postgres'
    $taskNewCredentials=$false
    foreach($taskExecutable in @('initdb.exe','postgres.exe','psql.exe','pg_ctl.exe')) {
        if(-not(Test-Path -LiteralPath (Join-Path $PgRoot "bin\$taskExecutable"))){throw 'Les fichiers PostgreSQL inclus dans le livrable sont incomplets.'}
    }
    if(Test-Path -LiteralPath $taskMeta) {
        $taskPlan=Get-Content -LiteralPath $taskMeta -Raw -Encoding UTF8 | ConvertFrom-Json
        if($taskPlan.Version -ne 18 -or $taskPlan.AdminPassword -notmatch '^[a-f0-9]{64}$' -or $taskPlan.AppPassword -notmatch '^[a-f0-9]{64}$' -or [int]$taskPlan.Port -lt 1024 -or [int]$taskPlan.Port -gt 65535 -or $ExcludedPorts -contains [int]$taskPlan.Port){throw 'Configuration de la base locale incompatible. Conserver les donnees et verifier les ports.'}
    } else {
        if(Test-Path -LiteralPath (Join-Path $taskCluster 'PG_VERSION')){throw 'Cette base PostgreSQL ne porte pas les identifiants Alias attendus. Elle ne sera pas modifiee.'}
        $taskPort=0
        for($taskCandidate=55432;$taskCandidate -le 55452;$taskCandidate++) {
            if($ExcludedPorts -contains $taskCandidate){continue}
            $taskListener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,$taskCandidate)
            try {$taskListener.Start();$taskPort=$taskCandidate;break} catch {} finally {$taskListener.Stop()}
        }
        if(-not $taskPort){throw 'Aucun port libre pour la base locale (55432 a 55452).'}
        $taskPlan=[pscustomobject]@{Version=18;Port=$taskPort;AdminPassword=New-AliasDatabasePassword;AppPassword=New-AliasDatabasePassword}
        [IO.File]::WriteAllText($taskMeta,'',(New-Object Text.UTF8Encoding($false)))
        $taskNewCredentials=$true
    }
    if($ProtectCredentials) {
        $taskInstallerSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        & icacls.exe $taskMeta /inheritance:r /grant:r '*S-1-5-32-544:F' '*S-1-5-18:F' "*${taskInstallerSid}:F" | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Impossible de proteger les identifiants de la base locale.'}
    }
    if($taskNewCredentials){$taskPlan | ConvertTo-Json | Set-Content -LiteralPath $taskMeta -Encoding UTF8}
    if(-not(Test-Path -LiteralPath (Join-Path $taskCluster 'PG_VERSION'))) {
        $taskPwFile=Join-Path $DataRoot ('.postgres-init-'+[Guid]::NewGuid().ToString('N'))
        [IO.File]::WriteAllText($taskPwFile,'',(New-Object Text.UTF8Encoding($false)))
        try {
            if($ProtectCredentials) {
                & icacls.exe $taskPwFile /inheritance:r /grant:r '*S-1-5-32-544:F' '*S-1-5-18:F' "*${taskInstallerSid}:F" | Out-Null
                if($LASTEXITCODE -ne 0){throw 'Impossible de proteger le fichier temporaire PostgreSQL.'}
            }
            [IO.File]::WriteAllText($taskPwFile,$taskPlan.AdminPassword,(New-Object Text.UTF8Encoding($false)))
            & (Join-Path $PgRoot 'bin\initdb.exe') '-D' $taskCluster '--username=alias_admin' "--pwfile=$taskPwFile" '--auth-host=scram-sha-256' '--auth-local=scram-sha-256' '--encoding=UTF8' '--locale-provider=builtin' '--builtin-locale=C.UTF-8' *> (Join-Path $DataRoot 'logs\postgres-init.log')
            if($LASTEXITCODE -ne 0){throw 'Initialisation PostgreSQL impossible. Consulter postgres-init.log.'}
        } finally { if(Test-Path -LiteralPath $taskPwFile){[IO.File]::Delete($taskPwFile)} }
    }
    $taskMajor=(Get-Content -LiteralPath (Join-Path $taskCluster 'PG_VERSION') -Raw).Trim()
    if($taskMajor -ne '18'){throw 'Version de la base locale incompatible. Aucune reinitialisation effectuee.'}
    # Reapply inherited service access: initdb may have restricted the new cluster ACL.
    if($ProtectCredentials) {
        & icacls.exe $taskCluster /grant '*S-1-5-20:(OI)(CI)M' /T /Q | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Impossible de donner acces au service PostgreSQL.'}
    }
    $taskPgConfig="listen_addresses = '127.0.0.1'`r`nport = $($taskPlan.Port)`r`npassword_encryption = 'scram-sha-256'`r`n"
    [IO.File]::WriteAllText((Join-Path $taskCluster 'postgresql.auto.conf'),$taskPgConfig,(New-Object Text.UTF8Encoding($false)))
    return $taskPlan
}
function Complete-AliasPostgres([string]$DataRoot,[string]$PgRoot,$Plan) {
    $taskPrevious=[Environment]::GetEnvironmentVariable('PGPASSWORD','Process')
    $taskPreviousTimeout=[Environment]::GetEnvironmentVariable('PGCONNECT_TIMEOUT','Process')
    try {
        $env:PGPASSWORD=$Plan.AdminPassword
        $env:PGCONNECT_TIMEOUT='2'
        $taskPsql=Join-Path $PgRoot 'bin\psql.exe'
        $taskArguments=@('-X','-h','127.0.0.1','-p',[string]$Plan.Port,'-U','alias_admin','-d','postgres','-v','ON_ERROR_STOP=1')
        $taskReady=$false
        for($taskAttempt=0;$taskAttempt -lt 45;$taskAttempt++) {
            try {
                & $taskPsql @taskArguments '-q' '-c' 'SELECT 1' *> (Join-Path $DataRoot 'logs\postgres-ready.log')
                $taskReady=$LASTEXITCODE -eq 0
            } catch {$taskReady=$false}
            if($taskReady){break};Start-Sleep -Seconds 1
        }
        if(-not $taskReady){throw 'La base PostgreSQL locale ne repond pas.'}
        # Send SQL on stdin so generated passwords never appear in process arguments.
        $taskSql=@"
SELECT 'CREATE ROLE alias_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD ''$($Plan.AppPassword)''' WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'alias_app')
\gexec
SELECT 'CREATE DATABASE alias OWNER alias_app' WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'alias')
\gexec
"@
        $taskSql | & $taskPsql @taskArguments '-q' *> (Join-Path $DataRoot 'logs\postgres-provision.log')
        if($LASTEXITCODE -ne 0){throw 'Creation de la base applicative impossible. Consulter postgres-provision.log.'}
        return "Host=127.0.0.1;Port=$($Plan.Port);Database=alias;Username=alias_app;Password=$($Plan.AppPassword)"
    } finally {
        [Environment]::SetEnvironmentVariable('PGPASSWORD',$taskPrevious,'Process')
        [Environment]::SetEnvironmentVariable('PGCONNECT_TIMEOUT',$taskPreviousTimeout,'Process')
    }
}
