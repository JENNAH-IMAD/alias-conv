[CmdletBinding()]
param([switch]$SkipFrontendBuild,[string]$PostgresSource='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskStage=Join-Path $taskRoot 'output\server-package'
$taskDownloads=Join-Path $taskRoot 'output\server-downloads'
$taskCompiler=Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
& node (Join-Path $PSScriptRoot 'Build-Alias-Icon.mjs')
if($LASTEXITCODE -ne 0){throw 'Creation de l icone echouee.'}
if(-not(Test-Path -LiteralPath $taskCompiler)){throw 'Installer Inno Setup 6 sur le poste de fabrication (pas sur les postes utilisateurs).'}
if(-not $SkipFrontendBuild){& (Join-Path $PSScriptRoot 'Prepare-Alias.ps1')}
New-Item -ItemType Directory -Path $taskStage,$taskDownloads,(Join-Path $taskStage 'services'),(Join-Path $taskStage 'node'),(Join-Path $taskStage 'installer') -Force | Out-Null
$taskFrontend=Join-Path $taskRoot 'output\runtime\frontend'
if(-not(Test-Path -LiteralPath (Join-Path $taskFrontend 'server.js'))){throw 'Frontend absent : executer Prepare-Alias.ps1.'}
$taskStageUi=Join-Path $taskStage 'frontend'
New-Item -ItemType Directory -Path $taskStageUi -Force | Out-Null
& robocopy.exe $taskFrontend $taskStageUi /E /XD cache route-cache /NFL /NDL /NJH /NJS /NP | Out-Null
if($LASTEXITCODE -ge 8){throw 'Copie du frontend echouee.'}
& dotnet publish (Join-Path $taskRoot 'backend\BankStatementConverter.API') -c Release -r win-x64 --self-contained true -o (Join-Path $taskStage 'backend')
if($LASTEXITCODE -ne 0){throw 'Publication autonome Windows echouee.'}
$taskRuntimePackage=Get-ChildItem (Join-Path $env:USERPROFILE '.nuget\packages\microsoft.netcore.app.runtime.win-x64') -Directory | Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if($taskRuntimePackage) {
    foreach($taskNotice in @('LICENSE.TXT','THIRD-PARTY-NOTICES.TXT')) {
        Copy-Item -LiteralPath (Join-Path $taskRuntimePackage.FullName $taskNotice) -Destination (Join-Path $taskStage "backend\DOTNET-$taskNotice") -Force
    }
}
$taskNodeArchive=Join-Path $taskDownloads 'node-v24.21.0-win-x64.zip'
if(-not(Test-Path -LiteralPath $taskNodeArchive)){Invoke-WebRequest 'https://nodejs.org/dist/v24.21.0/node-v24.21.0-win-x64.zip' -OutFile $taskNodeArchive -UseBasicParsing}
if((Get-FileHash -LiteralPath $taskNodeArchive -Algorithm SHA256).Hash -ne '158F7685B44DE51F6C0DF1D153526CBCD3E1BC739A8DFC607721CEF75DE9E541'){throw 'Empreinte Node invalide.'}
Expand-Archive -LiteralPath $taskNodeArchive -DestinationPath $taskDownloads -Force
Copy-Item -LiteralPath (Join-Path $taskDownloads 'node-v24.21.0-win-x64\node.exe'),(Join-Path $taskDownloads 'node-v24.21.0-win-x64\LICENSE') -Destination (Join-Path $taskStage 'node') -Force
$taskWrapper=Join-Path $taskDownloads 'WinSW-x64.exe'
if(-not(Test-Path -LiteralPath $taskWrapper)){Invoke-WebRequest 'https://github.com/winsw/winsw/releases/download/v2.12.0/WinSW-x64.exe' -OutFile $taskWrapper -UseBasicParsing}
if((Get-FileHash -LiteralPath $taskWrapper -Algorithm SHA256).Hash -ne '05B82D46AD331CC16BDC00DE5C6332C1EF818DF8CEEFCD49C726553209B3A0DA'){throw 'Empreinte WinSW invalide.'}
foreach($taskName in @('AliasFrontend','AliasBackend','AliasPostgreSQL')){Copy-Item -LiteralPath $taskWrapper -Destination (Join-Path $taskStage "services\$taskName.exe") -Force}
if(-not $PostgresSource) {
    $taskLocalPg=Join-Path $env:ProgramFiles 'PostgreSQL\18'
    if((Test-Path -LiteralPath (Join-Path $taskLocalPg 'bin\postgres.exe')) -and ((& (Join-Path $taskLocalPg 'bin\postgres.exe') --version) -match '18\.6$')){$PostgresSource=$taskLocalPg}
    else {
        $taskPgArchive=Join-Path $taskDownloads 'postgresql-18.6-1-windows-x64-binaries.zip'
        if(-not(Test-Path -LiteralPath $taskPgArchive)){Invoke-WebRequest 'https://get.enterprisedb.com/postgresql/postgresql-18.6-1-windows-x64-binaries.zip' -OutFile $taskPgArchive -UseBasicParsing}
        if((Get-FileHash -LiteralPath $taskPgArchive -Algorithm SHA256).Hash -ne 'FBE23DA234EE31547BF8A36D29DFD81E82B849DF2D2B78D2EECB43D360252F8C'){throw 'Empreinte du paquet PostgreSQL invalide.'}
        Expand-Archive -LiteralPath $taskPgArchive -DestinationPath (Join-Path $taskDownloads 'postgresql-18.6') -Force
        $PostgresSource=Join-Path $taskDownloads 'postgresql-18.6\pgsql'
    }
}
if((& (Join-Path $PostgresSource 'bin\postgres.exe') --version) -notmatch '18\.6$'){throw 'Le livrable attend PostgreSQL 18.6.'}
$taskPgStage=Join-Path $taskStage 'postgresql'
New-Item -ItemType Directory -Path $taskPgStage -Force | Out-Null
# Never copy a preexisting PostgreSQL data directory, passwords or installer logs.
foreach($taskPgDirectory in @('bin','lib','share')) {
    & robocopy.exe (Join-Path $PostgresSource $taskPgDirectory) (Join-Path $taskPgStage $taskPgDirectory) /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if($LASTEXITCODE -ge 8){throw 'Copie de PostgreSQL echouee.'}
}
foreach($taskLicense in @('server_license.txt','commandlinetools_3rd_party_licenses.txt')) {
    if(Test-Path -LiteralPath (Join-Path $PostgresSource $taskLicense)){Copy-Item -LiteralPath (Join-Path $PostgresSource $taskLicense) -Destination $taskPgStage -Force}
}
$taskVc=Join-Path $taskDownloads 'vc_redist.x64.exe'
if(-not(Test-Path -LiteralPath $taskVc)){Invoke-WebRequest 'https://aka.ms/vs/17/release/vc_redist.x64.exe' -OutFile $taskVc -UseBasicParsing}
$taskSignature=Get-AuthenticodeSignature -LiteralPath $taskVc
if($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'Microsoft Corporation'){throw 'Signature du runtime Visual C++ invalide.'}
Copy-Item -LiteralPath $taskVc -Destination (Join-Path $taskStage 'installer') -Force
Invoke-WebRequest 'https://raw.githubusercontent.com/winsw/winsw/v2.12.0/LICENSE.txt' -OutFile (Join-Path $taskStage 'services\WinSW-LICENSE.txt') -UseBasicParsing
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\Configure-Server.ps1'),(Join-Path $taskRoot 'installer\Remove-Services.ps1'),(Join-Path $taskRoot 'installer\Managed-Postgres.ps1'),(Join-Path $taskRoot 'installer\Run-Installation.ps1') -Destination (Join-Path $taskStage 'installer') -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Alias.Runtime.json.example') -Destination $taskStage -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\INSTALLATION.md') -Destination (Join-Path $taskStage 'LIRE-MOI.md') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\assets\Alias.ico') -Destination $taskStage -Force
$taskClients=Join-Path $taskStage 'Acces-postes'
New-Item -ItemType Directory -Path $taskClients -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\assets\Alias.ico'),(Join-Path $taskRoot 'installer\Creer-acces-client.ps1'),(Join-Path $taskRoot 'installer\Creer-acces.cmd') -Destination $taskClients -Force
& dotnet publish (Join-Path $taskRoot 'tools\ServerManager') -c Release -r win-x64 --self-contained true -o (Join-Path $taskStage 'management')
if($LASTEXITCODE -ne 0){throw 'Publication du gestionnaire desktop echouee.'}
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\Server-Control.ps1') -Destination (Join-Path $taskStage 'management') -Force
& $taskCompiler /Q "/DStageDir=$taskStage" (Join-Path $taskRoot 'installer\Alias-Server.iss')
if($LASTEXITCODE -ne 0){throw 'Compilation Inno Setup echouee.'}
Write-Output "Installateur : $taskRoot\output\installers\Alias-Serveur-Setup-1.2.0.exe"
