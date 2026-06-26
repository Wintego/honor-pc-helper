@echo off
REM Build BrightnessVHid.sys with the EWDK.
REM 1) Mount/extract the EWDK ISO, then run its LaunchBuildEnv.cmd ONCE to enter the build env.
REM 2) From that env, cd to this folder and run:  build.cmd
REM
REM If msbuild is not found, you are not inside the EWDK build environment.
REM Run  <EWDK>\LaunchBuildEnv.cmd  first.

setlocal
cd /d "%~dp0BrightnessVHid"

where msbuild >nul 2>&1
if errorlevel 1 (
    echo [ERROR] msbuild not found. Open the EWDK build environment first:
    echo         ^<EWDK^>\LaunchBuildEnv.cmd
    exit /b 1
)

msbuild BrightnessVHid.vcxproj /p:Configuration=Release /p:Platform=x64 /t:Rebuild
if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)

echo.
echo [OK] Output: %~dp0BrightnessVHid\x64\Release\BrightnessVHid\
dir /b "%~dp0BrightnessVHid\x64\Release\BrightnessVHid\"
endlocal
