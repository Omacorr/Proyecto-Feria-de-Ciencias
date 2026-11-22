using UnityEngine;

/// <summary>
/// Sonido de tension adaptativo (pedido de Omar 2026-11-11: "el juego ya no
/// pone nervioso"). Mantiene un valor interno de tension 0..1 y lo traduce en
/// tres cosas, SIN gritos ni susurros (solo cuerpo del protagonista):
///
/// 1. Latidos 2D en loop: volumen y pitch siguen la tension (suavizados).
/// 2. Respiracion 3D unos centimetros detras de la cabeza, cada vez mas
///    seguido con mas tension (nunca mas que el minimo configurable).
/// 3. Silencio repentino: de vez en cuando el ambiente y los latidos caen a
///    casi cero, se sostienen unos segundos y vuelven. El silencio es lo que
///    hace que el siguiente sonido asuste.
///
/// Uso: agregar a un GameObject vacio, asignar clips (todos opcionales) y
/// listo. Otros scripts empujan la tension con AddTension/SetTension desde un
/// UnityEvent. Los finales llaman Stop() / Resume().
///
/// Por que la tension sube con el tiempo quieto: en Cardboard el jugador solo
/// se mueve mirando puntos; quedarse mirando sin avanzar es el momento de
/// mas incomodidad. Moverse alivia un poco; el "piso" crece con los minutos
/// para que el nivel nunca se sienta totalmente seguro.
///
/// Se pausa (no suma ni dispara nada) durante desmayos (FaintOverlay) y con
/// la mirada bloqueada (GazeController.IsBlocked, p. ej. cinematicas).
/// Usa Time.deltaTime, asi que respeta Time.timeScale.
/// </summary>
public class TensionAudio : MonoBehaviour
{
    [Header("Tension")]
    [Tooltip("Tension al empezar la escena (0 a 1).")]
    [Range(0f, 1f)]
    [SerializeField] private float _initialTension = 0.1f;
    [Tooltip("Segundos quieto (sin teletransportarse) que hacen falta para subir la tension de 0 a 1.")]
    [SerializeField] private float _secondsStillToMax = 90f;
    [Tooltip("Cuanta tension se resta al llegar a un punto nuevo (0 a 1).")]
    [Range(0f, 1f)]
    [SerializeField] private float _dropOnMove = 0.12f;
    [Tooltip("Piso de tension al que se llega con el tiempo en el nivel: la tension nunca baja de este valor.")]
    [Range(0f, 1f)]
    [SerializeField] private float _floorAtMaxTime = 0.45f;
    [Tooltip("Minutos en el nivel para llegar al piso maximo (el piso crece linealmente).")]
    [SerializeField] private float _minutesToMaxFloor = 8f;
    [Tooltip("Tope de tension (0 a 1). Menos de 1 deja margen para que los eventos del guion la empujen con SetTension.")]
    [Range(0f, 1f)]
    [SerializeField] private float _maxTension = 1f;
    [Tooltip("Distancia en XZ (metros) que debe moverse la camara para contar como 'se movio' aunque no cambie de punto (por ejemplo una caminata o un loop).")]
    [SerializeField] private float _moveDistance = 0.5f;
    [Tooltip("TeleportManager de la escena. Vacio = se busca uno al arrancar.")]
    [SerializeField] private TeleportManager _teleportManager;
    [Tooltip("Cabeza del jugador. Vacio = Camera.main.")]
    [SerializeField] private Transform _listener;

