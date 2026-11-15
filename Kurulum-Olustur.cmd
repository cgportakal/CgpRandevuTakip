@echo off
setlocal
cd /d "%~dp0"
rem Ara surumlerde tasinabilir ZIP uretilmez; kararli surumde -Portable ekleyin.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Kurulum-Olustur.ps1"
if errorlevel 1 (
    echo.
    echo [CGP] Kurulum olusturma basarisiz.
    pause
    exit /b 1
)
echo.
echo [CGP] Bitti.
pause
