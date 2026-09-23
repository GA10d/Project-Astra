param([string]$BuildFolder = 'BuildNavigation', [ValidateRange(0,4)][int]$StartAt = 0)
$ErrorActionPreference = 'Stop'
$executable = Join-Path $PSScriptRoot ($BuildFolder + '\Astra Cabin.exe')
$suites = @(
    @{Name='Navigation-final'; Flag='--astra-navigation-qa'; Report='navigation-verification.txt'},
    @{Name='Navigation-base'; Flag='--astra-qa'; Report='verification.txt'},
    @{Name='Navigation-combat'; Flag='--astra-combat-qa'; Report='combat-verification.txt'},
    @{Name='Navigation-setpieces'; Flag='--astra-setpiece-qa'; Report='setpiece-verification.txt'},
    @{Name='Navigation-render'; Flag='--astra-render-qa'; Report='render-verification.txt'}
)
foreach ($suite in ($suites | Select-Object -Skip $StartAt)) {
    $directory = Join-Path $PSScriptRoot ('QA-' + $suite.Name)
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    # A distinct launch size makes suites that select 1600x900 recreate their
    # rendering surface even when Windows starts the QA player hidden.
    $arguments = '-screen-fullscreen 0 -screen-width 1024 -screen-height 768 ' + $suite.Flag + ' "' + $directory + '" -logFile "' + (Join-Path $directory 'player.log') + '"'
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output ('START ' + $suite.Name + ' PID=' + $process.Id)
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw ($suite.Name + ' failed with code ' + $process.ExitCode) }
    Write-Output ('PASS ' + $suite.Name)
}
