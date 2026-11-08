@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"

echo [CGP] Calisan uygulama kapatiliyor...
taskkill /IM CgpRandevuTakip.Desktop.exe /F >nul 2>&1

echo [CGP] Release derlemesi baslatiliyor...
dotnet build CgpRandevuTakip.sln -c Release
if errorlevel 1 (
    echo.
    echo [HATA] Release derlemesi basarisiz.
    pause
    exit /b 1
)

echo.
echo [CGP] Testler calistiriliyor...
dotnet test CgpRandevuTakip.sln -c Release --no-build
if errorlevel 1 (
    echo.
    echo [HATA] Testler basarisiz.
    pause
    exit /b 1
)

echo.
echo [OK] Release derlemesi ve testler basarili.
pause
endlocal
