# PROYECTO INTEGRADOR — VR Cardboard (Feria de Ciencias)

Este archivo es el punto de partida para cualquier IA (o persona) que retome este
proyecto. Resume qué es, cómo está armado, qué se implementó y por qué, y qué
falta. Los scripts en sí también tienen comentarios XML/tooltips extensos en
español explicando el "por qué" de cada decisión — este documento es el mapa
general; para el detalle de una mecánica puntual, siempre conviene leer el
script correspondiente.

## Qué es

Juego de realidad virtual para **Google Cardboard**, hecho como proyecto de
Feria de Ciencias por Omar (usuario `cutuc`), pensado para probarse en un
Samsung A54 con un visor Cardboard de cartón. Es un juego de escape/puzzles en
primera persona: el jugador se mueve por teletransporte (mirando un punto y
sosteniendo la mirada) por un hospital abandonado, resuelve acertijos (por
ahora, un candado de combinación) y avanza por la historia.

El proyecto está pensado como **varias escenas ("Parte 1", "Parte 2", ...)**,
pero por ahora todo el desarrollo real está concentrado en la primera:
`Assets/Scenes/Parte 1 - El Despertar.unity`.

## Entorno técnico

- **Unity 6000.3.22f1**, build target Android.
- **Google Cardboard XR Plugin** (com.google.xr.cardboard), integrado vía el
  paquete de XR Plugin Management / XR Interaction. El proyecto **partió del
  sample "Hello Cardboard"** de ese paquete — varios bugs de este proyecto
  fueron en realidad restos de ese sample que quedaron pegados sin querer
  (ver sección de gotchas más abajo).
- **URP** (Universal Render Pipeline).
- **glTFast** para importar modelos `.glb`/`.gltf` (el candado, las notas, los
  números de madera). Los modelos del hospital (`the_hospital_4_of_4`) también
  son un import de este tipo.
- **Input System** (paquete nuevo de Unity), usado por el `TrackedPoseDriver`
  que lee la pose del casco.
- El proyecto vive en `C:\Users\cutuc\PROYECTO INTEGRADOR` en la máquina de
  Omar ("rog-omar"). El repo remoto es
  `https://github.com/Omacorr/Proyecto-Feria-de-Ciencias.git`.
- **Importante para una IA que retome esto sin acceso directo al Editor de
  Unity:** todo el trabajo de Inspector (arrastrar referencias, crear
  GameObjects, tildar checkboxes, ajustar Transforms a ojo) lo tiene que hacer
  Omar a mano siguiendo instrucciones paso a paso — no hay forma de controlar
  el Editor de Unity de forma remota. Los cambios de código sí se pueden
  escribir y dejar guardados directamente en los archivos `.cs`. Para
  diagnosticar bugs de configuración de la escena, es válido (y se usó mucho)
  leer directamente el archivo `.unity` (es YAML) en vez de confiar en
  capturas de pantalla o descripciones — a veces revela la verdad cuando el
  usuario y la IA no se ponen de acuerdo sobre qué está pasando.

## Arquitectura: sistema de mirada (gaze)

Es el corazón de toda la interacción, porque no hay controles físicos (es
Cardboard, sin gatillo/mando): todo se hace **mirando un objeto y sosteniendo
la mirada 2 segundos**.

- **`GazeController`** (`Scripts/Gaze/GazeController.cs`): en su `Update()`
  tira un `Physics.Raycast` desde la cámara (o `Camera.main` si no se le
  asigna una) hacia adelante, filtrado por un `LayerMask` (`_interactiveLayerMask`,
  normalmente los layers `Interactive`, bits configurados en el Inspector —
  actualmente incluye los layers 7 y 9). Cuando el objeto mirado cambia, busca
  con `GetComponentInParent<IGazeInteractable>()` el primer componente que
  implemente esa interfaz y le dispara `OnGazeEnter`/`OnGazeExit`. Mientras se
  sigue mirando el mismo objeto, cuenta el tiempo (`_gazeSelectDuration`,
  2 segundos por defecto) y dispara `OnGazeStay(progress)` cada frame; al
  llegar a 1.0 dispara `OnGazeSelect()` y **reinicia el timer**, así que si se
  sigue mirando el mismo objeto, vuelve a dispararse `OnGazeSelect()` cada 2
  segundos (cada script decide si eso le importa o no).
