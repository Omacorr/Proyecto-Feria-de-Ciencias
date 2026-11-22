using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// El monstruo de "Recuerdos Rotos" (la culpa/trauma que la mente bloquea).
/// Independiente del modelo: solo mueve/gira ESTE Transform y prende/apaga lo
/// visual. Todo lo demas (pasos, mascara, cara) es opcional.
///
/// Estados (se cambian desde cualquier UnityEvent):
///  - Hide():   invisible y quieto.
///  - Watch():  aparece y se queda QUIETO mirando al jugador.
///  - Reveal(): aparece, se acerca hasta Reveal Approach Distance, y tras una
///              pausa se quita la mascara: On Mask Removed (etapa 5).
///
/// Armado minimo: GameObject vacio parado en el PISO (pivote en los pies) con
/// este script y como hijo el modelo mirando a +Z. No usa fisica ni NavMesh: se
/// mueve en linea recta en X/Z manteniendo la altura con la que arranca (o la
/// del marcador de AppearAt).
///
/// "El jugador lo esta mirando" se calcula solo por angulo y distancia, sin
/// chequear paredes (los modelos del mapa no tienen colliders).
/// (Se sacaron Stalk/Charge/NavMesh/Animator: ninguna escena los usaba.)
/// </summary>
public class MonsterController : MonoBehaviour
{
    // Valores explicitos: la escena serializa el enum como int.
    public enum MonsterState { Hidden = 0, Watching = 1, Revealed = 5 }

    [Header("Estado inicial")]
    [Tooltip("Como arranca al cargar la escena. Lo normal es Hidden y hacerlo aparecer por evento.")]
    [SerializeField] private MonsterState _startState = MonsterState.Hidden;

    [Header("Referencias (todas opcionales)")]
    [Tooltip("A quien mira: Main Camera. Vacio = Camera.main.")]
    [SerializeField] private Transform _player;
    [Tooltip("Lo que se prende/apaga al aparecer/esconderse. Vacio = todos los Renderer hijos. No poner este mismo objeto.")]
    [SerializeField] private GameObject _visualRoot;

    [Header("Movimiento")]
    [Tooltip("Velocidad al acercarse en Reveal (m/s). Lento a proposito.")]
    [SerializeField] private float _walkSpeed = 0.9f;
    [Tooltip("Grados por segundo que gira para encarar al jugador o hacia donde camina.")]
    [SerializeField] private float _turnSpeed = 240f;

    [Header("Mirada del jugador")]
    [Tooltip("Altura (sobre el pivote) del punto que el jugador tiene que mirar: la cabeza del monstruo.")]
    [SerializeField] private float _headHeight = 1.7f;
    [Tooltip("Angulo dentro del cual el jugador lo esta mirando DE FRENTE (On First Seen).")]
    [SerializeField] private float _stareAngle = 15f;
    [Tooltip("Mas lejos que esto no cuenta como visto.")]
    [SerializeField] private float _seenMaxDistance = 40f;

    [Header("Revelacion / mascara (etapa 5)")]
    [Tooltip("Al llamar Reveal(), primero camina hacia el jugador hasta quedar a esta distancia. 0 = aparece y se queda donde esta.")]
    [SerializeField] private float _revealApproachDistance;
    [Tooltip("Segundos quieto frente al jugador antes de quitarse la mascara.")]
    [SerializeField] private float _maskRemoveDelay = 2f;
    [Tooltip("La mascara como objeto hijo separado (opcional): se la 'saca' moviendola y despues se apaga.")]
    [SerializeField] private GameObject _maskObject;
    [Tooltip("Desplazamiento LOCAL (respecto del padre de la mascara) mientras se la saca.")]
    [SerializeField] private Vector3 _maskMoveOffset = new Vector3(0f, -0.35f, 0.3f);
    [Tooltip("Cuanto tarda en sacarse la mascara (y cuando se dispara On Mask Removed).")]
    [SerializeField] private float _maskRemoveDuration = 0.8f;
    [Tooltip("Objeto que se prende al sacarse la mascara (la cara real, opcional).")]
    [SerializeField] private GameObject _revealedFace;

    [Header("Pasos (opcional)")]
    [Tooltip("AudioSource 3D en el monstruo (Play On Awake destildado).")]
    [SerializeField] private AudioSource _footstepSource;
    [Tooltip("Clip de pasos en LOOP: suena mientras se mueve y se corta al frenar.")]
    [SerializeField] private AudioClip _footstepLoopClip;
    [Tooltip("Pitch del loop (<1 = pasos mas lentos y pesados).")]
    [SerializeField] private float _loopPitchWalk = 0.75f;

