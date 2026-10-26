using UnityEngine;

/// <summary>
/// "Charco de luz" falso en el piso, barato: un quad plano con el shader
/// Custom/FakeLightPool (degradado radial aditivo calculado en el shader, sin
/// textura). Sirve para que los teleport points (orbes) dejen un halo en el
/// piso sin gastar una Light real - en el celular (Forward, nivel Mobile) cada
/// objeto recibe como maximo 4 luces (gotcha #18).
///
/// Uso: agregar el componente al objeto (por ejemplo el orbe de un tp). Al
/// arrancar arma un hijo oculto "Charco de luz (generado)" que NO se guarda en
/// la escena (HideFlags.DontSave). Para previsualizarlo en el Editor sin dar
/// Play: menu contextual del componente > "Construir charco (preview)".
///
/// Asignar siempre el campo Material (un .mat con el shader FakeLightPool):
/// si queda vacio usa Shader.Find, que puede devolver null en el APK
/// (gotcha #25).
/// </summary>
[DisallowMultipleComponent]
public class FakeLightPool : MonoBehaviour
{
    private const string ChildName = "Charco de luz (generado)";

    [Header("Material")]
    [Tooltip("Material con el shader Custom/FakeLightPool. Recomendado asignarlo: sin esto se usa Shader.Find (puede fallar en el APK, gotcha #25). Se comparte entre todos los charcos; el color/intensidad de cada uno va por MaterialPropertyBlock.")]
    [SerializeField] private Material _material;

    [Header("Forma y color")]
    [Tooltip("Radio del charco en metros.")]
    [SerializeField, Min(0.05f)] private float _radius = 1.2f;
    [Tooltip("Color de la luz (HDR: subir Intensity para que el Bloom reaccione).")]
    [ColorUsage(false, true)]
    [SerializeField] private Color _color = new Color(1f, 0.85f, 0.5f, 1f);
    [Tooltip("Multiplicador del brillo. Es aditivo: valores bajos (0.2-0.5) alcanzan.")]
    [SerializeField, Min(0f)] private float _intensity = 0.35f;
    [Tooltip("Que tan concentrado queda el halo (mayor = mas chico y marcado).")]
    [SerializeField, Min(0.1f)] private float _falloff = 2f;
    [Tooltip("Brillo extra del centro (0-1).")]
    [SerializeField, Range(0f, 1f)] private float _core = 0.35f;

    [Header("Ubicacion")]
    [Tooltip("Desplazamiento (en metros de MUNDO) desde la posicion de este objeto. Sin Raycast Down: poner Y negativo para llegar al piso (ej. orbe a 1 m del piso: Y = -0.98). Con Raycast Down se suma al punto donde pego el rayo.")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 0.02f, 0f);
    [Tooltip("Tira un rayo hacia abajo y apoya el charco sobre lo que encuentre (piso). Los modelos glTF no tienen Collider, asi que en esta escena casi seguro no pega: sin impacto usa el Offset.")]
    [SerializeField] private bool _raycastDown;
    [Tooltip("Capas que cuentan como piso para Raycast Down.")]
    [SerializeField] private LayerMask _floorMask = ~0;
    [Tooltip("Hasta cuantos metros hacia abajo busca el piso.")]
    [SerializeField, Min(0.1f)] private float _maxRaycastDistance = 4f;

    [Header("Pulso (opcional)")]
    [Tooltip("Cuanto varia la intensidad (0 = fijo, 0.2 = +-20 %).")]
    [SerializeField, Range(0f, 1f)] private float _pulseAmount = 0.1f;
    [Tooltip("Velocidad del pulso en ciclos por segundo (suave).")]
    [SerializeField, Min(0f)] private float _pulseSpeed = 0.6f;
    [Tooltip("Desfasa el pulso para que varios charcos no respiren al unisono. -1 = al azar.")]
    [SerializeField] private float _phase = -1f;

    [Header("Arranque")]
    [Tooltip("Arma el charco en Start. Destildar para armarlo a mano con BuildPool() desde un UnityEvent.")]
    [SerializeField] private bool _buildOnStart = true;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int FalloffId = Shader.PropertyToID("_Falloff");
    private static readonly int CoreId = Shader.PropertyToID("_Core");

    private static Mesh s_quad;

    private GameObject _child;
    private MeshRenderer _renderer;
    private MaterialPropertyBlock _block;
    private Material _fallbackMaterial;

    private void Start()
    {
        if (_phase < 0f)
        {
            _phase = Random.value * 6.2832f;
        }
        if (_buildOnStart)
        {
            BuildPool();
        }
    }

    private void OnEnable()
    {
        if (_child != null)
        {
            _child.SetActive(true);
        }
    }

