using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// El monstruo de "Recuerdos Rotos" (la culpa/trauma que la mente bloquea).
/// Controlador independiente del modelo: funciona igual con una capsula de
/// prueba que con el modelo real, porque solo mueve/gira ESTE Transform y
/// prende/apaga lo visual. Todo lo demas (Animator, NavMeshAgent, pasos,
/// mascara) es opcional: si falta, no falla, simplemente no se usa.
///
/// Estados (se cambian llamando los metodos publicos desde cualquier
/// UnityEvent: On Player Arrived de un TeleportPoint, CodeLock.On Unlocked,
/// SequenceManager, On Loop, etc.):
///  - Hide():     invisible y quieto.
///  - Watch():    aparece y se queda QUIETO mirando al jugador (etapa 2,
///                "observa desde el fondo").
///  - Stalk():    camina LENTO detras del jugador manteniendo una distancia
///                (etapa 3). Sigue el RECORRIDO que hizo el jugador (asi
///                dobla las esquinas por donde dobló el jugador en vez de
///                atravesar paredes) y, si el jugador cruzo una puerta por
///                fade, lo sigue "saltando" esa puerta solo cuando el
///                jugador no esta mirando.
///  - Charge():   corre hacia el jugador; justo ANTES de tocarlo dispara
///                On Caught (para enganchar FaintTransition.Trigger) y se
///                frena (etapa 2, final).
///  - Reveal():   aparece frente al jugador (opcionalmente se acerca) y, tras
///                una pausa, se quita la mascara: On Mask Removed (etapa 5).
///
/// Armado minimo: GameObject vacio "Monstruo" parado en el PISO (el pivote
/// en los pies) con este script, y como hijo el modelo (o una Capsule de
/// prueba subida 1 m) mirando hacia la flecha azul (Z) del padre. No usa
/// fisica ni colliders; el mapa no tiene NavMesh, asi que se mueve en linea
/// recta en X/Z y mantiene la altura con la que arranca (o la del marcador de
/// AppearAt). Si algun dia se hornea un NavMesh, asignar un NavMeshAgent y
/// lo usa en su lugar.
///
/// Limitacion conocida: "el jugador lo esta mirando" se calcula solo por
/// angulo y distancia, sin chequear paredes (los modelos del mapa no tienen
/// colliders para hacer un raycast).
/// </summary>
public class MonsterController : MonoBehaviour
{
    public enum MonsterState { Hidden, Watching, Stalking, Charging, Caught, Revealed }

    private struct TrailPoint
    {
        public Vector3 Position;
        // True si el jugador llego a este punto por teletransporte con fade
        // (un salto largo en un frame: puerta, loop), no caminando.
        public bool Jump;
    }

    [Header("Estado inicial")]
    [Tooltip("Como arranca al cargar la escena. Lo normal es Hidden y hacerlo aparecer por evento.")]
    [SerializeField] private MonsterState _startState = MonsterState.Hidden;

    [Header("Referencias (todas opcionales)")]
    [Tooltip("A quien persigue/mira: Main Camera. Vacio = Camera.main.")]
    [SerializeField] private Transform _player;
    [Tooltip("Lo que se prende/apaga al aparecer/esconderse (el modelo o la capsula hija). Vacio = se prenden/apagan todos los Renderer hijos. No poner este mismo objeto.")]
    [SerializeField] private GameObject _visualRoot;
    [Tooltip("Animator del modelo. Vacio = se busca en los hijos; si no hay, no se anima nada. Destildar 'Apply Root Motion': el que mueve al monstruo es este script.")]
    [SerializeField] private Animator _animator;
    [Tooltip("SOLO si hay un NavMesh horneado en la escena (hoy ninguna lo tiene). Si esta asignado y parado sobre el NavMesh, se usa para caminar/correr en vez de la linea recta.")]
    [SerializeField] private NavMeshAgent _navAgent;
    [Tooltip("TeleportManager de la escena, para bloquear el teletransporte al embestir. Vacio = se busca solo.")]
    [SerializeField] private TeleportManager _teleportManager;

    [Header("Movimiento")]
    [Tooltip("Velocidad al acechar (m/s). Lento a proposito.")]
    [SerializeField] private float _walkSpeed = 0.9f;
    [Tooltip("Velocidad al embestir (m/s).")]
    [SerializeField] private float _runSpeed = 6f;
    [Tooltip("Grados por segundo que gira para encarar al jugador o hacia donde camina.")]
    [SerializeField] private float _turnSpeed = 240f;

