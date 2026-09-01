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
    [Tooltip("El KeyInventory que sabe si el jugador ya tiene la llave.")]
    [SerializeField] private KeyInventory _keyInventory;

    [Tooltip("Objeto con el cartel 'Te falta la llave' (Text/TextMeshPro). Arranca desactivado en la escena.")]
    [SerializeField] private GameObject _missingKeySign;

    [Tooltip("Cuantos segundos se queda visible el cartel de aviso.")]
    [SerializeField] private float _signDuration = 2.5f;

    [Header("Apertura")]
    [Tooltip("Posicion LOCAL a la que se mueve la puerta al abrirse (relativa a su posicion inicial).")]
    [SerializeField] private Vector3 _openLocalPosition;

    [Tooltip("Velocidad del movimiento de apertura, en metros por segundo.")]
    [SerializeField] private float _openSpeed = 1.5f;

    [Header("Feedback de mirada (opcional)")]
    [SerializeField] private Material _inactiveMaterial;
    [SerializeField] private Material _gazedAtMaterial;

    private Renderer _renderer;
    private Vector3 _closedLocalPosition;
    private bool _isOpen;
    private bool _isMoving;
    private Coroutine _signCoroutine;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _closedLocalPosition = transform.localPosition;

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
        if (_isOpen || _isMoving)
        {
            return;
        }

        bool hasKey = _keyInventory != null && _keyInventory.HasKey;

        if (hasKey)
        {
            _isOpen = true;
            StartCoroutine(MoveDoor(_closedLocalPosition, _openLocalPosition));
        }
        else
        {
            ShowMissingKeySign();
        }
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

    private IEnumerator MoveDoor(Vector3 from, Vector3 to)
    {
        _isMoving = true;

        float distance = Vector3.Distance(from, to);
        float duration = _openSpeed > 0f ? distance / _openSpeed : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.localPosition = to;
        _isMoving = false;
    }

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
