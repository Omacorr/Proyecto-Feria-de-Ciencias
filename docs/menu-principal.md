# Escena "Menu Principal"

## Cómo generarla (un solo paso)

1. Abrí el proyecto en Unity y esperá a que compile (no debe haber errores en la
   consola).
2. Menú **`Tools > Feria de Ciencias > Generar escena 'Menu Principal'`**.
3. Listo: se crea `Assets/Scenes/Menu Principal.unity` con todo armado y
   cableado, y queda agregada a Build Settings en el índice 0 (la app arranca
   ahí). `Tutorial` también queda incluida.
4. Abrí la escena y dale **Play**. Mirá cada botón ~2 s.

Se puede volver a correr cuando quieras; sobrescribe la escena entera. Si movés
cosas a mano después, no la regeneres o perdés esos ajustes.

## Fondo (habitación VR + ambiente de terror)

El generador también arma el clima: sin skybox, **niebla y ambiente casi
negros**, una **luz puntual parpadeante** (`FlickeringLight`) que ilumina el
menú y lo cercano, una direccional muy tenue para dar forma, y un `AudioSource`
en loop con el sonido ambiente. El menú usa materiales Unlit, así que se lee bien
aunque esté casi todo oscuro.

Ya está apuntado a los archivos reales del proyecto:

```csharp
private const string RoomModelPath   = "Assets/Modelos 3D/horror_room.glb";
private const string AmbientAudioPath = "Assets/Audios/AmbienteMenuPrincipal.flac";
```

Si un archivo no existe, esa parte se saltea con un warning (no rompe).

El generador **detecta el piso** de la habitación (punto más bajo de sus bounds)
y sube el rig para que la cámara quede `EyeHeight` (1,6 m) por encima de ese
piso. En consola loguea `Piso detectado en Y=... -> camara a Y=...`. Si aún
aparecés pegado al piso o flotando, poné `FloorYOverride` (Y del piso a mano) o
tocá `EyeHeight` y volvé a generar. También fuerza el `TrackedPoseDriver` de la
cámara a **Rotation Only** (Cardboard es 3DOF; si no, la cámara se va al origen).

El menú va a `MenuDistance` (2,5 m) de la cámara, **justo delante de la puerta**
(`DoorDistance` = 3,0 m). La puerta apoya en el piso detectado.

El menú es **muy transparente**: sin fondo sólido, sólo un **halo difuso**
(textura radial suave `MenuHaze.asset` + tinte `ColPanelHalo`, alpha ~0,34)
detrás del texto — no es un rectángulo duro pero igual se distingue del cuarto.
Los **botones** son translúcidos (`ColBotonIdle` alpha 0,55) y los **textos
quedan sólidos** con `renderQueue` alta para leerse siempre por encima.

## Secuencia de entrada (puerta)

El generador junta **todas las mallas de `horror_room.glb` cuyo nombre tiene
"door"/"puerta"** (o `DoorNameHint` = "door low"), excluyendo marco/frame/jamba,
y las **reparenta bajo un pivote `PuertaMenu`** con bisagra en un borde de la
caja envolvente combinada. `MainMenuController._door` apunta a ese `PuertaMenu`,
así que al abrir **giran todas las partes juntas** ~100° alrededor de la
vertical. La consola loguea qué nodos encontró y cuántas partes agrupó. Si NO
encuentra ninguna, cae en una puerta propia a `DoorDistance`.

El menú va **entre la cámara y `PuertaMenu`**, pegado a la puerta.

El jugador **aparece a `PlayerStandDistance` (2,6 m) delante de la puerta,
mirándola** (se rota la raíz del Player), con el menú a `MenuGapFromDoor` (0,7 m)
de la puerta. Se crea un `PuertaCentro` en el centro de la puerta como objetivo
del avance.

Si la puerta abre para el lado equivocado: flag `DoorHingeAtFarEdge` en el
generador, o invertí `_doorOpenAngle` (ej. `-100`) en `MenuManager`.

Al mirar **EMPEZAR**, `MainMenuController`:

