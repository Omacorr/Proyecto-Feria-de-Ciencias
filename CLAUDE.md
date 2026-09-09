# PROYECTO INTEGRADOR — VR Cardboard (Feria de Ciencias)

Este archivo es el punto de partida para cualquier IA (o persona) que retome este
proyecto. Resume qué es, cómo está armado, qué se implementó y por qué, y qué
falta. Los scripts en sí también tienen comentarios XML/tooltips extensos en
español explicando el "por qué" de cada decisión — este documento es el mapa
general; para el detalle de una mecánica puntual, siempre conviene leer el
script correspondiente.

> **Este documento se tiene que mantener actualizado.** Cada vez que se resuelva
> un bug importante, se agregue una mecánica nueva, o se tome una decisión de
> arquitectura, hay que volver a este archivo y agregarlo (sección nueva o
> ampliar una existente, más una línea en "Estado actual" si corresponde). No
> es un documento que se escribe una vez y se olvida — es la memoria del
> proyecto entre sesiones, y varias veces ya sirvió para no repetir el mismo
> diagnóstico dos veces.

## Qué es

Juego de realidad virtual para **Google Cardboard**, hecho como proyecto de
Feria de Ciencias por Omar (usuario `cutuc`), pensado para probarse en un
Samsung A54 con un visor Cardboard de cartón. Es un juego de escape/puzzles en
primera persona: el jugador se mueve por teletransporte (mirando un punto y
sosteniendo la mirada) por un hotel/hospital abandonado, resuelve acertijos
(por ahora, un candado de combinación) y avanza por la historia.

## Estructura de escenas (`Assets/Scenes/`)

El proyecto ya no es una sola escena — hay varias, y cada una tiene **su
propia** instancia de `Main Camera`, `TeleportManager`, `ReticleCanvas`, etc.
(no se comparte nada entre escenas salvo lo que pasa por `GameSettings`, ver
más abajo). Cualquier fix de Inspector hecho en una escena **no se propaga
sola a las demás** — hay que repetirlo a mano en cada una si aplica.

- **`Menu Principal.unity`**: pantalla de inicio. Ver sección propia más abajo.
- **`Tutorial.unity`**: primera escena de juego real, a la que entra
  `MainMenuController` al elegir "Empezar".
- **`Parte 1 - El Despertar.unity`**: primer nivel real, donde está el candado
  de combinación. Fue la escena más desarrollada al principio del proyecto.
- **`Parte 2 - Cumpleaños.unity`**, **`Parte 4 - Pasillo de madera.unity`**,
  **`Parte 5 - Backrooms.unity`**, **`Parte Final.unity`**: siguientes partes
  de la historia, en distintos grados de avance (algunas casi vacías todavía).
- **`Parte 3 - Pasillo de hotel.unity`**: **la escena más activamente
  trabajada actualmente** (a la fecha de esta actualización). Tiene el pasillo
  de hotel con varias puertas (`room_bellis_deluxe`, objetos `27_2`/`27_2 (1)`,
  etc.), varios `tp1`-`tp8` (algunos duplicados como "tp1 pieza 1"/"tp1 pieza 2"
  para distintos cuartos), y fue donde se resolvieron la mayoría de los bugs
  de gaze/Collider/Layer más recientes (ver gotchas).

## Entorno técnico

- **Unity 6000.3.22f1**, build target Android.
- **Google Cardboard XR Plugin** (com.google.xr.cardboard), integrado vía el
  paquete de XR Plugin Management / XR Interaction. El proyecto **partió del
  sample "Hello Cardboard"** de ese paquete — varios bugs de este proyecto
  fueron en realidad restos de ese sample que quedaron pegados sin querer
  (ver sección de gotchas más abajo).
