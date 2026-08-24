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
        _onCollected?.Invoke();
        gameObject.SetActive(false);
    }

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
