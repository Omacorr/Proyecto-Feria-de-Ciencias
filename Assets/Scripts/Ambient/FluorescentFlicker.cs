using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// Parpadeo tipo tubo fluorescente para Backrooms (etapa 5): casi siempre
/// estable, con "tartamudeos" cada tanto (varios cortes seguidos, como el
/// arrancador del tubo) y a veces el tubo queda muerto un rato.
///
/// Por que no alcanza FlickeringLight: el modelo backrooms_vr.glb viene con la
/// luz HORNEADA en las texturas y todos sus materiales son unlit
/// (KHR_materials_unlit -> shader glTF-unlit): una Light en tiempo real no le
/// cambia nada. Este componente hace parpadear lo que se ve de verdad:
///  - Glow Renderers: el color de los paneles/tubos (x Glow Boost, HDR -> Bloom).
///  - Ambient Renderers: oscurece paredes/techo/piso horneados al cortarse la luz.
///  - Light (opcional): para objetos NO unlit (fragmentos de la nota, monstruo).
///  - Dim Volume (opcional): baja la exposicion de toda la imagen.
///  - Hum (opcional): el volumen del zumbido sigue a la luz.
/// Todo es opcional y combinable. Barato para el A54: no necesita ninguna
/// Light real para el efecto principal.
///
/// Los colores se cambian con MaterialPropertyBlock (no toca los .mat, asi que
/// nada queda modificado al salir de Play). Un solo FluorescentFlicker por
/// Renderer: dos componentes sobre el mismo Renderer se pisan.
///
/// Metodos para UnityEvent: StutterNow(), Burst(segundos),
/// BurstThenOff(segundos), TurnOff(), TurnOn().
/// </summary>
public class FluorescentFlicker : MonoBehaviour
{
    [Header("Que parpadea (todo opcional, combinable)")]
    [Tooltip("Light en tiempo real. En Backrooms solo afecta objetos NO unlit (fragmentos, monstruo): el modelo backrooms_vr.glb la ignora. Si se deja vacio, usa la Light de este objeto (si tiene).")]
    [SerializeField] private Light _light;
    [Tooltip("Paneles/tubos de luz. backrooms_vr.glb: el objeto con material 'Translucent' (los difusores de TODAS las lamparas son una sola malla, asi que parpadean juntas). Para un tubo roto suelto: un Quad con material Custom/AbyssVoid (propiedad _Color).")]
    [SerializeField] private Renderer[] _glowRenderers;
    [Tooltip("Multiplicador del color de los paneles con la luz prendida. >1 hace reaccionar al Bloom (umbral 0.9 en el perfil compartido).")]
    [SerializeField] private float _glowBoost = 2f;
    [Tooltip("Multiplicador de los paneles con la luz cortada (tubo apagado = gris, no negro).")]
    [SerializeField] private float _glowOffMultiplier = 0.3f;
    [Tooltip("Ambiente horneado (paredes, techo, piso, muebles). Se oscurece hasta Ambient Min Level cuando la luz se corta. Opcional.")]
    [SerializeField] private Renderer[] _ambientRenderers;
    [Range(0f, 1f)]
    [SerializeField] private float _ambientMinLevel = 0.3f;
    [Tooltip("Propiedad de color del shader. glTF-unlit y glTF-pbrMetallicRoughness: baseColorFactor. URP Lit/Unlit: _BaseColor. Custom/AbyssVoid: _Color. Todos los renderers de ESTE componente tienen que usar la misma.")]
    [SerializeField] private string _colorProperty = "baseColorFactor";
    [Tooltip("Volume GLOBAL con un Profile PROPIO (no el Global Volume Profile compartido) que solo tenga Color Adjustments > Post Exposure = -3. Su peso sube cuando la luz se corta y oscurece TODA la imagen (tambien la mira). Opcional.")]
    [SerializeField] private Volume _dimVolume;
    [Tooltip("AudioSource del zumbido de los tubos (loop). Su volumen baja cuando la luz se corta. Opcional.")]
    [SerializeField] private AudioSource _hum;

