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

## Fuente
`public/fonts/` tiene Special Elite (licencia Apache 2.0 incluida), así la pantalla anda sin internet.