1. apaga los paneles y desactiva `PlayerFollowsCamera`,
2. **abre la puerta**: gira `PuertaMenu` `_doorOpenAngle` (~100°) alrededor de la
   vertical que pasa por su origen — la bisagra — (`_doorOpenDuration` 1,4 s),
3. **avanza el rig** en línea recta hacia `PuertaCentro` y `_extraWalkPastDoor`
   (1,5 m) más, cruzando la puerta (`_walkDuration` 2 s),
4. **funde a negro** y muestra la **pantalla de carga** con el nombre del nivel
   grande (`_levelDisplayName` = **TUTORIAL**) y `cargando...` chico debajo, y
   hace `LoadSceneAsync` (`allowSceneActivation = false`),
5. espera `_minLoadSeconds` (3 s) **y** a que la escena esté lista, y ahí
   activa → aparecés en el Tutorial.

Todo eso se cablea solo (`_door` = pivote de la hoja, `_cameraRig` = raíz del
Player, `_loadingText` = "PantallaCarga"). Si querés usar otra puerta (la del
`.glb`), arrastrá su transform a `_door` en `MenuManager` y ajustá los euler. Si
dejás `_door` / `_cameraRig` vacíos, EMPEZAR carga directo con fundido.

## Locución del Tutorial (voz explicando el nivel)

`Assets/Scripts/Ambient/SceneNarration.cs`: componente que reproduce un clip de
voz **una vez** al empezar la escena (con delay configurable y subtítulo TMP
opcional). Sirve para cualquier escena.

Para el Tutorial:

1. Grabá la voz (celular / web de texto-a-voz) y guardala como
   **`Assets/Audios/TutorialLocucion.wav`** (o `.mp3` / `.ogg` / `.flac`).
2. Menú **`Tools > Feria de Ciencias > Agregar locucion al Tutorial`**: crea el
   objeto **"Locucion"** (AudioSource + SceneNarration) en `Tutorial.unity` y le
   asigna el clip. Es idempotente (re-corrélo cuando cambies el audio).
3. Opcional: en el Inspector de "Locucion", asignar un TMP a `Subtitle Label` y
   escribir el texto en `Subtitle Text` para que se vea mientras habla.

Ajustes finos (constantes al principio del generador): `FogDensity`,
`AmbientColor`, `FillLightIntensity`, `FlickerLightIntensity`, `FlickerLightRange`,
`MenuDistance`, `DoorDistance`, `Col*`. En la escena podés mover **"Habitacion"**
y **"Puerta"** a mano.

## Qué genera

```
Menu Principal
├── Habitacion            (horror_room.glb, en el origen)
├── Luz Relleno (tenue) / Luz Parpadeante (FlickeringLight)
├── Ambiente              (AudioSource loop, AmbienteMenuPrincipal.flac)
├── Player                (instancia del prefab: cámara a 1,6 m sobre el origen)
├── GameSettings
├── MenuManager           (MainMenuController)
├── Puerta                (jambas + dintel + Pivote/Hoja) a 2,9 m
└── MenuRoot              (semitransparente, a 1,8 m, delante de la puerta)
    ├── PanelPrincipal    → EMPEZAR / CONFIGURACIÓN / CRÉDITOS
    ├── PanelConfig        (arranca oculto) → 2×(Bajo/Medio/Alto) + VOLVER + carteles
    └── PanelCreditos      (arranca oculto) → "Pablo Prato / Omar Correa" + VOLVER
```

- **EMPEZAR** → abre la puerta, cruza, funde a negro y carga `Tutorial` (ver
  "Secuencia de entrada").
- **CONFIGURACIÓN** → panel con presets **Bajo / Medio / Alto** para:
  - *Velocidad del gaze* (3 s / 2 s / 1,2 s de mirada sostenida)
  - *Tamaño del retículo* (×0,7 / ×1 / ×1,5)
  - El cambio se guarda en `PlayerPrefs` y se aplica al instante (se ve el
    retículo cambiar de tamaño en el mismo menú).
