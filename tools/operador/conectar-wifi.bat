@echo off
rem Conecta el celular por Wi-Fi (adb en el puerto 5555). Celular y laptop en la MISMA red.
rem Uso: conectar-wifi.bat 192.168.0.185      (la IP del celular: Ajustes > Acerca del telefono > Estado, o Depuracion inalambrica)
rem Si el celular esta enchufado por USB (cable de datos), primero lo pasa a modo Wi-Fi.
rem Ojo: el modo 5555 se pierde al reiniciar el celular; hay que volver a correr esto con USB,
rem o vincular con "Depuracion inalambrica" (adb pair / adb connect IP:puerto que muestra el celular).
cd /d "%~dp0"
set "ADB=adb"
if exist "%~dp0scrcpy\adb.exe" set "ADB=%~dp0scrcpy\adb.exe"
if "%~1"=="" (
  set /p IP=IP del celular:
) else (
  set "IP=%~1"
)
"%ADB%" -d tcpip 5555 >nul 2>nul && timeout /t 3 >nul
"%ADB%" connect %IP%:5555
"%ADB%" devices
echo.
echo Si dice "device" ya esta. Despues abri iniciar-operador.bat y scrcpy-un-ojo.bat.
echo Desenchufa el USB si queres: la conexion sigue por Wi-Fi.
pause
