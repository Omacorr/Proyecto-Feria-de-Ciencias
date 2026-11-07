using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pone (o saca) los portales nuevos (PortalVisual) en la escena abierta.
///
/// "Portales: aplicar a la escena abierta":
///  - A cada TeleportPoint de la escena (tambien los inactivos) que NO sea una
///    puerta (HasDestinationOverride) le agrega/configura PortalVisual con el
///    estilo de la escena, materiales, sonido, polvo, y le tilda
///    HasExternalVisual (el orbe/cubo viejo queda en la escena, apagado).
///  - Mide el piso bajo cada punto y la linea de vista entre puntos con
///    MeshColliders TEMPORALES (los .glb no tienen colliders, gotcha #1;
///    backfaces prendidos, gotcha #17) que se borran al terminar.
///  - Visible Distance = max(12, vecino visible mas lejano + 1).
///  - Saltea las flechas de Parte 2 (hijos de "Flechas": guian la persecucion).
///  - Usa Undo; deja la escena sucia SIN guardar.
/// "Portales: revertir en la escena abierta": saca PortalVisual y destilda
/// HasExternalVisual (el visual viejo vuelve tal cual).
///
/// Antes hay que correr "Portales: generar texturas y materiales".
/// </summary>
public static class PortalSetupTool
{
    private const int LayerIgnoreRaycast = 2;
    private const int LayerInteractive = 7;
    private const int LayerTeleport = 9;
    private const string ClipFolder = "Assets/Audios/Efectos";

