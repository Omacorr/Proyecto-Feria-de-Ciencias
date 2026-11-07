using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Mecanica de llave y puerta (2/3). Al mirar la puerta y sostener la mirada
/// (OnGazeSelect via GazeController):
///  - Si el jugador ya tiene la llave (KeyInventory.HasKey), la puerta se
///    desplaza a una posicion local distinta (efecto de "abrirse").
///  - Si no la tiene, se muestra un cartel de aviso ("Te falta la llave")
///    durante unos segundos y despues se oculta solo.
/// Necesita Collider + estar en la layer Interactive para que el gaze la
/// detecte, igual que el resto de los IGazeInteractable del proyecto.
/// </summary>
public class Door : MonoBehaviour, IGazeInteractable
{
    [Header("Referencias")]
    [Tooltip("Tildado (por defecto): la puerta pide la llave del KeyInventory para abrirse. Destildado: se abre directo al mirarla, sin pedir nada - sirve para puertas comunes del mapa que no son parte de un puzzle.")]
    [SerializeField] private bool _requiresKey = true;

    [Tooltip("El KeyInventory que sabe si el jugador ya tiene la llave. No hace falta asignarlo si 'Requires Key' esta destildado.")]
    [SerializeField] private KeyInventory _keyInventory;

    [Tooltip("Objeto con el cartel 'Te falta la llave' (Text/TextMeshPro). Arranca desactivado en la escena.")]
    [SerializeField] private GameObject _missingKeySign;

    [Tooltip("Cuantos segundos se queda visible el cartel de aviso.")]
    [SerializeField] private float _signDuration = 2.5f;

    [Tooltip("Opcional: texto en pantalla (VR) que se muestra al intentar abrir sin llave.")]
    [SerializeField] private VRHintText _hint;

    [Tooltip("Mensaje que se muestra en 'Hint' cuando falta la llave.")]
    [SerializeField] private string _missingKeyMessage = "Está cerrada. Necesito una llave.";

    [Tooltip("Se dispara cada vez que el jugador intenta abrir la puerta sin tener la llave.")]
    [SerializeField] private UnityEvent _onMissingKey;

    [Header("Apertura - Posicion")]
    [Tooltip("RECOMENDADO: arrastra a la Scene View un objeto vacio (GameObject vacio, Create Empty) hasta el lugar EXACTO donde tiene que terminar la puerta abierta, y GIRALO tambien hasta que se vea con la orientacion final que queres (usa el gizmo de rotacion, como si fuera la puerta ya abierta). Asignalo aca. En Play/Build la puerta va a terminar exactamente en esa posicion Y esa rotacion, sin importar rotaciones o escalas raras que tenga el padre - se convierte todo solo. Si esto esta asignado, se ignoran 'Open Local Position' y 'Open Local Euler Rotation' de abajo (los dos).")]
    [SerializeField] private Transform _openPositionTarget;

    [Tooltip("Alternativa vieja (legacy): Posicion LOCAL ABSOLUTA a la que queda la puerta abierta (es el valor final de transform.localPosition, no un offset que se suma). Solo se usa si 'Open Position Target' esta vacio arriba. Ojo: si el padre de la puerta tiene rotacion o escala rara (comun en modelos importados), el valor que ves al arrastrar la puerta a mano en el Editor puede no coincidir con lo que hace en Play/Build - por eso conviene usar 'Open Position Target' en vez de este campo.")]
    [SerializeField] private Vector3 _openLocalPosition;

    [Header("Apertura - Rotacion")]
    [Tooltip("Alternativa vieja (legacy): rotacion LOCAL adicional (grados, Euler XYZ) que gira la puerta al abrirse, sumada a su rotacion inicial - por ejemplo (0, 90, 0) para que gire como puerta de gozne. Se ignora si 'Open Position Target' esta asignado arriba (en ese caso se usa la rotacion del target directamente, ver tooltip de Open Position Target). Mismo problema que con la posicion vieja: si el padre tiene rotacion rara, el angulo que tipeas aca puede no coincidir con lo que hace en Play/Build.")]
    [SerializeField] private Vector3 _openLocalEulerRotation;

    [Header("Duracion")]
    [Tooltip("Cuantos segundos tarda la animacion de apertura (posicion y rotacion se mueven juntas en ese mismo tiempo). A diferencia de una velocidad en metros/segundo, esto no depende de la escala del objeto padre - por eso lo usamos en vez de una velocidad.")]
    [SerializeField] private float _openDuration = 1.5f;

    [Header("Feedback de mirada (opcional)")]
    [SerializeField] private Material _inactiveMaterial;
    [SerializeField] private Material _gazedAtMaterial;

    [Header("Audio (opcional)")]
    [Tooltip("AudioSource desde donde suena el clip al abrirse. Puede estar en la propia puerta o en otro objeto (por ejemplo uno central, si son varias hojas y no queres que suene una vez por cada una).")]
    [SerializeField] private AudioSource _audioSource;

