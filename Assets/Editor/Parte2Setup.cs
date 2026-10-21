using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Arma la parte MECANICA de "Parte 2 - Cumpleaños": el mapa, el rig de VR, el
/// inventario de cinta, la tele + VHS + monstruo + flechas con sus componentes,
/// toda la cadena de eventos cableada, el diorama escondido de la "grabacion",
/// la puerta de salida, y las escenas en Build Settings.
///
/// TODO se crea en posiciones PLACEHOLDER delante del spawn — despues las
/// acomodas vos: el mapa, dónde aparece el jugador, dónde va la tele, la cinta,
/// el monstruo, las flechas y la puerta.
///
/// Idempotente: borra lo que creo en corridas anteriores antes de rehacer.
/// Menu: Tools > Feria de Ciencias > Preparar Parte 2 (Cumpleaños).
/// </summary>
public static class Parte2Setup
{
    private const string ScenePath = "Assets/Scenes/Parte 2 - Cumpleaños.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player 1.prefab";
    private const string MapPath = "Assets/Modelos 3D/rec_room_-_backstage_of_reality_-_level_fun.glb";
    private const string TVPath = "Assets/Modelos 3D/Objetos 3D/TV.glb";
    private const string VhsPath = "Assets/Modelos 3D/Objetos 3D/VHSS.glb";
    private const string MonsterPath = "Assets/Modelos 3D/Objetos 3D/Moustruo1.glb";
    private const string ArrowPath = "Assets/Modelos 3D/Objetos 3D/flecha.glb";
    private const string NextScenePath = "Assets/Scenes/Parte 3 - Pasillo de hotel.unity";
    private const string NextSceneName = "Parte 3 - Pasillo de hotel";
    private const string InteractiveLayerName = "Interactive";
    private const string TeleportLayerName = "Teleport";
    private const int ArrowCount = 6;

    private static readonly string[] Generated =
    {
        "MAPA_Cumpleaños", "TapeInventory", "TV_Menu", "VHS_Menu", "Monstruo_Menu",
        "Flechas", "PuertaSalida", "DioramaGrabacion", "GameOver", "AmbienteParte2",
    };