- **`IGazeInteractable`** (`Scripts/Interaction/IGazeInteractable.cs`): la
  interfaz que implementa todo objeto que reacciona a la mirada. Solo 4
  métodos: `OnGazeEnter`, `OnGazeStay(float progress)`, `OnGazeExit`,
  `OnGazeSelect`. `GazeController` no sabe nada de lo que hace cada objeto —
  solo despacha estos eventos.
- **Requisito para que un objeto sea "mirable":** tiene que tener un
  `Collider` y estar en un layer incluido en el `_interactiveLayerMask` del
  `GazeController` (normalmente el layer `Interactive`). **Ojo:** ni glTFast
  ni FBX generan Colliders automáticamente al importar (a menos que se tilde
  "Generate Colliders" en el import de FBX, cosa que no se hizo acá) — hay
  que agregarlos a mano en cada pieza que deba reaccionar a la mirada. Esto
  causó varios bugs de "no me interactúa" durante el desarrollo.
- **`GazeReticle`** (`Scripts/Gaze/GazeReticle.cs`): puramente visual, lee
  `GazeController.CurrentGazedObject` y `.GazeProgress` para mostrar/llenar el
  punto de mira. Vive en `ReticleCanvas`, un Canvas **World Space** hijo de
  `Main Camera`, fijo en `localPosition (0, 0, 1.5)` con escala `0.002` — así
  queda perfectamente centrado en pantalla sea cual sea la rotación de la
  cámara. Un Canvas Screen Space Overlay normal **no funciona bien en
  estéreo/Cardboard**, por eso se usa este truco de Canvas World Space pegado
  a la cámara.
  - Al estar en el mundo 3D, el reticle sufre depth-testing normal: geometría
    cercana lo puede tapar (pasaba mucho en el modo "examinar" de cerca). Se
    resolvió con un shader custom `Assets/Shaders/UI_AlwaysOnTop.shader`
    (`ZTest Always`, `ZWrite Off`, `Queue=Overlay`) asignado como Material en
    `ProgressImage`/`DotImage` — así el reticle se dibuja siempre encima de
    todo, sin importar la distancia real.
- **`VRFadeController`** (`Scripts/Gaze/VRFadeController.cs`): fundido a
  negro opcional para los teletransportes (alternativa al desplazamiento
  caminando, por si marea).

## Locomoción: teletransporte

- **`TeleportPoint`** (`Scripts/Locomotion/TeleportPoint.cs`): un punto
  mirable (`IGazeInteractable`) que, al seleccionarse, le pide a
  `TeleportManager` que mueva al jugador ahí. Se auto-registra en el manager
  vía `OnEnable`/`OnDisable`. Puede forzar una altura de ojos específica
  (`_overridePlayerHeight` / `_targetPlayerHeight`, para agacharse en huecos
  bajos). Tiene un evento `On Player Arrived` (`UnityEvent`) que se dispara
  cada vez que el jugador termina de llegar parado ahí — pensado para marcar
  hitos (por ejemplo, "el jugador ya vio la pista del código").
- **`TeleportManager`** (`Scripts/Locomotion/TeleportManager.cs`): centraliza
  el movimiento. Soporta caminata gradual (por defecto, con sonido de pasos)
  o fade instantáneo (`VRFadeController`). Expone `CurrentOccupiedPoint` (el
  `TeleportPoint` donde está parado el jugador ahora) para que otros scripts
  puedan consultarlo o volver ahí después (usado por `ExamineTrigger` y por
  `Door` para restringir apertura por posición).
  - **Importante:** el campo que mueve `TeleportManager` (`_cameraTransform`,
    antes llamado `_playerTransform`) tiene que apuntar a **Main Camera**, NO
    al objeto `Player`. Ver la sección "Separación de Main Camera y Player"
    más abajo — es la razón de este diseño.
- **`PlayerFollowsCamera`** (`Scripts/Locomotion/PlayerFollowsCamera.cs`): va
  en el objeto `Player`. En `LateUpdate()` copia la posición X/Z de `Main
  Camera` a `Player` (no la altura Y ni la rotación), para que `Player` quede
  siempre "parado" donde está realmente la cámara.

### Separación de Main Camera y Player (decisión de arquitectura importante)

