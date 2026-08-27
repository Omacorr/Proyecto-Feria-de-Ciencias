using UnityEngine;

/// <summary>
/// Etapas 2 y 3 del MVP: Raycast + gaze, y estados Enter/Stay/Exit/Select.
/// GazeController solo detecta que se esta mirando y durante cuanto tiempo -
/// no decide que hace cada objeto. Eso lo define cada componente que implemente
/// IGazeInteractable (etapa 4 en adelante).
/// </summary>
public class GazeController : MonoBehaviour
{
    [Tooltip("Camara desde la que se dispara el rayo. Si se deja vacio, usa Camera.main.")]
    [SerializeField] private Camera _gazeCamera;

    [Tooltip("Layers que puede detectar el gaze. Deberia incluir la layer Interactive.")]
    [SerializeField] private LayerMask _interactiveLayerMask;

    [Tooltip("Distancia maxima del raycast, en metros.")]
    [SerializeField] private float _maxGazeDistance = 10f;

    [Tooltip("Segundos que hay que mantener la mirada sobre un objeto para seleccionarlo.")]
    [SerializeField] private float _gazeSelectDuration = 2f;

    // Objeto que se esta mirando actualmente (null si no hay nada en la mira).
    public GameObject CurrentGazedObject { get; private set; }

    // Progreso actual hacia la seleccion (0 a 1). Publico para que la etapa 5
    // (reticulo) lo pueda leer sin depender de eventos.
    public float GazeProgress { get; private set; }

    private IGazeInteractable _currentInteractable;
    private float _gazeTimer;

    private void Awake()
    {
        if (_gazeCamera == null)
        {
            _gazeCamera = Camera.main;
        }
    }

    private void Update()
    {
        GameObject hitObject = null;

        Ray ray = new Ray(_gazeCamera.transform.position, _gazeCamera.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, _maxGazeDistance, _interactiveLayerMask))
        {
            hitObject = hit.collider.gameObject;
        }

        if (hitObject != CurrentGazedObject)
        {
            HandleGazeChanged(hitObject);
        }
        else if (CurrentGazedObject != null)
        {
            HandleGazeStay();
        }
    }

    private void HandleGazeChanged(GameObject newObject)
    {
        // Salio del objeto anterior.
        if (_currentInteractable != null)
        {
            _currentInteractable.OnGazeExit();
        }

        CurrentGazedObject = newObject;
        _gazeTimer = 0f;
        GazeProgress = 0f;
        _currentInteractable = newObject != null
            ? newObject.GetComponent<IGazeInteractable>()
            : null;

        // Entro al objeto nuevo.
        if (_currentInteractable != null)
        {
            _currentInteractable.OnGazeEnter();
        }
    }

    private void HandleGazeStay()
    {
        _gazeTimer += Time.deltaTime;
        GazeProgress = Mathf.Clamp01(_gazeTimer / _gazeSelectDuration);

        if (_currentInteractable == null)
        {
            return;
        }

        _currentInteractable.OnGazeStay(GazeProgress);

        if (GazeProgress >= 1f)
        {
            _currentInteractable.OnGazeSelect();

            // Reinicia el timer para permitir otra seleccion si se sigue mirando
            // (cada IGazeInteractable decide si le importa o si la ignora, por
            // ejemplo TeleportManager mientras esta teletransportando).
            _gazeTimer = 0f;
            GazeProgress = 0f;
        }
    }
}
