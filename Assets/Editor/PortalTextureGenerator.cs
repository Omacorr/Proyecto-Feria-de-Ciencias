using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Genera por codigo (sin bajar nada) las texturas y materiales de los portales
/// nuevos (PortalVisual). Semillas fijas: regenerar da exactamente lo mismo.
///
/// Menu: Tools/Feria de Ciencias/Portales: generar texturas y materiales
///  - PNG en Assets/Texturas/Portales/ (alfa, mipmaps, clamp, Android ASTC 6x6
///    maximo 512).
///  - Materiales en Assets/Materials/Portales/ (Custom/PortalMark por estilo +
///    Portal_Polvo, copia de Polvo_Linterna.mat con la textura de polvo).
/// Si los materiales ya existen se actualizan en el lugar (conservan el GUID,
/// asi las escenas no pierden la referencia).
/// </summary>
public static class PortalTextureGenerator
{
    public const string TexFolder = "Assets/Texturas/Portales";
    public const string MatFolder = "Assets/Materials/Portales";
    private const string DustTemplate = "Assets/Materials/Polvo_Linterna.mat";

    // Nombres de material (los usa PortalSetupTool).
    public const string MatSimple = "Portal_Simple";
    public const string MatDescalzas = "Portal_HuellasDescalzas";
    public const string MatVelitaBrillo = "Portal_Velita_Brillo";
    public const string MatVelitaConfeti = "Portal_Velita_Confeti";
    public const string MatVelitaVela = "Portal_Velita_Vela";
    public const string MatVelitaLlama = "Portal_Velita_Llama";
    public const string MatPolillasMancha = "Portal_Polillas_Mancha";
    public const string MatPolilla = "Portal_Polillas_Polilla";
    public const string MatAtrasRayones = "Portal_HuellasAtras_Rayones";
    public const string MatAtrasHuellas = "Portal_HuellasAtras_Huellas";
    public const string MatHumedad = "Portal_ManchaHumedad";
    public const string MatPolvo = "Portal_Polvo";