Originalmente `Main Camera` era hija de `Player` (diseño típico). Esto causaba
un bug de drift del punto de mira al mover la cabeza. El profesor de Omar
encontró que separar `Main Camera` de `Player` (como objeto independiente en
la raíz de la escena) arreglaba el drift. Como consecuencia, se invirtió quién
mueve a quién:

- **Antes:** `TeleportManager` movía `Player`; `Main Camera` lo seguía porque
  era su hijo.
- **Ahora:** `TeleportManager` mueve directamente `Main Camera` (única forma
  de que el jugador vea que se desplazó), y `PlayerFollowsCamera` hace que
  `Player` siga a la cámara en X/Z, no al revés.

**Gotcha real que costó bastante diagnosticar:** el `TrackedPoseDriver` del
casco (Input System) tenía `Tracking Type = Rotation And Position`. Cardboard
es un casco de **3 grados de libertad** (solo rotación, sin tracking
posicional real) — lo que reporta como "posición" es ruido cercano a cero.
Mientras `Main Camera` era hija de `Player`, ese "casi cero" se aplicaba como
posición LOCAL respecto a `Player` (quedaba pegada a Player, sin drama). Al
separarla, ese mismo valor se empezó a aplicar como posición MUNDIAL directa,
mandando la cámara al origen del mundo (lejos de cualquier escenario) cada
frame. Se solucionó cambiando `Tracking Type` a **`Rotation Only`** en el
`Tracked Pose Driver (Input System)` de `Main Camera`. Si en el futuro la
cámara "desaparece" o queda flotando en el vacío después de tocar la jerarquía
Player/Camera, este es el primer sospechoso a revisar.

## Sistema de puertas y llaves

- **`KeyInventory`** (`Scripts/Interaction/KeyInventory.cs`): guarda si el
  jugador tiene la llave (`HasKey`). Se llena llamando `CollectKey()` desde el
  evento `OnCollected` de un `Collectable` (la llave en sí no necesita script
  propio).
- **`Door`** (`Scripts/Interaction/Door.cs`): puerta genérica, reusable.
  - `Requires Key` (tildado por defecto): pide `KeyInventory.HasKey` para
    abrirse; si no la tiene, muestra un cartel temporal (`_missingKeySign`).
    Destildado: se abre directo al mirarla (puertas comunes del mapa).
  - Apertura: mueve `transform.localPosition`/`localRotation` desde el valor
    inicial (guardado en `Awake`) hasta `_openLocalPosition`/
    `_openLocalEulerRotation` en `_openDuration` segundos fijos (no una
    velocidad — importante porque unidades locales bajo un padre con escala
    rara pueden representar cualquier distancia real).
  - **`Open()` es pública** justo para que otro script (como `CodeLock`) la
    pueda abrir directo, sin pasar por el chequeo de llave — así una puerta
    doble (dos `Door` independientes) se puede abrir en simultáneo cableando
    `CodeLock.OnUnlocked` a `Door.Open()` en ambas hojas.
  - Puede tener sonido de apertura (`_audioSource` + `_openSound`).
  - Puede restringirse a que **solo se abra por mirada si el jugador está
    parado sobre un `TeleportPoint` específico** (`_requiredPoint` +
    `_teleportManager`) — esto NO afecta a `Open()` llamado desde afuera
    (como `CodeLock`), que siempre funciona sin importar dónde está el
    jugador.

## El candado (puzzle de combinación) — mecánica principal implementada

Ubicado en una puerta doble (`tp9`), con el modelo `padlock.glb` (importado
con glTFast). Combinación configurada: **"067"** (3 dígitos, 0-9). Diseñado
para fomentar la exploración: el candado se ve a distancia normal desde el
pasillo, y el jugador tiene que **encontrar la pista del código primero**
(una nota, `sticky_notes`, cerca de `tp14`) antes de poder resolverlo.

### Jerarquía del modelo (`padlock.glb`)