    [Header("Comportamiento")]
    [SerializeField] private bool _startOn = true;
    [Tooltip("Segundos (min, max) entre tartamudeos.")]
    [SerializeField] private Vector2 _stutterInterval = new Vector2(6f, 15f);
    [Tooltip("Duracion (min, max) de cada tartamudeo.")]
    [SerializeField] private Vector2 _stutterDuration = new Vector2(0.3f, 0.9f);
    [Tooltip("Duracion (min, max) de cada estado prendido/apagado dentro de un tartamudeo. Si hace parpadear el ambiente entero, no bajar de ~0.07 (fotosensibilidad).")]
    [SerializeField] private Vector2 _stutterStep = new Vector2(0.07f, 0.18f);
    [Range(0f, 1f)]
    [Tooltip("Nivel de luz del tubo 'cortado' durante un tartamudeo (0 = apagado del todo).")]
    [SerializeField] private float _offLevel = 0.15f;
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que, al terminar un tartamudeo, el tubo quede muerto un rato.")]
    [SerializeField] private float _deadChance = 0.15f;
    [Tooltip("Segundos (min, max) que queda muerto.")]
    [SerializeField] private Vector2 _deadDuration = new Vector2(0.8f, 2.5f);
    [Range(0f, 0.2f)]
    [Tooltip("Micro variacion constante con la luz prendida.")]
    [SerializeField] private float _buzzAmount = 0.03f;

    [Header("Eventos")]
    [Tooltip("Se dispara al empezar cada tartamudeo (para un 'tic' del arrancador, por ejemplo AudioSource.Play).")]
    [SerializeField] private UnityEvent _onStutter;

    private struct Slot
    {
        public Renderer Renderer;
        public int MaterialIndex;
        public Vector4 BaseColor; // lineal
        public bool IsGlow;
    }

    private readonly List<Slot> _slots = new List<Slot>();
    private MaterialPropertyBlock _mpb;
    private int _colorId;
    private float _lightBase;
    private float _humBase;
    private float _noiseSeed;

    private bool _inStutter;
    private bool _stepOn = true;
    private float _stepUntil;
    private float _stutterUntil;
    private float _nextStutterAt;
    private float _deadUntil;
    private float _burstUntil;
    private bool _forcedOff;
    private bool _offAfterBurst;
    private float _appliedLevel = -1f;

    // ---------------- API para UnityEvents ----------------

    /// <summary>Tartamudeo inmediato (sin esperar el intervalo al azar).</summary>
    public void StutterNow()
    {
        _deadUntil = 0f;
        _nextStutterAt = Time.time;
    }

    /// <summary>Tartamudeo forzado durante "seconds" segundos (gana sobre todo).</summary>
    public void Burst(float seconds)
    {
        _burstUntil = Time.time + Mathf.Max(0f, seconds);
        _stepUntil = 0f;
    }

    /// <summary>Tartamudeo forzado y despues queda apagado hasta TurnOn().</summary>
    public void BurstThenOff(float seconds)
    {
        Burst(seconds);
        _offAfterBurst = true;
    }

    /// <summary>Apaga y deja apagado hasta TurnOn().</summary>
    public void TurnOff()
    {
        _forcedOff = true;
        _offAfterBurst = false;
        _burstUntil = 0f;
    }

    /// <summary>Vuelve a prender (y al comportamiento normal).</summary>
    public void TurnOn()
    {
        _forcedOff = false;
        _offAfterBurst = false;
        _deadUntil = 0f;
        ScheduleNext(Time.time);
    }

    // ---------------- Ciclo de vida ----------------

    private void Awake()
    {
        if (_light == null)
        {
            _light = GetComponent<Light>();
        }
        _lightBase = _light != null ? _light.intensity : 0f;
        _humBase = _hum != null ? _hum.volume : 0f;
        _mpb = new MaterialPropertyBlock();
        _colorId = Shader.PropertyToID(_colorProperty);
        Collect(_glowRenderers, true);
        Collect(_ambientRenderers, false);
        _forcedOff = !_startOn;
        _noiseSeed = Random.value * 100f;
        ScheduleNext(Time.time);
    }

