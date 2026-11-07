# Transmisión y operación en la feria

Cómo mostrar lo que ve el jugador en el proyector, cómo operar la partida y qué revisar antes de la feria.

## 1. Mostrar un solo ojo con scrcpy

El juego en Cardboard dibuja dos ojos lado a lado. Para el público conviene mostrar solo uno.

1. Instalá [scrcpy](https://github.com/Genymobile/scrcpy) y poné la carpeta en el PATH (o en `tools/operador/scrcpy/`).
2. Con el celular conectado y la depuración USB activa, ejecutá `tools/operador/scrcpy-un-ojo.bat`.
3. Lo que hace: `scrcpy --crop=1170:1080:0:0 --no-audio --max-fps 30 --window-title "VR-ojo-izquierdo"`.
   - El A54 en horizontal es 2340x1080; la mitad izquierda es `1170:1080:0:0` (ancho:alto:x:y).
   - Si el celular está en otra resolución, ajustá el ancho a la mitad.
   - `--no-audio` evita doble sonido (el jugador usa auriculares); `--max-fps 30` baja la carga para no pesar sobre el juego.
4. Ojo: scrcpy no debería usarse mientras se mide rendimiento fino; consume algo de CPU del celular.

## 2. Escena de OBS

1. Fuente 1: **Captura de ventana** → ventana `VR-ojo-izquierdo` (scrcpy). Recortá/escalá a 16:9 con el ojo centrado.
2. Fuente 2: **Navegador** → `http://localhost:8080/` (zona pública), ancho 1920 y alto 1080, usado como panel lateral o pantalla aparte.
3. Si usás un solo proyector, armá una escena con el ojo a la izquierda y el navegador a la derecha (crono, top 10). Si hay dos pantallas, mandá la zona pública a una y scrcpy a la otra con proyector de pantalla completa.
4. La página del **operador** (`/operador`) queda en el monitor de la notebook, no en el proyector.

## 3. Red: router de 5 GHz dedicado

Para transmitir por wifi sin depender de la red de la feria:

- Llevá un router propio y configuralo en banda de **5 GHz**, con un canal fijo y sin conexión a internet requerida.
- Conectá notebook y celular solo a ese router. Poné el celular con wifi y **modo avión** (así no entran llamadas ni notificaciones, pero el wifi sigue andando).
- Probá el alcance con el visor puesto y el celular en el bolsillo o la cabeza del jugador.

## 4. adb inalámbrico

### Con cable de partida (cualquier Android)
1. Celular por USB: `adb tcpip 5555`.
2. Averiguá la IP del celular (Ajustes > Wi-Fi) y ejecutá `adb connect <ip>:5555`.
3. Desenchufá el cable. `adb devices` tiene que mostrarlo. Si se corta, repetí `adb connect`.

### Android 11 o más nuevo, sin cable
1. Ajustes > Opciones de desarrollador > **Depuración inalámbrica** activada.
2. "Vincular dispositivo con código": anotá IP, puerto y código de 6 dígitos.
3. `adb pair <ip>:<puerto-de-emparejamiento>` e ingresá el código.
4. `adb connect <ip>:<puerto-de-depuración>` (el puerto principal que se ve en la pantalla de Depuración inalámbrica, distinto al de emparejamiento).

Con adb conectado por wifi, el operador funciona igual: `adb logcat` y `adb forward tcp:7777 tcp:7777` van por la misma conexión.

## 5. Plan B: USB

Si el wifi falla o hay mucha interferencia: cable USB corto de buena calidad al celular, conectado a la notebook. Sirve para todo (logcat, scrcpy y el socket). Desventaja: el jugador queda atado; usá un cable largo (2 a 3 m) y alguien que lo acompañe. Para pasar de wifi a USB: `adb disconnect` y enchufar; `adb devices` tiene que mostrar el serial.

## 6. Checklist del ensayo (el día anterior y el día de la feria)

- [ ] Celular cargado al 100 % y cargador portátil a mano.
- [ ] Auriculares probados (con volumen cómodo, no sorpresivo para un chico).
- [ ] Brillo del celular al máximo o a un valor fijo que se vea bien en el visor.
- [ ] Modo avión **con wifi** prendido; no molestar; bloqueo de pantalla sin timeout.
- [ ] Build Settings con todas las escenas tildadas (menos `HelloCardboard`) y APK nuevo instalado.
- [ ] `iniciar-operador.bat`: adb en verde y socket en verde en `/operador`.
- [ ] Partida completa de prueba: el cronómetro anda, los parciales aparecen y el "FIN" sale en la zona pública.
- [ ] Probar los botones: dar pista, saltar parte, volver al menú.
- [ ] Probar el corte de red: desconectar y reconectar; el estado vuelve a verde solo.
- [ ] scrcpy muestra solo el ojo izquierdo y OBS tiene las dos fuentes.
- [ ] **Borrar el ranking de prueba** (botón "Reiniciar ranking"; deja un backup).
- [ ] Copia de `tools/operador/datos/` al final del día.
