using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cumpleaños (Parte 2): el momento "te das vuelta y el monstruo ya viene".
///
/// Se cablea desde SecurityFootage._onMovieEnd (en lugar de ir directo a
/// MonsterChaser.StartChase). Hace la previa de la persecucion:
///  1. suena un golpe/rugido desde un AudioSource que conviene poner DETRAS
///     del jugador (en la habitacion de atras), para que gire la cabeza solo;
///  2. un parpadeo a negro (VRFadeController) para tapar el "corte";
///  3. mientras esta todo negro, reubica al monstruo en su punto de aparicion
///     (si se asigno uno) y llama a MonsterChaser.StartChase();
///  4. vuelve la imagen y el monstruo ya esta ahi, caminando.
///
/// Por que NO gira la camara sola por defecto: en Cardboard el
/// TrackedPoseDriver pisa la rotacion de la camara con la del casco cada
/// frame, asi que forzar el giro pelea con el hardware y marea. El giro
/// forzado queda como opcion (_forceCameraTurn) solo para probar parado.
/// </summary>
public class ChaseReveal : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El monstruo que persigue (Monstruo_Menu).")]
    [SerializeField] private MonsterChaser _chaser;

    [Tooltip("Para el parpadeo a negro. Si se deja vacio, se busca uno en la escena al arrancar.")]
    [SerializeField] private VRFadeController _fade;

    [Header("Revelado")]
    [Tooltip("Parpadeo a negro (funde y vuelve) para tapar el corte antes de que aparezca el monstruo.")]
    [SerializeField] private bool _blink = true;

    [Tooltip("Si NO hay parpadeo (o no hay VRFadeController), cuanto esperar antes de mostrar al monstruo y arrancar la persecucion.")]
    [SerializeField] private float _delayBeforeChase = 0.5f;

    [Tooltip("Opcional: si se asigna, el monstruo se teletransporta EXACTO a la posicion/rotacion de este Transform (ponelo vos parado en el umbral de la habitacion de atras). Si se deja vacio, el monstruo aparece solo, un poco DETRAS del jugador, parado y mirandolo.")]
    [SerializeField] private Transform _monsterRevealPoint;

    [Tooltip("Si no hay _monsterRevealPoint: a cuantos metros detras del jugador aparece el monstruo.")]
    [SerializeField] private float _spawnBehindDistance = 3f;

    [Tooltip("Si no hay _monsterRevealPoint: altura de ojos del jugador (para calcular el piso = camara.y - esto).")]
    [SerializeField] private float _eyeHeight = 1.6f;

    [Tooltip("Si no hay _monsterRevealPoint: ajuste fino de la Y del monstruo. Subilo si aparece hundido en el piso, bajalo si flota. Depende de donde tenga el pivote el modelo.")]
    [SerializeField] private float _monsterYOffset = 0f;

    [Tooltip("Camara del jugador (Main Camera). Necesaria para calcular 'detras del jugador'. Si se deja vacio usa Camera.main.")]
    [SerializeField] private Transform _playerCamera;

    [Header("Golpe de sonido (opcional)")]
    [Tooltip("AudioSource desde donde suena el golpe/rugido. Ponelo DETRAS del jugador (en la habitacion de atras) para que gire la cabeza hacia el.")]
    [SerializeField] private AudioSource _stingerSource;
    [SerializeField] private AudioClip _stingerClip;

    [Header("Giro forzado de camara (opcional, marea en VR)")]
    [Tooltip("Gira la camara hacia el monstruo por codigo. DESACTIVADO por defecto: en Cardboard pelea con el giro real del casco. Solo para pruebas paradas / debug.")]
    [SerializeField] private bool _forceCameraTurn;
    [SerializeField] private Transform _cameraToTurn;
    [SerializeField] private float _turnDuration = 1.2f;

    [Header("Evento")]
    [SerializeField] private UnityEvent _onRevealFinished;

    private bool _done;

    /// <summary>Cablear desde SecurityFootage._onMovieEnd.</summary>
    public void Play()
    {
        if (_done)
        {
            return;
        }
        _done = true;

        if (_fade == null)
        {
            _fade = Object.FindFirstObjectByType<VRFadeController>();
        }
        if (_playerCamera == null && Camera.main != null)
        {
            _playerCamera = Camera.main.transform;
        }

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (_stingerSource != null && _stingerClip != null)
        {
            _stingerSource.PlayOneShot(_stingerClip);
        }

        if (_blink && _fade != null)
        {
            // FadeOutAndIn ejecuta Reveal() cuando la pantalla esta 100% negra.
            yield return _fade.StartCoroutine(_fade.FadeOutAndIn(Reveal));
        }
        else
        {
            if (_delayBeforeChase > 0f)
            {
                yield return new WaitForSeconds(_delayBeforeChase);
            }
            Reveal();
        }

        if (_forceCameraTurn && _cameraToTurn != null && _chaser != null)
        {
            yield return StartCoroutine(TurnCameraToMonster());
        }

        _onRevealFinished?.Invoke();
    }

    private void Reveal()
    {
        if (_chaser == null)
        {
            Debug.LogWarning("[ChaseReveal] Falta asignar el MonsterChaser.");
            return;
        }

        // La Y que se le pasa aca es la del PISO; MonsterChaser.StartChase()
        // sube el modelo para que los pies queden apoyados (snap feet), y
        // ademas lo deja parado 1s antes de caminar.
        if (_monsterRevealPoint != null)
        {
            // Poné el punto SOBRE EL PISO en la habitacion de atras, apuntando
            // hacia donde viene el jugador. Solo se usa el giro horizontal.
            Vector3 f = _monsterRevealPoint.forward; f.y = 0f;
            Quaternion rot = f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f, Vector3.up) : _chaser.transform.rotation;
            _chaser.transform.SetPositionAndRotation(_monsterRevealPoint.position, rot);
        }
        else if (_playerCamera != null)
        {
            // Fallback: aparece un poco DETRAS del jugador, en el piso, mirandolo.
            Vector3 fwd = _playerCamera.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            fwd.Normalize();

            Vector3 pos = _playerCamera.position - fwd * _spawnBehindDistance;
            pos.y = _playerCamera.position.y - _eyeHeight + _monsterYOffset;
            _chaser.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
        }

        _chaser.StartChase();
    }

    private IEnumerator TurnCameraToMonster()
    {
        // Apagar cualquier TrackedPoseDriver mientras dura el giro (si no,
        // pisa la rotacion de la camara cada frame). Se busca por nombre de
        // tipo para no depender del assembly del paquete de Input System.
        List<Behaviour> drivers = new List<Behaviour>();
        foreach (Behaviour comp in _cameraToTurn.GetComponents<Behaviour>())
        {
            if (comp != null && comp.GetType().Name == "TrackedPoseDriver")
            {
                comp.enabled = false;
                drivers.Add(comp);
            }
        }

        Quaternion from = _cameraToTurn.rotation;
        Vector3 dir = _chaser.transform.position - _cameraToTurn.position;
        dir.y = 0f;
        Quaternion to = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir, Vector3.up) : from;

        float e = 0f;
        while (e < _turnDuration)
        {
            e += Time.deltaTime;
            _cameraToTurn.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, e / _turnDuration));
            yield return null;
        }
        _cameraToTurn.rotation = to;

        foreach (Behaviour d in drivers)
        {
            if (d != null)
            {
                d.enabled = true;
            }
        }
    }
}
