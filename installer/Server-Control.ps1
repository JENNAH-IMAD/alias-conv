[CmdletBinding()]
param(
 [Parameter(Mandatory)][ValidateSet('Start','Stop','Restart','Ports','ValidatePorts')][string]$Action,
 [Parameter(Mandatory)][string]$AppRoot,
 [int]$FrontendPort=0,[int]$BackendPort=0,[string]$PublicUrl=''
)
$ErrorActionPreference='Stop'
$AppRoot=[IO.Path]::GetFullPath($AppRoot)
[xml]$taskBackendXml=Get-Content -LiteralPath (Join-Path $AppRoot 'services\AliasBackend.xml') -Raw
$taskConfigPath=($taskBackendXml.service.env | Where-Object name -eq 'ALIAS_CONFIG_FILE').value
if(-not $taskConfigPath){throw 'Configuration serveur introuvable.'}
$taskData=Split-Path $taskConfigPath
$taskServerPath=Join-Path $taskData 'Server.json'
$taskSettings=Get-Content -LiteralPath $taskServerPath -Raw -Encoding UTF8 | ConvertFrom-Json
function Test-Ports {
 if($FrontendPort -lt 1024 -or $FrontendPort -gt 65535 -or $BackendPort -lt 1024 -or $BackendPort -gt 65535 -or $FrontendPort -eq $BackendPort){throw 'Choisissez deux ports distincts entre 1024 et 65535.'}
 $taskUri=$null
 if(-not [Uri]::TryCreate($PublicUrl,[UriKind]::Absolute,[ref]$taskUri) -or $taskUri.Scheme -ne 'http' -or $taskUri.Port -ne $FrontendPort -or $taskUri.UserInfo -or $taskUri.Query -or $taskUri.Fragment -or $taskUri.AbsolutePath -ne '/'){throw 'Adresse attendue : http://NOM_OU_IP:PORT_FRONTEND.'}
 foreach($taskPair in @(@($FrontendPort,[int]$taskSettings.FrontendPort,'AliasFrontend'),@($BackendPort,[int]$taskSettings.BackendPort,'AliasBackend'))) {
  $taskListeners=Get-NetTCPConnection -State Listen -LocalPort $taskPair[0] -ErrorAction SilentlyContinue
  if($taskListeners) {
   $taskService=Get-CimInstance Win32_Service -Filter "Name='$($taskPair[2])'"
   foreach($taskListener in $taskListeners) {
    $taskOwner=Get-CimInstance Win32_Process -Filter "ProcessId=$($taskListener.OwningProcess)"
    if($taskPair[0] -ne $taskPair[1] -or -not $taskService -or $taskService.ProcessId -eq 0 -or ($taskListener.OwningProcess -ne $taskService.ProcessId -and $taskOwner.ParentProcessId -ne $taskService.ProcessId)){throw "Port $($taskPair[0]) deja occupe."}
   }
  }
 }
 $taskDatabase=Join-Path $taskData 'Postgres.Admin.json'
 if($taskSettings.DatabaseMode -eq '0' -and (Test-Path -LiteralPath $taskDatabase)) {
  $taskDb=Get-Content -LiteralPath $taskDatabase -Raw -Encoding UTF8 | ConvertFrom-Json
  if($FrontendPort -eq $taskDb.Port -or $BackendPort -eq $taskDb.Port){throw 'Un port choisi est reserve a PostgreSQL.'}
 }
}
if($Action -in @('Ports','ValidatePorts')){Test-Ports}
if($Action -eq 'ValidatePorts'){Write-Output 'Ports et adresse valides.';exit}
$taskIdentity=[Security.Principal.WindowsIdentity]::GetCurrent()
if(-not([Security.Principal.WindowsPrincipal]::new($taskIdentity)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Cette action necessite les droits administrateur Windows.'}
$taskControlLock=[Threading.Mutex]::new($false,'Global\AliasServerMaintenance')
if(-not $taskControlLock.WaitOne(0)){$taskControlLock.Dispose();throw 'Une autre action de maintenance Alias est deja en cours.'}
try {
function Stop-App {foreach($taskName in @('AliasFrontend','AliasBackend')){if(Get-Service $taskName -ErrorAction SilentlyContinue){Stop-Service $taskName -Force; (Get-Service $taskName).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(45))}}}
function Start-App {
 if(Get-Service AliasPostgreSQL -ErrorAction SilentlyContinue){Start-Service AliasPostgreSQL}
 Start-Service AliasBackend
 $taskHealthy=$false
 for($taskAttempt=0;$taskAttempt -lt 45;$taskAttempt++){try{$taskHealthy=(Invoke-RestMethod "http://127.0.0.1:$($taskSettings.BackendPort)/health" -TimeoutSec 2).status -eq 'ok'}catch{};if($taskHealthy){break};Start-Sleep -Seconds 1}
 if(-not $taskHealthy){throw 'Backend indisponible. Consulter les journaux.'}
 Start-Service AliasFrontend
 $taskHealthy=$false
 for($taskAttempt=0;$taskAttempt -lt 45;$taskAttempt++){try{$taskSession=Invoke-RestMethod "http://127.0.0.1:$($taskSettings.FrontendPort)/api/auth/session" -TimeoutSec 2;$taskHealthy=$taskSession.PSObject.Properties.Name -contains 'setupRequired'}catch{};if($taskHealthy){break};Start-Sleep -Seconds 1}
 if(-not $taskHealthy){throw 'Interface indisponible. Consulter les journaux.'}
}
switch($Action) {
 'Start' {Start-App}
 'Stop' {Stop-App;if(Get-Service AliasPostgreSQL -ErrorAction SilentlyContinue){Stop-Service AliasPostgreSQL; (Get-Service AliasPostgreSQL).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(45))}}
 'Restart' {Stop-App;Start-App}
 'Ports' {
  $taskFiles=@($taskServerPath,(Join-Path $AppRoot 'services\AliasFrontend.xml'),(Join-Path $AppRoot 'services\AliasBackend.xml'),(Join-Path $AppRoot 'frontend\server.js'),(Join-Path $AppRoot 'frontend\.next-runtime\routes-manifest.json'),(Join-Path $AppRoot 'frontend\.next-runtime\required-server-files.json'),(Join-Path $AppRoot 'Acces-Alias.url'),(Join-Path $AppRoot 'Acces-postes\Adresse-serveur.txt'))
  $taskBackup=Join-Path $taskData ('logs\ports-backup-'+[Guid]::NewGuid().ToString('N'))
  New-Item -ItemType Directory -Path $taskBackup -Force | Out-Null
  $taskSnapshot=@{}; $taskIndex=0
  foreach($taskFile in $taskFiles){if(Test-Path -LiteralPath $taskFile){$taskSaved=Join-Path $taskBackup ([string]$taskIndex);Copy-Item -LiteralPath $taskFile -Destination $taskSaved; $taskSnapshot[$taskFile]=$taskSaved};$taskIndex++}
  $taskWasRunning=(Get-Service AliasFrontend).Status -eq 'Running' -or (Get-Service AliasBackend).Status -eq 'Running'
  try {
   Stop-App
   [xml]$taskFrontXml=Get-Content -LiteralPath (Join-Path $AppRoot 'services\AliasFrontend.xml') -Raw
   ($taskFrontXml.service.env | Where-Object name -eq 'PORT').SetAttribute('value',[string]$FrontendPort)
   $taskFrontXml.Save((Join-Path $AppRoot 'services\AliasFrontend.xml'))
   $taskBackendXml.service.arguments="--urls http://127.0.0.1:$BackendPort"
   $taskBackendXml.Save((Join-Path $AppRoot 'services\AliasBackend.xml'))
   foreach($taskFile in $taskFiles | Where-Object {$_ -match 'server\.js$|routes-manifest\.json$|required-server-files\.json$'}) {
    $taskText=[regex]::Replace([IO.File]::ReadAllText($taskFile),'http://127\.0\.0\.1:\d+/api',"http://127.0.0.1:$BackendPort/api")
    [IO.File]::WriteAllText($taskFile,$taskText,(New-Object Text.UTF8Encoding($false)))
   }
   $taskSettings.FrontendPort=$FrontendPort;$taskSettings.BackendPort=$BackendPort;$taskSettings.PublicUrl=$PublicUrl.TrimEnd('/')
   $taskSettings | ConvertTo-Json | Set-Content -LiteralPath $taskServerPath -Encoding UTF8
   [IO.File]::WriteAllText((Join-Path $AppRoot 'Acces-Alias.url'),"[InternetShortcut]`r`nURL=$($taskSettings.PublicUrl)`r`n",(New-Object Text.UTF8Encoding($false)))
   [IO.File]::WriteAllText((Join-Path $AppRoot 'Acces-postes\Adresse-serveur.txt'),$taskSettings.PublicUrl,(New-Object Text.UTF8Encoding($false)))
   Get-NetFirewallRule -Name AliasFrontend | Get-NetFirewallPortFilter | Set-NetFirewallPortFilter -LocalPort $FrontendPort
   Start-App
  } catch {
   $taskFailure=$_.Exception.Message
   Stop-App
   foreach($taskFile in $taskSnapshot.Keys){Copy-Item -LiteralPath $taskSnapshot[$taskFile] -Destination $taskFile -Force}
   $taskSettings=Get-Content -LiteralPath $taskServerPath -Raw -Encoding UTF8 | ConvertFrom-Json
   Get-NetFirewallRule -Name AliasFrontend | Get-NetFirewallPortFilter | Set-NetFirewallPortFilter -LocalPort $taskSettings.FrontendPort
   if($taskWasRunning){Start-App}
   throw "Modification annulee, configuration precedente restauree : $taskFailure"
  }
 }
}
Write-Output 'Action terminee.'
} finally {$taskControlLock.ReleaseMutex();$taskControlLock.Dispose()}