    [Header("Eventos")]
    [SerializeField] private UnityEvent _onAppear;
    [Tooltip("La primera vez que el jugador lo mira de frente desde que aparecio.")]
    [SerializeField] private UnityEvent _onFirstSeen;
    [Tooltip("Cuando queda frente al jugador en Reveal(), antes de la pausa.")]
    [SerializeField] private UnityEvent _onReveal;
    [Tooltip("Cuando termina de quitarse la mascara (parpadeo rapido, pitido de hospital, final).")]
    [SerializeField] private UnityEvent _onMaskRemoved;

    public MonsterState State { get; private set; } = MonsterState.Hidden;

    private const float RevealApproachTolerance = 0.01f;
    private Renderer[] _renderers;
    private Transform _cachedMainCam;
    private bool _visible;
    private float _groundY;
    private Vector3 _lastPosition;
    private float _currentSpeed;
    private bool _firstSeenFired;
    private bool _revealWaiting;
    private Coroutine _revealRoutine;
    private Coroutine _maskRoutine;

    // ---------------- Ciclo de vida ----------------

    private void Awake()
    {
        if (_visualRoot == gameObject)
        {
            Debug.LogWarning("[MonsterController] Visual Root no puede ser el mismo objeto del script (se apagaria el script). Se usan los Renderer hijos.");
            _visualRoot = null;
        }
        _renderers = GetComponentsInChildren<Renderer>(true);
        _groundY = transform.position.y;
        _lastPosition = transform.position;
        if (_revealedFace != null)
        {
            _revealedFace.SetActive(false);
        }
    }

    private void Start()
    {
        switch (_startState)
        {
            case MonsterState.Watching: Watch(); break;
            case MonsterState.Revealed: Reveal(); break;
            default: SetVisible(false); break;
        }
    }

    private void Update()
    {
        Transform player = GetPlayer();
        if (player == null || State == MonsterState.Hidden)
        {
            _currentSpeed = 0f;
            return;
        }

        if (_visible && !_firstSeenFired && IsStaredAt(player))
        {
            _firstSeenFired = true;
            _onFirstSeen?.Invoke();
        }

        if (State == MonsterState.Revealed && _revealWaiting)
        {
            UpdateApproach(player);
        }
        else
        {
            FaceDirection(player.position - transform.position);
        }

        float dt = Time.deltaTime;
        _currentSpeed = dt > 0f ? FlatDistance(transform.position, _lastPosition) / dt : 0f;
        _lastPosition = transform.position;
        UpdateFootsteps();
    }

    // ---------------- API publica (UnityEvent) ----------------

    /// <summary>Aparece donde esta (si estaba escondido, queda observando).</summary>
    public void Appear()
    {
        if (State == MonsterState.Hidden)
        {
            State = MonsterState.Watching;
        }
        EnsureShown();
    }

    /// <summary>Se mueve (sin animacion) al marcador y aparece ahi, mirando hacia donde mira el marcador.</summary>
    public void AppearAt(Transform marker)
    {
        WarpTo(marker);
        Appear();
    }

    /// <summary>Lo mueve de golpe al marcador sin cambiar si se ve o no. La altura del marcador pasa a ser su piso.</summary>
    public void WarpTo(Transform marker)
    {
        if (marker == null)
        {
            Debug.LogWarning("[MonsterController] WarpTo/AppearAt sin marcador.");
            return;
        }
        _groundY = marker.position.y;
        transform.SetPositionAndRotation(marker.position, Quaternion.Euler(0f, marker.eulerAngles.y, 0f));
        _lastPosition = transform.position;
    }

    /// <summary>Desaparece y se queda quieto.</summary>
    public void Hide()
    {
        StopReveal();
        State = MonsterState.Hidden;
        SetVisible(false);
        _currentSpeed = 0f;
        StopFootstepLoop();
    }

    /// <summary>Aparece (si hacia falta) y se queda quieto mirando al jugador.</summary>
    public void Watch()
    {
        StopReveal();
        EnsureShown();
        State = MonsterState.Watching;
    }

    /// <summary>
    /// Aparece (si hacia falta), opcionalmente se acerca hasta Reveal Approach
    /// Distance, dispara On Reveal, espera Mask Remove Delay y se quita la
    /// mascara (RemoveMask).
    /// </summary>
    public void Reveal()
    {
        // Idempotente: en Parte 5 lo llaman dos caminos (On First Seen y el
        // paso "Respaldo" de la secuencia). Sin esto, mirarlo DESPUES del
        // respaldo reiniciaba la revelacion: golpe + parpadeo repetidos.
        if (State == MonsterState.Revealed)
        {
            EnsureShown();
            return;
        }
        StopReveal();
        EnsureShown();
        State = MonsterState.Revealed;
        _revealWaiting = _revealApproachDistance > 0f;
        if (!_revealWaiting)
        {
            _revealRoutine = StartCoroutine(RevealSequence());
        }
    }

    /// <summary>Se quita la mascara ya (mascara hija + cara). Dispara On Mask Removed al terminar.</summary>
    public void RemoveMask()
    {
        if (_maskRoutine == null)
        {
            _maskRoutine = StartCoroutine(RemoveMaskSequence());
        }
    }