```
padlock
└─ 0db4f1c1c9554dd58cfff7c5754f3afe.fbx (nombre interno raro, es normal)
   └─ RootNode
      ├─ BaseLP.004   (cilindro giratorio → CodeDigit, layer 7)
      ├─ BaseLP.003   (cilindro giratorio → CodeDigit, layer 7)
      ├─ BaseLP.002   (aro decorativo, NO gira, SIN CodeDigit)
      ├─ BaseLP.001   (cilindro giratorio → CodeDigit, layer 7)
      │  └─ BaseLP.001_PadLock_0 (mesh hijo)
      ├─ BaseLP       (cuerpo del candado, layer 9 → tiene ExamineTrigger
      │                 + BoxCollider grande para detectar de lejos)
      └─ CodeLock      (objeto separado, solo el componente CodeLock)
```

**Regla de convención:** los componentes (`Layer`, `Collider`, `CodeDigit`,
etc.) van en el objeto **padre** de cada pieza (`BaseLP.001`, no
`BaseLP.001_PadLock_0`), siguiendo la convención ya usada en el resto del
proyecto.

### Piezas de código

- **`CodeDigit`** (`Scripts/Interaction/CodeDigit.cs`): un cilindro/dial. Al
  mirarlo y seleccionarlo, gira al siguiente símbolo de una lista configurable
  (`_symbols`, actualmente `"0123456789"`) y le avisa a `CodeLock` con
  `ReportDigitChanged()`. El eje de rotación (`_rotationAxis`) y los grados
  por paso (`_degreesPerStep`) son ajustables a ojo por Inspector, porque
  dependen de cómo quedó orientado el modelo importado.
- **`CodeLock`** (`Scripts/Interaction/CodeLock.cs`): tiene el array de
  `CodeDigit` en orden y el código objetivo (`_targetCode`). Cada vez que
  cualquier dígito cambia, arma la palabra completa y la compara. Si
  coincide, dispara `OnUnlocked` (UnityEvent) UNA sola vez (`IsUnlocked`).
  Reinicia su estado en `OnEnable` (vive bajo `GameplayRoot`, que se
  activa/desactiva por partida).
- **`ExamineTrigger`** (`Scripts/Interaction/ExamineTrigger.cs`): va en
  `BaseLP` (el cuerpo del candado, visto a tamaño y distancia normal). En vez
  de acercar el objeto a la cámara (se descartó esa idea a propósito: un
  objeto pegado a la cámara no permite apuntar con la mirada a sub-objetivos
  distintos, porque el reticle centrado y el objeto siempre se mueven
  juntos), **mueve al JUGADOR** hasta un `TeleportPoint` fijo pegado y de
  frente al candado (`_examinePoint`). Así seguir apuntando a cada
  `CodeDigit` por separado sigue funcionando igual que con cualquier otro
  objeto del mapa.
  - Guarda en qué `TeleportPoint` estaba el jugador antes de entrar
    (`_previousPoint`) para poder volver con `ReturnToPreviousPoint()`.
  - Tiene eventos `On Examine Start` / `On Examine End` (UnityEvent) para
    cablear lo que haga falta al entrar/salir del modo examinar (luces,
    Collider, botones — ver más abajo).
  - **Requiere pista (opcional):** campo `Clue Tracker` — si se asigna un
    `CodeClueTracker`, no deja entrar a examinar hasta que
    `HasSeenCode == true`. Si no se asigna nada, el candado es examinable
    desde el principio (retrocompatible).
- **`CodeClueTracker`** (`Scripts/Interaction/CodeClueTracker.cs`): objeto
  simple con un booleano `HasSeenCode` y un método `MarkSeen()` para cablear
  desde `TeleportPoint.OnPlayerArrived` (en `tp14`, frente a las notas).
- **`ExamineExitButton`** (`Scripts/Interaction/ExamineExitButton.cs`): un
  objeto/botón que SOLO existe mientras se está examinando (se activa/
  desactiva con los mismos eventos `On Examine Start`/`On Examine End`). Al
  mirarlo, llama a `ExamineTrigger.ReturnToPreviousPoint()` manualmente, sin
  pasar por `CodeLock`. Existe porque sin él, un jugador que entra a examinar
  y se quiere ir SIN resolver el candado quedaba trabado — el Collider grande
  de `BaseLP` se apaga al entrar (para no tapar a los `CodeDigit`, ver
  gotcha abajo) y solo se reactivaba al resolver el código.

### El bug de la hitbox (ya resuelto, pero vale entenderlo)