    [Header("Acechar (Stalk)")]
    [Tooltip("Distancia (medida a lo largo del recorrido del jugador) que mantiene detras de el.")]
    [SerializeField] private float _stalkDistance = 5f;
    [Tooltip("Tildado: sigue el camino que hizo el jugador (dobla donde el doblo). Destildado: va en linea recta hacia el jugador (solo sirve en un pasillo recto o un cuarto abierto).")]
    [SerializeField] private bool _followPlayerTrail = true;
    [Tooltip("Cada cuantos metros se anota un punto del recorrido del jugador.")]
    [SerializeField] private float _trailStep = 0.5f;
    [Tooltip("Si el jugador se mueve mas que esto en un solo frame, se considera un teletransporte con fade (puerta, loop): el monstruo cruza ese tramo de golpe, pero solo cuando el jugador no lo ve.")]
    [SerializeField] private float _teleportJumpDistance = 3f;
    [Tooltip("Tildado (recomendado): al llamar Stalk() NO camina en linea recta desde donde esta (atravesaria paredes); espera a que el jugador se aleje y no mire, y aparece en el punto donde estaba parado el jugador al llamar Stalk(). Desde ahi lo sigue caminando. Si estaba escondido, recien se hace visible en ese momento. Destildado: camina en linea recta hasta ese punto.")]
    [SerializeField] private bool _joinTrailUnseen = true;
    [Tooltip("Tildado: mientras el jugador lo tiene en el campo visual, se congela (estilo 'no le saques los ojos de encima').")]
    [SerializeField] private bool _freezeWhenSeen;

    [Header("Embestir (Charge)")]
    [Tooltip("Distancia al jugador (en X/Z) a la que se considera que 'casi lo toca': ahi se frena y dispara On Caught.")]
    [SerializeField] private float _catchDistance = 1.3f;
    [Tooltip("Seguro: si en estos segundos no llego (por ejemplo el jugador se alejo caminando), dispara On Caught igual. 0 = sin seguro.")]
    [SerializeField] private float _chargeTimeout = 6f;
    [Tooltip("Tildado: al empezar a embestir se bloquea el teletransporte del jugador (TeleportManager.LockMovement).")]
    [SerializeField] private bool _lockPlayerOnCharge = true;

    [Header("Mirada del jugador")]
    [Tooltip("Altura (sobre el pivote) del punto que el jugador tiene que mirar: la cabeza del monstruo.")]
    [SerializeField] private float _headHeight = 1.7f;
    [Tooltip("Angulo desde el centro de la vista dentro del cual el monstruo esta 'en el campo visual' (Cardboard ve ~45 grados para cada lado). Se usa para no saltar puertas a la vista y para Freeze When Seen.")]
    [SerializeField] private float _viewAngle = 50f;
    [Tooltip("Angulo dentro del cual el jugador lo esta mirando DE FRENTE. Se usa para On First Seen y Vanish After Stare Seconds.")]
    [SerializeField] private float _stareAngle = 15f;
    [Tooltip("Mas lejos que esto no cuenta como visto.")]
    [SerializeField] private float _seenMaxDistance = 40f;
    [Tooltip("Si el jugador lo mira de frente esta cantidad de segundos mientras observa o acecha, desaparece (On Vanish). 0 = nunca.")]
    [SerializeField] private float _vanishAfterStareSeconds;

    [Header("Revelacion / mascara (etapa 5)")]
    [Tooltip("Al llamar Reveal(), primero camina hacia el jugador hasta quedar a esta distancia. 0 = aparece y se queda donde esta.")]
    [SerializeField] private float _revealApproachDistance;
    [Tooltip("Segundos quieto frente al jugador antes de quitarse la mascara.")]
    [SerializeField] private float _maskRemoveDelay = 2f;
    [Tooltip("La mascara como objeto hijo separado (opcional). Si no hay animacion, se la 'saca' moviendola y despues se apaga.")]
    [SerializeField] private GameObject _maskObject;
    [Tooltip("Desplazamiento LOCAL (respecto del padre de la mascara) mientras se la saca.")]
    [SerializeField] private Vector3 _maskMoveOffset = new Vector3(0f, -0.35f, 0.3f);
    [Tooltip("Cuanto tarda en sacarse la mascara (y cuando se dispara On Mask Removed).")]
    [SerializeField] private float _maskRemoveDuration = 0.8f;
    [Tooltip("Objeto que se prende al sacarse la mascara (la cara real, opcional).")]
    [SerializeField] private GameObject _revealedFace;