- **URP** (Universal Render Pipeline), con Bloom activado (ver "Efectos
  visuales" más abajo).
- **glTFast** para importar modelos `.glb`/`.gltf` (candado, notas, números de
  madera, `room_bellis_deluxe`, y varios más en `Assets/Modelos 3D/`).
- **Input System** (paquete nuevo de Unity), usado por el `TrackedPoseDriver`
  que lee la pose del casco.
- El proyecto vive en `C:\Users\cutuc\PROYECTO INTEGRADOR` en la máquina de
  Omar ("rog-omar"). El repo remoto es
  `https://github.com/Omacorr/Proyecto-Feria-de-Ciencias.git`. Es un proyecto
  de **dos personas** (Omar + un compañero) — varias cosas nuevas en el
  proyecto (scripts de menú, `FlickeringLight`, el Volume de Bloom) las agregó
  el compañero sin avisar, así que si algo aparece "de la nada" en la escena o
  en `Assets/Scripts/`, antes de asumir que es un bug vale la pena revisar si
  ya está parcialmente armado y solo falta terminar de cablearlo (pasó más de
  una vez: un archivo/asset ya creado pero nunca conectado a nada en la
  escena).
- **Importante para una IA que retome esto sin acceso directo al Editor de
  Unity:** todo el trabajo de Inspector (arrastrar referencias, crear
  GameObjects, tildar checkboxes, ajustar Transforms a ojo) lo tiene que hacer
  Omar a mano siguiendo instrucciones paso a paso — no hay forma de controlar
  el Editor de Unity de forma remota. Los cambios de código sí se pueden
  escribir y dejar guardados directamente en los archivos `.cs`. Para
  diagnosticar bugs de configuración de la escena, es válido (y se usó mucho)
  leer directamente el archivo `.unity` (es YAML) en vez de confiar en
  capturas de pantalla o descripciones — a veces revela la verdad cuando el
  usuario y la IA no se ponen de acuerdo sobre qué está pasando. Incluso se
  llegó a **editar el YAML de la escena directamente** (agregar componentes,
  asignar referencias, corregir Layers) cuando el cambio era chico, mecánico y
  de bajo riesgo — pero para cosas que dependen de datos que Unity calcula
  solo (por ejemplo, el tamaño de un `Box Collider` ajustado a una malla real)
  eso NO se puede reproducir a mano de forma confiable, y ahí sí hace falta
  que Omar lo haga desde el Editor.
- **Unity MCP (desde 2026-09-08, conexión confirmada funcionando):** el
  proyecto ya tiene el paquete oficial `com.unity.ai.assistant` (2.19.0-pre.2)
  y el bridge Unity MCP habilitado (`Edit > Project Settings > AI > Unity
  MCP`), configurado para conectarse con Claude Code. Registro global en
  `~/.claude.json` → `mcpServers.unity-mcp` (`relay_win.exe --mcp`). Las
  herramientas (`Unity_ManageEditor`, `Unity_ManageScene`, etc.) aparecen como
  "deferred" al arrancar la sesión — hay que cargarlas primero con
  `ToolSearch` (`select:mcp__unity-mcp__...`) antes de poder invocarlas.
  Probado 2026-09-08: `Unity_ManageEditor GetState` y `Unity_ManageScene
  GetActive` devolvieron el estado real del Editor abierto (Unity
  6000.3.22f1, escena activa `Parte 3 - Pasillo de hotel`, 16 objetos raíz),
  confirmando que **una IA sí puede leer/inspeccionar la escena viva del
  Editor sin depender de adivinar a partir del `.unity`**, siempre que Omar
  tenga el Editor abierto con el proyecto cargado y haya aceptado la
  conexión. Aun así, seguí validando con el método de leer el YAML directo si
  algo no cierra, o si Omar no tiene el Editor abierto en el momento — es el
  que tiene historial probado en este proyecto.

## Arquitectura: sistema de mirada (gaze)

Es el corazón de toda la interacción, porque no hay controles físicos (es
Cardboard, sin gatillo/mando): todo se hace **mirando un objeto y sosteniendo
la mirada** (por defecto 2 segundos, configurable — ver `GameSettings`).

- **`GazeController`** (`Scripts/Gaze/GazeController.cs`): en su `Update()`
  tira un `Physics.Raycast` desde la cámara (o `Camera.main` si no se le
  asigna una) hacia adelante, filtrado por un `LayerMask` (`_interactiveLayerMask`,
  normalmente los layers `Interactive` (7) y `Teleport` (9)). Cuando el objeto
  mirado cambia, busca con `GetComponentInParent<IGazeInteractable>()` el
  primer componente que implemente esa interfaz y le dispara
  `OnGazeEnter`/`OnGazeExit`. Mientras se sigue mirando el mismo objeto,
  cuenta el tiempo y dispara `OnGazeStay(progress)` cada frame; al llegar a
  1.0 dispara `OnGazeSelect()` y **reinicia el timer**, así que si se sigue
  mirando el mismo objeto, vuelve a dispararse `OnGazeSelect()` periódicamente
  (cada script decide si eso le importa o no). Tiene un `Debug.Log` temporal
  todavía activo (`[GazeController] Gaze cambio -> ...`) útil para
  diagnosticar qué Collider está pegando el rayo realmente vs. en qué
  GameObject se encontró el `IGazeInteractable` — no sacarlo sin confirmarlo,
  se sigue usando activamente para debug.
- **`IGazeInteractable`** (`Scripts/Interaction/IGazeInteractable.cs`): la
  interfaz que implementa todo objeto que reacciona a la mirada. Solo 4
  métodos: `OnGazeEnter`, `OnGazeStay(float progress)`, `OnGazeExit`,
  `OnGazeSelect`. `GazeController` no sabe nada de lo que hace cada objeto —
  solo despacha estos eventos.
- **Requisito para que un objeto sea "mirable":** tiene que tener un
  `Collider` y estar en un layer incluido en el `_interactiveLayerMask` del
  `GazeController` (normalmente `Interactive`=7 o `Teleport`=9, según el tipo
  de objeto). **Ojo:** ni glTFast ni FBX generan Colliders automáticamente al
  importar — hay que agregarlos a mano en cada pieza que deba reaccionar a la
  mirada, y el Collider **tiene que ir en el mismo GameObject que tiene el
  script** `IGazeInteractable` (o en un HIJO de ese objeto — nunca en un
  padre, porque `GetComponentInParent` busca hacia arriba, no hacia abajo).
  Esto causó varios bugs de "no me interactúa" durante el desarrollo, el más
  reciente en `room_bellis_deluxe` (ver gotchas).
- **`GazeReticle`** (`Scripts/Gaze/GazeReticle.cs`): puramente visual, lee
  `GazeController.CurrentGazedObject` y `.GazeProgress` para mostrar/llenar el
  punto de mira. Vive en `ReticleCanvas`, un Canvas **World Space** hijo de
  `Main Camera`, fijo en `localPosition (0, 0, 1.5)` con escala `0.002` — así
  queda perfectamente centrado en pantalla sea cual sea la rotación de la
  cámara. Un Canvas Screen Space Overlay normal **no funciona bien en
  estéreo/Cardboard**, por eso se usa este truco de Canvas World Space pegado
  a la cámara.
- **`VRFadeController`** (`Scripts/Gaze/VRFadeController.cs`): fundido a
  negro. `FadeOutAndIn(onBlackout)` funde a negro, ejecuta una acción (por
  ejemplo mover al jugador) y vuelve a fundir a la escena — lo usa
  `TeleportManager` para el modo fade. `FadeToBlack()` funde a negro y se
  QUEDA en negro (no vuelve) — lo usa `MainMenuController` justo antes de
  cargar la escena de juego. Su `Image` de fundido vive en el mismo
  `ReticleCanvas` que la mira (objeto `FadeImage`).

### Geometría cercana tapando al Canvas World Space (reticle y fade) — resuelto

Al estar en el mundo 3D, cualquier elemento de ese `ReticleCanvas` (mira, aro
de progreso, `FadeImage`) sufre depth-testing normal como cualquier objeto 3D:
si hay geometría física más cerca de la cámara que el Canvas (fijo a 1.5m), esa
geometría lo tapa. Esto causaba dos síntomas que en un principio parecían
bugs separados pero eran la MISMA causa:
- La mira desaparecía al acercarse mucho a un objeto (por ejemplo la puerta
  corrediza `room_bellis_deluxe`).
- El fundido a negro dejaba "ver a través" cuando había geometría muy cerca de
  la cámara durante el fundido.

**Solución:** un shader custom `Assets/Shaders/UI_AlwaysOnTop.shader`
(`ZTest Always`, `ZWrite Off`, `RenderPipeline=UniversalPipeline`) + un
Material `Assets/Materials/Mat_ReticleAlwaysOnTop.mat` que lo usa, asignado en
el campo **Material** de los componentes `Image` de `DotImage`, `ProgressImage`
y `FadeImage` dentro de `ReticleCanvas` — así esos tres elementos se dibujan
SIEMPRE encima de todo, sin importar la distancia real de la geometría. Este
fix hay que aplicarlo **por escena** (cada escena tiene su propio
`ReticleCanvas`) — a la fecha de esta actualización está aplicado en
`Parte 1` y `Parte 3`; si se agrega geometría cercana en otra escena y
aparece el mismo síntoma, revisar primero si esos 3 `Image` tienen el
material puesto.

## Locomoción: teletransporte

- **`TeleportPoint`** (`Scripts/Locomotion/TeleportPoint.cs`): un punto
  mirable (`IGazeInteractable`) que, al seleccionarse, le pide a
  `TeleportManager` que mueva al jugador ahí. Se auto-registra en el manager
  vía `OnEnable`/`OnDisable`. Puede forzar una altura de ojos específica
  (`_overridePlayerHeight` / `_targetPlayerHeight`, para agacharse en huecos
  bajos). Tiene un evento `On Player Arrived` (`UnityEvent`) que se dispara
  cada vez que el jugador termina de llegar parado ahí — pensado para marcar
  hitos (por ejemplo, "el jugador ya vio la pista del código"), y también
  sirve para un sonido de LLEGADA sin código extra: arrastrar un objeto con
  `AudioSource` (con el clip ya puesto en ESE AudioSource) y elegir la función
  `AudioSource.Play()` en el dropdown del evento.
  - **Destino alternativo (`Destination Override`):** si se asigna un
    `Transform`, mirar este punto teletransporta a la posición de ESE
    Transform en vez de a la posición propia. Pensado para puertas: la puerta
    se queda quieta donde se ve bien, pero el destino real es otro punto al
    otro lado (por ejemplo un `Cube` vacío). `sourcePoint` (el punto en sí)
    sigue siendo el que se usa para `SetVisible`/`CurrentOccupiedPoint`/
    `NotifyArrived`.
  - **`UsesFadeTransition`** (propiedad de solo lectura, `true` si
    `Destination Override` está asignado): `TeleportManager` la consulta para
    forzar el modo fade en ESE teletransporte puntual, sin importar si
    `Use Walk Animation` está tildado para el resto de la escena — caminar
    hacia un destino que visualmente no es el mismo punto que se está mirando
    queda confuso.
  - **Audio (`Audio Source` + `Teleport Sound`, opcional):** mismo patrón que
    `Door` (ver más abajo) — suena apenas se selecciona el punto, antes de
    moverse. Si hay varios `TeleportPoint` juntos, se puede compartir un
    `Audio Source` central entre ellos. **Mismo bug que en `Door` confirmado
    acá también (2026-09-08):** en `Parte 3` había 6 `TeleportPoint` con
    `Destination Override` asignado (o sea, puertas por fade entre
    `hallway_hotel`, `custom_brown_axminster_carpet_hotel_room`,
    `hayama_washitsu_raw_scan` y `room_bellis_deluxe`) con el `Audio Source`
    bien puesto pero `Teleport Sound` vacío — mismo síntoma, mismo diagnóstico
    que en `Door`.
- **`TeleportManager`** (`Scripts/Locomotion/TeleportManager.cs`): centraliza
  el movimiento. Soporta caminata gradual (`_useWalkAnimation`, con sonido de
  pasos) o fade instantáneo (`VRFadeController`). La decisión real es:
  `useFade = !_useWalkAnimation || sourcePoint.UsesFadeTransition` — o sea,
  puntos normales respetan el toggle de la escena, pero un punto con
  `Destination Override` siempre usa fade. Expone `CurrentOccupiedPoint` (el
  `TeleportPoint` donde está parado el jugador ahora) para que otros scripts
  puedan consultarlo o volver ahí después (usado por `ExamineTrigger` y por
  `Door` para restringir apertura por posición).
  - **Importante:** el campo que mueve `TeleportManager` (`_cameraTransform`,
    antes llamado `_playerTransform`) tiene que apuntar a **Main Camera**, NO
    al objeto `Player`. Ver la sección "Separación de Main Camera y Player"
    más abajo — es la razón de este diseño. Si un `tp` "no hace nada" al
    mirarlo, lo primero a revisar es si este campo quedó vacío en esa escena
    en particular (pasó en `Parte 3` — cada escena tiene su propio
    `TeleportManager`, no se comparte).
- **`PlayerFollowsCamera`** (`Scripts/Locomotion/PlayerFollowsCamera.cs`): va
  en el objeto `Player`. En `LateUpdate()` copia la posición X/Z de `Main
  Camera` a `Player` (no la altura Y ni la rotación), para que `Player` quede
  siempre "parado" donde está realmente la cámara. `MainMenuController` la
  desactiva a mano durante la secuencia de entrada del menú, porque si no
  pelearía con el movimiento manual de la cámara cruzando la puerta.

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

### El rig de jugador NO está unificado entre escenas (fragmentación real, confirmado 2026-09-08)

No hay un solo prefab de "Player + Main Camera" que se use en todos lados —
hay como mínimo TRES configuraciones distintas conviviendo:

- **`Assets/Prefabs/Player 1.prefab`**: el que usa `Parte 1 - El Despertar`.
- **`Assets/Prefabs/Player.prefab`**: un prefab DISTINTO (no una instancia del
  anterior) que usa `Tutorial`.
- **`Parte 3 - Pasillo de hotel` y `Menu Principal`**: NO usan ninguno de los
  dos prefabs — tienen su propio `Main Camera` armado a mano directo en la
  escena (con su propio `GazeController`, `TrackedPoseDriver`, etc., sin
  vínculo con los prefabs de arriba).
- **`Parte 4` (agregado 2026-09-08):** se instanció `Player.prefab` y se
  desempaquetó (`PrefabUtility.UnpackPrefabInstance`) para poder separar
  `Main Camera` de `Player` — así que tampoco queda vinculado al prefab, es
  una **quinta** configuración suelta, aunque nació copiando la estructura de
  `Player.prefab`. Ver la sección "Rig de jugador de Parte 4" más abajo para
  el detalle completo de qué se hizo y qué gotcha nuevo salió en el proceso.

**Por qué importa:** un fix hecho en el `Tracked Pose Driver` (u otro
componente) de una de estas configuraciones **no se propaga a las demás** —
ni siquiera entre instancias del mismo prefab si alguna ya tiene el campo
overrideado. Esto fue exactamente lo que pasó con el bug de `Tracking Type`:
estaba bien en `Parte 1` pero mal en `Parte 3` (`Rotation And Position` en vez
de `Rotation Only`, y `Update Type` distinto) porque son configuraciones
completamente independientes, no builds del mismo prefab desactualizados.
**Pendiente real:** unificar esto en un solo prefab de rig (o al menos
auditar las 4+ configuraciones sueltas) para que este tipo de bug deje de
poder repetirse escena por escena.

## Menú principal y flujo de escenas

- **`MainMenuController`** (`Scripts/Menu/MainMenuController.cs`): vive en la
  escena `Menu Principal`. Muestra un panel por vez (`_mainPanel`/
  `_configPanel`/`_creditsPanel`) vía `OpenMain()`/`OpenConfig()`/
  `OpenCredits()`, cableados desde botones `InteractiveObject` comunes.
  `StartGame()` (botón Empezar) corre una secuencia opcional de "entrada":
  abre una puerta (rotación local), avanza la cámara cruzándola, funde a
  negro (`VRFadeController.FadeToBlack()`), espera un mínimo de segundos +
  a que la escena siguiente termine de cargar en segundo plano
  (`LoadSceneAsync` con `allowSceneActivation = false`), y recién ahí activa
  la escena nueva. El nombre de esa escena es un **string exacto**
  (`_gameSceneName`, por defecto `"Tutorial"`) — tiene que estar tal cual
  escrito en Build Settings, si no `SceneManager.LoadScene` falla en
  silencio/tira error en runtime.
- **`GameSettings`** (`Scripts/Settings/GameSettings.cs`): singleton
  persistente (`DontDestroyOnLoad`, se crea solo con
  `RuntimeInitializeOnLoadMethod` si no existe todavía). Guarda dos opciones
  en `PlayerPrefs` (sobreviven entre escenas y entre sesiones): velocidad de
  selección del gaze (`GazeSpeed`, 3 presets Bajo/Medio/Alto → 3/2/1.2
  segundos) y tamaño del reticle (`ReticleSize`, 3 presets → multiplicador
  0.7/1/1.5). Cada vez que cambia una opción o se carga una escena nueva, se
  auto-aplica al `GazeController`/`GazeReticle` que haya en esa escena
  (`Apply()`) — no hace falta ningún componente "applier" por escena.
- **`ConfigMenu`** (`Scripts/Menu/ConfigMenu.cs`): el panel de configuración
  de `Menu Principal`. 6 botones (3 presets x 2 opciones), cada uno cableado a
  uno de los métodos públicos (`SetGazeSpeedBajo()`, etc.), que llaman a
  `GameSettings.GetOrCreate().Set...()`. No hay sliders porque en Cardboard no
  se puede arrastrar nada — todo es mirar un botón de preset.
- **Flujo completo:** `Menu Principal` → (Empezar) → `Tutorial` → (de ahí en
  adelante, las escenas "Parte N" en orden).

## Efectos visuales

- **Bloom (post-procesado, URP):** hay un `Global Volume Profile` compartido
  en `Assets/Settings/Global Volume Profile.asset`. Para activarlo en una
  escena hace falta: 1) un GameObject `Volume` (`Global Volume`, `Is Global`
  tildado) con ese mismo Profile asignado, 2) el override `Bloom` agregado
  dentro del Profile con al menos Threshold/Intensity/Scatter tildados, y 3)
  `Post Processing` tildado en el componente `Camera` de `Main Camera` de esa
  escena. El Profile es compartido entre escenas (mismo archivo), así que
  ajustarlo en una escena afecta a todas las que tengan un `Global Volume`
  apuntando ahí — conviene avisar si se agrega un `Global Volume` nuevo en
  otra escena para no pisarse con el compañero.