El `BoxCollider` grande de `BaseLP` (usado por `ExamineTrigger` para
detectarlo de lejos) físicamente se solapa/tapa a los `BoxCollider` chicos de
cada `CodeDigit` una vez que el jugador está parado justo en frente, en modo
examinar — `Physics.Raycast` siempre devuelve el hit más cercano, así que
seguía "ganando" el Collider de `BaseLP` en vez de llegar a los de los
dígitos, y los cilindros no respondían a la mirada de cerca. Confirmado con
logs (`Debug.Log` temporales en `GazeController`, `CodeDigit`,
`ExamineTrigger` — ya sacados del código). Solución: apagar el `BoxCollider`
de `BaseLP` en `On Examine Start` y reactivarlo en `On Examine End`
(cableado como evento de Inspector, sin código extra).

### Wiring correcto de `On Examine Start` / `On Examine End` (estado final)

**On Examine Start** (4 filas):
1. `BaseLP → Box Collider.enabled = false` (destapa a los CodeDigit)
2. `BotonVolver → SetActive(true)`
3. *(luces, si se usan — ver nota abajo)*

**On Examine End** (debe tener):
1. `BaseLP → Box Collider.enabled = true` (para poder volver a examinar)
2. `BotonVolver → SetActive(false)`
3. *(luces, si se usan)*
4. **NO debe tener** `padlock → SetActive(false)` — ese fue un bug real
   (arrastraron el objeto raíz `padlock` en vez de una luz al armar esto a
   mano, y hacía desaparecer el candado entero al volver, como si se hubiese
   resuelto). Si se necesita ese efecto, ver el punto siguiente.

**`CodeLock.OnUnlocked`** (se dispara UNA sola vez, al acertar — nunca al
apretar "Volver" manualmente):
- `Door.Open()` en las dos hojas de la puerta.
- `ExamineTrigger.ReturnToPreviousPoint()`.
- Opcionalmente, `padlock → SetActive(false)` **acá sí es correcto** si se
  quiere que el candado desaparezca como efecto de haberlo resuelto — la
  diferencia con el punto anterior es que este evento NUNCA se dispara solo
  por salir del modo examinar sin resolver.

Las luces (apagar linterna del jugador / prender una luz propia del candado
para que no quede muy brillante y se puedan leer los números) son un tema
aparte y **todavía en definición** — Omar planea rehacer la iluminación del
cuarto más adelante, así que por ahora puede haber una versión simplificada o
temporal cableada en estos mismos eventos. No asumir que está resuelto sin
confirmar en la escena.

## Otras mecánicas ya implementadas (genéricas, listas para reusar)

- **`InteractiveObject`** (`Scripts/Interaction/InteractiveObject.cs`): botón/
  palanca genérico, dispara `OnSelected` (UnityEvent) al mirarlo. No se
  consume.
- **`Collectable`** (`Scripts/Interaction/Collectable.cs`): objeto
  recolectable, se desactiva una sola vez al mirarlo y dispara `OnCollected`.
- **`SequenceManager` / `SequenceStep`**
  (`Scripts/Interaction/SequenceManager.cs` / `SequenceStep.cs`): puzzle de
  secuencia genérico (mirar N pasos en el orden correcto). Si se falla el
  orden, reinicia todo. No está instanciado en la escena actual, pero el
  código está listo para usarse en otra parte/puzzle.
- **`GameManager`** (`Scripts/GameManager.cs`): loop mínimo de estado
  (MainMenu → Playing → Victory/Defeat → vuelve a Menu), activando/
  desactivando GameObjects raíz por estado (`_gameplayRoot`, etc.). Los
  "botones" de menú son `InteractiveObject` comunes cableados a
  `StartGame()`/`TriggerVictory()`/`TriggerDefeat()`/`ReturnToMenu()`.
- **`GazeDebugTarget`** (`Scripts/Interaction/GazeDebugTarget.cs`): script de
  prueba viejo (etapa temprana del proyecto, solo loguea los 4 estados de
  gaze por consola). Vestigial, no debería estar en uso real en la escena
  actual — si aparece en algún objeto, probablemente se puede sacar.
- **`CameraDebugLogger`** (`Scripts/Debug/CameraDebugLogger.cs`): script de
  diagnóstico temporal (loguea posición/rotación real de Main Camera + estado
  de XR una vez por segundo). Se usó para encontrar el bug de
  `Tracking Type`. Se puede sacar del objeto `Main Camera` si ya no hace
  falta, pero el archivo no molesta si queda en el proyecto sin usarse.

