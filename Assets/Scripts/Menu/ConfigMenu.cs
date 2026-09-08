using UnityEngine;
using TMPro;

/// <summary>
/// Panel de configuracion de la escena "Menu Principal". Seis botones de preset:
/// Bajo / Medio / Alto para "velocidad de carga del gaze", y lo mismo para
/// "tamaño del reticulo". Cada boton es un InteractiveObject que cablea su
/// OnSelected a uno de los metodos publicos de aca.
///
/// Al elegir un preset, GameSettings lo guarda en PlayerPrefs (persiste entre
/// escenas y sesiones) y lo re-aplica al instante a la escena activa, asi el
/// cambio de tamaño de reticulo / velocidad se ve sin salir del menu.
///
/// No hay sliders porque en Cardboard no se puede arrastrar nada: todo es mirar
/// un boton de preset.
/// </summary>
public class ConfigMenu : MonoBehaviour
{
    [Header("Carteles de estado (opcional)")]
    [Tooltip("Texto que muestra el preset actual de velocidad del gaze.")]
    [SerializeField] private TMP_Text _gazeSpeedLabel;

    [Tooltip("Texto que muestra el preset actual de tamaño del reticulo.")]
    [SerializeField] private TMP_Text _reticleSizeLabel;

    private void OnEnable()
    {
        RefreshLabels();
    }

    // --- Velocidad de carga del gaze (cablear cada boton a uno de estos) ---
    public void SetGazeSpeedBajo() { ChangeGazeSpeed(GameSettings.Level.Bajo); }
    public void SetGazeSpeedMedio() { ChangeGazeSpeed(GameSettings.Level.Medio); }
    public void SetGazeSpeedAlto() { ChangeGazeSpeed(GameSettings.Level.Alto); }

    // --- Tamaño del reticulo ---
    public void SetReticleSizeBajo() { ChangeReticleSize(GameSettings.Level.Bajo); }
    public void SetReticleSizeMedio() { ChangeReticleSize(GameSettings.Level.Medio); }
    public void SetReticleSizeAlto() { ChangeReticleSize(GameSettings.Level.Alto); }

    private void ChangeGazeSpeed(GameSettings.Level level)
    {
        GameSettings.GetOrCreate().SetGazeSpeed(level);
        RefreshLabels();
    }

    private void ChangeReticleSize(GameSettings.Level level)
    {
        GameSettings.GetOrCreate().SetReticleSize(level);
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        GameSettings s = GameSettings.GetOrCreate();
        if (_gazeSpeedLabel != null)
        {
            _gazeSpeedLabel.text = $"Velocidad del gaze: {s.GazeSpeed}";
        }
        if (_reticleSizeLabel != null)
        {
            _reticleSizeLabel.text = $"Tamaño del reticulo: {s.ReticleSize}";
        }
    }
}
