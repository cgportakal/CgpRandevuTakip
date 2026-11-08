@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"

echo [CGP] Calisan uygulama kapatiliyor...
taskkill /IM CgpRandevuTakip.Desktop.exe /F >nul 2>&1

echo [CGP] Debug derlemesi baslatiliyor...
dotnet build CgpRandevuTakip.sln -c Debug
if errorlevel 1 (
    echo.
    echo [HATA] Debug derlemesi basarisiz.
    pause
    exit /b 1
)

set "EXE=src\CgpRandevuTakip.Desktop\bin\Debug\net8.0-windows10.0.19041.0\CgpRandevuTakip.Desktop.exe"
if not exist "%EXE%" (
    for /f "delims=" %%F in ('dir /b /s "src\CgpRandevuTakip.Desktop\bin\Debug\CgpRandevuTakip.Desktop.exe" 2^>nul') do set "EXE=%%F"
)

if not exist "%EXE%" (
    echo [HATA] Uygulama exe dosyasi bulunamadi.
    pause
    exit /b 1
)

echo [CGP] Uygulama baslatiliyor...
start "" "%EXE%"
endlocal
