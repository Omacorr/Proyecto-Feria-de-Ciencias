using UnityEngine;

/// <summary>
/// Parte 4: el pasillo se "cierra" con cada vuelta del loop. Cada llamada a
/// Step() sube un nivel (hasta Max Steps): el weight del Volume sube a
/// nivel * Weight Per Step y las luces bajan su intensidad de forma
/// multiplicativa (con un piso). Se cablea desde tp1.On Loop.
/// </summary>
public class LoopDecay : MonoBehaviour
{
    [Tooltip("Volume (oscurecimiento / vineta) que se va sumando.")]
    [SerializeField] private VolumeWeightFade _volume;

    [Tooltip("Luces que se van apagando de a poco.")]
    [SerializeField] private FlickeringLight[] _lights;

    [Min(1)]
    [SerializeField] private int _maxSteps = 5;

    [Range(0f, 1f)]
    [Tooltip("Weight del Volume que se suma por nivel.")]
    [SerializeField] private float _weightPerStep = 0.2f;

    [Range(0f, 1f)]
    [Tooltip("Cada nivel multiplica la intensidad por este valor (0.85 = pierde 15 %).")]
    [SerializeField] private float _lightMultiplierPerStep = 0.85f;

    [Range(0f, 1f)]
    [Tooltip("Piso del multiplicador de las luces.")]
    [SerializeField] private float _minLightMultiplier = 0.45f;

    [Tooltip("Segundos de la transicion de cada paso.")]
    [SerializeField] private float _fadeSeconds = 3f;

    private int _level;

    /// <summary>Nivel actual (0 = sin decaimiento).</summary>
    public int Level { get { return _level; } }

    /// <summary>Un paso mas de decaimiento (llamar en cada vuelta del loop).</summary>
    public void Step()
    {
        if (_level >= _maxSteps)
        {
            return;
        }
        _level++;
        Debug.Log("[LoopDecay] Vuelta " + _level);

        if (_volume != null)
        {
            _volume.SetFadeSeconds(_fadeSeconds);
            _volume.FadeTo(_level * _weightPerStep);
        }

        float multiplier = Mathf.Max(_minLightMultiplier, Mathf.Pow(_lightMultiplierPerStep, _level));
        if (_lights != null)
        {
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] != null)
                {
                    _lights[i].FadeIntensityMultiplier(multiplier, _fadeSeconds);
                }
            }
        }
    }

    [ContextMenu("Probar paso (solo en Play)")]
    private void TestStep()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[LoopDecay] Solo funciona en Play.");
            return;
        }
        Step();
    }
}
