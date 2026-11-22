# Cómo conectar el celular a la compu por wifi (adb)

Son siempre los mismos dos pasos: prender la depuración en el celular y conectarse desde la compu.

## 1. En el celular

1. Andá a **Ajustes → Opciones de desarrollador → Depuración inalámbrica** y prendela.
2. Adentro aparece la **dirección IP y el puerto**, por ejemplo `192.168.137.245:36263`. El puerto cambia cada vez que la prendés.
3. El celular y la compu tienen que estar en la **misma red**: la zona con cobertura de la notebook o el wifi de casa.

## 2. En la compu (PowerShell)

Pararte en la carpeta de scrcpy:

```powershell
cd "C:\Users\cutuc\PROYECTO INTEGRADOR\tools\operador\scrcpy"
```

Conectarte con la IP y el puerto que muestra el celular:

```powershell
.\adb.exe connect 192.168.137.245:36263
```

> **Ojo con el `.\` adelante.** En PowerShell, para correr un programa que está en la carpeta donde estás hay que escribir `.\nombre.exe`. Si ponés `connect` solo, PowerShell busca un comando con ese nombre y tira error.

Verificar que aparezca como `device`:

```powershell
.\adb.exe devices
```

## 3. Pasar a un puerto fijo (opcional, recomendado)

Así no tenés que mirar el puerto cada vez. Dura hasta que se reinicie el celular.

```powershell
.\adb.exe tcpip 5555
.\adb.exe connect 192.168.137.245:5555
```

## 4. Para el panel del operador

Este paso redirige el puerto por el que el panel le manda los comandos al juego:

```powershell
.\adb.exe forward tcp:7777 tcp:7777
```

## Si algo falla

| Problema | Qué hacer |
|---|---|
| Aparece `offline` o la conexión se queda colgada | `.\adb.exe disconnect` y volvé a conectar. |
| `failed to authenticate` o el celular pide vincular | En Depuración inalámbrica tocá **"Vincular dispositivo con código"** y corré `.\adb.exe pair IP:PUERTO_DE_VINCULACION` con el puerto y el código que muestra esa pantalla. Después hacé el `connect` normal. |
| Cambió la IP | Pasa si cambiás de red. Copiá siempre la que muestra el celular. |
| Se reinició el celular | El puerto 5555 se pierde: volvé a empezar desde el paso 1. |
| `actively refused` (el puerto ya no sirve) | Corré `.\adb.exe mdns services`. En la línea `adb-R5CW..._adb-tls-connect` aparece el **puerto actual** (la IP de esa línea puede estar vieja). Conectate con la IP de la zona (`192.168.137.245`) y ese puerto. |

## Atajo

`conectar-wifi.bat`, en esta misma carpeta, hace los pasos 2 a 4 solo.
