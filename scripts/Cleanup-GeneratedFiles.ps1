[CmdletBinding()]
param([switch]$Execute)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$taskManifestPath = Join-Path $taskRoot '.local\cleanup-plan.json'
if (-not (Test-Path -LiteralPath $taskManifestPath)) { throw 'Manifeste de nettoyage introuvable.' }
$taskManifest = Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json
if ($taskManifest.Workspace -ne $taskRoot) { throw 'Le manifeste appartient à un autre dossier de projet.' }

# La liste contient uniquement les diagnostics et résultats générés identifiés lors de l'audit.
$taskAllowed = '^(tmp\\|\.local\\(desktop-verification|HybridSmoke|LabelDebug|ocr-check|ocr-tests|OpenRouterSmoke|hybrid-render\.py|ocr-diagnostic\.py|openrouter-method\.txt|reload-api-audit\.ps1|render-normalized-pdf\.py|table-layout-playwright\.config\.ts|update-catalog-selectors\.py|update-sage-goldens\.py|update-workspace-ui\.py|verify-live-workspace\.cjs)$|output\\(installation-log-test-[^\\]+|linux-test-[^\\]+|manager-test-[^\\]+|postgres-test-[^\\]+|server-test-[^\\]+|source-layout-qa|validation-source-layout|build-manual|dashboard-pdf-preview|dashboard-resources-preview|pdf-audit|screenshots|[^\\]+\.png)$|\.codex-test-artifacts$|backend\\BankStatementConverter\.Tests$|frontend\\(tests|test-results)$)'
$taskTargets = foreach ($taskEntry in $taskManifest.Targets) {
    $taskRelative = [string]$taskEntry.RelativePath
    $taskAbsolute = [IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
    if ($taskRelative -notmatch $taskAllowed -or $taskRelative -match '(?i)backup|restore' -or
        $taskAbsolute -ne $taskEntry.Path -or
        -not $taskAbsolute.StartsWith($taskRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Chemin non autorisé : ' + $taskRelative)
    }
    if (Test-Path -LiteralPath $taskAbsolute) { $taskAbsolute }
}

if (-not $Execute) {
    $taskTargets | ForEach-Object { $_.Substring($taskRoot.Length + 1) }
    Write-Output ('Éléments à supprimer : ' + @($taskTargets).Count)
    Write-Output 'Relancer avec -Execute pour effectuer le nettoyage.'
    return
}

$taskProcesses = Get-CimInstance Win32_Process | Where-Object ExecutablePath
$taskRemoved = 0
$taskSkipped = 0
foreach ($taskTarget in $taskTargets) {
    try {
        if ($taskProcesses | Where-Object {
            $_.ExecutablePath -eq $taskTarget -or
            $_.ExecutablePath.StartsWith($taskTarget + '\', [StringComparison]::OrdinalIgnoreCase)
        }) { throw 'Un processus utilise ce dossier.' }

        # Ne jamais suivre une jonction ou un lien symbolique lors d'une suppression récursive.
        $taskPending = New-Object 'System.Collections.Generic.Stack[string]'
        $taskPending.Push($taskTarget)
        while ($taskPending.Count -gt 0) {
            $taskItem = Get-Item -LiteralPath $taskPending.Pop() -Force
            if ($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Lien symbolique ou jonction : dossier conservé.'
            }
            if ($taskItem.PSIsContainer) {
                foreach ($taskChild in Get-ChildItem -LiteralPath $taskItem.FullName -Force) {
                    $taskPending.Push($taskChild.FullName)
                }
            }
        }
        Remove-Item -LiteralPath $taskTarget -Recurse -Force -ErrorAction Stop
        $taskRemoved++
    }
    catch {
        $taskSkipped++
        Write-Warning ($taskTarget + ' : ' + $_.Exception.Message)
    }
}
Write-Output ('Éléments supprimés : ' + $taskRemoved + ' ; conservés : ' + $taskSkipped)
