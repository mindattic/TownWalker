@echo off
rem Asks first: 1) Deploy & Run builds the Windows release player, publishes it to
rem C:\Apps\TownWalker (refreshing the "TownWalker" Desktop shortcut) and launches it;
rem 2) Run just launches the copy already in C:\Apps\TownWalker. See tools\deploy.ps1.
setlocal
set "EXE=C:\Apps\TownWalker\TownWalker.exe"
echo.
echo   TownWalker
echo     1^) Deploy ^& Run
echo     2^) Run
echo.
choice /c 12 /n /m "  Choose 1 or 2: "
if errorlevel 2 goto run

:deploy
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\deploy.ps1" -Launch %*
if errorlevel 1 (
    echo   Deploy failed; launching the installed copy instead.
    if exist "%EXE%" start "" "%EXE%"
    pause
)
exit /b

:run
if not exist "%EXE%" (
    echo   Nothing is deployed to C:\Apps\TownWalker yet; deploying first.
    goto deploy
)
start "" "%EXE%" %*
