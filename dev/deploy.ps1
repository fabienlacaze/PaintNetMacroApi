# Deploy.ps1 - kills Paint.NET, copies fresh DLL, optionally restarts.
# Run from elevated PowerShell. Or call with -SkipRestart to leave PDN closed.
param([switch]$SkipRestart, [switch]$Verbose)

$ErrorActionPreference = "Stop"

$src = "C:\tmp\PaintNetMacroApi\bin\Release\net9.0-windows\PaintNetMacroApi.dll"
$dst = "C:\Program Files\paint.net\Effects\PaintNetMacroApi.dll"
$pdn = "C:\Program Files\paint.net\paintdotnet.exe"

if (-not (Test-Path $src)) {
    Write-Host "[deploy] Source DLL missing: $src" -ForegroundColor Red
    Write-Host "[deploy] Run 'dotnet build -c Release' first." -ForegroundColor Yellow
    exit 1
}

# 1. Kill Paint.NET
$procs = Get-Process paintdotnet -ErrorAction SilentlyContinue
if ($procs) {
    Write-Host "[deploy] Killing Paint.NET ($($procs.Count) process)..." -ForegroundColor Cyan
    $procs | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

# 2. Copy DLL
Write-Host "[deploy] Copying DLL ($((Get-Item $src).Length) bytes)..." -ForegroundColor Cyan
Copy-Item -Force $src $dst
Write-Host "[deploy] Deployed -> $dst" -ForegroundColor Green

# 3. Restart Paint.NET (unless skipped)
if (-not $SkipRestart) {
    Write-Host "[deploy] Restarting Paint.NET..." -ForegroundColor Cyan
    Start-Process $pdn
    Write-Host "[deploy] Done. Click Effects -> Tools -> Macro API to test." -ForegroundColor Green
} else {
    Write-Host "[deploy] Done. Paint.NET NOT restarted (--SkipRestart)." -ForegroundColor Green
}
