using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Etapa 4: objeto interactivo generico. Cambia de material mientras esta en la
/// mira, y dispara un evento configurable cuando se lo selecciona (mirada
/// sostenida). Pensado para botones, palancas o cualquier elemento "activable"
/// que no se consume al usarlo (a diferencia de Collectable).
///
/// Sonidos opcionales: un "hover" al posar la mirada y un "select" al activar.
/// Si se asigna un AudioSource, se usa PlayOneShot; si no, no suena nada.
/// </summary>
public class InteractiveObject : MonoBehaviour, IGazeInteractable
{
    [Tooltip("Material cuando NO se esta mirando el objeto.")]
    [SerializeField] private Material _inactiveMaterial;

    [Tooltip("Material cuando SI se esta mirando el objeto.")]
    [SerializeField] private Material _gazedAtMaterial;

    [Tooltip("Se dispara cada vez que se mantiene la mirada el tiempo suficiente.")]
    [SerializeField] private UnityEvent _onSelected;

    [Header("Sonidos (opcional)")]
    [Tooltip("AudioSource por el que suenan los clips (2D). Si se deja vacio, no hay sonido.")]
    [SerializeField] private AudioSource _audioSource;

    [Tooltip("Sonido corto al posar la mirada sobre el objeto.")]
    [SerializeField] private AudioClip _hoverSound;

    [Tooltip("Sonido al activar el objeto (mirada sostenida).")]
    [SerializeField] private AudioClip _selectSound;

    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        SetGazed(false);
    }

    public void OnGazeEnter()
    {
        SetGazed(true);
        Play(_hoverSound);
    }

    public void OnGazeStay(float progress)
    {
        // El feedback visual de progreso (reticulo) se agrega en la etapa 5.
    }

    public void OnGazeExit()
    {
        SetGazed(false);
    }

    public void OnGazeSelect()
    {
        Play(_selectSound);
        _onSelected?.Invoke();
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
