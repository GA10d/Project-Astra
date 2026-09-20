param(
    [switch]$RegenerateModel,
    [string]$UnityEditor = 'F:\Unity\Installs\2022.3.62f1\Editor\Unity.exe',
    [string]$Blender = 'F:\Blender\blender.exe'
)
$ErrorActionPreference = 'Stop'
$demoRoot = $PSScriptRoot
$projectPath = Join-Path $demoRoot 'UnityProject'
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw "Unity Editor not found: $UnityEditor" }
if ($RegenerateModel) {
    if (-not (Test-Path -LiteralPath $Blender)) { throw "Blender not found: $Blender" }
    & $Blender -b --python (Join-Path $demoRoot 'Source\build_cabin.py')
    if ($LASTEXITCODE -ne 0) { throw 'Blender export failed.' }
}
$buildLog = Join-Path $demoRoot 'unity-build.log'
$arguments = '-batchmode -nographics -quit -projectPath "' + $projectPath + '" -executeMethod CabinBuild.Build -logFile "' + $buildLog + '"'
$unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$unityProcess.WaitForExit()
if ($unityProcess.ExitCode -ne 0) { throw "Unity build failed. Inspect $buildLog" }
Write-Host ('Ready: ' + (Join-Path $demoRoot 'Build\Astra Cabin.exe'))
