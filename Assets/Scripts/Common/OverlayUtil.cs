using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Utilidades compartidas por los overlays VR (FaintOverlay, ScreenBlink,
/// PhotosensitivityWarning): esfera/texto pegados a la camara con shaders que
/// ignoran la profundidad, camara de referencia y bloqueo de la mirada.
/// Un Canvas Screen Space Overlay no se ve en estereo (gotcha #7), por eso todo
/// esto son mallas 3D.
/// </summary>
public static class OverlayUtil
{
    public const int IgnoreRaycastLayer = 2;

    /// <summary>La camara que se esta usando: Camera.main o, si no hay, la primera de juego activa.</summary>
    public static Camera ResolveCamera()
    {
        Camera cam = Camera.main;
        if (cam != null && cam.isActiveAndEnabled)
        {
            return cam;
        }
        foreach (Camera c in Camera.allCameras)
        {
            if (c != null && c.cameraType == CameraType.Game)
            {
                return c;
            }
        }
        return null;
    }

    /// <summary>El Transform asignado en el Inspector o, si esta vacio, el de Camera.main (o null).</summary>
    public static Transform CameraOr(Transform assigned)
    {
        return assigned != null ? assigned : (Camera.main != null ? Camera.main.transform : null);
    }

    /// <summary>Sin sombras ni probes: para cualquier malla de overlay.</summary>
    public static void ConfigureRenderer(Renderer r)
    {
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    /// <summary>
    /// Esfera sin collider (si no, el raycast de la mirada pegaria contra ella)
    /// en Ignore Raycast. whiteVertexColors: los shaders de UI multiplican por
    /// el color de vertice y la esfera primitiva no trae. mesh = copia propia
    /// (hay que destruirla) o null.
    /// </summary>
    public static MeshRenderer CreateSphere(string name, Transform parent, Material mat, bool whiteVertexColors, out Mesh mesh)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = name;
        UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
        sphere.layer = IgnoreRaycastLayer;
        if (parent != null)
        {
            sphere.transform.SetParent(parent, false);
        }

        mesh = null;
        if (whiteVertexColors)
        {
            MeshFilter mf = sphere.GetComponent<MeshFilter>();
            mesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
            var white = new Color[mesh.vertexCount];
            Array.Fill(white, Color.white);
            mesh.colors = white;
            mf.sharedMesh = mesh;
        }

        MeshRenderer r = sphere.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        ConfigureRenderer(r);
        return r;
    }

    /// <summary>Centra la esfera en la camara con diametro holgado respecto del near clip.</summary>
    public static void PlaceSphere(Transform sphere, Camera cam)
    {
        sphere.position = cam.transform.position;
        float d = Mathf.Max(0.5f, cam.nearClipPlane * 4f);
        sphere.localScale = new Vector3(d, d, d);
    }

    /// <summary>
    /// Material de overlay con cadena de respaldo (Shader.Find puede dar null en
    /// el APK si ninguna escena referencia el shader): preferred -> UI/AlwaysOnTop
    /// -> UI/Default con ZTest Always (esta en Always Included Shaders) ->
    /// Hidden/Internal-Colored. Devuelve null (y loguea error) si no hay ninguno.
    /// </summary>
    public static Material CreateOverlayMaterial(Material preferred, string logTag, int renderQueue, bool warnIfNoPreferred = true)
    {
        Material m = BuildOverlayMaterial(preferred, logTag, warnIfNoPreferred);
        if (m != null)
        {
            m.renderQueue = renderQueue;
        }
        return m;
    }

    private static Material BuildOverlayMaterial(Material preferred, string tag, bool warnIfNoPreferred)
    {
        const string hint = " Asignar Mat_ReticleAlwaysOnTop como Overlay Material.";
        if (preferred != null && preferred.shader != null && preferred.shader.isSupported)
        {
            return new Material(preferred);
        }
        if (preferred != null)
        {
            Debug.LogWarning(tag + " El shader del Overlay Material ('" + preferred.name + "') no esta disponible en este dispositivo. Pruebo alternativas.");
        }

        Shader sh = Shader.Find("UI/AlwaysOnTop");
        if (sh != null && sh.isSupported)
        {
            if (preferred == null && warnIfNoPreferred)
            {
                Debug.LogWarning(tag + " Overlay Material vacio: uso Shader.Find(\"UI/AlwaysOnTop\"), que en el build de Android puede no estar incluido." + hint);
            }
            return new Material(sh);
        }

        // La ZTest de UI/Default sale de unity_GUIZTestMode: se fuerza a Always.
        sh = Shader.Find("UI/Default");
        if (sh != null && sh.isSupported)
        {
            Material m = new Material(sh);
            m.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
            Debug.LogWarning(tag + " No encontre UI/AlwaysOnTop (stripeado del build?). Uso UI/Default con ZTest Always." + hint);
            return m;
        }

        sh = Shader.Find("Hidden/Internal-Colored");
        if (sh != null && sh.isSupported)
        {
            Material m = new Material(sh);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_ZTest", (float)CompareFunction.Always);
            Debug.LogWarning(tag + " Uso Hidden/Internal-Colored para el fundido." + hint);
            return m;
        }

        Debug.LogError(tag + " No encontre NINGUN shader para el overlay: sin fundido." + hint);
        return null;
    }

    /// <summary>
    /// TextMesh con la copia del material de la fuente (shader GUI/Text, ZTest
    /// Always) para subirle la cola sin tocar el material compartido. El
    /// llamador suscribe Font.textureRebuilt con RefreshFontTexture.
    /// </summary>
    public static TextMesh CreateText(string name, Transform parent, Font font, int fontSize, float characterSize, int queue, out MeshRenderer renderer, out Material mat)
    {
        GameObject go = new GameObject(name);
        go.layer = IgnoreRaycastLayer;
        go.transform.SetParent(parent, false);
        renderer = go.AddComponent<MeshRenderer>();
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.fontSize = fontSize;
        tm.characterSize = characterSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.richText = true;
        mat = new Material(font.material) { renderQueue = queue };
        renderer.sharedMaterial = mat;
        ConfigureRenderer(renderer);
        return tm;
    }

    /// <summary>La textura de una fuente dinamica se puede regenerar: la copia del material tiene que seguirla.</summary>
    public static void RefreshFontTexture(Font changed, Font font, Material copy)
    {
        if (changed == font && copy != null && font != null && font.material != null)
        {
            copy.mainTexture = font.material.mainTexture;
        }
    }

    /// <summary>Deshabilita todos los GazeController activos y los anota en la lista.</summary>
    public static void DisableGaze(List<GazeController> blocked)
    {
        foreach (GazeController g in UnityEngine.Object.FindObjectsByType<GazeController>(FindObjectsSortMode.None))
        {
            if (g.enabled)
            {
                g.enabled = false;
                blocked.Add(g);
            }
        }
    }

    /// <summary>Rehabilita los GazeController de la lista y la vacia.</summary>
    public static void RestoreGaze(List<GazeController> blocked)
    {
        foreach (GazeController g in blocked)
        {
            if (g != null)
            {
                g.enabled = true;
            }
        }
        blocked.Clear();
    }
}
