@echo off
title Model Downloader - Uncensored Fiction Weights
echo ====================================================================
echo   Uncensored Model Downloader for Fiction, Romance, & Twitter Posts
echo ====================================================================
echo.
echo Select model to download to I:\UncensoredAI\models:
echo.
echo [1] Magnum-12B-v2 (Q4_K_M ~7.5 GB) - Top recommendation for deep descriptive prose & smut
echo [2] Llama-3.1-8B-Stheno-v3.2 (Q4_K_M ~4.9 GB) - Blazing fast evocative prose & dialogue
echo [3] Download Both Models
echo.
set /p choice="Enter choice (1, 2, or 3): "

if "%choice%"=="1" goto dl_magnum
if "%choice%"=="2" goto dl_stheno
if "%choice%"=="3" goto dl_both
echo Invalid choice.
pause
exit /b

:dl_magnum
echo.
echo Downloading Magnum-12B-v2 Q4_K_M (7.5 GB)...
python -c "import urllib.request; opener=urllib.request.build_opener(); opener.addheaders=[('User-Agent','Mozilla/5.0')]; urllib.request.install_opener(opener); urllib.request.urlretrieve('https://huggingface.co/bartowski/magnum-12b-v2-GGUF/resolve/main/magnum-12b-v2-Q4_K_M.gguf', r'I:\UncensoredAI\models\magnum-12b-v2-Q4_K_M.gguf', lambda b, bs, sz: print(f'Downloaded: {b*bs/(1024*1024):.1f} MB / {sz/(1024*1024):.1f} MB', end='\r'))"
echo.
echo [SUCCESS] Magnum-12B-v2 downloaded!
pause
exit /b

:dl_stheno
echo.
echo Downloading L3-8B-Stheno-v3.2 Q4_K_M (4.9 GB)...
python -c "import urllib.request; opener=urllib.request.build_opener(); opener.addheaders=[('User-Agent','Mozilla/5.0')]; urllib.request.install_opener(opener); urllib.request.urlretrieve('https://huggingface.co/bartowski/L3-8B-Stheno-v3.2-GGUF/resolve/main/L3-8B-Stheno-v3.2-Q4_K_M.gguf', r'I:\UncensoredAI\models\L3-8B-Stheno-v3.2-Q4_K_M.gguf', lambda b, bs, sz: print(f'Downloaded: {b*bs/(1024*1024):.1f} MB / {sz/(1024*1024):.1f} MB', end='\r'))"
echo.
echo [SUCCESS] L3-8B-Stheno-v3.2 downloaded!
pause
exit /b

:dl_both
call :dl_stheno
call :dl_magnum
exit /b
