[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskStage=Join-Path $taskRoot ('output\linux-stage-'+[Guid]::NewGuid().ToString('N'))
$taskOutput=Join-Path $taskRoot 'output\installers'
New-Item -ItemType Directory -Path $taskStage,$taskOutput,(Join-Path $taskStage 'installer') -Force | Out-Null
foreach($taskDirectory in @('backend','frontend')) {
    & robocopy.exe (Join-Path $taskRoot $taskDirectory) (Join-Path $taskStage $taskDirectory) /E /XD node_modules bin obj uploads .next .next-runtime .next-desktop .git .vs TestResults test-results playwright-report coverage /XF appsettings.Development.json Alias.Runtime.json .env .env.* *.user *.tsbuildinfo /NFL /NDL /NJH /NJS /NP | Out-Null
    if($LASTEXITCODE -ge 8){throw "Copie $taskDirectory echouee."}
}
Copy-Item -LiteralPath (Join-Path $taskRoot '.dockerignore') -Destination $taskStage
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\compose.linux.yml'),(Join-Path $taskRoot 'installer\install-linux.sh') -Destination (Join-Path $taskStage 'installer')
$taskShellScript=Join-Path $taskStage 'installer\install-linux.sh'
[IO.File]::WriteAllText($taskShellScript,[IO.File]::ReadAllText($taskShellScript).Replace("`r`n","`n"),(New-Object Text.UTF8Encoding($false)))
Copy-Item -LiteralPath (Join-Path $taskRoot 'installer\INSTALLATION.md') -Destination (Join-Path $taskStage 'LIRE-MOI.md')
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive=Join-Path $taskOutput 'Alias-Serveur-Linux.zip'
if(Test-Path -LiteralPath $taskArchive) {
    $taskArchive=Join-Path $taskOutput ('Alias-Serveur-Linux-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.zip')
}
[IO.Compression.ZipFile]::CreateFromDirectory($taskStage,$taskArchive,[IO.Compression.CompressionLevel]::Optimal,$false)
Write-Output "Livrable Linux : $taskArchive"
