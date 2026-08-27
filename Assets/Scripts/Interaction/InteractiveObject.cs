using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Etapa 4: objeto interactivo generico. Cambia de material mientras esta en la
/// mira, y dispara un evento configurable cuando se lo selecciona (mirada
/// sostenida). Pensado para botones, palancas o cualquier elemento "activable"
/// que no se consume al usarlo (a diferencia de Collectable).
/// </summary>
public class InteractiveObject : MonoBehaviour, IGazeInteractable
{
    [Tooltip("Material cuando NO se esta mirando el objeto.")]
    [SerializeField] private Material _inactiveMaterial;

    [Tooltip("Material cuando SI se esta mirando el objeto.")]
    [SerializeField] private Material _gazedAtMaterial;

    [Tooltip("Se dispara cada vez que se mantiene la mirada el tiempo suficiente.")]
    [SerializeField] private UnityEvent _onSelected;

    private Renderer _renderer;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        SetGazed(false);
    }

    public void OnGazeEnter()
    {
        SetGazed(true);
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
        Debug.Log($"[InteractiveObject] Seleccionado: {gameObject.name}");
        _onSelected?.Invoke();
    }

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
