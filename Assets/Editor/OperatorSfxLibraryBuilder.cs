using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea o actualiza Assets/Resources/OperatorSfxLibrary.asset con los clips que
/// usa el operador de la feria (OperatorScares), el prefab del monstruo y el
/// grito del screamer. Buscar los clips en Assets/Audios (incluye subcarpetas).
/// Menu: Tools > Feria de Ciencias > Operador: armar biblioteca de sonidos.
/// Es idempotente: se puede volver a correr cuando se agreguen clips.
/// </summary>
public static class OperatorSfxLibraryBuilder
{
    private const string AssetPath = "Assets/Resources/OperatorSfxLibrary.asset";
    private const string MonsterPrefabPath = "Assets/Prefabs/Monstruo_Rig.prefab";
    private const string ScreamClipName = "grito_screamer";
    private const string SearchFolder = "Assets/Audios";

    [MenuItem("Tools/Feria de Ciencias/Operador: armar biblioteca de sonidos")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        // Indexa todos los AudioClip de Assets/Audios por nombre de archivo.
        Dictionary<string, AudioClip> byName = new Dictionary<string, AudioClip>(System.StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { SearchFolder });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (byName.ContainsKey(name))
            {
                Debug.LogWarning("[OperatorSfxLibraryBuilder] Nombre repetido, se usa el primero: " + name + " (" + path + ")");
                continue;
            }
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                byName[name] = clip;
            }
        }

        OperatorSfxLibrary lib = AssetDatabase.LoadAssetAtPath<OperatorSfxLibrary>(AssetPath);
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<OperatorSfxLibrary>();
            AssetDatabase.CreateAsset(lib, AssetPath);
        }

        var clips = new List<AudioClip>();
        var missing = new List<string>();
        foreach (string name in OperatorScares.ClipNames)
        {
            if (byName.TryGetValue(name, out AudioClip clip))
            {
                clips.Add(clip);
            }
            else
            {
                missing.Add(name);
            }
        }

        lib.clips = clips.ToArray();
        lib.monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
        lib.screamClip = byName.TryGetValue(ScreamClipName, out AudioClip scream) ? scream : null;

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var sb = new StringBuilder();
        sb.Append("[OperatorSfxLibraryBuilder] Biblioteca lista (" + AssetPath + "): ");
        sb.Append(clips.Count + "/" + OperatorScares.ClipNames.Length + " clips");
        sb.Append(lib.monsterPrefab != null ? ", prefab OK" : ", PREFAB FALTANTE (" + MonsterPrefabPath + ")");
        sb.Append(lib.screamClip != null ? ", grito OK" : ", GRITO FALTANTE");
        if (missing.Count > 0)
        {
            sb.Append(". Faltan: " + string.Join(", ", missing));
        }
        Debug.Log(sb.ToString());
    }
}
