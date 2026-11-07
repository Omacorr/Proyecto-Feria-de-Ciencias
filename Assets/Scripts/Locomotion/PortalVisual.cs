using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Visual nuevo de un punto de teletransporte (va en el MISMO GameObject que el
/// TeleportPoint). Reemplaza al orbe/cubo viejo, que queda en la escena pero
/// apagado (TeleportPoint.HasExternalVisual = true).
///
/// Arma todo por codigo en Awake, como hijo "__PortalVisual" (layer Ignore
/// Raycast, sin colliders, asi la mirada sigue pegando en el Collider del
/// TeleportPoint):
///  - una marca en el piso (quad con el shader Custom/PortalMark);
///  - una columna de polvo (ParticleSystem de ~15 particulas, unlit);
///  - un AudioSource 3D que suena UNA vez por mirada (#30);
///  - y el decorado propio del estilo (velita, confeti, polillas...).
///
/// Comportamiento:
///  - Al mirar, la marca sube de brillo con el progreso y el polvo se arremolina.
///  - Al llegar, un soplo de polvo (la velita se apaga).
///  - Fundido por distancia (#29): a pleno hasta Visible Distance, baja suave
///    hasta Visible Distance + Fade Range. Los cercanos nunca se apagan.
///  - Modo sutil (#28, GameSettings.PortalsVisible = false): la marca baja a
///    ~35 % y el polvo a la mitad. Escucha FeriaEvents.PortalVisibilityChanged.
///  - SetHighlighted(true): pulso lento (&lt; 1 Hz) para que las pistas
///    directas marquen el camino. Nada de destellos rapidos (fotosensibilidad).
///  - Si TeleportManager oculta el punto, se oculta todo (con un fundido corto).
///
/// Materiales: siempre asignados por Inspector (los arma
/// "Tools/Feria de Ciencias/Portales: generar texturas y materiales"). Nunca
/// se usa Shader.Find (gotcha #25). Solo se ve en Play: en modo edicion no hay
/// nada armado.
/// </summary>
[RequireComponent(typeof(TeleportPoint))]
[DisallowMultipleComponent]
public class PortalVisual : MonoBehaviour
{
    /// <summary>Nombre del hijo que arma este script (TeleportPoint y StaticMeshCombiner lo ignoran).</summary>
    public const string RootName = "__PortalVisual";

    public enum PortalStyle
    {
        Simple,            // Tutorial (#22): anillo suave.
        HuellasDescalzas,  // Parte 1 (#23): par de huellas que se alejan del jugador.
        Velita,            // Parte 2 (#24): velita de cumpleanos + confeti.
        Polillas,          // Parte 3 (#25): polillas sobre una mancha.
        HuellasAtras,      // Parte 4 (#26): rayones; con SetBackwardMode, huellas que miran hacia el jugador.
        ManchaHumedad      // Parte 5 (#27): mancha de humedad celeste.
    }

    [Header("Estilo")]
    [SerializeField] private PortalStyle _style = PortalStyle.Simple;

    [Header("Materiales (Custom/PortalMark salvo el polvo)")]
    [Tooltip("Marca del piso. En HuellasAtras es la marca neutra (rayones) de antes de SetBackwardMode.")]
    [SerializeField] private Material _markMaterial;
    [Tooltip("Solo HuellasAtras: huellas que se usan despues de SetBackwardMode.")]
    [SerializeField] private Material _backwardMarkMaterial;
    [Tooltip("Solo Velita: confeti en el piso (alfa).")]
    [SerializeField] private Material _confettiMaterial;
    [Tooltip("Solo Velita: el cilindro de la vela (alfa, Cull Back, ZWrite On).")]
    [SerializeField] private Material _candleMaterial;
    [Tooltip("Solo Velita: la llama (aditivo).")]
    [SerializeField] private Material _flameMaterial;
    [Tooltip("Solo Polillas: cada polilla (alfa).")]
    [SerializeField] private Material _mothMaterial;
    [Tooltip("Particulas de polvo (URP Particles/Unlit). Sin esto no hay polvo.")]
    [SerializeField] private Material _dustMaterial;

    [Header("Ubicacion")]
    [Tooltip("Altura del piso respecto del pivote del TeleportPoint, en metros de mundo (negativa si el pivote esta arriba del piso, por ejemplo el orbe de Parte 4 a 1.2 m). La calcula PortalSetupTool.")]
    [SerializeField] private float _floorOffset;
    [Tooltip("Tildado: en Awake busca el piso con un raycast hacia abajo (contra todo salvo Teleport/Interactive). Los pisos de los .glb no tienen Collider (gotcha #1), asi que casi siempre conviene dejarlo apagado y usar Floor Offset.")]
    [SerializeField] private bool _raycastFloorAtRuntime;
    [Tooltip("Lado de la marca del piso, en metros.")]
    [SerializeField, Min(0.1f)] private float _markSize = 0.9f;
    [Tooltip("Giro fijo de la marca (grados). Las huellas que miran al jugador lo ignoran.")]
    [SerializeField] private float _markYaw;

    [Header("Fundido por distancia (#29)")]
    [Tooltip("Hasta esta distancia (horizontal, a la camara) se ve a pleno. Nunca menor que la distancia al vecino mas lejano que se ve.")]
    [SerializeField, Min(1f)] private float _visibleDistance = 12f;
    [Tooltip("Metros en los que baja de pleno a nada despues de Visible Distance.")]
    [SerializeField, Min(0.5f)] private float _fadeRange = 6f;

    [Header("Brillo")]
    [SerializeField, Min(0f)] private float _baseIntensity = 1f;
    [Tooltip("Cuanto sube el brillo con la mirada completa (1.5 = x2.5).")]
    [SerializeField, Min(0f)] private float _gazeBoost = 1.5f;
    [Tooltip("Factor del modo sutil (GameSettings.PortalsVisible = false).")]
    [SerializeField, Range(0f, 1f)] private float _subtleFactor = 0.35f;
    [Tooltip("Cuanto sube el brillo en el pico del pulso de SetHighlighted.")]
    [SerializeField, Min(0f)] private float _highlightBoost = 0.9f;
    [Tooltip("Frecuencia del pulso de SetHighlighted. Menos de 1 Hz (fotosensibilidad).")]
    [SerializeField, Range(0.1f, 0.9f)] private float _highlightHz = 0.5f;

    [Header("Polvo")]
    [SerializeField] private bool _dust = true;
    [SerializeField, Range(4, 40)] private int _dustCount = 15;
    [SerializeField] private Color _dustColor = new Color(0.85f, 0.8f, 0.7f, 0.55f);
    [SerializeField, Min(0.05f)] private float _dustRadius = 0.35f;
    [Tooltip("Velocidad orbital normal (rad/s).")]
    [SerializeField] private float _dustOrbit = 0.35f;
    [Tooltip("Velocidad orbital extra con la mirada completa (rad/s).")]
    [SerializeField] private float _dustSwirl = 2.2f;
    [Tooltip("Particulas del soplo al llegar.")]
    [SerializeField, Range(0, 30)] private int _arrivalBurst = 10;

    [Header("Sonido al mirar (#30)")]
    [Tooltip("Uno al azar, una sola vez por mirada (hasta dejar de mirar y volver). Vacio = sin sonido.")]
    [SerializeField] private AudioClip[] _gazeClips;
    [SerializeField, Range(0f, 1f)] private float _volume = 0.3f;
    [SerializeField, Min(0.1f)] private float _minDistance = 1f;
    [SerializeField, Min(1f)] private float _maxDistance = 8f;

    // ---------------- Estado estatico ----------------

    private static bool s_backward;
    private static Mesh s_quad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_backward = false;
        // Cada vez que se entra a Parte 4 el pasillo arranca "neutro".
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private static void OnActiveSceneChanged(Scene from, Scene to)
    {
        s_backward = false;
    }

    /// <summary>
    /// Parte 4: todas las marcas HuellasAtras pasan a huellas que miran hacia
    /// el jugador (las inactivas lo toman al prenderse). Es estatico: para
    /// cablearlo desde un UnityEvent usar el metodo de instancia SetBackwardMode
    /// de cualquier PortalVisual (por ejemplo el de tp1).
    /// </summary>
    public static void SetBackwardModeAll()
    {
        s_backward = true;
    }

    // ---------------- API de instancia ----------------

    /// <summary>Pulso lento de brillo para marcar el camino (pistas directas). Ignora el modo sutil.</summary>
    public void SetHighlighted(bool on)
    {
        _highlighted = on;
    }

    /// <summary>Para UnityEvent: activa el modo "hacia atras" en TODOS los portales (ver SetBackwardModeAll).</summary>
    public void SetBackwardMode()
    {
        SetBackwardModeAll();
    }

    // ---------------- Estado ----------------

    private static readonly int IdIntensity = Shader.PropertyToID("_Intensity");
    private static readonly int IdAlpha = Shader.PropertyToID("_Alpha");
    private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");

    private TeleportPoint _point;
    private Transform _root;
    private Transform _cam;
    private MaterialPropertyBlock _mpb;

    private MeshRenderer _mark;
    private Transform _markT;
    private MeshRenderer _confetti;
    private MeshRenderer _candle;
    private Transform _flamePivot;
    private MeshRenderer _flame;
    private class Moth
    {
        public Transform T;
        public MeshRenderer Renderer;
        public float Angle, Radius, Height, Speed, Phase;
    }
    private Moth[] _moths;

    private ParticleSystem _ps;
    private ParticleSystemRenderer _psRenderer;
    private Color _dustBaseColor = Color.white;
    private float _appliedOrbit = -1f;
    private float _appliedRate = -1f;
    private bool _appliedEmission = true;

    private AudioSource _audio;

    private bool _tpVisible = true;
    private bool _portalsVisible = true;
    private bool _highlighted;
    private bool _gazed;
    private bool _soundPlayedThisGaze;
    private float _progress;
    private float _progressSmooth;
    private float _vis = 1f;
    private float _puff;
    private bool _renderersOn = true;
    private float _flameK = 1f;
    private bool _flameLit = true;
    private bool _backwardApplied;
    private float _swapK = 1f;
    private float _currentYaw;
    private float _highlightTime;

    // ---------------- Ciclo de vida ----------------

    private void Awake()
    {
        _point = GetComponent<TeleportPoint>();
        _mpb = new MaterialPropertyBlock();
        _tpVisible = _point.IsVisible;
        _vis = _tpVisible ? 1f : 0f;
        _currentYaw = _markYaw;
        Build();

        // Si el modo "hacia atras" ya estaba activo (grupo que se prende tarde en
        // Parte 4), arranca directo con las huellas.
        if (_style == PortalStyle.HuellasAtras && s_backward && _mark != null && _backwardMarkMaterial != null)
        {
            _backwardApplied = true;
            _mark.sharedMaterial = _backwardMarkMaterial;
        }
    }

    private void OnEnable()
    {
        _point.GazeEntered += OnGazeEntered;
        _point.GazeExited += OnGazeExited;
        _point.GazeProgress += OnGazeProgress;
        _point.Arrived += OnArrived;
        _point.VisibilityChanged += OnVisibilityChanged;
        FeriaEvents.PortalVisibilityChanged += OnPortalVisibilityChanged;

        _portalsVisible = GameSettings.Instance != null ? GameSettings.Instance.PortalsVisible : true;
        _tpVisible = _point.IsVisible;
    }

    private void OnDisable()
    {
        _point.GazeEntered -= OnGazeEntered;
        _point.GazeExited -= OnGazeExited;
        _point.GazeProgress -= OnGazeProgress;
        _point.Arrived -= OnArrived;
        _point.VisibilityChanged -= OnVisibilityChanged;
        FeriaEvents.PortalVisibilityChanged -= OnPortalVisibilityChanged;

        _gazed = false;
        _progress = 0f;
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
    }

    // ---------------- Eventos del TeleportPoint ----------------

    private void OnGazeEntered()
    {
        _gazed = true;
        if (!_soundPlayedThisGaze && _tpVisible)
        {
            _soundPlayedThisGaze = true;
            PlayGazeSound();
        }
    }

    private void OnGazeExited()
    {
        _gazed = false;
        _soundPlayedThisGaze = false;
        _progress = 0f;
    }

    private void OnGazeProgress(float p)
    {
        _progress = Mathf.Clamp01(p);
    }

    private void OnArrived()
    {
        _flameLit = false;
        _puff = 1f;
        _progress = 0f;
        EmitPuff();
    }

    private void OnVisibilityChanged(bool visible)
    {
        _tpVisible = visible;
        if (visible)
        {
            _flameLit = true;
        }
        else
        {
            _progress = 0f;
            _gazed = false;
            _soundPlayedThisGaze = false;
        }
    }

    private void OnPortalVisibilityChanged(bool visible)
    {
        _portalsVisible = visible;
    }

    // ---------------- Armado ----------------

    private void Build()
    {
        GameObject rootGo = new GameObject(RootName);
        rootGo.layer = 2; // Ignore Raycast
        _root = rootGo.transform;
        _root.SetParent(transform, false);

        float floorY = transform.position.y + _floorOffset;
        if (_raycastFloorAtRuntime)
        {
            int mask = ~((1 << 2) | (1 << 7) | (1 << 9));
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 4f, mask, QueryTriggerInteraction.Ignore))
            {
                floorY = hit.point.y;
            }
        }
        _root.position = new Vector3(transform.position.x, floorY + 0.015f, transform.position.z);
        _root.rotation = Quaternion.identity;
        // Compensa la escala del TeleportPoint (algunos son cubos achatados).
        Vector3 ls = transform.lossyScale;
        _root.localScale = new Vector3(SafeInv(ls.x), SafeInv(ls.y), SafeInv(ls.z));

        switch (_style)
        {
            case PortalStyle.Velita:
                _mark = CreateQuad("Brillo", _markMaterial, _markSize, 0.012f, out _markT);
                _confetti = CreateQuad("Confeti", _confettiMaterial, _markSize * 1.25f, 0.006f, out Transform ct);
                ct.localRotation = Quaternion.Euler(0f, Hash01(1) * 360f, 0f);
                BuildCandle();
                break;
            case PortalStyle.Polillas:
                _mark = CreateQuad("Mancha", _markMaterial, _markSize, 0.01f, out _markT);
                BuildMoths(Hash01(2) > 0.5f ? 4 : 3);
                break;
            default:
                _mark = CreateQuad("Marca", _markMaterial, _markSize, 0.01f, out _markT);
                break;
        }
        if (_markT != null) _markT.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);

        if (_dust && _dustMaterial != null) BuildDust();
        BuildAudio();
    }

    private static float SafeInv(float v) => Mathf.Abs(v) < 1e-4f ? 1f : 1f / v;

    /// <summary>Numero fijo 0..1 por punto (para variar giros sin que cambie entre partidas).</summary>
    private float Hash01(int salt)
    {
        unchecked
        {
            int h = salt * 7919;
            string n = gameObject.name;
            for (int i = 0; i < n.Length; i++) h = h * 31 + n[i];
            return (h & 0xFFFF) / 65535f;
        }
    }

    private static Mesh GetQuad()
    {
        if (s_quad != null) return s_quad;
        // Quad horizontal (plano XZ, normal +Y), V hacia +Z: la punta de las
        // huellas (arriba en la textura) mira a +Z local.
        s_quad = new Mesh { name = "PortalQuad", hideFlags = HideFlags.DontUnloadUnusedAsset };
        s_quad.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f)
        };
        s_quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        s_quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        s_quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        s_quad.RecalculateBounds();
        return s_quad;
    }

    /// <summary>Hijo vacio en layer Ignore Raycast, a la altura "lift".</summary>
    private static Transform NewChild(string name, Transform parent, float lift = 0f)
    {
        Transform t = new GameObject(name) { layer = 2 }.transform;
        t.SetParent(parent, false);
        t.localPosition = new Vector3(0f, lift, 0f);
        return t;
    }

    private static MeshRenderer AddQuadRenderer(Transform t, Material mat)
    {
        t.gameObject.AddComponent<MeshFilter>().sharedMesh = GetQuad();
        MeshRenderer r = t.gameObject.AddComponent<MeshRenderer>();
        SetupRenderer(r, mat);
        return r;
    }

    private MeshRenderer CreateQuad(string name, Material mat, float size, float lift, out Transform t)
    {
        t = NewChild(name, _root, lift);
        t.localScale = new Vector3(size, 1f, size);
        return AddQuadRenderer(t, mat);
    }

    private static void SetupRenderer(Renderer r, Material mat)
    {
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.enabled = mat != null;
    }

    private void BuildCandle()
    {
        // Vela de primitivas: cilindro de 5 cm x 12 cm (el cilindro de Unity mide 2 de alto).
        GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = "Vela";
        cyl.layer = 2;
        Collider col = cyl.GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            Destroy(col);
        }
        Transform ct = cyl.transform;
        ct.SetParent(_root, false);
        ct.localPosition = new Vector3(0f, 0.06f, 0f);
        ct.localScale = new Vector3(0.05f, 0.06f, 0.05f);
        _candle = cyl.GetComponent<MeshRenderer>();
        SetupRenderer(_candle, _candleMaterial);

        // Llama: pivote en la punta de la vela (gira hacia la camara) y un quad
        // vertical encima. El pivote se escala (ancho, alto, ancho).
        _flamePivot = NewChild("Llama (pivote)", _root, 0.125f);
        Transform ft = NewChild("Llama", _flamePivot, 0.5f);
        ft.localRotation = Quaternion.Euler(-90f, 0f, 0f); // +Z del quad -> arriba
        _flame = AddQuadRenderer(ft, _flameMaterial);
        SetFlameScale(0.045f, 0.075f);
    }

    private void SetFlameScale(float w, float h)
    {
        _flamePivot.localScale = new Vector3(Mathf.Max(w, 0.0001f), Mathf.Max(h, 0.0001f), Mathf.Max(w, 0.0001f));
    }

    private void BuildMoths(int count)
    {
        _moths = new Moth[count];
        for (int i = 0; i < count; i++)
        {
            Moth m = new Moth();
            m.Renderer = CreateQuad("Polilla " + (i + 1), _mothMaterial, 0.07f, 0.6f, out m.T);
            m.Angle = Hash01(10 + i) * Mathf.PI * 2f;
            m.Radius = Mathf.Lerp(0.18f, 0.42f, Hash01(20 + i));
            m.Height = Mathf.Lerp(0.45f, 1.0f, Hash01(30 + i));
            m.Speed = Mathf.Lerp(1.4f, 2.6f, Hash01(40 + i)) * (Hash01(50 + i) > 0.5f ? 1f : -1f);
            m.Phase = Hash01(60 + i) * 10f;
            _moths[i] = m;
        }
    }

    private void BuildDust()
    {
        GameObject go = NewChild("Polvo", _root, 0.03f).gameObject;

        _ps = go.AddComponent<ParticleSystem>();
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = _ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.startColor = _dustColor;
        main.maxParticles = _dustCount + _arrivalBurst;
        main.gravityModifier = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        ParticleSystem.EmissionModule em = _ps.emission;
        em.rateOverTime = _dustCount / 5f;

        ParticleSystem.ShapeModule shape = _ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = _dustRadius;
        shape.radiusThickness = 1f;
        shape.rotation = new Vector3(90f, 0f, 0f); // circulo acostado en el piso

        // Sube despacio (las tres curvas en el mismo modo, si no Unity se queja).
        ParticleSystem.VelocityOverLifetimeModule vel = _ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        vel.y = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(_dustOrbit);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
        vel.radial = new ParticleSystem.MinMaxCurve(-0.02f);
        _appliedOrbit = _dustOrbit;

        ParticleSystem.ColorOverLifetimeModule col = _ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        _psRenderer = go.GetComponent<ParticleSystemRenderer>();
        SetupRenderer(_psRenderer, _dustMaterial);
        _psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        _psRenderer.sortMode = ParticleSystemSortMode.None;

        if (_dustMaterial.HasProperty(IdBaseColor)) _dustBaseColor = _dustMaterial.GetColor(IdBaseColor);

        _ps.Play();
    }

    private void BuildAudio()
    {
        if (_gazeClips == null || _gazeClips.Length == 0) return;
        _audio = _root.gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.loop = false;
        _audio.spatialBlend = 1f;
        _audio.rolloffMode = AudioRolloffMode.Linear;
        _audio.minDistance = _minDistance;
        _audio.maxDistance = Mathf.Max(_maxDistance, _minDistance + 0.5f);
        _audio.dopplerLevel = 0f;
        _audio.volume = _volume;
    }

    private void PlayGazeSound()
    {
        if (_audio == null || _gazeClips == null || _gazeClips.Length == 0) return;
        AudioClip clip = _gazeClips[Random.Range(0, _gazeClips.Length)];
        if (clip == null) return;
        _audio.pitch = Random.Range(0.92f, 1.08f);
        _audio.PlayOneShot(clip, 1f);
    }

    private void EmitPuff()
    {
        if (_ps == null || _arrivalBurst <= 0) return;
        ParticleSystem.EmitParams ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < _arrivalBurst; i++)
        {
            Vector2 c = Random.insideUnitCircle;
            ep.position = new Vector3(c.x * _dustRadius * 0.6f, 0.02f, c.y * _dustRadius * 0.6f);
            ep.velocity = new Vector3(c.x * 0.3f, Random.Range(0.25f, 0.55f), c.y * 0.3f);
            ep.startLifetime = Random.Range(1.2f, 2f);
            ep.startSize = Random.Range(0.035f, 0.07f);
            ep.applyShapeToPosition = false;
            _ps.Emit(ep, 1);
        }
    }

    // ---------------- Por frame ----------------

    private void Update()
    {
        if (_root == null) return;

        float dt = Time.deltaTime;
        if (_puff > 0f) _puff = Mathf.Max(0f, _puff - dt / 1.8f);

        _vis = Mathf.MoveTowards(_vis, _tpVisible ? 1f : 0f, dt * 3f);

        // Fuera de todo: apagado y sin trabajo extra.
        if (_vis <= 0f && _puff <= 0f)
        {
            SetRenderers(false);
            SetEmission(false);
            return;
        }

        if (_cam == null)
        {
            Camera c = Camera.main;
            if (c == null) return;
            _cam = c.transform;
        }

        Vector3 camPos = _cam.position;
        Vector3 pos = _root.position;
        float dx = camPos.x - pos.x, dz = camPos.z - pos.z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        float distAlpha = 1f - Mathf.SmoothStep(0f, 1f, (dist - _visibleDistance) / _fadeRange);

        _progressSmooth = Mathf.MoveTowards(_progressSmooth, _gazed ? _progress : 0f, dt * 2.5f);

        float subtle = (_portalsVisible || _highlighted) ? 1f : _subtleFactor;
        float pulse = 1f;
        if (_highlighted)
        {
            _highlightTime += dt;
            pulse = 1f + _highlightBoost * (0.5f - 0.5f * Mathf.Cos(_highlightTime * _highlightHz * Mathf.PI * 2f));
        }
        else
        {
            _highlightTime = 0f;
        }

        float alpha = _vis * distAlpha;
        bool show = alpha > 0.003f;
        SetRenderers(show);

        UpdateBackward(dt);
        UpdateYaw(dt, dx, dz);

        if (show)
        {
            float markIntensity = _baseIntensity * (1f + _gazeBoost * _progressSmooth) * subtle * pulse;
            ApplyBlock(_mark, markIntensity, alpha * _swapK);
            ApplyBlock(_confetti, subtle, alpha);
            ApplyBlock(_candle, 1f, alpha);

            if (_flame != null)
            {
                _flameK = Mathf.MoveTowards(_flameK, _flameLit ? 1f : 0f, dt * (_flameLit ? 1.5f : 4f));
                float flick = 1f + 0.08f * (Mathf.PerlinNoise(Time.time * 2.2f, Hash01(3) * 10f) - 0.5f);
                float grow = 1f + 0.8f * _progressSmooth;
                SetFlameScale(0.045f * _flameK * flick, 0.075f * _flameK * grow * flick);
                Vector3 toCam = new Vector3(dx, 0f, dz);
                if (toCam.sqrMagnitude > 1e-4f) _flamePivot.rotation = Quaternion.LookRotation(toCam);
                ApplyBlock(_flame, subtle * (1f + 0.5f * _progressSmooth), alpha);
            }

            if (_moths != null) UpdateMoths(alpha, subtle);
        }

        // Polvo: emision solo con el punto visible; el soplo se ve aunque el
        // punto ya este oculto (el jugador esta parado encima).
        if (_ps != null)
        {
            SetEmission(_tpVisible && show);
            float rate = (_dustCount / 5f) * (subtle < 1f ? 0.5f : 1f);
            if (Mathf.Abs(rate - _appliedRate) > 0.01f)
            {
                _appliedRate = rate;
                ParticleSystem.EmissionModule em = _ps.emission;
                em.rateOverTime = rate;
            }
            float orbit = _dustOrbit + _dustSwirl * _progressSmooth;
            if (Mathf.Abs(orbit - _appliedOrbit) > 0.02f)
            {
                _appliedOrbit = orbit;
                ParticleSystem.VelocityOverLifetimeModule vel = _ps.velocityOverLifetime;
                vel.orbitalY = new ParticleSystem.MinMaxCurve(orbit);
            }
            float dustAlpha = Mathf.Max(alpha * (subtle < 1f ? 0.6f : 1f), _puff * distAlpha);
            _psRenderer.enabled = dustAlpha > 0.003f;
            Color bc = _dustBaseColor;
            bc.a *= dustAlpha;
            _psRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdBaseColor, bc);
            _psRenderer.SetPropertyBlock(_mpb);
        }
    }

    private void ApplyBlock(Renderer r, float intensity, float alpha)
    {
        if (r == null || r.sharedMaterial == null) return;
        r.GetPropertyBlock(_mpb);
        _mpb.SetFloat(IdIntensity, intensity);
        _mpb.SetFloat(IdAlpha, alpha);
        r.SetPropertyBlock(_mpb);
    }

    private void SetRenderers(bool on)
    {
        if (_renderersOn == on) return;
        _renderersOn = on;
        SetOn(_mark, on);
        SetOn(_confetti, on);
        SetOn(_candle, on);
        SetOn(_flame, on);
        if (_moths != null)
        {
            foreach (Moth m in _moths) SetOn(m.Renderer, on);
        }
        if (!on && _psRenderer != null && _puff <= 0f) _psRenderer.enabled = false;
    }

    private static void SetOn(Renderer r, bool on)
    {
        if (r != null) r.enabled = on && r.sharedMaterial != null;
    }

    private void SetEmission(bool on)
    {
        if (_ps == null || _appliedEmission == on) return;
        _appliedEmission = on;
        ParticleSystem.EmissionModule em = _ps.emission;
        em.enabled = on;
    }

    private void UpdateBackward(float dt)
    {
        if (_style != PortalStyle.HuellasAtras || _mark == null) return;
        if (s_backward != _backwardApplied)
        {
            // Fundido corto: baja, cambia la textura, vuelve a subir.
            _swapK = Mathf.MoveTowards(_swapK, 0f, dt * 2f);
            if (_swapK <= 0f)
            {
                _backwardApplied = s_backward;
                Material m = _backwardApplied ? _backwardMarkMaterial : _markMaterial;
                if (m != null) _mark.sharedMaterial = m;
            }
        }
        else
        {
            _swapK = Mathf.MoveTowards(_swapK, 1f, dt * 1.2f);
        }
    }

    private void UpdateYaw(float dt, float dx, float dz)
    {
        if (_markT == null) return;
        // HuellasDescalzas: la punta se aleja del jugador (invita a ir).
        // HuellasAtras (modo atras): la punta mira AL jugador, como si alguien
        // hubiera caminado de espaldas hasta ahi.
        float target;
        if (_style == PortalStyle.HuellasDescalzas)
        {
            target = Mathf.Atan2(-dx, -dz) * Mathf.Rad2Deg;
        }
        else if (_style == PortalStyle.HuellasAtras && _backwardApplied)
        {
            target = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
        }
        else
        {
            return;
        }
        if (dx * dx + dz * dz < 0.25f) return; // encima: no girar
        _currentYaw = Mathf.MoveTowardsAngle(_currentYaw, target, 120f * dt);
        _markT.localRotation = Quaternion.Euler(0f, _currentYaw, 0f);
    }

    private void UpdateMoths(float alpha, float subtle)
    {
        float dt = Time.deltaTime;
        float t = Time.time;
        float speedMul = 1f + 1.2f * _progressSmooth;
        float radiusMul = 1f - 0.35f * _progressSmooth;
        foreach (Moth m in _moths)
        {
            m.Angle += m.Speed * speedMul * dt;
            float a = m.Angle;
            float wob = Mathf.PerlinNoise(t * 0.9f + m.Phase, m.Phase) - 0.5f;
            float r = m.Radius * radiusMul * (1f + 0.4f * wob);
            float h = m.Height + 0.12f * Mathf.Sin(t * 1.3f + m.Phase);
            m.T.localPosition = new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
            // Cuerpo (arriba en la textura = +Z) en la tangente de la orbita, un poco inclinado.
            float sign = m.Speed >= 0f ? 1f : -1f;
            Vector3 tangent = new Vector3(-Mathf.Sin(a) * sign, 0f, Mathf.Cos(a) * sign);
            m.T.localRotation = Quaternion.LookRotation(tangent) * Quaternion.Euler(-25f, 0f, 20f * sign);
            // Aleteo por escala en X (alas), sin cambiar el brillo.
            float flap = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(t * 7f * Mathf.PI + m.Phase));
            m.T.localScale = new Vector3(0.07f * flap, 1f, 0.07f);
            ApplyBlock(m.Renderer, subtle, alpha);
        }
    }
}