- **`FlickeringLight`** (`Scripts/Ambient/FlickeringLight.cs`): hace parpadear
  una `Light` (requiere el componente `Light` en el mismo GameObject) con dos
  efectos combinados: un parpadeo suave por ruido Perlin
  (`_flickerAmount`/`_flickerSpeed`) y apagones cortos aleatorios
  (`_blackoutChancePerSecond`/`_blackoutMaxDuration`). `_baseIntensity` en 0
  hace que tome automáticamente la intensidad que ya tenía la Light al
  arrancar (recomendado, evita tener que adivinar el número). Instancias
  conocidas: `Luz Parpadeante` en `Menu Principal` (luz ambiental de la
  habitación del menú) y el `Spot Light` de `Menu Principal` (la linterna que
  se ve en esa escena). **Ojo:** si se agrega este componente a un objeto que
  todavía no tiene una `Light` propia (por ejemplo, si se lo pone directo en
  `Main Camera` en vez de en su hijo `Spot Light`, que es la linterna real),
  Unity crea una `Light` nueva con valores por defecto en ese objeto por el
  `[RequireComponent(typeof(Light))]` — casi seguro NO es lo que se quiere;
  hay que ponerlo en el objeto que YA tiene la Light correcta.
- **`UI_AlwaysOnTop.shader`** (`Assets/Shaders/`) + material
  `Mat_ReticleAlwaysOnTop.mat`: ver sección de gaze/reticle más arriba.

### Ambiente tétrico de `Parte 4 - Pasillo de madera` (aplicado 2026-09-08 vía Unity MCP)

A pedido de Omar, se le copió a `Parte 4` la misma receta de iluminación de
terror que ya tenía `Parte 3` (`RenderSettings` + `Global Volume` + luces
parpadeantes). Se hizo **en caliente sobre el Editor abierto**, usando
`Unity_RunCommand` del bridge Unity MCP (código C# ejecutado directamente en
la sesión de Omar, no edición de YAML) — quedó como cambio sin guardar
(`EditorSceneManager.MarkSceneDirty`), pendiente de que Omar revise en el
Editor y guarde con Ctrl+S.

**Receta exacta (leída de `Parte 3` antes de aplicarla, no inventada):**
- `RenderSettings.fog = true`, `fogMode = ExponentialSquared`, `fogColor =
  (0.5, 0.5, 0.5)`, `fogDensity = 0.037`.
- `RenderSettings.ambientMode = Skybox`, `ambientIntensity = 0` (esto es lo
  que realmente mata la luz ambiente por defecto de Unity — los colores
  `ambientSkyColor`/`ambientEquatorColor` quedan copiados por prolijidad pero
  no aportan nada mientras la intensidad sea 0).
- **Directional Light nueva, con color NEGRO** (intensity 1) — mismo truco
  que `Parte 3`: no se borra la luz direccional, se la deja en negro para que
  no aporte luz global, y toda la iluminación real queda en manos de las
  luces puntuales.
