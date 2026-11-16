@echo off
setlocal

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\basla.ps1" %*

if errorlevel 1 (
  if /I "%~1"=="--yes" exit /b 1
  if /I "%~1"=="-y" exit /b 1
  if /I "%~1"=="--check" exit /b 1
  echo.
  pause
  exit /b 1
)

if /I "%~1"=="--yes" exit /b 0
if /I "%~1"=="-y" exit /b 0
if /I "%~1"=="--check" exit /b 0

echo.
pause
