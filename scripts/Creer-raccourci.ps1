[CmdletBinding()]
param(
    [ValidateSet('Desktop','Local','Server','Client')][string]$Mode='Desktop',
    [string]$ServerUrl='',
    [string]$Name='Alias Convert PDF'
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if($Name.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0 -or [string]::IsNullOrWhiteSpace($Name)) {
    throw 'Le nom du raccourci est invalide.'
}
$taskDesktop=[Environment]::GetFolderPath('DesktopDirectory')
$taskPath=Join-Path $taskDesktop "$Name.lnk"
if(Test-Path -LiteralPath $taskPath){throw "Le raccourci existe deja : $taskPath. Choisir un autre -Name."}
$taskShell=New-Object -ComObject WScript.Shell
$taskShortcut=$taskShell.CreateShortcut($taskPath)
$taskEdge=Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'
if($Mode -eq 'Client') {
    $taskUri=$null
    if(-not [Uri]::TryCreate($ServerUrl,[UriKind]::Absolute,[ref]$taskUri) -or $taskUri.Scheme -notin @('http','https') -or $taskUri.UserInfo -or $ServerUrl.Contains('"')) {
        throw 'Renseigner -ServerUrl avec une adresse http ou https du serveur.'
    }
    if(Test-Path -LiteralPath $taskEdge) {
        $taskShortcut.TargetPath=$taskEdge
        $taskShortcut.Arguments='--app="'+$taskUri.AbsoluteUri+'"'
    } else {
        $taskShortcut.TargetPath=Join-Path $env:WINDIR 'explorer.exe'
        $taskShortcut.Arguments='"'+$taskUri.AbsoluteUri+'"'
    }
    $taskShortcut.Description='Ouvrir Alias sur le serveur de votre entreprise'
} else {
    $taskLauncher=Join-Path $PSScriptRoot 'Start-Alias.ps1'
    if(-not(Test-Path -LiteralPath $taskLauncher)){throw "Lanceur introuvable : $taskLauncher"}
    $taskEscaped=$taskLauncher.Replace("'","''")
    $taskCommand="try { & '$taskEscaped' -Mode $Mode -Open } catch { Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.MessageBox]::Show(`$_.Exception.Message, 'Alias - lancement impossible') | Out-Null; exit 1 }"
    $taskEncoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($taskCommand))
    $taskShortcut.TargetPath=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $taskShortcut.Arguments="-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand $taskEncoded"
    $taskShortcut.WorkingDirectory=$taskRoot
    $taskShortcut.Description="Demarrer Alias et ouvrir l'application ($Mode)"
}
if(Test-Path -LiteralPath $taskEdge){$taskShortcut.IconLocation="$taskEdge,0"}
$taskShortcut.Save()
Write-Output "Raccourci cree : $taskPath"
