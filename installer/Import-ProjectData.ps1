function Import-AliasProjectData([string]$AppRoot,[string]$DataRoot,$Plan) {
    $ErrorActionPreference='Stop'
    $taskBundle=Join-Path $AppRoot 'project-data'
    $taskManifest=Get-Content -LiteralPath (Join-Path $taskBundle 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $taskDump=Join-Path $taskBundle 'database.dump'
    if($taskManifest.version -ne 1 -or (Get-FileHash -LiteralPath $taskDump -Algorithm SHA256).Hash -ne $taskManifest.databaseSha256){throw 'Copie de base invalide.'}
    $taskPreviousPassword=$env:PGPASSWORD
    try {
        $env:PGPASSWORD=$Plan.AppPassword
        $taskPsql=Join-Path $AppRoot 'postgresql\bin\psql.exe'
        $taskTables=& $taskPsql '-X' '-h' '127.0.0.1' '-p' ([string]$Plan.Port) '-U' 'alias_app' '-d' 'alias' '-tAc' "SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE'"
        if($LASTEXITCODE -ne 0 -or [int]($taskTables.Trim()) -ne 0){throw 'La restauration exige une base vide. Aucune base existante ne sera remplacee.'}
        $taskUploads=[IO.Path]::GetFullPath((Join-Path $DataRoot 'uploads'))
        foreach($taskFile in $taskManifest.files) {
            $taskSource=[IO.Path]::GetFullPath((Join-Path (Join-Path $taskBundle 'uploads') $taskFile.path))
            $taskDestination=[IO.Path]::GetFullPath((Join-Path $taskUploads $taskFile.path))
            if(-not $taskSource.StartsWith([IO.Path]::GetFullPath((Join-Path $taskBundle 'uploads'))+'\',[StringComparison]::OrdinalIgnoreCase) -or -not $taskDestination.StartsWith($taskUploads+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Chemin de document invalide.'}
            if((Get-FileHash -LiteralPath $taskSource -Algorithm SHA256).Hash -ne $taskFile.sha256){throw 'Document de la copie invalide.'}
            if(Test-Path -LiteralPath $taskDestination){throw 'Un document existe deja dans le stockage cible.'}
        }
        & (Join-Path $AppRoot 'postgresql\bin\pg_restore.exe') '-h' '127.0.0.1' '-p' ([string]$Plan.Port) '-U' 'alias_app' '-d' 'alias' '--no-owner' '--no-acl' '--exit-on-error' '--single-transaction' $taskDump *> (Join-Path $DataRoot 'logs\project-restore.log')
        if($LASTEXITCODE -ne 0){throw 'Restauration impossible. Consulter project-restore.log.'}
        foreach($taskFile in $taskManifest.files) {
            $taskDestination=[IO.Path]::GetFullPath((Join-Path $taskUploads $taskFile.path))
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskDestination)) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path (Join-Path $taskBundle 'uploads') $taskFile.path) -Destination $taskDestination
        }
        [IO.File]::WriteAllText((Join-Path $DataRoot 'ProjectDataImported.json'),(@{version=1;importedUtc=[DateTime]::UtcNow.ToString('o');counts=$taskManifest.counts}|ConvertTo-Json -Depth 5),(New-Object Text.UTF8Encoding($false)))
    } finally {[Environment]::SetEnvironmentVariable('PGPASSWORD',$taskPreviousPassword,'Process')}
}
