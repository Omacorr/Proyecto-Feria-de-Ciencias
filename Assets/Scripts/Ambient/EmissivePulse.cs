using UnityEngine;

/// <summary>
/// Hace "latir" el brillo de un objeto (las flechas del piso de Parte 2, o
/// cualquier cosa que tenga que llamar la atencion). Va en el GameObject que
/// tiene el Renderer.
///
/// Modo emisivo (por defecto): pulsa _EmissionColor (necesita que el material
/// tenga Emission activado; con el Bloom de las escenas de terror queda un
/// glow que late). Modo color: si el material no soporta emision, pulsa el
/// color base en su lugar.
/// </summary>
public class EmissivePulse : MonoBehaviour
{
    [SerializeField] private Color _color = new Color(1f, 0.85f, 0.3f);

    [Tooltip("Latidos por segundo (aprox).")]
    [SerializeField] private float _speed = 2f;

    [Tooltip("Brillo minimo y maximo del latido.")]
    [SerializeField] private float _min = 0.3f;
    [SerializeField] private float _max = 2.5f;

    [Tooltip("Desfasaje inicial (segundos). Poner distinto en cada flecha para que no latan todas iguales.")]
    [SerializeField] private float _phaseOffset;

    [Tooltip("Si el material NO tiene emision, pulsa el color base en vez del emisivo.")]
    [SerializeField] private bool _pulseBaseColorInstead;

    private Renderer _renderer;
    private MaterialPropertyBlock _mpb;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    // Shader glTF de glTFast (Shader Graphs/glTF-pbrMetallicRoughness).
    private static readonly int GltfEmissiveId = Shader.PropertyToID("emissiveFactor");
    private bool _useGltfEmissive;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();

        // Usa la propiedad emisiva que exista en el material.
        Material m = _renderer != null ? _renderer.sharedMaterial : null;
        _useGltfEmissive = m != null && !m.HasProperty(EmissionColorId) && m.HasProperty(GltfEmissiveId);
    }

    private void Update()
    {
        if (_renderer == null)
        {
            return;
        }

        float k = Mathf.Sin((Time.time + _phaseOffset) * _speed) * 0.5f + 0.5f;
        float intensity = Mathf.Lerp(_min, _max, k);

        _renderer.GetPropertyBlock(_mpb);
        if (_pulseBaseColorInstead)
        {
            Color c = _color * intensity;
            c.a = 1f;
            _mpb.SetColor(BaseColorId, c);
            _mpb.SetColor(ColorId, c);
        }
        else
        {
            if (_useGltfEmissive) _mpb.SetColor(GltfEmissiveId, _color * intensity);
            else _mpb.SetColor(EmissionColorId, _color * intensity);
        }
        _renderer.SetPropertyBlock(_mpb);
    }
}