    [MenuItem("Tools/Feria de Ciencias/Portales: generar texturas y materiales")]
    public static void Generate()
    {
        Directory.CreateDirectory(TexFolder);
        Directory.CreateDirectory(MatFolder);

        try
        {
            EditorUtility.DisplayProgressBar("Portales", "Generando texturas...", 0.1f);
            Texture2D huellas = SaveTex("portal_huellas", 256, Huellas);
            Texture2D rayones = SaveTex("portal_rayones", 256, Rayones);
            Texture2D mancha = SaveTex("portal_mancha", 256, Mancha);
            Texture2D anillo = SaveTex("portal_anillo", 256, Anillo);
            Texture2D confeti = SaveConfeti("portal_confeti", 256);
            Texture2D polilla = SaveTex("portal_polilla", 128, Polilla);
            Texture2D llama = SaveTex("portal_llama", 128, Llama);
            Texture2D polvo = SaveTex("portal_polvo", 64, Polvo);

            EditorUtility.DisplayProgressBar("Portales", "Creando materiales...", 0.8f);
            Shader sh = Shader.Find("Custom/PortalMark"); // Editor: Shader.Find es seguro aca.
            if (sh == null)
            {
                Debug.LogError("[PortalTextureGenerator] No encuentro el shader Custom/PortalMark (Assets/Shaders/PortalMark.shader). ¿Compiló?");
                return;
            }

            MarkMat(MatSimple, sh, anillo, new Color(0.9f, 0.8f, 0.6f) * 0.8f, true);
            MarkMat(MatDescalzas, sh, huellas, new Color(0.62f, 0.72f, 0.82f) * 0.65f, true);
            MarkMat(MatVelitaBrillo, sh, polvo, new Color(1f, 0.6f, 0.25f) * 0.55f, true);
            MarkMat(MatVelitaConfeti, sh, confeti, new Color(1f, 1f, 1f, 0.9f), false);
            Material vela = MarkMat(MatVelitaVela, sh, null, new Color(0.95f, 0.72f, 0.78f, 1f), false);
            vela.SetFloat("_Cull", 2f);
            vela.SetFloat("_ZWrite", 1f);
            MarkMat(MatVelitaLlama, sh, llama, new Color(1f, 0.85f, 0.6f) * 1.2f, true);
            MarkMat(MatPolillasMancha, sh, mancha, new Color(0.6f, 0.45f, 0.28f) * 0.6f, true);
            MarkMat(MatPolilla, sh, polilla, new Color(0.8f, 0.75f, 0.66f, 1f), false);
            MarkMat(MatAtrasRayones, sh, rayones, new Color(0.7f, 0.55f, 0.35f) * 0.55f, true);
            MarkMat(MatAtrasHuellas, sh, huellas, new Color(0.9f, 0.6f, 0.3f) * 0.6f, true);
            Material hum = MarkMat(MatHumedad, sh, mancha, new Color(0.35f, 0.7f, 0.85f) * 0.7f, true);
            hum.SetFloat("_FogAmount", 1f); // Parte 5 tiene niebla lineal 5-22 m.

            DustMat(polvo);

            AssetDatabase.SaveAssets();
            Debug.Log("[PortalTextureGenerator] Listo: 8 texturas en " + TexFolder + " y 12 materiales en " + MatFolder + ".");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ---------------- Materiales ----------------

    private static Material MarkMat(string name, Shader sh, Texture2D tex, Color color, bool additive)
    {
        string path = MatFolder + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (isNew) m = new Material(sh);
        m.shader = sh;
        m.SetTexture("_MainTex", tex);
        m.SetColor("_Color", color);
        m.SetFloat("_Intensity", 1f);
        m.SetFloat("_Alpha", 1f);
        m.SetFloat("_FogAmount", 0f);
        m.SetFloat("_FogToBlack", additive ? 1f : 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.renderQueue = 3000;
        if (isNew) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
        return m;
    }

    private static void DustMat(Texture2D polvo)
    {
        string path = MatFolder + "/" + MatPolvo + ".mat";
        Material template = AssetDatabase.LoadAssetAtPath<Material>(DustTemplate);
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = m == null;
        if (template != null)
        {
            if (isNew) m = new Material(template);
            else m.CopyPropertiesFromMaterial(template);
            if (m.shader != template.shader) m.shader = template.shader;
        }
        else
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null)
            {
                Debug.LogError("[PortalTextureGenerator] No hay Polvo_Linterna.mat ni Particles/Unlit: el polvo queda sin material.");
                return;
            }
            if (isNew) m = new Material(sh);
            // Transparente con alfa (mismo setup que Polvo_Linterna).
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
        }
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", polvo);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (isNew) AssetDatabase.CreateAsset(m, path);
        else EditorUtility.SetDirty(m);
    }

    // ---------------- Escritura e importacion ----------------

    private delegate Color PixelFn(float u, float v);

    private static Texture2D SaveTex(string name, int size, PixelFn fn)
    {
        Color32[] px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                px[y * size + x] = fn(u, v);
            }
        }
        return Write(name, size, px);
    }

    private static Texture2D Write(string name, int size, Color32[] px)
    {
        Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
        t.SetPixels32(px);
        t.Apply();
        string path = TexFolder + "/" + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.maxTextureSize = 512;
        imp.SetPlatformTextureSettings(new TextureImporterPlatformSettings
        {
            name = "Android",
            overridden = true,
            maxTextureSize = 512,
            format = TextureImporterFormat.ASTC_6x6,
            compressionQuality = 50
        });
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------- Ruido ----------------

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    private static float ValueNoise(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
        float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    private static float Fbm(float x, float y, int seed)
    {
        float sum = 0f, amp = 0.5f;
        for (int o = 0; o < 4; o++)
        {
            sum += amp * ValueNoise(x, y, seed + o * 17);
            x *= 2.03f; y *= 2.03f; amp *= 0.5f;
        }
        return sum; // ~0..1
    }

    /// <summary>Cobertura suave de una elipse (1 adentro, 0 afuera, borde de "soft").</summary>
    private static float Ellipse(float u, float v, float cx, float cy, float rx, float ry, float rotDeg, float soft = 0.25f)
    {
        float r = rotDeg * Mathf.Deg2Rad;
        float dx = u - cx, dy = v - cy;
        float x = dx * Mathf.Cos(r) + dy * Mathf.Sin(r);
        float y = -dx * Mathf.Sin(r) + dy * Mathf.Cos(r);
        float e = Mathf.Sqrt((x / rx) * (x / rx) + (y / ry) * (y / ry));
        return 1f - Mathf.SmoothStep(0f, 1f, (e - (1f - soft)) / soft);
    }

    private static Color White(float a) => new Color(1f, 1f, 1f, Mathf.Clamp01(a));

    // ---------------- Dibujos ----------------

    // Par de huellas descalzas: izquierda atras, derecha adelante; la punta
    // (dedos) hacia arriba en la textura (+V = +Z del quad).
    private static Color Huellas(float u, float v)
    {
        float a = Mathf.Max(Foot(u, v, 0.37f, 0.40f, -6f, true), Foot(u, v, 0.63f, 0.60f, 6f, false));
        float grain = 0.65f + 0.35f * Fbm(u * 40f, v * 40f, 11);
        return White(a * grain);
    }

    private static readonly float[] ToeX = { 0.045f, 0.018f, -0.008f, -0.03f, -0.05f };
    private static readonly float[] ToeY = { 0.36f, 0.385f, 0.375f, 0.35f, 0.315f };
    private static readonly float[] ToeR = { 0.026f, 0.017f, 0.015f, 0.013f, 0.012f };

    private static float Foot(float u, float v, float cx, float cy, float rotDeg, bool left)
    {
        // Pasar a coordenadas locales del pie (y hacia los dedos).
        float r = rotDeg * Mathf.Deg2Rad;
        float dx = u - cx, dy = v - cy;
        float x = dx * Mathf.Cos(r) + dy * Mathf.Sin(r);
        float y = -dx * Mathf.Sin(r) + dy * Mathf.Cos(r);
        float s = left ? 1f : -1f; // lado interno (dedo gordo) hacia el centro del par
        const float L = 0.40f;
        float c = 0f;
        c = Mathf.Max(c, Ellipse(x, y, 0f, 0.08f * L, 0.075f, 0.12f, 0f, 0.35f));            // planta (bola)
        c = Mathf.Max(c, Ellipse(x, y, -0.02f * s, -0.28f * L, 0.05f, 0.075f, 0f, 0.35f));   // talon
        c = Mathf.Max(c, Ellipse(x, y, -0.035f * s, -0.12f * L, 0.03f, 0.08f, 0f, 0.4f));    // arco (lado externo)
        // Dedos: del gordo (interno) al chico (externo).
        for (int i = 0; i < 5; i++)
        {
            c = Mathf.Max(c, Ellipse(x, y, ToeX[i] * s, ToeY[i] * L + 0.03f, ToeR[i], ToeR[i] * 1.15f, 0f, 0.4f));
        }
        return c;
    }

    private static float[] s_scratch; // segmentos: cx, cy, ang, len, ancho, intensidad

    private static Color Rayones(float u, float v)
    {
        if (s_scratch == null)
        {
            System.Random rnd = new System.Random(23);
            s_scratch = new float[16 * 6];
            for (int i = 0; i < 16; i++)
            {
                double ang = rnd.NextDouble() * Math.PI * 2, rr = Math.Sqrt(rnd.NextDouble()) * 0.3;
                s_scratch[i * 6 + 0] = 0.5f + (float)(Math.Cos(ang) * rr);
                s_scratch[i * 6 + 1] = 0.5f + (float)(Math.Sin(ang) * rr);
                s_scratch[i * 6 + 2] = (float)(rnd.NextDouble() * Math.PI);
                s_scratch[i * 6 + 3] = 0.08f + (float)rnd.NextDouble() * 0.22f;
                s_scratch[i * 6 + 4] = 0.003f + (float)rnd.NextDouble() * 0.006f;
                s_scratch[i * 6 + 5] = 0.4f + (float)rnd.NextDouble() * 0.6f;
            }
        }
        float a = 0f;
        for (int i = 0; i < 16; i++)
        {
            float cx = s_scratch[i * 6], cy = s_scratch[i * 6 + 1], ang = s_scratch[i * 6 + 2];
            float len = s_scratch[i * 6 + 3], w = s_scratch[i * 6 + 4], k = s_scratch[i * 6 + 5];
            float dx = u - cx, dy = v - cy;
            float along = dx * Mathf.Cos(ang) + dy * Mathf.Sin(ang);
            float across = -dx * Mathf.Sin(ang) + dy * Mathf.Cos(ang);
            across += (ValueNoise(along * 60f, i, 5) - 0.5f) * 0.004f; // trazo tembloroso
            float t = Mathf.Abs(along) / (len * 0.5f);
            if (t > 1f) continue;
            float taper = 1f - t * t;
            float line = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Abs(across) / (w * taper + 0.0005f));
            a = Mathf.Max(a, line * k * taper);
        }
        float radial = 1f - Mathf.SmoothStep(0.35f, 0.5f, Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)));
        return White(a * radial);
    }

    // Mancha organica de humedad: borde irregular con "linea de marea" mas fuerte.
    private static Color Mancha(float u, float v)
    {
        float dx = u - 0.5f, dy = v - 0.5f;
        float ang = Mathf.Atan2(dy, dx);
        float rr = 0.34f * (0.82f + 0.36f * Fbm(Mathf.Cos(ang) * 1.6f + 3f, Mathf.Sin(ang) * 1.6f + 3f, 31));
        float d = Mathf.Sqrt(dx * dx + dy * dy) / rr;
        float inside = 1f - Mathf.SmoothStep(0f, 1f, (d - 0.92f) / 0.12f);
        float tide = 0.35f + 0.65f * Mathf.SmoothStep(0f, 1f, (d - 0.55f) / 0.4f);
        float vari = 0.7f + 0.3f * Fbm(u * 9f, v * 9f, 37);
        return White(inside * tide * vari);
    }

    private static Color Anillo(float u, float v)
    {
        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
        float ring = Mathf.Exp(-Mathf.Pow((d - 0.72f) / 0.08f, 2f));
        float glow = 0.3f * Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
        float edge = 1f - Mathf.SmoothStep(0.9f, 1f, d);
        return White(Mathf.Max(ring, glow) * edge);
    }

    private static Texture2D SaveConfeti(string name, int size)
    {
        Color32[] px = new Color32[size * size];
        Color[] palette =
        {
            new Color(0.95f, 0.35f, 0.4f), new Color(0.98f, 0.85f, 0.3f), new Color(0.35f, 0.6f, 0.95f),
            new Color(0.4f, 0.85f, 0.5f), new Color(0.95f, 0.55f, 0.85f), new Color(0.95f, 0.95f, 0.9f)
        };
        System.Random rnd = new System.Random(57);
        for (int n = 0; n < 80; n++)
        {
            double ang = rnd.NextDouble() * Math.PI * 2, rr = Math.Pow(rnd.NextDouble(), 0.7) * 0.44;
            float cx = 0.5f + (float)(Math.Cos(ang) * rr), cy = 0.5f + (float)(Math.Sin(ang) * rr);
            float rot = (float)(rnd.NextDouble() * Math.PI);
            float hw = 0.009f + (float)rnd.NextDouble() * 0.006f, hh = 0.004f + (float)rnd.NextDouble() * 0.003f;
            Color col = palette[rnd.Next(palette.Length)] * (0.75f + 0.25f * (float)rnd.NextDouble());
            int x0 = Mathf.Max(0, (int)((cx - 0.03f) * size)), x1 = Mathf.Min(size - 1, (int)((cx + 0.03f) * size));
            int y0 = Mathf.Max(0, (int)((cy - 0.03f) * size)), y1 = Mathf.Min(size - 1, (int)((cy + 0.03f) * size));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f) / size - cx, dy = (y + 0.5f) / size - cy;
                    float lx = Mathf.Abs(dx * Mathf.Cos(rot) + dy * Mathf.Sin(rot));
                    float ly = Mathf.Abs(-dx * Mathf.Sin(rot) + dy * Mathf.Cos(rot));
                    float px1 = 1.5f / size;
                    float a = (1f - Mathf.Clamp01((lx - hw) / px1)) * (1f - Mathf.Clamp01((ly - hh) / px1));
                    if (a <= 0f) continue;
                    Color32 prev = px[y * size + x];
                    if (a * 255f > prev.a)
                    {
                        Color c = col;
                        c.a = a;
                        px[y * size + x] = c;
                    }
                }
            }
        }
        return Write(name, size, px);
    }

    // Polilla vista desde arriba, cabeza hacia +V.
    private static Color Polilla(float u, float v)
    {
        float body = Mathf.Max(Ellipse(u, v, 0.5f, 0.5f, 0.045f, 0.2f, 0f, 0.3f), Ellipse(u, v, 0.5f, 0.71f, 0.035f, 0.035f, 0f, 0.3f));
        float wings = 0f;
        wings = Mathf.Max(wings, Ellipse(u, v, 0.33f, 0.58f, 0.19f, 0.11f, -28f, 0.2f));
        wings = Mathf.Max(wings, Ellipse(u, v, 0.67f, 0.58f, 0.19f, 0.11f, 28f, 0.2f));
        wings = Mathf.Max(wings, Ellipse(u, v, 0.37f, 0.4f, 0.13f, 0.09f, 20f, 0.2f));
        wings = Mathf.Max(wings, Ellipse(u, v, 0.63f, 0.4f, 0.13f, 0.09f, -20f, 0.2f));
        float mottled = 0.6f + 0.4f * Fbm(u * 14f, v * 14f, 71);
        Color wingCol = new Color(0.62f, 0.56f, 0.46f) * mottled;
        Color bodyCol = new Color(0.32f, 0.28f, 0.23f);
        Color c = Color.Lerp(wingCol, bodyCol, body);
        c.a = Mathf.Clamp01(Mathf.Max(wings * 0.92f, body));
        return c;
    }

    // Llama en gota: base abajo, punta arriba; centro casi blanco, borde naranja.
    private static Color Llama(float u, float v)
    {
        float t = (v - 0.06f) / 0.9f;
        if (t <= 0f || t >= 1f) return new Color(1f, 0.5f, 0.1f, 0f);
        float hw = 0.2f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.55f)) * (1f - 0.25f * t) + 0.002f;
        float d = Mathf.Abs(u - 0.5f) / hw;
        float core = 1f - Mathf.SmoothStep(0f, 1f, d);
        Color inner = new Color(1f, 0.96f, 0.78f), outer = new Color(1f, 0.45f, 0.08f);
        Color c = Color.Lerp(inner, outer, Mathf.Clamp01(d * 0.9f + t * 0.5f));
        c.a = core * Mathf.SmoothStep(0f, 1f, t / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, (t - 0.8f) / 0.2f));
        return c;
    }

    private static Color Polvo(float u, float v)
    {
        float dx = u - 0.5f, dy = v - 0.5f;
        float r2 = (dx * dx + dy * dy) * 4f;
        return White(Mathf.Exp(-r2 * 4f) * (1f - Mathf.SmoothStep(0.8f, 1f, Mathf.Sqrt(r2))));
    }
}
