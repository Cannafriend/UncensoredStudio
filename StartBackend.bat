@echo off
title KoboldCPP Vulkan Backend (AMD RX 6700 XT)
cd /d "%~dp0backend"

echo ========================================================
echo   Launching KoboldCPP Engine (AMD Vulkan Hardware Acceleration)
echo ========================================================
echo.

if not exist "koboldcpp.exe" (
    echo [ERROR] koboldcpp.exe not found in %~dp0backend
    pause
    exit /b
)

REM Find first GGUF model in models folder if available
set MODEL_ARG=
for /r "%~dp0models" %%i in (*.gguf) do (
    if not defined MODEL_ARG set "MODEL_ARG=%%i"
)

if defined MODEL_ARG (
    echo [INFO] Found model: %MODEL_ARG%
    koboldcpp.exe --usevulkan --gpulayers 99 --contextsize 8192 --port 5001 --model "%MODEL_ARG%" --skiplauncher
) else (
    echo [INFO] No model specified. Starting KoboldCPP in server GUI mode...
    koboldcpp.exe --usevulkan --gpulayers 99 --contextsize 8192 --port 5001
)

pause
