param([string]$ServerUrl='')
$ErrorActionPreference='Stop'
if(-not $ServerUrl -and (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Adresse-serveur.txt'))){$ServerUrl=(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Adresse-serveur.txt') -Raw).Trim()}
if(-not $ServerUrl){$ServerUrl=Read-Host 'Adresse Alias du serveur (exemple http://192.168.1.10:3006)'}
$taskUri=$null
if(-not [Uri]::TryCreate($ServerUrl,[UriKind]::Absolute,[ref]$taskUri) -or $taskUri.Scheme -notin @('http','https') -or $taskUri.UserInfo -or $ServerUrl.Contains('"')){throw 'Adresse du serveur invalide.'}
$taskIcon=Join-Path $PSScriptRoot 'Alias.ico'
if(-not(Test-Path -LiteralPath $taskIcon)){throw 'Conserver Alias.ico avec ce script.'}
$taskShell=New-Object -ComObject WScript.Shell
$taskShortcut=$taskShell.CreateShortcut((Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'Alias Entreprise.lnk'))
$taskShortcut.TargetPath=Join-Path $env:WINDIR 'explorer.exe'
$taskShortcut.Arguments='"'+$taskUri.AbsoluteUri+'"'
$taskShortcut.IconLocation="$taskIcon,0"
$taskShortcut.Description='Ouvrir Alias sur le serveur de l entreprise'
$taskShortcut.Save()
Write-Output 'Raccourci Alias Entreprise cree sur le bureau. Conserver ce dossier pour son icone.'
