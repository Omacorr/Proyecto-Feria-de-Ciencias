using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Cumpleaños (Parte 2): la "grabacion de camara de seguridad" que se ve en la
/// tele cuando metes la cinta. No usa un archivo de video: renderiza EN VIVO un
/// diorama escondido (una copia del monstruo + una luz) con una camara dedicada
/// a un RenderTexture, y le agrega arriba la UI tipica de CCTV (timestamp,
/// "REC", estatica). El monstruo hace una animacion scripteada de acercarse y
/// tirarse al lente, corta a estatica, y dispara _onMovieEnd.
///
/// Play() se cablea desde VCRSlot._onTapeInserted.
/// _onMovieEnd se cablea a MonsterChaser.StartChase() (y a apagar la tele, etc.).
///
/// SETUP en el Editor:
///  1. Poner un "set" escondido lejos del mapa (ej. Y = -500): un piso, una luz
///     tenue (asignar en _flickerLight) y una copia de Moustruo1 (asignar su
///     Transform en _monster).
///  2. Una Camera nueva mirando ese set, asignada en _footageCamera
///     (dejarla DESACTIVADA en la escena; el script la prende).
///  3. _tvScreen = el Renderer de la PANTALLA de la tele (la sub-malla del
///     modelo TV que es el vidrio). El script le pone el RenderTexture y la
///     hace brillar.
/// </summary>
public class SecurityFootage : MonoBehaviour
{
    [Header("Render")]
    [Tooltip("Camara que filma el diorama escondido. Dejar DESACTIVADA; el script la prende.")]
    [SerializeField] private Camera _footageCamera;

    [Tooltip("Renderer de la pantalla de la tele. El script le asigna el RenderTexture y le prende la emision.")]
    [SerializeField] private Renderer _tvScreen;

    [SerializeField] private int _textureWidth = 640;
    [SerializeField] private int _textureHeight = 480;

    [Header("Diorama")]
    [Tooltip("Transform del monstruo dentro del diorama (una copia del modelo).")]
    [SerializeField] private Transform _monster;

    [Tooltip("Correccion de orientacion del monstruo del diorama. Si en la grabacion se ve ACOSTADO o DADO VUELTA, ajusta esto (p.ej. (-90,0,0) para pararlo). (0,0,0) = no toca nada.")]
    [SerializeField] private Vector3 _monsterLocalEuler;

    [Tooltip("Luz del diorama; parpadea durante la grabacion.")]
    [SerializeField] private Light _flickerLight;

    [Header("Tiempos")]
    [Tooltip("Duracion total de la 'grabacion' en segundos (sin contar la estatica final).")]
    [SerializeField] private float _duration = 16f;

    [Tooltip("Segundos de estatica al final, antes de que aparezca el monstruo en la sala.")]
    [SerializeField] private float _staticTail = 1.6f;