    private void OnDisable()
    {
        // Dejar todo como estaba (bloque vacio = sin overrides).
        if (_mpb != null)
        {
            _mpb.Clear();
            foreach (Slot s in _slots)
            {
                if (s.Renderer != null)
                {
                    s.Renderer.SetPropertyBlock(_mpb, s.MaterialIndex);
                }
            }
        }
        _appliedLevel = -1f;
        if (_light != null)
        {
            _light.intensity = _lightBase;
        }
        if (_dimVolume != null)
        {
            _dimVolume.weight = 0f;
        }
        if (_hum != null)
        {
            _hum.volume = _humBase;
        }
    }

    private void Update()
    {
        float now = Time.time;
        float level;

        if (now < _burstUntil)
        {
            level = StepLevel(now);
        }
        else
        {
            if (_offAfterBurst)
            {
                _offAfterBurst = false;
                _forcedOff = true;
            }

            if (_forcedOff)
            {
                _inStutter = false;
                _stepOn = true;
                level = 0f;
            }
            else
            {
                // Termino un tartamudeo: a veces el tubo queda muerto un rato.
                if (_inStutter && now >= _stutterUntil)
                {
                    _inStutter = false;
                    _stepOn = true;
                    if (Random.value < _deadChance)
                    {
                        _deadUntil = now + RandomIn(_deadDuration);
                    }
                    ScheduleNext(now);
                }

                // Empieza un tartamudeo.
                if (!_inStutter && now >= _nextStutterAt && now >= _deadUntil)
                {
                    _inStutter = true;
                    _stutterUntil = now + RandomIn(_stutterDuration);
                    _stepUntil = 0f;
                    _onStutter?.Invoke();
                }

                if (_inStutter)
                {
                    level = StepLevel(now);
                }
                else if (now < _deadUntil)
                {
                    level = 0f;
                }
                else
                {
                    level = 1f - _buzzAmount * Mathf.PerlinNoise(_noiseSeed, now * 30f);
                }
            }
        }

        Apply(level);
    }

    // ---------------- Interno ----------------

    private void Collect(Renderer[] renderers, bool isGlow)
    {
        if (renderers == null)
        {
            return;
        }
        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        foreach (Renderer r in renderers)
        {
            if (r == null)
            {
                continue;
            }
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null)
                {
                    continue;
                }
                if (!m.HasProperty(_colorId))
                {
                    Debug.LogWarning("[FluorescentFlicker] El material '" + m.name + "' de '" + r.name + "' no tiene la propiedad '" + _colorProperty + "': no parpadea.");
                    continue;
                }
                Color c = m.GetColor(_colorId);
                if (linear)
                {
                    c = c.linear;
                }
                _slots.Add(new Slot { Renderer = r, MaterialIndex = i, BaseColor = c, IsGlow = isGlow });
            }
        }
    }

    private float StepLevel(float now)
    {
        if (now >= _stepUntil)
        {
            _stepOn = !_stepOn;
            _stepUntil = now + Mathf.Max(0.02f, RandomIn(_stutterStep));
        }
        return _stepOn ? 1f : _offLevel;
    }

    private void ScheduleNext(float now)
    {
        _nextStutterAt = Mathf.Max(now, _deadUntil) + RandomIn(_stutterInterval);
    }

    private static float RandomIn(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    private void Apply(float level)
    {
        if (_light != null)
        {
            _light.intensity = _lightBase * level;
        }
        if (_dimVolume != null)
        {
            _dimVolume.weight = 1f - level;
        }
        if (_hum != null)
        {
            _hum.volume = _humBase * Mathf.Lerp(0.15f, 1f, level);
        }

        if (_slots.Count == 0 || Mathf.Abs(level - _appliedLevel) < 0.004f)
        {
            return;
        }
        _appliedLevel = level;

        float glowK = Mathf.Lerp(_glowOffMultiplier, _glowBoost, level);
        float ambientK = Mathf.Lerp(_ambientMinLevel, 1f, level);
        foreach (Slot s in _slots)
        {
            if (s.Renderer == null)
            {
                continue;
            }
            float k = s.IsGlow ? glowK : ambientK;
            Vector4 c = s.BaseColor;
            s.Renderer.GetPropertyBlock(_mpb, s.MaterialIndex);
            // SetVector (no SetColor): el color ya esta en espacio lineal.
            _mpb.SetVector(_colorId, new Vector4(c.x * k, c.y * k, c.z * k, c.w));
            s.Renderer.SetPropertyBlock(_mpb, s.MaterialIndex);
        }
    }
}
