using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// "Las luces se apagan detras tuyo": cuando el jugador avanza y deja atras una
/// lampara (y no la esta mirando), esa lampara se apaga SOLA, con un fundido
/// lento. Una sola vez por entrada y de a una por vez, para que se sienta como
/// que la oscuridad te va siguiendo. Nunca toca la camara.
///
/// Cada entrada puede tener una Light y/o renderers emisivos (el "foco" del
/// modelo). En los renderers solo se apaga el sub-material cuyo sharedMaterial
/// sea el Emissive Material; el resto (metal, etc.) no se toca.
/// </summary>
public class LightsOffBehind : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Posicion de referencia de la lampara (se usa para saber si quedo atras y fuera de vista).")]
        public Transform point;

        [Tooltip("Luz a apagar (opcional). Si tiene FlickeringLight se deshabilita antes del fundido.")]
        public Light light;

        [Tooltip("Renderers con el foco emisivo (opcional).")]
        public Renderer[] emissiveRenderers;

        [NonSerialized] public bool done;
    }

    [Header("Entradas")]
    [SerializeField] private Entry[] _entries;

    [Tooltip("Material emisivo del foco. En cada renderer solo se apaga el sub-material que use ESTE material.")]
    [SerializeField] private Material _emissiveMaterial;

    [Header("Cuando se apagan")]
    [Tooltip("Direccion general en que avanza el jugador (mundo).")]
    [SerializeField] private Vector3 _travelDirection = new Vector3(0f, 0f, 1f);

    [Tooltip("Metros que tiene que haber dejado atras la lampara, medidos sobre la direccion de avance.")]
    [SerializeField] private float _passDistance = 3f;

    [Tooltip("Angulo minimo (grados) entre hacia donde mira el jugador y la lampara para que cuente como 'fuera de vista'.")]
    [SerializeField] private float _minAngle = 100f;

    [Header("Fundido")]
    [Tooltip("Duracion del fundido de apagado, en segundos.")]
    [SerializeField] private float _fadeSeconds = 2.5f;

    [Tooltip("Segundos minimos entre que se apaga una lampara y la siguiente.")]
    [SerializeField] private float _minGapSeconds = 1.5f;

    [Header("Estado")]
    [Tooltip("Si esta apagado, no se apaga ninguna luz. Se prende con Arm().")]
    [SerializeField] private bool _armed = true;

    [Header("Eventos")]
    [SerializeField] private UnityEvent _onLightOff;

    private Camera _cam;
    private bool _fading;
    private float _lastOffTime = -999f;
    private MaterialPropertyBlock _mpb;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveFactorId = Shader.PropertyToID("emissiveFactor");

    /// <summary>Activa el sistema.</summary>
    public void Arm() { _armed = true; }

    /// <summary>Desactiva el sistema (las que ya se apagaron siguen apagadas).</summary>
    public void Disarm() { _armed = false; }

    private void Update()
    {
        if (!_armed || _fading || _entries == null) return;
        if (Time.time - _lastOffTime < _minGapSeconds) return;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 camPos = _cam.transform.position;
        Vector3 camFwd = _cam.transform.forward;
        Vector3 dir = _travelDirection.sqrMagnitude > 0.0001f ? _travelDirection.normalized : Vector3.forward;

        for (int i = 0; i < _entries.Length; i++)
        {
            Entry e = _entries[i];
            if (e == null || e.done || e.point == null) continue;

            Vector3 toPoint = e.point.position - camPos;
            if (Vector3.Dot(toPoint, dir) >= -_passDistance) continue;
            if (Vector3.Angle(camFwd, toPoint) <= _minAngle) continue;

            e.done = true;
            StartCoroutine(FadeOff(e));
            break; // de a una por vez
        }
    }

    private IEnumerator FadeOff(Entry e)
    {
        _fading = true;

        // Luz: se saca el parpadeo para que no pise el fundido.
        Light l = e.light;
        float startIntensity = 0f;
        if (l != null)
        {
            FlickeringLight fl = l.GetComponent<FlickeringLight>();
            if (fl != null) fl.enabled = false;
            startIntensity = l.intensity;
        }

        // Emision: color de partida de cada sub-material a apagar.
        var targets = new System.Collections.Generic.List<(Renderer r, int idx, int propId, Color from)>();
        if (_emissiveMaterial != null && e.emissiveRenderers != null)
        {
            for (int i = 0; i < e.emissiveRenderers.Length; i++)
            {
                Renderer r = e.emissiveRenderers[i];
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != _emissiveMaterial) continue;
                    int pid;
                    if (_emissiveMaterial.HasProperty(EmissionColorId)) pid = EmissionColorId;
                    else if (_emissiveMaterial.HasProperty(EmissiveFactorId)) pid = EmissiveFactorId;
                    else continue;
                    targets.Add((r, m, pid, _emissiveMaterial.GetColor(pid)));
                }
            }
        }

        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        float dur = Mathf.Max(0.01f, _fadeSeconds);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            float inv = 1f - k;
            if (l != null) l.intensity = startIntensity * inv;
            ApplyEmission(targets, inv);
            yield return null;
        }

        if (l != null)
        {
            l.intensity = 0f;
            l.enabled = false;
        }
        ApplyEmission(targets, 0f);

        _lastOffTime = Time.time;
        _fading = false;
        Debug.Log("[LightsOffBehind] Se apago: " + (e.point != null ? e.point.name : "?"));
        _onLightOff?.Invoke();
    }

    private void ApplyEmission(System.Collections.Generic.List<(Renderer r, int idx, int propId, Color from)> targets, float factor)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t.r == null) continue;
            _mpb.Clear();
            t.r.GetPropertyBlock(_mpb, t.idx);
            _mpb.SetColor(t.propId, Color.Lerp(Color.black, t.from, factor));
            t.r.SetPropertyBlock(_mpb, t.idx);
        }
    }
}
