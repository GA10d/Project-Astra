param(
    [switch]$RegenerateModel,
    [switch]$RebuildScene,
    [string]$UnityEditor = $env:UNITY_EDITOR_PATH,
    [string]$Blender = 'F:\Blender\blender.exe'
)
$ErrorActionPreference = 'Stop'
$demoRoot = $PSScriptRoot
$projectPath = Join-Path $demoRoot 'UnityProject'
if ([string]::IsNullOrWhiteSpace($UnityEditor)) {
    $candidates = @('F:\Unity\Installs\2022.3.62f1\Editor\Unity.exe', (Join-Path $env:ProgramFiles 'Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe'))
    $UnityEditor = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($UnityEditor) -or !(Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw 'Unity 2022.3.62f1 not found. Pass -UnityEditor with your installed Unity.exe path.' }
if ($RegenerateModel) {
    if (-not (Test-Path -LiteralPath $Blender)) { throw "Blender not found: $Blender" }
    & $Blender -b --python (Join-Path $demoRoot 'Source\build_cabin.py')
    if ($LASTEXITCODE -ne 0) { throw 'Blender export failed.' }
    & $Blender -b (Join-Path $demoRoot 'Source\Astra_Cabin.blend') --python (Join-Path $demoRoot 'Source\apply_materials.py')
    if ($LASTEXITCODE -ne 0) { throw 'Blender material refresh failed.' }
    $RebuildScene = $true
}
$buildLog = Join-Path $demoRoot 'unity-build.log'
# Daily builds retain the authored scene. Scene generation is an explicit operation.
$buildMethod = if ($RebuildScene) { 'CabinBuild.Build' } else { 'CabinBuild.BuildDevelopment' }
$arguments = '-batchmode -nographics -quit -projectPath "' + $projectPath + '" -executeMethod ' + $buildMethod + ' -logFile "' + $buildLog + '"'
$unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
$unityProcess.WaitForExit()
if ($unityProcess.ExitCode -ne 0) { throw "Unity build failed. Inspect $buildLog" }
Write-Host ('Ready: ' + (Join-Path $demoRoot 'Build\Astra Cabin.exe'))
