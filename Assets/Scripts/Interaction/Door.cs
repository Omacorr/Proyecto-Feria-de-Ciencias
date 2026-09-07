using System.Collections;
using UnityEngine;

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

    [Header("Apertura - Posicion")]
    [Tooltip("Posicion LOCAL ABSOLUTA a la que queda la puerta abierta (es el valor final de transform.localPosition, no un offset que se suma). Dejala en (0,0,0) si la puerta solo va a girar, sin desplazarse. Ver nota abajo sobre como conseguir este valor sin calcularlo a mano.")]
    [SerializeField] private Vector3 _openLocalPosition;

    [Header("Apertura - Rotacion")]
    [Tooltip("Rotacion LOCAL adicional (grados, Euler XYZ) que gira la puerta al abrirse, sumada a su rotacion inicial - por ejemplo (0, 90, 0) para que gire como puerta de gozne. Dejala en (0,0,0) si la puerta solo se desplaza, sin girar.")]
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

        if (_missingKeySign != null)
        {
            _missingKeySign.SetActive(false);
        }

        SetGazed(false);
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
        if (_isOpen || _isMoving || !IsPlayerInRange())
        {
            return;
        }

        bool canOpen = !_requiresKey || (_keyInventory != null && _keyInventory.HasKey);

        if (canOpen)
        {
            Open();
        }
        else
        {
            ShowMissingKeySign();
        }
    }

    /// <summary>
    /// True si no hay restriccion de posicion, o si la hay y el jugador
    /// esta parado exactamente sobre el Required Point. Solo se usa para
    /// filtrar la apertura por mirada (OnGazeSelect) - Open() llamado
    /// desde afuera (por ejemplo CodeLock) no pasa por aca.
    /// </summary>
    private bool IsPlayerInRange()
    {
        if (_requiredPoint == null)
        {
            return true;
        }

        return _teleportManager != null && _teleportManager.CurrentOccupiedPoint == _requiredPoint;
    }

    /// <summary>
    /// Abre la puerta directamente, sin pasar por el chequeo de llave.
    /// Publica para que otros scripts la puedan abrir por su cuenta - por
    /// ejemplo, CodeLock.OnUnlocked cableado a esta funcion en las dos hojas
    /// de una puerta doble, para que ambas se abran juntas apenas se
    /// resuelve el candado, sin que el jugador tenga que mirar cada hoja.
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

        Quaternion openRotation = _closedLocalRotation * Quaternion.Euler(_openLocalEulerRotation);
        StartCoroutine(MoveDoor(_closedLocalPosition, _openLocalPosition, _closedLocalRotation, openRotation));
    }

    private void ShowMissingKeySign()
    {
        if (_missingKeySign == null)
        {
            Debug.LogWarning($"[Door] {gameObject.name}: falta asignar Missing Key Sign en el Inspector.");
            return;
        }

        if (_signCoroutine != null)
        {
            StopCoroutine(_signCoroutine);
        }

        _signCoroutine = StartCoroutine(ShowSignTemporarily());
    }

    private IEnumerator ShowSignTemporarily()
    {
        _missingKeySign.SetActive(true);
        yield return new WaitForSeconds(_signDuration);
        _missingKeySign.SetActive(false);
        _signCoroutine = null;
    }

    private IEnumerator MoveDoor(Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot)
    {
        _isMoving = true;

        // Duracion fija en segundos (Open Duration), no una velocidad en
        // metros/segundo. Antes calculabamos el tiempo como distancia /
        // velocidad, pero esa distancia se mide en unidades LOCALES, y si el
        // padre de la puerta tiene una escala rara (comun en mapas
        // importados), unidades locales chicas pueden representar
        // muchisimos metros reales (o al reves) - eso hacia que la puerta
        // pareciera "trabada" tardando minutos u horas en terminar de
        // moverse aunque la posicion en si estuviera bien puesta.
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

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
