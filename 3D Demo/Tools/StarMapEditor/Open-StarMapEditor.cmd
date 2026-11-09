@echo off
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -STA -ExecutionPolicy Bypass -File "%~dp0..\LocalEditor\Start-Editor.ps1" -Editor StarMapEditor
if errorlevel 1 pause
