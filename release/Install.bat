@echo off
setlocal

REM Self-elevate to admin if needed
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

set "SRC=%~dp0PaintNetMacroApi.dll"
set "DST=C:\Program Files\paint.net\Effects"

echo.
echo === Paint.NET Macro API installer ===
echo.

if not exist "%SRC%" (
    echo ERROR: PaintNetMacroApi.dll not found next to this installer.
    pause
    exit /b 1
)

if not exist "%DST%" (
    echo ERROR: Paint.NET does not appear to be installed at:
    echo   C:\Program Files\paint.net\
    echo Install Paint.NET 5.x first from https://getpaint.net
    pause
    exit /b 1
)

REM Stop Paint.NET if running
tasklist /FI "IMAGENAME eq paintdotnet.exe" 2>nul | find /I "paintdotnet.exe" >nul
if not errorlevel 1 (
    echo Closing Paint.NET...
    taskkill /F /IM paintdotnet.exe >nul 2>&1
    timeout /t 1 /nobreak >nul
)

echo Copying plugin...
copy /Y "%SRC%" "%DST%\" >nul
if errorlevel 1 (
    echo ERROR: Failed to copy DLL.
    pause
    exit /b 1
)

echo.
echo Installed successfully.
echo Open Paint.NET and look for: Effects -^> Tools -^> Macro API
echo.
pause
