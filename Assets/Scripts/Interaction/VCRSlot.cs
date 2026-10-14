using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cumpleaños (Parte 2): la ranura de la tele / video. Al mirarla y sostener la
/// mirada:
///  - Si el jugador tiene la cinta (TapeInventory.HasTape): la "mete" (sonido,
///    prende el prop de la cinta en la ranura si se asigno) y dispara
///    _onTapeInserted UNA sola vez -> ahi se cablea TVMovie.Play().
///  - Si no la tiene: muestra un cartel temporal (_missingTapeSign).
///
/// Necesita Collider + Layer Interactive, como todo IGazeInteractable.
/// </summary>
public class VCRSlot : MonoBehaviour, IGazeInteractable
{
    [Header("Referencias")]
    [Tooltip("El TapeInventory de ESTA escena.")]
    [SerializeField] private TapeInventory _tapeInventory;

    [Tooltip("Cartel 'te falta la cinta' (arranca desactivado).")]
    [SerializeField] private GameObject _missingTapeSign;

    [SerializeField] private float _signDuration = 2.5f;

    [Tooltip("Opcional: prop de la cinta ya metida en la ranura (arranca oculto, se prende al insertar).")]
    [SerializeField] private GameObject _tapeInSlotVisual;

    [Header("Audio (opcional)")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _insertSound;

    [Header("Evento")]
    [Tooltip("Se dispara UNA vez, al meter la cinta. Cablear aca TVMovie.Play().")]
    [SerializeField] private UnityEvent _onTapeInserted;

    private bool _done;
    private Coroutine _signRoutine;

    private void Awake()
    {
        if (_missingTapeSign != null) _missingTapeSign.SetActive(false);
        if (_tapeInSlotVisual != null) _tapeInSlotVisual.SetActive(false);
    }

    public void OnGazeEnter() { }
    public void OnGazeStay(float progress) { }
    public void OnGazeExit() { }

    public void OnGazeSelect()
    {
        if (_done)
        {
            return;
        }

        if (_tapeInventory == null || !_tapeInventory.HasTape)
        {
            ShowMissingTapeSign();
            return;
        }

        _done = true;
        if (_audioSource != null && _insertSound != null) _audioSource.PlayOneShot(_insertSound);
        if (_tapeInSlotVisual != null) _tapeInSlotVisual.SetActive(true);

        // Apagar los colliders de la tele para que el reticle NO siga
        // rellenando el anillo cada vez que la mires: la cinta ya se metio,
        // no hay nada mas que hacer con la tele.
        foreach (Collider col in GetComponents<Collider>())
        {
            col.enabled = false;
        }

        Debug.Log("[VCRSlot] Cinta insertada -> arranca la pelicula.");
        _onTapeInserted?.Invoke();
    }

    private void ShowMissingTapeSign()
    {
        if (_missingTapeSign == null)
        {
            Debug.Log("[VCRSlot] Falta la cinta (y no hay cartel asignado).");
            return;
        }
        if (_signRoutine != null) StopCoroutine(_signRoutine);
        _signRoutine = StartCoroutine(SignRoutine());
    }

    private IEnumerator SignRoutine()
    {
        _missingTapeSign.SetActive(true);
        yield return new WaitForSeconds(_signDuration);
        _missingTapeSign.SetActive(false);
        _signRoutine = null;
    }
}