    [MenuItem("Tools/Feria de Ciencias/Preparar Parte 2 (Cumpleaños)")]
    public static void Prepare()
    {
        if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("Falta la escena", ScenePath, "Ok"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        int layer = LayerMask.NameToLayer(InteractiveLayerName);
        if (layer < 0) { EditorUtility.DisplayDialog("Falta la layer", "Crea la layer '" + InteractiveLayerName + "'.", "Ok"); return; }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // ---- limpieza de corridas viejas ----
        int trashed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            bool mine = Generated.Contains(root.name);
            bool prefabRig = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == PlayerPrefabPath;
            bool hasRig = root.GetComponentInChildren<GazeController>(true) != null;
            if (mine || prefabRig || hasRig) { Object.DestroyImmediate(root); trashed++; }
        }
        // camaras sueltas y overlays huerfanos
        foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c != null && c.transform.parent == null && c.name.ToLowerInvariant().Contains("main"))
            {
                Object.DestroyImmediate(c.gameObject); trashed++;
            }
        }
        Debug.Log("[Parte2Setup] Limpieza: " + trashed + " objetos viejos borrados.");

        // ---- mapa ----
        GameObject map = Spawn(MapPath, "MAPA_Cumpleaños", Vector3.zero, Vector3.one);

        // ---- rig ----
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null) { Debug.LogError("[Parte2Setup] Falta " + PlayerPrefabPath); return; }
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        player.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
        Camera cam = player.GetComponentInChildren<Camera>(true);
        GazeController gaze = player.GetComponentInChildren<GazeController>(true);
        VRFadeController fade = player.GetComponentInChildren<VRFadeController>(true);
        TeleportManager tpm = player.GetComponentInChildren<TeleportManager>(true);
        ForceRotationOnly(player);
        EnsureLayerInGazeMask(gaze, layer);
        if (gaze != null && cam != null) SetRef(gaze, "_gazeCamera", cam);
        if (tpm != null && cam != null) SetRef(tpm, "_cameraTransform", cam.transform);
        if (tpm != null && fade != null) SetRef(tpm, "_fadeController", fade);
        KeepOneAudioListener(cam);

        Vector3 front = cam != null ? cam.transform.position + Vector3.forward * 3f : new Vector3(0f, 1.2f, 3f);

        // ---- inventario de cinta ----
        TapeInventory tape = new GameObject("TapeInventory").AddComponent<TapeInventory>();

        // ---- VHS (llave de la tele) ----
        GameObject vhs = Spawn(VhsPath, "VHS_Menu", front + new Vector3(-0.8f, -0.6f, 0f), Vector3.one * 0.5f);
        Collectable vhsCol = GetOrAdd<Collectable>(vhs);
        EnsureCollider(vhs);
        vhs.layer = layer;
        ClearAndWire(vhsCol, "_onCollected", tape, nameof(TapeInventory.CollectTape));

        // ---- TV + grabacion ----
        GameObject tv = Spawn(TVPath, "TV_Menu", front + new Vector3(1.2f, -0.2f, 0.5f), Vector3.one);
        tv.layer = layer;
        EnsureCollider(tv);
        VCRSlot vcr = GetOrAdd<VCRSlot>(tv);
        SetRef(vcr, "_tapeInventory", tape);
        SecurityFootage footage = GetOrAdd<SecurityFootage>(tv);

        // ---- diorama escondido para la grabacion (Y = -500) ----
        GameObject diorama = new GameObject("DioramaGrabacion");
        diorama.transform.position = new Vector3(0f, -500f, 0f);

        GameObject dFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        dFloor.name = "Piso";
        dFloor.transform.SetParent(diorama.transform, false);
        dFloor.transform.localScale = Vector3.one * 0.6f;
        Object.DestroyImmediate(dFloor.GetComponent<Collider>());

        GameObject dLightGo = new GameObject("Luz");
        dLightGo.transform.SetParent(diorama.transform, false);
        dLightGo.transform.localPosition = new Vector3(0f, 3f, 0f);
        Light dLight = dLightGo.AddComponent<Light>();
        dLight.type = LightType.Point; dLight.range = 12f; dLight.intensity = 3f;
        dLight.color = new Color(0.8f, 0.85f, 0.9f);

        GameObject dCamGo = new GameObject("CamaraGrabacion");
        dCamGo.transform.SetParent(diorama.transform, false);
        dCamGo.transform.localPosition = new Vector3(0f, 1.7f, -3.5f);
        dCamGo.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
        Camera dCam = dCamGo.AddComponent<Camera>();
        dCam.tag = "Untagged";
        dCam.enabled = false;
        dCam.clearFlags = CameraClearFlags.SolidColor;
        dCam.backgroundColor = Color.black;
        dCam.fieldOfView = 55f;
        dCam.cullingMask = ~0;

        GameObject dMonster = Spawn(MonsterPath, "MonstruoGrabacion", Vector3.zero, Vector3.one);
        dMonster.transform.SetParent(diorama.transform, false);
        dMonster.transform.localPosition = new Vector3(0f, 0f, 1.5f);

        SetRef(footage, "_footageCamera", dCam);
        SetRef(footage, "_monster", dMonster.transform);
        SetRef(footage, "_flickerLight", dLight);
        // _tvScreen queda para asignar a mano (hay que saber cual submalla de TV es el vidrio)

        // ---- monstruo de la sala (el que persigue) ----
        GameObject monster = Spawn(MonsterPath, "Monstruo_Menu", front + new Vector3(0f, -1.0f, 6f), Vector3.one);
        MonsterChaser chaser = GetOrAdd<MonsterChaser>(monster);

        GameOver over = new GameObject("GameOver").AddComponent<GameOver>();

        // ---- flechas del piso ----
        GameObject arrowsRoot = new GameObject("Flechas");
        for (int i = 0; i < ArrowCount; i++)
        {
            GameObject a = Spawn(ArrowPath, "Flecha_" + i, front + new Vector3(0f, -1.4f, 2f + i * 2f), Vector3.one);
            a.transform.SetParent(arrowsRoot.transform, true);
            EmissivePulse ep = GetOrAdd<EmissivePulse>(a);
            SetFloat(ep, "_phaseOffset", i * 0.35f);
        }

        // ---- puerta de salida ----
        GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "PuertaSalida";
        door.transform.position = front + new Vector3(0f, 0f, 14f);
        door.transform.localScale = new Vector3(1.2f, 2.2f, 0.15f);
        door.layer = layer;
        LevelDoor lvl = GetOrAdd<LevelDoor>(door);
        SetStr(lvl, "_nextSceneName", NextSceneName);
        SetStr(lvl, "_bigText", "PARTE 3");
        SetStr(lvl, "_subText", "PASILLO DE HOTEL");
        SetBool(lvl, "_requiresKey", false);

        // ---- cadena de eventos ----
        ClearAndWire(vcr, "_onTapeInserted", footage, nameof(SecurityFootage.Play));
        ClearAndWire(footage, "_onMovieEnd", chaser, nameof(MonsterChaser.StartChase));
        ClearAndWire(chaser, "_onCaught", over, nameof(GameOver.RestartLevel));
        ClearAndWire(lvl, "_onOpen", chaser, nameof(MonsterChaser.StopChase));

        // ---- Build Settings ----
        EnableInBuild(ScenePath);
        EnableInBuild(NextScenePath);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("<b>[Parte2Setup]</b> LISTO. Todo creado en posiciones placeholder DELANTE del spawn.\n" +
                  "A MANO te falta:\n" +
                  " - acomodar el mapa y elegir dónde aparece el jugador\n" +
                  " - mover TV_Menu / VHS_Menu / Monstruo_Menu / Flechas / PuertaSalida a su lugar\n" +
                  " - en TV_Menu, componente Security Footage: asignar 'Tv Screen' = la sub-malla vidrio de la tele\n" +
                  " - poner teletransportes TeleportPoint por el salón\n" +
                  " - pasar los sonidos cuando los tengas");
    }

    // =====================================================================
    // Segunda pasada: NO recrea nada. Toma los objetos que ya pusiste vos en
    // la escena (por nombre) y les cablea todo: TeleportPoint en los TP y en
    // las flechas, LevelDoor en 'Puerta', y la cadena
    // pelicula -> ChaseReveal -> persecucion -> game over.
    // =====================================================================
    [MenuItem("Tools/Feria de Ciencias/Parte 2 · Configurar TP + flechas + puerta + cadena (no recrea)")]
    public static void ConfigurarSinRecrear()
    {
        int gazeLayer = LayerMask.NameToLayer(InteractiveLayerName);
        int tpLayer = LayerMask.NameToLayer(TeleportLayerName);
        if (gazeLayer < 0 || tpLayer < 0)
        {
            EditorUtility.DisplayDialog("Faltan layers",
                "Necesito las layers '" + InteractiveLayerName + "' y '" + TeleportLayerName + "'.", "Ok");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.path.Replace('\\', '/').EndsWith(ScenePath) &&
            !EditorUtility.DisplayDialog("Escena", "La escena activa no parece ser Parte 2. ¿Seguir igual?", "Seguir", "Cancelar"))
        {
            return;
        }

        TeleportManager tpm = Object.FindFirstObjectByType<TeleportManager>();
        if (tpm == null) Debug.LogWarning("[Parte2Cfg] No encontre TeleportManager — los TeleportPoint van a quedar sin manager.");

        GazeController gaze = Object.FindFirstObjectByType<GazeController>();
        Camera cam = Camera.main;
        if (cam == null && gaze != null) cam = gaze.GetComponentInChildren<Camera>(true);
        if (gaze != null)
        {
            EnsureLayerInGazeMask(gaze, gazeLayer);
            EnsureLayerInGazeMask(gaze, tpLayer);
        }

        int tpDone = 0, arrowDone = 0;

        // ---- TP (cubos primitivos dentro del empty "TP") ----
        GameObject tpRoot = FindAny("TP");
        if (tpRoot == null) Debug.LogWarning("[Parte2Cfg] No encontre el objeto 'TP'.");
        else
        {
            foreach (Transform child in tpRoot.transform)
            {
                GameObject go = child.gameObject;
                RemoveOtherGazeInteractables(go, typeof(TeleportPoint));
                TeleportPoint tp = GetOrAdd<TeleportPoint>(go);
                if (go.GetComponent<Collider>() == null) go.AddComponent<BoxCollider>();
                go.layer = tpLayer;
                if (tpm != null) SetRef(tp, "_teleportManager", tpm);
                tpDone++;
            }
        }

        // ---- Flechas (.glb) tambien teletransportables ----
        GameObject arrowsRoot = FindAny("Flechas");
        if (arrowsRoot == null) Debug.LogWarning("[Parte2Cfg] No encontre el objeto 'Flechas'.");
        else
        {
            foreach (Transform child in arrowsRoot.transform)
            {
                GameObject go = child.gameObject;
                RemoveOtherGazeInteractables(go, typeof(TeleportPoint));
                TeleportPoint tp = GetOrAdd<TeleportPoint>(go);
                EnsureCollider(go);
                go.layer = tpLayer;
                if (tpm != null) SetRef(tp, "_teleportManager", tpm);
                arrowDone++;
            }
        }

        // ---- Puerta de salida (.glb) ----
        GameObject puerta = FindAny("Puerta");
        LevelDoor lvl = null;
        if (puerta == null) Debug.LogWarning("[Parte2Cfg] No encontre el objeto 'Puerta'.");
        else
        {
            RemoveOtherGazeInteractables(puerta, typeof(LevelDoor));
            lvl = GetOrAdd<LevelDoor>(puerta);
            EnsureCollider(puerta);
            puerta.layer = gazeLayer;
            SetStr(lvl, "_nextSceneName", NextSceneName);
            SetStr(lvl, "_bigText", "PARTE 3");
            SetStr(lvl, "_subText", "PASILLO DE HOTEL");
            SetBool(lvl, "_requiresKey", false);
        }

        // ---- cinta VHS: que sea recolectable (VHSS y/o VHS_Menu) ----
        TapeInventory tape = Object.FindFirstObjectByType<TapeInventory>();
        if (tape == null) Debug.LogWarning("[Parte2Cfg] No encontre TapeInventory — la cinta no va a poder recogerse.");
        else
        {
            int tapesWired = 0;
            foreach (string tapeName in new[] { "VHSS", "VHS_Menu" })
            {
                GameObject vhs = FindAny(tapeName);
                if (vhs == null) continue;
                RemoveOtherGazeInteractables(vhs, typeof(Collectable));
                Collectable col = GetOrAdd<Collectable>(vhs);
                EnsureCollider(vhs);
                vhs.layer = gazeLayer;
                ClearAndWire(col, "_onCollected", tape, nameof(TapeInventory.CollectTape));
                tapesWired++;
            }
            if (tapesWired == 0) Debug.LogWarning("[Parte2Cfg] No encontre ni 'VHSS' ni 'VHS_Menu' en la escena.");
            else Debug.Log("[Parte2Cfg] Cinta(s) recolectable(s) cableada(s): " + tapesWired + " (si dejaste las dos, borrá una).");
        }

        // ---- monstruo perseguidor + cadena de la pelicula ----
        MonsterChaser chaser = FindComponentOn<MonsterChaser>("Monstruo_Menu");
        SecurityFootage footage = FindComponentOn<SecurityFootage>("TV_Menu");
        GameOver over = Object.FindFirstObjectByType<GameOver>();
        VRFadeController fade = Object.FindFirstObjectByType<VRFadeController>();

        if (chaser != null)
        {
            if (cam != null) SetRef(chaser, "_target", cam.transform);
            SetBool(chaser, "_keepYFixed", true);
            SetFloat(chaser, "_fixedY", chaser.transform.position.y);
        }
        else Debug.LogWarning("[Parte2Cfg] No encontre 'Monstruo_Menu' con MonsterChaser.");

        if (lvl != null && chaser != null)
            ClearAndWire(lvl, "_onOpen", chaser, nameof(MonsterChaser.StopChase));

        // ChaseReveal en un GO de logica dedicado (se crea solo si falta).
        GameObject cineGo = FindAny("CinematicaPersecucion");
        if (cineGo == null) cineGo = new GameObject("CinematicaPersecucion");
        ChaseReveal reveal = GetOrAdd<ChaseReveal>(cineGo);
        if (chaser != null) SetRef(reveal, "_chaser", chaser);
        if (fade != null) SetRef(reveal, "_fade", fade);
        if (cam != null)
        {
            SetRef(reveal, "_cameraToTurn", cam.transform);
            SetRef(reveal, "_playerCamera", cam.transform);
        }

        // Punto de aparicion del monstruo: un empty que TENES QUE MOVER vos a
        // la habitacion de atras, sobre el piso, apuntando hacia el jugador.
        GameObject revealPt = FindAny("MonsterRevealPoint");
        if (revealPt == null)
        {
            revealPt = new GameObject("MonsterRevealPoint");
            Vector3 basePos = gaze != null ? gaze.transform.root.position : Vector3.zero;
            Vector3 back = gaze != null ? -gaze.transform.root.forward : Vector3.back;
            revealPt.transform.SetPositionAndRotation(basePos + back * 4f, Quaternion.LookRotation(-back, Vector3.up));
            Debug.LogWarning("[Parte2Cfg] Cree 'MonsterRevealPoint' detras del spawn — MOVELO a la habitacion de atras, sobre el piso, con la flecha azul (Z) apuntando hacia donde viene el jugador.");
        }
        SetRef(reveal, "_monsterRevealPoint", revealPt.transform);

        if (footage != null)
            ClearAndWire(footage, "_onMovieEnd", reveal, nameof(ChaseReveal.Play));
        else
            Debug.LogWarning("[Parte2Cfg] No encontre 'TV_Menu' con SecurityFootage — no pude recablear _onMovieEnd.");

        if (chaser != null && over != null)
            ClearAndWire(chaser, "_onCaught", over, nameof(GameOver.RestartLevel));

        // ---- intentar asignar el vidrio de la TV ----
        if (footage != null)
        {
            SerializedObject fso = new SerializedObject(footage);
            SerializedProperty screenProp = fso.FindProperty("_tvScreen");
            if (screenProp != null && screenProp.objectReferenceValue == null)
            {
                Renderer screen = GuessScreenRenderer(footage.gameObject);
                if (screen != null)
                {
                    screenProp.objectReferenceValue = screen;
                    fso.ApplyModifiedPropertiesWithoutUndo();
                    Debug.Log("[Parte2Cfg] SecurityFootage._tvScreen = '" + screen.name + "' (adivinado por nombre; si no es el vidrio, cambialo a mano).");
                }
                else
                {
                    Debug.LogWarning("[Parte2Cfg] No pude adivinar el vidrio de la TV. Asigna 'Tv Screen' a mano en el componente Security Footage de TV_Menu.");
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("<b>[Parte2Cfg]</b> Listo (no se recreo nada).\n" +
                  " - TeleportPoint puestos en TP: " + tpDone + " | en Flechas: " + arrowDone + "\n" +
                  " - Puerta con LevelDoor -> Parte 3: " + (lvl != null) + "\n" +
                  " - Cadena recableada: VCRSlot -> SecurityFootage.Play -> ChaseReveal.Play -> MonsterChaser.StartChase; _onCaught -> GameOver; Puerta._onOpen -> StopChase\n" +
                  "FALTA A MANO:\n" +
                  " - MOVER 'MonsterRevealPoint' a la habitacion de atras, SOBRE EL PISO, flecha azul (Z) hacia el jugador\n" +
                  " - guardar la escena (Ctrl+S)\n" +
                  " - si avisó arriba, asignar 'Tv Screen' en Security Footage de TV_Menu\n" +
                  " - pasar sonidos: en ChaseReveal el golpe/rugido (AudioSource DETRAS del jugador) + insert/scream/static/ambience\n" +
                  " - nota: las flechas ahora son TeleportPoint, asi que TeleportManager las oculta cuando estan lejos (igual que los TP). Si las queres SIEMPRE visibles como guia, decime y lo cambio.");
    }

    // =====================================================================
    // Player 1.prefab NO trae Main Camera (es un rig incompleto: solo Gaze,
    // VRFadeController y TeleportManager — esperaba que la escena pusiera la
    // camara, como pasa en Parte 1). Este comando le injerta una Main Camera
    // completa (con ReticleCanvas, CardboardReticlePointer y linterna) sacada
    // de Player.prefab, sin tocar la posicion ni el cableado que ya hiciste.
    // =====================================================================
    [MenuItem("Tools/Feria de Ciencias/Parte 2 · Agregar Main Camera al rig (Player 1 no tiene)")]
    public static void AgregarCamaraAlRig()
    {
        int gazeLayer = LayerMask.NameToLayer(InteractiveLayerName);
        int tpLayer = LayerMask.NameToLayer(TeleportLayerName);
        Scene scene = SceneManager.GetActiveScene();

        GazeController gaze = Object.FindFirstObjectByType<GazeController>();
        if (gaze == null)
        {
            EditorUtility.DisplayDialog("Sin rig", "No encontre un GazeController en la escena.", "Ok");
            return;
        }
        GameObject rigRoot = gaze.transform.root.gameObject;

        if (rigRoot.GetComponentInChildren<Camera>(true) != null)
        {
            EditorUtility.DisplayDialog("Ya hay camara",
                "El rig '" + rigRoot.name + "' ya tiene una Camera. No hago nada.", "Ok");
            return;
        }

        const string playerFull = "Assets/Prefabs/Player.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerFull);
        if (prefab == null) { EditorUtility.DisplayDialog("Falta prefab", playerFull, "Ok"); return; }

        // instancia temporal de Player.prefab -> le saco solo la Main Camera
        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Camera cam = temp.GetComponentInChildren<Camera>(true);
        if (cam == null)
        {
            Object.DestroyImmediate(temp);
            EditorUtility.DisplayDialog("Prefab raro", "Player.prefab no tiene Camera.", "Ok");
            return;
        }

        GameObject camGo = cam.gameObject;
        camGo.transform.SetParent(rigRoot.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f); // altura de ojos parado
        camGo.transform.localRotation = Quaternion.identity;
        camGo.transform.localScale = Vector3.one;
        Object.DestroyImmediate(temp); // se lleva Gaze/VRFade/TeleportManager sobrantes

        if (cam.tag != "MainCamera") cam.tag = "MainCamera";

        if (rigRoot.transform.localScale != Vector3.one)
        {
            Debug.LogWarning("[Parte2Cam] '" + rigRoot.name + "' tenia escala " + rigRoot.transform.localScale +
                             " -> la paso a (1,1,1) (escala no-uniforme rompe la camara y los colliders hijos). Quiza tengas que reubicar el rig en Y.");
            rigRoot.transform.localScale = Vector3.one;
        }

        ForceRotationOnly(rigRoot);
        KeepOneAudioListener(cam);

        // imagenes del ReticleCanvas que vino con la camara
        Image fadeImg = null, dotImg = null, progImg = null;
        foreach (Image img in camGo.GetComponentsInChildren<Image>(true))
        {
            if (img.name == "FadeImage") fadeImg = img;
            else if (img.name == "DotImage") dotImg = img;
            else if (img.name == "ProgressImage") progImg = img;
        }

        EnsureLayerInGazeMask(gaze, gazeLayer);
        EnsureLayerInGazeMask(gaze, tpLayer);
        SetRef(gaze, "_gazeCamera", cam);

        GazeReticle reticle = rigRoot.GetComponentInChildren<GazeReticle>(true);
        if (reticle != null)
        {
            if (progImg != null) SetRef(reticle, "_progressImage", progImg);
            if (dotImg != null) SetRef(reticle, "_dotImage", dotImg);
        }

        VRFadeController fade = rigRoot.GetComponentInChildren<VRFadeController>(true);
        if (fade != null && fadeImg != null) SetRef(fade, "_fadeImage", fadeImg);

        TeleportManager tpm = rigRoot.GetComponentInChildren<TeleportManager>(true);
        if (tpm != null)
        {
            SetRef(tpm, "_cameraTransform", cam.transform);
            if (fade != null) SetRef(tpm, "_fadeController", fade);
        }

        foreach (MonsterChaser mc in Object.FindObjectsByType<MonsterChaser>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            SetRef(mc, "_target", cam.transform);
        foreach (ChaseReveal cr in Object.FindObjectsByType<ChaseReveal>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (fade != null) SetRef(cr, "_fade", fade);
            SetRef(cr, "_cameraToTurn", cam.transform);
            SetRef(cr, "_playerCamera", cam.transform);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("<b>[Parte2Cam]</b> Main Camera agregada como hija de '" + rigRoot.name + "' en local (0, 1.6, 0).\n" +
                  " - taggeada MainCamera, TrackedPoseDriver = Rotation Only\n" +
                  " - cableado: GazeController._gazeCamera, GazeReticle (dot/progress), VRFadeController._fadeImage, TeleportManager._cameraTransform/_fadeController\n" +
                  " - MonsterChaser._target y ChaseReveal._cameraToTurn -> la camara\n" +
                  "REVISAR: si spawneás hundido en el piso o muy alto, ajustá la Main Camera local Y (1.6 = ojos parado). Después Ctrl+S.");
    }

    // ------------------------------------------------------------- helpers

    private static GameObject FindAny(string name)
    {
        GameObject child = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != name) continue;
            if (t.parent == null) return t.gameObject;
            if (child == null) child = t.gameObject;
        }
        return child;
    }

    private static T FindComponentOn<T>(string goName) where T : Component
    {
        GameObject go = FindAny(goName);
        return go != null ? go.GetComponentInChildren<T>(true) : null;
    }

    private static void RemoveOtherGazeInteractables(GameObject go, System.Type keep)
    {
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
        {
            if (mb == null || mb.GetType() == keep) continue;
            if (mb is IGazeInteractable)
            {
                Debug.Log("[Parte2Cfg] Saco " + mb.GetType().Name + " de '" + go.name + "' (dejo " + keep.Name + ").");
                Object.DestroyImmediate(mb);
            }
        }
    }

    private static Renderer GuessScreenRenderer(GameObject tv)
    {
        string[] keys = { "screen", "pantalla", "glass", "vidrio", "display", "lcd", "crt", "tube", "monitor" };
        foreach (Renderer r in tv.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name.ToLowerInvariant();
            foreach (string k in keys)
                if (n.Contains(k)) return r;
        }
        return null;
    }

    private static GameObject Spawn(string assetPath, string name, Vector3 pos, Vector3 scale)
    {
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
        {
            Debug.LogWarning("[Parte2Setup] No se encontro " + assetPath + " — se crea un cubo placeholder para '" + name + "'.");
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name; cube.transform.position = pos; cube.transform.localScale = scale;
            return cube;
        }
        GameObject go = PrefabUtility.InstantiatePrefab(asset) as GameObject;
        if (go == null) go = (GameObject)Object.Instantiate(asset);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = scale;
        return go;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static void EnsureCollider(GameObject go)
    {
        if (go.GetComponent<Collider>() != null) return;
        BoxCollider bc = go.AddComponent<BoxCollider>();
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length > 0)
        {
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            bc.center = go.transform.InverseTransformPoint(b.center);
            Vector3 s = go.transform.InverseTransformVector(b.size);
            bc.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
        }
    }

    private static void ForceRotationOnly(GameObject player)
    {
        foreach (Component comp in player.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp.GetType().Name != "TrackedPoseDriver") continue;
            SerializedObject so = new SerializedObject(comp);
            SerializedProperty p = so.FindProperty("m_TrackingType") ?? so.FindProperty("trackingType");
            if (p != null) { p.enumValueIndex = 1; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }

    private static void EnsureLayerInGazeMask(GazeController gaze, int layer)
    {
        if (gaze == null) return;
        SerializedObject so = new SerializedObject(gaze);
        SerializedProperty mask = so.FindProperty("_interactiveLayerMask");
        if (mask == null) return;
        int bit = 1 << layer;
        if ((mask.intValue & bit) == 0) { mask.intValue |= bit; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void KeepOneAudioListener(Camera cam)
    {
        AudioListener keep = cam != null ? cam.GetComponent<AudioListener>() : null;
        if (keep == null && cam != null) keep = cam.gameObject.AddComponent<AudioListener>();
        foreach (AudioListener al in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (al != keep) Object.DestroyImmediate(al);
    }

    private static void EnableInBuild(string path)
    {
        if (!File.Exists(path)) { Debug.LogWarning("[Parte2Setup] No existe " + path); return; }
        var list = EditorBuildSettings.scenes.ToList();
        int i = list.FindIndex(s => s.path == path);
        if (i >= 0) list[i] = new EditorBuildSettingsScene(path, true);
        else list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    private static void ClearAndWire(Component c, string field, Object target, string method)
    {
        FieldInfo fi = c.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi == null) { Debug.LogWarning("[Parte2Setup] " + c.GetType().Name + " no tiene " + field); return; }
        UnityEvent evt = fi.GetValue(c) as UnityEvent;
        if (evt == null) { evt = new UnityEvent(); fi.SetValue(c, evt); }
        for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(evt, i);
        UnityEventTools.AddVoidPersistentListener(evt, (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, method));
        EditorUtility.SetDirty(c);
    }

    private static void SetRef(Component c, string prop, Object value)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[Parte2Setup] " + c.GetType().Name + " no tiene " + prop); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStr(Component c, string prop, string value)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) { p.stringValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetBool(Component c, string prop, bool value)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) { p.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }

    private static void SetFloat(Component c, string prop, float value)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) { p.floatValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
