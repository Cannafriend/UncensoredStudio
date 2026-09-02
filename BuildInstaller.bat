@echo off
title Building Uncensored Studio Modern Installer...
echo ======================================================================
echo           UNCENSORED STUDIO - MODERN INSTALLER SUITE
echo ======================================================================
echo.

echo [1/4] Building Studio Release Executable...
dotnet publish "%~dp0src\UncensoredStudio\UncensoredStudio.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0Release"
if %errorlevel% neq 0 (
    echo [ERROR] Dotnet studio build failed!
    pause
    exit /b %errorlevel%
)

echo.
echo [2/4] Packing Distribution Payload Archive...
python "%~dp0installer\pack_payload.py"
if %errorlevel% neq 0 (
    echo [ERROR] Payload packing failed!
    pause
    exit /b %errorlevel%
)

echo.
echo [3/4] Compiling Custom Modern Dark Installer (WPF)...
dotnet publish "%~dp0src\UncensoredStudioInstaller\UncensoredStudioInstaller.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0Release"
if %errorlevel% neq 0 (
    echo [ERROR] Modern installer build failed!
    pause
    exit /b %errorlevel%
)

echo.
echo [4/4] Compiling Inno Setup Fallback Package...
"C:\Users\rawco\AppData\Local\Programs\Inno Setup 6\ISCC.exe" "%~dp0installer\UncensoredStudioSetup.iss" >nul 2>&1
copy /Y "%~dp0installer\dist\UncensoredStudio-v1.0.0-Setup.exe" "%~dp0Release\UncensoredStudio-v1.0.0-Setup.exe" >nul 2>&1

echo.
echo ======================================================================
echo [SUCCESS] Professional Modern Installer Suite Created!
echo.
echo  1. Modern Studio Installer:  %~dp0Release\UncensoredStudio-Installer.exe
echo  2. Standard Inno Setup:      %~dp0Release\UncensoredStudio-v1.0.0-Setup.exe
echo ======================================================================
pause
