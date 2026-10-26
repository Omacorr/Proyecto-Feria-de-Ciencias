using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

/// <summary>
/// Cutscene coreografiada de persecucion (Parte 1, despues de resolver el
/// candado): el protagonista camina por un camino de waypoints, a los N
/// segundos suena un rugido, aparece el monstruo detras y el jugador se pone
/// a correr; al llegar al final tropieza y cae, y se dispara On Finished
/// (cablear ahi FaintTransition.Trigger).
///
/// VR Cardboard: NUNCA se rota la camara por codigo (el TrackedPoseDriver
/// manda en la rotacion). Solo se mueve su POSICION. La inclinacion de la
/// caida se hace con un pivote temporal padre de la camara, igual que
/// AbyssFall.
///
/// Todo el movimiento va por una polilinea: [posicion inicial de la camara] +
/// waypoints. Solo se usa X/Z del camino para mover; la Y sigue la de los
/// waypoints manteniendo la altura de ojos inicial sobre el primer waypoint.
/// Usa tiempo escalado.
/// </summary>
public class ScriptedChase : MonoBehaviour
{
    [Header("Camara y camino")]
    [Tooltip("Camara del jugador (Main Camera). Vacio = Camera.main.")]
    [SerializeField] private Transform _camera;

    [Tooltip("Puntos del camino, en orden. El primero deberia estar cerca del jugador (a la altura del piso). La camara arranca donde esta y va hacia el primero.")]
    [SerializeField] private Transform[] _waypoints;

    [Tooltip("Segundos de espera antes de empezar a moverse (por ejemplo para que terminen de abrirse las puertas).")]
    [SerializeField] private float _startDelay = 1.5f;

    [Tooltip("Altura de ojos (m sobre los waypoints) a la que se endereza al empezar a caminar. 0 = mantiene la altura con la que arranca (por ejemplo si estaba agachado examinando algo, sigue agachado).")]
    [SerializeField] private float _standEyeHeight = 0f;

    [Header("Velocidades")]
    [Tooltip("Velocidad caminando (m/s).")]
    [SerializeField] private float _walkSpeed = 1.4f;

    [Tooltip("Velocidad corriendo (m/s), despues del rugido.")]
    [SerializeField] private float _runSpeed = 3.2f;

    [Tooltip("Segundos caminando (desde que empieza a moverse) antes del rugido y de salir corriendo.")]
    [SerializeField] private float _runAfterSeconds = 3f;

    [Tooltip("Que tan rapido (m/s por segundo) cambia la velocidad al pasar de caminar a correr.")]
    [SerializeField] private float _acceleration = 6f;

    [Header("Balanceo de cabeza (solo posicion)")]
    [SerializeField] private float _bobAmplitudeWalk = 0.025f;
    [SerializeField] private float _bobAmplitudeRun = 0.05f;

    [Tooltip("Pasos por metro recorrido: la frecuencia del balanceo depende de la velocidad.")]
    [SerializeField] private float _stepsPerMeter = 0.9f;

    [Header("Monstruo")]
    [Tooltip("Instancia de Monstruo_Rig que arranca INACTIVA. Se activa en el rugido. Su ProceduralWalk mide la velocidad solo.")]
    [SerializeField] private Transform _monster;

    [Tooltip("Distancia (m, por el camino) a la que aparece detras del jugador.")]
    [SerializeField] private float _monsterStartBehind = 7f;

    [Tooltip("Distancia detras del jugador en el momento de la caida.")]
    [SerializeField] private float _monsterEndBehind = 1.6f;

    [Tooltip("Distancia detras del jugador a la que llega el monstruo despues de la caida (se le echa encima).")]
    [SerializeField] private float _monsterFinalBehind = 0.6f;

    [Header("Audio (todo opcional)")]
    [Tooltip("Fuente 3D del rugido. Se la mueve a la posicion del monstruo al sonar.")]
    [SerializeField] private AudioSource _roarSource;
    [SerializeField] private AudioClip _roarClip;

    [Tooltip("Pasos del jugador en loop mientras se mueve (pitch sube al correr).")]
    [SerializeField] private AudioSource _footstepsSource;
    [SerializeField] private AudioClip _walkStepsClip;

    [Tooltip("Pasos del monstruo en loop desde el rugido (se mueve con el monstruo).")]
    [SerializeField] private AudioSource _monsterStepsSource;

    [SerializeField] private AudioSource _fallSource;
    [SerializeField] private AudioClip _fallClip;

