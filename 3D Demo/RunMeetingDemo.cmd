@echo off
cd /d "%~dp0"
if not exist "Build\Astra Cabin.exe" (
    echo Build is missing. Run Rebuild.ps1 first.
    pause
    exit /b 1
)
start "" "Build\Astra Cabin.exe" --astra-meeting-demo