    [MenuItem("Tools/Feria de Ciencias/Portales: aplicar a la escena abierta")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        PortalVisual.PortalStyle style;
        if (!StyleForScene(scene.name, out style))
        {
            if (!EditorUtility.DisplayDialog("Portales", "La escena '" + scene.name + "' no es Tutorial ni Parte 1-5. ¿Aplicar el estilo Simple igual?", "Aplicar", "Cancelar"))
            {
                return;
            }
            style = PortalVisual.PortalStyle.Simple;
        }

        if (!LoadMaterials(style, out StyleMaterials mats, out string missing))
        {
            EditorUtility.DisplayDialog("Portales", "Faltan materiales en " + PortalTextureGenerator.MatFolder + ":\n" + missing +
                "\n\nCorré primero 'Tools/Feria de Ciencias/Portales: generar texturas y materiales'.", "OK");
            return;
        }
        AudioClip[] clips = ClipsForStyle(style);

        // Puntos: todos los TeleportPoint de ESTA escena, tambien inactivos.
        List<TeleportPoint> points = new List<TeleportPoint>();
        StringBuilder skipped = new StringBuilder();
        foreach (TeleportPoint tp in SceneTeleportPoints(scene))
        {
            if (tp.HasDestinationOverride) { skipped.AppendLine("  " + tp.name + " (puerta)"); continue; }
            if (tp.transform.parent != null && tp.transform.parent.name == "Flechas") { skipped.AppendLine("  " + tp.name + " (flecha de Parte 2)"); continue; }
            points.Add(tp);
        }
        if (points.Count == 0)
        {
            EditorUtility.DisplayDialog("Portales", "No hay TeleportPoint (sin puertas) en '" + scene.name + "'.", "OK");
            return;
        }

        float maxGaze = ReadMaxGazeDistance();
        int n = points.Count;
        float[] floorY = new float[n];
        string[] floorSrc = new string[n];
        float[] visDist = new float[n];

        List<MeshCollider> temp = new List<MeshCollider>();
        bool prevBackfaces = Physics.queriesHitBackfaces;
        int skippedUnreadable = 0;
        try
        {
            AddTempColliders(scene, temp, ref skippedUnreadable);
            Physics.queriesHitBackfaces = true;
            Physics.SyncTransforms();
            int mask = ~((1 << LayerIgnoreRaycast) | (1 << LayerInteractive) | (1 << LayerTeleport));

            EditorUtility.DisplayProgressBar("Portales", "Midiendo el piso...", 0.5f);
            RaycastHit[] hits = new RaycastHit[32];
            for (int i = 0; i < n; i++)
            {
                floorY[i] = MeasureFloor(points[i], hits, mask, out floorSrc[i]);
            }

            EditorUtility.DisplayProgressBar("Portales", "Linea de vista entre puntos...", 0.75f);
            bool haveColliders = temp.Count > 0 || AnySceneCollider(scene, mask);
            for (int i = 0; i < n; i++)
            {
                Vector3 pi = points[i].transform.position;
                Vector3 eyeI = new Vector3(pi.x, floorY[i] + 1.5f, pi.z);
                float farthestVisible = 0f, nearest = float.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (i == j) continue;
                    Vector3 pj = points[j].transform.position;
                    Vector3 eyeJ = new Vector3(pj.x, floorY[j] + 1.5f, pj.z);
                    float d = Vector3.Distance(eyeI, eyeJ);
                    if (d < 0.5f) continue;
                    if (d < nearest) nearest = d;
                    if (d > maxGaze || !haveColliders) continue;
                    if (!Physics.Linecast(eyeI, eyeJ, mask, QueryTriggerInteraction.Ignore))
                    {
                        if (d > farthestVisible) farthestVisible = d;
                    }
                }
                float v;
                if (farthestVisible > 0f) v = farthestVisible + 1f;
                else if (nearest < float.MaxValue) v = Mathf.Max(nearest * 1.5f, nearest + 1f);
                else v = 12f;
                visDist[i] = Mathf.Max(12f, v);
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = prevBackfaces;
            foreach (MeshCollider mc in temp)
            {
                if (mc != null) Object.DestroyImmediate(mc);
            }
            Physics.SyncTransforms();
            EditorUtility.ClearProgressBar();
        }

        // Aplicar.
        Undo.SetCurrentGroupName("Portales: aplicar");
        int group = Undo.GetCurrentGroup();
        StringBuilder log = new StringBuilder();
        log.AppendLine("[PortalSetupTool] '" + scene.name + "': estilo " + style + ", " + n + " puntos (mirada max " + maxGaze + " m).");
        for (int i = 0; i < n; i++)
        {
            TeleportPoint tp = points[i];
            PortalVisual pv = tp.GetComponent<PortalVisual>();
            if (pv == null) pv = Undo.AddComponent<PortalVisual>(tp.gameObject);

            SerializedObject so = new SerializedObject(pv);
            so.FindProperty("_style").enumValueIndex = (int)style;
            SetObj(so, "_markMaterial", mats.Mark);
            SetObj(so, "_backwardMarkMaterial", mats.Backward);
            SetObj(so, "_confettiMaterial", mats.Confetti);
            SetObj(so, "_candleMaterial", mats.Candle);
            SetObj(so, "_flameMaterial", mats.Flame);
            SetObj(so, "_mothMaterial", mats.Moth);
            SetObj(so, "_dustMaterial", mats.Dust);
            so.FindProperty("_floorOffset").floatValue = floorY[i] - tp.transform.position.y;
            so.FindProperty("_raycastFloorAtRuntime").boolValue = false;
            so.FindProperty("_markSize").floatValue = MarkSizes[(int)style];
            so.FindProperty("_markYaw").floatValue = NameHash01(tp.name) * 360f;
            so.FindProperty("_visibleDistance").floatValue = visDist[i];
            so.FindProperty("_dustColor").colorValue = DustColors[(int)style];
            SerializedProperty clipsProp = so.FindProperty("_gazeClips");
            clipsProp.arraySize = clips.Length;
            for (int c = 0; c < clips.Length; c++) clipsProp.GetArrayElementAtIndex(c).objectReferenceValue = clips[c];
            so.ApplyModifiedProperties();

            SerializedObject tso = new SerializedObject(tp);
            tso.FindProperty("_hasExternalVisual").boolValue = true;
            tso.ApplyModifiedProperties();

            log.AppendLine("  " + Path(tp.transform) + (tp.gameObject.activeInHierarchy ? "" : " (inactivo)") +
                ": piso " + floorSrc[i] + " (" + (floorY[i] - tp.transform.position.y).ToString("F2") + " m), visible " + visDist[i].ToString("F1") + " m");
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);

        if (skipped.Length > 0) log.AppendLine(" Salteados:\n" + skipped);
        if (skippedUnreadable > 0) log.AppendLine(" Mallas no legibles sin collider temporal: " + skippedUnreadable + " (piso/vista aproximados en esas zonas).");
        log.AppendLine(clips.Length == 0 ? " Sonido al mirar: ninguno asignado (decidir a mano en _gazeClips)." : " Sonido al mirar: " + clips.Length + " clip(s).");
        log.AppendLine(" Escena sucia, SIN guardar. Revisá y guardá con Ctrl+S.");
        Debug.Log(log.ToString());
    }

