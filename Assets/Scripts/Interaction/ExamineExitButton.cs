using UnityEngine;

/// <summary>
/// Boton/icono de "volver" que solo existe mientras el jugador esta
/// examinando el candado de cerca. Soluciona un problema real: antes, la
/// UNICA forma de que ExamineTrigger.ReturnToPreviousPoint() se llamara era
/// resolviendo el CodeLock (CodeLock.OnUnlocked -> ReturnToPreviousPoint) -
/// si el jugador entraba a examinar y se queria ir SIN terminar de resolver
/// el candado, el Collider de BaseLP (que ExamineTrigger apaga al entrar,
/// para que no tape a los CodeDigit) se quedaba apagado para siempre, y no
/// habia forma de volver a entrar en modo examinar.
///
/// Este objeto se activa/desactiva con los mismos eventos On Examine
/// Start/On Examine End que ya tiene ExamineTrigger (agregale una fila mas
/// a cada uno con GameObject.SetActive sobre este objeto) - asi solo es
/// visible/interactuable mientras se esta examinando. Al mirarlo, llama
/// directamente a ReturnToPreviousPoint(), sin pasar por el CodeLock.
/// </summary>
public class ExamineExitButton : MonoBehaviour, IGazeInteractable
{
    [Tooltip("El ExamineTrigger del candado (el que esta en BaseLP).")]
    [SerializeField] private ExamineTrigger _examineTrigger;

    [Header("Feedback de mirada (opcional)")]
    [SerializeField] private Material _inactiveMaterial;
    [SerializeField] private Material _gazedAtMaterial;

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
        // El feedback de progreso ya lo muestra GazeReticle.
    }

    public void OnGazeExit()
    {
        SetGazed(false);
    }

    public void OnGazeSelect()
    {
        _examineTrigger?.ReturnToPreviousPoint();
    }

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
