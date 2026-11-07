@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
set "ADBEXE="
where adb >nul 2>nul && set "ADBEXE=adb"
if not defined ADBEXE if exist "%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe" set "ADBEXE=%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe"
if not defined ADBEXE (
  echo No encontre adb. Instala platform-tools o ponelo en el PATH.
  echo El operador arranca igual, pero sin conexion con el celular.
  set "ADBEXE=adb"
)
echo Usando adb: %ADBEXE%
start "" "http://localhost:8080/operador"
start "" "http://localhost:8080/"
node server.js --port 8080 --adb "%ADBEXE%" %*
pause