    [Tooltip("Sonido que se reproduce una vez apenas arranca a abrirse la puerta.")]
    [SerializeField] private AudioClip _openSound;

    [Header("Restriccion de posicion (opcional)")]
    [Tooltip("Si se asigna, mirar la puerta solo la abre cuando el jugador esta parado exactamente sobre este TeleportPoint (por ejemplo, el punto justo frente a ella). Dejalo vacio si la puerta se puede abrir desde cualquier lado. No afecta a Open() cuando se llama desde otro script (como CodeLock.OnUnlocked) - esa sigue funcionando aunque el jugador este en otro lado.")]
    [SerializeField] private TeleportManager _teleportManager;
    [SerializeField] private TeleportPoint _requiredPoint;

    private Renderer _renderer;
    private Vector3 _closedLocalPosition;
    private Quaternion _closedLocalRotation;
    private bool _isOpen;
    private bool _isMoving;
    private Coroutine _signCoroutine;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _closedLocalPosition = transform.localPosition;
        _closedLocalRotation = transform.localRotation;
        TimedSign.Hide(_missingKeySign);
        SetGazed(false);
    }

    public void OnGazeEnter() => SetGazed(true);
    public void OnGazeStay(float progress) { }
    public void OnGazeExit() => SetGazed(false);

    public void OnGazeSelect()
    {
        if (_isOpen || _isMoving || !IsPlayerInRange())
        {
            return;
        }

        if (!_requiresKey || (_keyInventory != null && _keyInventory.HasKey))
        {
            Open();
        }
        else
        {
            ShowMissingKeySign();
        }
    }

    /// <summary>
    /// True si no hay restriccion de posicion, o si el jugador esta parado
    /// sobre el Required Point. Solo filtra la apertura por mirada: Open()
    /// llamado desde afuera (CodeLock) no pasa por aca.
    /// </summary>
    private bool IsPlayerInRange()
    {
        return _requiredPoint == null
            || (_teleportManager != null && _teleportManager.CurrentOccupiedPoint == _requiredPoint);
    }

    /// <summary>
    /// Abre la puerta sin pasar por el chequeo de llave. Publica para que
    /// CodeLock.OnUnlocked abra las dos hojas de una puerta doble juntas.
    /// </summary>
    public void Open()
    {
        if (_isOpen || _isMoving)
        {
            return;
        }

        _isOpen = true;

        if (_audioSource != null && _openSound != null)
        {
            _audioSource.PlayOneShot(_openSound);
        }

        // Con Open Position Target se convierte su posicion y rotacion MUNDIALES
        // a locales segun el padre actual, asi el resultado es exactamente lo que
        // se ve en el Editor aunque el padre tenga rotacion o escala raras. Sin
        // target se usan los valores legacy (Open Local Position / Euler Rotation).
        Vector3 targetPos = _openLocalPosition;
        Quaternion targetRot = _closedLocalRotation * Quaternion.Euler(_openLocalEulerRotation);

        if (_openPositionTarget != null)
        {
            Transform parent = transform.parent;
            targetPos = parent != null ? parent.InverseTransformPoint(_openPositionTarget.position) : _openPositionTarget.position;
            targetRot = parent != null ? Quaternion.Inverse(parent.rotation) * _openPositionTarget.rotation : _openPositionTarget.rotation;
        }

        StartCoroutine(MoveDoor(_closedLocalPosition, targetPos, _closedLocalRotation, targetRot));
    }

    private void ShowMissingKeySign()
    {
        if (_hint != null)
        {
            _hint.Show(_missingKeyMessage);
        }

        _onMissingKey?.Invoke();

        if (_missingKeySign == null && _hint == null)
        {
            Debug.LogWarning($"[Door] {gameObject.name}: falta asignar Missing Key Sign en el Inspector.");
        }

        TimedSign.Show(this, _missingKeySign, _signDuration, ref _signCoroutine);
    }

    private IEnumerator MoveDoor(Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot)
    {
        _isMoving = true;

        // Duracion fija en segundos, no velocidad: la distancia se mide en
        // unidades LOCALES y con un padre de escala rara la puerta tardaba
        // minutos en terminar de moverse.
        float duration = Mathf.Max(0f, _openDuration);
        float elapsed = 0f;

        Debug.Log($"[Door] {gameObject.name} arranca MoveDoor. fromPos={fromPos} toPos={toPos} duration={duration} worldPosAntes={transform.position}");

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localPosition = Vector3.Lerp(fromPos, toPos, t);
            transform.localRotation = Quaternion.Slerp(fromRot, toRot, t);
            yield return null;
        }

        transform.localPosition = toPos;
        transform.localRotation = toRot;
        _isMoving = false;

        Debug.Log($"[Door] {gameObject.name} termino MoveDoor. localPosFinal={transform.localPosition} worldPosDespues={transform.position}");
    }

    private void SetGazed(bool gazedAt) => GazeHighlight.Swap(_renderer, _inactiveMaterial, _gazedAtMaterial, gazedAt);
}
