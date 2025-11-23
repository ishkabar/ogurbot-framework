@echo off
setlocal

echo Cleaning old packages and build artifacts...
cd /d "%~dp0"

rmdir /s /q "ogur.core\bin" 2>nul
rmdir /s /q "ogur.core\obj" 2>nul

echo.
echo Building and packing Ogur.Core...
echo.

dotnet pack ogur.core\ogur.core.csproj -c Release

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Pack failed!
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [SUCCESS] Package generated!
dir "C:\Users\Dominik\source\artifacts\nuget\Ogur.Core.0.2.1-*"
echo.
pause
