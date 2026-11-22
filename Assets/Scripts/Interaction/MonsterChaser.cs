using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cumpleaños (Parte 2): el monstruo de la pelicula. Arranca oculto.
/// StartChase() lo activa (se cablea desde SecurityFootage._onMovieEnd via
/// ChaseReveal) y desde ahi persigue al jugador. Primero se queda PARADO un
/// segundo mirandote y despues camina, adaptando su velocidad a la del jugador
/// (nunca mas rapido que 0.9 x la caminata: la persecucion es tensa pero ganable).
///
/// Si alcanza al jugador (_catchDistance), dispara _onCaught UNA vez.
/// StopChase() lo frena (cablearlo desde LevelDoor._onOpen al llegar a la salida).
/// </summary>
public class MonsterChaser : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("A quien persigue. Si se deja vacio, usa Camera.main en runtime.")]
    [SerializeField] private Transform _target;

    [Header("Movimiento")]
    [Tooltip("Metros por segundo: velocidad base (arranque lento).")]
    [SerializeField] private float _speed = 0.7f;

    [Header("Velocidad adaptativa")]
    [Tooltip("Fraccion de la velocidad promedio del jugador que iguala el monstruo (0.85 = un poco mas lento, ganable).")]
    [SerializeField] private float _matchFactor = 0.85f;

    [Tooltip("Velocidad maxima (m/s). 0 = automatico: 0.9 x la velocidad de caminata del TeleportManager si se encuentra, si no 3.")]
    [SerializeField] private float _maxSpeed = 0f;

    [Tooltip("Distancia por el camino (m) a partir de la cual el monstruo acelera para no quedar demasiado atras.")]
    [SerializeField] private float _rubberBandStartDistance = 8f;

    [Tooltip("m/s extra por cada metro de distancia sobre 'Rubber Band Start Distance'.")]
    [SerializeField] private float _rubberBandPerMeter = 0.12f;

    [Tooltip("Ventana (segundos) del promedio de la velocidad del jugador. Mas grande = reacciona mas lento.")]
    [SerializeField] private float _playerSpeedWindow = 2f;

    [Tooltip("Velocidad (m/s) a la que se arrastra el monstruo cuando el jugador esta quieto. Al caminar el jugador, la velocidad sube de esta hacia la adaptativa.")]
    [SerializeField] private float _minSpeed = 0.5f;

    [Header("Persecucion")]
    [Tooltip("Distancia (m, horizontal) a la que se considera que te alcanzo.")]
    [SerializeField] private float _catchDistance = 1.4f;

    [Header("Rastro (para no atravesar paredes)")]
    [Tooltip("El monstruo sigue el CAMINO que hizo el jugador (las 'migas' que va dejando) en vez de ir en linea recta, porque el mapa no tiene colliders. Como el jugador solo se mueve por puntos validos, el monstruo tambien.")]
    [SerializeField] private float _trailSpacing = 1.2f;

    [Tooltip("Si el monstruo esta a menos de esta distancia del jugador, va derecho a el (tramo final), sin importar el rastro.")]
    [SerializeField] private float _directChaseRange = 3f;

    [Header("Aparicion")]
    [Tooltip("Segundos que el monstruo se queda PARADO (girando a mirarte) despues de aparecer, antes de empezar a caminar.")]
    [SerializeField] private float _holdBeforeWalk = 1f;

    [Header("Audio (opcional)")]
    [Tooltip("Loop que se prende al empezar la persecucion (pasos, respiracion, musica de tension).")]
    [SerializeField] private AudioSource _chaseLoop;

    [SerializeField] private AudioSource _appearAudioSource;
    [SerializeField] private AudioClip _appearSound;

    [Header("Evento")]
    [Tooltip("Se dispara UNA vez si el monstruo te alcanza.")]
    [SerializeField] private UnityEvent _onCaught;

    private bool _chasing;
    private bool _caught;
    private float _walkStartTime;
    private float _fixedY;
    private readonly List<Vector3> _trail = new List<Vector3>();
    private Coroutine _loopFade;
    private float _playerAvgSpeed;
    private Vector3 _lastTargetPos;
    private bool _hasLastTargetPos;
    private TeleportManager _teleportManager;
    private bool _teleportManagerSearched;
    private Transform _cachedMainCam;

    /// <summary>Velocidad actual del monstruo en m/s (para depurar).</summary>
    public float CurrentSpeed { get; private set; }

    private void Awake()
    {
        SetVisible(false);
        enabled = false; // no corre Update hasta StartChase
    }

    /// <summary>Cablear desde SecurityFootage._onMovieEnd (via ChaseReveal).</summary>
    public void StartChase()
    {
        if (_chasing || _caught)
        {
            return;
        }
        _chasing = true;
        enabled = true;
        SetVisible(true);

        // La Y que le paso ChaseReveal es la del piso: se bloquea esa altura.
        _fixedY = transform.position.y;
        _trail.Clear();
        _playerAvgSpeed = 0f;
        _hasLastTargetPos = false;
        CurrentSpeed = _speed;
        _walkStartTime = Time.time + Mathf.Max(0f, _holdBeforeWalk);

        if (_appearAudioSource != null && _appearSound != null) _appearAudioSource.PlayOneShot(_appearSound);
        if (_chaseLoop != null) { _chaseLoop.loop = true; if (!_chaseLoop.isPlaying) _chaseLoop.Play(); }

        Debug.Log("[MonsterChaser] Aparece parado; camina en " + _holdBeforeWalk + "s.");
    }

    /// <summary>Cablear desde LevelDoor._onOpen (al llegar a la salida).</summary>
    public void StopChase()
    {
        _chasing = false;
        enabled = false;
        if (_loopFade != null) { StopCoroutine(_loopFade); _loopFade = null; }
        if (_chaseLoop != null) _chaseLoop.Stop();
        if (_appearAudioSource != null) _appearAudioSource.Stop();
        Debug.Log("[MonsterChaser] Persecucion cortada (llegaste a la salida).");
    }

    private void Update()
    {
        if (!_chasing || _caught)
        {
            return;
        }

        Transform t = _target;
        if (t == null)
        {
            // Se cachea la camara: evita consultar Camera.main en cada frame.
            if (_cachedMainCam == null && Camera.main != null) _cachedMainCam = Camera.main.transform;
            t = _cachedMainCam;
        }
        if (t == null)
        {
            return;
        }

        Vector3 playerFlat = t.position;
        playerFlat.y = _fixedY;

        float distToPlayer = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(t.position.x, t.position.z));

        bool walking = Time.time >= _walkStartTime;

        // A donde va este frame: derecho al jugador (tramo final) o a la miga
        // mas vieja del rastro que dejo el jugador.
        Vector3 goal = playerFlat;
        bool followingTrail = walking && distToPlayer > _directChaseRange;
        if (followingTrail)
        {
            if (_trail.Count == 0 || Vector3.Distance(_trail[_trail.Count - 1], playerFlat) >= _trailSpacing)
            {
                _trail.Add(playerFlat);
            }
            while (_trail.Count > 1 && Vector3.Distance(transform.position, _trail[0]) <= _trailSpacing * 0.6f)
            {
                _trail.RemoveAt(0);
            }
            goal = _trail[0];
        }

        UpdatePlayerSpeed(t.position);
        float speed = ComputeSpeed(walking, playerFlat, distToPlayer, followingTrail);
        CurrentSpeed = speed;

        // Primero parado (compas de espera): solo gira a mirarte. Despues camina.
        if (walking)
        {
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
        }

        Vector3 dir = (walking ? goal : playerFlat) - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 4f * Time.deltaTime);
        }

        // No se chequea la captura durante la espera inicial (parado).
        if (walking && distToPlayer <= _catchDistance)
        {
            _caught = true;
            _chasing = false;
            // Deja de moverse (Update ya no corre) y funde el loop de persecucion.
            if (_chaseLoop != null && _chaseLoop.isPlaying)
            {
                _loopFade = StartCoroutine(FadeOutLoop(0.5f));
            }
            Debug.Log("[MonsterChaser] Te alcanzo.");
            _onCaught?.Invoke();
        }
    }

    private TeleportManager GetTeleportManager()
    {
        if (_teleportManager == null && !_teleportManagerSearched)
        {
            _teleportManagerSearched = true;
            _teleportManager = FindFirstObjectByType<TeleportManager>();
        }
        return _teleportManager;
    }

    /// <summary>Promedio movil exponencial de la velocidad horizontal del objetivo.</summary>
    private void UpdatePlayerSpeed(Vector3 targetPos)
    {
        float dt = Time.deltaTime;
        Vector3 d = targetPos - _lastTargetPos;
        d.y = 0f;
        _lastTargetPos = targetPos;
        if (!_hasLastTargetPos)
        {
            _hasLastTargetPos = true;
            return;
        }
        if (dt <= 0f)
        {
            return;
        }

        // Saltos instantaneos (fade, loop sin fade) no son velocidad de
        // caminata: se descartan.
        float instant = d.magnitude / dt;
        float walk = WalkSpeed();
        if (instant > (walk > 0f ? walk * 2f : 10f))
        {
            return;
        }

        float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(0.1f, _playerSpeedWindow));
        _playerAvgSpeed += (instant - _playerAvgSpeed) * alpha;
    }

    private float WalkSpeed()
    {
        TeleportManager tm = GetTeleportManager();
        return tm != null ? tm.WalkSpeed : 0f;
    }

    private float ComputeSpeed(bool walking, Vector3 playerFlat, float distToPlayer, bool followingTrail)
    {
        if (!walking)
        {
            return _speed;
        }

        float walk = WalkSpeed();
        float max = Mathf.Max(_maxSpeed > 0f ? _maxSpeed : (walk > 0f ? 0.9f * walk : 3f), _speed);

        // Distancia aproximada por el camino: hasta la miga actual + tramos del
        // rastro + ultimo tramo hasta el jugador. Sin rastro, la directa.
        float pathDist = distToPlayer;
        if (followingTrail && _trail.Count > 0)
        {
            pathDist = Vector3.Distance(transform.position, _trail[0]);
            for (int i = 1; i < _trail.Count; i++)
            {
                pathDist += Vector3.Distance(_trail[i - 1], _trail[i]);
            }
            pathDist += Vector3.Distance(_trail[_trail.Count - 1], playerFlat);
        }

        float rubber = Mathf.Max(0f, pathDist - _rubberBandStartDistance) * _rubberBandPerMeter;
        float s = Mathf.Max(_speed, _playerAvgSpeed * _matchFactor) + rubber;

        // Actividad del jugador: 0 = quieto, 1 = caminando a ritmo normal. Quieto,
        // el monstruo apenas se arrastra a _minSpeed.
        float activity = Mathf.Clamp01(_playerAvgSpeed / ((walk > 0f ? walk : 3f) * 0.66f));
        float low = Mathf.Min(_minSpeed, max);
        return Mathf.Clamp(Mathf.Lerp(low, s, activity), low, max);
    }

    /// <summary>Baja el volumen del loop de persecucion y lo corta (tiempo real).</summary>
    private IEnumerator FadeOutLoop(float seconds)
    {
        AudioSource src = _chaseLoop;
        float v0 = src.volume;
        for (float e = 0f; e < seconds && src != null; e += Time.unscaledDeltaTime)
        {
            src.volume = Mathf.Lerp(v0, 0f, e / seconds);
            yield return null;
        }
        if (src != null)
        {
            src.Stop();
            src.volume = v0; // restaurar por si se reusa
        }
        _loopFade = null;
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = visible;
        }
    }
}