    [Header("Vineta de confort")]
    [Tooltip("Volume (viñeta) opcional. Su weight sube 0 -> 1 al empezar a moverse y queda en 1 corriendo.")]
    [SerializeField] private Volume _comfortVolume;

    [Tooltip("Segundos que tarda la viñeta en llegar a su maximo.")]
    [SerializeField] private float _comfortRampSeconds = 1.2f;

    [Header("Caida")]
    [Tooltip("Distancia (m, por el camino desde el inicio) a la que tropieza. 0 = en el ultimo waypoint.")]
    [SerializeField] private float _fallAtDistance = 0f;

    [Tooltip("Metros que baja la camara al caer.")]
    [SerializeField] private float _fallDrop = 1.1f;

    [Tooltip("Segundos que dura la caida (aceleracion tipo gravedad).")]
    [SerializeField] private float _fallSeconds = 0.45f;

    [Tooltip("Metros que se va hacia adelante al caer.")]
    [SerializeField] private float _fallLurch = 0.3f;

    [Tooltip("Grados que se inclina la vista hacia abajo (pivote temporal).")]
    [SerializeField] private float _fallPitch = 35f;

    [Tooltip("Segundos despues de la caida antes de disparar On Finished (el monstruo sigue acercandose).")]
    [SerializeField] private float _afterFallSeconds = 0.6f;

    [Header("Eventos")]
    [SerializeField] private UnityEvent _onRoar;
    [SerializeField] private UnityEvent _onRunStart;
    [SerializeField] private UnityEvent _onFall;
    [Tooltip("Al terminar todo. Cablear FaintTransition.Trigger.")]
    [SerializeField] private UnityEvent _onFinished;

    public bool IsRunning { get; private set; }

    private Vector3[] _pts;
    private float[] _cum;      // distancia acumulada hasta cada punto
    private float _total;
    private float _eyeOffset;
    private bool _started;
    private Transform _cam;
    private Transform _pivot;
    private Transform _camOriginalParent;
    private bool _blocking;
    private Coroutine _routine;

    /// <summary>Arranca la cutscene. Llamable desde un UnityEvent (por ejemplo CodeLock.On Unlocked).</summary>
    public void Play()
    {
        if (_started)
        {
            return;
        }
        _started = true;

        _cam = _camera != null ? _camera : (Camera.main != null ? Camera.main.transform : null);
        if (_cam == null)
        {
            Debug.LogWarning("[ScriptedChase] No hay camara: no se puede reproducir.");
            return;
        }
        if (!BuildPath())
        {
            Debug.LogWarning("[ScriptedChase] Faltan waypoints: no se puede reproducir.");
            return;
        }

        _routine = StartCoroutine(Run());
    }

    private bool BuildPath()
    {
        int n = 0;
        if (_waypoints != null)
        {
            foreach (Transform w in _waypoints)
            {
                if (w != null) n++;
            }
        }
        if (n == 0)
        {
            return false;
        }

        _pts = new Vector3[n + 1];
        int k = 1;
        Transform first = null;
        foreach (Transform w in _waypoints)
        {
            if (w == null) continue;
            if (first == null) first = w;
            _pts[k++] = w.position;
        }
        _eyeOffset = _cam.position.y - first.position.y;
        _pts[0] = new Vector3(_cam.position.x, first.position.y, _cam.position.z);

        _cum = new float[_pts.Length];
        for (int i = 1; i < _pts.Length; i++)
        {
            Vector3 a = _pts[i - 1];
            Vector3 b = _pts[i];
            a.y = 0f; b.y = 0f;
            _cum[i] = _cum[i - 1] + Vector3.Distance(a, b);
        }
        _total = _cum[_cum.Length - 1];
        return _total > 0.01f;
    }

    /// <summary>Posicion y direccion (horizontal) en el camino a la distancia d. Fuera de rango se extrapola.</summary>
    private Vector3 Sample(float d, out Vector3 dir)
    {
        if (d <= 0f)
        {
            dir = SegDir(1);
            return _pts[0] + dir * d;
        }
        if (d >= _total)
        {
            dir = SegDir(_pts.Length - 1);
            return _pts[_pts.Length - 1] + dir * (d - _total);
        }
        int i = 1;
        while (i < _pts.Length - 1 && _cum[i] < d) i++;
        float segLen = _cum[i] - _cum[i - 1];
        float t = segLen > 0.0001f ? (d - _cum[i - 1]) / segLen : 0f;
        dir = SegDir(i);
        return Vector3.Lerp(_pts[i - 1], _pts[i], t);
    }