    private void OnDisable()
    {
        if (_child != null)
        {
            _child.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        DestroyChild();
        if (_fallbackMaterial != null)
        {
            DestroyObj(_fallbackMaterial);
        }
    }

    private void LateUpdate()
    {
        if (_renderer == null)
        {
            return;
        }
        // El charco es hijo: acompana al objeto. Solo se actualiza el pulso.
        ApplyBlock(PulseMultiplier());
    }

    /// <summary>Arma (o rearma) el charco. Llamable desde un UnityEvent.</summary>
    public void BuildPool()
    {
        DestroyChild();

        Material mat = ResolveMaterial();
        if (mat == null)
        {
            return;
        }

        _child = new GameObject(ChildName);
        _child.hideFlags = HideFlags.DontSave;
        _child.transform.SetParent(transform, false);

        MeshFilter mf = _child.AddComponent<MeshFilter>();
        mf.sharedMesh = GetQuad();
        _renderer = _child.AddComponent<MeshRenderer>();
        _renderer.sharedMaterial = mat;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        Place();
        ApplyBlock(1f);
    }

    /// <summary>Borra el charco generado.</summary>
    public void ClearPool()
    {
        DestroyChild();
    }

    [ContextMenu("Construir charco (preview)")]
    private void BuildPreview()
    {
        if (_phase < 0f)
        {
            _phase = 0f;
        }
        BuildPool();
    }

    [ContextMenu("Borrar charco generado")]
    private void ClearPreview()
    {
        ClearPool();
    }

    // ---------------- Interno ----------------

    private void Place()
    {
        Vector3 pos = transform.position + _offset;
        if (_raycastDown)
        {
            Vector3 origin = transform.position + Vector3.up * 0.3f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _maxRaycastDistance + 0.3f, _floorMask, QueryTriggerInteraction.Ignore))
            {
                pos = hit.point + _offset;
            }
        }

        Transform t = _child.transform;
        t.position = pos;
        t.rotation = Quaternion.identity;

        // El quad mide 1x1: escalar en MUNDO a 2*radio aunque el padre tenga
        // escala (los orbes importados la traen).
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Abs(ls.x) > 0.0001f ? 1f / Mathf.Abs(ls.x) : 1f;
        float sz = Mathf.Abs(ls.z) > 0.0001f ? 1f / Mathf.Abs(ls.z) : 1f;
        float d = _radius * 2f;
        t.localScale = new Vector3(d * sx, 1f, d * sz);
    }

    private float PulseMultiplier()
    {
        if (_pulseAmount <= 0f || _pulseSpeed <= 0f)
        {
            return 1f;
        }
        return 1f + Mathf.Sin(Time.time * _pulseSpeed * 6.2832f + _phase) * _pulseAmount;
    }

    private void ApplyBlock(float mult)
    {
        if (_block == null)
        {
            _block = new MaterialPropertyBlock();
        }
        _renderer.GetPropertyBlock(_block);
        _block.SetColor(ColorId, _color);
        _block.SetFloat(IntensityId, _intensity * mult);
        _block.SetFloat(FalloffId, _falloff);
        _block.SetFloat(CoreId, _core);
        _renderer.SetPropertyBlock(_block);
    }

    private Material ResolveMaterial()
    {
        if (_material != null)
        {
            return _material;
        }
        if (_fallbackMaterial != null)
        {
            return _fallbackMaterial;
        }
        Debug.LogWarning("[FakeLightPool] Falta asignar Material (con el shader Custom/FakeLightPool) en '" + name + "'. Uso Shader.Find, que puede devolver null en el APK (gotcha #25).", this);
        Shader sh = Shader.Find("Custom/FakeLightPool");
        if (sh == null)
        {
            Debug.LogError("[FakeLightPool] No se encontro el shader Custom/FakeLightPool: no se arma el charco.", this);
            return null;
        }
        _fallbackMaterial = new Material(sh);
        return _fallbackMaterial;
    }

    private static Mesh GetQuad()
    {
        if (s_quad != null)
        {
            return s_quad;
        }
        s_quad = new Mesh { name = "FakeLightPool Quad" };
        s_quad.hideFlags = HideFlags.DontSave;
        s_quad.vertices = new[]
        {
            new Vector3(-0.5f, 0f, -0.5f),
            new Vector3(-0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, 0.5f),
            new Vector3(0.5f, 0f, -0.5f),
        };
        s_quad.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f),
        };
        s_quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        s_quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        s_quad.RecalculateBounds();
        return s_quad;
    }

    private void DestroyChild()
    {
        if (_child != null)
        {
            DestroyObj(_child);
        }
        _child = null;
        _renderer = null;
    }

    private static void DestroyObj(Object o)
    {
        if (Application.isPlaying)
        {
            Destroy(o);
        }
        else
        {
            DestroyImmediate(o);
        }
    }
}
