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

## Historia del juego — "Recuerdos Rotos" (guion oficial, recibido 2026-09-23)

**Esta es la fuente de verdad del diseño.** Todo trabajo nuevo en una escena
"Parte N" tiene que servir a lo que dice su etapa acá. Si algo ya implementado
contradice la historia, **no lo borres ni lo cambies por tu cuenta**: anotalo
en "Decisiones abiertas" y consultá a Omar.

**Premisa:** el protagonista tiene amnesia severa por un evento traumático.
Cada mapa es un **recuerdo distorsionado**, no un edificio real. El
**monstruo** es la culpa/trauma que la mente bloquea. Al resolver un puzzle
(o asustarse demasiado) el cerebro colapsa: **desmayo brusco a negro** y
despierta de golpe en el mapa siguiente. Ese desmayo es la transición
estándar entre TODAS las etapas.

| Etapa | Escena | Puzzle según guion | Transición según guion |
|---|---|---|---|
| 1. La Habitación (El Despertar) | `Parte 1 - El Despertar` | Arranca a oscuras con latidos acelerados, despertás de pie. Nota con manchas de tiza + reloj parado (ej. 03:15) → combinación de la **caja fuerte del armario**. | Al abrirla: **foto vieja tachada**, la luz parpadea, **susurro al oído** (audio 3D), negro con golpe seco. |
| 2. Salón de Cumpleaños | `Parte 2 - Cumpleaños` | Reventar **globos en el orden de la edad pasada** (pista escrita en la pared). | El **monstruo** observa desde el fondo durante el puzzle. Al último globo, la música festiva se distorsiona, el monstruo corre hacia vos y te desmayás justo antes de que te toque. |
| 3. Pasillo de Hotel (La Persecución) | `Parte 3 - Pasillo de hotel` | Pasillo "interminable". Mirar por las **mirillas** de las puertas; en una se ve **luz roja y una llave** en el suelo. Monstruo caminando lento detrás. | Con la llave se abre la puerta del fondo: **abismo negro**, al dar un paso caés y perdés el conocimiento. |
| 4. Pasillo de Madera sin Puertas (Claustrofobia) | `Parte 4 - Pasillo de madera` | **Loop**: caminar hacia adelante te devuelve al mismo punto. Encontrar una **pintura al revés**, enderezarla y **caminar de espaldas** hacia la pared ciega. | La pared de madera se desmorona y revela una **luz amarilla cegadora** que te absorbe. |
| 5. Backrooms (La Revelación) | `Parte 5 - Backrooms` (+ `Parte Final`?) | Despertás tirado en el piso, zumbido ensordecedor de luces. Juntar **3 fragmentos de la nota inicial** y llevarlos al **centro del mapa**. | El monstruo aparece, **se quita la máscara**: era tu mente protegiéndote. Parpadeo rápido, **pitido de monitor de hospital**, fin. |

### Estado real vs. guion (análisis 2026-09-23, actualizado al cierre de la sesión 2026-09-23)

El análisis de la mañana tenía errores (se leyeron mal Parte 2, 3, 5 y
Final); acá queda la versión corregida después de la ronda de agentes. Los
números `#N` son de "Decisiones abiertas", más abajo.

