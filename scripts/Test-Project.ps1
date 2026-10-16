[CmdletBinding()]
param(
    [switch]$IncludeIntegration,
    [switch]$IncludeE2E
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot

function Invoke-ProjectCheck {
    param(
        [string]$Command,
        [string[]]$Arguments
    )

    Write-Host "`n> $Command $($Arguments -join ' ')"
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Verification echouee : $Command $($Arguments -join ' ')"
    }
}

Push-Location $projectRoot
try {
    Invoke-ProjectCheck -Command 'dotnet' -Arguments @('build', 'BankStatementConverter.sln')
    Invoke-ProjectCheck -Command 'dotnet' -Arguments @(
        'format', 'whitespace', 'BankStatementConverter.sln', '--no-restore',
        '--verify-no-changes', '--exclude', 'backend/BankStatementConverter.Infrastructure/Migrations'
    )

    Push-Location (Join-Path $projectRoot 'frontend')
    try {
        Invoke-ProjectCheck -Command 'npm' -Arguments @('run', 'format:check')
        Invoke-ProjectCheck -Command 'npm' -Arguments @('run', 'typecheck')
        Invoke-ProjectCheck -Command 'npm' -Arguments @('run', 'lint', '--', '--max-warnings=0')
        Invoke-ProjectCheck -Command 'npm' -Arguments @('run', 'build')
        if ($IncludeE2E) {
            Invoke-ProjectCheck -Command 'npm' -Arguments @('run', 'test:e2e')
        }
    }
    finally {
        Pop-Location
    }

    # Les tests d'integration creent une base temporaire : activation explicite.
    $testArguments = @('test', 'backend/BankStatementConverter.Tests', '--no-build')
    if (-not $IncludeIntegration) {
        $testArguments += @('--filter', 'Category!=Integration')
    }
    Invoke-ProjectCheck -Command 'dotnet' -Arguments $testArguments
}
finally {
    Pop-Location
}
