@echo off
setlocal

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

set "DLL=C:\Program Files\paint.net\Effects\PaintNetMacroApi.dll"

echo.
echo === Paint.NET Macro API uninstaller ===
echo.

tasklist /FI "IMAGENAME eq paintdotnet.exe" 2>nul | find /I "paintdotnet.exe" >nul
if not errorlevel 1 (
    echo Closing Paint.NET...
    taskkill /F /IM paintdotnet.exe >nul 2>&1
    timeout /t 1 /nobreak >nul
)

if exist "%DLL%" (
    del /F /Q "%DLL%"
    echo Removed %DLL%
) else (
    echo Plugin was not installed.
)

echo.
echo Note: your saved macros are still under
echo   %%AppData%%\PaintNetMacroApi\macros
echo Delete that folder manually if you want them gone.
echo.
pause
