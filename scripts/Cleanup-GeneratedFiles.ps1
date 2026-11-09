[CmdletBinding()]
param([switch]$Execute)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')

# Known, reproducible diagnostics only. Runtimes, releases and backups stay intact.
$taskLocalNames = @(
    'auto-check', 'auto-check-artifacts', 'build-ui-review', 'bank-catalog', 'bank-catalog-artifacts',
    'clear-imports', 'clear-imports-artifacts', 'data-cleanup',
    'date-review', 'date-review-artifacts', 'diagnose-result',
    'latest-import-diagnosis', 'review-bilingual-build',
    'ui-review', 'ui-review-artifacts', 'settings-review', 'login-review', 'navigation-review', 'clean-review-build', 'check-project',
    'ollama-install', 'cleanup-plan.json', 'design-base.css',
    'python-embed.zip', 'python312-embed.zip',
    'project-clean-audit.cjs', 'clean-css.cjs', 'clean-review.log'
)
$taskRelativePaths = @($taskLocalNames | ForEach-Object { '.local\' + $_ })
$taskRelativePaths += @('tmp\result-diagnosis', 'output\analyse-releves', 'output\pdf',
    'output\audit-frontend.md', 'output\desktop-latest-stage.txt',
    '.codex-test-artifacts', 'frontend\test-results', 'frontend\playwright-report',
    'backend\BankStatementConverter.API\build-check')
$taskOutput = Join-Path $taskRoot 'output'
if (Test-Path -LiteralPath $taskOutput) {
    $taskRelativePaths += @(Get-ChildItem -LiteralPath $taskOutput -Directory | Where-Object {
        $_.Name -match '^(desktop-package-|linux-stage-|previous-route-cache-|route-cache-before-)' -or
        $_.Name -eq 'server-stage'
    } | ForEach-Object { 'output\' + $_.Name })
}

$taskProcesses = @(Get-CimInstance Win32_Process)
$taskRemoved = 0
$taskSkipped = 0
$taskBytes = [long]0
foreach ($taskRelative in ($taskRelativePaths | Sort-Object -Unique)) {
    $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
    if (-not $taskTarget.StartsWith($taskRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Target outside workspace: ' + $taskRelative)
    }
    if (-not (Test-Path -LiteralPath $taskTarget)) { continue }
    try {
        # Never delete tracked sources or anything used by a running process.
        $taskTracked = @(& git -C $taskRoot ls-files -- $taskRelative)
        if ($LASTEXITCODE -ne 0) { throw 'Git inventory failed.' }
        if ($taskTracked.Count -gt 0) { throw 'Contains tracked files.' }
        if ($taskProcesses | Where-Object {
            ($_.ExecutablePath -and ($_.ExecutablePath -eq $taskTarget -or
                $_.ExecutablePath.StartsWith($taskTarget + '\', [StringComparison]::OrdinalIgnoreCase))) -or
            ($_.CommandLine -and $_.CommandLine.IndexOf($taskTarget, [StringComparison]::OrdinalIgnoreCase) -ge 0)
        }) { throw 'In use by a running process.' }

        $taskPending = New-Object 'System.Collections.Generic.Stack[string]'
        $taskPending.Push($taskTarget)
        $taskSize = [long]0
        while ($taskPending.Count -gt 0) {
            $taskItem = Get-Item -LiteralPath $taskPending.Pop() -Force
            if ($taskItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw 'Contains a symbolic link or junction; kept.'
            }
            if ($taskItem.PSIsContainer) {
                foreach ($taskChild in Get-ChildItem -LiteralPath $taskItem.FullName -Force) {
                    $taskPending.Push($taskChild.FullName)
                }
            } else { $taskSize += $taskItem.Length }
        }
        if ($Execute) { Remove-Item -LiteralPath $taskTarget -Recurse -Force -ErrorAction Stop }
        $taskRemoved++
        $taskBytes += $taskSize
        Write-Output ('{0}: {1} ({2:N1} MiB)' -f $(if ($Execute) { 'Removed' } else { 'Candidate' }), $taskRelative, ($taskSize / 1MB))
    } catch {
        $taskSkipped++
        Write-Warning ($taskRelative + ': ' + $_.Exception.Message)
    }
}
Write-Output ('{0}: {1} targets, {2:N1} MiB; kept: {3}.' -f $(if ($Execute) { 'Removed' } else { 'Preview' }), $taskRemoved, ($taskBytes / 1MB), $taskSkipped)
if (-not $Execute) { Write-Output 'Run again with -Execute to remove the listed artifacts.' }
