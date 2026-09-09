using System.Collections;
using UnityEngine;

/// <summary>
/// Puerta que termina un nivel: al mirarla y sostener la mirada (y tener la
/// llave, si se pide), abre la puerta y dispara la transicion a la escena
/// siguiente: fundido a negro con el texto del nivel + carga
/// (SceneTransitionOverlay se encarga, y sobrevive el cambio de escena para
/// que el fundido de salida quede prolijo).
///
/// Necesita Collider + estar en una layer que el GazeController detecte.
/// </summary>
public class LevelDoor : MonoBehaviour, IGazeInteractable
{
    [Header("Destino")]
    [Tooltip("Nombre EXACTO de la escena a cargar (tiene que estar en Build Settings).")]
    [SerializeField] private string _nextSceneName = "Parte 1 - El Despertar";

    [Tooltip("Texto grande de la pantalla de carga.")]
    [SerializeField] private string _bigText = "PARTE 1";

    [Tooltip("Texto chico debajo (subtitulo del nivel).")]
    [SerializeField] private string _subText = "EL DESPERTAR";

    [Header("Transicion")]
    [SerializeField] private float _fadeDuration = 1.2f;

    [Tooltip("Segundos en negro con el texto antes de aparecer en la escena nueva.")]
    [SerializeField] private float _holdSeconds = 2.5f;

    [Header("Requiere llave (opcional)")]
    [SerializeField] private bool _requiresKey;
    [SerializeField] private KeyInventory _keyInventory;
    [Tooltip("Cartel 'te falta la llave' (arranca desactivado).")]
    [SerializeField] private GameObject _missingKeySign;
    [SerializeField] private float _signDuration = 2.5f;

    [Header("Apertura visual (opcional)")]
    [Tooltip("Transform que gira al abrir (una bisagra). Si se deja vacio, no hay animacion de puerta.")]
    [SerializeField] private Transform _doorPivot;
    [SerializeField] private float _doorOpenAngle = 100f;
    [SerializeField] private float _doorOpenDuration = 1.2f;

    [Header("Sonido (opcional)")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _openSound;

    private bool _busy;
    private Coroutine _signRoutine;

    public void OnGazeEnter() { }
    public void OnGazeStay(float progress) { }
    public void OnGazeExit() { }

    public void OnGazeSelect()
    {
        if (_busy)
        {
            return;
        }

        if (_requiresKey && (_keyInventory == null || !_keyInventory.HasKey))
        {
            Debug.Log("[LevelDoor] " + gameObject.name + ": mirada recibida pero FALTA la llave " +
                      "(KeyInventory=" + (_keyInventory != null) + ", HasKey=" + (_keyInventory != null && _keyInventory.HasKey) + ").");
            ShowMissingKeySign();
            return;
        }

        Debug.Log("[LevelDoor] " + gameObject.name + ": abriendo -> transicion a '" + _nextSceneName + "'.");
        _busy = true;
        StartCoroutine(OpenThenGo());
    }

    private void ShowMissingKeySign()
    {
        if (_missingKeySign == null)
        {
            return;
        }
        if (_signRoutine != null)
        {
            StopCoroutine(_signRoutine);
        }
        _signRoutine = StartCoroutine(SignRoutine());
    }

    private IEnumerator SignRoutine()
    {
        _missingKeySign.SetActive(true);
        yield return new WaitForSeconds(_signDuration);
        _missingKeySign.SetActive(false);
        _signRoutine = null;
    }

    private IEnumerator OpenThenGo()
    {
        if (_audioSource != null && _openSound != null)
        {
            _audioSource.PlayOneShot(_openSound);
        }

        if (_doorPivot != null)
        {
            Quaternion from = _doorPivot.rotation;
            Quaternion to = Quaternion.AngleAxis(_doorOpenAngle, Vector3.up) * from;
            float e = 0f;
            while (e < _doorOpenDuration)
            {
                e += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / _doorOpenDuration));
                _doorPivot.rotation = Quaternion.Slerp(from, to, k);
                yield return null;
            }
            _doorPivot.rotation = to;
        }

        // El overlay hace el fundido + carga y sobrevive el cambio de escena.
        SceneTransitionOverlay.Play(_nextSceneName, _bigText, _subText, _fadeDuration, _holdSeconds, Color.black);
    }
}
