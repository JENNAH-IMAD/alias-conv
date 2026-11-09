[CmdletBinding()]
param([switch]$Production)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$taskArtifacts = Join-Path $taskRoot '.local\check-project'
function Invoke-ProjectCheck([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw ('Check failed: ' + $Command + ' ' + ($Arguments -join ' ')) }
}

Push-Location $taskRoot
try {
    Invoke-ProjectCheck 'dotnet' @('build', 'BankStatementConverter.sln', '--artifacts-path', $taskArtifacts, '--warnaserror')
    $taskTools = @('ProjectSnapshot')
    if ($env:OS -eq 'Windows_NT') { $taskTools += @('DesktopApp', 'ServerManager') }
    foreach ($taskTool in $taskTools) {
        Invoke-ProjectCheck 'dotnet' @('build', "tools/$taskTool/$taskTool.csproj", '--artifacts-path', $taskArtifacts, '--warnaserror')
        Invoke-ProjectCheck 'dotnet' @('format', 'whitespace', "tools/$taskTool/$taskTool.csproj", '--no-restore',
            '--verify-no-changes', '--verbosity', 'quiet')
    }
    Invoke-ProjectCheck 'dotnet' @('format', 'whitespace', 'BankStatementConverter.sln', '--no-restore',
        '--verify-no-changes', '--exclude', 'backend/BankStatementConverter.Infrastructure/Migrations', '--verbosity', 'quiet')

    foreach ($taskDirectory in @('scripts', 'installer', 'services')) {
        Get-ChildItem -LiteralPath $taskDirectory -Recurse -Filter '*.ps1' -File | ForEach-Object {
            $taskTokens = $null
            $taskErrors = $null
            [Management.Automation.Language.Parser]::ParseFile($_.FullName, [ref]$taskTokens, [ref]$taskErrors) | Out-Null
            if ($taskErrors.Count) { throw ('Invalid PowerShell: ' + $_.FullName + ': ' + $taskErrors[0].Message) }
        }
    }

    Push-Location (Join-Path $taskRoot 'frontend')
    try {
        $taskNpm = if ($env:OS -eq 'Windows_NT') { 'npm.cmd' } else { 'npm' }
        Invoke-ProjectCheck $taskNpm @('run', 'typecheck')
        Invoke-ProjectCheck $taskNpm @('run', 'lint', '--', '--max-warnings=0')
        Invoke-ProjectCheck $taskNpm @('run', 'format:check')
        if ($Production) {
            # Preserve the active development server's .next directory.
            $taskOldBuild = $env:ALIAS_RUNTIME_BUILD
            try {
                $env:ALIAS_RUNTIME_BUILD = '1'
                Invoke-ProjectCheck $taskNpm @('run', 'build')
            } finally { $env:ALIAS_RUNTIME_BUILD = $taskOldBuild }
        }
    } finally { Pop-Location }
} finally { Pop-Location }
Write-Output 'Project checks passed. Review real bank PDFs manually before release.'
