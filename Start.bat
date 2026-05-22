@echo off
dotnet --list-runtimes 2>nul | findstr "Microsoft.NETCore.App 8\." >nul
if %errorlevel% neq 0 (
    echo.
    echo  [FerrariX] .NET 8 Runtime no encontrado.
    echo  Abriendo descarga automaticamente...
    echo.
    start "" "https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-desktop-8.0.15-windows-x64-installer"
    pause
    exit /b 1
)
start "" "%~dp0FerrariX.exe"
