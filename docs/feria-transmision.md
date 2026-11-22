# Transmisión y operación en la feria

Cómo mostrar lo que ve el jugador en el proyector, cómo operar la partida y qué revisar antes de la feria.

## 1. Mostrar un solo ojo con scrcpy

El juego en Cardboard dibuja dos ojos lado a lado. Para el público conviene mostrar solo uno.

1. Instalá [scrcpy](https://github.com/Genymobile/scrcpy) y poné la carpeta en el PATH (o en `tools/operador/scrcpy/`).
2. Con el celular conectado y la depuración USB activa, ejecutá `tools/operador/scrcpy-un-ojo.bat`.
3. Lo que hace: `scrcpy --crop=1080:1170:0:0 --no-audio --max-fps 30 --window-title "VR-ojo-izquierdo"`.
   - El A54 en horizontal es 2340x1080; la mitad izquierda es `1170:1080:0:0` (ancho:alto:x:y).
   - Si el celular está en otra resolución, ajustá el ancho a la mitad.
   - `--no-audio` evita doble sonido (el jugador usa auriculares); `--max-fps 30` baja la carga para no pesar sobre el juego.
4. Ojo: scrcpy no debería usarse mientras se mide rendimiento fino; consume algo de CPU del celular.

## 2. Escena de OBS

**Ya configurado en la notebook de Omar (2026-11-07):** OBS 33 instalado con winget, perfil y colección de escenas "Feria VR" (1920x1080, 30 fps) en `%APPDATA%\obs-studio\basic\`. Tiene 3 escenas:
- "Feria - ojo + ranking": scrcpy a la izquierda y la zona pública a la derecha, cada uno de 960x1080.
- "Solo el juego".
- "Solo ranking".

La captura busca la ventana por el título `VR-ojo-izquierdo`, así que hay que abrir primero `scrcpy-un-ojo.bat` y `iniciar-operador.bat`. Para el proyector: clic derecho en la vista previa → *Proyector en pantalla completa* → la pantalla del proyector.

Si hay que armarlo a mano en otra PC:

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

**Atajo:** `tools/operador/conectar-wifi.bat <ip-del-celular>` hace los pasos de "con cable" solo.

**Probado el 2026-11-12 en el A54** (red de la casa, 2,4/5 GHz sin dedicar):
- Comando `ping` → `pong` y `status` por el 7777.
- scrcpy de un ojo a ~28 de 30 FPS.

Notas:
- El A54 ya quedó vinculado por Depuración inalámbrica: bastó con `adb connect <ip>:<puerto>` sin volver a hacer `pair`.
- El cable USB que estaba enchufado no aparecía en Windows (probablemente solo carga). Para `adb tcpip` por cable hace falta un cable de datos, pero no es imprescindible: con la Depuración inalámbrica conectada se puede correr `adb -s <ip>:<puerto> tcpip 5555` y queda fijo en el 5555.
- El modo 5555 se pierde al reiniciar el celular.

### Punto de acceso desde la notebook (probado el 2026-11-12)
En vez de usar otro Wi-Fi, la notebook puede crear su propia red: **Configuración → Red e Internet → Zona con cobertura inalámbrica móvil**. La red se llama `ROG-OMAR`, conviene elegir la banda de 5 GHz y la contraseña aparece en esa misma pantalla.
- El celular recibe una IP `192.168.137.x`. La notebook es `192.168.137.1`.
- Para conectar: Depuración inalámbrica → `adb connect <ip>:<puerto>` → `adb -s <ip>:<puerto> tcpip 5555` → `conectar-wifi.bat <ip>`.
- Resultado de la prueba: socket `ping`/`status` OK y scrcpy de un ojo a **30 de 30 FPS**, mejor que con la red de la casa.
- El ping del celular a la notebook no responde porque el firewall de Windows bloquea ICMP. No afecta a adb.
- **Falta probar** si Windows deja prender la zona con la notebook **sin internet**, que es como va a estar en la feria. Si no deja, usar el router propio de la sección 3.

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

## 7. Panel del operador (`/operador`): control en vivo

Además del nombre, las pistas y el ranking, el panel controla el juego:

- **Ir a nivel / Reiniciar nivel:** un botón por escena (más el Menú). Saltar de nivel marca la partida "con salto": no entra al ranking. Reiniciar el nivel no la marca. Pide confirmación.
- **Juego (abrir / cerrar):** abre la app por adb (`monkey`) o la fuerza a cerrar (`am force-stop`). Cerrar corta la partida activa; no entra al ranking.
- **Opciones en vivo:** modo feria sí/no, portales visibles/sutiles y 30/60 FPS. El botón resaltado es el valor que el juego informa (se consulta cada 5 s).
- **Rendimiento:** FPS actual (verde desde 55, amarillo desde 28, rojo debajo), 1 % bajo, peor frame y tabla por escena de la sesión. "Borrar tabla" la reinicia (conviene después del ensayo).
- **Pistas:** objetivo actual y nivel de pista (se leen del juego cada 5 s). Suave, Media y Directa piden la pista de ese nivel; "Dar pista" avanza un escalón. Abajo, un campo de texto (máx. 120) con "Mostrar en el visor": Enter también lo envía y el campo se vacía si el juego respondió bien.
- **Sustos:** un botón por tipo (Screamer, Susurro, Pasos, Ruido lejano, Golpes, Apagón, Latidos). El Screamer es el rojo. Después de cada susto los botones se apagan 1,5 s. El control de **Tensión** (0 a 1, de a 0,1) se envía al soltarlo.
- **Sonidos:** una grilla con cada clip del juego para probar el audio en vivo.
- **Estado:** luces de adb, socket y proceso del juego.
- El registro de abajo muestra, al final, la respuesta del juego a cada acción del operador.

**Si el juego se cuelga o se cierra a mitad de partida:** a los 12 s sin señal del juego el cronómetro se congela (el panel avisa "juego desconectado" y el proyector muestra "en pausa"). Si el proceso ya no existe (se chequea cada 5 s con `adb shell pidof`), la partida se corta como interrumpida y no entra al ranking. Si el juego vuelve a hablar mientras está congelado, el cronómetro sigue solo.

Checklist extra: probar "Ir a nivel", abrir y cerrar el juego, y cerrar la app a mano para ver el aviso.