- **`Global Volume`** nuevo, `Is Global` tildado, apuntando al **mismo
  `Assets/Settings/Global Volume Profile.asset`** compartido que ya usan
  `Menu Principal` y `Parte 3` (con el Bloom ya configurado ahí) — no se creó
  un profile nuevo, así que cualquier ajuste futuro al Bloom en ese asset
  sigue afectando a las tres escenas por igual.
- **Post Processing tildado** en el `Main Camera` de la escena
  (`UniversalAdditionalCameraData.renderPostProcessing = true`), paso
  necesario para que el Bloom del Volume realmente se vea (ver receta general
  en la sección de Bloom más arriba).
- **9 luces `Spot` con `FlickeringLight`**, una por cada lámpara colgante del
  modelo `house_corridor_interior` (agrupadas bajo un GameObject organizador
  nuevo, `Luces Tetricas`). Las posiciones de las lámparas se encontraron
  programáticamente (buscando meshes chicos cerca del techo, no a ojo) y cada
  luz quedó con los mismos valores que la única luz parpadeante que ya tenía
  `Parte 3` (`Spot`, color casi blanco `(0.90, 0.90, 0.90)`, intensity 10,
  range 10, spot angle ~74.4°, sin sombras) + `FlickeringLight` con sus
  valores **por defecto del script** (`_baseIntensity=1.5`,
  `_flickerAmount=0.35`, `_flickerSpeed=8`,
  `_blackoutChancePerSecond=0.25`, `_blackoutMaxDuration=0.08`) — son los
  mismos que ya traía el script en `Parte 3`, no hubo que inventar
  parámetros nuevos.

**Nota importante sobre `FlickeringLight` y el campo `Intensity` del
Inspector:** el valor de `Intensity` que se ve en el componente `Light` (10
en este caso) es **cosmético en el Editor** — en cuanto arranca el juego,
`FlickeringLight.Awake()` pisa la intensidad real con `_baseIntensity` (1.5
por defecto) porque ese campo no está en 0. No confundir "el número que se ve
en el Inspector" con "la intensidad real en Play" — ya pasó una vez con este
mismo script en `Parte 3` y puede volver a generar confusión si alguien
cambia el `Intensity` del `Light` esperando ver un efecto que en realidad
está gobernado por `_baseIntensity`.

