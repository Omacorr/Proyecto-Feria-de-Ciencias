using UnityEngine;

/// <summary>
/// Boton de "volver" que solo existe mientras el jugador examina el candado
/// de cerca. Sin el, quien entraba a examinar y se queria ir SIN resolver el
/// codigo quedaba trabado: ExamineTrigger apaga el Collider de BaseLP al
/// entrar (para no tapar a los CodeDigit) y solo volvia a prenderlo al
/// resolver el CodeLock.
///
/// Se activa/desactiva con los eventos On Examine Start/End de ExamineTrigger
/// (una fila SetActive sobre este objeto en cada uno). Al mirarlo llama a
/// ReturnToPreviousPoint() sin pasar por el CodeLock.
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

    public void OnGazeEnter() => SetGazed(true);
    public void OnGazeStay(float progress) { }
    public void OnGazeExit() => SetGazed(false);

    public void OnGazeSelect()
    {
        _examineTrigger?.ReturnToPreviousPoint();
    }

    private void SetGazed(bool gazedAt) => GazeHighlight.Swap(_renderer, _inactiveMaterial, _gazedAtMaterial, gazedAt);
}
