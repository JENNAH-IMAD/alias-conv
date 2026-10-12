param([switch]$Install, [ValidateRange(1,8)][int]$Workers = 2)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskRuntime = Join-Path $taskRoot '.local/paddle-runtime'
$taskPython = Join-Path $taskRuntime 'Scripts/python.exe'
# Copied virtual environments can reference a Python installation on another PC.
# Check executability and version before selecting one.
if (!$Install) {
    $taskRuntimeFound = $false
    foreach ($taskCandidate in @($taskPython,
        (Join-Path $taskRoot '.local/hybrid-runtime/Scripts/python.exe'),
        (Join-Path $taskRoot '.local/python312/python.exe'))) {
        if (!(Test-Path -LiteralPath $taskCandidate)) { continue }
        try { $taskVersion = & $taskCandidate -c 'import sys; print(sys.version_info.major, sys.version_info.minor, sep=chr(46))' 2>$null }
        catch { continue }
        if ($LASTEXITCODE -eq 0 -and $taskVersion -eq '3.12') { $taskPython = $taskCandidate; $taskRuntimeFound = $true; break }
    }
    if (!$taskRuntimeFound -and (Test-Path -LiteralPath $taskPython)) {
        throw 'Le runtime OCR existant ne fonctionne pas sur ce poste. Installez Python 3.12 puis relancez start.ps1 -Install.'
    }
}
if ($Install -or !(Test-Path -LiteralPath $taskPython)) {
    if (!(Test-Path -LiteralPath $taskPython)) { py -3.12 -m venv $taskRuntime; if ($LASTEXITCODE) { py -V:Astral/CPython3.12.13 -m venv $taskRuntime }; if ($LASTEXITCODE) { throw 'Python 3.12 est requis.' } }
    & $taskPython -m pip install -r (Join-Path $PSScriptRoot 'requirements.txt')
    if ($LASTEXITCODE) { throw 'Installation OCR échouée.' }
}
$env:PADDLE_PDX_CACHE_HOME = Join-Path $taskRoot '.local/paddle-cache'
& $taskPython -m uvicorn app:app --app-dir $PSScriptRoot --host 127.0.0.1 --port 8091 --workers $Workers --no-access-log
exit $LASTEXITCODE