**Método usado para no adivinar posiciones a ciegas:** en vez de tipear
coordenadas a ojo, se generó una cámara temporal top-down (ortográfica,
mirando hacia abajo) y se capturó con `Unity_Camera_Capture` para ver la
forma real del pasillo desde arriba, y después se buscaron por código los
meshes chicos cerca del techo (`Renderer.bounds` chico + `center.y` cerca del
máximo) para encontrar las 9 lámparas reales del modelo sin tener Colliders
(este modelo, como todos los `.glb` de glTFast, no trae Colliders — ver
gotcha #1). Todas las cámaras temporales usadas para diagnóstico y
verificación visual (`__TempTopDownCam`, `__TempPreviewCam*`) se borraron al
terminar — no deberían quedar restos en la escena.

**Pendiente después de este cambio:** esto es solo la atmósfera visual — la
escena sigue sin ningún script de gameplay (gaze, teletransporte, rig de
cámara VR) al momento de aplicar esto. Ver la sección siguiente, donde sí se
agregó el rig.

### Rig de jugador de `Parte 4` (agregado 2026-09-08, también vía Unity MCP)

A pedido de Omar ("agregá la cámara como tiene que quedar en la escena... es
todo dentro del prefab de Player, después separalo"), se instanció
`Assets/Prefabs/Player.prefab` (el que trae Main Camera empaquetada COMO
HIJO de Player — no `Player 1.prefab`, que ya la tiene separada de fábrica) y
después se hizo la cirugía de separación que ya usan `Parte 1`/`Parte 3`:

1. Se borró la Main Camera default/placeholder que tenía la escena.
2. Se instanció `Player.prefab` completo (con Main Camera adentro) en
   `(-47.99, 6.5, -155.70)` — piso real del pasillo (`Y≈4.81`, confirmado
   buscando las losas grandes y chatas del modelo, NO el bound inferior de
   toda la malla que baja hasta `Y≈-4.73` y corresponde a estructura exterior
   no transitable) + altura de ojos (~1.7).
3. **Unity no deja reparentar un hijo dentro de una instancia de Prefab
   conectada** (`Setting the parent of a transform which resides in a Prefab
   instance is not possible`) — hace falta desempaquetarla primero
   (`PrefabUtility.UnpackPrefabInstance(..., PrefabUnpackMode.Completely)`).
   Después de desempaquetar, `Player` en `Parte 4` queda como objetos sueltos
   en la escena, **sin vínculo al asset `.prefab`** (mismo patrón que ya
   tienen `Parte 3` y `Menu Principal`, que tampoco usan un prefab de rig).
4. Con la instancia ya desempaquetada, se sacó `Main Camera` de ser hijo de
   `Player` y pasó a ser un objeto raíz independiente de la escena (
   `SetParent(null, true)`, conserva posición/rotación mundial).
5. Se aplicaron los mismos fixes ya documentados para este patrón:
   - `TrackedPoseDriver.Tracking Type` → **Rotation Only** (si no, con
     Main Camera ya separada de Player, el "casi cero" que reporta Cardboard
     como posición se aplicaría como posición MUNDIAL y mandaría la cámara
     al origen — ver gotcha #8).
   - `PlayerFollowsCamera` agregado a `Player`, con `_mainCamera` apuntando a
     la Main Camera ya separada.
   - `TeleportManager._cameraTransform` (vive en el hijo `TeleportManager`
     de `Player`) apuntando a la Main Camera separada — **hizo falta
     asignarlo a mano por código**, ver el gotcha nuevo más abajo sobre el
     campo con nombre viejo.
   - `Post Processing` tildado en la `UniversalAdditionalCameraData` de la
     Main Camera nueva (si no, el Bloom del `Global Volume` de la sección
     anterior no se ve).
6. Verificado con `Camera.main` (coincide con esta Main Camera por el tag
   `MainCamera`), reticle visible y funcionando, un solo `AudioListener` en
   la escena (no quedó duplicado del placeholder viejo), y capturas desde la
   Main Camera real confirmando el resultado.

**Jerarquía final en `Parte 4`:** `Player` (con `PlayerFollowsCamera`, hijos
`Gaze` → `GazeController`+`GazeReticle`, `VRFadeController`,
`TeleportManager`) y `Main Camera` (raíz aparte, hijos `CardboardReticlePointer`,
`ReticleCanvas` con `DotImage`/`ProgressImage`/`FadeImage`, `Spot Light` del
reticle de Cardboard) — mismo esquema que `Player 1.prefab`, que ya tiene
Main Camera afuera de fábrica.

**Nuevo gotcha descubierto en el proceso — campo con nombre viejo en AMBOS
prefabs de Player:** `TeleportManager._playerTransform` se renombró a
`_cameraTransform` en algún momento del desarrollo, pero **ni
`Player.prefab` ni `Player 1.prefab` tienen el default actualizado** — el
YAML de los dos assets todavía serializa la key vieja `_playerTransform`. Al
instanciar cualquiera de los dos prefabs de cero (como se hizo acá), el
campo `Camera Transform` del Inspector viene **vacío**, y
`TeleportManager` tira su warning
`"Falta asignar Camera Transform en el Inspector"` en cuanto se intenta
teletransportar. **`Parte 1` no tiene este problema** porque en algún
momento se re-asignó a mano en esa instancia puntual (queda como override
en el `.unity` de esa escena, no en el prefab), pero cualquier instancia
NUEVA de cualquiera de los dos prefabs lo va a repetir hasta que se corrija
el asset del prefab en sí (pendiente, no se tocó en esta sesión para no
arriesgar la instancia ya funcionando de `Parte 1`).

### TeleportPoint de `Parte 4` (agregados 2026-09-08)

Se agregaron 9 `TeleportPoint` (`tp1` a `tp9`, agrupados bajo un GameObject
organizador `Teletransportadores`), recorriendo el loop del pasillo más el
tramo en L, todo vía Unity MCP (`Unity_RunCommand`) sobre la escena viva:

- Cada uno es un `Cube` primitivo achatado (`localScale (0.8, 0.05, 0.8)`,
  como una baldosa chata en el piso), con el `BoxCollider` default que ya
  trae el primitivo — no hubo que ajustarlo a mano.
- Layer `Teleport` (9) en los 9, confirmado contra `_interactiveLayerMask`
  del `GazeController` (bits 640 = layers 7 + 9, ya incluye Teleport).
- `_teleportManager` de cada uno apunta al `TeleportManager` real que vive
  bajo `Player` (asignado por código vía `SerializedObject`, no quedó
  ninguno sin asignar).
- **Las posiciones XZ son las mismas que ya se habían usado para las 9
  lámparas de techo del "Ambiente tétrico"** (sección de arriba) — no se
  recalcularon de cero: como esas posiciones ya estaban confirmadas como
  piso real transitable (encontradas buscando las lámparas que cuelgan
  sobre el pasillo), reusarlas evita el riesgo de poner un punto flotando
  fuera de la geometría. Y quedaron a la altura real del piso (`Y≈4.88`,
  losas del modelo, no el bound inferior de toda la malla) en vez de a la
  altura del techo.
- **No tienen `_inactiveMaterial`/`_gazedAtMaterial` asignados** (igual que
  los `tp1`..`tpN` de `Parte 3`, que tampoco los tienen) — el único feedback
  de mirada es el aro de progreso de `GazeReticle`, no un cambio de color
  del punto en sí. Sigue pendiente en la lista general si se quiere ese
  feedback visual extra.
- **No tienen `Destination Override`, audio, ni override de altura** — son
  los 9 puntos "básicos" del recorrido. Faltaría, más adelante, decidir si
  alguno necesita agacharse (huecos bajos) o redirigir a otro punto (una
  puerta), como ya usa `Parte 3`.

**Importante:** `OnEnable`/`RegisterPoint` de `TeleportPoint` NO corren en
el Editor en modo edición (no tienen `[ExecuteAlways]`) — recién se
registran contra el `TeleportManager` cuando arranca el Play/Build. Si algo
"no aparece registrado" mientras se inspecciona la escena sin darle Play,
es esperable, no es un bug.

**Pendiente:** con esto el pasillo ya es recorrible en teoría (spawn cerca
de `tp9`, loop completo + tramo en L), pero falta **probarlo en Play/Build
real** — las posiciones se derivaron matemáticamente de datos de mesh
(`Renderer.bounds`), no de haber caminado la escena, así que conviene que
Omar lo pruebe en el Editor (o compile) antes de darlo por confirmado. Sigue
sin haber ninguna `Door` en esta escena.
- **Materiales PBR de modelos glTF:** el shader `Shader Graphs/glTF-pbrMetallicRoughness`
  expone `Roughness`/`Metallic` en el Inspector del material, NO `Smoothness`.
  Si algo "brilla" o tiene un reflejo especular que rebota raro con el
  movimiento de cabeza en VR, subir `Roughness` (más mate) y/o bajar
  `Metallic` — no buscar un campo `Smoothness` que no está ahí en este
  workflow. Aplicado en `PadLock_sinbrillo.mat` (candado).

## Sistema de puertas y llaves

- **`KeyInventory`** (`Scripts/Interaction/KeyInventory.cs`): guarda si el
  jugador tiene la llave (`HasKey`). Se llena llamando `CollectKey()` desde el
  evento `OnCollected` de un `Collectable` (la llave en sí no necesita script
  propio, solo el componente `Collectable` + Collider + Layer Interactive).
  **No hay ninguna instancia persistente/singleton de esto** (a diferencia de
  `GameSettings`) — cada escena que use llave necesita su propio GameObject
  con `KeyInventory` puesto a mano, y cada `Door` de esa escena tiene que
  apuntarle a ESE mismo objeto en su campo `Key Inventory`.
- **`Door`** (`Scripts/Interaction/Door.cs`): puerta genérica, reusable.
  - `Requires Key` (tildado por defecto): pide `KeyInventory.HasKey` para
    abrirse; si no la tiene, muestra un cartel temporal (`_missingKeySign`).
    Destildado: se abre directo al mirarla (puertas comunes del mapa).
  - **Apertura — posición y rotación vía `Open Position Target` (recomendado):**
    en vez de tipear números de posición/rotación a mano (`Open Local
    Position`/`Open Local Euler Rotation`, ahora legacy), se arrastra a la
    Scene View un objeto vacío hasta el lugar Y ángulo exacto donde tiene que
    terminar la puerta abierta, y se asigna ese Transform en `Open Position
    Target`. En runtime, `Door` convierte la posición Y rotación MUNDIALES de
    ese marcador a los valores LOCALES que corresponden según el padre actual
    de la puerta (`transform.parent.InverseTransformPoint(...)` para
    posición, `Quaternion.Inverse(transform.parent.rotation) * marker.rotation`
    para rotación) — así el resultado en Play/Build es exactamente lo que se
    ve en el Editor, sin importar rotaciones/escalas raras que traiga el
    padre (muy común en modelos importados). Los campos viejos
    (`Open Local Position`/`Open Local Euler Rotation`) se ignoran por
    completo si `Open Position Target` está asignado.
  - Duración de apertura (`_openDuration`) es tiempo fijo en segundos, no una
    velocidad — importante porque unidades locales bajo un padre con escala
    rara pueden representar cualquier distancia real.
  - **`Open()` es pública** justo para que otro script (como `CodeLock`) la
    pueda abrir directo, sin pasar por el chequeo de llave — así una puerta
    doble (dos `Door` independientes) se puede abrir en simultáneo cableando
    `CodeLock.OnUnlocked` a `Door.Open()` en ambas hojas.
  - **Audio (`Audio Source` + `Open Sound`):** son DOS cosas separadas a
    propósito. `Open Sound` es el clip de audio (el archivo). `Audio Source`
    es el COMPONENTE `AudioSource` desde donde suena (no se puede arrastrar
    el clip ahí, tiene que ser un objeto que ya tenga ese componente
    agregado). Si falta cualquiera de los dos, no suena nada y no tira ningún
    error/warning — **error común:** poner el clip en `Open Sound` y olvidarse
    de asignar también un `Audio Source`. Cada puerta necesita su propio
    `AudioSource` con **Play On Awake destildado** (si no, suena solo una vez
    apenas arranca la escena, además de cuando se abre). Solo si dos hojas son
    realmente el mismo vano de puerta (double door real, no el caso de
    `room_bellis_deluxe` que son puertas independientes) conviene compartir un
    único `AudioSource` central entre ambas para que no suene dos veces.
  - **Este bug (Audio Source asignado pero Open Sound vacío, o al revés) se
    repitió confirmado en `Parte 1` Y `Parte 3` (2026-09-08).** En los dos
    casos el patrón fue idéntico: alguien le puso el clip directo al slot
    "AudioClip" del COMPONENTE `AudioSource` (lo que se ve al seleccionar ese
    GameObject en el Inspector) pensando que con eso alcanzaba, pero
    `Door.Open()` nunca lee ese campo — solo lee `_openSound`, el campo propio
    del script `Door`. Si "el sonido de la puerta no suena" y visualmente
    parece que "ya está todo puesto", **lo primero a chequear es si el clip
    está en el lugar correcto (el campo `Open Sound` del componente `Door`,
    no el del `AudioSource`)**, no asumir que el problema es otra cosa.
  - Puede restringirse a que **solo se abra por mirada si el jugador está
    parado sobre un `TeleportPoint` específico** (`_requiredPoint` +
    `_teleportManager`) — esto NO afecta a `Open()` llamado desde afuera
    (como `CodeLock`), que siempre funciona sin importar dónde está el
    jugador.

### `room_bellis_deluxe` — ejemplo real de modelo glTF sin Collider en sus nodos interactivos

Prefab/modelo (`Assets/Modelos 3D/room_bellis_deluxe.glb`) usado en
`Parte 3`, con varias hojas de puerta (`Door`) y al menos un nodo con
`TeleportPoint` (el que redirige adentro del cuarto vía `Destination
Override`) puestos sobre nodos internos del glTF. Ninguno traía Collider (como
es esperable, ver gotcha #1) — hubo que agregar un `Box Collider` a mano en
CADA nodo que tuviera un script `IGazeInteractable` (Unity ajusta el tamaño
solo a la malla real al agregarlo desde el Editor, no se puede reproducir
bien a mano por archivo) Y además poner el Layer correcto (`Interactive` para
las puertas, `Teleport` para el nodo de teletransporte) — ninguno de los dos
pasos alcanza sin el otro. Las puertas de este modelo (`Box047`, `Box036`,
etc.) son piezas independientes, NO una puerta doble — cada una necesita su
propio `AudioSource`, no comparten.

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
dígitos, y los cilindros no respondían a la mirada de cerca. Solución: apagar
el `BoxCollider` de `BaseLP` en `On Examine Start` y reactivarlo en
`On Examine End` (cableado como evento de Inspector, sin código extra).

### Wiring correcto de `On Examine Start` / `On Examine End` (estado final)

**On Examine Start** (mínimo):
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
  consume. Es lo que usan los botones de `MainMenuController` y `ConfigMenu`.
- **`Collectable`** (`Scripts/Interaction/Collectable.cs`): objeto
  recolectable, se desactiva una sola vez al mirarlo y dispara `OnCollected`.
  Es lo que se usa para la llave (junto con `KeyInventory.CollectKey()`
  cableado a `OnCollected`).
- **`SequenceManager` / `SequenceStep`**
  (`Scripts/Interaction/SequenceManager.cs` / `SequenceStep.cs`): puzzle de
  secuencia genérico (mirar N pasos en el orden correcto). Si se falla el
  orden, reinicia todo. No está instanciado en ninguna escena todavía, pero
  el código está listo para usarse en otra parte/puzzle.
- **`GameManager`** (`Scripts/GameManager.cs`): loop mínimo de estado
  (MainMenu → Playing → Victory/Defeat → vuelve a Menu), activando/
  desactivando GameObjects raíz por estado (`_gameplayRoot`, etc.). Nota:
  esto es distinto/anterior al flujo nuevo de `MainMenuController` +
  escenas separadas — confirmar en cada escena cuál de los dos sistemas está
  realmente en uso antes de asumir.
- **`GazeDebugTarget`** (`Scripts/Interaction/GazeDebugTarget.cs`): script de
  prueba viejo (etapa temprana del proyecto, solo loguea los 4 estados de
  gaze por consola). Vestigial, no debería estar en uso real en la escena
  actual — si aparece en algún objeto, probablemente se puede sacar.
- **`CameraDebugLogger`** (`Scripts/Debug/CameraDebugLogger.cs`): script de
  diagnóstico temporal (loguea posición/rotación real de Main Camera + estado
  de XR una vez por segundo). Se usó para encontrar el bug de
  `Tracking Type`. Se puede sacar del objeto `Main Camera` si ya no hace
  falta, pero el archivo no molesta si queda en el proyecto sin usarse.

### Efectos visuales de `Parte 1 - El Despertar` (aplicado 2026-09-08 vía Unity MCP)

A pedido de Omar, se le agregó a `Parte 1` la misma receta de efectos
visuales que ya tenían `Parte 3`/`Parte 4` (fog + Bloom + luces
parpadeantes), pero **con dos ajustes deliberados por las particularidades
de esta escena** — no fue un copy-paste ciego de los valores de Parte 3/4:

1. **Densidad de niebla MÁS BAJA que Parte 3/4 (0.01, no 0.037).** El cuarto
   de `Parte 1` (`the_hospital_4_of_4`) mide `58.90 x 4.38 x 68.71` —
   mucho más chico y con techo mucho más bajo que el pasillo de `Parte 4`
   (`166 x 21.8 x 108.5`). Usar la misma densidad 0.037 en un espacio tan
   chico se ve desproporcionadamente denso incluso cerca. Casualmente, la
   escena ya tenía pre-cargado `fogDensity: 0.01` en `RenderSettings`
   (`fogMode`/`fogColor` también ya estaban puestos) — solo faltaba activar
   el toggle `Fog` en sí, así que se usó ese valor ya existente en vez de
   inventar uno nuevo. **Esto queda relacionado con el pedido pendiente de
   Omar de ajustar/hacer volumétrica la niebla de `Parte 4` porque "es muy
   densa cerca de los objetos"** — cuando se resuelva esa decisión para
   `Parte 4`, conviene revisar si `Parte 1` necesita el mismo tratamiento o
   si esta densidad más baja ya alcanza.
2. **Se agregó una luz práctica NUEVA dedicada al candado
   (`Spot Light (Candado)`, con `FlickeringLight`).** Al bajar
   `ambientIntensity` a 0 (mismo tratamiento que `Parte 3`/`Parte 4`), la
   puerta doble donde está el candado (`tp9`, cerca de `padlock`) quedó
   **completamente negra e invisible** — un problema real, no cosmético: el
   diseño de este puzzle depende explícitamente de que "el candado se ve a
   distancia normal desde el pasillo" (ver sección del candado más abajo) y
   ese distancia estaba fuera del alcance de las 3 luces prácticas que ya
   había cerca del spawn del jugador. Se comprobó con una captura de cámara
   antes/después: antes, negro total; después, la puerta se ve clara con la
   luz nueva. **La luz de examine del candado (`Spot Light (3)`, inactiva
   por defecto, se prende/apaga con `On Examine Start`/`On Examine End`) NO
   se tocó** — sigue siendo un sistema aparte, esta luz nueva solo resuelve
   la visibilidad general del pasillo/puerta antes de entrar en modo
   examinar.

**Receta aplicada (resto, igual que Parte 3/4):**
- `RenderSettings.fog = true` (density/mode/color ya estaban preconfigurados,
  ver punto 1).
- `RenderSettings.ambientMode = Skybox`, `ambientIntensity = 0`.
- La luz Directional existente, `Luz de trabajo` (nombre real en la escena,
  ya bastante tenue: color `(0.223,0.223,0.223)`), puesta en **negro** —
  mismo truco que en Parte 3/4, no se borró el objeto.
- `Global Volume` nuevo, apuntando al mismo
  `Assets/Settings/Global Volume Profile.asset` compartido (Bloom).
- `Post Processing` tildado en la `UniversalAdditionalCameraData` de la
  `Main Camera` de la escena (que ya vivía en la raíz, separada de
  `Player` — esta escena ya tenía la arquitectura correcta de fábrica).
- `FlickeringLight` agregado a las 3 luces prácticas que ya existían cerca
  del spawn (`Spot Light`, `Spot Light (1)`, `Spot Light (2)`, todas ya
  `Spot`, intensity 2) + la luz nueva del candado. **Cuidado si se busca por
  nombre:** hay OTRO objeto también llamado `Spot Light`, hijo de
  `Main Camera` — es el puntero/reticle de Cardboard (intensity 5), no una
  luz de ambiente; no se le tocó nada.

**No se tocó:** `MZ4_Lamp_OFF_m_0` (una `Light` intensity 50 encontrada
colgando del objeto `MZ4`, que está posicionado en el origen del mundo
`(0,0,0)` — bien afuera del cuarto jugable, bounds center
`(-70.48, 1.76, 18.99)`). Parece geometría/luz sin usar o mal ubicada
heredada del asset importado, pero no se tocó porque no se pidió y no afecta
nada visible desde el juego — queda como posible limpieza futura, no
confirmado como bug real todavía.

**Verificado** con capturas desde la Main Camera real: el cuarto del
despertar se ve atmosférico (paredes con textura visible, luces con caída
correcta, Bloom sutil) sin perder legibilidad, y la puerta del candado sigue
siendo encontrable a distancia normal. **La escena quedó con cambios sin
guardar** (`EditorSceneManager.MarkSceneDirty`) — falta que Omar la revise y
guarde con Ctrl+S.

## Paredes grises que no responden a la luz — normales de shading invertidas (`Parte 4`, 2026-09-08)

Omar reportó que, después de armar el ambiente tétrico y el rig, las paredes
de `Parte 4` se seguían viendo "grises, no responden a la iluminación" — un
gris totalmente plano, sin ningún brillo/sombra ni siquiera con una luz muy
cerca. Esto NO era el ambiente tétrico funcionando (fog/ambient en 0 a
propósito) sino un bug real de import del modelo.

**Diagnóstico (método reusable si vuelve a pasar con otro modelo):**
1. Se revisaron los materiales del modelo (`Shader Graphs/glTF-pbrMetallicRoughness`,
   el shader correcto/esperado) — tenían texturas asignadas, colores
   razonables, nada obviamente roto a simple vista.
2. Primera pista (resultó ser un **falso positivo**, ver más abajo): 2 de los
   17 materiales únicos del modelo (`Material.002`, usado en
   `Object_258` — la cáscara COMPLETA de paredes de todo el edificio en un
   solo mesh — y `Material.008`, un pilar) tenían `_WorkflowMode=0` +
   keyword `_SPECULAR_SETUP` activo, mientras que los otros 15 materiales
   (que se ven bien) tienen `_WorkflowMode=1` + keyword apagado. Se
   corrigieron para que coincidan (son cosméticamente más correctos ahora),
   pero **esto NO era la causa real** — después de este fix las paredes
   seguían exactamente igual de planas.
3. **Prueba decisiva:** se puso una `Point Light` de intensidad 80 pegada
   literalmente a 1 unidad de la pared (usando un `Physics.Raycast` con un
   `MeshCollider` temporal para medir la distancia real a la superficie, en
   vez de adivinar — el modelo no tiene Colliders, ver gotcha #1) y **la
   pared no cambió ni un poco** — la prueba definitiva de que el material no
   estaba recibiendo luces en tiempo real para nada, no un problema de
   "está muy oscuro" o "el fog tapa todo".
4. **Causa real encontrada:** se comparó, en el punto exacto de impacto del
   raycast, la normal FÍSICA (la que calcula el `MeshCollider` a partir del
   winding real del triángulo) contra la normal de SHADING (la guardada en
   `Mesh.normals`, la que usa el shader para calcular luz). Resultado:
   **apuntan exactamente al revés** (`Vector3.Dot` = `-0.9999`, o sea 180°
   invertidas) tanto en `Object_258` (la cáscara de paredes) como en
   `Object_178` (el pilar) — los dos casos que habían aparecido como
   "sospechosos" en el paso 2 (correlación real, aunque la causa
   subyacente no tenía nada que ver con el workflow mode). Con las
   normales de shading apuntando hacia AFUERA del pasillo en vez de hacia
   adentro, cualquier luz puesta adentro del pasillo calcula
   `dot(normal, direccionALaLuz)` negativo siempre → se clampea a 0 → la
   superficie se renderiza como si no hubiera ninguna luz, sin importar
   cuán cerca o intensa sea.
5. **Fix aplicado:** se invirtieron las normales (y la dirección de las
   tangentes) de esos 2 meshes, por código, tomando `meshFilter.mesh` (NO
   `sharedMesh` — esto clona la malla para esa instancia puntual en la
   escena) y multiplicando cada normal por `-1`. **Este fix vive SOLO en la
   escena `Parte 4`**, no toca el archivo `.glb` original
   (`Assets/Modelos 3D/house_corridor_interior.glb`) — si ese modelo se
   usa en otra escena a futuro, o si se reimporta, va a traer las normales
   invertidas de nuevo y hay que repetir este mismo fix (o, mejor, corregir
   las normales en el software 3D de origen antes de re-exportar el
   `.glb`, que sería la solución definitiva en vez de este parche
   por-instancia).
6. Verificado con capturas antes/después desde la Main Camera real: antes,
   gris totalmente plano incluso con la luz de prueba pegada; después, se ve
   claramente la textura de la pared (un estampado de rombos) con una
   iluminación que cae de forma correcta alrededor del punto de luz.

**Para el futuro:** si aparece otra superficie que se ve "plana, gris, no
reacciona a ninguna luz" (no solo en `Parte 4` — cualquier modelo
`.glb`/`.fbx` importado), el mismo método sirve: agregar un `MeshCollider`
temporal, `Physics.Raycast` para conseguir el punto y la normal física real,
comparar contra `mesh.normals` en ese mismo triángulo (`hit.triangleIndex`).
Un `Vector3.Dot` negativo confirma normales invertidas — no hace falta
adivinar mirando capturas de pantalla.

## Gotchas / lecciones aprendidas (leer antes de tocar cosas relacionadas)

1. **glTFast y FBX no generan Colliders automáticamente.** Cualquier pieza
   nueva que deba reaccionar a la mirada necesita que le agreguen un
   `Collider` a mano, además del `Layer` correcto. El Collider auto-ajustado
   a la malla real SOLO se puede conseguir agregándolo desde el Editor de
   Unity (`Add Component > Box Collider`) — no es reproducible bien editando
   el archivo `.unity` a mano, porque Unity calcula el tamaño en base a la
   geometría real de la malla, que no está disponible fuera del Editor.
2. **El `Layer` es un gate independiente del Collider/script.** Un objeto
   puede tener Collider + el script de `IGazeInteractable` correcto y
   *igual* no reaccionar si su Layer no está incluido en el
   `_interactiveLayerMask` de `GazeController`. Esto fue la causa real de
   varios "no me interactúa" a lo largo del proyecto — incluyendo un caso
   donde se agregó el Collider correctamente pero se olvidó el paso del
   Layer, y siguió sin funcionar hasta corregir eso también.
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
   poder editar sliders como `Roughness`/`Metallic`.
6. **El shader de glTFast (`Shader Graphs/glTF-pbrMetallicRoughness`) usa
   `Roughness`, no `Smoothness`** (workflow glTF estándar). Si hay que sacar
   brillo especular que "rebota" con el movimiento de cabeza en VR, subir
   `Roughness` (más mate) y/o bajar `Metallic`, no buscar `Smoothness`.
7. **Un Canvas para HUD/VR tiene que ser World Space, hijo de la cámara,**
   nunca Screen Space Overlay (no renderiza bien en estéreo Cardboard). Y
   además, si hay geometría que puede quedar muy cerca de la cámara, sus
   elementos necesitan el material `UI_AlwaysOnTop` para no ser tapados por
   depth-testing (ver sección de gaze más arriba).
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
    especulando. Varios de los `Debug.Log` puestos en `Door.cs`/
    `GazeController.cs` para diagnósticos anteriores siguen activos a
    propósito — son baratos y ya sirvieron más de una vez para el bug
    siguiente.
13. **Un cambio en la escena/Inspector que "no aparece" del lado de la IA
    casi siempre es porque no se guardó (`Ctrl+S`) en el Editor.** Pasó
    reiteradas veces: Omar agrega un componente o cablea algo, pero como los
    cambios en el Editor solo viven en memoria hasta guardar, el archivo
    `.unity` en disco (lo único que la IA puede leer) sigue mostrando el
    estado viejo. Si algo "debería estar ahí" y no aparece al revisar el
    archivo, lo primero a preguntar es si se guardó la escena.
14. **Después de un cambio de CÓDIGO (`.cs`), hay que recompilar/re-buildear
    el APK para que se note en el celular.** Cambiar la escena (por ejemplo
    asignar un campo nuevo del Inspector) no alcanza si el build instalado es
    de antes de ese cambio de código — el juego sigue corriendo la lógica
    vieja aunque el campo nuevo ya esté asignado en la escena. Si un fix de
    código "no hizo nada" en el celular, confirmar primero si se compiló un
    build nuevo después del cambio.
15. **Antes de asumir que algo es un bug nuevo, revisar si ya está
    parcialmente armado por el compañero.** Pasó con el Volume de Bloom
    (existía el Profile vacío, sin Volume en ninguna escena todavía) y con
    `FlickeringLight` (el script ya estaba, solo faltaba ponerlo en el objeto
    correcto). También pasó al revés: `MainMenuController.cs` llamaba a un
    método `VRFadeController.FadeToBlack()` que **no existía todavía** en
    `VRFadeController.cs` — un error de compilación real que bloqueaba todo
    el proyecto hasta que se agregó el método (ya solucionado). Si el
    proyecto no compila, revisar primero si hay una llamada a un método que
    un script todavía no tiene.
16. **Una superficie "gris plana que no responde a ninguna luz" (ni siquiera
    una luz muy intensa pegada encima) casi seguro son normales de shading
    invertidas, no un problema de material/shader.** Pasó en `Parte 4`
    (`house_corridor_interior.glb`, ver sección dedicada más arriba) — el
    shader y las texturas estaban bien, pero `Mesh.normals` apuntaba 180°
    opuesto a la normal física real del triángulo, así que
    `dot(normal, luz)` daba negativo siempre y se clampeaba a 0. Se
    diagnostica agregando un `MeshCollider` temporal + `Physics.Raycast`
    para conseguir la normal física real, y comparándola contra
    `mesh.normals` en ese mismo triángulo (`hit.triangleIndex`) — un
    `Vector3.Dot` negativo lo confirma. El fix (invertir `mesh.normals`) hay
    que aplicarlo sobre `meshFilter.mesh`, no `sharedMesh`, para no tocar el
    asset `.glb`/`.fbx` original.

## Cómo se trabaja en este proyecto (para una IA sin acceso directo al Editor)

- El acceso a los archivos es vía un bridge de archivos remoto (no hay
  `device_bash` para esta máquina — solo listar/leer/escribir archivos
  puntuales).
- Los cambios de **código** (`.cs`) se escriben directamente y se depositan
  en el proyecto — Omar no tiene que tipearlos.
- Los cambios de **configuración de escena/Inspector** (crear GameObjects,
  arrastrar referencias, tildar checkboxes, ajustar Transforms a ojo,
  extraer materiales, agregar Colliders ajustados a una malla, etc.) los
  tiene que hacer Omar a mano en el Editor, siguiendo instrucciones **muy
  concretas y en orden** (nombre exacto del menú/botón, qué arrastrar dónde)
  — instrucciones vagas generaron confusión varias veces a lo largo del
  proyecto. Cambios puramente mecánicos en el YAML de la escena (asignar una
  referencia ya existente, corregir un valor, agregar un componente sin
  datos calculados por el Editor) sí se hicieron directamente por archivo
  cuando tenía sentido, con cuidado de no pisar cambios sin guardar de Omar.
- Para probar en el celular real: Omar compila e instala el APK, y si hace
  falta diagnosticar algo en tiempo real, conecta el celular por USB y corre
  `adb logcat -s Unity` mientras prueba.

## Estado actual (a esta fecha) y pendientes conocidos

### Estado real por escena (confirmado 2026-09-08 leyendo los `.unity`, no por descripción)

| Escena | Tamaño | Estado |
|---|---|---|
| `Menu Principal.unity` | 243 KB | **Completa.** Paneles, `MainMenuController`, `ConfigMenu`/`GameSettings`, secuencia de entrada con puerta, `FlickeringLight`, Bloom. |
| `Tutorial.unity` | 75 KB | **Esqueleto, no un nivel real todavía.** Tiene un `Cartel`, 2 `Cube`, 8 `Teletransportadores` y un `Key inventory` — sin candado ni otro puzzle. Usa `Player.prefab` (rig propio, ver sección de rig fragmentado). |
| `Parte 1 - El Despertar.unity` | 219 KB (+efectos visuales 2026-09-08) | **Completa** (candado, puertas, teletransporte) y ahora con la misma receta de efectos visuales que `Parte 3`/`Parte 4` (fog, Bloom, luces parpadeantes) — ver sección "Efectos visuales de Parte 1" más abajo. Usa `Player 1.prefab`. |
| `Parte 2 - Cumpleaños.unity` | 12 KB | **Vacía.** Solo `Directional Light` + `Main Camera` default de Unity, nada más. |
| `Parte 3 - Pasillo de hotel.unity` | 669 KB | **La más trabajada activamente.** 4 `Door` + 6 `TeleportPoint` tipo puerta (fade) con audio ya corregido (ver gotcha de arriba), geometría de `hallway_hotel`, `custom_brown_axminster_carpet_hotel_room`, `hayama_washitsu_raw_scan` y `room_bellis_deluxe`. Rig de cámara propio (no prefab). |
| `Parte 4 - Pasillo de madera.unity` | 693 KB (+ambiente, rig y TP 2026-09-08) | **Ambiente tétrico, rig de jugador y 9 `TeleportPoint` puestos; falta `Door` y probarlo en Play real.** `Player` + `Main Camera` separada, gaze funcionando, y ahora un recorrido completo de teletransporte (`tp1`-`tp9`) por el loop del pasillo + el tramo en L. Sin ninguna `Door` todavía. Ver secciones "Ambiente tétrico", "Rig de jugador" y "TeleportPoint de Parte 4" más abajo. |
| `Parte 5 - Backrooms.unity` | 12 KB | **Vacía**, igual que Parte 2. |
| `Parte Final.unity` | 12 KB | **Vacía**, igual que Parte 2. |

Build Settings (`ProjectSettings/EditorBuildSettings.asset`) a esta fecha:
**solo `Parte 3` está tildada (`enabled: 1`)** — todas las demás (incluida
`Menu Principal` y `Tutorial`) están agregadas a la lista pero destildadas.
Un build real del juego hoy no incluiría el menú ni el resto de las escenas.

**Funcionando / implementado:**
- Sistema de gaze (raycast, reticle centrado, shader always-on-top para
  reticle y fade contra geometría cercana).
- Teletransporte (caminata + fade, fade forzado en puntos con Destination
  Override), con Main Camera separada de Player. Audio opcional en
  TeleportPoint (selección) y vía On Player Arrived (llegada).
- Puertas con llave (genérico), audio de apertura, posición/rotación de
  apertura vía objeto marcador (`Open Position Target`). Puertas dobles
  abiertas en simultáneo desde `CodeLock`.
- Candado de combinación completo (solo en `Parte 1`).
- Menú principal funcional de punta a punta.
- Bloom + `FlickeringLight` en Menú Principal.
- `Parte 3`: puertas físicas y teletransportes-puerta con audio funcionando
  (arreglado 2026-09-08).
- Unity MCP configurado (ver "Entorno técnico") — pendiente confirmar que la
  conexión con Claude Code funciona en la práctica (Omar aceptó el "Pending
  Connection" el 2026-09-08, falta la primera prueba real post-reinicio).

**Pendiente / a implementar (por orden de lo más bloqueante):**
- **`Parte 2`, `Parte 5` y `Parte Final` están completamente vacías** — ni
  siquiera tienen geometría todavía, mucho menos gameplay. Son las partes
  más grandes de trabajo que faltan del proyecto.
- **`Parte 4` ya tiene rig de cámara, ambiente y 9 `TeleportPoint`** (ver
  sección dedicada) — sigue sin ninguna `Door`, y falta **probarlo en
  Play/Build real** en el Editor: las posiciones de los `tp` se calcularon
  matemáticamente a partir de la geometría, no caminando la escena.
- **`Tutorial` es un esqueleto** — tiene teletransportes y un inventario de
  llave puestos, pero no queda claro qué mecánica enseña todavía; confirmar
  con Omar el diseño antes de asumir que falta "completarlo" sin más.
- **Build Settings solo tiene `Parte 3` habilitada** — antes de compilar un
  build real hay que decidir el orden final de escenas y tildar las que
  correspondan (`enabled: 1`).
- Unificar el rig de jugador (ver sección dedicada más arriba) — hoy hay 5
  configuraciones independientes (`Player.prefab`, `Player 1.prefab`, rig
  suelto en `Parte 3`, rig suelto en `Menu Principal`, rig suelto en
  `Parte 4`) que pueden divergir silenciosamente, como ya pasó con
  `Tracking Type`.
- **Corregir el campo `_playerTransform` → `_cameraTransform` desactualizado
  en `Player.prefab` y `Player 1.prefab`** (descubierto 2026-09-08 al armar
  el rig de `Parte 4`) — el default serializado en los dos assets todavía
  tiene el nombre viejo del campo de `TeleportManager`, así que cualquier
  instancia nueva de cualquiera de los dos prefabs necesita que alguien
  re-arrastre `Main Camera` a mano en `Camera Transform`, o va a tirar el
  warning de "Falta asignar Camera Transform" al primer teletransporte. No
  se tocó el asset en esta sesión para no arriesgar la instancia ya
  funcionando de `Parte 1` sin poder probarla en el Editor en el momento.
- Iluminación final del modo examinar del candado (Omar planea rehacerla).
- Confirmar que el texto/textura de `sticky_notes` realmente muestre "067"
  legible.
- `Requires Key` en las puertas que correspondan — confirmar que sigue en
  `true` donde hace falta.
- Materiales de `_inactiveMaterial`/`_gazedAtMaterial` en varios objetos
  (candado, dígitos, botón de volver, puertas de `Parte 3`) — quedaron
  opcionales, puede que falten para tener feedback visual de mirada.
- Colliders + Layer en el resto de la geometría de `Parte 3` y otras escenas
  que use gaze — no está cubierto todo, solo lo que se fue reportando.
- Etapas de puzzle 2/3/4/5 mencionadas en el diseño original (pinturas que
  se dan vuelta, contador de fragmentos, etc.) — todavía no instanciadas en
  escena, aunque `SequenceManager`/`SequenceStep`/`Collectable` ya están
  listos como building blocks genéricos para varias de ellas.
- Pase de rendimiento para Android/A54 (nivel final) — Bloom es barato pero
  vale la pena confirmar que no afecte el framerate en el celular real.
- Sacar `GazeDebugTarget` y `CameraDebugLogger` de la escena una vez que ya
  no se necesiten para diagnosticar (los archivos no molestan si quedan sin
  usar, pero no deberían tener componentes activos en objetos de juego).
- Confirmar si `GameManager` (loop de estado viejo) sigue en uso real en
  alguna escena, o si quedó reemplazado por el flujo nuevo de
  `MainMenuController` + escenas separadas — no queda claro cuál es la
  fuente de verdad actual.
