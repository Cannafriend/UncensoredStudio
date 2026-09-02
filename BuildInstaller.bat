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
python -c "
import zipfile, os
payload_zip = r'%~dp0installer\payload.zip'
os.makedirs(os.path.dirname(payload_zip), exist_ok=True)
files = [(r'%~dp0UncensoredStudio.exe', 'UncensoredStudio.exe'), (r'%~dp0backend\koboldcpp.exe', r'backend\koboldcpp.exe'), (r'%~dp0Logo.png', 'Logo.png'), (r'%~dp0LaunchStudio.bat', 'LaunchStudio.bat'), (r'%~dp0README.md', 'README.md')]
with zipfile.ZipFile(payload_zip, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
    for src, dst in files:
        if os.path.exists(src): zf.write(src, dst)
    assets_dir = r'%~dp0Assets'
    if os.path.exists(assets_dir):
        for root, dirs, f_list in os.walk(assets_dir):
            for f in f_list:
                full_p = os.path.join(root, f)
                rel_p = os.path.relpath(full_p, r'%~dp0')
                zf.write(full_p, rel_p)
"

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
