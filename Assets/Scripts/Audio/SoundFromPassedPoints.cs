using System.Collections;
using UnityEngine;

/// <summary>
/// Sonidos sueltos (golpes en puertas, crujidos) que salen SIEMPRE de puntos
/// que el jugador ya paso, o sea, que quedaron detras. Cada tanto elige al azar
/// uno de esos puntos y suena ahi en 3D (se juega con auriculares).
/// Usa un AudioSource propio creado en runtime.
/// </summary>
public class SoundFromPassedPoints : MonoBehaviour
{
    [Header("Fuentes")]
    [Tooltip("Puntos candidatos (por ejemplo, las puertas del pasillo).")]
    [SerializeField] private Transform[] _points;

    [Tooltip("Clips posibles. Se elige uno al azar.")]
    [SerializeField] private AudioClip[] _clips;

    [Header("Tiempos")]
    [Tooltip("Segundos hasta el primer sonido (min, max).")]
    [SerializeField] private Vector2 _firstDelay = new Vector2(30f, 50f);

    [Tooltip("Segundos entre un sonido y el siguiente (min, max).")]
    [SerializeField] private Vector2 _gap = new Vector2(40f, 90f);

    [Header("Posicion y sonido")]
    [Tooltip("Distancia valida al jugador (min, max) en metros.")]
    [SerializeField] private Vector2 _distance = new Vector2(4f, 16f);

    [Range(0f, 1f)]
    [SerializeField] private float _volume = 0.8f;

    [Tooltip("Pitch al azar (min, max).")]
    [SerializeField] private Vector2 _pitch = new Vector2(0.9f, 1.05f);

    [SerializeField] private bool _startOnPlay = true;

    private AudioSource _source;
    private Coroutine _routine;
    private int _lastIndex = -1;

    private void Start()
    {
        if (_startOnPlay) StartSounds();
    }

    private void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    /// <summary>Empieza el ciclo de sonidos.</summary>
    public void StartSounds()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(Loop());
    }

    /// <summary>Corta el ciclo (un sonido que ya esta sonando termina solo).</summary>
    public void StopSounds()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    /// <summary>Reproduce un sonido ahora, si hay un punto valido detras.</summary>
    public void PlayNow()
    {
        TryPlay();
    }

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(Random.Range(_firstDelay.x, _firstDelay.y));
        while (true)
        {
            if (TryPlay())
                yield return new WaitForSeconds(Random.Range(_gap.x, _gap.y));
            else
                yield return new WaitForSeconds(5f);
        }
    }

    private bool TryPlay()
    {
        if (_points == null || _points.Length == 0 || _clips == null || _clips.Length == 0) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 camPos = cam.transform.position;
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        var valid = new System.Collections.Generic.List<int>();
        for (int i = 0; i < _points.Length; i++)
        {
            Transform p = _points[i];
            if (p == null || i == _lastIndex) continue;
            Vector3 to = p.position - camPos;
            if (Vector3.Dot(to, fwd) >= 0f) continue;
            float d = to.magnitude;
            if (d < _distance.x || d > _distance.y) continue;
            valid.Add(i);
        }
        if (valid.Count == 0) return false;

        int idx = valid[Random.Range(0, valid.Count)];
        AudioClip clip = _clips[Random.Range(0, _clips.Length)];
        if (clip == null) return false;

        EnsureSource();
        _source.transform.position = _points[idx].position;
        _source.pitch = Random.Range(_pitch.x, _pitch.y);
        _source.PlayOneShot(clip, _volume);
        _lastIndex = idx;
        Debug.Log("[SoundFromPassedPoints] Suena en " + _points[idx].name);
        return true;
    }

    private void EnsureSource()
    {
        if (_source != null) return;
        var go = new GameObject("Sonido desde puntos (runtime)");
        go.transform.SetParent(transform, false);
        _source = go.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.spatialBlend = 1f;
        _source.dopplerLevel = 0f;
        _source.rolloffMode = AudioRolloffMode.Logarithmic;
        _source.minDistance = 1.5f;
        _source.maxDistance = 20f;
    }
}
