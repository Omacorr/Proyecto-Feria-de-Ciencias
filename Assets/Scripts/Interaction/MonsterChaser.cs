using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cumpleaños (Parte 2): el monstruo de la pelicula. Arranca quieto/oculto.
/// StartChase() lo activa (se cablea desde TVMovie._onMovieEnd) y desde ahi
/// persigue al jugador a velocidad CONSTANTE y LENTA (a proposito: en este
/// juego moverse tarda, asi que el monstruo tiene que ir despacio para que la
/// persecucion sea tensa pero ganable).
///
/// Si alcanza al jugador (_catchDistance), dispara _onCaught UNA vez -> ahi se
/// cablea el "te agarro" (reiniciar la escena, volver al menu, jumpscare, etc.).
/// StopChase() lo frena (cablearlo desde LevelDoor._onOpen al llegar a la salida).
/// </summary>
public class MonsterChaser : MonoBehaviour
{
    [Header("Objetivo")]
    [Tooltip("A quien persigue. Si se deja vacio, usa Camera.main en runtime.")]
    [SerializeField] private Transform _target;

    [Header("Movimiento")]
    [Tooltip("Metros por segundo. LENTO a proposito (0.5 - 0.9 anda bien).")]
    [SerializeField] private float _speed = 0.7f;

    [Header("Velocidad adaptativa")]
    [Tooltip("Tildado: la velocidad se adapta a la del jugador (promedio de los ultimos segundos) y sube si se queda muy atras. 'Speed' pasa a ser la velocidad MINIMA (arranque lento). Destildado: velocidad constante como antes.")]
    [SerializeField] private bool _adaptToPlayerSpeed = true;

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

    [Tooltip("Opcional. TeleportManager del jugador (para saber su velocidad de caminata). Vacio = se busca uno en la escena.")]
    [SerializeField] private TeleportManager _teleportManager;

    [Header("Persecucion")]
    [Tooltip("Distancia (m, horizontal) a la que se considera que te alcanzo.")]
    [SerializeField] private float _catchDistance = 1.4f;

    [Tooltip("Mantener el monstruo a una altura Y fija. Se BLOQUEA en la Y que tenga el monstruo al empezar la persecucion (donde lo puso ChaseReveal), asi no se hunde en el piso ni flota.")]
    [SerializeField] private bool _keepYFixed = true;
    [SerializeField] private float _fixedY = 0f;

    [Tooltip("Girar para mirar al jugador mientras persigue.")]
    [SerializeField] private bool _faceTarget = true;

    [Header("Rastro (para no atravesar paredes)")]
    [Tooltip("En vez de ir en linea recta al jugador (que lo hace atravesar paredes, porque el mapa no tiene colliders), el monstruo sigue el CAMINO que hizo el jugador: va pisando las 'migas' que el jugador va dejando. Como el jugador solo se mueve por puntos validos, el monstruo tambien.")]
    [SerializeField] private bool _followTrail = true;

    [Tooltip("Cada cuantos metros se deja una miga del recorrido del jugador.")]
    [SerializeField] private float _trailSpacing = 1.2f;

    [Tooltip("Si el monstruo esta a menos de esta distancia del jugador, va derecho a el (tramo final), sin importar el rastro.")]
    [SerializeField] private float _directChaseRange = 3f;

    [Header("Aparicion")]
    [Tooltip("Segundos que el monstruo se queda PARADO (girando a mirarte) despues de aparecer, antes de empezar a caminar.")]
    [SerializeField] private float _holdBeforeWalk = 1f;

    [Tooltip("Al aparecer, subir el modelo para que los PIES queden apoyados en el piso. Corrige modelos cuyo pivote esta en el centro del cuerpo (si no, se ve 'hundido/arrastrandose').")]
    [SerializeField] private bool _snapFeetToGround = true;

    [Header("Modelo visual")]
    [Tooltip("El nodo del modelo importado (si se deja vacio, el primer hijo). Se usa para el meneo y para la correccion de orientacion.")]
    [SerializeField] private Transform _visualRoot;

    [Tooltip("Correccion de orientacion del modelo. Si el bicho aparece ACOSTADO o DADO VUELTA, ajusta esto (p.ej. (-90,0,0) para pararlo, (0,180,0) para que mire para el otro lado). (0,0,0) = no toca nada.")]
    [SerializeField] private Vector3 _visualLocalEuler;

