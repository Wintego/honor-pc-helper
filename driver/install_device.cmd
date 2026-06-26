@echo off
REM Install the root-enumerated BrightnessVHid device. Run as Administrator.
REM Requires test-signing ON (bcdedit /set testsigning on + reboot) and signed driver.

setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%BrightnessVHid\x64\Release\BrightnessVHid"

if not exist "%OUT%\BrightnessVHid.inf" (
    echo [ERROR] %OUT%\BrightnessVHid.inf not found. Build + sign first.
    exit /b 1
)

REM Prefer devcon (ships with EWDK/WDK). Fallback to pnputil add-driver.
where devcon >nul 2>&1
if not errorlevel 1 (
    devcon install "%OUT%\BrightnessVHid.inf" root\BrightnessVHid
    goto :verify
)

echo [info] devcon not found, using pnputil + manual device add is required.
pnputil /add-driver "%OUT%\BrightnessVHid.inf" /install
echo [info] If no device appears, install devcon from the WDK and run:
echo        devcon install "%OUT%\BrightnessVHid.inf" root\BrightnessVHid

:verify
echo.
echo [check] Look for "Brightness Virtual HID" in Device Manager (System devices),
echo         and a new HID-compliant consumer control device.
powershell -NoProfile -Command "Get-PnpDevice | Where-Object { $_.InstanceId -match 'BrightnessVHid' } | Select-Object Status,FriendlyName,InstanceId | Format-List"
endlocal