- **Etapa 1 — candado en la puerta (decisión #1) y desmayo YA cableado.**
  `Parte 1` tiene el candado de 3 dígitos en la puerta doble (código `067`,
  pista en `sticky_notes`), que por decisión de Omar se queda (no caja
  fuerte). **Hecho 2026-09-23:** `CodeLock.OnUnlocked → FaintTransition.Trigger`
  hacia `Parte 2 - Cumpleaños` (ver "Transiciones desmayo"). Falta del guion:
  reloj parado, nota con tiza, foto tachada, latidos al arrancar y el clip del
  susurro 3D (el campo existe en `FaintTransition`, el clip no). Ojo: `067`
  no es una hora, así que si el reloj tiene que ser la pista hay que cambiar
  el código (#8). Assets que ya están en el proyecto y sirven:
  `horror_room.glb` ("La Habitación": reloj de pie con agujas separadas
  `hours_6`/`minuts_15`, armario `almirah`, cuadro `frame_61`, ya se usa en
  el Menú) y `crime_scene_tape__sign_with_chalk_body_outline.glb` (silueta de
  tiza).
- **Etapa 2 — del compañero (decisión #2), no tocar.** Corrección: la escena
  **no está vacía**, tiene `rec_room_-_backstage_of_reality_-_level_fun.glb`,
  pero **no tiene rig** (sin `TrackedPoseDriver`/`GazeController`): después del
  desmayo de Parte 1 la cabeza no rota y no hay salida. Además ese modelo pesa
  451.288 triángulos en 5.258 mallas (~10.500 draw calls por frame en
  multi-pass), no es viable en el A54 tal cual. Falta que termine con un
  `FaintTransition` hacia `Parte 3 - Pasillo de hotel`. Todo esto hay que
  avisárselo al compañero (#19). `SequenceManager`/`SequenceStep` siguen
  siendo lo indicado para "globos en orden".
- **Etapa 3 — la llave y la puerta del fondo YA estaban; el abismo quedó
  cableado.** Corrección: Parte 3 ya tenía `KeyInventory` + `key_with_tag`
  (`Collectable → CollectKey`) dentro de `hayama_washitsu_raw_scan` (se entra
  por la puerta-teletransporte `1_3`) y la puerta del fondo
  `hallway_hotel/doors/27_2` con `Requires Key`. **Hecho 2026-09-23:**
  `AbyssFall` + `tp pozo` + `Desmayo -> Parte 4` detrás de `27_2`. Código
  listo pero sin instanciar: `Peephole` (mirillas) y `MonsterController`.
  Falta: poner las mirillas (en qué puertas, #10), la luz roja sobre la llave
  (receta en "Puzzles nuevos"), el monstruo acechando (#9) y probar en Play.
- **Etapa 4 — pared que se desmorona hecha y probada en Play; loop y pintura
  con código listo, sin cablear.** `WallCollapse` + `Desmayo -> Parte 5`
  funcionan (el bug de la pared invisible se resolvió y el arreglo ya está
  guardado). `UpsideDownPainting` y `TeleportPoint.Seamless Loop`
  existen, con propuestas de ubicación (#11, #12). `tp pared ciega` sigue
  siempre activo; con la pintura tiene que arrancar apagado (ojo con el
  gotcha #20: destildar el componente no alcanza).
- **Etapa 5 — geometría sí, rig no.** Corrección: no está vacía, tiene
  `backrooms_vr` en `(-46.8, 1.26, -580.2)`, rot X -90, pero sin
  Player/gaze/tp, así que **toda la etapa está bloqueada** hasta que alguien
  arme el rig (#14). `backrooms_vr.glb` es unlit con luz horneada (gotcha
  #19): el parpadeo fluorescente va con `FluorescentFlicker`, no con `Light`.
  Código listo: `FragmentCounter` + `FragmentDropOff`; los 3 fragmentos
  pueden ser las 3 notas de `Assets/Models/sticky_notes.glb`
  (`pPlane3/4/5`). Revelación: `MonsterController.Reveal`/`RemoveMask`;
  parpadeo rápido final: `ScreenBlink.RapidBlinks`.
- **`Parte Final`:** tampoco está vacía (`kidman_room`, 116 texturas, ~272 MB
  estimados sin comprimir), sin rig. Qué la dispara sigue abierto (#5).
- **Transversal — monstruo:** el **código** ya existe (`MonsterController`,
  hoy con una cápsula de placeholder); el **modelo** no (#3). Presupuesto de
  polígonos/texturas para el A54 en "QA de build Android / A54".
- **Transversal — transición "desmayo": hecha** (`FaintOverlay` +
  `FaintTransition`) y cableada en 1→2, 3→4 y 4→5. Tutorial→Parte 1 sigue
  con `LevelDoor` + `SceneTransitionOverlay` del compañero, y qa-performance
  confirmó leyendo el código de URP 17 que **un Canvas Screen Space Overlay no
  se dibuja en los ojos con XR activo** → casi seguro no se ve en Cardboard
  (#19). 2→3 (compañero) y 5→Final: sin nada todavía.
- **Transversal — audio narrativo: sigue casi inexistente.** No se importó
  ningún clip nuevo. audio-integrator dejó la lista de ~30 clips faltantes con
  su destino exacto (script + campo), el script `AudioFader` y tres bugs de
  audio en escenas (ver "Audio: AudioFader y bugs encontrados").
- **`SceneNarration`** (`Assets/Scripts/Ambient/`, del compañero, usado en
  `Tutorial`): reproduce una locución 2D una vez al arrancar la escena, con
  subtítulo TMP opcional. Reusable para los latidos del inicio de `Parte 1`
  o voces internas del protagonista.

### Decisiones abiertas (las decide Omar, no un agente)

1. **Etapa 1:** ¿se reemplaza el candado de la puerta doble por la caja
   fuerte del armario, o el candado actual "pasa a ser" la caja fuerte
   (mismo sistema, cambiar código a la hora del reloj)?
2. **`Tutorial`** no aparece en el guion. ¿Queda como escena previa fuera de
   la historia, o se funde con el despertar de `Parte 1`?
3. **Modelo del monstruo** (con máscara removible para el final): no hay
   ningún asset. ¿De dónde sale y con qué animaciones (caminar, correr,
   quitarse la máscara)?
4. **"Caminar de espaldas" en Cardboard:** no hay caminata libre, solo
   teletransporte por mirada. Propuesta a validar: el paso cuenta como "de
   espaldas" si al teletransportarse hacia la pared ciega la cámara mira en
   sentido contrario al movimiento (para eso hay que poder elegir el punto sin
   mirarlo, por ejemplo un punto que se activa con un disparador por
   posición y no por mirada).
5. **`Parte Final`:** ¿es la escena del final de la etapa 5 (revelación +
   pitido de hospital), o se borra y todo pasa en `Parte 5`? *(Ampliada
   2026-09-23)*: ¿qué dispara el paso Parte 5 → Final (entregar los
   fragmentos, la revelación del monstruo)? ¿Se vuelve al Menú al terminar?
6. **Texto de transición:** ¿el desmayo muestra texto de nivel ("PARTE 2 —
   CUMPLEAÑOS") como hace hoy `SceneTransitionOverlay`, o es un corte a
   negro seco sin texto como describe el guion? *(2026-09-23: el toggle ya
   existe en `FaintTransition` — `Show Level Text`, apagado por defecto — así
   que es solo decidir y tildar.)*

> #1, #2 y #4 ya están resueltas (ver "Decisiones de Omar" más abajo). Las
> que siguen salieron de la ronda de agentes del 2026-09-23.

7. ~~**Parte 4 tenía cambios SIN guardar de origen desconocido**~~ —
   **resuelta por evidencia (2026-09-24):** en `Editor-prev.log` el guardado de
   `Parte 4` y de `EditorBuildSettings.asset` (21:14:03) aparece justo antes de
   "About to save layout" y del apagado del Editor, o sea que Omar cerró Unity
   y eligió **Save** en el diálogo de salida. La escena quedó guardada con el
   arreglo de la pared y con esos cambios previos. Si no era lo que quería,
   revertir con git y reaplicar el arreglo (valores en "Transiciones desmayo").
8. **Etapa 1:** ¿va el reloj parado del guion? El código `067` no es una
   hora: ¿se cambia a `315` (03:15)? ¿Nota con tiza? ¿Foto tachada?
9. **Monstruo en la etapa 3:** ¿empieza a acechar (`Stalk`) al agarrar la
   llave (propuesta: 5 m, 0.9 m/s)? Durante la caída al abismo, ¿mira o
   desaparece?
10. **Mirillas (etapa 3):** propuesta "opción A" = la llave se ve primero
    desde una mirilla, lo que obliga a cerrar la puerta-tp `1_3` (apagar su
    `TeleportPoint` Y su `BoxCollider`, ver gotcha #20). ¿Qué otras puertas
    llevan mirilla falsa (ciega)?
11. **Loop de la etapa 4:** ¿`tp8-9` → `tp9` con `Seamless Loop`
    (alternativa `tp6` → `tp5-6`)? ¿Se apaga el loop al enderezar la pintura?
12. **Pintura al revés (etapa 4):** ¿el cuadro de la cabaña (`frame_61`) en
    la pared x=-27.90? ¿Visible desde el inicio o recién después de N vueltas
    del loop?
13. **Luz cegadora de Parte 4:** hoy satura a blanco total ~1.5 s antes del
    desmayo y pierde el amarillo. ¿Se baja `Glow Max` (12) y
    `Light Max Intensity` (60)?
14. **Etapa 5:** ¿quién arma el rig de `Parte 5` (y de `Parte Final`)? ¿Qué
    texto revela cada fragmento de la nota?
15. **Gaze a través de paredes:** ¿se agregan Colliders a las paredes + un
    `Occluder Layer Mask` en `GazeController` para que no se puedan elegir
    `tp` a través de una pared (gotcha #24)?
16. **`Mobile_Renderer`:** ¿pasar a Forward+ o subir el tope a 8 luces por
    objeto (gotcha #18)? Decidir después de medir FPS en el A54.
17. **Fotosensibilidad:** ¿se pone un aviso de epilepsia al inicio?
    `FlickeringLight.Burst Step Seconds` ¿0.05 (5–10 Hz, actual) o 0.17
    (más lento, más seguro)?
18. **Audio:** ¿se juega con auriculares en la feria (el susurro 3D depende
    de eso)? ¿Quién graba el susurro? ¿Pitido de monitor regular o línea
    plana? ¿`Doppler Factor = 0`? ¿Se borra el `AudioSource` de `FadeImage` en
    Parte 3 y se arregla el doble sonido del candado de Parte 1?
19. **Cosas del compañero (consultarle, no tocar):** `Tutorial → Parte 1` usa
    `SceneTransitionOverlay` (Screen Space Overlay, casi seguro invisible en
    Cardboard): ¿lo cambia por `FaintTransition`/`FaintOverlay`? Avisarle que
    `Parte 2` necesita rig, un `FaintTransition` final hacia
    `Parte 3 - Pasillo de hotel`, y que `rec_room…` es demasiado pesado para
    el A54.
20. **Texturas embebidas en `.glb`:** probablemente ~1 GB sin comprimir en el
    APK. ¿Se reexportan como `.gltf` con imágenes externas o se bajan a 512?
21. **Perfil de post-procesado compartido** (`Global Volume Profile`, lo usa
    también el compañero): ¿se apagan Screen Space Lens Flare y Chromatic
    Aberration y se baja la calidad del Bloom (ver "QA de build Android")?
22. **Iluminación de Parte 3** (escena muy trabajada, pedir antes de tocar):
    ¿Directional negra en `No Shadows`, HDR prendido en la cámara (también
    Parte 1) y `reflectionIntensity = 0`?

### Reparto de trabajo entre agentes (`.claude/agents/`)

| Agente | Tareas de la historia |
|---|---|
| `ui-menu-flow` | Transición "desmayo" genérica y VR-safe (reusar `SceneTransitionOverlay`/`VRFadeController`, llamable desde `UnityEvent`, con sonido opcional de golpe seco); encadenar el orden Menú → (Tutorial?) → Parte 1…5 → Final en Build Settings. |
| `puzzle-designer` | Etapa 1 caja fuerte + reloj + foto; etapa 2 globos con `SequenceManager`; etapa 3 mirillas + llave; etapa 4 pintura al revés; etapa 5 contador de 3 fragmentos y entrega en el centro. |
| `gameplay-core` | Monstruo (observar, caminar lento detrás, correr hacia el jugador, aparecer al final); loop sin fade de la etapa 4 (hoy `Destination Override` fuerza fade); detección de "caminar de espaldas"; caída al abismo de la etapa 3. |
| `visual-fx` | Parpadeo forzado de luces en los momentos de transición (`FlickeringLight` hoy solo es aleatorio); luz roja de la mirilla; pared que se desmorona + luz amarilla cegadora; ambiente fluorescente de Backrooms; parpadeo rápido final. |
| `audio-integrator` | Lista de clips que faltan; latidos al inicio, susurro 3D al oído, música de cumpleaños + distorsión, pasos del monstruo, zumbido de Backrooms, pitido de hospital. |
| `qa-performance` | Confirmar en el A54 si `SceneTransitionOverlay` (Screen Space Overlay) se ve en Cardboard; Build Settings completo; rendimiento de `backrooms_vr.glb` y del monstruo. |
| `scene-auditor` | Mantener esta sección al día: mover cada fila de "Estado real vs. guion" a medida que se implementa, y registrar las decisiones abiertas cuando Omar las resuelva. |

**Primera ronda completa (2026-09-23):** los 7 agentes entregaron. El código
nuevo compila en el Unity de Omar (consola sin errores ni warnings,
verificado vía Unity MCP). Lo de cada uno quedó en: "Transiciones desmayo"
(ui-menu-flow, visual-fx, gameplay-core), "Loop sin fade" (gameplay-core),
"Monstruo", "Puzzles nuevos" (puzzle-designer), "Efectos visuales"
(visual-fx), "Audio: AudioFader y bugs encontrados" (audio-integrator) y
"QA de build Android / A54" (qa-performance).

### Decisiones de Omar (2026-09-23)

- #1 → la etapa 1 **se queda con el candado en la puerta** (no caja fuerte).
- #2 y etapa 2 → `Tutorial` y `Parte 2 - Cumpleaños` **los desarrolla el
  compañero**: no tocarlos.
- #4 → "caminar de espaldas" = **alcanza con llegar a un tp específico**,
  sin chequear hacia dónde mira.

### Transiciones "desmayo" — estado al cierre de la sesión 2026-09-23

Código en `Assets/Scripts/Transitions/` (compila sin errores ni warnings):
- **`FaintOverlay`**: fundido que sobrevive el cambio de escena
  (`DontDestroyOnLoad`). Es una **esfera alrededor de la cámara**, no un
  Canvas, porque un Canvas Screen Space Overlay no se ve en Cardboard (gotcha
  #7) y uno World Space hijo de la cámara moriría con la escena vieja. Carga
  con `LoadSceneAsync` y "abre los ojos" con parpadeo en la escena nueva; la
  escena destino no necesita nada puesto (sirve igual para las del
  compañero). No se pone a mano: lo crea `FaintTransition`. **Reescrito por
  ui-menu-flow (2026-09-23), API compatible** (`Play(Settings)`,
  `IsRunning`; `Settings` suma `PreDelay`, `ShowText`, `Title`, `Subtitle`):
  - Cadena de shaders de respaldo: `Overlay Material` → `UI/AlwaysOnTop` →
    `UI/Default` con ZTest Always (está en Always Included Shaders, fileID
    10770, verificado) → `Hidden/Internal-Colored`; si no hay ninguno,
    loguea error. Existe porque `Shader.Find` puede devolver null en el APK
    (gotcha #25).
  - Sigue a `Camera.main` (o a la primera cámara activa si todavía no hay
    una) y se reubica sobre cada cámara justo antes de dibujar.
  - `IsRunning` es `true` desde el disparo y se reinicia al entrar a Play.
    Limpia su material/mesh, la esfera se apaga mientras es transparente, y
    la mirada queda bloqueada en la escena nueva hasta terminar el parpadeo.
- **`FaintTransition`**: uno por escena, `Trigger()` desde un `UnityEvent`.
  Bloquea el gaze, `FlickeringLight.Burst()` en las luces asignadas, susurro
  3D al oído, espera `Pre Delay` y llama a `FaintOverlay`. Cambios del
  2026-09-23: `Trigger()` ya no usa corrutinas propias (el `Pre Delay` corre
  dentro del overlay y el susurro usa `PlayDelayed`), así que **funciona
  aunque su propio objeto se apague en el mismo evento** (por ejemplo, un
  `SetActive(false)` en la misma lista de `On Unlocked`). Valida la escena con
  `Application.CanStreamedLevelBeLoaded`: si no está en Build Settings tira
  error y no dispara (antes quedaba en negro para siempre). Nuevo
  `TriggerToScene(string)`. `Reset()` autoasigna `Mat_ReticleAlwaysOnTop`.
  Menú contextual "Probar desmayo (solo en Play)". Decisión #6: toggle
  `Show Level Text` (apagado) + `Level Title`/`Level Subtitle` (TextMesh con
  fuente interna, always on top, a 3 m, solo con la pantalla tapada).
- **`AbyssFall`** (Parte 3): arma al arrancar un pozo negro detrás de la
  puerta y hace caer la cámara con gravedad; `StartFall()` desde el
  `On Player Arrived` del tp que queda dentro del pozo. Cambios de
  gameplay-core: al caer apaga el gaze y llama
  `TeleportManager.LockMovement()`; la pared del lado de adentro tiene un
  hueco de puerta (`Doorway Width 1.1`/`Height 2.2`) porque las paredes del
  modelo son de una sola cara; `Entry Point` + `Door To Watch` +
  `Door Open Angle` (15°): el tp del pozo arranca apagado y se prende solo
  cuando la puerta ya giró; `Arm()` público; `Plug Doorway` (apagado);
  `On Fall Start`; `Hang Time`, `Gravity`, `Fall Clip`.
- **`WallCollapse`** (Parte 4): arma una pared falsa de bloques más un panel
  brillante; `Collapse()` hace grieta, derrumbe, luz y tirón de cámara.
  Cambios de visual-fx: `Panel Inset` (panel 5 cm detrás de los bloques),
  `Light Offset` (luz 0.5 m delante, `ForcePixel`), `UV Per Meter`/
  `UV Offset` (el empapelado de los bloques continúa el de la pared real),
  sin sombras, limpieza en `OnDestroy`, y **`Check Placement In Editor` +
  `CheckPlacement()`**: autodiagnóstico en Play (en el Editor) con
  MeshColliders temporales y backfaces, 13×121 rayos, que avisa si los
  bloques quedaron adentro de un muro. Fue lo que encontró el bug de abajo.
- **`Assets/Shaders/Abyss_Void.shader`** (`Custom/AbyssVoid`): color plano
  sin luz; ahora con `[HDR]` y opción "Aplicar niebla" (apagada por defecto,
  el pozo y el panel se ven igual que antes).
- **`FlickeringLight`**: `Burst(seconds)` (parpadeo violento forzado);
  `Burst Step Seconds` (0.05 = 5–10 Hz; antes cambiaba cada frame, 15–30 Hz
  según FPS; 0 = comportamiento viejo); `BurstThenOff(s)`, `TurnOff()`,
  `TurnOn()`. Ver decisión #17 (fotosensibilidad).
- **Build Settings** (verificado leyendo `EditorBuildSettings.asset`): 0
  `Menu Principal`, 1 `Tutorial`, 2 `Parte 1`, 3 `Parte 2`, 4 `Parte 3`, 5
  `Parte 4`, 6 `Parte 5`, 7 `Parte Final`, todas tildadas; HelloCardboard
  destildado. `MainMenuController` ahora valida `_gameSceneName` contra Build
  Settings antes de arrancar la secuencia.
- **Calidad:** resuelto, Android usa el nivel **Mobile** (ver "QA de build
  Android / A54").

**Tramos de la historia:**

| Tramo | Mecanismo | Estado (2026-09-23) |
|---|---|---|
| Menú → Tutorial | `MainMenuController._gameSceneName` | Funciona. |
| Tutorial → Parte 1 | `LevelDoor` + `SceneTransitionOverlay` (compañero) | Casi seguro invisible en Cardboard (#19). |
| Parte 1 → Parte 2 | `CodeLock.On Unlocked` → `Desmayo -> Parte 2` | Probado en Play (2026-09-24), sin errores. |
| Parte 2 → Parte 3 | (compañero) | Nada todavía. |
| Parte 3 → Parte 4 | `27_2` abierta → `tp pozo` → `AbyssFall` → `Desmayo -> Parte 4` | Probado en Play (2026-09-24), sin errores. |
| Parte 4 → Parte 5 | `tp pared ciega` → `WallCollapse` → `Desmayo -> Parte 5` | Probado en Play; arreglo de la pared guardado (21:14, ver abajo). |
| Parte 5 → Final | — | Nada (#5). |

**Prueba en Play de Parte 1 → 2 y Parte 3 → 4 (2026-09-24, vía Unity MCP, por
el camino real del código):**
- *Parte 1:* `CodeClueTracker.MarkSeen()` → `ExamineTrigger.OnGazeSelect()`
  (el jugador llega caminando a `VistaCandado`) → `CodeDigit.OnGazeSelect()`
  hasta `067` (con `_rotationDuration = 0` solo en esa sesión de Play, porque
  cada dígito ignora selecciones mientras gira) → `[CodeLock] Combinacion
  correcta.` → `[FaintTransition] … -> 'Parte 2 - Cumpleaños'` → carga Parte 2,
  el overlay despierta y se destruye (0 vivos, `IsRunning=false`). Único
  warning: `BoxCollider` con escala negativa en `BotonVolver` (viejo, sin
  relación).
- *Parte 3:* `tp10.OnGazeSelect()` (caminata) → `key_with_tag.OnGazeSelect()`
  (`HasKey=true`) → `27_2.OnGazeSelect()` (con llave y parado en `tp10`) →
  `[AbyssFall] Puerta abierta: se habilita el punto del pozo.` → cámara
  apuntada al vano: el rayo pega en `tp pozo` y la captura muestra el vano
  **negro** (sin magenta) → la mirada completa la selección → caminata →
  `[AbyssFall] Cayendo al abismo.` → `[FaintTransition] … -> 'Parte 4 -
  Pasillo de madera'` → carga Parte 4, overlay destruido, gaze reactivado.
  Sin errores ni warnings.
- Técnica: `Time.timeScale = 3` para acortar caminatas largas; captura en Play
  renderizando una cámara temporal a PNG (gotcha de captura). Al terminar se
  volvió a dejar el Editor en Parte 4, sin cambios.

**Parte 1 (guardado en disco, probado en Play 2026-09-24):** nuevo
`Transiciones/Desmayo -> Parte 2` (`FaintTransition`): destino
`Parte 2 - Cumpleaños`, `preDelay 3.5`, negro, `fadeOut 0.12`, `Overlay
Material = Mat_ReticleAlwaysOnTop`, Flicker Lights = `Spot Light`,
`Spot Light (1)`, `Spot Light (2)` y `Spot Light (Candado)` (**no** el
`Spot Light` hijo de `Main Camera`, que es la linterna/puntero). El
`CodeLock.OnUnlocked` quedó, en orden:
`ExamineTrigger.ReturnToPreviousPoint` → `Door.Open` (hoja 1) → `Door.Open`
(hoja 2) → `FaintTransition.Trigger` → `padlock.SetActive(false)`. El diff
fue de 110 líneas agregadas y 0 borradas (el archivo bajó de tamaño solo
porque Unity lo re-guardó con fin de línea LF). Clips de susurro, golpe y
despertar vacíos: no existen todavía.

**Parte 3 (guardado en disco, probado en Play 2026-09-24):**
- **Puerta del fondo = `hallway_hotel/doors/27_2`**: es la única `Door` con
  `Requires Key` (`Required Point = tp10`, tp10 en `(-3.59, -0.23, 24.04)`),
  en la pared oeste de un nicho al extremo norte del pasillo. Antes de
  cablear se verificó con MeshColliders temporales + raycasts con backfaces
  (y un rayo de control que sí pega en `walls`) que **detrás del vano de
  `27_2` no hay pared** (hueco real) → `Plug Doorway` queda destildado. El
  nicho está cerrado al sur (z≈23.0) y al norte (z≈26.0), y al oeste de
  x=-4.25 no hay geometría, así que el pozo no se ve desde el pasillo.
  Ningún renderer activo cae dentro del volumen del pozo (`27_2 (1)` lo
  intersecta pero está inactivo).
- Bajo `Transiciones`: `Desmayo -> Parte 4` (`FaintTransition`, destino
  `Parte 4 - Pasillo de madera`, `preDelay 1.8`, Flicker Lights =
  `Main Camera/Spot Light`, que es la única `FlickeringLight` de la escena);
  `Abismo (puerta del fondo)` (`AbyssFall`) en `(-4.30, -0.26, 24.07)`, rot
  `(0, -90, 0)`, con `Faint`, `Camera Transform = Main Camera`,
  `Void Shader = Abyss_Void`, `Teleport Manager = Player/TeleportManager`,
  `Entry Point = tp pozo`, `Door To Watch = 27_2` (resto default: pozo 3×3,
  80 m); y `tp pozo` (`TeleportPoint`, layer Teleport) en
  `(-5.45, -0.26, 24.07)`, `BoxCollider` center `(1, 1.05, 0)` size
  `(0.2, 2.1, 1.0)` (tapa el vano), `On Player Arrived → AbyssFall.StartFall`.
  `AbyssFall` lo apaga al arrancar y lo prende cuando `27_2` gira más de 15°.
- Diff: 391 líneas agregadas, 0 borradas.

**Parte 4 (en disco desde antes; verificado leyendo el YAML):**
`Transiciones/Desmayo -> Parte 5` (`FaintTransition`, GUID
`87a6d01e…` = `FaintTransition.cs.meta`): destino `Parte 5 - Backrooms`,
color `(1, 0.93, 0.7)`, `preDelay 4.5`, `fadeOut 1.5`, 18 `FlickeringLight`
(las 17 de `Luces Tetricas` + la linterna). `Transiciones/Pared Ciega
(derrumbe)` (`WallCollapse`, GUID `b3cd3831…` = `WallCollapse.cs.meta`), rot
Y=90 (la flecha Z mira hacia el pasillo, +X). `tp pared ciega` (layer
Teleport) en `(-132.5, 7.5, -202.04)`, a ~25.4 m de `tp1` (dentro de
`_maxGazeDistance = 35`), con `On Player Arrived → WallCollapse.Collapse`
como `PersistentCall`. La pared ciega es el extremo oeste del tramo en L.

**Bug de la pared invisible — RESUELTO (2026-09-23, visual-fx):** los bloques
y el panel brillante no se veían porque **estaban adentro del muro**. La
pared ciega de `Object_258` tiene **0.60 m de espesor**: la cara visible
(hacia el pasillo) está en x=-136.310, y x=-136.911 es la cara **exterior**.
El pivote de `Pared Ciega (derrumbe)` se había puesto en la exterior, así que
los bloques (x≈-136.55) y el panel quedaban dentro del muro (el
autodiagnóstico dio 1561 de 1573 puntos tapados). La medición vieja daba
-136.911 porque el padre de la malla (`duvar.071_169`) tiene **escala
negativa** y `Material.002` es doble cara: con `queriesHitBackfaces = false`
la física se saltea la cara visible y pega en la de atrás (gotcha #17). Las
hipótesis anteriores (caché de captura, depth priming, precisión de
profundidad) eran falsas pistas.
- **Arreglo aplicado:** posición `(-136.31, 4.85, -202.04)` (rot Y=90
  igual), `Gap 0.4` (la cornisa `Object_228` sobresale 0.27 m),
  `UV Per Meter (0.07447, -0.11033)`, `UV Offset (0.51984, 1.15433)`.
  Autodiagnóstico en Play: "Ubicacion OK … puntos tapados 0/1573". **Ya está
  guardado en disco** (la escena se re-guardó el 2026-09-23 a las 21:14;
  verificado leyendo el YAML: pivote en -136.31, `_gap: 0.4`, UV nuevos,
  `_checkPlacementInEditor: 1`). Si alguna vez se revierte la escena, estos
  son los valores a reaplicar.
- **Probado en Play** (cámara del jugador en `(-132.5, 11, -202.04)`
  mirando al oeste, capturas a PNG con la técnica del gotcha #22): antes del
  derrumbe la pared falsa se ve con el empapelado alineado; a ~1.7 s las
  grietas dejan pasar la luz; desde ~3 s la luz satura toda la pantalla a
  blanco. Cadena completa: `Collapse` → desmayo → carga
  `Parte 5 - Backrooms` → el overlay despierta y se destruye
  (`FaintOverlay.IsRunning = false`, 0 vivos), sin errores ni warnings.
- **Observación abierta (#13):** el blanco total llega ~1.5 s antes del
  desmayo y pierde el tono amarillo del guion. Si se lo quiere amarillo,
  bajar `Glow Max` (12) y `Light Max Intensity` (60).

**Estado de guardado de Parte 4:** al empezar la sesión, la escena ya tenía
cambios sin guardar de origen desconocido en el Editor (los valores de
`Transiciones`, cámara y luces coincidían con el disco). El arreglo de la
pared se sumó a eso en memoria y **la escena se guardó a las 21:14**, cuando
Omar cerró Unity y eligió Save (#7, resuelta por el `Editor-prev.log`). Toda
la escena sigue **sin commitear** (el último commit la tenía en ~1.26 MB; hoy
~1.57 MB).

**Falta:** probar en el A54 los tres tramos (en el Editor ya funcionan los
tres); clips de audio (susurro,
golpe seco, respiración, derrumbe, caída: ninguno existe todavía); `tp pared
ciega` hoy está siempre activo y, cuando exista la pintura, tiene que arrancar
apagado y prenderse al enderezarla (apagando su Collider o el GameObject, no
solo el componente: gotcha #20); rig en Parte 2/5/Final para que el desmayo
tenga a dónde llegar; tramos 2→3 y 5→Final. Largo máximo de los clips que
viven en la escena vieja (se cortan al descargarla): susurro y `Fall Clip` ≈
`preDelay + fadeOut + hold` (Parte 1 ~5.6 s, Parte 3 ~3.6 s); `Collapse
Clip` de Parte 4 ~8.5 s. `Impact Clip` y `Wake Clip` sobreviven porque suenan
en `FaintOverlay`.

**Orden sugerido (por dependencias, actualizado 2026-09-24):** 1) commitear
lo de esta sesión (rama + PR) y probar en el A54 los tres desmayos
(plan en "QA de build Android / A54"), 2) rig de Parte 5
(bloquea la etapa 5 entera), 3) loop + pintura de la etapa 4 (código listo),
4) mirillas + luz roja + monstruo de la etapa 3, 5) conseguir clips de audio,
6) etapa 5 (fragmentos + revelación; depende del modelo del monstruo).

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
  de la historia. **Corrección 2026-09-23:** Parte 2 (`rec_room…`), Parte 5
  (`backrooms_vr`) y Final (`kidman_room`) **tienen geometría pero no rig**
  (sin `TrackedPoseDriver`/`GazeController`/`tp`), así que hoy, si se llega a
  ellas por un desmayo, la cabeza no rota y no hay salida. Parte 4 es la más
  avanzada de la historia (ver sus secciones).
- **Escenas generadas por script (del compañero, 2026-09-08/09):** en
  `Assets/Editor/` hay tres herramientas de menú `Tools/Feria de Ciencias/…`.
  **`MenuPrincipalGenerator`** ("Generar escena 'Menu Principal'") arma
  `Menu Principal` **desde cero** (`EditorSceneManager.NewScene(EmptyScene)` +
  `SaveScene`): correrlo de nuevo **pisa cualquier cambio manual** hecho en
  esa escena (gotcha #23). `TutorialSetup` ("Preparar Tutorial (llave +
  puerta + carga)") abre `Tutorial`, borra solo restos de corridas viejas de
  sí mismo y cablea llave → `LevelDoor` hacia Parte 1; `TutorialNarrationSetup`
  agrega la locución (`SceneNarration`). Antes de tocar `Menu Principal` o
  `Tutorial` a mano, preguntar si el compañero las sigue regenerando con
  estas herramientas.
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
  (cada script decide si eso le importa o no). **Corrección 2026-09-23:** el
  `Debug.Log` `[GazeController] Gaze cambio -> ...` que se usaba para ver qué
  Collider pegaba el rayo **ya no existe** — lo sacó el compañero en el commit
  `00c9a0b` (2026-09-09). Si hace falta de nuevo, se agrega temporal; para
  diagnosticar hoy quedan los logs `[TeleportManager]`, `[Door]`,
  `[CodeLock]`, `[FaintTransition]`, `[FaintOverlay]`, `[AbyssFall]` y
  `[WallCollapse]`.
  - **Cambios de gameplay-core (2026-09-23):** `OnDisable()` + `ResetGaze()`
    público (dispara `OnGazeExit` y vacía el progreso, así el aro no queda
    congelado a medio llenar cuando `FaintOverlay`/`AbyssFall` apagan el
    gaze); ya no tira `NullReferenceException` si `Camera.main` todavía no
    existe; y `Occluder Layer Mask` opcional (default `Nothing` = igual que
    antes) para que las paredes con Collider en ese layer corten el rayo.
  - **Ojo: el rayo atraviesa paredes** (gotcha #24): las paredes de los
    modelos no tienen Collider, así que un `tp` que queda detrás de una pared
    a menos de `_maxGazeDistance` se puede elegir igual. En Parte 4 hay 7
    pares así (tp4↔tp9, tp3↔tp9, tp6↔tp7, tp7↔tp8, tp4↔tp5, tp1-2↔tp3,
    tp2↔tp3). Arreglo posible: MeshCollider en las paredes + `Occluder Layer
    Mask` (decisión #15).
  - **Destildar un `IGazeInteractable` NO lo saca de la mirada** (gotcha
    #20): hay que apagar su Collider o el GameObject.
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

### Loop sin fade y bloqueo de movimiento (gameplay-core, 2026-09-23)

Para el pasillo "que te devuelve al mismo punto" de la etapa 4 hacía falta
un salto **invisible**: hasta ahora cualquier `TeleportPoint` con
`Destination Override` forzaba fade, y un fundido delata el truco.

- **`TeleportPoint`**: campos nuevos `Seamless Loop` (apagado),
  `Loop Only When Moving Away` (prendido) y evento `On Loop`. Con
  `Destination Override` + `Seamless Loop`, el jugador **camina** hasta el
  punto y al llegar salta **sin fade** al destino; si el destino es otro
  `TeleportPoint`, queda parado ahí y se dispara el `On Player Arrived` de ese
  punto. Para que no se note, **el destino tiene que mirar en la misma
  dirección** que el origen (el salto conserva la rotación de la cabeza).
  `Loop Only When Moving Away` hace que el loop solo se active cuando el
  jugador se aleja del destino (si viene de vuelta, el punto se comporta
  normal).
- **`TeleportManager`**: ejecuta el salto del loop y resetea el gaze.
  `LockMovement()`/`UnlockMovement()`/`IsMovementLocked` cortan caminatas
  (no fades); lo usa `AbyssFall` para que el jugador no pueda elegir otro
  `tp` mientras cae.
- **Propuesta para Parte 4 (NO cableada, decisión #11):** `tp8-9` con
  `Destination Override = tp9`, `Seamless Loop` + `Loop Only When Moving
  Away`: desde el spawn hacia el este, al llegar a `tp8-9` vuelve a `tp9`
  (23.40 m atrás). Alternativa: `tp6` → `tp5-6`.

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
  - **Qué escenas lo tienen de verdad (corrección 2026-09-23, qa-performance
    leyendo los `.unity`):** `Parte 1`, `Parte 3` y `Parte 4`. **`Menu
    Principal` NO tiene Bloom** (sin `Global Volume` y con Post Processing
    apagado en la cámara), contra lo que decía este documento; probablemente
    se perdió al regenerar la escena con `MenuPrincipalGenerator` (gotcha
    #23). `Tutorial`, `Parte 2`, `Parte 5` y `Parte Final` tampoco tienen
    post-procesado. Además `Parte 1` y `Parte 3` tienen **HDR apagado en la
    cámara** (`m_HDR: 0`), así que su Bloom se ve distinto al de `Parte 4`
    (ver sección de `allowHDR`).
  - El perfil compartido también trae **Screen Space Lens Flare (intensidad
    1) y Chromatic Aberration (0.091)**, caros y molestos en estéreo; ver
    "QA de build Android / A54" y decisión #21.
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
  **Métodos para momentos de guion (2026-09-23):** `Burst(seconds)` (parpadeo
  violento forzado, a `Burst Step Seconds`, default 0.05 = 5–10 Hz),
  `BurstThenOff(seconds)`, `TurnOff()`, `TurnOn()`, todos llamables desde un
  `UnityEvent`. Detalle en "Transiciones desmayo".
- **`ScreenBlink`** (`Scripts/Ambient/ScreenBlink.cs`, visual-fx
  2026-09-23) + shader `Custom/EyelidOverlay`
  (`Assets/Shaders/Eyelid_Overlay.shader`): párpados del protagonista. Es
  una esfera alrededor de la cámara (mismo truco que `FaintOverlay`, queue
  4998, o sea debajo del desmayo). `Blink()`, `RapidBlinks()` (el parpadeo
  rápido del final, ~2.5 parpadeos/s por defecto), `CloseEyes()`,
  `OpenEyes()`, `SetEyesClosed(bool)`, `Start Closed` (para arrancar una
  escena con los ojos cerrados) y `On Sequence Finished`. **Asignar siempre
  el campo del shader** (`_eyelidShader`): si queda vacío usa `Shader.Find`,
  que puede devolver null en el APK (gotcha #25).
- **`FluorescentFlicker`** (`Scripts/Ambient/FluorescentFlicker.cs`,
  visual-fx 2026-09-23): parpadeo de tubos fluorescentes para los Backrooms.
  Existe porque `backrooms_vr.glb` es **unlit con luz horneada** (gotcha #19):
  una `Light` no lo afecta, así que el script hace parpadear el **color de
  los materiales** (vía `MaterialPropertyBlock`, sin clonar materiales) y,
  opcionalmente, una `Light`, un `Volume` y el volumen del zumbido.
  **Receta de ambiente propuesta para Parte 5** (no aplicada, la escena no
  tiene rig todavía): skybox `None`, ambiente color `(0.45, 0.42, 0.30)`,
  `reflectionIntensity 0`, fog ExponentialSquared color `(0.40, 0.37, 0.24)`
  densidad `0.05`, cámara con Post Processing + HDR y `far 45`.
- **Luz roja de la mirilla (receta de visual-fx para la etapa 3, no
  aplicada):** `Spot` 1.2 m sobre la llave, ángulo 70/35, color
  `(1, 0.12, 0.07)`, intensity 4, range 5, sin sombras, render mode
  Important; `FlickeringLight` Base 4, Amount 0.15, Speed 1.5, Blackout 0; y
  un foco visible (esfera de 0.08 m con `Custom/AbyssVoid` rojo HDR +2).
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
  (0.5, 0.5, 0.5)`, `fogDensity = 0.037`. **Actualizado 2026-09-09:** Omar
  pidió bajarle la niebla y preferir oscuridad en su lugar ("prefiero que
  este nivel se vea muy oscuro en vez de que haya mucha niebla") —
  `fogDensity` bajado a `0.012` y `fogColor` oscurecido a
  `(0.03, 0.03, 0.035)` (casi negro, en vez del gris `0.5` original), así lo
  lejano se pierde en oscuridad real en vez de un gris lechoso. Ver la
  sección "Reflejos del skybox filtrándose..." más abajo — esto se hizo en
  la misma sesión donde se encontró que el reflejo del skybox estaba
  aportando gran parte de lo que hasta entonces se veía como "iluminado".
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
  `Menu Principal` y `Parte 3` (con el Bloom ya configurado ahí; *nota
  2026-09-23: hoy `Menu Principal` ya no tiene `Global Volume`, ver sección
  de Bloom*) — no se creó
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

### Tamaño de los `TeleportPoint` y modelo visual (2026-09-09)

Dos ajustes a pedido de Omar sobre los `tp1`-`tp9` de `Parte 4`:

- **Tamaño:** el cubo achatado original quedó chico/desproporcionado
  (`0.8 x 0.05 x 0.8`) comparado con el resto del proyecto. Se midió el
  tamaño real (en MUNDO, no el valor local del Inspector, que puede estar
  afectado por escalas de padres) de los `TeleportPoint` de `Parte 3` —
  **`1.0 x 0.2 x 1.0`**, consistente en casi todos — y se re-escalaron los 9
  de `Parte 4` para igualarlo, corrigiendo también la posición Y (el pivote
  de un `Cube` está centrado, así que al subir la altura de `0.05` a `0.2`
  había que subir el objeto la mitad de esa diferencia para que la base
  siguiera apoyada en el piso, no hundida).
- **Modelo visual:** Omar importó `Assets/Modelos 3D/model_3glow_orb.glb`
  (un orbe emisivo amarillo, ~0.42m de diámetro, con `Animator` propio) para
  reemplazar los cubos grises. Se probó en los 9 puntos de `Parte 4`:
  - Se sacó el `MeshFilter`/`MeshRenderer` del cubo primitivo de cada
    `TeleportPoint`, dejando el `BoxCollider` (agrandado a `0.9x0.9x0.9` y
    centrado en la altura del orbe) como interacción.
    Se instanció el orbe como HIJO (`OrbVisual`), flotando `0.35` unidades
    arriba del pivote y escalado x1.8 (~0.76m de diámetro final).
  - **Hizo falta un cambio de código, chico y retrocompatible:**
    `TeleportPoint.Awake()` buscaba el `Renderer` con `GetComponent`
    (solo en el propio objeto), lo cual no encuentra el mesh real del orbe
    porque vive varios niveles adentro de la jerarquía importada
    (`OrbVisual > Sketchfab_model > root > GLTF_SceneRootNode > Cube_0 >
    Object_4`). Se cambió a `GetComponentInChildren<Renderer>()` (el
    `Collider` se dejó como estaba, `GetComponent`, porque ese sigue
    viviendo en el propio `TeleportPoint`, no en el modelo visual) — esto
    **no rompe ningún `TeleportPoint` existente** en `Parte 1`/`Parte 3`
    (`GetComponentInChildren` encuentra el componente igual si está en el
    propio objeto, es un superconjunto de `GetComponent`), pero ahora
    también sirve para decorar cualquier `TeleportPoint` futuro con un
    modelo importado con jerarquía propia.
  - Verificado visualmente: el orbe se ve flotando con su propio brillo
    (material emisivo + Bloom), mucho más integrado que el cubo gris.
  - **No se le agregó una `Light` real ni `FlickeringLight`** a los orbes
    (a propósito, por costo de rendimiento — ya hay 9 luces reales en
    `Luces Tetricas`, sumar 9 más pareció excesivo para Android/A54) — el
    brillo que se ve es puramente el material emisivo + Bloom, no ilumina
    de verdad el entorno. Si Omar quiere que además iluminen, es una
    decisión aparte a pedir explícitamente.

  **Actualizado 2026-09-09:** la estructura de dos niveles descripta arriba
  (`tpN` vacío + `OrbVisual` hijo) causaba que el hitbox no coincidiera con
  el orbe (por la escala no-uniforme del wrapper) — se reestructuró para
  que sea un solo objeto por punto. Ver la sección "Reestructuración de los
  `TeleportPoint` con orbe" más abajo para el estado actual real.
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
    apenas arranca la escena, además de cuando se abre). **Puertas dobles
    (corregido 2026-09-23, audio-integrator):** antes se decía que compartir
    un único `AudioSource` entre las dos hojas evitaba que suene dos veces.
    **Es falso:** `Door` usa `PlayOneShot`, y dos `PlayOneShot` sobre el
    mismo `AudioSource` se superponen igual (+6 dB, suena "doble"). Lo que
    sí lo evita es dejar **`Open Sound` vacío en UNA de las dos hojas** (la
    otra pone el sonido por las dos). Caso real: la puerta del candado de
    `Parte 1` (las 2 hojas con `Required Point = tp9` comparten
    `puerta_metal` y las dos tienen `Open Sound`), sin corregir todavía
    (decisión #18). En `Parte 1` y `Parte 3` todas las puertas usan un
    `AudioSource` central 2D (`puerta_metal` y `puerta_madera`
    respectivamente), y ninguna tiene el bug de "un solo campo de audio".
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
  cableado a `OnCollected`). *2026-09-23:* sonido opcional al recolectar
  (`Audio Source` + `Collect Sound`; mismo patrón de dos campos que `Door`).
- **`LevelDoor`** + **`SceneTransitionOverlay`**
  (`Scripts/Interaction/`, del compañero, usados solo en `Tutorial` hacia
  `Parte 1`): puerta que se abre por mirada (pide llave opcional, con cartel
  si falta), gira sobre `_doorPivot`, funde a negro con texto grande
  (`_bigText`/`_subText`) y carga `_nextSceneName`; el overlay sobrevive el
  cambio de escena con `DontDestroyOnLoad`. **Problema:** el overlay es un
  Canvas `RenderMode.ScreenSpaceOverlay`, que URP 17 no dibuja en los ojos
  con XR activo (ver "QA de build Android / A54"): en Cardboard lo esperable
  es ver ~3.7 s del Tutorial con el gaze activo y después un corte seco. No
  tocar sin consultarle (decisión #19); el reemplazo natural es
  `FaintOverlay`.
- **`SceneNarration`** (`Scripts/Ambient/`, del compañero, usado en
  `Tutorial`): locución 2D una vez al arrancar, con subtítulo TMP opcional.
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
  *Confirmado 2026-09-23: no está en ninguna escena.*
- **`CameraDebugLogger`** (`Scripts/Debug/CameraDebugLogger.cs`): script de
  diagnóstico temporal (loguea posición/rotación real de Main Camera + estado
  de XR una vez por segundo). Se usó para encontrar el bug de
  `Tracking Type`. Se puede sacar del objeto `Main Camera` si ya no hace
  falta, pero el archivo no molesta si queda en el proyecto sin usarse.
  *Confirmado 2026-09-23: sigue activo en la `Main Camera` de `Parte 1`.*
  Conviene dejarlo hasta la prueba en el A54 (loguea si XR está prendido y
  en estéreo, útil para el plan de prueba de "QA de build Android") y
  sacarlo después.

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

## Monstruo — `MonsterController` (gameplay-core, 2026-09-23)

`Assets/Scripts/Monster/MonsterController.cs`. Es el personaje central de la
historia (etapas 2, 3 y 5). El **código** existe; el **modelo** no (decisión
#3), así que está escrito para ser **independiente del modelo**: hoy se
prueba con una cápsula, y cuando haya modelo se cuelga como hijo sin tocar
el script.

- **Métodos para `UnityEvent`:** `Hide`, `Appear`, `AppearAt(Transform)`,
  `WarpTo`, `Watch` (quieto, mirando al jugador), `Stalk` (acecha detrás),
  `SetStalkDistance(float)`, `Charge` (corre hacia el jugador), `Reveal`,
  `RemoveMask`.
- **`Stalk`** sigue el **recorrido real** del jugador (dobla donde él dobló,
  no corta por las paredes) y cruza puertas solo "fuera de vista".
- **`Charge`** dispara `On Caught` **antes** de tocar al jugador (con
  timeout), que es justo el momento del desmayo del guion en la etapa 2.
- **Opcionales:** `Animator` (parámetros `Speed`, `IsWalking`, `IsRunning`,
  `Charge`, `RemoveMask`), `NavMeshAgent`, pasos (clips sueltos o loop), y
  la máscara como objeto hijo.
- **Eventos:** aparecer, primera vez visto, desaparecer, empieza a embestir,
  atrapado, revelación, máscara quitada.
- **Convenciones:** pivote en los pies; el modelo hijo mira a +Z. "Visto"
  usa solo el ángulo de la cámara, **no chequea paredes** (si está detrás de
  un muro pero dentro del campo de visión, cuenta como visto).
- **Propuesta para Parte 3 (NO cableada, decisión #9):** vacío `Monstruo` en
  el piso, `Start State = Hidden`, cápsula hija, `AudioSource` 3D, y
  `key_with_tag.On Collected → MonsterController.Stalk`.
- **Presupuesto del modelo para el A54:** ver "QA de build Android / A54".

## Puzzles nuevos (puzzle-designer, 2026-09-23) — código listo, nada instanciado

Todo en `Assets/Scripts/Puzzles/`. Ninguno está puesto en una escena todavía.

- **`Peephole`** (etapa 3, mirillas): al mirar la mirilla funde a negro y la
  cámara pasa a un `View Anchor` puesto del otro lado de la puerta, con una
  máscara circular armada por código (`UI/AlwaysOnTop`, queue 3999, o sea
  debajo del reticle y del fade). **No usa `TeleportManager` a propósito**:
  en modo caminata el jugador atravesaría la pared. Guarda y vuelve al punto
  exacto de antes. Mientras se espía apaga (y después restaura) los Collider
  de todos los `TeleportPoint`, para no teletransportarse sin querer. Se sale
  girando la cabeza más de 80°, por tiempo (20 s) o con `ExitPeephole()`/
  `ExitAfterDelay(float)`. Sin `View Anchor` = mirilla ciega (para las
  puertas falsas). Eventos `On Peep Start`/`On Peep End`. **Asignar siempre
  `_maskMaterial`** (gotcha #25).
- **`UpsideDownPainting`** (etapa 4): se acomoda **derecha** en el Editor y
  el script la invierte al arrancar (así se posiciona cómodo). Al mirarla
  tiembla, gira, se asienta y dispara `On Straightened` una sola vez.
  `Straighten()`/`ResetUpsideDown()`. **Propuesta (decisión #12):** pared del
  fondo del tramo este de la L, x=-27.90 (piso 4.81, pasillo z entre
  -209.55 y -197.7); desde ahí la pared ciega queda a la espalda, que es
  justo el "caminar de espaldas" del guion. Objeto `Pintura al reves` en
  `(-28.05, 10.5, -203.7)`, rot `(0, 90, 0)`, con el cuadro `frame_61` de
  `horror_room.glb` (una cabaña con luz amarilla, que conecta con la luz del
  final).
- **`FragmentCounter`** + **`FragmentDropOff`** (etapa 5): contador **por
  escena** (no singleton, igual que `KeyInventory`), `AddFragment()`
  (cablear desde el `On Collected` de cada `Collectable`), textos y sonido
  opcionales, `On Fragment Collected(int)` y `On All Collected`. La entrega
  en el centro del mapa se hace por mirada o con `TryDeliver()`; si faltan,
  muestra "Faltan N"; `On Delivered` se dispara una vez.
- **Assets candidatos:** 3 fragmentos = las 3 notas de
  `Assets/Models/sticky_notes.glb` (`pPlane3/4/5`); silueta de tiza =
  `crime_scene_tape__sign_with_chalk_body_outline.glb`; reloj/armario/cuadro
  = `horror_room.glb`.
- **Gotcha que salió de acá:** destildar un `IGazeInteractable` no lo saca de
  la mirada (gotcha #20). Importa para `tp pared ciega`, la puerta-tp `1_3` y
  cualquier cosa que se quiera "prender" más tarde.

## Audio: `AudioFader` y bugs encontrados (audio-integrator, 2026-09-23)

- **`Assets/Scripts/Audio/AudioFader.cs`** (nuevo): fundidos de entrada y
  salida, arranque retrasado, auto fade-out, y `Distort()` (baja el pitch y,
  si se le da, cambia a un clip distorsionado), todo llamable desde
  `UnityEvent`. Pensado para la música de cumpleaños que se distorsiona
  (etapa 2) y el zumbido de los Backrooms.
- **Unity 6 guarda el clip de un `AudioSource` en `m_Resource:`**, no en
  `m_audioClip:` (que queda vacío). Al auditar escenas por YAML, grep por
  `m_Resource` (gotcha #21).
- **Bugs encontrados, NO corregidos (son de escena, decisión #18):**
  - `Parte 1`: las 2 hojas de la puerta del candado suenan dos veces
    superpuestas (ver "Sistema de puertas y llaves").
  - `Parte 3`: `Main Camera/ReticleCanvas/FadeImage` tiene un `AudioSource`
    con `puerta_madera.wav` y **Play On Awake** → suena un portazo al cargar
    la escena.
  - `Parte 4`: no hay ningún `AudioSource`; `TeleportManager.Footsteps Audio
    Source` está vacío → la caminata es muda.
  - `Parte 3`: el componente `Door` de `27_2 (1)` está deshabilitado (y el
    objeto inactivo); no molesta, pero no confundirlo con la puerta del
    fondo real, que es `27_2`.
- **Importación:** los 10 clips están en Decompress On Load + Vorbis 100% +
  Preload off. `AmbienteMenuPrincipal` (~53 MB en RAM) y `TutorialAmbiente`
  (~68 MB) conviene pasarlos a **Streaming**; SFX cortos: Decompress on Load
  + Preload on; los clips 3D: Force To Mono. No hay spatializer ni
  AudioMixer. Recomienda `Doppler Factor = 0` (#18).
- **Largo máximo de clips que viven en la escena vieja:** ver "Transiciones
  desmayo" (se cortan cuando se descarga la escena).
- **Clips faltantes (~30):** golpe seco, respiración al despertar, latidos,
  susurro, caída/viento, derrumbe de madera, música de cumpleaños + versión
  distorsionada, globos, pasos y grito del monstruo, mirilla, llave, cuadro,
  zumbido fluorescente, papel, entrega, máscara, pitido de monitor. La tabla
  completa con el destino exacto de cada uno (script + campo) está en el
  reporte de audio-integrator del 2026-09-23; ninguno existe en el proyecto
  todavía.

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

## Reflejos del skybox filtrándose en escenas "a oscuras" — bug encontrado en `Parte 4` (2026-09-09)

Omar reportó que varios objetos (alfombra, mesa, puerta, bordes, paneles sobre
zócalos) se veían con "un contorno gris/celeste" y **se notaban iluminados
incluso lejos de cualquier luz** — un síntoma distinto al de las normales
invertidas (sección de arriba), porque acá el shader y las normales estaban
bien.

**Causa real:** `RenderSettings.reflectionIntensity` seguía en `1` (el
default de Unity) y `defaultReflectionMode` en `Skybox`, con el skybox
default de Unity puesto (celeste/azul). **Bajar `ambientIntensity` a 0 (lo
que se hizo para el ambiente tétrico) NO apaga esto** — son dos sistemas
separados en Unity: `ambientIntensity` controla la luz ambiente DIFUSA
derivada del skybox, pero `reflectionIntensity` controla por separado el
reflejo ESPECULAR (vía sonda de reflexión) del mismo skybox, y ese reflejo
NO depende de si hay luces reales cerca ni de la niebla — por eso se veía
"iluminado" a cualquier distancia. Cualquier material con algo de
`Metallic`/baja `Roughness` (o sea, casi cualquier material PBR no 100%
mate) capta ese reflejo como un tinte parejo, más notorio en los bordes
(efecto Fresnel).

**Prueba que lo confirmó:** se comparó una captura con `reflectionIntensity=1`
vs `=0` desde la misma cámara — con el reflejo apagado, la enorme mayoría de
lo que se veía "iluminado" en la niebla/oscuridad **desapareció por completo**,
confirmando que ese reflejo estaba actuando como una luz ambiental fantasma en
toda la escena, no solo un tinte cosmético en un par de objetos.

**Fix aplicado (solo en `Parte 4` por ahora):** `RenderSettings.reflectionIntensity = 0`.

**Efecto secundario importante (y otro descubrimiento en el proceso):** al
sacar ese reflejo, se reveló que la iluminación REAL de los 9 focos del
pasillo (`Luces Tetricas`) era insuficiente por sí sola — porque el
`Intensity` que se ve en el Inspector del `Light` (que se había dejado en 10)
**no es el valor real en Play**: `FlickeringLight.Awake()` lo pisa con
`_baseIntensity`, que estaba en su default de fábrica (`1.5`, pensado
originalmente para una luz puramente decorativa en `Menu Principal`, no para
ser la única fuente de luz real de un pasillo entero). Es decir, **todas las
capturas de este proyecto hechas con `Unity_Camera_Capture` en modo Editor
(sin Play) venían mostrando una escena más brillante de lo que realmente se
ve jugando**, porque ese script nunca corre en modo edición. Se subió
`_baseIntensity` (y el `Intensity` del `Light`, solo por prolijidad visual en
el Editor) a `20` con `range 14` en las 9 luces para compensar la pérdida del
reflejo y que el pasillo siga siendo navegable — cada lámpara ahora forma un
pozo de luz real y bien oscuro alrededor, en vez de un cuarto entero
iluminado de forma pareja. **Esto es un valor de partida, no definitivo** —
falta que Omar lo prueebe en Play/Build real (que sí ejecuta el flicker/
apagones aleatorios) para terminar de calibrarlo a gusto.

**Pendiente real, no aplicado todavía:** `Parte 1`, `Parte 3` y
`Menu Principal` también tienen `ambientIntensity = 0` (o similar) pero
**nadie tocó `reflectionIntensity` ahí tampoco** — es muy probable que
tengan el mismo problema en cualquier material con algo de brillo, solo que
no se había reportado todavía (quizás porque sus materiales son más mate en
promedio, o porque nadie miró de cerca un borde). Cuando se vuelva a esas
escenas, vale la pena chequear `RenderSettings.reflectionIntensity` ahí
también antes de asumir que están bien.

**Nota sobre la herramienta de captura de Unity MCP
(`Unity_Camera_Capture`):** parece cachear el render por transform de la
cámara — si se cambia SOLO algo de iluminación/material sin mover ni rotar
la cámara ni un poco, puede devolver la imagen anterior sin re-renderizar
(confirmado: cambiar `Light.intensity` de 8 a 40 dio el mismo PNG byte por
byte). **Solución:** mover la cámara de verificación una fracción minúscula
(por ejemplo `+0.001` en un eje) antes de cada captura de comparación si lo
único que cambió fue algo que no es la cámara misma — si no, el "antes/
después" puede ser un falso negativo.

## Portales sin interacción tras poner el orbe — escala no-uniforme aplastando al collider (`Parte 4`, 2026-09-09)

Después de swapear los cubos por el orbe, Omar reportó que "los portales no
reciben interacción". Bug real, causado por mí en el paso anterior — no un
problema del orbe en sí.

**Causa:** cada `TeleportPoint` tenía `localScale (1, 0.2, 1)` (de cuando se
ajustó el tamaño para que el CUBO visual coincidiera con el de `Parte 3`).
Esa escala no-uniforme en el eje Y se hereda multiplicativamente por
CUALQUIER hijo o por el `BoxCollider` del mismo objeto — así que cuando
después se le puso `box.size = (0.9,0.9,0.9)` y `box.center = (0,0.35,0)`
pensando en unidades de MUNDO, en realidad Unity los interpreta en espacio
LOCAL y los aplasta por ese mismo 0.2 al calcular el collider real: el
`BoxCollider` terminó midiendo `0.9 x 0.18 x 0.9` en el mundo (no `0.9x0.9x0.9`)
y centrado más abajo de lo esperado. Lo mismo le pasó a la posición del
`OrbVisual` hijo (el offset de flotación de 0.35 también quedó aplastado a
0.07) — por eso además el orbe se veía como un óvalo achatado en vez de una
esfera pareja.

**Fix:** en vez de tratar de compensar la escala no-uniforme con matemática
inversa (frágil y confuso), se sacó la escala rara directamente:
`transform.localScale = Vector3.one` en los 9 `TeleportPoint`, y en su lugar
el tamaño/posición del collider y del orbe se setean directamente en esas
unidades ya sin distorsión. **Regla general para el futuro:** evitar
`localScale` no-uniforme en cualquier GameObject que vaya a tener hijos o
colliders configurados después por tamaño en unidades de mundo — mejor
ajustar el tamaño real del `Collider`/mesh directamente y dejar el
`Transform.scale` en `(1,1,1)` siempre que se pueda.

**Gotcha adicional encontrado mientras se diagnosticaba esto — `Physics.autoSyncTransforms`
está en `False` en este proyecto:** cambiar `Collider.size`/`.center` (o
mover/escalar un `Transform`) por código y **en el mismo frame** llamar a
`Physics.Raycast`/`OverlapBox` puede devolver el estado VIEJO (no se actualiza
solo). Hace falta llamar a `Physics.SyncTransforms()` explícitamente antes de
la query para verificar cambios recién hechos por script en el mismo tick —
esto costó bastante detectar acá porque el síntoma ("el raycast de prueba no
pega en nada") parecía indicar un collider realmente roto, cuando en
realidad el collider ya estaba bien pero la consulta de física todavía veía
la versión anterior. Puede no importar en gameplay real de Play/Build (ahí
la física se sincroniza sola en su propio ciclo), pero sí importa para
diagnosticar/verificar cambios hechos por `Unity_RunCommand` en el mismo
script.

## Reestructuración de los `TeleportPoint` con orbe — un solo objeto, no dos anidados (`Parte 4`, 2026-09-09)

Después del fix de la escala no-uniforme (sección de arriba), Omar pidió
directamente sacar la jerarquía `tp1..tp9` (vacíos, solo con el
`Collider`) + `OrbVisual` (hijo, el modelo) y dejar UN SOLO objeto por
punto — el propio orbe importado es ahora el `TeleportPoint`.

**Cómo quedó `Parte 4` ahora:** `Teletransportadores` → `tp1`..`tp9`
directos (sin wrapper), cada uno con `Transform` + `Animator` (viene del
prefab del orbe) + `BoxCollider` + `TeleportPoint`, y como hijo la
jerarquía propia del modelo importado (`Sketchfab_model > root >
GLTF_SceneRootNode > Cube_0 > Object_4`).

**Segundo bug encontrado en el proceso (más sutil):** el modelo
`model_3glow_orb.glb` tiene un nodo interno, `Cube_0`, con
`localPosition = (-2, 0, 0)` respecto a su padre — un offset que
probablemente viene de cómo estaba armada la escena original en Sketchfab
(quizás había más objetos alrededor del orbe en la escena de origen). A la
escala final del orbe en el juego (`1.8`), ese `-2` local se traduce en
**`-3.6` unidades de offset en el mundo** entre el pivote del objeto
(`transform.position`, que es lo que `TeleportPoint` usa como destino real
del teletransporte) y donde realmente se dibuja la malla. El `BoxCollider`
ya se calculaba a partir del `Renderer.bounds` real, así que quedaba bien
puesto sobre la malla — pero el **destino de teletransporte** (que usa
`transform.position` directo) hubiese quedado a 3.6 unidades del lugar
donde visualmente aparece el orbe.

**Fix:** se contrarresta el offset seteando
`Sketchfab_model.localPosition = (2, 0, 0)` (el hijo directo del
`TeleportPoint`) — así el mesh real termina exactamente en el pivote del
objeto padre, y `transform.position` (destino), `Renderer.bounds.center`
(mesh visible) y `BoxCollider.bounds.center` (hitbox) quedan los TRES
coincidiendo exactamente. Verificado en los 9 puntos, no solo a ojo:
comparando esos tres valores por código, no por captura de pantalla.

**Nota para el futuro si se usa este mismo modelo (`model_3glow_orb.glb`)
en otro lado:** este offset (`Sketchfab_model.localPosition = (2,0,0)`)
es una propiedad del modelo, no de esta escena en particular — si se
instancia este orbe de nuevo (otra escena, otro puzzle), hay que aplicar
la misma corrección o el pivote del objeto NO va a coincidir con dónde se
ve la malla.

## Tres bugs más en `Parte 4` — portales fuera de rango, zócalo sin luz, linterna probada mal (2026-09-09)

Después de la sesión anterior, Omar reportó: "los portales no funcionan",
"el revestimiento bajo de la pared se ve negro negro" y "la linterna sigue
sin iluminar a distancia". Los tres eran reales, tres causas distintas.

### 1. Portales sin interacción — el verdadero motivo: `maxGazeDistance`

El collider de cada `TeleportPoint` estaba perfecto (ya verificado la
sesión anterior) — el bug real es que **nunca comparé las distancias reales
entre puntos consecutivos contra el rango de mirada**. `GazeController.
_maxGazeDistance` (heredado del prefab, valor `10`) es muchísimo menor que
la distancia real entre CUALQUIER par de `TeleportPoint` consecutivos en
este pasillo — incluso después de agregar los 8 puntos intermedios de la
sesión anterior, el salto más chico es **15.86** unidades y el más grande
**30.66** — los 16 saltos, sin excepción, superaban el límite de 10. O sea:
agregar puntos intermedios ayudó a que el recorrido sea geométricamente
válido (piso real bajo cada uno), pero **no alcanzaba nada mientras el rayo
de mirada no llegara ni a la mitad de esa distancia** — ningún punto era
alcanzable desde ningún otro, sin importar qué tan bien estuviera el
collider. **Fix:** `_maxGazeDistance` subido de `10` a `35` (cubre el salto
más largo con margen). Verificado calculando la distancia real de los 16
tramos por código, no a ojo.

### 2. Zócalo/friso de madera "negro negro" — no era un bug de material

Se probó primero con una luz de prueba muy cerca: el zócalo (`Object_6` y
similares, material `Material.005`) respondía perfecto a la luz — normales
correctas (`dot=1`), `_WorkflowMode`/`_SPECULAR_SETUP` consistentes con el
resto de materiales sanos. **La causa real era simplemente que estaba
fuera del alcance de cualquier luz real** (la más cercana estaba a 23.4
unidades, con `range=14`) — entre los 9 focos originales había tramos
largos del pasillo completamente sin cobertura de luz real (los 8 puntos
intermedios de la sesión anterior solo tenían el orbe emisivo, sin una
`Light` real que ilumine el entorno).

**Fix (dos partes):**
- **Se agregó una luz real (`Point` + `FlickeringLight`) en cada uno de los
  8 puntos intermedios**, a la misma altura de techo que las 9 originales —
  duplicando la densidad de cobertura de luz a lo largo de todo el
  recorrido. Ahora hay **17 luces**, una por cada `TeleportPoint`.
- **Las luces se cambiaron de `Spot` a `Point`.** Un `Spot` apuntando
  derecho al piso (como estaban) prácticamente no llega a las paredes
  cercanas — solo ilumina el círculo de piso debajo suyo. Dado que el
  modelo real es un foco/bombita colgante EXPUESTA (no una lámpara con
  pantalla direccional), tiene más sentido físico que ilumine en todas
  direcciones. Como el `Point` reparte la misma energía en un volumen
  mucho mayor que un `Spot` enfocado, hubo que subir la intensidad de
  compensación de `20` a `35` (y el rango de `14` a `16`) para que el piso
  siguiera viéndose bien Y las paredes cercanas también reciban algo.
  Verificado con captura general del pasillo: ahora se ven detalles que
  antes eran invisibles (alfombra, papel tapiz, zócalo, veta de la
  madera), sin perder el clima oscuro.

### 3. Linterna "sigue sin iluminar a distancia" — el test anterior estaba contaminado

Al probar de nuevo, la primera captura desde el spawn salió toda amarilla/
sobreexpuesta — no por la linterna, sino porque **el spawn del jugador
coincide exactamente con la posición de `tp9`**, y como el orbe de la
sesión anterior quedó más grande, más brillante y a media altura (cerca de
los ojos), la cámara terminaba metida DENTRO del halo del propio orbe. Al
mover la cámara de prueba unos metros lejos del orbe de spawn, la linterna
se ve funcionando bien: ilumina el papel tapiz, el zócalo (ya con luz real
cerca) y el piso en un radio razonable.

**Pendiente real para Omar, no resuelto acá:** el jugador va a spawnear
literalmente adentro/pegado al brillo de `tp9` cada vez que arranca la
escena — vale la pena decidir si mover el punto de spawn unos metros, mover
`tp9` un poco, o achicar el brillo/tamaño de ESE orbe en particular
(dejando los demás como están). No se tocó porque es una decisión de
diseño (dónde arranca el jugador), no un bug técnico.

**Valores finales de luz en `Parte 4` (por si hace falta retocar):**
- 17 luces en `Luces Tetricas`: `Point`, intensity `35`, range `16`,
  `FlickeringLight._baseIntensity = 35`.
- Linterna (`Spot Light` hijo de `Main Camera`): intensity `70`, range
  `28`. **Actualizado 2026-09-09:** se le agregó `FlickeringLight`
  (Omar pidió el mismo efecto de parpadeo/apagones que las demás luces) —
  `_baseIntensity` seteado explícitamente en `70` para que coincida con el
  valor ya calibrado (si se dejaba en el default del script, `1.5`, la
  linterna real hubiese quedado casi apagada). Con esto, el valor que se ve
  en `Light.Intensity` en el Inspector vuelve a ser cosmético en el Editor,
  igual que las demás luces con `FlickeringLight` — el real en Play es
  `_baseIntensity`.
- `GazeController._maxGazeDistance = 35`.

## Spawn a la altura real (9.73) y niebla de luces bajada de nuevo (`Parte 4`, 2026-09-09)

Después de la prueba real en Play (sección de abajo), Omar pidió: 1) subir
la altura de spawn de la `Main Camera` a `9.73` (la altura real a la que
salta sola al arrancar Play, en vez del `6.5` calculado a mano que se
usaba antes) y 2) bajar todas las luces, quiere que quede bien oscuro de
nuevo — la sesión anterior las había subido bastante (`35`/`70`) para
compensar haber sacado el reflejo del skybox y el cambio de Spot a Point.

**Valores nuevos:**
- `Main Camera` spawn: `Y = 9.73` (antes `6.5`).
- 17 luces de `Luces Tetricas`: `intensity 35→15`, `range 16→12`
  (`FlickeringLight._baseIntensity` igual, `15`).
- Linterna: `intensity 70→30`, `range 28→18`
  (`FlickeringLight._baseIntensity` igual, `30`).

**Bajado un escalón más (mismo día, Omar pidió "más oscuro aún"):**
- 17 luces: `intensity 15→8`, `range 12→10`.
- Linterna: `intensity 30→15`, `range 18→14`.
- Verificado de nuevo con prueba real en Play: quedó casi todo negro, con
  un pozo de luz lejano y los orbes marcando el camino — bastante más
  tenso/oscuro que la iteración anterior.

**Verificado con una prueba real en Play** (no solo captura en Editor,
gracias a la técnica de la sección de abajo) — quedó apropiadamente oscuro:
se distingue apenas el papel tapiz, el resto cae en negro real.

**Gotcha nuevo encontrado al guardar después de esta prueba — el revert de
Play mode no restaura bien los valores que un script modifica CADA
FRAME:** al salir de Play, `Main Camera.position` y `Light.intensity` (de
los objetos con `FlickeringLight`, que los reescribe en cada `Update()`)
**no volvieron a los valores limpios guardados** — quedaron con el último
valor "congelado" a mitad de un parpadeo (ej. `10.7` en vez de `15`). Esto
es distinto del comportamiento normal de Unity (que sí revierte cambios de
Play mode) — parece que un valor que un script reescribe en cada frame
"gana" en algún punto de la comparación de revert. **Antes de guardar
después de cualquier prueba en Play, conviene chequear
`Unity_ManageScene GetActive` (`isDirty`)** — si da `true` después de haber
salido de Play sin haber tocado nada a mano, es señal de este mismo
problema: hay que corregir esos valores de vuelta a mano (no simplemente
recargar la escena, la herramienta lo bloquea con cambios sin guardar de
por medio) antes de guardar, para no persistir sin querer un valor
"congelado a mitad de parpadeo" en vez del valor base real.

## Mucho más oscuro, velocidad de caminata, y el verdadero motivo del "cegado de cerca" (`Parte 4`, 2026-09-09)

Omar pidió: mucho más oscuro todavía ("apenas se pueda ver"), que los
orbes "siguen cegando cuando se está muy cerca", y subir la velocidad de
caminata (estaba muy lenta).

**Valores bajados aún más:**
- `Orb_Glow.mat`: `emissiveFactor` de `(1.3,1.29,0.04)` a
  `(1.0,0.99,0.03)`, `emissiveExposureWeight` de `0.5` a `0.3`.
- 17 luces de `Luces Tetricas`: `intensity 4→1.5` (rango se dejó en `20`).
- Linterna: `intensity 15→5` (rango se dejó en `14`).
- `TeleportManager._walkSpeed`: `2→5` (m/s).

**Sobre el "cegado de cerca" — investigado a fondo, no solo bajando
números:** se probó en Play real acercando la cámara al orbe de varias
formas distintas para entender la causa real:
1. Cámara a `0.5` unidades del orbe, A LA MISMA ALTURA que el orbe
   (`Y≈6.05`) → pantalla completamente blanca/amarilla, encandilado total.
2. Cámara a la altura REAL de ojos en Play (`Y=9.73`, ver sección de
   arriba) a `2.5` unidades de distancia horizontal del orbe → nada, ni
   siquiera se distingue el brillo.
3. Cámara en el escenario real de una llegada por teletransporte (mismo
   X/Z que el orbe, altura real de ojos `9.73`, mirando hacia abajo) →
   tampoco encandila, se ve tenue y normal.

**Conclusión: la prueba 1 era artificial e irreal.** El teletransporte por
caminata (`TeleportManager.DoWalk`) **solo cambia X/Z del jugador, nunca
la altura de ojos** (`targetY = sourcePoint.OverridesPlayerHeight ?
... : startPos.y` — ningún `TeleportPoint` de `Parte 4` tiene
`Overrides Player Height` tildado). Como los orbes están a `Y≈6.05` y la
altura real de ojos en Play es `≈9.73`, hay casi `3.7` unidades de
diferencia vertical — un jugador real NUNCA termina con la cámara
literalmente adentro/al lado del mesh del orbe después de teletransportarse,
por más cerca que esté horizontalmente. Con los valores de brillo YA
bajados en esta misma sesión (`emissiveFactor≈1.0`, `exposureWeight=0.3`),
los tres escenarios realistas probados NO mostraron nada de
encandilamiento — es muy probable que el problema que reportó Omar ya
estuviera resuelto por los cambios de la sesión anterior, y esta ronda de
bajar todo aún más lo termina de asegurar. Si Omar lo sigue viendo
encandilado en el celular real después de este cambio, es una señal fuerte
de que el problema NO es el brillo del material en sí, sino algo más (por
ejemplo, revisar si el jugador se detiene MUY cerca de un orbe por algún
otro motivo, o si hay una diferencia de comportamiento entre el Editor y
el build real de Android que no se pudo reproducir acá).

## Brillo del orbe saturando la mirada en el celular real + hitbox y luces ajustadas (`Parte 4`, 2026-09-09)

Omar probó compilando al celular real (no solo Editor) y reportó: el
brillo de los orbes es tan fuerte que interrumpe la mirada (el halo de
Bloom tapa/satura la zona del reticle, dificultando apuntar), pidió
agrandar el hitbox de los portales, y que las luces del pasillo "abarquen
más pero iluminen menos" (rango más grande, intensidad más baja — una
caída más lenta/suave en vez de una zona chica muy intensa).

**Cambios aplicados:**
- **`Assets/Materials/Orb_Glow.mat`** (el material extraído del orbe, ver
  sección de arriba): `emissiveFactor` bajado de `(4, 3.98, 0.12)` a
  `(1.3, 1.29, 0.04)`, y `emissiveExposureWeight` de `1` a `0.5` (ya no
  ignora tan agresivamente la exposición de la cámara). Verificado con
  prueba real en Play: el reticle quedó nítido, sin ningún halo tapándolo.
- **Hitbox de los 17 `TeleportPoint`:** el multiplicador sobre el bounding
  real del mesh subió de `x1.3` a `x2.2` (mismo método de siempre: calcula
  el tamaño a partir de `Renderer.bounds`, no a ojo).
- **Luces de `Luces Tetricas`:** `intensity 8→4`, `range 10→20` — la mitad
  de intensa pero el doble de alcance, para una caída de luz más gradual
  en vez de una pileta chica y marcada.

**Nota:** el brillo del orbe y el rango/intensidad de las luces reales son
ajustes de gusto que ya se tocaron varias veces esta sesión — si hace
falta retocar de nuevo, los valores actuales de referencia son:
`Orb_Glow.emissiveFactor=(1.3,1.29,0.04)`,
`emissiveExposureWeight=0.5`, luces `intensity=4/range=20`, linterna
`intensity=15/range=14`, hitbox `x2.2`.

## Primera prueba real en Play vía Unity MCP — el sistema de portales SÍ funciona (`Parte 4`, 2026-09-09)

Omar preguntó si se podía probar jugando en vez de solo con capturas
estáticas. Se pudo: `Unity_ManageEditor` tiene acciones `Play`/`Pause`/
`Stop`, así que se entró a Play de verdad y se manipuló la `Main Camera`
por código (`Unity_RunCommand`) para simular "mirar" hacia un
`TeleportPoint" sin mando ni casco real.

**Gotcha grande encontrado en el camino — `Application.isFocused: False` +
`runInBackground: False` hace que el juego casi no avance frames:** la
primera tanda de pruebas (esperar con `sleep` de Bash y despues chequear)
no mostraba ningún progreso de gaze nunca, ni con la cámara apuntada
perfecto al objeto. La pista fue `Time.time` — después de varios `sleep 3`
reales, `Time.time` marcaba `0.02` segundos. La ventana del Editor no tiene
foco de SO mientras se interactúa vía MCP (no hay click ni teclado real en
la ventana), y con `runInBackground` en `False` (el default), Unity
prácticamente no corre `Update()` en ese estado — los frames solo avanzan
en ráfagas cuando el bridge de Unity MCP fuerza actividad en el Editor.
**Fix para poder probar:** `Application.runInBackground = true` seteado en
runtime al entrar a Play (no se guarda, es una propiedad de sesión, vuelve
sola al valor del proyecto al salir de Play) — con eso `Time.time` avanzó
normal y los `sleep` de Bash volvieron a corresponder a tiempo real del
juego.

**Segundo gotcha:** el `TrackedPoseDriver` de Cardboard (sin dispositivo
real conectado) pisa la rotación de la cámara cada frame con una pose
simulada — para poder "apuntar" la cámara a mano y que se mantenga, hubo
que desactivar `TrackedPoseDriver.enabled` durante la sesión de Play
(se revierte solo al salir de Play, como cualquier cambio de Play mode).

**Resultado de la prueba real (una vez resueltos los dos gotchas de
arriba):** apuntando la cámara real a `tp9` y esperando ~3 segundos reales,
`TeleportManager.CurrentOccupiedPoint` pasó de `NULL` a `tp9` — la cadena
completa (`GazeController` detecta → acumula progreso → `OnGazeSelect()` →
`TeleportManager.RequestTeleport()` → llegada) funcionó de punta a punta.
Repitiendo el mismo test apuntando a `tp8-9` y sosteniendo la mirada
(en modo caminata, no fade), **el jugador caminó de verdad** — `Main
Camera`/`Player` pasaron de `X=-47.99` a `X=-1.18` (terminó en `tp8`,
no en `tp8-9`, porque al mantener la rotación fija del mundo mientras el
jugador camina, el rayo terminó re-alineándose con el siguiente punto de
la fila una vez que el anterior quedó atrás — comportamiento esperable de
la técnica de prueba, no un bug real: un jugador real ajusta la cabeza
mientras camina, no mantiene una rotación de mundo fija). **Conclusión:
el sistema de portales de `Parte 4` funciona correctamente** — los
problemas anteriores eran reales (rango de gaze, luces, HDR) y ya están
resueltos; esto lo confirma con una prueba de extremo a extremo, no solo
estática.

**Hallazgo nuevo, sin explicar todavía:** al entrar a Play, la `Main
Camera` saltó sola de `Y=6.5` (el spawn guardado) a `Y=9.73` — una
diferencia de `+3.23` unidades, ni bien arrancó el Play, sin que ningún
script propio del proyecto la tocara todavía en ese instante. Es
consistente con algún tipo de "modelo de cuello" (neck model) del SDK de
Cardboard, que simula la altura/offset de los ojos respecto a un pivote de
cuello aunque `Tracking Type` esté en `Rotation Only` — pero no se confirmó
la causa exacta. **Importante:** esto significa que la altura de ojos REAL
en Play puede ser sistemáticamente más alta que lo que muestran las
capturas en modo Editor sin Play (que usan la posición cruda del
`Transform`, sin este ajuste) — los cálculos de "media altura" de los
orbes, por ejemplo, se hicieron asumiendo altura de ojos `≈6.5`, pero si en
Play real es `≈9.73`, los orbes podrían terminar viéndose más abajo
(a la altura de las rodillas/cintura en vez del pecho) de lo planeado.
**Pendiente:** confirmar esto en un build real o entendiendo mejor el SDK
de Cardboard, y si se confirma, recalibrar la altura de los orbes en
consecuencia.

## Vista Scene mucho más clara que la vista Game/Play — `Main Camera.allowHDR` estaba en `False` (`Parte 4`, 2026-09-09)

Omar reportó una captura de pantalla real del Editor en Play: la vista
**Scene** (arriba) mostraba el cuarto bastante iluminado (mesa, zócalo,
orbe, todo visible), pero la vista **Game** (abajo, lo que realmente ve el
jugador) estaba casi completamente negra — solo se distinguía el punto
blanco del reticle. Pidió que se vuelva a ver oscuro "como antes", pero
la discrepancia entre las dos vistas (misma escena, mismo instante) era la
pista real de que no era solo "hay que bajar la intensidad" — bajar más
las luces hubiese dejado la vista Game (ya casi negra) directamente
imposible de jugar.

**Causa encontrada:** el componente `Camera` de `Main Camera` tenía
**`allowHDR = False`**. El `UniversalRenderPipelineAsset` del proyecto
(`PC_RPAsset`) sí soporta HDR (`supportsHDR: true`), pero la cámara en
particular lo tenía apagado. Con las intensidades de luz que se fueron
subiendo en esta sesión (`35` en las 17 luces, `70` en la linterna, más el
`emissiveFactor` del orbe llevado a valores HDR reales como `(4, 3.98,
0.12)`), sin HDR esos valores no se representan correctamente en el buffer
de color antes de pasar por `Bloom`/`Vignette`/`Chromatic Aberration` — la
vista Scene del Editor no necesariamente usa la misma configuración de la
cámara real, así que mostraba algo mucho más parecido a lo "esperado" sin
sufrir ese problema, mientras la vista Game (la cámara real, con HDR
apagado) quedaba muy oscura por el procesamiento incorrecto de esos valores
tan altos.

**Fix:** `Camera.allowHDR = true` en la `Main Camera` de `Parte 4`.
Verificado con una captura desde la cámara real (misma que usa el juego):
el resultado quedó en un punto medio razonable — bastante más oscuro que lo
que mostraba la vista Scene antes del fix (con lo cual probablemente
resuelve el "se ve con muchísima más luz" de Omar), pero ya no el negro
total que mostraba la vista Game rota (con lo cual sigue siendo jugable/
visible).

**Pendiente de confirmar por Omar:** este fix se verificó con
`Unity_Camera_Capture` en el Editor, no jugando de verdad — falta que
confirme en Play real (donde además `FlickeringLight` sí hace parpadear/
apagar las luces de verdad, algo que ninguna captura estática puede
mostrar) si el balance de oscuridad quedó bien, o si además hace falta
bajar un poco más las intensidades ahora que el render es correcto.

**Duda para revisar más adelante, no resuelta acá:** el
`UniversalRenderPipelineAsset` activo se llama `PC_RPAsset` — dado que el
build target del proyecto es Android (ver "Entorno técnico"), vale la pena
confirmar en algún momento que este sea realmente el asset usado para el
build de Android y no un asset pensado para probar en PC con configuración
distinta (calidad más alta, HDR con más bits, etc.) que podría no rendir
igual en el Samsung A54 real. **Resuelta 2026-09-23 (qa-performance):**
Android usa el nivel **Mobile** (`Mobile_RPAsset` + `Mobile_Renderer`), no
`PC_RPAsset`; `allowHDR` sí funciona en el celular porque `Mobile_RPAsset`
también soporta HDR. La diferencia que sí importa es otra (Forward con tope
de 4 luces por objeto): ver "QA de build Android / A54".

## Puntos intermedios, altura y brillo de los orbes (`Parte 4`, 2026-09-09)

A pedido de Omar: agregar `TeleportPoint` intermedios entre los 9
originales (los saltos entre luces eran muy largos — 30 a 60 unidades),
subir la altura de los orbes (estaban casi a ras del piso) y aumentar su
brillo.

### Puntos intermedios (8 nuevos, `tp1-2`, `tp2-3`, ..., `tp8-9`)

Los 9 `TeleportPoint` originales trazan el recorrido en orden (`tp1`→`tp2`→
...→`tp9`), pero varios tramos consecutivos NO son una línea recta
caminable — el pasillo dobla en esquina. Se comprobó (no se asumió) con dos
métodos:
1. Primero se intentó un `Physics.Raycast` hacia abajo en el punto medio de
   cada tramo — varias de las losas de piso de esta escena son
   prácticamente de espesor cero, y el raycast contra ellas fallaba de
   forma inconsistente (a veces ni siquiera detectaba losas donde
   claramente había piso) — **no confiar en raycast contra mallas de piso
   ultra finas, dan falsos negativos**.
2. Método que sí funcionó: comparar el punto candidato contra los
   `Renderer.bounds` (rectángulo XZ) ya conocidos de las losas de piso reales
   (`Object_16`, `Object_100`, `Object_126`, `Object_166`, `Object_202`,
   `Object_260`, `Object_308`, `Object_306`, `Object_304`, `Object_322`) —
   sin física de por medio, solo geometría de los bounds. Con esto se
   confirmó que 4 de los 8 tramos SÍ tienen punto medio recto válido
   (`1→2`, `3→4`, `5→6`, `8→9`) y los otros 4 doblan en esquina — para esos
   se probaron las dos esquinas posibles de cada giro en L y se usó la que
   caía dentro de una losa real (`2→3`, `4→5`, `6→7`, `7→8`).
- Los 8 nuevos son duplicados exactos de un orbe ya arreglado (mismo
  material, mismo fix del offset interno, mismo `BoxCollider` calculado por
  bounding) — no hubo que rehacer ninguno de los fixes anteriores a mano.
- **Total ahora: 17 `TeleportPoint`** bajo `Teletransportadores`
  (`tp1`..`tp9` + `tp1-2`..`tp8-9`).

### Altura de los orbes ("media altura", no a ras del piso)

Estaban a `Y = floor + 0.45` (prácticamente a la altura del tobillo). Se
subieron todos a `Y = floor + 1.2` (`≈6.05` en esta escena, con
`floor≈4.85`) — a media altura del cuerpo del jugador, no en el piso. Al
mover el pivote, se recalculó el `BoxCollider` de cada uno contra el
`Renderer.bounds` actualizado (mismo método que el fix del offset interno)
para que el hitbox se mantenga centrado en el mesh después del movimiento.

### Brillo del orbe — material extraído y con emisión subida

El material del orbe viene embebido en `model_3glow_orb.glb`, de solo
lectura (ver gotcha #5) — **`AssetDatabase.ExtractAsset` no funcionó**
(`"El importer ScriptedImporter debe soportar remapeo de sub-assets de tipo
Material"` — el importer de glTFast no lo soporta). Alternativa que sí
funcionó: crear un material NUEVO desde cero con el mismo shader
(`new Material(embeddedMat.shader)`), copiarle todas las propiedades del
original (`CopyPropertiesFromMaterial`), y guardarlo como asset real:
**`Assets/Materials/Orb_Glow.mat`**. Reasignado a los 9 (ahora 17) orbes.

Dos cambios en ese material nuevo para que brille más:
- `emissiveFactor`: de `(1, 0.996, 0.029)` a `(4, 3.98, 0.12)` — los
  shaders glTF nominalmente esperan este valor en rango `0-1`, pero nada
  impide ponerlo más alto acá (no se va a re-exportar a `.glb`) — un valor
  HDR más alto hace que el `Bloom` reaccione con más fuerza.
- `emissiveExposureWeight`: de `0` a `1` — esta propiedad controla si la
  emisión se trata como parte de la escena (afectada por la exposición de
  la cámara, que en este proyecto usa parámetros de cámara física —
  `ISO`/`Shutter`/`Aperture`, ver `Camera` de `Main Camera`) o si se
  muestra "cruda", ignorando la exposición. En `0` la cámara física puede
  estar atenuando el brillo sin que se note en el Inspector del material.
- Verificado con captura antes/después: el halo de Bloom alrededor del
  orbe quedó notablemente más grande e intenso.

**Nota para el futuro:** si se necesita variar el brillo de los orbes de
nuevo, el material a tocar es `Assets/Materials/Orb_Glow.mat` — no el
material embebido en el `.glb` (que ya no se usa en ninguno de los 17
puntos, pero sigue existiendo dentro del archivo importado por si se
necesita comparar el original).

## Linterna del jugador demasiado débil para el tamaño del pasillo (`Parte 4`, 2026-09-09)

Omar reportó que la linterna (el `Spot Light` hijo de `Main Camera`, el
mismo puntero que también usa el reticle de Cardboard — es un solo objeto
con doble función) "no ilumina casi nada hacia adelante". Diagnosticado
apagando primero la hipótesis de las normales invertidas (se revisaron las
paredes reales frente al spawn con la técnica de siempre — normales
correctas, `dot ≈ +1`, así que NO era ese bug) — la causa real es más simple:
`intensity 20` y `range 10` son insuficientes para el tamaño real de este
pasillo (166x108 unidades, techos de ~12-17 de alto) — a la distancia real
donde está la pared más cercana al spawn (~6.6 unidades), la caída por
inverso del cuadrado prácticamente no dejaba nada visible.

**Fix:** `intensity 20 → 45`, `range 10 → 18`. **A diferencia de las 9 luces
de `Luces Tetricas`, esta linterna NO tiene `FlickeringLight`** — confirmado
antes de tocar nada, así que el valor que se ve en el Inspector **sí** es el
valor real en Play (no hay sorpresa de "el número real es otro" acá). Se
verificó con una captura desde la posición real de spawn del rig, mirando
por el pasillo: ahora se ve el piso de madera cerca de los pies y la pared
lejana con su estampado, en vez de nada. **Sigue siendo un punto de
partida** — recomendable que Omar lo pruebe caminando de verdad antes de
darlo por definitivo, sobre todo porque una franja "oscura" a distancia
media (entre el piso cercano bien iluminado y la pared lejana apenas visible)
puede ser simplemente el volumen abierto del pasillo (nada que iluminar ahí,
normal en un pasillo con techo muy alto) o puede necesitar más ajuste — no
se determinó con certeza cuál de las dos cosas es.

## Auditoría de `reflectionIntensity` en el resto de las escenas (2026-09-09)

A pedido de Omar, se chequeó (sin modificar, solo diagnóstico) si el mismo
bug de reflejos del skybox (ver sección de arriba) también afecta a las
demás escenas que bajan la luz ambiente:

| Escena | `reflectionIntensity` | `ambientIntensity` | Riesgo |
|---|---|---|---|
| `Parte 1 - El Despertar` | **0.1** | 0 | Bajo — alguien ya lo había atenuado antes (no es el default 1), consistente con que Omar no vio el problema ahí. |
| `Parte 3 - Pasillo de hotel` | **1** (default, sin tocar) | 0 | **Mismo setup riesgoso que tenía `Parte 4` antes del fix** — probablemente tiene el mismo bug latente, simplemente no reportado todavía (quizás sus materiales predominantes son menos brillantes). **No se tocó** — es la escena más activamente trabajada, cualquier cambio de iluminación ahí merece pedirse explícitamente antes de aplicarlo. |
| `Menu Principal` | 1 (default) | **1** (Flat, sin bajar) | Bajo — esta escena NO apaga la luz ambiente (no es una escena "a oscuras" como las demás), así que el reflejo extra se mezcla con la luz normal en vez de aparecer como una luz fantasma en la nada. |

**Pendiente real:** si en algún momento se decide aplicar el mismo fix a
`Parte 3`, es literalmente una sola línea (`RenderSettings.reflectionIntensity
= 0`) — pero conviene probarla ahí con el mismo método de captura antes/
después usado en `Parte 4`, porque esa escena también tiene luces reales que
podrían necesitar recalibrarse después (mismo efecto secundario que pasó acá
con `Luces Tetricas`).

## Sway de linterna, ronda final de brillo/alturas y verificación de las 21 paredes (`Parte 4`, 2026-09-09)

Después de probar "un rato" en el celular real, Omar reportó una lista de 6
puntos: 1) las luces de los orbes seguían muy fuertes, 2) había paredes que
no reaccionaban a la luz, 3) quería la linterna más intensa, 4) pidió un
efecto de **sway** (delay) para que la linterna se demore un poco en seguir
hacia donde mira la cabeza, 5) subir un poco más la altura de la cámara, y
6) subir la altura de los orbes.

### 1. Orbe bajado un escalón más

`Orb_Glow.mat`: `emissiveFactor` de `(1.0, 0.99, 0.03)` a
`(0.5, 0.49, 0.015)`, `emissiveExposureWeight` de `0.3` a `0.1`. Valor de
referencia más bajo hasta ahora en toda la sesión — si Omar lo sigue viendo
fuerte, este es el archivo a tocar de nuevo.

### 2. Las "paredes que no reaccionan" — el fix de la sesión anterior SÍ estaba aplicado

El intento anterior de arreglar las normales invertidas de 21 objetos más
(`Object_14, 48, 54, 60, 66, 72, 78, 84, 90, 96, 130, 162, 226, 236, 270,
272, 278, 282, 288, 294, 298`) había quedado sin verificar porque el
harness de Unity MCP marcó la llamada como `UNEXPECTED_ERROR` (por los 21
warnings benignos de "Instantiating mesh... will leak meshes", el mismo
patrón ya conocido de `Object_258`/`Object_178`) aunque el log interno
decía "21 de 21" corregidos. Se re-verificó con el mismo método de
`MeshCollider` temporal + `Physics.Raycast` + comparación de normales
(`Vector3.Dot`) usado en el bug original — **las 21 dieron `dot > 0.99`
(correctas)**, confirmando que el fix anterior sí se había aplicado
realmente pese al error cosmético del harness. **Lección para el futuro:**
un `UNEXPECTED_ERROR` de Unity MCP con muchos warnings de "leak meshes" no
significa necesariamente que la operación haya fallado — conviene
re-verificar con una consulta de solo lectura antes de asumir que hay que
reintentar (reintentar a ciegas podría haber vuelto a invertir normales ya
corregidas).

### 3. Linterna más intensa

`intensity` de `5` a `40`, `_baseIntensity` (FlickeringLight) igual, a
`40` (rango se dejó en `14`).

### 4. Nuevo script `FlashlightSway.cs` — efecto de demora/sway

**`Assets/Scripts/Ambient/FlashlightSway.cs`** (nuevo): hace que la
ROTACIÓN de la linterna se demore en seguir a la cámara en vez de girar
rígidamente pegada a la cabeza (la posición sigue rígida, sigue siendo
hija de `Main Camera`). Guarda en `Awake()` la rotación LOCAL que tenía
respecto al padre como "offset de apuntado" fijo, y en `LateUpdate()`
usa `Quaternion.RotateTowards(transform.rotation, parent.rotation *
offset, _followSpeedDegrees * Time.deltaTime)` — un patrón estándar de
demora angular limitada por grados/segundo. Agregado al `Spot Light`
(linterna) hijo de `Main Camera`, con `_followSpeedDegrees = 220`
(bastante rápido — sigue sintiéndose responsivo pero con un dejo de
demora; si Omar lo quiere más notorio, bajar este número lo hace más
lento/pesado).

**Gotcha de testing importante, no del código en sí — verificar un efecto
de demora angular vía Unity MCP es casi imposible de forma numérica:**
se intentó confirmar el lag girando la cámara 90° de golpe y midiendo el
ángulo restante entre linterna y cámara en una llamada posterior. Dio
`0.0` siempre, sin importar si `_followSpeedDegrees` se bajaba a `10` o
incluso `0.5`. Causa: 1) cada llamada a `Unity_RunCommand` tiene su propio
round-trip de compilación + red, que en la práctica dura varios segundos
reales — tiempo de sobra para que `RotateTowards` cierre CUALQUIER gap
dado que el objetivo (la rotación de la cámara) se queda quieto entre
llamadas (no es un giro continuo como el de una cabeza real); y 2) en un
intento de "reset" del test se igualó la rotación local de la linterna a
la de la cámara justo antes de girar esta última, lo cual sin querer puso
el offset guardado en identidad — con offset=identidad, la propagación
automática de Unity padre→hijo hace que la rotación mundial de la
linterna YA sea idéntica a la de la cámara en cuanto esta gira, sin que
haga falta que corra ningún script, así que el "error" a corregir por
`RotateTowards` es cero por construcción, no por un bug. **Conclusión: la
única forma confiable de confirmar el timing exacto de un efecto de este
tipo es probarlo en el dispositivo real** (donde la cabeza gira de forma
continua, no en saltos separados por segundos de latencia) — la
implementación se validó por lectura de código (patrón estándar, sin
errores de compilación, componente presente/activo confirmado en Play),
pero el "se siente bien" final depende de que Omar lo prueebe caminando y
ajuste `_followSpeedDegrees` a gusto si hace falta.

### 5 y 6. Alturas subidas

`Main Camera` spawn: `Y = 9.73 → 11`. Los 17 `TeleportPoint` (orbes):
`Y = 6.05 → 7.5`.

**Valores de referencia actuales tras esta ronda** (por si hace falta
retocar de nuevo): `Orb_Glow.emissiveFactor=(0.5,0.49,0.015)`,
`emissiveExposureWeight=0.1`; linterna `intensity=40, range=14,
FlashlightSway._followSpeedDegrees=220`; `Main Camera` spawn `Y=11`;
`TeleportPoint` `Y=7.5`.

**Verificado:** se entró a Play real (`Unity_ManageEditor Play` +
`Application.runInBackground=true`, misma técnica que sesiones
anteriores) y se confirmaron todos los valores numéricamente en runtime
(incluidas las 21 paredes, ver punto 2). Al salir de Play,
`Unity_ManageScene GetActive` dio `isDirty: false` — no hizo falta
restaurar ningún valor "congelado a mitad de frame" esta vez (el gotcha de
sesiones anteriores sobre esto sigue siendo válido, simplemente no se dio
en esta ronda porque la escena ya estaba guardada antes de entrar a Play).

## QA de build Android / A54 (qa-performance, 2026-09-23)

Auditoría de solo lectura (sin compilar ni tocar escenas). Lo que sigue es lo
que hay que tener en cuenta **antes de dar por buena cualquier prueba hecha
en el Editor**, porque el celular no renderiza igual.

### El celular usa el nivel de calidad Mobile, no el del Editor

- `QualitySettings.m_PerPlatformDefaultQuality: Android: 0` → Android usa el
  nivel **Mobile** (`Mobile_RPAsset` + `Mobile_Renderer`). El Editor usa el
  nivel PC (`m_CurrentQuality: 1`, `PC_RPAsset` + `PC_Renderer`). Esto
  resuelve la duda vieja de la sección de `allowHDR`.
- **Diferencias Mobile vs. PC:** Forward (tope de **4 luces adicionales por
  objeto**) vs. Forward+ (sin tope); render scale 0.8 vs. 1; sin SSAO vs.
  SSAO; 1 cascada de sombras de 1024 sin sombras suaves ni de luces
  adicionales vs. 4 cascadas de 2048; Skin Weights 2 vs. 4 huesos por
  vértice. HDR y MSAA iguales (HDR sí, MSAA apagado). Tope total por cámara
  en mobile: 32 luces visibles (Parte 4 tiene 18, no se cortan por eso).
- **Para ver en el Editor lo mismo que el celular:** `Project Settings >
  Quality` y hacer clic en la fila "Mobile".

### Riesgos

- **R1 — Parte 4 en el celular: solo 4 de las 18 luces tocan las paredes.**
  `Object_258` es UNA sola malla para todo el edificio, y en Forward Unity
  elige las 4 luces por el objeto entero, no por dónde está el jugador. Es
  la causa más probable de "paredes que no reaccionan a la luz" en el A54
  (visual-fx llegó a lo mismo por su lado). Opciones (decisión #16):
  `Mobile_Renderer` → Forward+ (medir FPS; afecta todas las escenas), subir
  el tope a 8 (no alcanza para 18), o partir la malla.
- **R2 — `SceneTransitionOverlay` no se ve en Cardboard.** URP 17 no dibuja
  un Canvas Screen Space Overlay en los ojos con XR activo
  (`UniversalRenderPipeline.cs` líneas 2601–2607, `AdjustUIOverlayOwnership`).
  El tramo Tutorial → Parte 1 (`LevelDoor`, del compañero) casi seguro se ve
  como ~3.7 s de Tutorial con el gaze activo y después un corte seco.
  Solución: que `LevelDoor` use `FaintOverlay` (consultar, #19). Confirma y
  endurece el gotcha #7.
- **R3 — Texturas embebidas en `.glb`:** glTFast 6.20 las crea en R8G8B8A8
  sin compresión por plataforma → probablemente **~1.07 GB sin comprimir en
  el APK** (inferencia; confirmar con el Build Report del `Editor.log`).
  Estimación por escena: Menú ~176 MB, Tutorial ~94, Parte 1 ~186, Parte 2
  ~117, Parte 3 ~70, Parte 4 ~14, Parte 5 ~140, Final ~272 (`kidman_room`,
  116 texturas). **En cada desmayo conviven dos escenas en memoria.**
  Solución: reexportar como `.gltf` con imágenes externas (así Unity las
  importa como texturas normales y las puede comprimir) o bajarlas a 512
  (decisión #20).
- **R4 — Post-procesado:** los dos RP assets usan
  `Assets/Settings/SampleSceneProfile.asset` como perfil por defecto (Bloom
  `highQualityFiltering 1`, `skipIterations 0`, Tonemapping Neutral). El
  `Global Volume Profile` compartido tiene Screen Space Lens Flare
  intensidad 1 y Chromatic Aberration 0.091, caros y molestos en estéreo.
  Propuesta (perfil compartido con el compañero, decide Omar, #21):
  `highQualityFiltering` off, `skipIterations 1`, apagar Lens Flare y
  Chromatic Aberration.
- **R5 — Parte 3:** la Directional negra proyecta sombras
  (`m_Shadows.m_Type: 2`): gasto sin ningún efecto visible. Ponerla en
  `No Shadows` (pedir antes de tocar Parte 3, #22).
- **HDR:** funciona en el celular (`Mobile_RPAsset` lo soporta). `Parte 1` y
  `Parte 3` tienen HDR apagado en la cámara (`m_HDR: 0`), así que su Bloom se
  ve distinto al de `Parte 4`.

### Bloqueantes

- **B1 — Parte 2, Parte 5 y Parte Final sin rig:** tienen geometría
  (`rec_room…`, `backrooms_vr`, `kidman_room`) pero no `TrackedPoseDriver`
  ni `GazeController`: después de un desmayo hacia ellas la cabeza no rota y
  no hay salida.
- **B2 — `rec_room_-_backstage_of_reality_-_level_fun.glb`** (Parte 2, del
  compañero): 451.288 triángulos en 5.258 mallas → ~10.500 draw calls por
  frame en multi-pass. No es viable en el A54 tal cual; avisarle (#19).

### Menores

- **M1:** el `Debug.Log` de `GazeController` ya no existe (ver sección de
  gaze).
- **M2:** `CameraDebugLogger` sigue activo en `Parte 1` (dejarlo hasta la
  prueba, loguea XR/estéreo); `GazeDebugTarget` no está en ninguna escena.
- **M3:** `Menu Principal` no tiene Bloom (ver sección de Bloom).
- **M5:** `ScreenBlink._eyelidShader` y `Peephole._maskMaterial`: con el
  campo vacío usan `Shader.Find`, que puede devolver null en el APK (gotcha
  #25). Asignar siempre el campo.
- **M6:** los shaders propios (`UI/AlwaysOnTop`, `Custom/AbyssVoid`,
  `Custom/EyelidOverlay`) **no soportan single-pass instanced**; hoy andan
  porque Cardboard está en multi-pass (`m_StereoRenderingPath: 0`). **No
  cambiar a single-pass** sin adaptarlos antes (se verían en un solo ojo).
- **M7:** probar MSAA 2x en `Mobile_RPAsset` (casi gratis en GPUs Mali como
  la del A54).

### Presupuesto del monstruo para el A54

10–15k triángulos (máximo 25k); 1 malla con esqueleto + la máscara como
pieza rígida colgada del hueso de la cabeza (así `RemoveMask` la puede
soltar); 2–3 materiales opacos; ≤50 huesos, pensando en 2 huesos por vértice
(es lo que usa el nivel Mobile); 1 textura de 1024² (+ normal opcional) **en
archivo aparte** (FBX o `.gltf`, no `.glb` embebido, por R3); `Animator` con
Culling Mode `Cull Update Transforms`, `updateWhenOffscreen = false`, sin
root motion ni sombras; 4 animaciones (caminar, correr, quieto/mirar,
quitarse la máscara).

### Plan de prueba en el A54

1. APK nuevo con **Development Build + Autoconnect Profiler**, y `adb logcat`
   filtrado por
   `FaintTransition|FaintOverlay|AbyssFall|WallCollapse|CodeLock|TeleportManager|LevelDoor|SceneTransitionOverlay|CameraDebugLogger`.
2. **Build A:** Menú → Tutorial (¿se ve el fundido de `LevelDoor`?) →
   Parte 1, candado → desmayo → Parte 2.
3. **Build B** (destildar temporalmente Menú..Parte 2 en Build Settings,
   **NO commitear eso**): Parte 3 abismo → Parte 4 (luces en tp1/tp5/tp9,
   FPS) → pared ciega → Parte 5.
4. Si algo se ve **magenta**: `logcat` sin filtro (un shader que no entró al
   build o no compila en GLES3).

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
   depth-testing (ver sección de gaze más arriba). **Confirmado por código
   (2026-09-23):** URP 17 directamente **no dibuja** un Canvas Screen Space
   Overlay en los ojos cuando XR está activo (`AdjustUIOverlayOwnership`); no
   es que "se vea mal", es que no se ve. Para algo que tiene que sobrevivir
   el cambio de escena (y por lo tanto no puede ser hijo de la cámara), usar
   el truco de la esfera alrededor de la cámara (`FaintOverlay`,
   `ScreenBlink`).
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
    especulando. Varios de los `Debug.Log` puestos en `Door.cs` (y en los
    scripts nuevos de transición) para diagnósticos anteriores siguen activos
    a propósito — son baratos y ya sirvieron más de una vez para el bug
    siguiente. *(El de `GazeController.cs` ya no existe desde el commit
    `00c9a0b`, ver sección de gaze.)*
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
    asset `.glb`/`.fbx` original. *(Efecto secundario, 2026-09-23: cada malla
    clonada así queda serializada dentro del `.unity`; por eso
    `Parte 4 - Pasillo de madera.unity` pasó de ~1.26 MB en el último commit a
    ~1.57 MB, con 23 `Mesh` embebidas: `Object_258`, `Object_178` y las 21
    paredes. No es un error, pero explica el tamaño.)*
17. **Medir una pared de un glTF con raycast puede darte la cara de ATRÁS**
    (2026-09-23, bug de la pared invisible de Parte 4). Si el padre de la
    malla tiene **escala negativa** (pasa en `house_corridor_interior`,
    `duvar.071_169`) y el material es doble cara, el winding de los
    triángulos queda invertido y, con `Physics.queriesHitBackfaces = false`
    (el default), el rayo se saltea la cara visible y pega en la exterior.
    Además las paredes tienen espesor real (0.60 m en ese caso). **Siempre
    medir con `queriesHitBackfaces = true`, desde los dos lados, y calcular
    el espesor** antes de pegar algo "delante" de una pared.
    `WallCollapse.CheckPlacement()` ya lo hace solo.
18. **En el celular (nivel Mobile, Forward) cada objeto recibe como máximo 4
    luces adicionales**, elegidas por el objeto entero y no por dónde está el
    jugador. El Editor usa Forward+ (sin tope), así que **lo que se ve en el
    Editor no es lo que se ve en el A54**. Una malla gigante (como
    `Object_258`, todo el edificio de Parte 4) con muchas luces encima va a
    verse "sin luz" en el celular. Para previsualizar: `Project Settings >
    Quality` → fila "Mobile". Ver "QA de build Android / A54".
19. **`backrooms_vr.glb` es unlit con la luz horneada en las texturas:** una
    `Light` de Unity no lo afecta en nada. Para que "parpadeen los tubos"
    hay que modular el color de los materiales (`FluorescentFlicker`), no
    una luz. Antes de iluminar cualquier modelo importado, revisar si su
    shader es unlit.
20. **Destildar un componente `IGazeInteractable` NO lo saca de la mirada.**
    `GazeController` lo encuentra con `GetComponentInParent` (que devuelve
    componentes deshabilitados también) y le llama `OnGazeSelect` igual. Para
    "apagar" un `TeleportPoint`, una `Door`, etc., hay que **apagar su
    Collider o desactivar el GameObject**. Ojo con lo segundo: un
    `TeleportPoint` que arranca desactivado a mano recién se registra en el
    `TeleportManager` cuando se activa (`OnEnable`).
21. **En Unity 6 el clip de un `AudioSource` se guarda en `m_Resource:`**,
    no en `m_audioClip:` (que queda vacío). Si se audita audio leyendo el
    YAML, buscar `m_Resource`; si no, parece que ningún AudioSource tiene
    clip.
22. **`Unity_Camera_Capture` falla en Play** (con XR/estéreo activo),
    incluso usando una cámara temporal. Técnica que sí funciona (2026-09-23):
    crear una cámara temporal, `Camera.Render()` a un `RenderTexture`,
    `ReadPixels` + `EncodeToPNG`, escribir el PNG al scratchpad y leerlo.
    Bajar `Time.timeScale` sirve para capturar fases de una animación
    (`WallCollapse` y el `Pre Delay` usan tiempo escalado; los fundidos de
    `FaintOverlay` usan tiempo real, no se frenan).
23. **`Menu Principal` se genera por script** (`Tools/Feria de Ciencias/
    Generar escena 'Menu Principal'`, del compañero): arma la escena desde
    cero y la guarda, así que **cualquier cambio manual en esa escena se
    pierde** si alguien vuelve a correr el generador. Probablemente es por eso
    que el Bloom del Menú desapareció. Antes de tocar `Menu Principal` a mano,
    preguntar; si hace falta un cambio permanente, va en
    `Assets/Editor/MenuPrincipalGenerator.cs`, no en la escena.
24. **El rayo de la mirada atraviesa paredes.** Las paredes de los modelos
    importados no tienen Collider (gotcha #1), así que cualquier `tp` a menos
    de `_maxGazeDistance` se puede elegir aunque esté detrás de un muro (en
    Parte 4 hay 7 pares así, y con caminata el jugador "atraviesa" la pared).
    Arreglo disponible: MeshCollider en las paredes + `Occluder Layer Mask`
    de `GazeController` (decisión #15).
25. **`Shader.Find` puede devolver null en el APK** si ningún material de
    ninguna escena del build referencia ese shader (Unity lo deja afuera del
    build). Cualquier script que arme materiales por código (`FaintOverlay`,
    `ScreenBlink`, `Peephole`, `AbyssFall`, `WallCollapse`) tiene que tener
    **asignado el campo del shader/material en el Inspector**, no confiar en
    el fallback. Si algo se ve magenta en el celular, es lo primero a revisar.

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

### Estado real por escena (actualizado 2026-09-23 leyendo los `.unity`, no por descripción)

| Escena | Tamaño | Estado |
|---|---|---|
| `Menu Principal.unity` | 236 KB | **Funcional.** Paneles, `MainMenuController` (ahora valida la escena destino contra Build Settings), `ConfigMenu`/`GameSettings`, secuencia de entrada con puerta, `FlickeringLight`. **Sin Bloom** (sin `Global Volume`, Post Processing apagado; corregido 2026-09-23). Generada por script (`MenuPrincipalGenerator`, gotcha #23). |
| `Tutorial.unity` | 105 KB | **Del compañero.** Según su commit `00c9a0b` y `TutorialSetup`: llave (`keys` → `KeyInventory`), puerta de salida `LevelDoor` hacia `Parte 1` (texto "PARTE 1"), pantalla de carga, ambiente en loop y locución (`SceneNarration`). Su transición usa Screen Space Overlay (probablemente invisible en Cardboard, #19). Usa `Player.prefab` desempaquetado. |
| `Parte 1 - El Despertar.unity` | 224 KB | **Completa** (candado `067`, puertas, teletransporte, fog + Bloom + luces parpadeantes) **y con el desmayo a Parte 2 cableado** (2026-09-23, probado en Play 2026-09-24). HDR apagado en la cámara. `CameraDebugLogger` sigue activo en `Main Camera`. Usa `Player 1.prefab`. |
| `Parte 2 - Cumpleaños.unity` | 13 KB | **Del compañero.** Tiene `rec_room…` (demasiado pesado para el A54) pero **sin rig**. Corrección: no estaba vacía. |
| `Parte 3 - Pasillo de hotel.unity` | 675 KB | **Con el abismo cableado** (2026-09-23, probado en Play 2026-09-24): `27_2` (con llave) → `tp pozo` → `AbyssFall` → `Desmayo -> Parte 4`. 4 `Door` + 6 `TeleportPoint` tipo puerta (fade) con audio, llave `key_with_tag` en `hayama_washitsu_raw_scan`. Pendientes de iluminación a pedir (#22) y el portazo al cargar (`AudioSource` de `FadeImage`). Rig propio (no prefab). |
| `Parte 4 - Pasillo de madera.unity` | 1.5 MB | **La más avanzada de la historia.** Ambiente tétrico, rig, 18 `TeleportPoint` con orbe (17 + `tp pared ciega`), linterna con `FlashlightSway`, 21+2 paredes con normales corregidas, y **la pared que se desmorona + desmayo a Parte 5 probados en Play** (arreglo guardado a las 21:14). Falta: loop, pintura, audio (no hay ningún `AudioSource`), y probar en el A54 (ojo: solo 4 luces por objeto en Mobile, gotcha #18). Sin commitear. |
| `Parte 5 - Backrooms.unity` | 13 KB | `backrooms_vr` puesto, **sin rig** → bloquea toda la etapa 5. Corrección: no estaba vacía. |
| `Parte Final.unity` | 13 KB | `kidman_room` puesto (116 texturas, la escena más pesada en memoria), **sin rig**. Corrección: no estaba vacía. |

Build Settings (`ProjectSettings/EditorBuildSettings.asset`), verificado
2026-09-23: **las 8 escenas de la historia están tildadas y en orden** (0
Menú, 1 Tutorial, 2 Parte 1, 3 Parte 2, 4 Parte 3, 5 Parte 4, 6 Parte 5, 7
Final); el sample HelloCardboard está destildado. *(Antes decía "solo
`Parte 3` tildada": quedó viejo.)*

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
- `FlickeringLight` en Menú Principal. *(El Bloom del Menú que figuraba acá
  no existe: ver sección de Bloom.)*
- `Parte 3`: puertas físicas y teletransportes-puerta con audio funcionando
  (arreglado 2026-09-08).
- Unity MCP configurado y **usado a fondo** desde 2026-09-08 (lectura de
  escena viva, `Unity_RunCommand`, pruebas en Play). La nota vieja de
  "pendiente confirmar la conexión" quedó obsoleta.
- **Transición "desmayo"** (`FaintOverlay` + `FaintTransition`, VR-safe,
  llamable desde `UnityEvent`), cableada en Parte 1 → 2, Parte 3 → 4 y
  Parte 4 → 5 (2026-09-23). **Las tres probadas en Play de punta a punta**
  (4 → 5 el 2026-09-23; 1 → 2 y 3 → 4 el 2026-09-24), sin errores. Falta el A54.
- **Final de la etapa 4** (`WallCollapse`): bug de la pared invisible
  resuelto y guardado (2026-09-23).
- **Final de la etapa 3** (`AbyssFall` detrás de la puerta con llave `27_2`),
  cableado 2026-09-23.
- Build Settings completo y en orden (2026-09-23).
- **Código listo, sin instanciar (2026-09-23):** `MonsterController`,
  `Peephole`, `UpsideDownPainting`, `FragmentCounter`/`FragmentDropOff`,
  `ScreenBlink`, `FluorescentFlicker`, `AudioFader`, loop sin fade
  (`TeleportPoint.Seamless Loop`), `TeleportManager.LockMovement`,
  `GazeController.Occluder Layer Mask`. Todo compila sin errores ni
  warnings.

**Pendiente / a implementar (por orden de lo más bloqueante):**
- **Commitear lo de la sesión 2026-09-23** (en una rama + PR, nunca directo
  a `main`): hoy están sin commitear `Parte 1`, `Parte 3`, `Parte 4`,
  `Build Settings`, `CLAUDE.md` y todo el código nuevo (`Transitions/`,
  `Monster/`, `Puzzles/`, `Audio/`, `ScreenBlink`, `FluorescentFlicker`,
  shaders). (#7 ya resuelta: Parte 4 se guardó al cerrar Unity.)
- **Probar en el A54 los tres desmayos** siguiendo el plan de "QA de build
  Android / A54" (Build A y Build B). Es lo que más información da ahora:
  confirma o descarta el tope de 4 luces (#16), la transición del Tutorial
  (#19) y el peso de las texturas (#20).
- **`Parte 2`, `Parte 5` y `Parte Final` tienen geometría pero NO rig**
  (corregido 2026-09-23: no estaban vacías). Sin rig, un desmayo hacia ellas
  deja al jugador con la cabeza quieta y sin salida. Parte 2 es del
  compañero (#19); Parte 5 y Final, decidir quién (#14).
- **Etapa 4:** cablear el loop (#11) y la pintura (#12), dejar `tp pared
  ciega` apagado hasta enderezarla (Collider o GameObject, gotcha #20),
  poner audio (no hay ningún `AudioSource` en la escena; pasos mudos).
- **Etapa 3:** mirillas (`Peephole`, #10), luz roja sobre la llave (receta
  en "Efectos visuales"), monstruo acechando (#9), y probar el abismo en
  Play.
- **Etapa 1:** reloj/nota/foto (#8) y clip de latidos al arrancar.
- **Monstruo:** conseguir el modelo (#3) dentro del presupuesto de "QA de
  build Android / A54".
- **Audio:** conseguir los ~30 clips faltantes (ver "Audio: AudioFader y
  bugs encontrados") y resolver los bugs de escena (#18).
- **Rendimiento:** texturas embebidas (#20), perfil de post-procesado
  compartido (#21), `Mobile_Renderer` (#16), Directional con sombras en
  Parte 3 (#22), MSAA 2x a probar.
- **`Parte 4` — calibración de brillo:** los valores de referencia están en
  las secciones de 2026-09-09; en el celular, además, solo 4 de las 18 luces
  tocan las paredes (gotcha #18), así que cualquier ajuste fino conviene
  hacerlo con el Editor en nivel Mobile o directo en el A54.
- **`Parte 3` tiene el mismo `reflectionIntensity=1` riesgoso que tenía
  `Parte 4`** (auditado 2026-09-09, ver sección dedicada) — probablemente el
  mismo bug de reflejos del skybox, simplemente no reportado todavía. No se
  tocó porque es la escena más activamente trabajada y merece un pedido
  explícito antes de tocar su iluminación. `Parte 1` (`reflectionIntensity
  0.1`, ya atenuado) y `Menu Principal` (ambiente no apagado) están en bajo
  riesgo, no necesitan acción.
- **`Tutorial` es del compañero** (decisión #2): tiene llave, `LevelDoor`
  hacia Parte 1 y locución; lo único a coordinar con él es la transición
  (#19).
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
- Puzzles de las etapas 3/4/5: el código ya existe (2026-09-23, ver "Puzzles
  nuevos"); falta instanciarlos en escena (ver ítems de etapa más arriba).
  Etapa 2 (globos con `SequenceManager`) es del compañero.
- Pase de rendimiento para Android/A54: la auditoría estática ya está hecha
  (ver "QA de build Android / A54"); falta medir FPS en el celular real.
- Sacar `CameraDebugLogger` de la `Main Camera` de `Parte 1` **después** de
  la prueba en el A54 (hasta entonces sirve). `GazeDebugTarget` ya no está en
  ninguna escena.
- Posibles ajustes menores de `Parte 1`: sacar la luz `MZ4_Lamp_OFF_m_0`
  (intensity 50, fuera del cuarto jugable) si se confirma que sobra, y
  prender HDR en la cámara (#22).
- Confirmar si `GameManager` (loop de estado viejo) sigue en uso real en
  alguna escena, o si quedó reemplazado por el flujo nuevo de
  `MainMenuController` + escenas separadas — no queda claro cuál es la
  fuente de verdad actual.
