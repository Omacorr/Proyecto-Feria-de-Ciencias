using UnityEngine;

/// <summary>
/// Brillo emisivo para el escalon "directo" de HintDirector: hace que el
/// objeto que hay que usar (la VHS, la pintura, un fragmento...) brille
/// despacio para que el visitante lo encuentre.
///
/// Usa un MaterialPropertyBlock sobre TODOS los renderers hijos, asi que no
/// clona ni toca los materiales compartidos (otros objetos con el mismo
/// material no se enteran). Escribe a la vez <c>emissiveFactor</c> (shader
/// glTF de glTFast, que es el de casi todos los modelos del proyecto) y
/// <c>_EmissionColor</c> (URP Lit), mismo patron que EmissivePulse.
///
/// El pulso es un seno lento (por defecto 0.5 Hz, tope 1 Hz) y nunca baja a
/// cero: nada de destellos, por el aviso de fotosensibilidad.
///
/// Ojo con URP Lit: si el material no tiene la emision activada (keyword
/// _EMISSION), _EmissionColor no hace nada. Con el shader glTF funciona siempre.
/// Y no lo pongas en el mismo objeto que un EmissivePulse/GazeAttractor: los
/// dos escriben la misma propiedad y el ultimo en escribir gana. El brillo se
/// SUMA a la emision original de cada material (leida al arrancar) y al
/// apagarse la restaura, asi no le borra el brillo propio a los objetos que ya
/// brillan (los fragmentos de la Parte 5, la VHS, etc.).
/// </summary>
public class HintGlow : MonoBehaviour
{
    [Tooltip("Color del brillo. Calido y suave por defecto, para que combine con la estetica de la Parte 1.")]
    [SerializeField] private Color _color = new Color(1f, 0.78f, 0.45f);

    [Tooltip("Fuerza maxima del brillo (multiplica al color).")]
    [SerializeField] private float _strength = 0.8f;

    [Tooltip("Fuerza minima del pulso, como fraccion de la maxima. Nunca llega a 0 para que no 'parpadee'.")]
    [Range(0f, 1f)]
    [SerializeField] private float _minFraction = 0.35f;

    [Tooltip("Velocidad del pulso en ciclos por segundo. Se limita a 1 Hz (fotosensibilidad).")]
    [Range(0.05f, 1f)]
    [SerializeField] private float _pulseHz = 0.5f;

    [Tooltip("Segundos del fundido al prender/apagar.")]
    [SerializeField] private float _fadeSeconds = 0.6f;

    [Tooltip("Prender el brillo al arrancar (para probar).")]
    [SerializeField] private bool _startOn;

    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private Color[] _baseGltf;     // emision original (emissiveFactor) por renderer
    private Color[] _baseEmission; // emision original (_EmissionColor) por renderer
    private bool _on;
    private float _level; // 0..1, fundido de prendido/apagado
    private float _phaseTime;

    /// <summary>true si el brillo esta prendido (o prendiendose).</summary>
    public bool IsOn => _on;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
        _baseGltf = new Color[_renderers.Length];
        _baseEmission = new Color[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
        {
            Material m = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
            _baseGltf[i] = EmissionUtil.Read(m, EmissionUtil.GltfId);
            _baseEmission[i] = EmissionUtil.Read(m, EmissionUtil.UrpId);
        }
        _on = _startOn;
        _level = _startOn ? 1f : 0f;
    }

    private void OnDisable()
    {
        // Sin brillo pegado si el objeto se apaga a mitad de pulso. Si nunca
        // se prendio no escribe nada (no pisa la emision propia del material).
        if (_level > 0f)
        {
            _level = 0f;
            Write(0f);
        }
    }

    /// <summary>Prende el brillo (con fundido). Llamable desde UnityEvent.</summary>
    public void On()
    {
        if (!_on)
        {
            _phaseTime = 0f;
        }
        _on = true;
    }

    /// <summary>Apaga el brillo (con fundido). Llamable desde UnityEvent.</summary>
    public void Off()
    {
        _on = false;
    }

    /// <summary>Prende o apaga segun el bool (para UnityEvent&lt;bool&gt;).</summary>
    public void SetOn(bool on)
    {
        if (on) On(); else Off();
    }

    private void Update()
    {
        float target = _on ? 1f : 0f;
        if (_level == 0f && target == 0f)
        {
            return; // apagado y quieto: no escribe nada
        }

        float step = _fadeSeconds > 0f ? Time.deltaTime / _fadeSeconds : 1f;
        _level = Mathf.MoveTowards(_level, target, step);
        if (_level == 0f)
        {
            Write(0f); // termino de apagarse: vuelve a la emision original
            return;
        }

        _phaseTime += Time.deltaTime;
        float hz = Mathf.Clamp(_pulseHz, 0.05f, 1f);
        // Seno de 0..1, arrancando en el minimo para que entre suave.
        float wave = 0.5f - 0.5f * Mathf.Cos(_phaseTime * hz * 2f * Mathf.PI);
        float amount = Mathf.Lerp(_minFraction, 1f, wave);

        Write(_strength * amount * _level);
    }

    private void Write(float intensity)
    {
        if (_renderers == null || _mpb == null)
        {
            return;
        }

        Color c = _color * Mathf.Max(0f, intensity);
        c.a = 1f;
        for (int i = 0; i < _renderers.Length; i++)
        {
            Color g = _baseGltf[i] + c; g.a = 1f;
            Color e = _baseEmission[i] + c; e.a = 1f;
            EmissionUtil.Set(_renderers[i], _mpb, g, e);
        }
    }
}