- **CRÉDITOS** → "Pablo Prato / Omar Correa" + VOLVER.

Los botones son `InteractiveObject` (Quad + BoxCollider en la layer
`Interactive` + `OnSelected` cableado). El generador también se asegura de que la
layer `Interactive` esté en el `Interactive Layer Mask` del `GazeController`.

## Cómo funciona la configuración en el resto del juego

No hay que tocar ninguna escena de gameplay. `GameSettings`:

- Se crea solo al arrancar el juego (`RuntimeInitializeOnLoadMethod`) y persiste
  entre escenas (`DontDestroyOnLoad`).
- Cada vez que se carga una escena (evento `sceneLoaded`) y cada vez que cambia
  un preset, busca el `GazeController` / `GazeReticle` de esa escena y les aplica
  los valores. Si la escena no los tiene, no hace nada.

Presets y valores concretos: en `Assets/Scripts/Settings/GameSettings.cs`
(`GazeSelectSecondsByLevel` y `ReticleScaleByLevel`).

## Scripts

| Archivo | Rol |
|---|---|
| `Assets/Scripts/Settings/GameSettings.cs` | Presets persistentes + auto-aplicación por escena |
| `Assets/Scripts/Menu/MainMenuController.cs` | Navegación de paneles + carga de escena |
| `Assets/Scripts/Menu/ConfigMenu.cs` | Los 6 botones de preset + carteles de estado |
| `Assets/Editor/MenuPrincipalGenerator.cs` | El generador (menú `Tools > Feria de Ciencias`). No entra en la build. |

Cambios menores en scripts existentes: `GazeController.SetSelectDuration(float)`
y `GazeReticle.SetSizeMultiplier(float)` (retrocompatibles).

## Sonidos del menú

- `InteractiveObject` ahora tiene `_hoverSound` (al posar la mirada) y
  `_selectSound` (al activar), que suenan por un `AudioSource` compartido.
- El generador crea `MenuAudio` (AudioSource 2D) y `Assets/Audios/MenuHover.wav`
  / `MenuSelect.wav` (generados por código), y los cablea en cada botón.
- Tamaño de todas las letras: constante `TextScale` (1.3) en el generador.

## Preparar el Tutorial (rig + teleports + llave + puerta)

`Tools > Feria de Ciencias > Preparar Tutorial` (`Assets/Editor/TutorialSetup.cs`):

- Agrega el rig de VR (prefab `Player`) si la escena no lo tiene, ubicándolo en
  la Main Camera vieja y borrándola.
- Convierte cada objeto `Teletransportadores*` en `TeleportPoint` funcional
  (Collider + layer `Interactive` + ref al `TeleportManager`).
- Asegura `KeyInventory`; si no hay llave, crea `LlaveTutorial` (usa
  `Assets/Models/keys.glb` si existe) con `Collectable` cableado a `CollectKey`.
- Crea/usa la **puerta de salida** con `LevelDoor`
  (`Assets/Scripts/Interaction/LevelDoor.cs`): al mirarla (con la llave) funde a
  negro, muestra **"PARTE 1 / HOSPITAL"** y carga `Parte 1 - El Despertar`.
- Sonido relajante en loop: poné `Assets/Audios/TutorialAmbiente.wav`
  (o `.mp3/.ogg/.flac`) y (re)corré el menú.
- Habilita `Parte 1 - El Despertar` en Build Settings.

Es idempotente y loguea todo. Después ajustá a mano la posición de
`LlaveTutorial` / `PuertaSalida` si las creó el tool. La locución la sigue
poniendo `Tools > Feria de Ciencias > Agregar locucion al Tutorial`.

## Notas

- La escena `Tutorial` se prepara con el tool de arriba.
- Si el prefab `Player` no tuviera `VRFadeController`, "EMPEZAR" corta seco sin
  fundido (el generador lo avisa en consola).
- Si algún texto o botón queda mal de posición en el visor, es sólo moverlo en
  la escena; la lógica ya está cableada.