    [Header("Animator: nombres de parametros (vacio = no se usa)")]
    [SerializeField] private string _speedParam = "Speed";
    [SerializeField] private string _walkingBool = "IsWalking";
    [SerializeField] private string _runningBool = "IsRunning";
    [SerializeField] private string _appearTrigger = "";
    [SerializeField] private string _chargeTrigger = "Charge";
    [SerializeField] private string _removeMaskTrigger = "RemoveMask";

    [Header("Pasos (opcional)")]
    [Tooltip("AudioSource 3D en el monstruo (Spatial Blend = 1, Play On Awake destildado).")]
    [SerializeField] private AudioSource _footstepSource;
    [SerializeField] private AudioClip[] _walkStepClips;
    [Tooltip("Vacio = usa los de caminar.")]
    [SerializeField] private AudioClip[] _runStepClips;
    [SerializeField] private float _walkStepInterval = 0.75f;
    [SerializeField] private float _runStepInterval = 0.28f;
    [Tooltip("Alternativa a los clips sueltos: un clip de pasos en LOOP (por ejemplo Assets/Audios/pasos_madera.wav, el mismo tipo que usa TeleportManager). Si se asigna, suena en loop mientras se mueve y se corta al frenar; los clips sueltos de arriba se ignoran.")]
    [SerializeField] private AudioClip _footstepLoopClip;
    [Tooltip("Pitch del loop al caminar (<1 = pasos mas lentos y pesados).")]
    [SerializeField] private float _loopPitchWalk = 0.75f;
    [Tooltip("Pitch del loop al correr.")]
    [SerializeField] private float _loopPitchRun = 1.35f;

    [Header("Eventos")]
    [SerializeField] private UnityEvent _onAppear;
    [Tooltip("La primera vez que el jugador lo mira de frente desde que aparecio.")]
    [SerializeField] private UnityEvent _onFirstSeen;
    [SerializeField] private UnityEvent _onVanish;
    [Tooltip("Al empezar a embestir (musica que se distorsiona, grito...).")]
    [SerializeField] private UnityEvent _onChargeStart;
    [Tooltip("Justo antes de tocar al jugador. Cablear aca FaintTransition.Trigger().")]
    [SerializeField] private UnityEvent _onCaught;
    [Tooltip("Cuando queda frente al jugador en Reveal(), antes de la pausa.")]
    [SerializeField] private UnityEvent _onReveal;
    [Tooltip("Cuando termina de quitarse la mascara (parpadeo rapido, pitido de hospital, final).")]
    [SerializeField] private UnityEvent _onMaskRemoved;

    public MonsterState State { get; private set; } = MonsterState.Hidden;

    /// <summary>True si el monstruo esta dentro del campo visual del jugador (sin chequear paredes).</summary>
    public bool IsInPlayerView { get; private set; }

    /// <summary>True si el jugador lo esta mirando de frente (sin chequear paredes).</summary>
    public bool IsStaredAt { get; private set; }

    private readonly List<TrailPoint> _trail = new List<TrailPoint>();
    private const int MaxTrailPoints = 600;
    private Renderer[] _renderers;
    private readonly Dictionary<string, AnimatorControllerParameterType> _animParams = new Dictionary<string, AnimatorControllerParameterType>();
    private bool _animParamsCached;
    private bool _visible;
    private float _groundY;
    private Vector3 _lastPosition;
    private Vector3 _lastPlayerFlat;
    private bool _hasLastPlayer;
    private float _currentSpeed;
    private float _stepTimer;
    private float _stareTimer;
    private float _chargeTimer;
    private bool _firstSeenFired;
    private bool _caughtFired;
    private bool _revealWaiting;
    private Coroutine _revealRoutine;
    private Coroutine _maskRoutine;

    // ---------------- Ciclo de vida ----------------