    [Header("Latidos")]
    [Tooltip("Clip de latidos (se usa en loop). Vacio = sin latidos.")]
    [SerializeField] private AudioClip _heartbeatClip;
    [Tooltip("Debajo de esta tension no se oyen los latidos.")]
    [Range(0f, 1f)]
    [SerializeField] private float _heartbeatThreshold = 0.2f;
    [Tooltip("Volumen de los latidos con tension 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float _heartbeatMaxVolume = 0.6f;
    [Tooltip("Pitch (velocidad) de los latidos: x = tension baja, y = tension maxima.")]
    [SerializeField] private Vector2 _heartbeatPitch = new Vector2(0.85f, 1.35f);
    [Tooltip("Que tan rapido siguen los latidos a la tension (mayor = mas rapido).")]
    [SerializeField] private float _heartbeatSmoothing = 1.5f;

    [Header("Respiracion detras")]
    [Tooltip("Clips de respiracion (3D, detras del jugador). Vacio = sin respiracion.")]
    [SerializeField] private AudioClip[] _breathClips;
    [Range(0f, 1f)]
    [SerializeField] private float _breathVolume = 0.8f;
    [Tooltip("Distancia detras de la camara (metros, min/max).")]
    [SerializeField] private Vector2 _breathDistance = new Vector2(0.6f, 1f);
    [Tooltip("Debajo de esta tension no hay respiracion.")]
    [Range(0f, 1f)]
    [SerializeField] private float _breathMinTension = 0.25f;
    [Tooltip("Nunca mas seguido que esto (segundos entre respiraciones, incluso con tension 1).")]
    [SerializeField] private float _breathMinGap = 25f;
    [Tooltip("Espera entre respiraciones con tension baja (segundos); con tension 1 baja hasta 'Breath Min Gap'.")]
    [SerializeField] private float _breathMaxGap = 70f;

    [Header("Silencio repentino")]
    [Tooltip("AudioSources de ambiente (zumbido, musica, etc.) que bajan durante el silencio. Se restauran siempre a su volumen original.")]
    [SerializeField] private AudioSource[] _ambientSources;
    [Tooltip("Segundos entre silencios (min/max). El reloj solo corre con tension alta.")]
    [SerializeField] private Vector2 _silenceEvery = new Vector2(60f, 110f);
    [Tooltip("Solo hay silencios con tension mayor a esto.")]
    [Range(0f, 1f)]
    [SerializeField] private float _silenceMinTension = 0.4f;
    [Tooltip("Cuanto dura el silencio ya instalado (segundos, min/max).")]
    [SerializeField] private Vector2 _silenceHold = new Vector2(2f, 4f);
    [Tooltip("Fraccion del volumen que queda durante el silencio (casi cero).")]
    [Range(0f, 0.2f)]
    [SerializeField] private float _silenceLevel = 0.03f;
    [SerializeField] private float _silenceFadeOut = 0.3f;
    [SerializeField] private float _silenceFadeIn = 1.5f;

    [Header("Final")]
    [Tooltip("Duracion del fundido a silencio de Stop() y de la vuelta de Resume() (segundos).")]
    [SerializeField] private float _stopFade = 1f;

    private enum SilencePhase { Waiting, Falling, Holding, Rising }

    private float _tension;
    private float _stillSeconds;
    private float _levelSeconds;
    private TeleportPoint _lastPoint;
    private Vector3 _lastPos;
    private bool _hasLastPos;

    private AudioSource _heart;
    private AudioSource _breath;
    private float _heartSmoothedTension;
    private float _breathTimer;
    private float _breathSide = 1f;

    private SilencePhase _phase = SilencePhase.Waiting;
    private float _silenceTimer;
    private float _silenceFactor = 1f;

    // 1 = normal, 0 = Stop(). Se mueve hacia _masterTarget en _stopFade segundos.
    private float _master = 1f;
    private float _masterTarget = 1f;

    private float[] _ambientOriginal;
    private bool _ambientTouched;
    private Camera _cam;

    /// <summary>Tension actual (0..1).</summary>
    public float Tension => _tension;

    private void Awake()
    {
        _heart = CreateSource("Latidos (tension)", 0f, 1f, 500f);
        _heart.loop = true;
        _breath = CreateSource("Respiracion (tension)", 1f, 0.3f, 6f);
        _tension = Mathf.Clamp(_initialTension, 0f, _maxTension);
        _heartSmoothedTension = _tension;
    }

    private void OnEnable()
    {
        _levelSeconds = 0f;
        _stillSeconds = 0f;
        _hasLastPos = false;
        _lastPoint = null;
        _phase = SilencePhase.Waiting;
        _silenceFactor = 1f;
        _silenceTimer = Random.Range(_silenceEvery.x, _silenceEvery.y);
        _breathTimer = Random.Range(_breathMaxGap * 0.5f, _breathMaxGap);

        if (_teleportManager == null)
        {
            _teleportManager = FindFirstObjectByType<TeleportManager>();
        }

        // Volumenes originales: se guardan al activar y se restauran en OnDisable.
        int n = _ambientSources != null ? _ambientSources.Length : 0;
        if (_ambientOriginal == null || _ambientOriginal.Length != n)
        {
            _ambientOriginal = new float[n];
        }
        for (int i = 0; i < n; i++)
        {
            _ambientOriginal[i] = _ambientSources[i] != null ? _ambientSources[i].volume : 0f;
        }
        _ambientTouched = false;

        if (_heartbeatClip != null)
        {
            _heart.clip = _heartbeatClip;
            _heart.volume = 0f;
            _heart.pitch = _heartbeatPitch.x;
            _heart.Play();
        }
    }

    private void OnDisable()
    {
        RestoreAmbient();
        _silenceFactor = 1f;
        _phase = SilencePhase.Waiting;
        if (_heart != null)
        {
            _heart.Stop();
        }
        if (_breath != null)
        {
            _breath.Stop();
        }
    }

    /// <summary>Suma (o resta, con negativo) tension. Para cablear desde UnityEvents.</summary>
    public void AddTension(float amount)
    {
        _tension = Mathf.Clamp(_tension + amount, 0f, _maxTension);
    }

    /// <summary>Fija la tension (0..1). Para cablear desde UnityEvents.</summary>
    public void SetTension(float value)
    {
        _tension = Mathf.Clamp(value, 0f, _maxTension);
    }

    /// <summary>Fundido de todo (latidos, respiracion, ambiente asignado) a silencio. Para los finales.</summary>
    public void Stop()
    {
        _masterTarget = 0f;
    }

    /// <summary>Deshace Stop(): todo vuelve con el mismo fundido.</summary>
    public void Resume()
    {
        _masterTarget = 1f;
    }

    private AudioSource CreateSource(string objName, float spatialBlend, float minDistance, float maxDistance)
    {
        var go = new GameObject(objName);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = spatialBlend;
        s.dopplerLevel = 0f; // el Doppler chirriaria en los saltos del loop de Parte 4
        s.spread = 0f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = minDistance;
        s.maxDistance = maxDistance;
        s.priority = 64;
        return s;
    }

    private bool IsPaused()
    {
        return FaintOverlay.IsRunning || GazeController.IsBlocked;
    }

    private Transform Head()
    {
        if (_listener != null)
        {
            return _listener;
        }
        if (_cam == null)
        {
            _cam = Camera.main; // cacheada: Camera.main no se pide por frame
        }
        return _cam != null ? _cam.transform : null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // El fundido de Stop/Resume y el suavizado de audio corren siempre,
        // para que pausar no corte nada de golpe.
        _master = Mathf.MoveTowards(_master, _masterTarget, dt / Mathf.Max(0.05f, _stopFade));

        if (!IsPaused() && _masterTarget > 0f)
        {
            UpdateTension(dt);
            UpdateSilence(dt);
            UpdateBreath(dt);
        }
        else if (_phase != SilencePhase.Waiting && IsPaused())
        {
            // Pausa a mitad de un silencio: no se queda mudo; sigue el ciclo
            // normal pero el reloj de espera no avanza (ver UpdateSilence).
        }

        ApplyVolumes(dt);
    }

    private void UpdateTension(float dt)
    {
        _levelSeconds += dt;

        Transform head = Head();
        TeleportPoint point = _teleportManager != null ? _teleportManager.CurrentOccupiedPoint : null;
        bool moved = false;

        if (point != _lastPoint)
        {
            // Primer valor conocido no cuenta como movimiento; un cambio real si.
            moved = _lastPoint != null || (point != null && _hasLastPos);
            _lastPoint = point;
        }
        if (head != null)
        {
            Vector3 p = head.position;
            if (!_hasLastPos)
            {
                _lastPos = p;
                _hasLastPos = true;
            }
            else
            {
                float dx = p.x - _lastPos.x;
                float dz = p.z - _lastPos.z;
                if (dx * dx + dz * dz > _moveDistance * _moveDistance)
                {
                    moved = true;
                }
            }
            if (moved)
            {
                _lastPos = p;
            }
        }

        if (moved)
        {
            _stillSeconds = 0f;
            _tension = Mathf.Max(0f, _tension - _dropOnMove);
        }
        else if (_teleportManager == null || !_teleportManager.IsTeleporting)
        {
            _stillSeconds += dt;
            _tension += dt / Mathf.Max(1f, _secondsStillToMax);
        }

        // Piso que crece con el tiempo en el nivel: nunca se siente seguro.
        float floor = _floorAtMaxTime * Mathf.Clamp01(_levelSeconds / Mathf.Max(1f, _minutesToMaxFloor * 60f));
        _tension = Mathf.Clamp(_tension, Mathf.Min(floor, _maxTension), _maxTension);
    }

    private void UpdateBreath(float dt)
    {
        if (_breathClips == null || _breathClips.Length == 0 || _tension < _breathMinTension)
        {
            return;
        }
        _breathTimer -= dt;
        if (_breathTimer > 0f)
        {
            return;
        }
        Transform head = Head();
        if (head == null)
        {
            return;
        }

        // Proxima espera: mas corta con mas tension, nunca menor al minimo.
        float gap = Mathf.Lerp(_breathMaxGap, _breathMinGap, _tension);
        gap = Mathf.Max(_breathMinGap, gap);
        _breathTimer = gap * Random.Range(0.85f, 1.15f);
        _breathTimer = Mathf.Max(_breathMinGap, _breathTimer);

        // Detras de la cabeza (plano horizontal), un poco al costado, alternando lados.
        Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f)
        {
            fwd = Vector3.forward;
        }
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        _breathSide = -_breathSide;
        Vector3 pos = head.position - fwd * Random.Range(_breathDistance.x, _breathDistance.y)
                      + right * (_breathSide * 0.25f);
        pos.y = head.position.y - 0.15f;

        AudioClip clip = _breathClips[Random.Range(0, _breathClips.Length)];
        if (clip == null)
        {
            return;
        }
        _breath.transform.position = pos;
        _breath.clip = clip;
        _breath.pitch = Random.Range(0.92f, 1.05f);
        _breath.Play();
    }

    private void UpdateSilence(float dt)
    {
        switch (_phase)
        {
            case SilencePhase.Waiting:
                // El reloj solo corre con tension alta.
                if (_tension > _silenceMinTension)
                {
                    _silenceTimer -= dt;
                    if (_silenceTimer <= 0f)
                    {
                        _phase = SilencePhase.Falling;
                    }
                }
                break;
            case SilencePhase.Falling:
                _silenceFactor = Mathf.MoveTowards(_silenceFactor, _silenceLevel,
                    dt * (1f - _silenceLevel) / Mathf.Max(0.05f, _silenceFadeOut));
                if (_silenceFactor <= _silenceLevel)
                {
                    _silenceTimer = Random.Range(_silenceHold.x, _silenceHold.y);
                    _phase = SilencePhase.Holding;
                }
                break;
            case SilencePhase.Holding:
                _silenceTimer -= dt;
                if (_silenceTimer <= 0f)
                {
                    _phase = SilencePhase.Rising;
                }
                break;
            case SilencePhase.Rising:
                _silenceFactor = Mathf.MoveTowards(_silenceFactor, 1f,
                    dt * (1f - _silenceLevel) / Mathf.Max(0.05f, _silenceFadeIn));
                if (_silenceFactor >= 1f)
                {
                    _silenceTimer = Random.Range(_silenceEvery.x, _silenceEvery.y);
                    _phase = SilencePhase.Waiting;
                }
                break;
        }
    }

    private void ApplyVolumes(float dt)
    {
        float factor = _silenceFactor * _master;

        // Latidos: siguen la tension suavizados; debajo del umbral, mudos.
        if (_heartbeatClip != null)
        {
            _heartSmoothedTension = Mathf.Lerp(_heartSmoothedTension, _tension, 1f - Mathf.Exp(-_heartbeatSmoothing * dt));
            float t = Mathf.InverseLerp(_heartbeatThreshold, 1f, _heartSmoothedTension);
            _heart.volume = _heartbeatMaxVolume * t * factor;
            _heart.pitch = Mathf.Lerp(_heartbeatPitch.x, _heartbeatPitch.y, Mathf.Clamp01(_heartSmoothedTension));
        }

        // La respiracion solo sigue el fundido de Stop (no el silencio: si
        // suena justo despues del silencio es parte del susto).
        if (_breath.isPlaying)
        {
            _breath.volume = _breathVolume * _master;
        }

        // Ambiente asignado: solo se toca mientras el factor != 1 (o una vez
        // al volver a 1, para restaurar exacto).
        if (_ambientSources == null)
        {
            return;
        }
        if (factor < 0.999f)
        {
            for (int i = 0; i < _ambientSources.Length; i++)
            {
                if (_ambientSources[i] != null)
                {
                    _ambientSources[i].volume = _ambientOriginal[i] * factor;
                }
            }
            _ambientTouched = true;
        }
        else if (_ambientTouched)
        {
            RestoreAmbient();
        }
    }

    private void RestoreAmbient()
    {
        if (_ambientSources != null && _ambientOriginal != null)
        {
            int n = Mathf.Min(_ambientSources.Length, _ambientOriginal.Length);
            for (int i = 0; i < n; i++)
            {
                if (_ambientSources[i] != null)
                {
                    _ambientSources[i].volume = _ambientOriginal[i];
                }
            }
        }
        _ambientTouched = false;
    }
}
