@echo off
cd /d "%~dp0"
if not exist "BuildMeeting\Astra Cabin.exe" (
    echo BuildMeeting is missing. Run SyncPerformance.ps1 -Build first.
    pause
    exit /b 1
)
start "" "BuildMeeting\Astra Cabin.exe" --astra-meeting-demo
