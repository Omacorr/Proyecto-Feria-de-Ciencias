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

    [Tooltip("Opcional. Layers que TAPAN la mirada sin ser interactivas (por ejemplo paredes con MeshCollider). Si lo primero que toca el rayo esta en una de estas layers, se considera que no se mira nada. Sin esto el rayo atraviesa las paredes (los modelos importados no traen collider) y se pueden elegir puntos que estan del otro lado de una pared. Nothing (por defecto) = comportamiento de siempre.")]
    [SerializeField] private LayerMask _occluderLayerMask;

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

    private void OnDisable()
    {
        // FaintTransition / AbyssFall apagan este componente para bloquear la
        // mirada durante un desmayo o una caida. Sin este reset, el objeto que
        // se estaba mirando quedaba "congelado" como mirado (sin OnGazeExit) y
        // el aro de GazeReticle quedaba a medio llenar en pantalla.
        ResetGaze();
    }

    /// <summary>
    /// Olvida el objeto mirado actual (le manda OnGazeExit) y pone el progreso
    /// en 0. En el frame siguiente, si se sigue mirando lo mismo, se vuelve a
    /// detectar como una mirada NUEVA (OnGazeEnter + timer desde cero). Lo usa
    /// TeleportManager despues de un "loop sin fade": el jugador aparece
    /// mirando otra vez el mismo punto, y sin esto el timer seguia corriendo y
    /// lo volvia a seleccionar solo, sin que el jugador lo decidiera.
    /// </summary>
    public void ResetGaze()
    {
        IGazeInteractable previous = _currentInteractable;
        _currentInteractable = null;
        CurrentGazedObject = null;
        _gazeTimer = 0f;
        GazeProgress = 0f;

        if (previous == null)
        {
            return;
        }

        // Si el interactuable es un componente ya destruido (por ejemplo al
        // descargar la escena), no se le puede avisar nada.
        if (previous is Object unityObject && unityObject == null)
        {
            return;
        }

        previous.OnGazeExit();
    }

    private void Update()
    {
        if (_gazeCamera == null)
        {
            // Camera.main puede no existir todavia en Awake (o haber cambiado
            // tras un cambio de escena): se reintenta en vez de tirar
            // NullReferenceException en cada frame.
            _gazeCamera = Camera.main;
            if (_gazeCamera == null)
            {
                return;
            }
        }

        GameObject hitObject = null;

        Ray ray = new Ray(_gazeCamera.transform.position, _gazeCamera.transform.forward);
        // Con Occluder Layer Mask en Nothing, la mascara es exactamente la de
        // siempre. Si hay oclusores, un hit en una layer que NO es interactiva
        // significa que hay una pared en el medio: no se mira nada.
        int mask = _interactiveLayerMask.value | _occluderLayerMask.value;
        if (Physics.Raycast(ray, out RaycastHit hit, _maxGazeDistance, mask))
        {
            GameObject hitGo = hit.collider.gameObject;
            if ((_interactiveLayerMask.value & (1 << hitGo.layer)) != 0)
            {
                hitObject = hitGo;
            }
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
        // GetComponentInParent (no GetComponent) porque en mallas importadas
        // el Collider que golpea el rayo suele estar en un objeto hijo
        // distinto de donde pusimos el script interactuable (por ejemplo,
        // Door en el padre de la puerta+vidrio). GetComponentInParent
        // revisa el propio objeto primero y despues sube por la jerarquia,
        // asi que sigue encontrando todo lo que ya funcionaba antes.
        _currentInteractable = newObject != null
            ? newObject.GetComponentInParent<IGazeInteractable>()
            : null;

        // Entro al objeto nuevo.
        if (_currentInteractable != null)
        {
            _currentInteractable.OnGazeEnter();
        }
    }

    /// <summary>
    /// Cambia en runtime los segundos de mirada sostenida necesarios para
    /// seleccionar. Lo usa GameSettings.Apply() con el preset elegido en el menu
    /// de configuracion. Ignora valores &lt;= 0 para no dejar el gaze en un
    /// estado donde selecciona al instante o nunca.
    /// </summary>
    public void SetSelectDuration(float seconds)
    {
        if (seconds > 0f)
        {
            _gazeSelectDuration = seconds;
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