    [MenuItem("Tools/Feria de Ciencias/Portales: revertir en la escena abierta")]
    public static void Revert()
    {
        Scene scene = SceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("Portales: revertir");
        int group = Undo.GetCurrentGroup();
        int removed = 0;
        foreach (TeleportPoint tp in SceneTeleportPoints(scene))
        {
            PortalVisual pv = tp.GetComponent<PortalVisual>();
            if (pv != null)
            {
                Undo.DestroyObjectImmediate(pv);
                removed++;
            }
            SerializedObject tso = new SerializedObject(tp);
            SerializedProperty p = tso.FindProperty("_hasExternalVisual");
            if (p != null && p.boolValue)
            {
                p.boolValue = false;
                tso.ApplyModifiedProperties();
            }
        }
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[PortalSetupTool] '" + scene.name + "': " + removed + " PortalVisual quitados, HasExternalVisual destildado. Escena sucia, SIN guardar.");
    }

    // ---------------- Estilos ----------------

    // Los estilos siguen el orden de las partes: Parte N usa el enum N (Tutorial = Simple = 0).
    private static bool StyleForScene(string sceneName, out PortalVisual.PortalStyle style)
    {
        style = PortalVisual.PortalStyle.Simple;
        if (sceneName.Contains("Tutorial")) return true;
        for (int i = 1; i <= 5; i++)
        {
            if (sceneName.Contains("Parte " + i)) { style = (PortalVisual.PortalStyle)i; return true; }
        }
        return false;
    }

