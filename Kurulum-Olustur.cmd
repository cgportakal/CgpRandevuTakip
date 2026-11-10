@echo off
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Kurulum-Olustur.ps1" -Version 1.2.0
if errorlevel 1 (
    echo.
    echo [CGP] Kurulum olusturma basarisiz.
    pause
    exit /b 1
)
echo.
echo [CGP] Bitti.
pause
