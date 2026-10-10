param(
    [string]$ToolRoot,
    [string]$EditorPath=$env:UNITY_EDITOR_PATH,
    [string]$Package='Assets/AstraToolV2/Samples/Meeting/RelayMeeting.asset',
    [ValidatePattern('^[A-Za-z0-9_-]{1,64}$')][string]$PerformanceId='relay',
    [switch]$Build
)
$ErrorActionPreference='Stop'
if ([string]::IsNullOrWhiteSpace($ToolRoot)) { throw 'Optional external performance import: pass -ToolRoot with the authoring tool directory. Existing meetings already work inside UnityProject.' }
if ([string]::IsNullOrWhiteSpace($EditorPath)) {
    $candidates=@('F:\Unity\Installs\2022.3.62f1\Editor\Unity.exe',(Join-Path $env:ProgramFiles 'Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe'))
    $EditorPath=$candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
$sourceProject=Join-Path ([IO.Path]::GetFullPath($ToolRoot)) 'UnityProject'
$exportFolder=Join-Path ([IO.Path]::GetFullPath($ToolRoot)) ('Export\Meeting-'+$PerformanceId)
$targetProject=Join-Path $PSScriptRoot 'UnityProject'
$reportFolder=Join-Path $PSScriptRoot 'QA-Meeting'
New-Item -ItemType Directory -Path $reportFolder -Force | Out-Null
if([string]::IsNullOrWhiteSpace($EditorPath) -or -not (Test-Path -LiteralPath $EditorPath -PathType Leaf)){throw 'Unity 2022.3.62f1 not found. Pass -EditorPath with your Unity.exe path.'}
if(-not (Test-Path -LiteralPath $sourceProject -PathType Container)){throw ('External authoring project not found: '+$sourceProject)}
# Close the relevant Unity project before running batch export/build.
$exportLog=Join-Path $reportFolder 'export.log'
$arguments=@('-batchmode','-nographics','-quit','-projectPath',('"'+$sourceProject+'"'),'-executeMethod','ToolV2MeetingExport.ExportSample','-meetingPackage',('"'+$Package+'"'),'-meetingId',$PerformanceId,'-meetingOutput',('"'+$exportFolder+'"'),'-logFile',('"'+$exportLog+'"'))
$exportProcess=Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if($exportProcess.ExitCode -ne 0 -or -not (Select-String -LiteralPath $exportLog -Pattern 'ASTRA_MEETING_EXPORT_SUCCEEDED' -Quiet)){throw ('Export failed: '+$exportLog)}
$sourceAssets=[IO.Path]::GetFullPath((Join-Path $exportFolder 'Assets')).TrimEnd('\')
$targetAssets=[IO.Path]::GetFullPath((Join-Path $targetProject 'Assets')).TrimEnd('\')
foreach($file in Get-ChildItem -LiteralPath $sourceAssets -File -Recurse) {
    $relative=$file.FullName.Substring($sourceAssets.Length+1)
    $destination=[IO.Path]::GetFullPath((Join-Path $targetAssets $relative))
    if(-not $destination.StartsWith($targetAssets+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid exported path'}
    # Preserve an existing alias GUID so scene/editor references to it remain stable.
    if($relative -eq ('AstraToolV2\Resources\Performances\'+$PerformanceId+'.asset.meta') -and (Test-Path -LiteralPath $destination)){continue}
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}
Copy-Item -LiteralPath (Join-Path $ToolRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $PSScriptRoot 'Source\PerformanceToolV2_NOTICES.md') -Force
Write-Output ('Synced performance: '+$PerformanceId)
if($Build) {
    $buildLog=Join-Path $reportFolder 'build.log'
    $arguments=@('-batchmode','-nographics','-quit','-projectPath',('"'+$targetProject+'"'),'-executeMethod','CabinMeetingBuild.Build','-logFile',('"'+$buildLog+'"'))
    $buildProcess=Start-Process -FilePath $EditorPath -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if($buildProcess.ExitCode -ne 0 -or -not (Select-String -LiteralPath $buildLog -Pattern 'ASTRA_MEETING_BUILD_SUCCEEDED' -Quiet)){throw ('Build failed: '+$buildLog)}
    Write-Output 'Built BuildMeeting/Astra Cabin.exe'
}
