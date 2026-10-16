[CmdletBinding()]
param([switch]$SkipFrontendBuild)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskStage=Join-Path $taskRoot ('output\desktop-package-'+[Guid]::NewGuid().ToString('N'))
$taskSnapshot=Join-Path $taskRoot ('output\desktop-snapshot-'+[Guid]::NewGuid().ToString('N'))
$taskCompiler=Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
if(-not(Test-Path -LiteralPath $taskCompiler)){throw 'Inno Setup 6 est requis sur le poste de fabrication.'}
if(-not $SkipFrontendBuild){& (Join-Path $PSScriptRoot 'Prepare-Alias.ps1');if($LASTEXITCODE -ne 0){throw 'Compilation frontend impossible.'}}
New-Item -ItemType Directory -Path $taskStage,(Join-Path $taskStage 'services'),(Join-Path $taskStage 'installer') -Force | Out-Null
foreach($taskDirectory in @('frontend','node','postgresql')){
    $taskSource=if($taskDirectory -eq 'frontend'){Join-Path $taskRoot 'output\runtime\frontend'}else{Join-Path $taskRoot "output\server-package\$taskDirectory"}
    if(-not(Test-Path -LiteralPath $taskSource)){throw "Dependance absente : $taskDirectory. Executer Build-Server-Installer.ps1 pour preparer les runtimes."}
    & robocopy.exe $taskSource (Join-Path $taskStage $taskDirectory) /E /XD cache route-cache /NFL /NDL /NJH /NJS /NP | Out-Null
    if($LASTEXITCODE -ge 8){throw 'Copie du runtime impossible.'}
}
& dotnet publish (Join-Path $taskRoot 'backend\BankStatementConverter.API') -c Release -r win-x64 --self-contained true -o (Join-Path $taskStage 'backend')
if($LASTEXITCODE -ne 0){throw 'Publication backend impossible.'}
& dotnet publish (Join-Path $taskRoot 'tools\DesktopApp') -c Release -r win-x64 --self-contained true -o (Join-Path $taskStage 'desktop')
if($LASTEXITCODE -ne 0){throw 'Publication desktop impossible.'}
& dotnet run --project (Join-Path $taskRoot 'tools\ProjectSnapshot') -c Release -- $taskRoot $taskSnapshot (Join-Path $taskStage 'postgresql\bin\pg_dump.exe')
if($LASTEXITCODE -ne 0){throw 'Export de la base impossible.'}
Copy-Item -LiteralPath $taskSnapshot -Destination (Join-Path $taskStage 'project-data') -Recurse
foreach($taskName in @('AliasDesktopFrontend','AliasDesktopBackend','AliasDesktopPostgreSQL')){Copy-Item -LiteralPath (Join-Path $taskRoot 'output\server-downloads\WinSW-x64.exe') -Destination (Join-Path $taskStage "services\$taskName.exe")}
Copy-Item -LiteralPath (Join-Path $taskRoot 'scripts\Alias.Runtime.json.example'),(Join-Path $taskRoot 'installer\assets\Alias.ico') -Destination $taskStage
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\Managed-Postgres.ps1'),(Join-Path $taskRoot 'installer\Import-ProjectData.ps1'),(Join-Path $taskRoot 'installer\Configure-Desktop.ps1'),(Join-Path $taskRoot 'installer\Remove-Desktop.ps1'),(Join-Path $taskRoot 'installer\Run-DesktopInstallation.ps1') -Destination (Join-Path $taskStage 'installer')
Copy-Item -LiteralPath (Join-Path $taskRoot 'output\server-downloads\vc_redist.x64.exe') -Destination (Join-Path $taskStage 'installer')
$taskWebView=Join-Path $taskRoot 'output\server-downloads\MicrosoftEdgeWebview2Setup.exe'
if(-not(Test-Path -LiteralPath $taskWebView)){Invoke-WebRequest 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $taskWebView -UseBasicParsing}
$taskSignature=Get-AuthenticodeSignature -LiteralPath $taskWebView
if($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'Microsoft Corporation'){throw 'Signature WebView2 invalide.'}
Copy-Item -LiteralPath $taskWebView -Destination (Join-Path $taskStage 'installer')
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\GUIDE-DESKTOP.md') -Destination (Join-Path $taskStage 'GUIDE-INSTALLATION.md')
& $taskCompiler /Q "/DStageDir=$taskStage" (Join-Path $taskRoot 'installer\Alias-Desktop.iss')
if($LASTEXITCODE -ne 0){throw 'Compilation de l installateur impossible.'}
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\GUIDE-DESKTOP.md') -Destination (Join-Path $taskRoot 'Desktop-Install\Guide-installation.md') -Force
$taskManifest=Get-Content -LiteralPath (Join-Path $taskStage 'project-data\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
[pscustomobject]@{Version='2.0.0';CreatedUtc=$taskManifest.createdUtc;Counts=$taskManifest.counts;ApiKeysIncluded=$false;SessionsIncluded=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskRoot 'Desktop-Install\Donnees-incluses.json') -Encoding UTF8
$taskHash=Get-FileHash -LiteralPath (Join-Path $taskRoot 'Desktop-Install\Alias-Desktop-Setup-2.0.0.exe') -Algorithm SHA256
($taskHash.Hash+'  Alias-Desktop-Setup-2.0.0.exe') | Set-Content -LiteralPath (Join-Path $taskRoot 'Desktop-Install\SHA256.txt') -Encoding ASCII
[IO.File]::WriteAllText((Join-Path $taskRoot 'output\desktop-latest-stage.txt'),$taskStage,(New-Object Text.UTF8Encoding($false)))
Write-Output "Installateur : $taskRoot\Desktop-Install\Alias-Desktop-Setup-2.0.0.exe"
