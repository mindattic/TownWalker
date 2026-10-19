@echo off
rem Builds the Windows release player and publishes it to C:\Apps\TownWalker, refreshing
rem the "TownWalker" Desktop shortcut to point at the published exe. See tools\deploy.ps1.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\deploy.ps1" %*
