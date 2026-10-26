using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Riggea automáticamente el monstruo (Moustruo1.gltf, una sola malla estática
/// "Object_0") para poder animarlo con <see cref="ProceduralWalk"/>.
///
/// Genera:
///  - Assets/Modelos 3D/Objetos 3D/Moustruo1_Rig.asset  (malla con pesos y bindposes)
///  - Assets/Prefabs/Monstruo_Rig.prefab                 (huesos + SkinnedMeshRenderer + ProceduralWalk)
///
/// Todas las posiciones de huesos salen de proporciones del modelo y de sus
/// vértices (centro de piernas, punta de brazos); las constantes de abajo se
/// pueden tocar y volver a correr el menú (sobrescribe los mismos assets, así
/// que las referencias existentes sobreviven).
/// </summary>
public static class MonsterAutoRig
{
    // ---------- rutas ----------
    private const string GltfPath = "Assets/Modelos 3D/Objetos 3D/Moustruo1.gltf";
    private const string MeshPath = "Assets/Modelos 3D/Objetos 3D/Moustruo1_Rig.asset";
    private const string PrefabFolder = "Assets/Prefabs";
    private const string PrefabPath = "Assets/Prefabs/Monstruo_Rig.prefab";

    // ---------- opciones ----------
    /// <summary>
    /// false: el frente del modelo es -Y del espacio de la malla (tras rotar -90 grados en X queda en +Z).
    /// true: el frente es +Y. Si el monstruo sale mirando para atrás, ponelo en true y corré de nuevo.
    /// </summary>
    public static bool FlipForward = false;

    /// <summary>Altura final del monstruo en metros (se hornea en los vértices).</summary>
    private const float TargetHeight = 2.5f;

    // ---------- proporciones (fracción de la altura H salvo que se aclare) ----------
    private const float HipsH = 0.50f;
    private const float SpineH = 0.62f;
    private const float ChestH = 0.74f;
    private const float NeckH = 0.86f;
    private const float HeadH = 0.92f;
    private const float KneeH = 0.25f;
    private const float AnkleH = 0.04f;
    private const float ToeH = 0.02f;
    private const float ToeForward = 0.08f;      // x H
    /// <summary>Vértices por debajo de esta altura se usan para hallar el centro de cada pierna.</summary>
    private const float LegZoneH = 0.45f;
    /// <summary>Para el centro de las piernas se ignoran vértices con |x| mayor a esta fracción del semiancho W (brazos caídos).</summary>
    private const float LegMaxXFrac = 0.5f;
    /// <summary>Posición X del hombro como fracción del semiancho W.</summary>
    private const float ShoulderXFrac = 0.15f;
    /// <summary>La altura del hombro sigue la punta del brazo pero se limita a este rango.</summary>
    private const float ShoulderMinH = 0.62f;
    private const float ShoulderMaxH = 0.80f;
    /// <summary>Puntos del brazo a lo largo hombro->punta.</summary>
    private const float ElbowT = 0.50f;
    private const float WristT = 0.85f;
    /// <summary>Por encima de esta altura (x H), vértices con |x| mayor que el hombro van solo a huesos de brazo.</summary>
    private const float ArmZoneMinH = 0.30f;
    /// <summary>Por debajo de esta altura (x H) los vértices van a cadera y pierna de su lado.</summary>
    private const float LegWeightMaxH = 0.50f;
    // Margen (fracción del semiancho) que se suma al centro de cada pierna para
    // decidir qué vértices bajos ya son del brazo y no de la cadera/muslo.
    private const float LegOuterMargin = 0.14f;

    private class BoneDef
    {
        public string name;
        public int parent;
        public Vector3 head;
        public Vector3 tail;
    }

    [MenuItem("Tools/Feria de Ciencias/Riggear monstruo (Moustruo1)")]
    public static void RigMenu()
    {
        try
        {
            GameObject prefab = BuildRig();
            if (prefab != null) Selection.activeObject = prefab;
        }
        catch (Exception e)
        {
            Debug.LogError("[MonsterAutoRig] Falló: " + e);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    private static bool _onlyUpdateMesh;

    [MenuItem("Tools/Feria de Ciencias/Riggear monstruo: solo actualizar malla")]
    public static void UpdateMeshOnlyMenu()
    {
        _onlyUpdateMesh = true;
        try
        {
            BuildRig();
        }
        catch (Exception e)
        {
            Debug.LogError("[MonsterAutoRig] Falló: " + e);
        }
        finally
        {
            _onlyUpdateMesh = false;
            EditorUtility.ClearProgressBar();
        }
    }

    [MenuItem("Tools/Feria de Ciencias/Riggear monstruo: probar en escena")]
    public static void TestInScene()
    {
        try
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = BuildRig();
            if (prefab == null) return;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Vector3 pos = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;
            go.transform.position = pos;
            Undo.RegisterCreatedObjectUndo(go, "Monstruo_Rig de prueba");
            Selection.activeGameObject = go;
            Debug.Log("[MonsterAutoRig] Monstruo_Rig instanciado en " + pos +
                      ". Para ver la caminata: Play y mover el objeto, o SetSpeedOverride desde un script.");
        }
        catch (Exception e)
        {
            Debug.LogError("[MonsterAutoRig] No se pudo instanciar: " + e);
        }
    }

    // =====================================================================
    private static GameObject BuildRig()
    {
        // ---- 1) cargar malla y material ----
        UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(GltfPath);
        if (all == null || all.Length == 0)
            throw new Exception("No se pudo cargar " + GltfPath + " (¿existe y está importado?).");

        Mesh src = null;
        Material mat = null;
        foreach (UnityEngine.Object o in all)
        {
            if (o is Mesh m && (src == null || m.name == "Object_0")) src = m;
            if (o is Material mt && mat == null) mat = mt;
        }
        if (src == null) throw new Exception("No hay ninguna Mesh dentro de " + GltfPath);
        if (mat == null) Debug.LogWarning("[MonsterAutoRig] No se encontró Material en el gltf; el prefab quedará sin material.");

        Vector3[] sv = src.vertices;
        Vector3[] sn = src.normals;
        Vector4[] st = src.tangents;
        Vector2[] suv = src.uv;
        int n = sv.Length;
        if (n == 0) throw new Exception("La malla no tiene vértices (¿no es legible?).");
        Debug.Log("[MonsterAutoRig] Malla '" + src.name + "': " + n + " vértices, bounds " + src.bounds);

        // ---- 2) espacio "upright" (Y arriba, Z adelante), escalado y con pies en el origen ----
        Vector3[] v = new Vector3[n];
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = -min;
        for (int i = 0; i < n; i++)
        {
            v[i] = ToUpright(sv[i]);
            min = Vector3.Min(min, v[i]);
            max = Vector3.Max(max, v[i]);
        }
        float rawH = max.y - min.y;
        if (rawH < 1e-4f) throw new Exception("Altura de la malla casi cero tras rotar; revisá el eje vertical.");
        if (rawH < (max.x - min.x) * 0.5f)
            Debug.LogWarning("[MonsterAutoRig] El modelo rotado es mucho más ancho que alto; verificá el eje vertical.");
        float scale = TargetHeight / rawH;
        Vector3 center = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
        for (int i = 0; i < n; i++) v[i] = (v[i] - center) * scale;

        float H = TargetHeight;
        float W = 0f;
        for (int i = 0; i < n; i++) W = Mathf.Max(W, Mathf.Abs(v[i].x));

        // ---- 3) datos del cuerpo: centro de piernas y punta de brazos ----
        float[] legX = new float[2];
        float[] legD = new float[2];
        int[] legCnt = new int[2];
        float torsoD = 0f; int torsoCnt = 0;
        Vector3[] tip = new Vector3[2];
        bool[] hasTip = new bool[2];
        for (int i = 0; i < n; i++)
        {
            Vector3 p = v[i];
            int side = p.x < 0f ? 0 : 1;
            if (p.y < LegZoneH * H && Mathf.Abs(p.x) < LegMaxXFrac * W)
            {
                legX[side] += p.x; legD[side] += p.z; legCnt[side]++;
            }
            if (p.y > 0.5f * H && p.y < 0.8f * H && Mathf.Abs(p.x) < 0.25f * W)
            {
                torsoD += p.z; torsoCnt++;
            }
            if (p.y > LegZoneH * H && (!hasTip[side] || Mathf.Abs(p.x) > Mathf.Abs(tip[side].x)))
            {
                tip[side] = p; hasTip[side] = true;
            }
        }
        torsoD = torsoCnt > 0 ? torsoD / torsoCnt : 0f;
        for (int s = 0; s < 2; s++)
        {
            float sign = s == 0 ? -1f : 1f;
            if (legCnt[s] > 0) { legX[s] /= legCnt[s]; legD[s] /= legCnt[s]; }
            else { legX[s] = sign * 0.12f * W; legD[s] = torsoD; }
            if (!hasTip[s]) tip[s] = new Vector3(sign * W, ChestH * H, torsoD);
        }

        // ---- 4) huesos ----
        var bones = new List<BoneDef>();
        var index = new Dictionary<string, int>();
        Func<string, string, Vector3, Vector3, int> add = (name, parent, head, tail) =>
        {
            index[name] = bones.Count;
            bones.Add(new BoneDef { name = name, parent = parent == null ? -1 : index[parent], head = head, tail = tail });
            return bones.Count - 1;
        };

        Vector3 hipsP = new Vector3(0, HipsH * H, torsoD);
        Vector3 spineP = new Vector3(0, SpineH * H, torsoD);
        Vector3 chestP = new Vector3(0, ChestH * H, torsoD);
        Vector3 neckP = new Vector3(0, NeckH * H, torsoD);
        Vector3 headP = new Vector3(0, HeadH * H, torsoD);
        Vector3 topP = new Vector3(0, H, torsoD);

        add("Hips", null, hipsP, spineP);
        add("Spine", "Hips", spineP, chestP);
        add("Chest", "Spine", chestP, neckP);
        add("Neck", "Chest", neckP, headP);
        add("Head", "Neck", headP, topP);

        string[] sfx = { "_L", "_R" };
        Vector3[] shoulderJ = new Vector3[2];
        for (int s = 0; s < 2; s++)
        {
            float sign = s == 0 ? -1f : 1f;
            float sh = Mathf.Clamp(tip[s].y, ShoulderMinH * H, ShoulderMaxH * H);
            Vector3 shoulderBase = new Vector3(0, sh, torsoD);
            Vector3 sj = new Vector3(sign * ShoulderXFrac * W, sh, Mathf.Lerp(torsoD, tip[s].z, 0.3f));
            Vector3 tp = new Vector3(tip[s].x, tip[s].y, Mathf.Lerp(torsoD, tip[s].z, 0.6f));
            Vector3 elbow = Vector3.Lerp(sj, tp, ElbowT);
            Vector3 wrist = Vector3.Lerp(sj, tp, WristT);
            shoulderJ[s] = sj;

            add("Shoulder" + sfx[s], "Chest", shoulderBase, sj);
            add("UpperArm" + sfx[s], "Shoulder" + sfx[s], sj, elbow);
            add("LowerArm" + sfx[s], "UpperArm" + sfx[s], elbow, wrist);
            add("Hand" + sfx[s], "LowerArm" + sfx[s], wrist, tp);

            Vector3 hip = new Vector3(legX[s], HipsH * H, legD[s]);
            Vector3 knee = new Vector3(legX[s], KneeH * H, legD[s]);
            Vector3 ankle = new Vector3(legX[s], AnkleH * H, legD[s]);
            Vector3 toe = new Vector3(legX[s], ToeH * H, legD[s] + ToeForward * H);
            add("Thigh" + sfx[s], "Hips", hip, knee);
            add("Shin" + sfx[s], "Thigh" + sfx[s], knee, ankle);
            add("Foot" + sfx[s], "Shin" + sfx[s], ankle, toe);
        }

        // ---- 5) pesos (máx. 2 influencias) ----
        int[][] armIdx = new int[2][];
        int[][] legIdx = new int[2][];
        for (int s = 0; s < 2; s++)
        {
            armIdx[s] = new[] { index["Shoulder" + sfx[s]], index["UpperArm" + sfx[s]], index["LowerArm" + sfx[s]], index["Hand" + sfx[s]] };
            legIdx[s] = new[] { index["Hips"], index["Thigh" + sfx[s]], index["Shin" + sfx[s]], index["Foot" + sfx[s]] };
        }
        int[] torsoIdx = { index["Hips"], index["Spine"], index["Chest"], index["Neck"], index["Head"], index["Shoulder_L"], index["Shoulder_R"] };

        BoneWeight[] weights = new BoneWeight[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 p = v[i];
            int side = p.x < 0f ? 0 : 1;
            int[] cand;
            // Zona de brazo: afuera del hombro y, por debajo del pecho, también
            // afuera de la pierna (si no, el costado de la cadera y del muslo
            // se va con el brazo y se estira una "lámina" al caminar).
            float legOuter = Mathf.Abs(legX[side]) + LegOuterMargin * W;
            bool armX = Mathf.Abs(p.x) > Mathf.Abs(shoulderJ[side].x);
            bool armZone = armX && p.y > ArmZoneMinH * H && (p.y > HipsH * H + 0.1f * H || Mathf.Abs(p.x) > legOuter);
            // La cabeza va rígida al hueso Head: si se mezcla con el cuello, los
            // espasmos mueven la malla menos que el hueso y la máscara (hija de
            // Head) queda corrida de la cara.
            if (!armZone && p.y >= (NeckH + HeadH) * 0.5f * H)
            {
                weights[i] = new BoneWeight { boneIndex0 = index["Head"], weight0 = 1f };
                continue;
            }
            if (armZone) cand = armIdx[side];
            else if (p.y < LegWeightMaxH * H) cand = legIdx[side];
            else cand = torsoIdx;
            weights[i] = ComputeWeight(p, cand, bones);
        }

        // ---- 6) malla nueva ----
        Mesh dst = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool existed = dst != null;
        if (existed) dst.Clear(); else dst = new Mesh();
        dst.name = "Moustruo1_Rig";
        dst.indexFormat = n > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        dst.vertices = v;
        if (sn != null && sn.Length == n)
        {
            Vector3[] nn = new Vector3[n];
            for (int i = 0; i < n; i++) nn[i] = ToUpright(sn[i]).normalized;
            dst.normals = nn;
        }
        if (st != null && st.Length == n)
        {
            Vector4[] tt = new Vector4[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 t3 = ToUpright(new Vector3(st[i].x, st[i].y, st[i].z)).normalized;
                tt[i] = new Vector4(t3.x, t3.y, t3.z, st[i].w);
            }
            dst.tangents = tt;
        }
        if (suv != null && suv.Length == n) dst.uv = suv;
        dst.subMeshCount = src.subMeshCount;
        for (int sm = 0; sm < src.subMeshCount; sm++) dst.SetTriangles(src.GetTriangles(sm), sm);
        if (dst.normals == null || dst.normals.Length != n) dst.RecalculateNormals();
        dst.boneWeights = weights;

        // ---- 7) jerarquía de huesos (temporal, para sacar bindposes y armar el prefab) ----
        var rootGO = new GameObject("Monstruo_Rig");
        try
        {
            var armature = new GameObject("Armature").transform;
            armature.SetParent(rootGO.transform, false);
            var bt = new Transform[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                var g = new GameObject(bones[i].name);
                bt[i] = g.transform;
                bt[i].SetParent(bones[i].parent < 0 ? armature : bt[bones[i].parent], false);
                bt[i].position = bones[i].head;
                bt[i].rotation = Quaternion.identity;
            }

            Matrix4x4 rootInv = rootGO.transform.localToWorldMatrix;
            Matrix4x4[] bind = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++) bind[i] = bt[i].worldToLocalMatrix * rootInv;
            dst.bindposes = bind;
            dst.RecalculateBounds();

            if (!existed) AssetDatabase.CreateAsset(dst, MeshPath);
            EditorUtility.SetDirty(dst);
            AssetDatabase.SaveAssets();

            // ---- 8) cuerpo con SkinnedMeshRenderer ----
            var body = new GameObject("Cuerpo");
            body.transform.SetParent(rootGO.transform, false);
            var smr = body.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = dst;
            if (mat != null) smr.sharedMaterial = mat;
            smr.bones = bt;
            smr.rootBone = bt[index["Hips"]];
            smr.updateWhenOffscreen = false;
            smr.quality = SkinQuality.Bone2;
            smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // localBounds se expresa relativo al rootBone (Hips), no a la raíz.
            Bounds lb = dst.bounds;
            lb.center -= bones[index["Hips"]].head;
            lb.Expand(new Vector3(0.6f, 0.4f, 0.6f)); // margen por el swing
            smr.localBounds = lb;

            // ---- 9) ProceduralWalk ----
            var walk = rootGO.AddComponent<ProceduralWalk>();
            walk.AutoFindBones();
            EditorUtility.SetDirty(walk);

            // ---- 10) prefab ----
            // Si el prefab ya existe y solo se pidió actualizar la malla, no se lo
            // vuelve a guardar: reemplazarlo puede cambiar los IDs de los huesos y
            // desenganchar lo que las escenas colgaron de ellos (la máscara de
            // Parte 5 es hija del hueso Head).
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (_onlyUpdateMesh && existing != null)
            {
                Debug.Log("[MonsterAutoRig] Malla actualizada (pesos/bindposes); el prefab no se tocó.");
                return existing;
            }
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets", "Prefabs");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rootGO, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[MonsterAutoRig] Listo. Altura " + H.ToString("0.00") + " m, semiancho " + W.ToString("0.00") +
                      " m, escala " + scale.ToString("0.000") + ", " + bones.Count + " huesos.\n" +
                      "Piernas x: L=" + legX[0].ToString("0.00") + " R=" + legX[1].ToString("0.00") +
                      " | puntas de brazo: L=" + tip[0] + " R=" + tip[1] + "\n" +
                      "Malla: " + MeshPath + "\nPrefab: " + PrefabPath + (FlipForward ? " (FlipForward activo)" : ""));
            return prefab;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rootGO);
        }
    }

    /// <summary>Espacio de malla del gltf (Z arriba, -Y frente) a upright (Y arriba, +Z frente). Es una rotación pura.</summary>
    private static Vector3 ToUpright(Vector3 m)
    {
        return FlipForward ? new Vector3(-m.x, m.z, m.y) : new Vector3(m.x, m.z, -m.y);
    }

    private static BoneWeight ComputeWeight(Vector3 p, int[] cand, List<BoneDef> bones)
    {
        int b0 = -1, b1 = -1;
        float d0 = float.MaxValue, d1 = float.MaxValue;
        foreach (int c in cand)
        {
            float d = SegDist(p, bones[c].head, bones[c].tail);
            if (d < d0) { b1 = b0; d1 = d0; b0 = c; d0 = d; }
            else if (d < d1) { b1 = c; d1 = d; }
        }
        var bw = new BoneWeight();
        const float eps = 1e-4f;
        float w0 = 1f / (d0 * d0 + eps);
        float w1 = b1 >= 0 ? 1f / (d1 * d1 + eps) : 0f;
        float sum = w0 + w1;
        bw.boneIndex0 = b0; bw.weight0 = w0 / sum;
        if (b1 >= 0 && w1 / sum > 0.02f)
        {
            bw.boneIndex1 = b1; bw.weight1 = w1 / sum;
            float t = bw.weight0 + bw.weight1;
            bw.weight0 /= t; bw.weight1 /= t;
        }
        else
        {
            bw.weight0 = 1f;
        }
        return bw;
    }

    private static float SegDist(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
        return (p - (a + ab * t)).magnitude;
    }
}
