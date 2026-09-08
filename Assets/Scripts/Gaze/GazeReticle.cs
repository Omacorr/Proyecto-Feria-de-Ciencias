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

    // Escala original de cada grafico al arrancar. El menu de configuracion
    // multiplica SOBRE esto (no sobre el valor ya escalado), asi cambiar de
    // preset varias veces no acumula. Solo se tocan estos dos graficos: el
    // fundido a negro y cualquier otra cosa del canvas quedan intactos.
    private Vector3 _baseProgressScale = Vector3.one;
    private Vector3 _baseDotScale = Vector3.one;
    private float _sizeMultiplier = 1f;

    private void Awake()
    {
        if (_progressImage != null)
        {
            _baseProgressScale = _progressImage.rectTransform.localScale;
        }
        if (_dotImage != null)
        {
            _baseDotScale = _dotImage.rectTransform.localScale;
        }
    }

    /// <summary>
    /// Ajusta el tamaño del reticulo como multiplo de su tamaño original de
    /// escena. Lo usa GameSettings.Apply() con el preset elegido en el menu de
    /// configuracion.
    /// </summary>
    public void SetSizeMultiplier(float multiplier)
    {
        _sizeMultiplier = Mathf.Max(0.1f, multiplier);

        if (_progressImage != null)
        {
            _progressImage.rectTransform.localScale = _baseProgressScale * _sizeMultiplier;
        }
        // El punto tambien se re-escala en Update (tiene un latido al mirar
        // algo), pero lo dejamos consistente aca por si Update todavia no corrio.
        if (_dotImage != null)
        {
            _dotImage.rectTransform.localScale = _baseDotScale * _sizeMultiplier;
        }
    }

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
            // Latido: el punto crece un poco cuando hay algo interactivo en la
            // mira. Se combina con el multiplicador de tamaño elegido en
            // configuracion (por eso se parte de _baseDotScale y no de one).
            float pulse = isGazingAtSomething ? 1.3f : 1f;
            _dotImage.rectTransform.localScale = _baseDotScale * (_sizeMultiplier * pulse);
        }
    }
}
