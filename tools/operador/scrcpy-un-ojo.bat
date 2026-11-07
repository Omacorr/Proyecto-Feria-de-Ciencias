@echo off
rem Muestra solo el ojo izquierdo del A54 (horizontal, 2340x1080) en una ventana.
rem Si el celular esta en otra resolucion, ajusta --crop=ANCHO:ALTO:X:Y (ancho/2 : alto : 0 : 0).
cd /d "%~dp0"
set "SC=scrcpy"
where scrcpy >nul 2>nul || (
  if exist "%~dp0scrcpy\scrcpy.exe" (set "SC=%~dp0scrcpy\scrcpy.exe") else (echo No encontre scrcpy. Descargalo de https://github.com/Genymobile/scrcpy & pause & exit /b 1)
)
"%SC%" --crop=1170:1080:0:0 --no-audio --max-fps 30 --window-title "VR-ojo-izquierdo" %*
