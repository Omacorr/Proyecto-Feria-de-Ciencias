using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Se pone sobre el cuerpo del candado, visto de lejos a su tamano normal
/// (no en los cilindros que giran - esos llevan CodeDigit). Al mirarlo y
/// mantener la mirada, en vez de abrirse le pide al TeleportManager que
/// lleve al jugador hasta un punto fijo, pegado y de frente al candado
/// (_examinePoint) - ahi los tres CodeDigit quedan comodos para apuntarles
/// con la mirada. El candado en si no se mueve ni se pega a la camara: es
/// el jugador el que se acerca, asi que apuntar a cada dial por separado
/// sigue funcionando igual que con cualquier otro objeto del mapa.
///
/// Guarda en que TeleportPoint estaba parado el jugador antes de entrar,
/// para poder volver ahi con ReturnToPreviousPoint() - cablea esa funcion
/// desde CodeLock.OnUnlocked para que la vuelta sea automatica apenas se
/// resuelve el candado.
/// </summary>
public class ExamineTrigger : MonoBehaviour, IGazeInteractable
{
    [Tooltip("El mismo TeleportManager que ya usa el resto del mapa.")]
    [SerializeField] private TeleportManager _teleportManager;

    [Tooltip("TeleportPoint fijo, pegado y de frente al candado. Podes dejarlo con el renderer/collider desactivados si no queres que tambien aparezca como punto de recorrido normal del cuarto.")]
    [SerializeField] private TeleportPoint _examinePoint;

    [Header("Feedback de mirada (opcional)")]
    [SerializeField] private Material _inactiveMaterial;
    [SerializeField] private Material _gazedAtMaterial;

    [Header("Requiere pista (opcional)")]
    [Tooltip("Si se asigna, el jugador no puede acercarse al candado hasta que CodeClueTracker.HasSeenCode sea true (por ejemplo, hasta que haya pisado el TeleportPoint de enfrente de las notas). Dejalo vacio si el candado se puede examinar desde el principio, sin buscar nada antes.")]
    [SerializeField] private CodeClueTracker _clueTracker;

    [Tooltip("Objeto con un cartel tipo 'Todavia no se el codigo' (Text/TextMeshPro). Arranca desactivado en la escena. Se usa solo si Clue Tracker esta asignado.")]
    [SerializeField] private GameObject _lockedHintSign;

    [Tooltip("Cuantos segundos se queda visible el cartel de aviso.")]
    [SerializeField] private float _lockedHintDuration = 2.5f;

    [Header("Eventos")]
    [Tooltip("Se dispara apenas se arranca el acercamiento. Util para apagar la linterna del jugador y prender una luz propia del candado, para que no quede tan brillante y se puedan leer los numeros.")]
    [SerializeField] private UnityEvent _onExamineStart;

    [Tooltip("Se dispara al volver al punto anterior. Util para revertir lo que hayas hecho en On Examine Start (prender la linterna de nuevo, apagar la luz del candado).")]
    [SerializeField] private UnityEvent _onExamineEnd;

    private Renderer _renderer;
    private TeleportPoint _previousPoint;
    private Coroutine _hintCoroutine;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        SetGazed(false);

        if (_lockedHintSign != null)
        {
            _lockedHintSign.SetActive(false);
        }
    }

    public void OnGazeEnter()
    {
        SetGazed(true);
    }

    public void OnGazeStay(float progress)
    {
        // El feedback de progreso ya lo muestra GazeReticle.
    }

    public void OnGazeExit()
    {
        SetGazed(false);
    }

    public void OnGazeSelect()
    {
        if (_teleportManager == null || _examinePoint == null || _teleportManager.IsTeleporting)
        {
            return;
        }

        if (_clueTracker != null && !_clueTracker.HasSeenCode)
        {
            ShowLockedHint();
            return;
        }

        _previousPoint = _teleportManager.CurrentOccupiedPoint;
        _teleportManager.RequestTeleport(_examinePoint.transform.position, _examinePoint);
        _onExamineStart?.Invoke();
    }

    private void ShowLockedHint()
    {
        if (_lockedHintSign == null)
        {
            return;
        }

        if (_hintCoroutine != null)
        {
            StopCoroutine(_hintCoroutine);
        }

        _hintCoroutine = StartCoroutine(ShowHintTemporarily());
    }

    private IEnumerator ShowHintTemporarily()
    {
        _lockedHintSign.SetActive(true);
        yield return new WaitForSeconds(_lockedHintDuration);
        _lockedHintSign.SetActive(false);
        _hintCoroutine = null;
    }

    /// <summary>
    /// Vuelve al TeleportPoint donde estaba parado el jugador antes de
    /// entrar a examinar el candado. Cablealo desde CodeLock.OnUnlocked.
    /// </summary>
    public void ReturnToPreviousPoint()
    {
        if (_teleportManager == null || _previousPoint == null || _teleportManager.IsTeleporting)
        {
            return;
        }

        _teleportManager.RequestTeleport(_previousPoint.transform.position, _previousPoint);
        _onExamineEnd?.Invoke();
    }

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