    [Header("Animacion / look (opcional)")]
    [Tooltip("Animator del modelo. Si tiene controller + params, se le setea el float y el bool de abajo mientras camina. Vacio = se busca en los hijos.")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _animSpeedParam = "Speed";
    [SerializeField] private string _animWalkParam = "Walking";

    [Tooltip("Componente Animation (LEGACY) — lo que suele traer un .glb animado de Sketchfab importado con glTFast. Si tiene, se reproduce el clip de abajo mientras camina. Vacio = se busca en los hijos.")]
    [SerializeField] private Animation _legacyAnimation;
    [Tooltip("Nombre del clip de caminar dentro del Animation legacy. Vacio = reproduce el clip por defecto.")]
    [SerializeField] private string _walkClipName = "";

    [Tooltip("Meneo vertical procedural mientras camina. Solo como PLAN B para un modelo SIN animacion (si no, parece que flota deslizando). Apagalo cuando pongas el modelo animado.")]
    [SerializeField] private bool _bobWhileWalking = true;
    [SerializeField] private float _bobAmplitude = 0.05f;
    [SerializeField] private float _bobFrequency = 6f;

    [Header("Estado inicial")]
    [Tooltip("Ocultar el objeto hasta StartChase().")]
    [SerializeField] private bool _hiddenUntilChase = true;

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
    private readonly List<Vector3> _trail = new List<Vector3>();
    private float _bobBaseLocalY;
    private Coroutine _loopFade;
    private float _playerAvgSpeed;
    private Vector3 _lastTargetPos;
    private bool _hasLastTargetPos;
    private bool _teleportManagerSearched;

    /// <summary>Velocidad actual del monstruo en m/s (para depurar).</summary>
    public float CurrentSpeed { get; private set; }

    private void Awake()
    {
        if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
        if (_legacyAnimation == null) _legacyAnimation = GetComponentInChildren<Animation>(true);
        if (_visualRoot == null && transform.childCount > 0) _visualRoot = transform.GetChild(0);

        if (_visualRoot != null)
        {
            if (_visualLocalEuler != Vector3.zero) _visualRoot.localEulerAngles = _visualLocalEuler;
            _bobBaseLocalY = _visualRoot.localPosition.y;
        }

        if (_hiddenUntilChase)
        {
            SetVisible(false);
        }
        enabled = false; // no corre Update hasta StartChase
    }

    /// <summary>Cablear desde TVMovie._onMovieEnd.</summary>
    public void StartChase()
    {
        if (_chasing || _caught)
        {
            return;
        }
        _chasing = true;
        enabled = true;
        SetVisible(true);

        // Subir el modelo para que los pies queden en el piso (la Y que le
        // paso ChaseReveal es la del PISO; si el pivote esta al centro, el
        // cuerpo queda hundido a la mitad -> se ve arrastrandose).
        if (_snapFeetToGround)
        {
            float low = LowestRendererY();
            if (!float.IsInfinity(low))
            {
                transform.position += Vector3.up * (transform.position.y - low);
            }
        }

        // Bloquear esa altura ya corregida. Rastro limpio. Y arrancar el
        // "compas de espera": primero parado, despues camina.
        if (_keepYFixed) _fixedY = transform.position.y;
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
        StopWalkAnimation();
        Debug.Log("[MonsterChaser] Persecucion cortada (llegaste a la salida).");
    }

    private void Update()
    {
        if (!_chasing || _caught)
        {
            return;
        }

        Transform t = _target != null ? _target : (Camera.main != null ? Camera.main.transform : null);
        if (t == null)
        {
            return;
        }

        Vector3 playerFlat = t.position;
        if (_keepYFixed) playerFlat.y = _fixedY;

        float distToPlayer = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(t.position.x, t.position.z));

        bool walking = Time.time >= _walkStartTime;

        // A donde va este frame: derecho al jugador (modo directo o tramo
        // final) o a la miga mas vieja del rastro que dejo el jugador.
        Vector3 goal = playerFlat;
        if (walking && _followTrail && distToPlayer > _directChaseRange)
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
        float speed = ComputeSpeed(walking, goal, playerFlat, distToPlayer);
        CurrentSpeed = speed;

        // Primero parado (compas de espera): solo gira a mirarte. Despues camina.
        if (walking)
        {
            transform.position = Vector3.MoveTowards(transform.position, goal, speed * Time.deltaTime);
        }

        if (_faceTarget)
        {
            Vector3 dir = (walking ? goal : playerFlat) - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir, Vector3.up), 4f * Time.deltaTime);
            }
        }

        // animacion del modelo
        if (_animator != null)
        {
            if (HasParam(_animSpeedParam)) _animator.SetFloat(_animSpeedParam, walking ? speed : 0f);
            if (HasParam(_animWalkParam)) _animator.SetBool(_animWalkParam, walking);
        }
        if (_legacyAnimation != null)
        {
            if (walking && !_legacyAnimation.isPlaying)
            {
                if (!string.IsNullOrEmpty(_walkClipName)) _legacyAnimation.Play(_walkClipName);
                else _legacyAnimation.Play();
            }
            else if (!walking && _legacyAnimation.isPlaying)
            {
                _legacyAnimation.Stop();
            }
        }
        // plan B: meneo procedural para un modelo SIN animacion
        if (_bobWhileWalking && _visualRoot != null)
        {
            float bob = walking ? Mathf.Abs(Mathf.Sin(Time.time * _bobFrequency)) * _bobAmplitude : 0f;
            Vector3 lp = _visualRoot.localPosition;
            _visualRoot.localPosition = new Vector3(lp.x, _bobBaseLocalY + bob, lp.z);
        }

        // No se chequea la captura durante la espera inicial (parado).
        if (walking && distToPlayer <= _catchDistance)
        {
            _caught = true;
            _chasing = false;
            // Deja de moverse (Update ya no corre) y funde el loop de persecucion.
            StopWalkAnimation();
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
        if (!_hasLastTargetPos)
        {
            _lastTargetPos = targetPos;
            _hasLastTargetPos = true;
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        Vector3 d = targetPos - _lastTargetPos;
        d.y = 0f;
        _lastTargetPos = targetPos;
        float instant = d.magnitude / dt;

        // Saltos instantaneos (fade, loop sin fade) no son velocidad de
        // caminata: se descartan.
        TeleportManager tm = GetTeleportManager();
        float plausible = tm != null && tm.WalkSpeed > 0f ? tm.WalkSpeed * 2f : 10f;
        if (instant > plausible)
        {
            return;
        }

        float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(0.1f, _playerSpeedWindow));
        _playerAvgSpeed += (instant - _playerAvgSpeed) * alpha;
    }

    private float ComputeSpeed(bool walking, Vector3 goal, Vector3 playerFlat, float distToPlayer)
    {
        if (!_adaptToPlayerSpeed || !walking)
        {
            return _speed;
        }

        TeleportManager tm = GetTeleportManager();
        float max = _maxSpeed > 0f ? _maxSpeed : (tm != null ? 0.9f * tm.WalkSpeed : 3f);
        max = Mathf.Max(max, _speed);

        // Distancia aproximada por el camino: hasta la miga actual + tramos del
        // rastro + ultimo tramo hasta el jugador. Sin rastro, la directa.
        float pathDist = distToPlayer;
        if (_followTrail && _trail.Count > 0 && distToPlayer > _directChaseRange)
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
        float walk = tm != null && tm.WalkSpeed > 0f ? tm.WalkSpeed : 3f;
        float activity = Mathf.Clamp01(_playerAvgSpeed / (walk * 0.66f));
        float low = Mathf.Min(_minSpeed, max);
        return Mathf.Clamp(Mathf.Lerp(low, s, activity), low, max);
    }

    /// <summary>Baja el volumen del loop de persecucion y lo corta (tiempo real).</summary>
    private System.Collections.IEnumerator FadeOutLoop(float seconds)
    {
        AudioSource src = _chaseLoop;
        float v0 = src.volume;
        float e = 0f;
        while (e < seconds && src != null)
        {
            e += Time.unscaledDeltaTime;
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

    private void StopWalkAnimation()
    {
        if (_animator != null)
        {
            if (HasParam(_animSpeedParam)) _animator.SetFloat(_animSpeedParam, 0f);
            if (HasParam(_animWalkParam)) _animator.SetBool(_animWalkParam, false);
        }
        if (_legacyAnimation != null && _legacyAnimation.isPlaying) _legacyAnimation.Stop();
    }

    private float LowestRendererY()
    {
        float low = float.PositiveInfinity;
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            low = Mathf.Min(low, r.bounds.min.y);
        }
        return low;
    }

    private bool HasParam(string n)
    {
        if (_animator == null || string.IsNullOrEmpty(n)) return false;
        foreach (AnimatorControllerParameter p in _animator.parameters)
        {
            if (p.name == n) return true;
        }
        return false;
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = visible;
        }
    }
}