## Gotchas / lecciones aprendidas (leer antes de tocar cosas relacionadas)

1. **glTFast y FBX no generan Colliders automáticamente.** Cualquier pieza
   nueva que deba reaccionar a la mirada necesita que le agreguen un
   `Collider` a mano, además del `Layer` correcto.
2. **El `Layer` es un gate independiente del Collider/script.** Un objeto
   puede tener Collider + el script de `IGazeInteractable` correcto y
   *igual* no reaccionar si su Layer no está incluido en el
   `_interactiveLayerMask` de `GazeController`. Esto fue la causa real de
   varios "no me interactúa" a lo largo del proyecto.
3. **Un objeto pegado como hijo rígido de la cámara no puede tener
   sub-objetivos de mirada independientes** (el reticle centrado y el objeto
   siempre se mueven juntos). Por eso el modo "examinar" mueve al jugador en
   vez de acercar el objeto a la cámara.
4. **Colliders grandes pensados para detección "de lejos" pueden tapar a
   Colliders chicos "de cerca"** si se solapan espacialmente — Raycast
   siempre devuelve el hit más cercano, no importa la jerarquía de objetos.
   Solución general: desactivar el Collider grande mientras se necesite el
   chico (ver el patrón de `ExamineTrigger`/`On Examine Start`/`On Examine
   End`).
5. **Materiales embebidos en un modelo importado (`.glb`/`.fbx`) son de solo
   lectura en el Inspector.** Hay que extraerlos primero: seleccionar el
   material sub-asset → click derecho → `Extract From Asset...` (o desde el
   importer del modelo, pestaña Materials → `Extract Materials...`) para
   poder editar sliders como `Roughness`/`Metallic`/`Smoothness`.
6. **El shader de glTFast (`Shader Graphs/glTF-pbrMetallicRoughness`) usa
   `Roughness`, no `Smoothness`** (workflow glTF estándar). Si hay que sacar
   brillo especular que "rebota" con el movimiento de cabeza en VR, subir
   `Roughness` (más mate) y/o bajar `Metallic`, no buscar `Smoothness`.
7. **Un Canvas para HUD/VR tiene que ser World Space, hijo de la cámara,**
   nunca Screen Space Overlay (no renderiza bien en estéreo Cardboard). El
   patrón ya probado es `ReticleCanvas` — para textos nuevos, lo más simple
   es agregarlos como hijos de ese mismo Canvas en vez de crear uno nuevo.
8. **`TrackedPoseDriver` con `Tracking Type = Rotation And Position` en un
   objeto SIN padre (o cuyo padre no debe recibir esa posición) puede
   mandarlo al origen del mundo**, porque Cardboard no tiene tracking
   posicional real (reporta ~0,0,0). Usar `Rotation Only` salvo que
   explícitamente se necesite posición 6DOF.
9. **Cuidado al duplicar componentes en vez de agregar filas a un evento ya
   existente.** Pasó más de una vez: en vez de agregar una fila nueva al
   `UnityEvent` de un componente que ya estaba, se agregó un componente
   *nuevo* del mismo tipo con solo esa fila — como `GetComponentInParent<T>()`
   devuelve un solo componente, el segundo simplemente nunca se ejecutaba.
   Si algo cableado "debería" funcionar y no lo hace, revisar si hay
   componentes duplicados en el mismo GameObject.
10. **Al cablear `UnityEvent.SetActive` desde el Inspector, doble-chequear
    qué objeto se arrastró.** Un caso real: se quiso apagar una luz al salir
    del modo examinar, pero se arrastró el objeto raíz `padlock` por error —
    el candado entero desaparecía. Cuando algo "desaparece" o se comporta
    como si un puzzle ya resuelto lo hubiera afectado, revisar los
    `SetActive` de los eventos cerca de esa mecánica.
11. **Todos los objetos que "reinician" estado por partida** (`CodeLock`,
    `CodeDigit`, `SequenceManager`, `SequenceStep`) lo hacen en `OnEnable()`,
    porque viven bajo `GameplayRoot`, que `GameManager` activa/desactiva en
    cada cambio de estado — así una partida nueva no arrastra el progreso de
    la anterior.
