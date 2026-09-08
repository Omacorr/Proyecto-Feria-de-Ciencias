using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Genera de cero la escena "Menu Principal" ya armada y cableada:
///   - rig de VR (instancia del prefab Player: camara + gaze + reticulo + fade),
///   - objeto GameSettings,
///   - MenuManager con MainMenuController,
///   - tres paneles (principal / configuracion / creditos) con botones mirables
///     (InteractiveObject + Collider en la layer Interactive),
///   - todos los UnityEvent y referencias del Inspector cableados,
///   - la escena agregada a Build Settings en el indice 0 (y Tutorial incluida).
///
/// Uso: menu  Tools > Feria de Ciencias > Generar escena 'Menu Principal'.
/// Se puede volver a correr cuando quieras: sobrescribe la escena entera.
/// Despues de correrlo: abrir Assets/Scenes/Menu Principal.unity y dar Play.
///
/// Este script vive en Assets/Editor/, asi que NO entra en la build; es solo una
/// herramienta de autor.
/// </summary>
public static class MenuPrincipalGenerator
{
    private const string ScenePath = "Assets/Scenes/Menu Principal.unity";
    private const string TutorialScenePath = "Assets/Scenes/Tutorial.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string MatDir = "Assets/Materials/Menu";
    private const string InteractiveLayerName = "Interactive";

    // --------- Ambiente / fondo ---------
    // Nombre EXACTO de los archivos que agregues al proyecto. Si el archivo no
    // existe todavia, esa parte se saltea sin romper: se puede correr el
    // generador antes de tener los assets y volver a correrlo despues.
    private const string RoomModelPath = "Assets/Modelos 3D/horror_room.glb";
    private const string AmbientAudioPath = "Assets/Audios/AmbienteMenuPrincipal.flac";

    // Altura de ojos (m) por ENCIMA del piso de la habitacion. El generador
    // detecta el piso con los bounds del modelo (su punto mas bajo) y sube el
    // rig para que la camara quede a EyeHeight de ahi.
    private const float EyeHeight = 1.6f;

    // Si el piso automatico queda mal, poner aca la Y del piso a mano (en metros)
    // y dejar de usar la deteccion. NaN = automatico.
    private static readonly float FloorYOverride = float.NaN;

    // Distancia del menu a la camara (m) y de la puerta a la camara (m).
    // El menu va delante de la puerta; al Empezar la puerta se abre y la camara
    // avanza cruzandola antes de cargar el Tutorial.
    private const float MenuDistance = 2.5f;
    private const float DoorDistance = 3.0f;

    // "Casi a oscuras": ambiente y niebla casi negros. Una luz parpadeante
    // mantiene iluminada la zona del menu (atmosferica, sin llegar a tapar).
    // El menu igual usa materiales Unlit, asi que se lee sin depender de la luz.
    private static readonly Color AmbientColor = new Color(0.018f, 0.018f, 0.026f, 1f);
    private static readonly Color FogColor = new Color(0.010f, 0.010f, 0.016f, 1f);
    private const float FogDensity = 0.028f;
    private static readonly Color FillLightColor = new Color(0.25f, 0.30f, 0.45f, 1f); // direccional MUY tenue, fria
    private const float FillLightIntensity = 0.06f;
    private static readonly Color FlickerLightColor = new Color(1f, 0.80f, 0.58f, 1f); // point calida, tipo lampara
    private const float FlickerLightIntensity = 1.5f;
    private const float FlickerLightRange = 8.5f;

    // Colores del menu, pensados para que resalte sobre negro. El alpha < 1 deja
    // el menu "un toque" transparente (se ve algo de la sala por detras). Los
    // textos van aparte y quedan solidos.
    // Menu MUY transparente: sin fondo solido, apenas un halo difuso detras del
    // texto. Los textos van aparte y quedan solidos.
    private static readonly Color ColBotonIdle = new Color(0.16f, 0.18f, 0.26f, 0.55f);
    private static readonly Color ColBotonMirado = new Color(0.22f, 0.55f, 1.00f, 0.88f);
    private static readonly Color ColPanelHalo = new Color(0.04f, 0.05f, 0.08f, 0.34f); // tinte del halo (se multiplica por la textura difusa)

