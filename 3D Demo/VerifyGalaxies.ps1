param([string]$BuildFolder = 'Build')
$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot ($BuildFolder + '\Astra Cabin.exe')
if (!(Test-Path -LiteralPath $executable -PathType Leaf)) { throw ('Build executable not found: ' + $executable) }
$suites = @(
    @{Name='Galaxies'; Flag='--astra-navigation-qa'; Report='navigation-verification.txt'},
    @{Name='Galaxies-base'; Flag='--astra-qa'; Report='verification.txt'},
    @{Name='Galaxies-meeting'; Flag='--astra-meeting-qa'; Report='meeting-verification.txt'}
)
foreach ($suite in $suites) {
    $directory = Join-Path $PSScriptRoot ('QA-' + $suite.Name)
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $arguments = '-screen-fullscreen 0 -screen-width 1024 -screen-height 768 ' + $suite.Flag + ' "' + $directory + '" -logFile "' + (Join-Path $directory 'player.log') + '"'
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output ('START ' + $suite.Name + ' PID=' + $process.Id)
    if (!$process.WaitForExit(360000)) { Stop-Process -Id $process.Id -Force; throw ($suite.Name + ' timed out') }
    if ($process.ExitCode -ne 0) {
        Get-Content -LiteralPath (Join-Path $directory $suite.Report) -Encoding utf8 | Where-Object { $_ -match '^(FAIL|ERROR|RESULT)' }
        throw ($suite.Name + ' failed with code ' + $process.ExitCode)
    }
    Get-Content -LiteralPath (Join-Path $directory $suite.Report) -Encoding utf8 | Select-Object -Last 1
    Write-Output ('PASS ' + $suite.Name)
}
