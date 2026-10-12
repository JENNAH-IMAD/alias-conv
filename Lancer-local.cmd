@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-Alias.ps1" -Mode Local -Open
if errorlevel 1 pause
