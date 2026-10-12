[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AppRoot,
    [Parameter(Mandatory)][string]$SettingsFile,
    [switch]$ConfigureOnly
)
$ErrorActionPreference='Stop'
$AppRoot=[IO.Path]::GetFullPath($AppRoot)
$taskInput=Get-Content -LiteralPath $SettingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$taskManaged=$taskInput.DatabaseMode -eq '0'
if($taskManaged -and $ConfigureOnly){throw 'Le mode automatique initialise une vraie base. ConfigureOnly est reserve a la configuration avancee.'}
$taskFront=[int]$taskInput.FrontendPort
$taskBack=[int]$taskInput.BackendPort
if($taskFront -lt 1024 -or $taskFront -gt 65535 -or $taskBack -lt 1024 -or $taskBack -gt 65535 -or $taskFront -eq $taskBack){throw 'Les ports doivent etre distincts et compris entre 1024 et 65535.'}
$taskUri=$null
if(-not [Uri]::TryCreate([string]$taskInput.PublicUrl,[UriKind]::Absolute,[ref]$taskUri) -or $taskUri.Scheme -ne 'http' -or $taskUri.UserInfo -or $taskUri.Query -or $taskUri.Fragment -or $taskUri.AbsolutePath -ne '/' -or $taskUri.Port -ne $taskFront){throw 'Adresse publique attendue : http://NOM_OU_IP:PORT_FRONTEND sans chemin.'}
$taskData=[IO.Path]::GetFullPath([string]$taskInput.DataDirectory)
if(-not [IO.Path]::IsPathRooted([string]$taskInput.DataDirectory)){throw 'Le stockage doit etre un chemin absolu.'}
if($taskData.TrimEnd('\') -eq [IO.Path]::GetPathRoot($taskData).TrimEnd('\') -or $taskData.StartsWith($AppRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or $taskData.TrimEnd('\') -eq $AppRoot.TrimEnd('\') -or $taskData.StartsWith($env:WINDIR.TrimEnd('\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Choisir un dossier dedie aux donnees, hors Windows et hors programme.'}
if(Test-Path -LiteralPath $taskData) {
    $taskOther=Get-ChildItem -LiteralPath $taskData -Force | Where-Object { $_.Name -notin @('uploads','logs','Alias.Runtime.json','Server.json','postgres','Postgres.Admin.json') }
    if($taskOther){throw 'Ce dossier contient des fichiers etrangers a Alias. Choisir un dossier dedie.'}
}
$taskConfig=Join-Path $taskData 'Alias.Runtime.json'
$taskServerConfig=Join-Path $taskData 'Server.json'
if(-not $taskManaged -and [string]::IsNullOrWhiteSpace($taskInput.ConnectionString) -and -not(Test-Path -LiteralPath $taskConfig)){throw 'Renseigner la connexion PostgreSQL pour la premiere installation.'}
if(-not $ConfigureOnly) {
    foreach($taskExistingName in @('AliasBackend','AliasFrontend','AliasPostgreSQL')) {
        if(Get-Service $taskExistingName -ErrorAction SilentlyContinue){throw 'Des services Alias existent deja. Les desinstaller avant de reinstaller.'}
    }
}
foreach($taskPort in @($taskFront,$taskBack)) {
    if(-not $ConfigureOnly -and (Get-NetTCPConnection -State Listen -LocalPort $taskPort -ErrorAction SilentlyContinue)){throw "Le port $taskPort est deja utilise. Choisir un port libre."}
}
New-Item -ItemType Directory -Path $taskData,(Join-Path $taskData 'uploads'),(Join-Path $taskData 'logs'),(Join-Path $AppRoot 'services') -Force | Out-Null
$taskRouteCache=[IO.Path]::GetFullPath((Join-Path $AppRoot 'frontend\.next-runtime\server\route-cache'))
if((Test-Path -LiteralPath $taskRouteCache) -and (Get-ChildItem -LiteralPath $taskRouteCache -Force)) {
    $taskSavedCache=[IO.Path]::GetFullPath((Join-Path $taskData ('logs\previous-route-cache-'+[Guid]::NewGuid().ToString('N'))))
    if(-not($taskRouteCache.StartsWith($AppRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -and $taskSavedCache.StartsWith($taskData.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase))){throw 'Chemin de cache invalide.'}
    Move-Item -LiteralPath $taskRouteCache -Destination $taskSavedCache
}
New-Item -ItemType Directory -Path (Join-Path $AppRoot 'frontend\.next-runtime\cache'),(Join-Path $AppRoot 'frontend\.next-runtime\server\route-cache') -Force | Out-Null
if(-not $ConfigureOnly) {
    # Only administrators, SYSTEM and the unprivileged service account can read secrets.
    $taskInstallerSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    & icacls.exe $taskData /inheritance:r /grant:r '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-18:(OI)(CI)F' '*S-1-5-20:(OI)(CI)M' "*${taskInstallerSid}:(OI)(CI)F" | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Impossible de proteger le dossier de configuration.'}
    foreach($taskCache in @('frontend\.next-runtime\cache','frontend\.next-runtime\server\route-cache')) {
        & icacls.exe (Join-Path $AppRoot $taskCache) /grant '*S-1-5-20:(OI)(CI)M' | Out-Null
        if($LASTEXITCODE -ne 0){throw 'Impossible de configurer le cache frontend.'}
    }
}
$taskPgPlan=$null
if($taskManaged) {
    . (Join-Path $PSScriptRoot 'Managed-Postgres.ps1')
    $taskPgPlan=Initialize-AliasPostgres $taskData (Join-Path $AppRoot 'postgresql') @($taskFront,$taskBack) -ProtectCredentials
    $taskInput.ConnectionString="Host=127.0.0.1;Port=$($taskPgPlan.Port);Database=alias;Username=alias_app;Password=$($taskPgPlan.AppPassword)"
}
if(Test-Path -LiteralPath $taskConfig){$taskRuntime=Get-Content -LiteralPath $taskConfig -Raw -Encoding UTF8 | ConvertFrom-Json}
else {$taskRuntime=Get-Content -LiteralPath (Join-Path $AppRoot 'Alias.Runtime.json.example') -Raw -Encoding UTF8 | ConvertFrom-Json}
if(-not [string]::IsNullOrWhiteSpace($taskInput.ConnectionString)){$taskRuntime.ConnectionStrings.Default=[string]$taskInput.ConnectionString}
$taskRuntime.Storage.Root=Join-Path $taskData 'uploads'
$taskRuntime.AllowedHosts='*'
$taskRuntime.Database.AutoMigrate=$true
$taskRuntime.Seed.DemoData=$false
$taskRuntime | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $taskConfig -Encoding UTF8
$taskInput.ConnectionString=$null
$taskInput | ConvertTo-Json | Set-Content -LiteralPath $taskServerConfig -Encoding UTF8
# Next standalone stores rewrites at build time. Change only the packaged loopback API destinations.
$taskFiles=@('frontend\server.js','frontend\.next-runtime\routes-manifest.json','frontend\.next-runtime\required-server-files.json')
foreach($taskRelative in $taskFiles) {
    $taskFile=Join-Path $AppRoot $taskRelative
    if(-not(Test-Path -LiteralPath $taskFile)){throw "Livrable incomplet : $taskRelative"}
    $taskText=[IO.File]::ReadAllText($taskFile)
    $taskText=[regex]::Replace($taskText,'http://127\.0\.0\.1:\d+/api',"http://127.0.0.1:$taskBack/api")
    [IO.File]::WriteAllText($taskFile,$taskText,(New-Object Text.UTF8Encoding($false)))
}
function Escape-Xml([string]$Value){return [Security.SecurityElement]::Escape($Value)}
function Write-Service([string]$Name,[string]$Executable,[string]$Arguments,[string]$Directory,[string]$Environment,[string]$Dependency='',[string]$Stop='') {
    $taskArgumentElement=if($Stop){'startarguments'}else{'arguments'}
    $taskXml=@"
<service>
 <id>$Name</id><name>$Name</name><description>Alias Convert PDF - serveur entreprise</description>
 <executable>$(Escape-Xml $Executable)</executable><$taskArgumentElement>$(Escape-Xml $Arguments)</$taskArgumentElement>
 <workingdirectory>$(Escape-Xml $Directory)</workingdirectory>
 <startmode>Automatic</startmode><delayedAutoStart>true</delayedAutoStart>
 <serviceaccount><domain>NT AUTHORITY</domain><user>NetworkService</user></serviceaccount>
 <logpath>$(Escape-Xml (Join-Path $taskData 'logs'))</logpath><log mode="roll-by-size"><sizeThreshold>10240</sizeThreshold><keepFiles>5</keepFiles></log>
 <onfailure action="restart" delay="10 sec"/><stoptimeout>30 sec</stoptimeout>
 $Environment $Dependency $Stop
</service>
"@
    [IO.File]::WriteAllText((Join-Path $AppRoot "services\$Name.xml"),$taskXml,(New-Object Text.UTF8Encoding($false)))
}
$taskEnv='<env name="ASPNETCORE_ENVIRONMENT" value="Production"/><env name="ALIAS_CONFIG_FILE" value="'+(Escape-Xml $taskConfig)+'"/>'
$taskDependency=if($taskManaged){'<depend>AliasPostgreSQL</depend>'}else{''}
Write-Service 'AliasBackend' (Join-Path $AppRoot 'backend\BankStatementConverter.API.exe') "--urls http://127.0.0.1:$taskBack" (Join-Path $AppRoot 'backend') $taskEnv $taskDependency
if($taskManaged) {
    $taskPgRoot=Join-Path $AppRoot 'postgresql'
    $taskCluster=Join-Path $taskData 'postgres'
    $taskStop='<stopexecutable>'+(Escape-Xml (Join-Path $taskPgRoot 'bin\pg_ctl.exe'))+'</stopexecutable><stoparguments>'+(Escape-Xml ('stop -D "'+$taskCluster+'" -m fast -w'))+'</stoparguments>'
    Write-Service 'AliasPostgreSQL' (Join-Path $taskPgRoot 'bin\postgres.exe') ('-D "'+$taskCluster+'"') $taskPgRoot '' '' $taskStop
}
$taskEnv="<env name=`"NODE_ENV`" value=`"production`"/><env name=`"HOSTNAME`" value=`"0.0.0.0`"/><env name=`"PORT`" value=`"$taskFront`"/>"
Write-Service 'AliasFrontend' (Join-Path $AppRoot 'node\node.exe') ('"'+(Join-Path $AppRoot 'frontend\server.js')+'"') (Join-Path $AppRoot 'frontend') $taskEnv '<depend>AliasBackend</depend>'
$taskUrl=$taskUri.GetLeftPart([UriPartial]::Authority)
New-Item -ItemType Directory -Path (Join-Path $AppRoot 'Acces-postes') -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $AppRoot 'Acces-postes\Adresse-serveur.txt'),$taskUrl,(New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText((Join-Path $AppRoot 'Acces-Alias.url'),"[InternetShortcut]`r`nURL=$taskUrl`r`n",(New-Object Text.UTF8Encoding($false)))
if($ConfigureOnly){Write-Output 'Configuration generee (aucun service installe).'; exit}
$taskInstalled=@()
try {
    $taskNames=if($taskManaged){@('AliasPostgreSQL','AliasBackend','AliasFrontend')}else{@('AliasBackend','AliasFrontend')}
    foreach($taskName in $taskNames) {
        & (Join-Path $AppRoot "services\$taskName.exe") install
        if($LASTEXITCODE -ne 0){throw "Installation du service $taskName impossible."}
        $taskInstalled+=,$taskName
        $taskService=Get-CimInstance Win32_Service -Filter "Name='$taskName'"
        $taskServiceSid=([Security.Principal.NTAccount]::new($taskService.StartName)).Translate([Security.Principal.SecurityIdentifier]).Value
        if($taskServiceSid -ne 'S-1-5-20'){throw "Le service $taskName n'utilise pas le compte NetworkService attendu. Demarrage annule."}
    }
    if($taskManaged) {
        Start-Service AliasPostgreSQL
        $taskConnection=Complete-AliasPostgres $taskData (Join-Path $AppRoot 'postgresql') $taskPgPlan
    }
    Start-Service AliasBackend
    $taskReady=$false
    for($taskAttempt=0;$taskAttempt -lt 60;$taskAttempt++) {
        try {$taskReady=(Invoke-RestMethod "http://127.0.0.1:$taskBack/health" -TimeoutSec 2).status -eq 'ok'}catch{}
        if($taskReady){break};Start-Sleep -Seconds 1
    }
    if(-not $taskReady){throw 'Backend indisponible : verifier PostgreSQL et les journaux serveur.'}
    Start-Service AliasFrontend
    $taskReady=$false
    for($taskAttempt=0;$taskAttempt -lt 45;$taskAttempt++) {
        try {$taskSession=Invoke-RestMethod "http://127.0.0.1:$taskFront/api/auth/session" -TimeoutSec 2;$taskReady=$taskSession.PSObject.Properties.Name -contains 'setupRequired'}catch{}
        if($taskReady){break};Start-Sleep -Seconds 1
    }
    if(-not $taskReady){throw 'Interface indisponible : consulter les journaux serveur.'}
    Get-NetFirewallRule -Name 'AliasFrontend' -ErrorAction SilentlyContinue | Remove-NetFirewallRule
    New-NetFirewallRule -Name 'AliasFrontend' -DisplayName 'Alias - acces des postes' -Direction Inbound -Protocol TCP -LocalPort $taskFront -Action Allow -Profile Domain,Private -RemoteAddress LocalSubnet | Out-Null
    Write-Output 'Services installes et acces reseau configure.'
} catch {
    foreach($taskName in @('AliasFrontend','AliasBackend','AliasPostgreSQL')) {
        if($taskInstalled -contains $taskName) {
            Stop-Service $taskName -Force -ErrorAction SilentlyContinue
            & (Join-Path $AppRoot "services\$taskName.exe") uninstall | Out-Null
        }
    }
    throw
}
