@echo off
rem Builds TowerHaat and deploys it to MonsterASP with Web Deploy, like the GitHub "Deploy" workflow.
rem
rem   publish.cmd [staging^|production] [/self-contained] [/skip-tests]
rem
rem Credentials come from publish.staging.local.cmd or publish.production.local.cmd next to this
rem file (gitignored; the first run writes a template). Needs the .NET SDK, Node.js and Web Deploy
rem (https://www.iis.net/downloads/microsoft/web-deploy, or: choco install webdeploy).
setlocal EnableExtensions DisableDelayedExpansion
cd /d "%~dp0"

set "TARGET=staging"
set "SELF_CONTAINED=false"
set "SKIP_TESTS="
:args
if "%~1"=="" goto args_done
if /i "%~1"=="staging" (set "TARGET=staging") else if /i "%~1"=="production" (set "TARGET=production") else if /i "%~1"=="/self-contained" (set "SELF_CONTAINED=true") else if /i "%~1"=="/skip-tests" (set "SKIP_TESTS=1") else (
  echo Unknown argument: %~1
  echo Usage: publish.cmd [staging^|production] [/self-contained] [/skip-tests]
  exit /b 2
)
shift
goto args
:args_done

if /i "%TARGET%"=="production" (set "ENV_NAME=Production") else (set "ENV_NAME=Staging")

set "SETTINGS=publish.%TARGET%.local.cmd"
if not exist "%SETTINGS%" (
  > "%SETTINGS%" (
    echo @rem Web Deploy settings for %TARGET%. Never commit this file.
    echo @rem From the MonsterASP panel: the site's Web Deploy / publish settings.
    echo @rem Write %%%% for a %% sign in the password.
    echo set "WEBDEPLOY_URL=https://SERVER:8172/msdeploy.axd?site=SITE"
    echo set "WEBDEPLOY_SITE=SITE"
    echo set "WEBDEPLOY_USERNAME=USER"
    echo set "WEBDEPLOY_PASSWORD=PASSWORD"
    echo set "SITE_URL=https://staging.towerhaat.com"
  )
  echo Wrote %SETTINGS%. Fill in the Web Deploy details from the hosting panel, then run this again.
  exit /b 1
)
call "%SETTINGS%"
for %%V in (WEBDEPLOY_URL WEBDEPLOY_SITE WEBDEPLOY_USERNAME WEBDEPLOY_PASSWORD SITE_URL) do (
  if not defined %%V (
    echo %%V is not set in %SETTINGS%.
    exit /b 1
  )
)
if "%WEBDEPLOY_SITE%"=="SITE" (
  echo Fill in %SETTINGS% first.
  exit /b 1
)

set "MSDEPLOY=%ProgramFiles%\IIS\Microsoft Web Deploy V3\msdeploy.exe"
if not exist "%MSDEPLOY%" (
  echo Web Deploy was not found at "%MSDEPLOY%".
  echo Install it from https://www.iis.net/downloads/microsoft/web-deploy or with: choco install webdeploy
  exit /b 1
)

if /i "%TARGET%"=="production" (
  set /p "CONFIRM=Deploy to PRODUCTION (%SITE_URL%)? Type yes: "
)
if /i "%TARGET%"=="production" if /i not "%CONFIRM%"=="yes" (
  echo Cancelled.
  exit /b 1
)

echo.
echo === Deploying to %TARGET% (%SITE_URL%) ===

if defined SKIP_TESTS goto build
echo.
echo === Backend tests ===
call dotnet test --configuration Release || goto failed

:build
echo.
echo === Frontend: install, test, build into wwwroot ===
pushd web
call npm ci || (popd & goto failed)
if not defined SKIP_TESTS (
  call npm test || (popd & goto failed)
)
call npm run build || (popd & goto failed)
popd

echo.
echo === dotnet publish (%ENV_NAME%, self-contained %SELF_CONTAINED%) ===
if exist publish rmdir /s /q publish
call dotnet publish src\TowerHaat.Api --configuration Release --output publish --runtime win-x64 --self-contained %SELF_CONTAINED% -p:EnvironmentName=%ENV_NAME% || goto failed

rem AppOffline stops the site while files are copied; DoNotDeleteRule and the App_Data skip keep
rem uploaded photos, logs, backups and the settings file on the server.
echo.
echo === Web Deploy ===
"%MSDEPLOY%" -verb:sync ^
  "-source:contentPath=%CD%\publish" ^
  "-dest:contentPath=%WEBDEPLOY_SITE%,computerName=%WEBDEPLOY_URL%,userName=%WEBDEPLOY_USERNAME%,password=%WEBDEPLOY_PASSWORD%,authType=Basic" ^
  -allowUntrusted ^
  -enableRule:AppOffline ^
  -enableRule:DoNotDeleteRule ^
  "-skip:Directory=\\App_Data" ^
  "-skip:objectName=filePath,absolutePath=appsettings\.(Production|Staging)\.json$" || goto failed

echo.
echo === Checking %SITE_URL%/api/health ===
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "for ($i = 1; $i -le 12; $i++) { try { $h = Invoke-RestMethod '%SITE_URL%/api/health' -TimeoutSec 20; if ($h.status -eq 'ok') { Write-Host ('Healthy: ' + $h.utcNow); exit 0 } } catch { Write-Host ('Not up yet: ' + $_.Exception.Message) }; Start-Sleep -Seconds 10 }; exit 1" ^
  || (echo The site did not answer /api/health. Check Admin - Error log or the hosting panel. & exit /b 1)

echo.
echo Deployed to %TARGET%.
exit /b 0

:failed
echo.
echo Deploy stopped: the step above failed.
exit /b 1
