using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Cablea la mecanica del Tutorial SIN tocar el rig ni mover NADA de lugar:
///   - "keys"        -> llave (Collectable -> KeyInventory.CollectKey)
///   - "FrontSide_55" -> puerta de salida (LevelDoor -> "Parte 1 - El Despertar",
///     cartel "PARTE 1 / HOSPITAL", pide la llave)
///   - crea la pantalla de carga (hija de la camara) y el ambiente en loop
///   - habilita Parte 1 en Build Settings
///
/// El rig (Player desempaquetado, Main Camera afuera de Player, Cardboard
/// desactivado) y todas las posiciones quedan intactas. Lo unico que borra es
/// basura de corridas viejas de este mismo tool (LlaveTutorial, PuertaSalida,
/// AmbienteTutorial, PantallaCarga, y rigs Player sin desempaquetar).
/// </summary>
public static class TutorialSetup
{
    private const string TutorialScenePath = "Assets/Scenes/Tutorial.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string KeyObjectName = "keys";
    private const string DoorObjectName = "FrontSide_55";
    private const string NextSceneName = "Parte 1 - El Despertar";
    private const string InteractiveLayerName = "Interactive";

    private static readonly string[] AmbientCandidates =
    {
        "Assets/Audios/TutorialAmbiente.wav",
        "Assets/Audios/TutorialAmbiente.mp3",
        "Assets/Audios/TutorialAmbiente.ogg",
        "Assets/Audios/TutorialAmbiente.flac",
    };