12. **Para diagnosticar sin adivinar:** este proyecto tiene un historial largo
    de "el usuario dice X, pero el archivo de la escena dice Y" en ambas
    direcciones (a veces el usuario tenía razón y la IA se equivocaba
    sospechando, a veces al revés). El método que funcionó siempre fue el
    mismo: pedir el archivo `.unity`/`.prefab` real (vía el bridge de
    archivos del dispositivo), grep/leer los bloques YAML relevantes
    (`m_Modifications`, `m_IsActive`, `m_Layer`, `PersistentCalls` de los
    `UnityEvent`), y recién ahí sacar conclusiones — nunca confiar solo en
    la descripción o en una captura de pantalla parcial. Cuando algo sigue
    sin resolverse tras la evidencia estática, pedir logs (`Debug.Log`
    temporales + `adb logcat -s Unity` en vivo) en vez de seguir
    especulando.

## Cómo se trabaja en este proyecto (para una IA sin acceso directo al Editor)

- El acceso a los archivos es vía un bridge de archivos remoto (no hay
  `device_bash` para esta máquina — solo listar/leer/escribir archivos
  puntuales).
- Los cambios de **código** (`.cs`) se escriben directamente y se depositan
  en el proyecto — Omar no tiene que tipearlos.
- Los cambios de **configuración de escena/Inspector** (crear GameObjects,
  arrastrar referencias, tildar checkboxes, ajustar Transforms a ojo,
  extraer materiales, etc.) los tiene que hacer Omar a mano en el Editor,
  siguiendo instrucciones **muy concretas y en orden** (nombre exacto del
  menú/botón, qué arrastrar dónde) — instrucciones vagas generaron
  confusión varias veces a lo largo del proyecto.
- Para probar en el celular real: Omar compila e instala el APK, y si hace
  falta diagnosticar algo en tiempo real, conecta el celular por USB y corre
  `adb logcat -s Unity` mientras prueba.

## Estado actual (a esta fecha) y pendientes conocidos

**Funcionando / implementado:**
- Sistema de gaze (raycast, reticle centrado, always-on-top shader).
- Teletransporte (caminata + fade), con Main Camera separada de Player y
  `Tracking Type = Rotation Only` (fix del bug de la cámara en el origen).
- Puertas con llave (genérico) y puertas dobles abiertas en simultáneo desde
  `CodeLock`.
- Candado de combinación completo: dígitos giratorios, verificación de
  código, modo examinar (mover al jugador, no al objeto), botón de volver,
  requisito de haber visto la pista antes de poder examinar.
- Nivel de "Parte 1" con `tp1`-`tp15` armados por Omar, candado en `tp9`
  (código "067"), nota de la pista cerca de `tp14`.

**Pendiente / a confirmar (no dar por hecho sin verificar en la escena):**
- Iluminación final del modo examinar del candado (Omar planea rehacerla).
- Confirmar que el texto/textura de `sticky_notes` realmente muestre "067"
  legible (no se pudo verificar el contenido visual desde acá).
- `Requires Key` en las dos hojas de la puerta del candado: en algún momento
  se encontró en `false` (bypass del puzzle) — confirmar que sigue en `true`.
- Materiales de `_inactiveMaterial`/`_gazedAtMaterial` en varios objetos
  (candado, dígitos, botón de volver) — quedaron opcionales, puede que
  falten para tener feedback visual de mirada.
- Agregar las escenas "Parte 2" en adelante a Build Settings cuando existan.
- Colliders en el resto de la geometría de "Parte 1" (fuera del candado) —
  no cubierto en el trabajo reciente.
- Etapas de puzzle 2/3/4/5 mencionadas en el diseño original (pinturas que
  se dan vuelta, contador de fragmentos, etc.) — todavía no instanciadas en
  escena, aunque `SequenceManager`/`SequenceStep`/`Collectable` ya están
  listos como building blocks genéricos para varias de ellas.
- Pase de rendimiento para Android/A54 (nivel final).
- Sacar `GazeDebugTarget` y `CameraDebugLogger` de la escena una vez que ya
  no se necesiten para diagnosticar (los archivos no molestan si quedan sin
  usar, pero no deberían tener componentes activos en objetos de juego).
