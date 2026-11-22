# Operador de la feria

App local (Node puro, sin `npm install`) que lee el cronómetro del juego por `adb logcat`, arma el ranking y muestra la zona pública.

## Uso
1. Conectá el celular por USB (o por wifi, ver `docs/feria-transmision.md`) con la depuración USB activa.
2. Doble clic en `iniciar-operador.bat` (abre las dos páginas). A mano: `node server.js [--port 8080] [--adb <ruta>]`.
3. **Zona pública** (proyector): `http://localhost:8080/`. **Operador**: `http://localhost:8080/operador`.
4. Escribí el nombre del próximo jugador y apretá "Guardar nombre" antes de que empiece.

Sin celular, para probar: `node server.js --simular` (una partida falsa de ~2 min).

## Datos
- Ranking: `datos/ranking.json` (se guarda solo; "Reiniciar" hace backup `ranking-<fecha>.json` en la misma carpeta). Las partidas interrumpidas no entran.
- `datos/config.json`: último nombre guardado.

## Protocolo
El juego imprime líneas `[CRONO] ...` por logcat (start, scene, split, loadstart, loadend, hint, end, abort, name, cmd, tick) y escucha comandos (`ping`, `hint`, `skip`, `menu`, `name`, `status`) en TCP 7777, que el servidor alcanza con `adb forward tcp:7777 tcp:7777`. Si adb o el socket se cortan, reintenta cada 3 s; el estado se ve en la página del operador.

Además de lo anterior, el juego manda `[CRONO] perf <fps> <1%bajo> <msPeor> <escena>` cada 5 s y acepta `goto <escena>`, `restart`, `fair on|off`, `portals visible|subtle` y `fps 30|60`; `status` devuelve `status <activa> <tiempo> fps=.. target=.. fair=.. portals=.. <escena> obj=<objetivo> hint=<n>` (`obj` y `hint` se leen en cualquier posición; si faltan, el panel muestra "—").

Comandos nuevos del visor (validados en `server.js`, lista blanca `CMDS_OK`):
- `scare screamer|whisper|steps|distant|knock|blackout|heartbeat`: sustos. El juego tiene un enfriamiento de 1,5 s.
- `tension <0..1>`: nivel de tensión del audio (punto decimal).
- `sfx <clip>`: reproduce un sonido de la lista fija de `SFX` en `server.js` (susurros, pasos, golpes, crujidos, rugido, latidos, etc.).
- `hintlvl 1|2|3`: pista suave, media o directa del objetivo actual.
- `say <texto>`: muestra un texto en el visor. Se limpia (sin saltos de línea, espacios colapsados), máximo 120 caracteres, solo caracteres imprimibles (acentos y ¿¡ incluidos).

## Panel del operador
Ir a nivel / reiniciar, abrir y cerrar el juego (adb), opciones en vivo (modo feria, portales, 30/60 FPS), rendimiento (FPS, 1% bajo y tabla por escena) y estado de adb, socket y proceso del juego.
Además: **Pistas** (objetivo y nivel de pista leídos del juego, botones Suave/Media/Directa, "Dar pista" y un campo de texto para "Mostrar en el visor"), **Sustos** (un botón por tipo y un control de tensión 0 a 1) y **Sonidos** (una grilla con cada clip). Cada acción escribe su respuesta del juego al final del registro.
- Si pasan 12 s sin líneas `[CRONO]` la partida se congela ("juego desconectado"; el público ve "en pausa"). Cada 5 s se consulta `adb shell pidof com.omarcorrea.proyectointegrador`: si no hay proceso, la partida se corta y no entra al ranking. Si el juego vuelve, se descongela sola.
- `goto` marca la partida con salto (no entra al ranking); `restart` no.
- API: `POST /api/cmd {cmd,arg}`, `/api/app/abrir`, `/api/app/cerrar`, `/api/perf/borrar`. Con `--simular` también `/api/sim/{colgar,reanudar,cerrar,abrir}` para probar fallos. Para probar sin tocar el servidor real: `node server.js --simular --port 8091`.

## Fuente
`public/fonts/` tiene Special Elite (licencia Apache 2.0 incluida), así la pantalla anda sin internet.