    [MenuItem("Tools/Feria de Ciencias/Preparar Tutorial (llave + puerta + carga)")]
    public static void Prepare()
    {
        if (!File.Exists(TutorialScenePath))
        {
            EditorUtility.DisplayDialog("Falta la escena", "No se encontro " + TutorialScenePath, "Ok");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);
        int interactiveLayer = LayerMask.NameToLayer(InteractiveLayerName);

        // ---- borrar SOLO basura de corridas viejas de este tool ----
        int trashed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            bool oldTrash = root.name == "LlaveTutorial" || root.name == "PuertaSalida" || root.name == "AmbienteTutorial";
            bool prefabRig = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) == PlayerPrefabPath;
            if (oldTrash || prefabRig)
            {
                Object.DestroyImmediate(root);
                trashed++;
            }
        }
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToArray())
        {
            if (t != null && t.name == "PantallaCarga")
            {
                Object.DestroyImmediate(t.gameObject);
                trashed++;
            }
        }
        if (trashed > 0)
        {
            Debug.Log("[TutorialSetup] Borrados " + trashed + " objetos basura de corridas viejas.");
        }

        // ---- KeyInventory ----
        KeyInventory inv = Object.FindFirstObjectByType<KeyInventory>();
        if (inv == null)
        {
            GameObject invGo = GameObject.Find("Key inventory");
            if (invGo == null) invGo = new GameObject("Key inventory");
            inv = invGo.AddComponent<KeyInventory>();
            Debug.Log("[TutorialSetup] KeyInventory agregado.");
        }

        // ---- llave: el objeto "keys" (no lo movemos) ----
        GameObject keyGo = FindByName(KeyObjectName);
        if (keyGo == null)
        {
            Debug.LogWarning("[TutorialSetup] No encontre ningun objeto '" + KeyObjectName + "'. La llave NO se cablea.");
        }
        else
        {
            RemoveOtherGazeInteractables(keyGo, typeof(Collectable));
            Collectable col = GetOrAdd<Collectable>(keyGo);
            EnsureCollider(keyGo);
            if (interactiveLayer >= 0) keyGo.layer = interactiveLayer;
            ClearAndWireEvent(col, "_onCollected", inv, nameof(KeyInventory.CollectKey));
            Debug.Log("[TutorialSetup] Llave = '" + keyGo.name + "' (pos " + keyGo.transform.position.ToString("0.0") + ", intacta).");
        }

        // ---- puerta de salida: el objeto "FrontSide_55" (no lo movemos) ----
        GameObject doorGo = FindByName(DoorObjectName);
        if (doorGo == null)
        {
            Debug.LogWarning("[TutorialSetup] No encontre ningun objeto '" + DoorObjectName + "'. La puerta NO se cablea.");
        }
        else
        {
            RemoveOtherGazeInteractables(doorGo, typeof(LevelDoor));
            LevelDoor lvl = GetOrAdd<LevelDoor>(doorGo);
            EnsureCollider(doorGo);
            if (interactiveLayer >= 0) doorGo.layer = interactiveLayer;
            SetStr(lvl, "_nextSceneName", NextSceneName);
            SetStr(lvl, "_bigText", "PARTE 1");
            SetStr(lvl, "_subText", "EL DESPERTAR");
            SetBool(lvl, "_requiresKey", true);
            SetRef(lvl, "_keyInventory", inv);
            Debug.Log("[TutorialSetup] Puerta de salida = '" + doorGo.name + "' (pos " + doorGo.transform.position.ToString("0.0") +
                      ", intacta) -> " + NextSceneName + " (con fundido + cartel 'PARTE 1 / EL DESPERTAR').");
        }

        // ---- ambiente en loop ----
        AudioClip amb = null; string ambPath = null;
        foreach (string p in AmbientCandidates)
        {
            amb = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (amb != null) { ambPath = p; break; }
        }
        if (amb != null)
        {
            AudioSource src = GetOrAdd<AudioSource>(new GameObject("AmbienteTutorial"));
            src.clip = amb; src.loop = true; src.playOnAwake = true; src.spatialBlend = 0f; src.volume = 0.5f;
            Debug.Log("[TutorialSetup] Ambiente en loop: " + ambPath);
        }
        else
        {
            Debug.LogWarning("[TutorialSetup] Sin ambiente: agrega Assets/Audios/TutorialAmbiente.wav (o .mp3/.ogg/.flac) y volve a correr.");
        }

        // ---- Build Settings ----
        EnableSceneInBuild("Assets/Scenes/" + NextSceneName + ".unity");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("<b>[TutorialSetup]</b> LISTO. Rig y posiciones intactos. " +
                  (keyGo != null ? "Llave OK. " : "SIN llave. ") +
                  (doorGo != null ? "Puerta OK -> " + NextSceneName + ". " : "SIN puerta. ") +
                  "Dale Play y proba.");
    }

    // ---------------------------------------------------------------- helpers

    private static GameObject FindByName(string name)
    {
        string low = name.ToLowerInvariant();
        Transform exact = null, contains = null;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            string tn = t.name.ToLowerInvariant();
            if (tn == low) { exact = t; break; }
            if (contains == null && tn.Contains(low)) contains = t;
        }
        Transform pick = exact != null ? exact : contains;
        return pick != null ? pick.gameObject : null;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    // Saca de 'go' todo MonoBehaviour que sea IGazeInteractable menos el tipo
    // 'keep'. El GazeController usa el PRIMER IGazeInteractable que encuentra, asi
    // que si queda un Door viejo + un LevelDoor nuevo, gana el viejo y el nuevo
    // no dispara nunca (gotcha conocido del proyecto).
    private static void RemoveOtherGazeInteractables(GameObject go, System.Type keep)
    {
        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
        {
            if (mb == null || mb.GetType() == keep) continue;
            if (mb is IGazeInteractable)
            {
                Debug.Log("[TutorialSetup] Saco '" + mb.GetType().Name + "' de '" + go.name +
                          "' (chocaba con la mecanica nueva).");
                Object.DestroyImmediate(mb);
            }
        }
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
        else
        {
            bc.size = Vector3.one;
        }
    }

    private static void EnableSceneInBuild(string path)
    {
        if (!File.Exists(path)) { Debug.LogWarning("[TutorialSetup] No existe " + path + " para Build Settings."); return; }
        var list = EditorBuildSettings.scenes.ToList();
        int i = list.FindIndex(s => s.path == path);
        if (i >= 0) list[i] = new EditorBuildSettingsScene(path, true);
        else list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    private static void ClearAndWireEvent(Component c, string field, Object target, string method)
    {
        FieldInfo fi = c.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi == null) { Debug.LogWarning("[TutorialSetup] " + c.GetType().Name + " no tiene " + field); return; }
        UnityEvent evt = fi.GetValue(c) as UnityEvent;
        if (evt == null) { evt = new UnityEvent(); fi.SetValue(c, evt); }
        for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(evt, i);
        }
        UnityEventTools.AddVoidPersistentListener(evt,
            (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, method));
        EditorUtility.SetDirty(c);
    }

    private static void SetRef(Component c, string prop, Object value)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("[TutorialSetup] " + c.GetType().Name + " no tiene " + prop); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStr(Component c, string prop, string value)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) return;
        p.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Component c, string prop, bool value)
    {
        SerializedObject so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(prop);
        if (p == null) return;
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