    // ---------------- Revelacion ----------------

    private void UpdateApproach(Transform player)
    {
        Vector3 playerFlat = Flat(player.position);
        // Tolerancia de 1 cm: MoveTowards frena EXACTAMENTE en la distancia
        // pedida y, con coordenadas grandes (z = -570 en Parte 5), el redondeo
        // de float puede dejarlo a 2.20003 m para siempre: el final quedaba
        // trabado sin sacarse la mascara.
        if (FlatDistance(transform.position, playerFlat) > _revealApproachDistance + RevealApproachTolerance)
        {
            MoveTowards(playerFlat, _walkSpeed, _revealApproachDistance);
            return;
        }
        _revealWaiting = false;
        _revealRoutine = StartCoroutine(RevealSequence());
    }

    private IEnumerator RevealSequence()
    {
        _onReveal?.Invoke();
        if (_maskRemoveDelay > 0f)
        {
            yield return new WaitForSeconds(_maskRemoveDelay);
        }
        _revealRoutine = null;
        RemoveMask();
    }

    private IEnumerator RemoveMaskSequence()
    {
        if (_maskObject != null && _maskObject.activeInHierarchy)
        {
            Transform mask = _maskObject.transform;
            Vector3 from = mask.localPosition;
            Vector3 to = from + _maskMoveOffset;
            float duration = Mathf.Max(0.01f, _maskRemoveDuration);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                mask.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            _maskObject.SetActive(false);
        }
        else if (_maskRemoveDuration > 0f)
        {
            yield return new WaitForSeconds(_maskRemoveDuration);
        }

        if (_revealedFace != null)
        {
            _revealedFace.SetActive(true);
        }
        Debug.Log("[MonsterController] " + name + " se quito la mascara (On Mask Removed).");
        _onMaskRemoved?.Invoke();
    }

    private void StopReveal()
    {
        if (_revealRoutine != null)
        {
            StopCoroutine(_revealRoutine);
            _revealRoutine = null;
        }
        _revealWaiting = false;
    }

    // ---------------- Movimiento y vista ----------------

    private bool IsStaredAt(Transform player)
    {
        Vector3 toHead = transform.position + Vector3.up * _headHeight - player.position;
        float distance = toHead.magnitude;
        return distance < 0.01f || (distance <= _seenMaxDistance && Vector3.Angle(player.forward, toHead) <= _stareAngle);
    }

    /// <summary>Avanza en X/Z hacia target sin pasar de 'stopAt' metros de distancia, y gira hacia donde camina.</summary>
    private void MoveTowards(Vector3 target, float speed, float stopAt)
    {
        Vector3 position = transform.position;
        Vector3 toTarget = target - position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance <= stopAt || distance < 0.001f)
        {
            return;
        }
        float step = Mathf.Min(distance - stopAt, speed * Time.deltaTime);
        Vector3 direction = toTarget / distance;
        transform.position = new Vector3(position.x, _groundY, position.z) + direction * step;
        FaceDirection(direction);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }
        Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * Time.deltaTime);
    }

    // ---------------- Visual y pasos ----------------

    private void EnsureShown()
    {
        if (_visible)
        {
            return;
        }
        SetVisible(true);
        _firstSeenFired = false;
        _lastPosition = transform.position;
        _onAppear?.Invoke();
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;
        if (_visualRoot != null)
        {
            _visualRoot.SetActive(visible);
            return;
        }
        foreach (Renderer r in _renderers)
        {
            if (r != null)
            {
                r.enabled = visible;
            }
        }
    }

    private void UpdateFootsteps()
    {
        if (_footstepSource == null || _footstepLoopClip == null)
        {
            return;
        }
        if (_currentSpeed <= 0.1f)
        {
            StopFootstepLoop();
            return;
        }
        if (!_footstepSource.isPlaying || _footstepSource.clip != _footstepLoopClip)
        {
            _footstepSource.clip = _footstepLoopClip;
            _footstepSource.loop = true;
            _footstepSource.Play();
        }
        _footstepSource.pitch = _loopPitchWalk;
    }

    private void StopFootstepLoop()
    {
        if (_footstepSource != null && _footstepLoopClip != null && _footstepSource.clip == _footstepLoopClip && _footstepSource.isPlaying)
        {
            _footstepSource.Stop();
        }
    }

    // ---------------- Utilidades ----------------

    private Transform GetPlayer()
    {
        if (_player != null)
        {
            return _player;
        }
        if (_cachedMainCam == null && Camera.main != null)
        {
            _cachedMainCam = Camera.main.transform;
        }
        return _cachedMainCam;
    }

    private Vector3 Flat(Vector3 worldPosition)
    {
        return new Vector3(worldPosition.x, _groundY, worldPosition.z);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
