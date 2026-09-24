using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Etapa 4: objeto recolectable. A diferencia de InteractiveObject, al
/// seleccionarlo se "recolecta": se desactiva una sola vez y avisa mediante un
/// evento, para que mas adelante (etapa 9) un manager cuente cuantos se juntaron.
/// </summary>
public class Collectable : MonoBehaviour, IGazeInteractable
{
    [Tooltip("Material cuando NO se esta mirando el objeto.")]
    [SerializeField] private Material _inactiveMaterial;

    [Tooltip("Material cuando SI se esta mirando el objeto.")]
    [SerializeField] private Material _gazedAtMaterial;

    [Tooltip("Se dispara una sola vez, cuando el objeto se recolecta.")]
    [SerializeField] private UnityEvent _onCollected;

    [Header("Sonido (opcional)")]
    [Tooltip("AudioSource desde donde suena el clip al recolectar. Conviene que este en OTRO objeto (este se desactiva al recolectarse y cortaria el sonido); si igual esta en este objeto o en un hijo, el clip se reproduce en la posicion del objeto con AudioSource.PlayClipAtPoint. Si falta el AudioSource o el clip, no suena nada.")]
    [SerializeField] private AudioSource _audioSource;

    [Tooltip("Sonido al recolectar (por ejemplo, llave o papel).")]
    [SerializeField] private AudioClip _collectSound;

    public bool IsCollected { get; private set; }

    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        SetGazed(false);
    }

    public void OnGazeEnter()
    {
        if (!IsCollected)
        {
            SetGazed(true);
        }
    }

    public void OnGazeStay(float progress)
    {
        // El feedback visual de progreso (reticulo) se agrega en la etapa 5.
    }

    public void OnGazeExit()
    {
        if (!IsCollected)
        {
            SetGazed(false);
        }
    }

    public void OnGazeSelect()
    {
        if (IsCollected)
        {
            return;
        }

        IsCollected = true;
        Debug.Log($"[Collectable] Recolectado: {gameObject.name}");
        PlayCollectSound();
        _onCollected?.Invoke();
        gameObject.SetActive(false);
    }

    private void PlayCollectSound()
    {
        if (_audioSource == null || _collectSound == null)
        {
            return;
        }

        // Si el AudioSource vive en este mismo objeto (o en un hijo), se apaga
        // junto con el en la linea siguiente y el sonido se cortaria.
        if (_audioSource.transform.IsChildOf(transform))
        {
            AudioSource.PlayClipAtPoint(_collectSound, transform.position, _audioSource.volume);
        }
        else
        {
            _audioSource.PlayOneShot(_collectSound);
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