    private void Awake()
    {
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>(true);
        }
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
            case MonsterState.Stalking: Stalk(); break;
            case MonsterState.Charging: Charge(); break;
            case MonsterState.Revealed: Reveal(); break;
            default:
                State = MonsterState.Hidden;
                SetVisible(false);
                break;
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

        UpdateSeen(player);

        switch (State)
        {
            case MonsterState.Watching:
                StandStill();
                FaceTowards(player.position);
                break;
            case MonsterState.Stalking:
                UpdateStalk(player);
                break;
            case MonsterState.Charging:
                UpdateCharge(player);
                break;
            case MonsterState.Caught:
                StandStill();
                FaceTowards(player.position);
                break;
            case MonsterState.Revealed:
                UpdateReveal(player);
                break;
        }

        // Puede haber cambiado a Hidden en este mismo frame (Vanish).
        if (State == MonsterState.Hidden)
        {
            return;
        }

        UpdateSpeed();
        UpdateAnimator();
        UpdateFootsteps();
    }

    // ---------------- API publica (UnityEvent) ----------------

    /// <summary>Aparece donde esta, sin cambiar de comportamiento (si estaba escondido, queda observando).</summary>
    public void Appear()
    {
        if (State != MonsterState.Hidden)
        {
            EnsureShown();
            return;
        }
        State = MonsterState.Watching;
        Show();
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
        if (NavAgentActive)
        {
            _navAgent.Warp(marker.position);
        }
        else
        {
            transform.position = marker.position;
        }
        transform.rotation = Quaternion.Euler(0f, marker.eulerAngles.y, 0f);
        _lastPosition = transform.position;
        _trail.Clear();
        _hasLastPlayer = false;
    }

    /// <summary>Desaparece y se queda quieto.</summary>
    public void Hide()
    {
        StopRoutines();
        State = MonsterState.Hidden;
        StandStill();
        SetVisible(false);
        _currentSpeed = 0f;
        UpdateAnimator();
        StopFootstepLoop();
    }

    /// <summary>Aparece (si hacia falta) y se queda quieto mirando al jugador.</summary>
    public void Watch()
    {
        StopRoutines();
        EnsureShown();
        State = MonsterState.Watching;
    }

    /// <summary>Aparece (si hacia falta) y camina lento detras del jugador manteniendo Stalk Distance.</summary>
    public void Stalk()
    {
        StopRoutines();
        bool wasHidden = State == MonsterState.Hidden;
        _trail.Clear();
        _hasLastPlayer = false;
        // Con Join Trail Unseen y estando escondido, sigue invisible hasta que
        // "se une" al recorrido del jugador fuera de su vista (ver UpdateStalk).
        if (!(wasHidden && _joinTrailUnseen && _followPlayerTrail && !NavAgentActive))
        {
            EnsureShown();
        }
        State = MonsterState.Stalking;
    }

    /// <summary>Cambia la distancia de acecho en caliente (por ejemplo, que se acerque despues de cada vuelta del loop).</summary>
    public void SetStalkDistance(float meters)
    {
        _stalkDistance = Mathf.Max(0f, meters);
    }

    /// <summary>Aparece (si hacia falta) y corre hacia el jugador. Justo antes de tocarlo dispara On Caught.</summary>
    public void Charge()
    {
        StopRoutines();
        EnsureShown();
        State = MonsterState.Charging;
        _chargeTimer = 0f;
        _caughtFired = false;

        if (_lockPlayerOnCharge)
        {
            TeleportManager manager = _teleportManager != null ? _teleportManager : FindFirstObjectByType<TeleportManager>();
            if (manager != null)
            {
                manager.LockMovement();
            }
        }

        SetAnimTrigger(_chargeTrigger);
        _onChargeStart?.Invoke();
    }

    /// <summary>
    /// Aparece (si hacia falta), opcionalmente se acerca hasta Reveal Approach
    /// Distance, dispara On Reveal, espera Mask Remove Delay y se quita la
    /// mascara (RemoveMask).
    /// </summary>
    public void Reveal()
    {
        StopRoutines();
        EnsureShown();
        State = MonsterState.Revealed;
        _revealWaiting = _revealApproachDistance > 0f;
        if (!_revealWaiting)
        {
            _revealRoutine = StartCoroutine(RevealSequence());
        }
    }

    /// <summary>Se quita la mascara ya (trigger del Animator + mascara hija + cara). Dispara On Mask Removed al terminar.</summary>
    public void RemoveMask()
    {
        if (_maskRoutine != null)
        {
            return;
        }
        _maskRoutine = StartCoroutine(RemoveMaskSequence());
    }

    // ---------------- Comportamientos ----------------

    private void UpdateStalk(Transform player)
    {
        if (_freezeWhenSeen && IsInPlayerView)
        {
            StandStill();
            return;
        }

        Vector3 playerFlat = Flat(player.position);

        if (NavAgentActive)
        {
            _navAgent.speed = _walkSpeed;
            _navAgent.stoppingDistance = _stalkDistance;
            _navAgent.SetDestination(playerFlat);
            if (!IsNavMoving())
            {
                FaceTowards(player.position);
            }
            return;
        }

        if (!_followPlayerTrail)
        {
            if (FlatDistance(transform.position, playerFlat) > _stalkDistance)
            {
                MoveTowards(playerFlat, _walkSpeed, _stalkDistance);
            }
            else
            {
                FaceTowards(player.position);
            }
            return;
        }

        RecordTrail(playerFlat);

        // Consumir los puntos del recorrido que ya alcanzo (y cruzar puertas
        // de golpe, solo fuera de la vista del jugador).
        while (_trail.Count > 0)
        {
            TrailPoint next = _trail[0];
            if (next.Jump)
            {
                if (PathLengthToPlayer(playerFlat) <= _stalkDistance || IsInPlayerView || IsPointInView(next.Position + Vector3.up * _headHeight, player))
                {
                    break;
                }
                transform.position = next.Position;
                FaceTowards(player.position);
                _lastPosition = transform.position;
                _trail.RemoveAt(0);
                if (!_visible)
                {
                    Show();
                }
                continue;
            }
            if (FlatDistance(transform.position, next.Position) <= 0.15f)
            {
                _trail.RemoveAt(0);
                continue;
            }
            break;
        }

        if (_trail.Count == 0 || _trail[0].Jump || PathLengthToPlayer(playerFlat) <= _stalkDistance)
        {
            FaceTowards(player.position);
            return;
        }

        MoveTowards(_trail[0].Position, _walkSpeed, 0f);
    }

    private void UpdateCharge(Transform player)
    {
        _chargeTimer += Time.deltaTime;
        Vector3 playerFlat = Flat(player.position);
        float distance = FlatDistance(transform.position, playerFlat);

        if (distance <= _catchDistance || (_chargeTimeout > 0f && _chargeTimer >= _chargeTimeout))
        {
            Catch(player);
            return;
        }

        if (NavAgentActive)
        {
            _navAgent.speed = _runSpeed;
            _navAgent.stoppingDistance = _catchDistance * 0.8f;
            _navAgent.SetDestination(playerFlat);
            return;
        }

        MoveTowards(playerFlat, _runSpeed, _catchDistance * 0.8f);
    }

    private void Catch(Transform player)
    {
        State = MonsterState.Caught;
        StandStill();
        FaceTowards(player.position);
        if (_caughtFired)
        {
            return;
        }
        _caughtFired = true;
        Debug.Log("[MonsterController] " + name + " alcanzo al jugador (On Caught).");
        _onCaught?.Invoke();
    }

    private void UpdateReveal(Transform player)
    {
        if (!_revealWaiting)
        {
            StandStill();
            FaceTowards(player.position);
            return;
        }

        Vector3 playerFlat = Flat(player.position);
        if (FlatDistance(transform.position, playerFlat) > _revealApproachDistance)
        {
            if (NavAgentActive)
            {
                _navAgent.speed = _walkSpeed;
                _navAgent.stoppingDistance = _revealApproachDistance;
                _navAgent.SetDestination(playerFlat);
            }
            else
            {
                MoveTowards(playerFlat, _walkSpeed, _revealApproachDistance);
            }
            return;
        }

        _revealWaiting = false;
        StandStill();
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
        SetAnimTrigger(_removeMaskTrigger);

        if (_maskObject != null && _maskObject.activeInHierarchy)
        {
            Transform mask = _maskObject.transform;
            Vector3 from = mask.localPosition;
            Vector3 to = from + _maskMoveOffset;
            float duration = Mathf.Max(0.01f, _maskRemoveDuration);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                mask.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration));
                yield return null;
            }
            _maskObject.SetActive(false);
        }
        else if (_maskRemoveDuration > 0f)
        {
            // Sin mascara separada (solo animacion): esperar lo que dura.
            yield return new WaitForSeconds(_maskRemoveDuration);
        }

        if (_revealedFace != null)
        {
            _revealedFace.SetActive(true);
        }

        Debug.Log("[MonsterController] " + name + " se quito la mascara (On Mask Removed).");
        _onMaskRemoved?.Invoke();
    }

    private void Vanish()
    {
        Debug.Log("[MonsterController] " + name + " desaparece al ser mirado.");
        Hide();
        _onVanish?.Invoke();
    }

    // ---------------- Vista del jugador ----------------

    private void UpdateSeen(Transform player)
    {
        if (!_visible)
        {
            // Todavia invisible (esperando unirse al recorrido): no cuenta como visto.
            IsInPlayerView = false;
            IsStaredAt = false;
            _stareTimer = 0f;
            return;
        }

        Vector3 head = transform.position + Vector3.up * _headHeight;
        IsInPlayerView = IsPointInView(head, player);
        IsStaredAt = IsPointWithinAngle(head, player, _stareAngle);

        if (IsStaredAt && !_firstSeenFired)
        {
            _firstSeenFired = true;
            _onFirstSeen?.Invoke();
        }

        if (_vanishAfterStareSeconds > 0f && (State == MonsterState.Watching || State == MonsterState.Stalking))
        {
            _stareTimer = IsStaredAt ? _stareTimer + Time.deltaTime : 0f;
            if (_stareTimer >= _vanishAfterStareSeconds)
            {
                _stareTimer = 0f;
                Vanish();
            }
        }
    }

    private bool IsPointInView(Vector3 point, Transform player)
    {
        return IsPointWithinAngle(point, player, _viewAngle);
    }

    private bool IsPointWithinAngle(Vector3 point, Transform player, float angle)
    {
        Vector3 toPoint = point - player.position;
        float distance = toPoint.magnitude;
        if (distance < 0.01f)
        {
            return true;
        }
        if (distance > _seenMaxDistance)
        {
            return false;
        }
        return Vector3.Angle(player.forward, toPoint) <= angle;
    }

    // ---------------- Recorrido del jugador ----------------

    private void RecordTrail(Vector3 playerFlat)
    {
        if (!_hasLastPlayer)
        {
            // Primer punto = donde esta el jugador al empezar a acechar. Con
            // Join Trail Unseen, el monstruo llega ahi "de golpe" fuera de la
            // vista en vez de caminar en linea recta a traves de paredes.
            _hasLastPlayer = true;
            _lastPlayerFlat = playerFlat;
            _trail.Add(new TrailPoint { Position = playerFlat, Jump = _joinTrailUnseen });
            return;
        }

        bool jumped = Vector3.Distance(playerFlat, _lastPlayerFlat) > _teleportJumpDistance;
        _lastPlayerFlat = playerFlat;

        if (jumped)
        {
            _trail.Add(new TrailPoint { Position = playerFlat, Jump = true });
        }
        else if (_trail.Count == 0 || Vector3.Distance(playerFlat, _trail[_trail.Count - 1].Position) >= _trailStep)
        {
            _trail.Add(new TrailPoint { Position = playerFlat, Jump = false });
        }

        if (_trail.Count > MaxTrailPoints)
        {
            _trail.RemoveAt(0);
        }
    }

    /// <summary>Largo del camino (a pie) desde el monstruo hasta el jugador siguiendo el recorrido. Los tramos de salto cuentan 0.</summary>
    private float PathLengthToPlayer(Vector3 playerFlat)
    {
        float length = 0f;
        Vector3 from = Flat(transform.position);
        for (int i = 0; i < _trail.Count; i++)
        {
            if (!_trail[i].Jump)
            {
                length += Vector3.Distance(from, _trail[i].Position);
            }
            from = _trail[i].Position;
        }
        return length + Vector3.Distance(from, playerFlat);
    }

    // ---------------- Movimiento ----------------

    private bool NavAgentActive => _navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh;

    private bool IsNavMoving()
    {
        return NavAgentActive && _navAgent.velocity.sqrMagnitude > 0.01f;
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

    private void StandStill()
    {
        if (NavAgentActive && _navAgent.hasPath)
        {
            _navAgent.ResetPath();
        }
    }

    private void FaceTowards(Vector3 worldPoint)
    {
        Vector3 direction = worldPoint - transform.position;
        direction.y = 0f;
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

    private void UpdateSpeed()
    {
        float dt = Time.deltaTime;
        if (NavAgentActive)
        {
            Vector3 v = _navAgent.velocity;
            v.y = 0f;
            _currentSpeed = v.magnitude;
        }
        else
        {
            _currentSpeed = dt > 0f ? FlatDistance(transform.position, _lastPosition) / dt : 0f;
        }
        _lastPosition = transform.position;
    }

    // ---------------- Visual, Animator y pasos ----------------

    private void EnsureShown()
    {
        if (!_visible)
        {
            Show();
        }
    }

    private void Show()
    {
        SetVisible(true);
        _firstSeenFired = false;
        _stareTimer = 0f;
        _lastPosition = transform.position;
        SetAnimTrigger(_appearTrigger);
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
        if (_renderers == null)
        {
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

    private void StopRoutines()
    {
        if (_revealRoutine != null)
        {
            StopCoroutine(_revealRoutine);
            _revealRoutine = null;
        }
        _revealWaiting = false;
    }

    private bool AnimatorReady()
    {
        if (_animator == null || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
        {
            return false;
        }
        if (!_animParamsCached)
        {
            _animParamsCached = true;
            _animParams.Clear();
            foreach (AnimatorControllerParameter p in _animator.parameters)
            {
                _animParams[p.name] = p.type;
            }
        }
        return true;
    }

    private bool HasParam(string paramName, AnimatorControllerParameterType type)
    {
        return !string.IsNullOrEmpty(paramName)
            && AnimatorReady()
            && _animParams.TryGetValue(paramName, out AnimatorControllerParameterType found)
            && found == type;
    }

    private void SetAnimTrigger(string paramName)
    {
        if (HasParam(paramName, AnimatorControllerParameterType.Trigger))
        {
            _animator.SetTrigger(paramName);
        }
    }

    private void UpdateAnimator()
    {
        bool moving = _currentSpeed > 0.1f;
        bool running = moving && _currentSpeed > (_walkSpeed + _runSpeed) * 0.5f;

        if (HasParam(_speedParam, AnimatorControllerParameterType.Float))
        {
            _animator.SetFloat(_speedParam, _currentSpeed);
        }
        if (HasParam(_walkingBool, AnimatorControllerParameterType.Bool))
        {
            _animator.SetBool(_walkingBool, moving && !running);
        }
        if (HasParam(_runningBool, AnimatorControllerParameterType.Bool))
        {
            _animator.SetBool(_runningBool, running);
        }
    }

    private void UpdateFootsteps()
    {
        if (_footstepSource == null)
        {
            return;
        }

        bool moving = _currentSpeed > 0.1f;
        bool running = moving && _currentSpeed > (_walkSpeed + _runSpeed) * 0.5f;

        if (_footstepLoopClip != null)
        {
            if (!moving)
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
            _footstepSource.pitch = running ? _loopPitchRun : _loopPitchWalk;
            return;
        }

        if (!moving)
        {
            _stepTimer = 0f;
            return;
        }

        _stepTimer -= Time.deltaTime;
        if (_stepTimer > 0f)
        {
            return;
        }
        _stepTimer = running ? _runStepInterval : _walkStepInterval;

        AudioClip[] clips = running && _runStepClips != null && _runStepClips.Length > 0 ? _runStepClips : _walkStepClips;
        if (clips == null || clips.Length == 0)
        {
            return;
        }
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip != null)
        {
            _footstepSource.pitch = Random.Range(0.93f, 1.07f);
            _footstepSource.PlayOneShot(clip);
        }
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
        Camera main = Camera.main;
        return main != null ? main.transform : null;
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, _catchDistance);
        Gizmos.color = new Color(0.9f, 0.6f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _stalkDistance);
        Gizmos.DrawLine(transform.position + Vector3.up * _headHeight, transform.position + Vector3.up * _headHeight + transform.forward);
        if (_trail.Count > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 1; i < _trail.Count; i++)
            {
                Gizmos.DrawLine(_trail[i - 1].Position, _trail[i].Position);
            }
        }
    }
}