    // Puerta (geometria propia, no depende de la del .glb).
    private static readonly Color ColPuertaHoja = new Color(0.16f, 0.11f, 0.07f, 1f);
    private static readonly Color ColPuertaMarco = new Color(0.08f, 0.06f, 0.05f, 1f);

    [MenuItem("Tools/Feria de Ciencias/Generar escena 'Menu Principal'")]
    public static void Generate()
    {
        if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog(
                "Regenerar 'Menu Principal'",
                "Ya existe la escena 'Menu Principal.unity'. Se va a sobrescribir por completo.\n\n¿Seguir?",
                "Sobrescribir", "Cancelar"))
        {
            return;
        }

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            EditorUtility.DisplayDialog("Falta el prefab",
                "No se encontro " + PlayerPrefabPath + ".\nNo se puede armar la escena sin el rig de VR.", "Ok");
            return;
        }

        int interactiveLayer = LayerMask.NameToLayer(InteractiveLayerName);
        if (interactiveLayer < 0)
        {
            EditorUtility.DisplayDialog("Falta la layer",
                "No existe la layer '" + InteractiveLayerName + "'. Creala en Project Settings > Tags and Layers y volve a correr.", "Ok");
            return;
        }

        // --- materiales compartidos (assets, para poder reusarlos) ---
        Texture2D haze = CreateOrLoadHazeTexture();
        Material matIdle = CreateOrLoadMaterial("MenuBoton_Idle", ColBotonIdle);
        Material matHot = CreateOrLoadMaterial("MenuBoton_Mirado", ColBotonMirado);
        Material matPanel = CreateOrLoadMaterial("MenuPanel_Halo", ColPanelHalo);
        if (matPanel.HasProperty("_BaseMap")) matPanel.SetTexture("_BaseMap", haze);
        if (matPanel.HasProperty("_MainTex")) matPanel.SetTexture("_MainTex", haze);

        // --- escena vacia ---
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // --- rig de VR ---
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Camera cam = player.GetComponentInChildren<Camera>(true);
        VRFadeController fade = player.GetComponentInChildren<VRFadeController>(true);

        // Cardboard es 3DOF: si el TrackedPoseDriver esta en "Rotation And
        // Position" manda la camara al origen del mundo cada frame (gotcha
        // conocido del proyecto). Lo forzamos a Rotation Only.
        ForceCameraRotationOnly(player);

        // --- habitacion (primero, para saber donde esta el piso) ---
        GameObject room = SpawnRoom(scene);

        // Piso: el punto mas bajo de los bounds del modelo. Si no hay modelo (o
        // hay override), se usa 0 / el override.
        float floorY = 0f;
        if (room != null)
        {
            Renderer[] rends = room.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                floorY = b.min.y;
            }
        }
        if (!float.IsNaN(FloorYOverride))
        {
            floorY = FloorYOverride;
        }

        // Reubica el rig para que la camara quede a EyeHeight por encima del
        // piso, y centrada en X/Z sobre el origen. El pivote del prefab tiene la
        // camara en un offset raro; esto lo normaliza.
        if (cam != null)
        {
            Vector3 camWorld = cam.transform.position;
            player.transform.position += new Vector3(
                -camWorld.x,
                (floorY + EyeHeight) - camWorld.y,
                -camWorld.z);
        }
        Debug.Log("[MenuPrincipalGenerator] Piso detectado en Y=" + floorY.ToString("0.00") +
                  " -> camara a Y=" + (floorY + EyeHeight).ToString("0.00") +
                  ". Si aparecés pegado al piso o flotando, ajusta EyeHeight o FloorYOverride.");

        EnsureInteractiveLayerInMask(player, interactiveLayer);

        // --- GameSettings (se auto-crearia igual, pero lo dejamos visible) ---
        new GameObject("GameSettings").AddComponent<GameSettings>();

        // --- MenuManager ---
        GameObject managerGO = new GameObject("MenuManager");
        MainMenuController menu = managerGO.AddComponent<MainMenuController>();

        // --- raiz del menu, plantada frente a la camara ---
        Vector3 camPos = cam != null ? cam.transform.position : new Vector3(0f, EyeHeight, 0f);
        Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;
        if (camFwd.sqrMagnitude < 0.001f) camFwd = Vector3.forward;
        camFwd = new Vector3(camFwd.x, 0f, camFwd.z).normalized; // horizontal: el menu no queda inclinado
        if (camFwd.sqrMagnitude < 0.001f) camFwd = Vector3.forward;

        GameObject menuRoot = new GameObject("MenuRoot");
        menuRoot.transform.position = camPos + camFwd * MenuDistance;
        menuRoot.transform.rotation = Quaternion.LookRotation(menuRoot.transform.position - camPos, Vector3.up);

        BuildAtmosphere(scene, menuRoot.transform, cam, camPos);

        // Puerta detras del menu; se abre y se cruza al Empezar.
        Transform doorPivot = BuildDoor(camPos, camFwd, floorY);

        GameObject panelMain = MakePanel("PanelPrincipal", menuRoot.transform, matPanel, 5.0f, 4.2f);
        GameObject panelConfig = MakePanel("PanelConfig", menuRoot.transform, matPanel, 5.2f, 4.8f);
        GameObject panelCreditos = MakePanel("PanelCreditos", menuRoot.transform, matPanel, 5.0f, 3.8f);

        // ---------- Panel principal ----------
        MakeText("Titulo", panelMain.transform, "MENÚ PRINCIPAL", new Vector3(0f, 1.15f, -0.02f), 0.5f, FontStyles.Bold);
        GameObject bStart = MakeButton("BotonEmpezar", panelMain.transform, new Vector3(0f, 0.25f, 0f), "EMPEZAR", 3.4f, matIdle, matHot, interactiveLayer);
        GameObject bConfig = MakeButton("BotonConfiguracion", panelMain.transform, new Vector3(0f, -0.45f, 0f), "CONFIGURACIÓN", 3.4f, matIdle, matHot, interactiveLayer);
        GameObject bCred = MakeButton("BotonCreditos", panelMain.transform, new Vector3(0f, -1.15f, 0f), "CRÉDITOS", 3.4f, matIdle, matHot, interactiveLayer);

        WireButton(bStart, menu, nameof(MainMenuController.StartGame));
        WireButton(bConfig, menu, nameof(MainMenuController.OpenConfig));
        WireButton(bCred, menu, nameof(MainMenuController.OpenCredits));

        // ---------- Panel configuracion ----------
        MakeText("Titulo", panelConfig.transform, "CONFIGURACIÓN", new Vector3(0f, 1.5f, -0.02f), 0.42f, FontStyles.Bold);

        GameObject lblGaze = MakeText("LabelGaze", panelConfig.transform, "Velocidad del gaze: Medio", new Vector3(0f, 0.75f, -0.02f), 0.26f, FontStyles.Normal);
        GameObject gLow = MakeButton("GazeBajo", panelConfig.transform, new Vector3(-1.25f, 0.2f, 0f), "Bajo", 1.05f, matIdle, matHot, interactiveLayer);
        GameObject gMid = MakeButton("GazeMedio", panelConfig.transform, new Vector3(0f, 0.2f, 0f), "Medio", 1.05f, matIdle, matHot, interactiveLayer);
        GameObject gHigh = MakeButton("GazeAlto", panelConfig.transform, new Vector3(1.25f, 0.2f, 0f), "Alto", 1.05f, matIdle, matHot, interactiveLayer);

        GameObject lblRet = MakeText("LabelReticulo", panelConfig.transform, "Tamaño del retículo: Medio", new Vector3(0f, -0.55f, -0.02f), 0.26f, FontStyles.Normal);
        GameObject rLow = MakeButton("ReticuloBajo", panelConfig.transform, new Vector3(-1.25f, -1.1f, 0f), "Bajo", 1.05f, matIdle, matHot, interactiveLayer);
        GameObject rMid = MakeButton("ReticuloMedio", panelConfig.transform, new Vector3(0f, -1.1f, 0f), "Medio", 1.05f, matIdle, matHot, interactiveLayer);
        GameObject rHigh = MakeButton("ReticuloAlto", panelConfig.transform, new Vector3(1.25f, -1.1f, 0f), "Alto", 1.05f, matIdle, matHot, interactiveLayer);

        GameObject bBackCfg = MakeButton("BotonVolver", panelConfig.transform, new Vector3(0f, -1.85f, 0f), "VOLVER", 2.2f, matIdle, matHot, interactiveLayer);

        ConfigMenu cfg = panelConfig.AddComponent<ConfigMenu>();
        SetRef(cfg, "_gazeSpeedLabel", lblGaze.GetComponent<TextMeshPro>());
        SetRef(cfg, "_reticleSizeLabel", lblRet.GetComponent<TextMeshPro>());

        WireButton(gLow, cfg, nameof(ConfigMenu.SetGazeSpeedBajo));
        WireButton(gMid, cfg, nameof(ConfigMenu.SetGazeSpeedMedio));
        WireButton(gHigh, cfg, nameof(ConfigMenu.SetGazeSpeedAlto));
        WireButton(rLow, cfg, nameof(ConfigMenu.SetReticleSizeBajo));
        WireButton(rMid, cfg, nameof(ConfigMenu.SetReticleSizeMedio));
        WireButton(rHigh, cfg, nameof(ConfigMenu.SetReticleSizeAlto));
        WireButton(bBackCfg, menu, nameof(MainMenuController.OpenMain));

        // ---------- Panel creditos ----------
        MakeText("Titulo", panelCreditos.transform, "CRÉDITOS", new Vector3(0f, 1.1f, -0.02f), 0.46f, FontStyles.Bold);
        MakeText("Nombres", panelCreditos.transform, "Pablo Prato\nOmar Correa", new Vector3(0f, 0f, -0.02f), 0.42f, FontStyles.Normal);
        GameObject bBackCred = MakeButton("BotonVolver", panelCreditos.transform, new Vector3(0f, -1.25f, 0f), "VOLVER", 2.2f, matIdle, matHot, interactiveLayer);
        WireButton(bBackCred, menu, nameof(MainMenuController.OpenMain));

        // ---------- referencias del MainMenuController ----------
        SetRef(menu, "_mainPanel", panelMain);
        SetRef(menu, "_configPanel", panelConfig);
        SetRef(menu, "_creditsPanel", panelCreditos);
        SetStr(menu, "_gameSceneName", "Tutorial");
        if (fade != null)
        {
            SetRef(menu, "_fade", fade);
        }
        SetRef(menu, "_door", doorPivot);
        // El rig que se mueve es la RAIZ del Player (mueve camara + todo rigido).
        SetRef(menu, "_cameraRig", player.transform);

        GameObject loadingGO = MakeLoadingText(cam != null ? cam.transform : null);
        if (loadingGO != null)
        {
            SetRef(menu, "_loadingText", loadingGO);
        }

        panelConfig.SetActive(false);
        panelCreditos.SetActive(false);

        // ---------- guardar ----------
        if (!Directory.Exists("Assets/Scenes"))
        {
            Directory.CreateDirectory("Assets/Scenes");
        }
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!saved)
        {
            Debug.LogError("[MenuPrincipalGenerator] No se pudo guardar la escena en " + ScenePath);
            return;
        }

        AddScenesToBuildSettings();

        bool roomOk = AssetDatabase.LoadAssetAtPath<GameObject>(RoomModelPath) != null;
        bool audioOk = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbientAudioPath) != null;

        Debug.Log("<b>[MenuPrincipalGenerator]</b> Listo.\n" +
                  "- Escena: " + ScenePath + "\n" +
                  "- Build Settings: 'Menu Principal' en indice 0, 'Tutorial' incluida.\n" +
                  "- Ambiente: " + (roomOk ? "habitacion cargada" : "SIN habitacion (falta " + RoomModelPath + ")") +
                  " / " + (audioOk ? "audio cargado" : "SIN audio (falta " + AmbientAudioPath + ")") +
                  " / niebla + luz parpadeante OK.\n" +
                  "- Menu MUY transparente (halo difuso, sin fondo solido) a " + MenuDistance +
                  " m; puerta justo detras a " + DoorDistance + " m. 'Empezar': se abre la puerta, " +
                  "la camara la cruza, funde a negro, 'CARGANDO...' unos segundos y aparece en el Tutorial.\n" +
                  (fade == null ? "NOTA: el prefab Player no tiene VRFadeController; el fundido final de 'Empezar' se saltea.\n" : "") +
                  (roomOk ? "" : "Para el fondo: agrega el .glb a Assets/Modelos 3D/, edita la constante RoomModelPath y volve a correr.\n") +
                  (audioOk ? "" : "Para el sonido: agrega el audio a Assets/Audios/, edita la constante AmbientAudioPath y volve a correr.\n") +
                  "Abri la escena y dale Play. Ajustes a mano si hace falta: objeto 'Habitacion' (si el pivote del .glb no cae bien) y objeto 'Puerta' (posicion/tamaño del hueco).");
    }

    // ------------------------------------------------------------------ helpers

    private static void BuildAtmosphere(Scene scene, Transform menuRoot, Camera cam, Vector3 camPos)
    {
        // Sin skybox; ambiente y niebla casi negros.
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = AmbientColor;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogDensity = FogDensity;

        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        // Direccional MUY tenue: da forma a la geometria sin romper la oscuridad.
        GameObject fill = new GameObject("Luz Relleno (tenue)");
        Light fillLight = fill.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = FillLightColor;
        fillLight.intensity = FillLightIntensity;
        fillLight.shadows = LightShadows.None;
        fill.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

        // Point parpadeante entre la camara y el menu, un poco arriba.
        GameObject flick = new GameObject("Luz Parpadeante");
        flick.transform.position = Vector3.Lerp(camPos, menuRoot.position, 0.45f) + Vector3.up * 0.5f;
        Light flickLight = flick.AddComponent<Light>();
        flickLight.type = LightType.Point;
        flickLight.color = FlickerLightColor;
        flickLight.intensity = FlickerLightIntensity;
        flickLight.range = FlickerLightRange;
        flickLight.shadows = LightShadows.Soft;
        FlickeringLight flicker = flick.AddComponent<FlickeringLight>();
        SetFloat(flicker, "_baseIntensity", FlickerLightIntensity);

        // Sonido ambiente en loop 2D (si el clip existe).
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbientAudioPath);
        if (clip != null)
        {
            GameObject audioGO = new GameObject("Ambiente");
            audioGO.transform.position = camPos;
            AudioSource src = audioGO.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = true;
            src.spatialBlend = 0f;
            src.volume = 0.7f;
        }
        else
        {
            Debug.LogWarning("[MenuPrincipalGenerator] No se encontro el audio en '" + AmbientAudioPath +
                             "'. El menu queda sin sonido. Ajusta la constante AmbientAudioPath y volve a correr.");
        }
    }

    private static void ForceCameraRotationOnly(GameObject player)
    {
        Camera cam = player.GetComponentInChildren<Camera>(true);
        if (cam == null)
        {
            return;
        }

        foreach (Component comp in cam.GetComponents<Component>())
        {
            if (comp == null || comp.GetType().Name != "TrackedPoseDriver")
            {
                continue;
            }

            SerializedObject so = new SerializedObject(comp);
            // Input System TrackedPoseDriver: m_TrackingType (0 = Rot+Pos,
            // 1 = Rotation Only, 2 = Position Only).
            SerializedProperty p = so.FindProperty("m_TrackingType");
            if (p != null && p.enumValueIndex != 1)
            {
                p.enumValueIndex = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[MenuPrincipalGenerator] TrackedPoseDriver de la camara -> Rotation Only.");
            }
        }
    }

    private static GameObject SpawnRoom(Scene scene)
    {
        GameObject roomAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RoomModelPath);
        if (roomAsset == null)
        {
            Debug.LogWarning("[MenuPrincipalGenerator] No se encontro la habitacion en '" + RoomModelPath +
                             "'. El menu queda en negro con niebla, sin sala. Ajusta RoomModelPath y volve a correr.");
            return null;
        }

        GameObject room = PrefabUtility.InstantiatePrefab(roomAsset, scene) as GameObject;
        if (room == null)
        {
            room = UnityEngine.Object.Instantiate(roomAsset);
        }
        room.name = "Habitacion";
        room.transform.position = Vector3.zero;
        room.transform.rotation = Quaternion.identity;
        return room;
    }

    private static GameObject MakeLoadingText(Transform camTransform)
    {
        if (camTransform == null)
        {
            return null;
        }

        // Hijo de la camara, delante del fundido a negro, con renderQueue muy
        // alta para dibujarse encima de todo. Arranca apagado.
        GameObject go = new GameObject("CargandoText", typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetParent(camTransform, false);
        go.transform.localPosition = new Vector3(0f, -0.12f, 1.0f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 0.03f;

        TextMeshPro tmp = go.GetComponent<TextMeshPro>();
        tmp.text = "CARGANDO...";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 8f;
        tmp.color = Color.white;
        tmp.enableAutoSizing = false;
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null)
        {
            tmp.font = font;
            if (tmp.fontMaterial != null) tmp.fontMaterial.renderQueue = 5000;
        }
        tmp.rectTransform.sizeDelta = new Vector2(20f, 6f);

        go.SetActive(false);
        return go;
    }

    private static Texture2D CreateOrLoadHazeTexture()
    {
        string path = MatDir + "/MenuHaze.asset";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null)
        {
            return tex;
        }

        const int s = 128;
        tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            name = "MenuHaze",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float nx = (x + 0.5f) / s * 2f - 1f;
                float ny = (y + 0.5f) / s * 2f - 1f;
                // Rectangulo con bordes muy suaves: alpha 1 en el centro, 0 antes
                // de llegar al borde. Da un halo difuso, no un rectangulo duro.
                float d = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));
                float a = Mathf.SmoothStep(1f, 0.35f, d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        EnsureFolder(MatDir);
        AssetDatabase.CreateAsset(tex, path);
        return tex;
    }

    private static Transform BuildDoor(Vector3 camPos, Vector3 camFwdFlat, float floorY)
    {
        Material wood = CreateOrLoadMaterial("MenuPuerta_Hoja", ColPuertaHoja, "Universal Render Pipeline/Lit");
        Material frame = CreateOrLoadMaterial("MenuPuerta_Marco", ColPuertaMarco, "Universal Render Pipeline/Lit");

        Vector3 doorPos = camPos + camFwdFlat * DoorDistance;
        doorPos.y = floorY; // la puerta apoya en el piso

        GameObject door = new GameObject("Puerta");
        door.transform.position = doorPos;
        door.transform.rotation = Quaternion.LookRotation(
            doorPos - new Vector3(camPos.x, 0f, camPos.z), Vector3.up);

        const float openW = 1.15f; // ancho del hueco
        const float openH = 2.25f; // alto del hueco
        const float jamb = 0.12f;  // grosor del marco

        MakeBox("Jamba Izq", door.transform, new Vector3(-(openW / 2f + jamb / 2f), openH / 2f, 0f),
            new Vector3(jamb, openH + jamb, jamb), frame);
        MakeBox("Jamba Der", door.transform, new Vector3(openW / 2f + jamb / 2f, openH / 2f, 0f),
            new Vector3(jamb, openH + jamb, jamb), frame);
        MakeBox("Dintel", door.transform, new Vector3(0f, openH + jamb / 2f, 0f),
            new Vector3(openW + jamb * 2f, jamb, jamb), frame);

        // Pivote sobre el borde izquierdo del hueco, a media altura: la hoja
        // cuelga de aca y este es el objeto que rota MainMenuController.
        GameObject pivot = new GameObject("Pivote");
        pivot.transform.SetParent(door.transform, false);
        pivot.transform.localPosition = new Vector3(-openW / 2f, openH / 2f, 0f);
        pivot.transform.localRotation = Quaternion.identity;

        MakeBox("Hoja", pivot.transform, new Vector3(openW / 2f, 0f, 0f),
            new Vector3(openW, openH, 0.06f), wood);

        return pivot.transform;
    }

    private static GameObject MakeBox(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localRotation = Quaternion.identity;
        box.transform.localScale = size;
        box.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return box;
    }

    private static Material CreateOrLoadMaterial(string name, Color color)
    {
        return CreateOrLoadMaterial(name, color, "Universal Render Pipeline/Unlit");
    }

    private static Material CreateOrLoadMaterial(string name, Color color, string preferredShader)
    {
        string path = MatDir + "/" + name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = Shader.Find(preferredShader)
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Unlit/Color")
                            ?? Shader.Find("Sprites/Default");
            mat = new Material(shader) { name = name };
            EnsureFolder(MatDir);
            AssetDatabase.CreateAsset(mat, path);
        }

        bool transparent = color.a < 0.999f;
        if (transparent)
        {
            // Config estandar de transparencia para shaders URP (Lit / Unlit).
            mat.SetOverrideTag("RenderType", "Transparent");
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // 1 = Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);       // 0 = Alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder))
        {
            return;
        }
        string parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
        string leaf = Path.GetFileName(assetFolder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static GameObject MakePanel(string name, Transform parent, Material halo, float width, float height)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        // Sin fondo solido: solo un halo difuso (textura radial suave) muy
        // transparente detras del texto, para que el menu no sea un rectangulo
        // duro pero igual se distinga del cuarto oscuro.
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Halo";
        UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(go.transform, false);
        quad.transform.localPosition = new Vector3(0f, -0.35f, 0.06f);
        quad.transform.localScale = new Vector3(width, height, 1f);
        quad.GetComponent<MeshRenderer>().sharedMaterial = halo;
        return go;
    }

    private static GameObject MakeText(string name, Transform parent, string text, Vector3 localPos, float size, FontStyles style)
    {
        // RectTransform explicito: TMP lo necesita y no siempre se agrega solo al
        // crear el componente por codigo.
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshPro));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;

        TextMeshPro tmp = go.GetComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.enableAutoSizing = false;
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null)
        {
            tmp.font = font;
            // El texto va SIEMPRE por encima de los paneles transparentes.
            if (tmp.fontMaterial != null) tmp.fontMaterial.renderQueue = 3010;
        }
        tmp.rectTransform.sizeDelta = new Vector2(4.2f, 1.4f);
        return go;
    }

    private static GameObject MakeButton(string name, Transform parent, Vector3 localPos, string label,
        float width, Material idle, Material hot, int layer)
    {
        const float height = 0.55f;

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = localPos;
        quad.transform.localRotation = Quaternion.identity;
        quad.transform.localScale = new Vector3(width, height, 1f);
        quad.layer = layer;

        // El primitive trae un MeshCollider; lo cambiamos por un BoxCollider,
        // mas predecible para el raycast del gaze.
        UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
        BoxCollider box = quad.AddComponent<BoxCollider>();
        box.size = new Vector3(1f, 1f, 0.3f); // en local: con la escala del quad queda width x height x 0.3

        quad.GetComponent<MeshRenderer>().sharedMaterial = idle;

        InteractiveObject io = quad.AddComponent<InteractiveObject>();
        SetRef(io, "_inactiveMaterial", idle);
        SetRef(io, "_gazedAtMaterial", hot);

        // Etiqueta: contra-escalada para que el texto NO salga deformado por la
        // escala no uniforme del quad.
        GameObject lab = new GameObject("Label", typeof(RectTransform), typeof(TextMeshPro));
        lab.transform.SetParent(quad.transform, false);
        lab.transform.localPosition = new Vector3(0f, 0f, -0.06f);
        lab.transform.localRotation = Quaternion.identity;
        lab.transform.localScale = new Vector3(1f / width, 1f / height, 1f);

        TextMeshPro tmp = lab.GetComponent<TextMeshPro>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.05f;
        tmp.fontSizeMax = 0.44f;
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font != null)
        {
            tmp.font = font;
            if (tmp.fontMaterial != null) tmp.fontMaterial.renderQueue = 3010;
        }
        tmp.rectTransform.sizeDelta = new Vector2(width * 0.92f, height * 0.82f);
        return quad;
    }

    private static void WireButton(GameObject button, Component target, string method)
    {
        InteractiveObject io = button.GetComponent<InteractiveObject>();
        if (io == null)
        {
            Debug.LogWarning("[MenuPrincipalGenerator] " + button.name + " no tiene InteractiveObject.");
            return;
        }

        UnityEvent evt = GetOrCreateUnityEvent(io, "_onSelected");
        UnityAction action;
        try
        {
            action = (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, method);
        }
        catch (Exception e)
        {
            Debug.LogError("[MenuPrincipalGenerator] No se pudo cablear " + button.name + " -> " +
                           target.GetType().Name + "." + method + " : " + e.Message);
            return;
        }

        UnityEventTools.AddVoidPersistentListener(evt, action);
        EditorUtility.SetDirty(io);
    }

    private static UnityEvent GetOrCreateUnityEvent(Component component, string fieldName)
    {
        FieldInfo field = component.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            Debug.LogError("[MenuPrincipalGenerator] " + component.GetType().Name + " no tiene el campo " + fieldName);
            return new UnityEvent();
        }
        UnityEvent evt = field.GetValue(component) as UnityEvent;
        if (evt == null)
        {
            evt = new UnityEvent();
            field.SetValue(component, evt);
        }
        return evt;
    }

    private static void SetRef(Component component, string propertyPath, UnityEngine.Object value)
    {
        SerializedObject so = new SerializedObject(component);
        SerializedProperty prop = so.FindProperty(propertyPath);
        if (prop == null)
        {
            Debug.LogWarning("[MenuPrincipalGenerator] " + component.GetType().Name + " no tiene la propiedad " + propertyPath);
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStr(Component component, string propertyPath, string value)
    {
        SerializedObject so = new SerializedObject(component);
        SerializedProperty prop = so.FindProperty(propertyPath);
        if (prop == null)
        {
            Debug.LogWarning("[MenuPrincipalGenerator] " + component.GetType().Name + " no tiene la propiedad " + propertyPath);
            return;
        }
        prop.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetFloat(Component component, string propertyPath, float value)
    {
        SerializedObject so = new SerializedObject(component);
        SerializedProperty prop = so.FindProperty(propertyPath);
        if (prop == null)
        {
            Debug.LogWarning("[MenuPrincipalGenerator] " + component.GetType().Name + " no tiene la propiedad " + propertyPath);
            return;
        }
        prop.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureInteractiveLayerInMask(GameObject player, int interactiveLayer)
    {
        GazeController gaze = player.GetComponentInChildren<GazeController>(true);
        if (gaze == null)
        {
            Debug.LogWarning("[MenuPrincipalGenerator] El prefab Player no tiene GazeController; los botones no van a responder hasta que se lo agregues.");
            return;
        }

        SerializedObject so = new SerializedObject(gaze);
        SerializedProperty mask = so.FindProperty("_interactiveLayerMask");
        if (mask == null)
        {
            return;
        }
        int bit = 1 << interactiveLayer;
        if ((mask.intValue & bit) == 0)
        {
            mask.intValue |= bit;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[MenuPrincipalGenerator] Se agrego la layer '" + InteractiveLayerName +
                      "' al Interactive Layer Mask del GazeController de esta escena (los botones la necesitan).");
        }
    }

    private static void AddScenesToBuildSettings()
    {
        var list = EditorBuildSettings.scenes.ToList();

        list.RemoveAll(s => s.path == ScenePath);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

        if (File.Exists(TutorialScenePath))
        {
            int idx = list.FindIndex(s => s.path == TutorialScenePath);
            if (idx >= 0)
            {
                list[idx] = new EditorBuildSettingsScene(TutorialScenePath, true);
            }
            else
            {
                list.Add(new EditorBuildSettingsScene(TutorialScenePath, true));
            }
        }

        EditorBuildSettings.scenes = list.ToArray();
    }
}
