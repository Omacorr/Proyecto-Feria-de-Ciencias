@echo off
rem Muestra solo el ojo izquierdo del A54 (horizontal, 2340x1080) en una ventana.
rem scrcpy 5 toma el recorte con el celular en VERTICAL (1080x2340): ALTO:ANCHO/2:0:0 da el ojo izquierdo en horizontal.
cd /d "%~dp0"
set "SC=scrcpy"
where scrcpy >nul 2>nul || (
  if exist "%~dp0scrcpy\scrcpy.exe" (set "SC=%~dp0scrcpy\scrcpy.exe") else (echo No encontre scrcpy. Descargalo de https://github.com/Genymobile/scrcpy & pause & exit /b 1)
)
"%SC%" --crop=1080:1170:0:0 --no-audio --max-fps 30 --window-title "VR-ojo-izquierdo" %*
