@echo off
REM Self-sign and install the BrightnessVHid driver (test-signing required).
REM Run as Administrator. Adjust OUT and SIGNTOOL paths if needed.

setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%BrightnessVHid\x64\Release\BrightnessVHid"
set "CERTNAME=HonorPCHelper BrightnessVHid Test"

if not exist "%OUT%\BrightnessVHid.sys" (
    echo [ERROR] Build output not found: %OUT%\BrightnessVHid.sys
    echo         Run build.cmd inside the EWDK environment first.
    exit /b 1
)

REM --- locate signtool / inf2cat from the EWDK/WDK (already in PATH inside EWDK env) ---
where signtool >nul 2>&1 || ( echo [ERROR] signtool not found ^(open EWDK env^). & exit /b 1 )

REM --- 1) create a self-signed code-signing cert in LocalMachine\My (once) ---
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$c = Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.Subject -eq 'CN=%CERTNAME%' } | Select-Object -First 1;" ^
  "if (-not $c) { $c = New-SelfSignedCertificate -Type CodeSigningCert -Subject 'CN=%CERTNAME%' -CertStoreLocation Cert:\LocalMachine\My -KeyUsage DigitalSignature -KeyExportPolicy Exportable -NotAfter (Get-Date).AddYears(10) };" ^
  "$pwd = ConvertTo-SecureString -String 'bvhid' -Force -AsPlainText;" ^
  "Export-PfxCertificate -Cert $c -FilePath '%ROOT%bvhid.pfx' -Password $pwd | Out-Null;" ^
  "Export-Certificate -Cert $c -FilePath '%ROOT%bvhid.cer' | Out-Null;" ^
  "Write-Host ('cert thumbprint: ' + $c.Thumbprint)"
if errorlevel 1 ( echo [ERROR] cert creation failed & exit /b 1 )

REM --- 2) trust the cert (Root + TrustedPublisher) so Windows accepts the driver ---
certutil -addstore -f Root "%ROOT%bvhid.cer"
certutil -addstore -f TrustedPublisher "%ROOT%bvhid.cer"

REM --- 3) sign the .sys ---
signtool sign /fd SHA256 /a /f "%ROOT%bvhid.pfx" /p bvhid /t http://timestamp.digicert.com "%OUT%\BrightnessVHid.sys"
if errorlevel 1 ( echo [ERROR] signing .sys failed & exit /b 1 )

REM --- 4) (re)generate and sign the catalog so the INF install is trusted ---
inf2cat /driver:"%OUT%" /os:10_X64 /verbose
signtool sign /fd SHA256 /a /f "%ROOT%bvhid.pfx" /p bvhid /t http://timestamp.digicert.com "%OUT%\BrightnessVHid.cat"
if errorlevel 1 ( echo [ERROR] signing .cat failed & exit /b 1 )

echo.
echo [OK] Signed. Next: enable test-signing if not already:
echo        bcdedit /set testsigning on   ^&^&   reboot
echo      Then install with:  install_device.cmd
endlocal