    [Header("Audio (opcional)")]
    [Tooltip("AudioSource del diorama (o de la tele). Suena el ambiente + el grito + la estatica.")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _ambienceLoop;
    [SerializeField] private AudioClip _screamClip;
    [SerializeField] private AudioClip _staticClip;

    [Header("Evento")]
    [Tooltip("Se dispara UNA vez al terminar. Cablear MonsterChaser.StartChase().")]
    [SerializeField] private UnityEvent _onMovieEnd;

    private RenderTexture _rt;
    private bool _played;
    private Material _screenMat;
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // UI generada en runtime
    private Canvas _overlay;
    private TMP_Text _camLabel;
    private TMP_Text _timeLabel;
    private Image _recDot;
    private RawImage _staticImg;
    private Image _vignette;
    private Texture2D _noiseTex;

    private Vector3 _camBasePos;
    private Vector3 _monsterBasePos;
    private Quaternion _monsterFix = Quaternion.identity;

    private void Awake()
    {
        _rt = new RenderTexture(_textureWidth, _textureHeight, 16, RenderTextureFormat.Default)
        {
            name = "SecurityFootage_RT",
            filterMode = FilterMode.Point // look mas CCTV
        };
        _rt.Create();

        if (_footageCamera != null)
        {
            _footageCamera.targetTexture = _rt;
            _footageCamera.enabled = false;
            _camBasePos = _footageCamera.transform.localPosition;
        }
        if (_monster != null)
        {
            _monsterBasePos = _monster.position;
            if (_monsterLocalEuler != Vector3.zero)
            {
                _monsterFix = Quaternion.Euler(_monsterLocalEuler);
                _monster.rotation = _monsterFix;
            }
        }

        // Si no se asigno la pantalla a mano, tratar de encontrarla sola.
        if (_tvScreen == null)
        {
            _tvScreen = ResolveScreen();
            if (_tvScreen != null)
                Debug.Log("[SecurityFootage] _tvScreen no estaba asignada -> uso '" + _tvScreen.name + "' (si no es el vidrio, asignala a mano).");
            else
                Debug.LogWarning("[SecurityFootage] No encontre la pantalla de la tele. Asigna 'Tv Screen' a mano o la pelicula no se va a ver.");
        }

        if (_tvScreen != null)
        {
            // En vez de pelear con las propiedades del shader glTF (que usa
            // _BaseColorMap / emissiveTexture, no _BaseMap), le pongo un
            // material Unlit nuevo que solo muestra el RenderTexture. Unlit =
            // siempre a full brillo, se lee como una pantalla encendida.
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Texture");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            _screenMat = new Material(sh) { name = "SecurityFootage_ScreenMat" };
            if (_screenMat.HasProperty(BaseMapId)) _screenMat.SetTexture(BaseMapId, _rt);
            if (_screenMat.HasProperty(MainTexId)) _screenMat.SetTexture(MainTexId, _rt);
            _tvScreen.material = _screenMat;
            SetScreenBlack(true); // apagada hasta Play()
        }

        BuildOverlay();
    }

    /// <summary>
    /// Busca el Renderer de la pantalla dentro de la jerarquia de la tele:
    /// primero por nombre (propio o del padre) que contenga "screen"/"pantalla"/
    /// "glass"/etc.; si no, agarra el Renderer mas "plano y grande" (una
    /// pantalla es un rectangulo fino).
    /// </summary>
    private Renderer ResolveScreen()
    {
        string[] keys = { "screen", "pantalla", "glass", "vidrio", "display", "lcd", "crt", "tube", "monitor" };
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name.ToLowerInvariant();
            string pn = r.transform.parent != null ? r.transform.parent.name.ToLowerInvariant() : "";
            foreach (string k in keys)
                if (n.Contains(k) || pn.Contains(k)) return r;
        }

        Renderer best = null;
        float bestScore = -1f;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            Vector3 s = r.bounds.size;
            float mn = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            float mx = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            if (mx <= 0.0001f) continue;
            float flat = 1f - mn / mx;                       // ~1 = losa fina
            float area = (s.x * s.y + s.y * s.z + s.x * s.z); // cara mas grande, aprox
            float score = flat * area;
            if (score > bestScore) { bestScore = score; best = r; }
        }
        return best;
    }

    private void OnDestroy()
    {
        if (_rt != null) _rt.Release();
    }

    public void Play()
    {
        if (_played)
        {
            return;
        }
        _played = true;

        if (_footageCamera == null)
            Debug.LogWarning("[SecurityFootage] Falta _footageCamera (la camara del diorama) — la pantalla va a quedar en negro.");
        if (_screenMat == null)
            Debug.LogWarning("[SecurityFootage] No hay pantalla resuelta — la pelicula no se va a ver en la tele.");

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        SetScreenBlack(false);
        if (_footageCamera != null) _footageCamera.enabled = true;
        if (_overlay != null) _overlay.gameObject.SetActive(true);
        if (_staticImg != null) _staticImg.enabled = false;

        if (_audioSource != null && _ambienceLoop != null)
        {
            _audioSource.clip = _ambienceLoop;
            _audioSource.loop = true;
            _audioSource.Play();
        }

        float t = 0f;
        float recBlink = 0f;
        while (t < _duration)
        {
            t += Time.deltaTime;
            float p = t / _duration; // 0..1

            // timestamp CCTV
            if (_timeLabel != null)
            {
                int total = 12000 + Mathf.RoundToInt(t); // arranca en un numero feo
                _timeLabel.text = string.Format("{0:00}:{1:00}:{2:00}", (total / 3600) % 24, (total / 60) % 60, total % 60);
            }
            // REC parpadeante
            recBlink += Time.deltaTime;
            if (_recDot != null && recBlink > 0.5f)
            {
                _recDot.enabled = !_recDot.enabled;
                recBlink = 0f;
            }

            // luz que parpadea, mas violenta cerca del final
            if (_flickerLight != null)
            {
                float chaos = Mathf.Lerp(0.15f, 0.9f, p);
                _flickerLight.enabled = Random.value > chaos * 0.35f;
            }

            // el monstruo: quieto/sway al principio, avanza al lente en el ultimo 45%
            if (_monster != null && _footageCamera != null)
            {
                if (p < 0.55f)
                {
                    Vector3 sway = new Vector3(Mathf.Sin(t * 1.3f) * 0.05f, 0f, Mathf.Sin(t * 0.7f) * 0.03f);
                    _monster.position = _monsterBasePos + sway;
                }
                else
                {
                    float k = Mathf.InverseLerp(0.55f, 1f, p);
                    Vector3 lensTarget = _footageCamera.transform.position + _footageCamera.transform.forward * Mathf.Lerp(2.2f, 0.35f, k);
                    _monster.position = Vector3.MoveTowards(_monster.position, lensTarget, Mathf.Lerp(0.6f, 4.5f, k) * Time.deltaTime);
                    Vector3 look = _footageCamera.transform.position - _monster.position; look.y = 0f;
                    if (look.sqrMagnitude > 0.001f) _monster.rotation = Quaternion.LookRotation(look, Vector3.up) * _monsterFix;
                }
            }

            // sacudida de camara (crece con p)
            if (_footageCamera != null)
            {
                float shake = Mathf.Lerp(0.004f, 0.06f, p * p);
                _footageCamera.transform.localPosition = _camBasePos + (Vector3)Random.insideUnitCircle * shake;
            }

            // glitches cortos aleatorios
            if (_staticImg != null)
            {
                if (Random.value < Mathf.Lerp(0.005f, 0.08f, p))
                {
                    RefreshNoise();
                    _staticImg.enabled = true;
                }
                else if (_staticImg.enabled && Random.value < 0.4f)
                {
                    _staticImg.enabled = false;
                }
            }

            yield return null;
        }

        // --- corte final: grito + estatica plena ---
        if (_audioSource != null)
        {
            if (_screamClip != null) _audioSource.PlayOneShot(_screamClip);
        }
        if (_footageCamera != null) _footageCamera.enabled = false;
        if (_staticImg != null)
        {
            _staticImg.enabled = true;
        }
        if (_audioSource != null && _staticClip != null)
        {
            _audioSource.loop = true;
            _audioSource.clip = _staticClip;
            _audioSource.Play();
        }

        float s = 0f;
        while (s < _staticTail)
        {
            s += Time.deltaTime;
            RefreshNoise();
            yield return null;
        }

        // apagar todo
        if (_overlay != null) _overlay.gameObject.SetActive(false);
        if (_audioSource != null) _audioSource.Stop();
        SetScreenBlack(true);

        Debug.Log("[SecurityFootage] Grabacion terminada -> aparece el monstruo en la sala.");
        _onMovieEnd?.Invoke();
    }

    private void SetScreenBlack(bool black)
    {
        if (_screenMat == null) return;
        // Unlit multiplica mapa x color, asi que color negro = pantalla apagada,
        // color blanco = se ve el RenderTexture tal cual.
        Color c = black ? Color.black : Color.white;
        if (_screenMat.HasProperty(BaseColorId)) _screenMat.SetColor(BaseColorId, c);
        if (_screenMat.HasProperty(ColorId)) _screenMat.SetColor(ColorId, c);
    }

    // ---------------------------------------------------------- overlay CCTV

    private void BuildOverlay()
    {
        GameObject go = new GameObject("FootageOverlay");
        go.transform.SetParent(transform, false);
        _overlay = go.AddComponent<Canvas>();
        _overlay.renderMode = RenderMode.ScreenSpaceCamera;
        _overlay.worldCamera = _footageCamera;
        _overlay.planeDistance = 0.5f;
        CanvasScaler sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(_textureWidth, _textureHeight);

        _noiseTex = new Texture2D(128, 96, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        RefreshNoise();

        _staticImg = NewChild<RawImage>("Static");
        Stretch(_staticImg.rectTransform);
        _staticImg.texture = _noiseTex;
        _staticImg.color = new Color(1f, 1f, 1f, 0.9f);
        _staticImg.enabled = false;

        _vignette = NewChild<Image>("Vignette");
        Stretch(_vignette.rectTransform);
        _vignette.color = new Color(0f, 0f, 0f, 0.28f);
        _vignette.raycastTarget = false;

        _camLabel = NewText("CamLabel", "CAM 03  -  BACKSTAGE", TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(12f, -10f));
        _timeLabel = NewText("TimeLabel", "00:00:00", TextAlignmentOptions.TopRight,
            new Vector2(1f, 1f), new Vector2(-12f, -10f));

        _recDot = NewChild<Image>("RecDot");
        _recDot.color = new Color(1f, 0.15f, 0.1f, 1f);
        RectTransform rd = _recDot.rectTransform;
        rd.anchorMin = rd.anchorMax = new Vector2(0f, 1f);
        rd.sizeDelta = new Vector2(14f, 14f);
        rd.anchoredPosition = new Vector2(14f, -34f);

        NewText("RecTxt", "REC", TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(30f, -27f));

        _overlay.gameObject.SetActive(false);
    }

    private void RefreshNoise()
    {
        if (_noiseTex == null) return;
        Color32[] px = new Color32[_noiseTex.width * _noiseTex.height];
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)Random.Range(0, 255);
            px[i] = new Color32(v, v, v, 255);
        }
        _noiseTex.SetPixels32(px);
        _noiseTex.Apply(false);
    }

    private T NewChild<T>(string n) where T : Component
    {
        GameObject c = new GameObject(n, typeof(RectTransform));
        c.transform.SetParent(_overlay.transform, false);
        return c.AddComponent<T>();
    }

    private TMP_Text NewText(string n, string txt, TextAlignmentOptions align, Vector2 anchor, Vector2 pos)
    {
        TextMeshProUGUI t = NewChild<TextMeshProUGUI>(n);
        t.text = txt;
        t.fontSize = 22f;
        t.color = new Color(0.9f, 0.95f, 0.9f, 0.85f);
        t.alignment = align;
        t.raycastTarget = false;
        TMP_FontAsset f = TMP_Settings.defaultFontAsset;
        if (f != null) t.font = f;
        RectTransform rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(320f, 40f);
        rt.anchoredPosition = pos;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
