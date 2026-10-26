using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de Editor para bajar draw calls: combina las mallas estaticas de un modelo
/// importado (por ejemplo un .glb de glTFast con miles de renderers) en pocas mallas grandes,
/// agrupadas por material y por celda espacial XZ (para que el frustum culling siga sirviendo).
///
/// Por que existe: en Cardboard (multi-pass) cada renderer cuesta dos veces; un modelo con
/// ~5000 MeshRenderers es inviable en el A54. Batching estatico de Unity no ayuda aca porque
/// los objetos vienen de un import y no se quiere tocar el .glb.
///
/// No destruye nada: los renderers originales solo se desactivan (enabled = false) y todo se
/// registra con Undo. "Revertir" los reactiva y borra lo generado.
/// </summary>
public static class StaticMeshCombiner
{
    /// <summary>Nombre del GameObject que contiene las mallas combinadas (hermano de root).</summary>
    public const string CombinedContainerName = "__Combinadas";

    /// <summary>Archivo de registro (en la carpeta de salida) con los renderers que se desactivaron.</summary>
    private const string RegistryFileName = "_registro_combinado.txt";

    private const int DefaultCellSize = 20;

    // ------------------------------------------------------------------ Menu

    [MenuItem("Tools/Feria de Ciencias/Combinar mallas estáticas (selección)")]
    private static void MenuCombine()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null) return;
        string folder = DefaultFolder(root);
        string result = Combine(root, DefaultCellSize, folder);
        Debug.Log(result, root);
    }

    [MenuItem("Tools/Feria de Ciencias/Revertir combinado (selección)")]
    private static void MenuRevert()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null) return;
        string result = Revert(root, DefaultFolder(root));
        Debug.Log(result, root);
    }

    [MenuItem("Tools/Feria de Ciencias/Combinar mallas estáticas (selección)", true)]
    [MenuItem("Tools/Feria de Ciencias/Revertir combinado (selección)", true)]
    private static bool MenuValidate()
    {
        GameObject go = Selection.activeGameObject;
        return go != null && go.scene.IsValid() && !EditorUtility.IsPersistent(go);
    }

    /// <summary>Carpeta de salida por defecto: Assets/Modelos 3D/Combinadas/&lt;escena&gt;_&lt;root&gt;.</summary>
    public static string DefaultFolder(GameObject root)
    {
        string sceneName = root.scene.IsValid() && !string.IsNullOrEmpty(root.scene.name) ? root.scene.name : "Escena";
        return "Assets/Modelos 3D/Combinadas/" + Sanitize(sceneName) + "_" + Sanitize(root.name);
    }

    // ------------------------------------------------------------------ Tipos internos

    private sealed class Candidate
    {
        public MeshRenderer Renderer;
        public Mesh Mesh;
        public Material[] Materials;
    }

    private sealed class Group
    {
        public Material Material;
        public int CellX, CellZ, Layer;
        public ShadowCastingMode Shadows;
        public bool Receive;
        public LightProbeUsage LightProbes;
        public ReflectionProbeUsage ReflectionProbes;
        public MotionVectorGenerationMode MotionVectors;
        public readonly List<CombineInstance> Instances = new List<CombineInstance>();
        public readonly List<Candidate> Owners = new List<Candidate>();
        public bool SingleSubmeshOnly = true; // true si todos sus duenos tienen un solo submesh
        public long Vertices;
        public long Triangles;
        public bool Skip;
    }

    // ------------------------------------------------------------------ Combinar

    /// <summary>
    /// Combina las mallas estaticas activas bajo <paramref name="root"/>.
    /// </summary>
    /// <param name="root">Objeto cuyo contenido se combina. El propio root no se revisa por componentes extra.</param>
    /// <param name="cellSize">Tamano de celda en metros (plano XZ). Si es &lt;= 0 se usa 20.</param>
    /// <param name="outputFolder">Carpeta de Assets donde se guardan las mallas (se crea si falta).</param>
    /// <returns>Resumen en texto; empieza con "ERROR" si no se pudo hacer nada.</returns>
    public static string Combine(GameObject root, float cellSize, string outputFolder)
    {
        if (root == null) return "ERROR: root es null.";
        if (EditorApplication.isPlaying) return "ERROR: no combinar en modo Play.";
        if (!root.scene.IsValid() || EditorUtility.IsPersistent(root)) return "ERROR: root tiene que ser un objeto de escena.";
        if (cellSize <= 0f) cellSize = DefaultCellSize;
        if (string.IsNullOrEmpty(outputFolder)) outputFolder = DefaultFolder(root);
        outputFolder = outputFolder.Replace('\\', '/').TrimEnd('/');
        if (!outputFolder.StartsWith("Assets")) return "ERROR: outputFolder tiene que empezar con 'Assets'.";

        Transform parent = root.transform.parent;
        if (FindContainer(root) != null)
            return "ERROR: ya existe un '" + CombinedContainerName + "' junto a '" + root.name + "'. Revertí primero.";

        if (root.GetComponent<Animator>() != null || root.GetComponent<LODGroup>() != null)
            Debug.LogWarning("[StaticMeshCombiner] root tiene Animator/LODGroup; se combina igual (el root no se revisa).");

        int renderersTotal = 0, excludedComponents = 0, excludedNoMesh = 0, excludedInvalid = 0;
        long trianglesBefore = 0;
        var unreadable = new HashSet<string>();
        var candidates = new List<Candidate>();
        var extraCache = new Dictionary<Transform, bool>();

        MeshRenderer[] all = root.GetComponentsInChildren<MeshRenderer>(false); // solo activos en la jerarquia

        foreach (MeshRenderer mr in all)
        {
            if (mr == null || !mr.enabled) continue;
            if (IsUnderContainer(mr.transform, root.transform)) continue;
            renderersTotal++;

            if (HasExtraComponents(mr.transform, root.transform, extraCache)) { excludedComponents++; continue; }

            MeshFilter mf = mr.GetComponent<MeshFilter>();
            Mesh mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null) { excludedNoMesh++; continue; }
            if (!mesh.isReadable) { unreadable.Add(mesh.name); excludedInvalid++; continue; }

            Material[] mats = mr.sharedMaterials;
            bool valid = mesh.subMeshCount > 0;
            for (int i = 0; i < mesh.subMeshCount && valid; i++)
            {
                if (i >= mats.Length || mats[i] == null) valid = false;
                else if (mesh.GetTopology(i) != MeshTopology.Triangles) valid = false;
            }
            if (!valid) { excludedInvalid++; continue; }

            candidates.Add(new Candidate { Renderer = mr, Mesh = mesh, Materials = mats });
        }

        if (unreadable.Count > 0)
            Debug.LogWarning("[StaticMeshCombiner] Mallas no legibles (Read/Write apagado), se saltearon: " + string.Join(", ", unreadable));

        // ---- Agrupar por submesh
        var groups = new Dictionary<(Material, int, int, int, ShadowCastingMode, bool, LightProbeUsage, ReflectionProbeUsage, MotionVectorGenerationMode), Group>();
        var order = new List<Group>();

        foreach (Candidate c in candidates)
        {
            MeshRenderer mr = c.Renderer;
            Vector3 center = mr.bounds.center;
            int cx = Mathf.FloorToInt(center.x / cellSize);
            int cz = Mathf.FloorToInt(center.z / cellSize);
            Matrix4x4 m = mr.transform.localToWorldMatrix;

            for (int i = 0; i < c.Mesh.subMeshCount; i++)
            {
                var key = (c.Materials[i], cx, cz, mr.gameObject.layer, mr.shadowCastingMode, mr.receiveShadows,
                           mr.lightProbeUsage, mr.reflectionProbeUsage, mr.motionVectorGenerationMode);
                if (!groups.TryGetValue(key, out Group g))
                {
                    g = new Group
                    {
                        Material = c.Materials[i], CellX = cx, CellZ = cz, Layer = mr.gameObject.layer,
                        Shadows = mr.shadowCastingMode, Receive = mr.receiveShadows,
                        LightProbes = mr.lightProbeUsage, ReflectionProbes = mr.reflectionProbeUsage,
                        MotionVectors = mr.motionVectorGenerationMode
                    };
                    groups.Add(key, g);
                    order.Add(g);
                }
                g.Instances.Add(new CombineInstance { mesh = c.Mesh, subMeshIndex = i, transform = m });
                g.Owners.Add(c);
                if (c.Mesh.subMeshCount > 1) g.SingleSubmeshOnly = false;
                g.Vertices += c.Mesh.vertexCount;
                long tris = c.Mesh.GetIndexCount(i) / 3;
                g.Triangles += tris;
                trianglesBefore += tris;
            }
        }

        // Un grupo con un solo objeto de un solo submesh no gana nada: se deja el original.
        foreach (Group g in order)
            if (g.Instances.Count == 1 && g.SingleSubmeshOnly) g.Skip = true;

        // Renderers que se desactivan: los que tienen todos sus submeshes en grupos combinados.
        var toDisable = new HashSet<Candidate>();
        var blocked = new HashSet<Candidate>();
        foreach (Group g in order)
            foreach (Candidate c in g.Owners)
            {
                if (g.Skip) blocked.Add(c); else toDisable.Add(c);
            }
        toDisable.ExceptWith(blocked);

        int groupsToBuild = 0;
        foreach (Group g in order) if (!g.Skip) groupsToBuild++;
        if (groupsToBuild == 0)
            return "Nada para combinar bajo '" + root.name + "' (renderers revisados: " + renderersTotal + ").";

        // ---- Crear carpeta y contenedor
        EnsureFolder(outputFolder);

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Combinar mallas estáticas");

        GameObject container = new GameObject(CombinedContainerName);
        Undo.RegisterCreatedObjectUndo(container, "Combinar mallas estáticas");
        SceneManager.MoveGameObjectToScene(container, root.scene);
        if (parent != null)
        {
            if (IsIdentity(parent.localToWorldMatrix))
                container.transform.SetParent(parent, false);
            else
                Debug.LogWarning("[StaticMeshCombiner] El padre de root tiene transform no identidad; '" + CombinedContainerName + "' va en la raiz de la escena.");
        }
        container.transform.position = Vector3.zero;
        container.transform.rotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;

        int built = 0;
        long trianglesAfter = 0;
        int meshIndex = 0;

        try
        {
            foreach (Group g in order)
            {
                if (g.Skip) continue;
                EditorUtility.DisplayProgressBar("Combinando mallas", g.Material.name + " (" + g.Instances.Count + ")", (float)built / groupsToBuild);

                var mesh = new Mesh();
                mesh.name = "comb_" + meshIndex.ToString("000") + "_" + Sanitize(g.Material.name);
                mesh.indexFormat = g.Vertices > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.CombineMeshes(g.Instances.ToArray(), true, true, false);
                mesh.RecalculateBounds();

                string path = AssetDatabase.GenerateUniqueAssetPath(outputFolder + "/" + mesh.name + ".asset");
                AssetDatabase.CreateAsset(mesh, path);
                trianglesAfter += mesh.GetIndexCount(0) / 3;

                var go = new GameObject("Comb_" + Sanitize(g.Material.name) + "_" + g.CellX + "_" + g.CellZ);
                go.layer = g.Layer;
                go.transform.SetParent(container.transform, false); // padre identidad: la malla ya esta en mundo
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mrNew = go.AddComponent<MeshRenderer>();
                mrNew.sharedMaterial = g.Material;
                mrNew.shadowCastingMode = g.Shadows;
                mrNew.receiveShadows = g.Receive;
                mrNew.lightProbeUsage = g.LightProbes;
                mrNew.reflectionProbeUsage = g.ReflectionProbes;
                mrNew.motionVectorGenerationMode = g.MotionVectors;
                // Estatico pero SIN BatchingStatic: ya esta combinado.
                GameObjectUtility.SetStaticEditorFlags(go,
                    StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
                built++;
                meshIndex++;
            }

            // ---- Desactivar originales (con Undo) y guardar registro
            var registry = new StringBuilder();
            int n = 0;
            foreach (Candidate c in toDisable)
            {
                if (n++ % 200 == 0)
                    EditorUtility.DisplayProgressBar("Desactivando originales", n + " / " + toDisable.Count, (float)n / toDisable.Count);
                Undo.RecordObject(c.Renderer, "Combinar mallas estáticas");
                c.Renderer.enabled = false;
                registry.Append(PathFromRoot(c.Renderer.transform, root.transform)).Append('\t').AppendLine(c.Renderer.gameObject.name);
            }

            string fullRegistry = Path.Combine(Directory.GetParent(Application.dataPath).FullName, outputFolder, RegistryFileName);
            File.WriteAllText(fullRegistry, registry.ToString());
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(root.scene);

        string summary =
            "[StaticMeshCombiner] '" + root.name + "' (celda " + cellSize + " m)\n" +
            "  Renderers activos revisados: " + renderersTotal + "\n" +
            "  Excluidos por componentes extra (Collider/script/Animator/LOD...): " + excludedComponents + "\n" +
            "  Excluidos sin malla: " + excludedNoMesh + " | no legibles/material nulo/topologia: " + excludedInvalid + "\n" +
            "  Combinados (renderers desactivados): " + toDisable.Count + "\n" +
            "  Objetos nuevos: " + built + " (en '" + CombinedContainerName + "')\n" +
            "  Triangulos combinados: " + trianglesAfter + " (de " + trianglesBefore + " candidatos)\n" +
            "  Carpeta: " + outputFolder;
        return summary;
    }

    // ------------------------------------------------------------------ Revertir

    /// <summary>
    /// Reactiva los renderers originales, borra '__Combinadas' y las mallas guardadas.
    /// </summary>
    public static string Revert(GameObject root, string outputFolder)
    {
        if (root == null) return "ERROR: root es null.";
        if (EditorApplication.isPlaying) return "ERROR: no revertir en modo Play.";
        outputFolder = (outputFolder ?? DefaultFolder(root)).Replace('\\', '/').TrimEnd('/');

        int reenabled = 0, missing = 0;
        string fullRegistry = Path.Combine(Directory.GetParent(Application.dataPath).FullName, outputFolder, RegistryFileName);
        if (File.Exists(fullRegistry))
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Revertir mallas combinadas");
            foreach (string line in File.ReadAllLines(fullRegistry))
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('\t');
                Transform t = ResolvePath(parts[0], root.transform);
                if (t == null || (parts.Length > 1 && t.gameObject.name != parts[1])) { missing++; continue; }
                MeshRenderer mr = t.GetComponent<MeshRenderer>();
                if (mr == null) { missing++; continue; }
                Undo.RecordObject(mr, "Revertir mallas combinadas");
                mr.enabled = true;
                reenabled++;
            }
            Undo.CollapseUndoOperations(undoGroup);
        }
        else
        {
            Debug.LogWarning("[StaticMeshCombiner] No hay registro en '" + outputFolder + "'; no se reactivo ningun renderer.");
        }

        GameObject container = FindContainer(root);
        if (container != null) UnityEngine.Object.DestroyImmediate(container);

        int deleted = 0;
        if (AssetDatabase.IsValidFolder(outputFolder))
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { outputFolder }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(p).StartsWith("comb_") && AssetDatabase.DeleteAsset(p)) deleted++;
            }
            AssetDatabase.DeleteAsset(outputFolder + "/" + RegistryFileName);
            // Si la carpeta quedo vacia, se borra.
            if (AssetDatabase.FindAssets("", new[] { outputFolder }).Length == 0) AssetDatabase.DeleteAsset(outputFolder);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (root.scene.IsValid()) EditorSceneManager.MarkSceneDirty(root.scene);

        return "[StaticMeshCombiner] Revertido '" + root.name + "': " + reenabled + " renderers reactivados"
               + (missing > 0 ? " (" + missing + " no encontrados)" : "")
               + ", " + (container != null ? "'" + CombinedContainerName + "' borrado" : "sin contenedor")
               + ", " + deleted + " mallas borradas.";
    }

    // ------------------------------------------------------------------ Utilidades

    private static GameObject FindContainer(GameObject root)
    {
        Transform parent = root.transform.parent;
        if (parent != null)
        {
            Transform t = parent.Find(CombinedContainerName);
            if (t != null) return t.gameObject;
        }
        foreach (GameObject go in root.scene.GetRootGameObjects())
            if (go.name == CombinedContainerName) return go;
        return null;
    }

    private static bool IsUnderContainer(Transform t, Transform root)
    {
        for (; t != null; t = t.parent)
        {
            if (t.name == CombinedContainerName) return true;
            if (t == root) break;
        }
        return false;
    }

    /// <summary>True si el objeto o algun padre hasta root (sin incluir root) tiene componentes ademas de Transform/MeshFilter/MeshRenderer.</summary>
    private static bool HasExtraComponents(Transform t, Transform root, Dictionary<Transform, bool> cache)
    {
        for (; t != null && t != root; t = t.parent)
        {
            if (!cache.TryGetValue(t, out bool extra))
            {
                extra = false;
                foreach (Component c in t.GetComponents<Component>())
                {
                    if (c == null || !(c is Transform || c is MeshFilter || c is MeshRenderer)) { extra = true; break; }
                }
                cache[t] = extra;
            }
            if (extra) return true;
        }
        return false;
    }

    private static string PathFromRoot(Transform t, Transform root)
    {
        var parts = new List<int>();
        for (; t != null && t != root; t = t.parent) parts.Add(t.GetSiblingIndex());
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static Transform ResolvePath(string path, Transform root)
    {
        Transform t = root;
        if (string.IsNullOrEmpty(path)) return t;
        foreach (string s in path.Split('/'))
        {
            if (!int.TryParse(s, out int idx) || idx < 0 || idx >= t.childCount) return null;
            t = t.GetChild(idx);
        }
        return t;
    }

    private static bool IsIdentity(Matrix4x4 m)
    {
        Matrix4x4 id = Matrix4x4.identity;
        for (int i = 0; i < 16; i++)
            if (Mathf.Abs(m[i] - id[i]) > 1e-4f) return false;
        return true;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "sin_nombre";
        var sb = new StringBuilder(s.Length);
        foreach (char ch in s)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == ' ' ? ch : '_');
        return sb.ToString().Trim();
    }
}
