using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Pintura colgada al reves (Etapa 4, "Pasillo de madera"). Al mirarla, el
/// cuadro empieza a temblar cada vez mas fuerte mientras se llena el aro del
/// reticle; al completarse la mirada sostenida gira suave hasta quedar derecho,
/// se asienta con un pequeno vaiven y dispara On Straightened (por ejemplo:
/// activar "tp pared ciega", que tiene que arrancar desactivado).
///
/// Uso recomendado en la escena:
///  - Un objeto VACIO como raiz (escala 1,1,1), con el PIVOTE en el CENTRO del
///    cuadro (gira alrededor de ese punto), con este script + un BoxCollider del
///    tamano del cuadro + Layer Interactive (7).
///  - El modelo visual (marco + lienzo) como HIJO, centrado en la raiz.
///  - En el Editor se deja el cuadro DERECHO (se ve bien la imagen al ubicarlo):
///    el script lo da vuelta solo al arrancar.
///
/// El giro es alrededor de un eje LOCAL (Flip Axis), por defecto Z: el eje que
/// atraviesa el lienzo de frente. Si al probar gira "de costado" en vez de
/// girar en el plano de la pared, cambiar Flip Axis (igual que CodeDigit).
/// </summary>
public class UpsideDownPainting : MonoBehaviour, IGazeInteractable
{
    [Header("Giro")]
    [Tooltip("Eje LOCAL perpendicular al lienzo (el que 'atraviesa' la pared). Con un objeto raiz cuyo eje Z azul apunta hacia la pared (o hacia afuera), es (0, 0, 1).")]
    [SerializeField] private Vector3 _flipAxis = Vector3.forward;

    [Tooltip("Grados que esta girado el cuadro cuando esta 'al reves'. 180 = cabeza abajo.")]
    [SerializeField] private float _flipDegrees = 180f;

    [Tooltip("Tildado: arranca DERECHO y sin collider (no se puede usar) hasta que alguien llame TurnUpsideDown(). Sirve para que el cuadro recien 'se de vuelta' despues de algo (en Parte 4, la primera vuelta del loop).")]
    [SerializeField] private bool _startUpright = false;

    [Header("Animacion")]
    [Tooltip("Segundos que tarda en enderezarse.")]
    [SerializeField] private float _straightenDuration = 1.8f;

    [Tooltip("Vaiven al terminar de enderezarse, en grados (0 = sin vaiven).")]
    [SerializeField] private float _settleWobbleDegrees = 5f;

    [Tooltip("Duracion del vaiven final, en segundos.")]
    [SerializeField] private float _settleWobbleDuration = 0.9f;

    [Tooltip("Temblor maximo (grados) mientras se sostiene la mirada, crece con el aro del reticle. 0 = sin temblor.")]
    [SerializeField] private float _gazeTrembleDegrees = 2.5f;

    [Tooltip("Velocidad del temblor.")]
    [SerializeField] private float _trembleSpeed = 18f;

    [Header("Eventos")]
    [Tooltip("Se dispara apenas empieza a girar.")]
    [SerializeField] private UnityEvent _onStraightenStart;

    [Tooltip("Se dispara UNA vez, cuando termina de quedar derecho. Ej: tp pared ciega -> GameObject.SetActive(true).")]
    [SerializeField] private UnityEvent _onStraightened;

    private bool _straightened;
    private Quaternion _uprightRotation;
    private Vector3 _axis;
    private bool _isAnimating;
    private bool _isGazed;
    private float _trembleAmount;
    // Start Upright: todavia derecho y sin usar, esperando TurnUpsideDown().
    private bool _isDormant;

    private void Awake()
    {
        _axis = _flipAxis.sqrMagnitude > 0.0001f ? _flipAxis.normalized : Vector3.forward;

        // Rotacion "derecha" de referencia; todo lo demas es un giro alrededor
        // del eje local a partir de ella.
        _uprightRotation = transform.localRotation;

        _isDormant = _startUpright;
        ApplyAngle(_startUpright ? 0f : _flipDegrees);
        if (_startUpright)
        {
            SetCollidersEnabled(false);
        }
    }

    /// <summary>
    /// Con Start Upright: el cuadro se da vuelta de golpe y pasa a poder
    /// enderezarse. Solo la primera vez; despues no hace nada.
    /// </summary>
    public void TurnUpsideDown()
    {
        if (!_isDormant)
        {
            return;
        }
        _isDormant = false;
        SetCollidersEnabled(true);
        ApplyAngle(_flipDegrees);
        Debug.Log($"[UpsideDownPainting] {gameObject.name}: se dio vuelta.");
    }

    private void Update()
    {
        if (_straightened || _isAnimating || !_isGazed || _trembleAmount <= 0f)
        {
            return;
        }

        float noise = (Mathf.PerlinNoise(Time.time * _trembleSpeed, 0.37f) - 0.5f) * 2f;
        ApplyAngle(_flipDegrees + noise * _trembleAmount);
    }

    public void OnGazeEnter()
    {
        _isGazed = true;
        _trembleAmount = 0f;
    }

    public void OnGazeStay(float progress)
    {
        _trembleAmount = Mathf.Clamp01(progress) * _gazeTrembleDegrees;
    }

    public void OnGazeExit()
    {
        _isGazed = false;
        _trembleAmount = 0f;

        if (!_straightened && !_isAnimating && !_isDormant)
        {
            ApplyAngle(_flipDegrees);
        }
    }

    public void OnGazeSelect()
    {
        Straighten();
    }

    /// <summary>
    /// Endereza el cuadro (con animacion). Publica para poder dispararla desde
    /// un UnityEvent sin mirar el cuadro (por ejemplo, para probar).
    /// </summary>
    public void Straighten()
    {
        if (_isDormant || _straightened || _isAnimating || !isActiveAndEnabled)
        {
            return;
        }

        StartCoroutine(StraightenRoutine());
    }

    private IEnumerator StraightenRoutine()
    {
        _isAnimating = true;
        _onStraightenStart?.Invoke();
        Debug.Log($"[UpsideDownPainting] {gameObject.name}: enderezando.");

        float duration = Mathf.Max(0.01f, _straightenDuration);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            // Arranca lento (como si costara despegarlo), acelera y frena.
            ApplyAngle(Mathf.Lerp(_flipDegrees, 0f, t * t * (3f - 2f * t)));
            yield return null;
        }

        ApplyAngle(0f);
        if (_settleWobbleDegrees > 0f && _settleWobbleDuration > 0f)
        {
            for (float elapsed = 0f; elapsed < _settleWobbleDuration; elapsed += Time.deltaTime)
            {
                float u = Mathf.Clamp01(elapsed / _settleWobbleDuration);
                // Dos oscilaciones que se apagan.
                ApplyAngle(_settleWobbleDegrees * (1f - u) * Mathf.Sin(u * Mathf.PI * 4f));
                yield return null;
            }
            ApplyAngle(0f);
        }

        _isAnimating = false;
        _straightened = true;
        // Sin collider el aro del reticle no se vuelve a llenar sobre un cuadro que ya no hace nada.
        SetCollidersEnabled(false);

        Debug.Log($"[UpsideDownPainting] {gameObject.name}: derecho.");
        _onStraightened?.Invoke();
    }

    private void ApplyAngle(float degrees)
    {
        transform.localRotation = _uprightRotation * Quaternion.AngleAxis(degrees, _axis);
    }

    private void SetCollidersEnabled(bool on)
    {
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            col.enabled = on;
        }
    }
}
