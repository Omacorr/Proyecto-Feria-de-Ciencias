using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Etapa 5: feedback visual del gaze. Un punto fijo en el centro de la vista
/// (siempre visible) y, cuando hay un objeto interactivo en la mira, un anillo
/// que se va llenando segun el progreso hacia la seleccion (0 a 1), leido
/// directamente de GazeController. No decide nada de logica, solo muestra.
/// </summary>
public class GazeReticle : MonoBehaviour
{
    [Tooltip("GazeController del que se lee el progreso y el objeto mirado.")]
    [SerializeField] private GazeController _gazeController;

    [Tooltip("Imagen tipo Filled (Radial 360) que se va llenando con el progreso.")]
    [SerializeField] private Image _progressImage;

    [Tooltip("Punto central que siempre esta visible, mirando algo interactivo o no.")]
    [SerializeField] private Image _dotImage;

    private void Update()
    {
        if (_gazeController == null)
        {
            return;
        }

        bool isGazingAtSomething = _gazeController.CurrentGazedObject != null;

        if (_progressImage != null)
        {
            _progressImage.enabled = isGazingAtSomething;
            _progressImage.fillAmount = isGazingAtSomething ? _gazeController.GazeProgress : 0f;
        }

        if (_dotImage != null)
        {
            // Refuerza el feedback: el punto crece un poco cuando hay algo
            // interactivo en la mira, ademas del anillo llenandose.
            _dotImage.transform.localScale = isGazingAtSomething
                ? Vector3.one * 1.3f
                : Vector3.one;
        }
    }
}