    private Vector3 SegDir(int i)
    {
        // Busca el segmento con largo > 0 mas cercano a i.
        for (int k = i; k < _pts.Length; k++)
        {
            Vector3 d = _pts[k] - _pts[k - 1];
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }
        for (int k = i; k >= 1; k--)
        {
            Vector3 d = _pts[k] - _pts[k - 1];
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) return d.normalized;
        }
        return Vector3.forward;
    }

    private IEnumerator Run()
    {
        IsRunning = true;
        SetBlocked(true);

        if (_startDelay > 0f)
        {
            yield return new WaitForSeconds(_startDelay);
        }

        float fallAt = _fallAtDistance > 0f ? Mathf.Min(_fallAtDistance, _total) : _total;
        float s = 0f;
        float speed = 0f;
        float movingTime = 0f;
        float comfort = 0f;
        float bobPhase = 0f;
        bool roared = false;
        bool running = false;
        float runStartS = 0f;
        float eyeTarget = _standEyeHeight > 0f ? _standEyeHeight : _eyeOffset;

        StartFootsteps(false);

        while (s < fallAt)
        {
            float dt = Time.deltaTime;
            movingTime += dt;
            _eyeOffset = Mathf.MoveTowards(_eyeOffset, eyeTarget, 0.8f * dt);

            if (!roared && movingTime >= _runAfterSeconds)
            {
                roared = true;
                running = true;
                runStartS = s;
                DoRoar(s);
                _onRoar?.Invoke();
                _onRunStart?.Invoke();
                StartFootsteps(true);
            }

            float target = running ? _runSpeed : _walkSpeed;
            speed = Mathf.MoveTowards(speed, target, _acceleration * dt);
            s = Mathf.Min(fallAt, s + speed * dt);

            // Camara: solo posicion (XZ del camino, Y de waypoints + altura de ojos + balanceo).
            Vector3 dir;
            Vector3 p = Sample(s, out dir);
            bobPhase += speed * _stepsPerMeter * Mathf.PI * 2f * dt;
            float amp = Mathf.Lerp(_bobAmplitudeWalk, _bobAmplitudeRun, Mathf.InverseLerp(_walkSpeed, _runSpeed, speed));
            _cam.position = new Vector3(p.x, p.y + _eyeOffset + Mathf.Sin(bobPhase) * amp, p.z);

            if (_footstepsSource != null)
            {
                _footstepsSource.pitch = Mathf.Lerp(1f, 1.5f, Mathf.InverseLerp(_walkSpeed, _runSpeed, speed));
            }

            // Viñeta de confort.
            if (_comfortVolume != null)
            {
                comfort = Mathf.MoveTowards(comfort, 1f, dt / Mathf.Max(0.05f, _comfortRampSeconds));
                _comfortVolume.weight = comfort;
            }

            // Monstruo: se acerca de _monsterStartBehind a _monsterEndBehind a lo largo de la carrera.
            if (roared)
            {
                float runLen = Mathf.Max(0.01f, fallAt - runStartS);
                float k = Mathf.Clamp01((s - runStartS) / runLen);
                float behind = Mathf.Lerp(_monsterStartBehind, _monsterEndBehind, k);
                PlaceMonster(s - behind);
            }

            yield return null;
        }

        // ---- Caida ----
        StopFootsteps();
        _onFall?.Invoke();
        if (_fallSource != null && _fallClip != null)
        {
            _fallSource.PlayOneShot(_fallClip);
        }
        if (!roared)
        {
            // Llego al final sin rugido: que al menos aparezca el monstruo.
            roared = true;
            DoRoar(s);
            _onRoar?.Invoke();
        }

        yield return FallRoutine(s);

        // Despues de la caida el monstruo se echa encima.
        float after = 0f;
        float behindNow = _monsterEndBehind;
        while (after < _afterFallSeconds)
        {
            after += Time.deltaTime;
            float k = Mathf.Clamp01(after / Mathf.Max(0.01f, _afterFallSeconds));
            behindNow = Mathf.Lerp(_monsterEndBehind, _monsterFinalBehind, k);
            PlaceMonster(s - behindNow);
            yield return null;
        }

        if (_monsterStepsSource != null) _monsterStepsSource.Stop();
        IsRunning = false;
        _routine = null;
        _onFinished?.Invoke();
    }

    private IEnumerator FallRoutine(float s)
    {
        // Pivote temporal (como AbyssFall): el TrackedPoseDriver pisa la rotacion
        // de la camara, asi que la inclinacion se aplica a un padre vacio.
        _camOriginalParent = _cam.parent;
        GameObject pivotGo = new GameObject("Pivote de tropiezo (generado)");
        _pivot = pivotGo.transform;
        _pivot.SetParent(_camOriginalParent, true);
        _pivot.position = _cam.position;
        _pivot.localRotation = Quaternion.identity;
        _cam.SetParent(_pivot, true);

        Vector3 dir;
        Sample(s, out dir);
        Vector3 basePos = _pivot.position;
        float t = 0f;
        float dur = Mathf.Max(0.05f, _fallSeconds);

        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float ease = k * k; // ease-in: acelera como una caida

            _pivot.position = basePos + Vector3.down * (_fallDrop * ease) + dir * (_fallLurch * Mathf.SmoothStep(0f, 1f, k));

            if (_fallPitch > 0f)
            {
                Quaternion yaw = Quaternion.Euler(0f, _cam.localEulerAngles.y, 0f);
                _pivot.localRotation = Quaternion.AngleAxis(_fallPitch * ease, yaw * Vector3.right);
            }

            // El monstruo sigue corriendo mientras cae.
            float behind = Mathf.Lerp(_monsterEndBehind, _monsterEndBehind, k);
            PlaceMonster(s - behind);
            yield return null;
        }
    }

    private void DoRoar(float s)
    {
        float behind = _monsterStartBehind;
        if (_monster != null)
        {
            _monster.gameObject.SetActive(true);
            PlaceMonster(s - behind);
        }
        if (_roarSource != null)
        {
            if (_monster != null)
            {
                _roarSource.transform.position = _monster.position + Vector3.up * 1.5f;
            }
            if (_roarClip != null)
            {
                _roarSource.PlayOneShot(_roarClip);
            }
        }
        if (_monsterStepsSource != null)
        {
            _monsterStepsSource.loop = true;
            if (!_monsterStepsSource.isPlaying) _monsterStepsSource.Play();
        }
    }

    private void PlaceMonster(float d)
    {
        if (_monster == null)
        {
            return;
        }
        Vector3 dir;
        Vector3 p = Sample(d, out dir);
        _monster.position = new Vector3(p.x, p.y, p.z);
        if (dir.sqrMagnitude > 0.0001f)
        {
            _monster.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
        if (_monsterStepsSource != null)
        {
            _monsterStepsSource.transform.position = _monster.position;
        }
    }

    private void StartFootsteps(bool running)
    {
        if (_footstepsSource == null)
        {
            return;
        }
        if (_walkStepsClip != null && _footstepsSource.clip != _walkStepsClip)
        {
            _footstepsSource.clip = _walkStepsClip;
        }
        _footstepsSource.loop = true;
        if (!_footstepsSource.isPlaying && _footstepsSource.clip != null)
        {
            _footstepsSource.Play();
        }
    }

    private void StopFootsteps()
    {
        if (_footstepsSource != null)
        {
            _footstepsSource.Stop();
        }
    }

    private void SetBlocked(bool block)
    {
        if (block == _blocking)
        {
            return;
        }
        _blocking = block;
        if (block)
        {
            GazeController.AddBlocker(this);
            TeleportManager tm = FindFirstObjectByType<TeleportManager>();
            if (tm != null) tm.LockMovement();
        }
        else
        {
            GazeController.RemoveBlocker(this);
        }
    }

    private void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
        IsRunning = false;
        StopFootsteps();
    }

    private void OnDestroy()
    {
        SetBlocked(false);
        if (_cam != null && _pivot != null && _cam.parent == _pivot)
        {
            _cam.SetParent(_camOriginalParent, true);
        }
        if (_pivot != null)
        {
            Destroy(_pivot.gameObject);
        }
        if (_comfortVolume != null)
        {
            _comfortVolume.weight = 0f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_waypoints == null)
        {
            return;
        }
        Gizmos.color = Color.cyan;
        Transform prev = null;
        foreach (Transform w in _waypoints)
        {
            if (w == null) continue;
            Gizmos.DrawSphere(w.position, 0.12f);
            if (prev != null) Gizmos.DrawLine(prev.position, w.position);
            prev = w;
        }
        if (_monster != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_monster.position, 0.3f);
        }
    }
}