    /// <summary>Todos los TeleportPoint de la escena (tambien inactivos), sin assets.</summary>
    private static IEnumerable<TeleportPoint> SceneTeleportPoints(Scene scene)
    {
        foreach (TeleportPoint tp in Object.FindObjectsByType<TeleportPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tp != null && tp.gameObject.scene == scene && !EditorUtility.IsPersistent(tp)) yield return tp;
        }
    }

    private struct StyleMaterials
    {
        public Material Mark, Backward, Confetti, Candle, Flame, Moth, Dust;
    }

    private static bool LoadMaterials(PortalVisual.PortalStyle style, out StyleMaterials m, out string missing)
    {
        m = new StyleMaterials();
        StringBuilder miss = new StringBuilder();
        m.Dust = Mat(PortalTextureGenerator.MatPolvo, miss);
        switch (style)
        {
            case PortalVisual.PortalStyle.Simple:
                m.Mark = Mat(PortalTextureGenerator.MatSimple, miss); break;
            case PortalVisual.PortalStyle.HuellasDescalzas:
                m.Mark = Mat(PortalTextureGenerator.MatDescalzas, miss); break;
            case PortalVisual.PortalStyle.Velita:
                m.Mark = Mat(PortalTextureGenerator.MatVelitaBrillo, miss);
                m.Confetti = Mat(PortalTextureGenerator.MatVelitaConfeti, miss);
                m.Candle = Mat(PortalTextureGenerator.MatVelitaVela, miss);
                m.Flame = Mat(PortalTextureGenerator.MatVelitaLlama, miss);
                break;
            case PortalVisual.PortalStyle.Polillas:
                m.Mark = Mat(PortalTextureGenerator.MatPolillasMancha, miss);
                m.Moth = Mat(PortalTextureGenerator.MatPolilla, miss);
                break;
            case PortalVisual.PortalStyle.HuellasAtras:
                m.Mark = Mat(PortalTextureGenerator.MatAtrasRayones, miss);
                m.Backward = Mat(PortalTextureGenerator.MatAtrasHuellas, miss);
                break;
            case PortalVisual.PortalStyle.ManchaHumedad:
                m.Mark = Mat(PortalTextureGenerator.MatHumedad, miss); break;
        }
        missing = miss.ToString();
        return missing.Length == 0;
    }

    private static Material Mat(string name, StringBuilder miss)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(PortalTextureGenerator.MatFolder + "/" + name + ".mat");
        if (mat == null) miss.AppendLine("  " + name + ".mat");
        return mat;
    }

    private static AudioClip[] ClipsForStyle(PortalVisual.PortalStyle style)
    {
        // Parte 3 (pedido del plan) y Parte 4 (pasillo de madera): crujidos.
        // El resto: no hay en Assets/Audios/Efectos nada bajo y adecuado; se
        // deja vacio para que lo decida la sesion principal / Omar.
        if (style == PortalVisual.PortalStyle.Polillas || style == PortalVisual.PortalStyle.HuellasAtras)
        {
            List<AudioClip> list = new List<AudioClip>();
            for (int i = 1; i <= 3; i++)
            {
                AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipFolder + "/crujido_madera_0" + i + ".wav");
                if (c != null) list.Add(c);
            }
            return list.ToArray();
        }
        return new AudioClip[0];
    }

    // Por estilo, en el orden del enum PortalStyle (Simple, Descalzas, Velita, Polillas, HuellasAtras, Humedad).
    private static readonly float[] MarkSizes = { 0.9f, 0.75f, 0.8f, 0.9f, 0.8f, 1.1f };

    private static readonly Color[] DustColors =
    {
        new Color(0.85f, 0.82f, 0.75f, 0.5f), new Color(0.75f, 0.8f, 0.85f, 0.45f), new Color(1f, 0.85f, 0.6f, 0.5f),
        new Color(0.7f, 0.65f, 0.55f, 0.45f), new Color(0.85f, 0.7f, 0.5f, 0.45f), new Color(0.6f, 0.8f, 0.9f, 0.4f)
    };

    // ---------------- Medicion ----------------

    private static float ReadMaxGazeDistance()
    {
        GazeController gc = Object.FindFirstObjectByType<GazeController>(FindObjectsInactive.Include);
        if (gc == null) return 35f;
        SerializedProperty p = new SerializedObject(gc).FindProperty("_maxGazeDistance");
        return p != null ? Mathf.Max(p.floatValue, 10f) : 35f;
    }

    /// <summary>MeshCollider temporal en cada MeshRenderer activo de la escena de mas de 0.8 m, salvo los de los TeleportPoint.</summary>
    private static void AddTempColliders(Scene scene, List<MeshCollider> temp, ref int unreadable)
    {
        MeshRenderer[] all = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            MeshRenderer mr = all[i];
            if ((i & 63) == 0) EditorUtility.DisplayProgressBar("Portales", "Colliders temporales (" + i + "/" + all.Length + ")...", 0.1f + 0.35f * i / all.Length);
            if (mr == null || !mr.enabled || mr.gameObject.scene != scene) continue;
            int layer = mr.gameObject.layer;
            if (layer == LayerIgnoreRaycast || layer == LayerInteractive || layer == LayerTeleport) continue;
            if (mr.GetComponentInParent<TeleportPoint>(true) != null) continue;
            if (mr.GetComponent<Collider>() != null) continue;
            Vector3 size = mr.bounds.size;
            if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < 0.8f) continue;
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            if (!mf.sharedMesh.isReadable) { unreadable++; continue; }
            MeshCollider mc = mr.gameObject.AddComponent<MeshCollider>();
            mc.hideFlags = HideFlags.HideAndDontSave;
            mc.sharedMesh = mf.sharedMesh;
            temp.Add(mc);
        }
    }

    private static bool AnySceneCollider(Scene scene, int mask)
    {
        foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c != null && c.enabled && !c.isTrigger && c.gameObject.scene == scene && ((1 << c.gameObject.layer) & mask) != 0) return true;
        }
        return false;
    }

    private static float MeasureFloor(TeleportPoint tp, RaycastHit[] hits, int mask, out string source)
    {
        Vector3 p = tp.transform.position;
        int count = Physics.RaycastNonAlloc(p + Vector3.up * 0.3f, Vector3.down, hits, 4f, mask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        float y = 0f;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hits[i];
            if (Mathf.Abs(h.normal.y) < 0.6f) continue;
            if (h.collider.GetComponentInParent<TeleportPoint>(true) != null) continue;
            if (h.distance < best) { best = h.distance; y = h.point.y; }
        }
        if (best < float.MaxValue)
        {
            source = "raycast";
            return y;
        }

        // Sin piso con collider: el visual viejo. Un cubo achatado esta apoyado
        // en el piso; un orbe flota (Parte 4 a ~1.2 m, Parte 5 a ~1 m).
        Renderer r = null;
        foreach (Renderer rr in tp.GetComponentsInChildren<Renderer>(true))
        {
            if (rr.transform.name != PortalVisual.RootName) { r = rr; break; }
        }
        if (r != null && r.bounds.size.y < 0.4f)
        {
            source = "base del cubo";
            return r.bounds.min.y;
        }
        source = "ESTIMADO (1 m bajo el pivote, revisar)";
        return p.y - 1f;
    }

    // ---------------- Helpers ----------------

    private static void SetObj(SerializedObject so, string prop, Object value)
    {
        SerializedProperty p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }

    private static float NameHash01(string s)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < s.Length; i++) h = h * 31 + s[i];
            return (h & 0xFFFF) / 65535f;
        }
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        for (Transform x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
        return p;
    }
}
