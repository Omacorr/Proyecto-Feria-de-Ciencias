using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Punto de entrega de los fragmentos en el centro del mapa (Etapa 5,
/// "Backrooms"). Solo se "activa" si FragmentCounter ya tiene todos: en ese
/// caso dispara On Delivered UNA vez (aparicion del monstruo, final, desmayo a
/// Parte Final, etc. - todo por UnityEvent, este script no sabe nada de eso).
/// Si todavia faltan, muestra un aviso temporal y dispara On Not Ready.
///
/// Dos formas de entregar (se pueden usar las dos a la vez):
///  - Mirando este objeto (pedestal/mesa/nota incompleta del centro): necesita
///    Collider + Layer Interactive (7). Se desactiva con "Deliver On Gaze".
///  - Llegando a un TeleportPoint del centro: en su On Player Arrived, elegir
///    FragmentDropOff.TryDeliver(). Asi "llevarlos al centro" es literalmente
///    pararse ahi.
/// </summary>
public class FragmentDropOff : MonoBehaviour, IGazeInteractable
{
    [Tooltip("El FragmentCounter de la escena (el mismo al que llaman los fragmentos).")]
    [SerializeField] private FragmentCounter _counter;

    [Tooltip("Tildado: mirar este objeto intenta entregar. Destildado: solo entrega por TryDeliver() (por ejemplo desde el On Player Arrived del tp del centro).")]
    [SerializeField] private bool _deliverOnGaze = true;

    [Header("Aviso si faltan fragmentos (opcional)")]
    [Tooltip("Cartel que se muestra unos segundos si todavia faltan fragmentos. Arranca desactivado.")]
    [SerializeField] private GameObject _missingSign;

    [Tooltip("Texto TMP dentro del cartel, para escribir cuantos faltan. Opcional.")]
    [SerializeField] private TMP_Text _missingLabel;

    [Tooltip("{0} = cuantos faltan, {1} = total.")]
    [SerializeField] private string _missingFormat = "Faltan {0} pedazos de la nota";

    [Tooltip("Segundos que queda visible el cartel.")]
    [SerializeField] private float _signDuration = 2.5f;

    [Header("Audio (opcional)")]
    [SerializeField] private AudioSource _audioSource;

    [Tooltip("Al entregar los fragmentos.")]
    [SerializeField] private AudioClip _deliverSound;

    [Tooltip("Si todavia faltan fragmentos.")]
    [SerializeField] private AudioClip _notReadySound;

    [Header("Feedback de mirada (opcional)")]
    [SerializeField] private Material _inactiveMaterial;
    [SerializeField] private Material _gazedAtMaterial;

    [Header("Eventos")]
    [Tooltip("Se dispara UNA vez, al entregar con todos los fragmentos.")]
    [SerializeField] private UnityEvent _onDelivered;

    [Tooltip("Se dispara cada vez que se intenta entregar sin tener todos.")]
    [SerializeField] private UnityEvent _onNotReady;

    public bool IsDelivered { get; private set; }

    private Renderer _renderer;
    private Coroutine _signRoutine;
    private bool _hasTriedThisGaze;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        SetGazed(false);

        if (_missingSign != null)
        {
            _missingSign.SetActive(false);
        }
    }

    public void OnGazeEnter()
    {
        if (!IsDelivered)
        {
            SetGazed(true);
        }
    }

    public void OnGazeStay(float progress)
    {
        // El feedback de progreso ya lo muestra GazeReticle.
    }

    public void OnGazeExit()
    {
        _hasTriedThisGaze = false;
        SetGazed(false);
    }

    public void OnGazeSelect()
    {
        // GazeController vuelve a disparar OnGazeSelect cada 2 segundos si se
        // sigue mirando: un intento por mirada (hay que apartar la vista para
        // reintentar), asi el aviso de "faltan" no se repite en loop.
        if (!_deliverOnGaze || _hasTriedThisGaze)
        {
            return;
        }

        _hasTriedThisGaze = true;
        TryDeliver();
    }

    /// <summary>
    /// Entrega si ya estan todos los fragmentos; si no, muestra el aviso.
    /// Llamable desde un UnityEvent (TeleportPoint.On Player Arrived).
    /// </summary>
    public void TryDeliver()
    {
        if (IsDelivered)
        {
            return;
        }

        if (_counter == null)
        {
            Debug.LogWarning($"[FragmentDropOff] {gameObject.name}: falta asignar Counter en el Inspector.");
            return;
        }

        if (!_counter.HasAll)
        {
            Debug.Log($"[FragmentDropOff] Faltan {_counter.Missing} fragmentos.");
            Play(_notReadySound);
            ShowMissingSign();
            _onNotReady?.Invoke();
            return;
        }

        IsDelivered = true;
        SetGazed(false);
        Play(_deliverSound);
        Debug.Log("[FragmentDropOff] Fragmentos entregados.");
        _onDelivered?.Invoke();
    }

    private void ShowMissingSign()
    {
        if (_missingSign == null)
        {
            return;
        }

        if (_missingLabel != null && !string.IsNullOrEmpty(_missingFormat))
        {
            try
            {
                _missingLabel.text = string.Format(_missingFormat, _counter.Missing, _counter.RequiredCount);
            }
            catch (System.FormatException)
            {
                _missingLabel.text = _missingFormat;
            }
        }

        if (_signRoutine != null)
        {
            StopCoroutine(_signRoutine);
        }
        _signRoutine = StartCoroutine(SignRoutine());
    }

    private IEnumerator SignRoutine()
    {
        _missingSign.SetActive(true);
        yield return new WaitForSeconds(_signDuration);
        _missingSign.SetActive(false);
        _signRoutine = null;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
