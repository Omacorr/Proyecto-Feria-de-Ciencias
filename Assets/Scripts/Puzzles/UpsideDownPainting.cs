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
///    el script lo da vuelta solo al arrancar ("Authored Upright" tildado).
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

    [Tooltip("Tildado (recomendado): en el Editor el cuadro esta DERECHO y el script lo pone al reves al arrancar. Destildado: ya lo dejaste al reves a mano en el Editor.")]
    [SerializeField] private bool _authoredUpright = true;

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

    [Header("Uso")]
    [Tooltip("Distancia maxima (horizontal) entre la camara y el cuadro para poder enderezarlo. 0 = sin limite (alcanza con que llegue el gaze).")]
    [SerializeField] private float _maxUseDistance = 0f;

    [Tooltip("Main Camera. Solo hace falta si se usa Max Use Distance; si queda vacio usa Camera.main.")]
    [SerializeField] private Transform _cameraTransform;

    [Tooltip("Tildado: al quedar derecho se apagan sus Collider, asi el aro del reticle no se vuelve a llenar sobre un cuadro que ya no hace nada.")]
    [SerializeField] private bool _disableCollidersWhenDone = true;

    [Header("Audio (opcional)")]
    [Tooltip("AudioSource desde donde suenan los clips (puede estar en el propio cuadro).")]
    [SerializeField] private AudioSource _audioSource;

    [Tooltip("Sonido al empezar a girar (roce de madera, clavo).")]
    [SerializeField] private AudioClip _straightenSound;

    [Tooltip("Sonido al quedar derecho (golpe seco contra la pared).")]
    [SerializeField] private AudioClip _settledSound;

    [Header("Eventos")]
    [Tooltip("Se dispara apenas empieza a girar.")]
    [SerializeField] private UnityEvent _onStraightenStart;

    [Tooltip("Se dispara UNA vez, cuando termina de quedar derecho. Ej: tp pared ciega -> GameObject.SetActive(true).")]
    [SerializeField] private UnityEvent _onStraightened;

    /// <summary>True una vez que el cuadro termino de quedar derecho.</summary>
    public bool IsStraightened { get; private set; }

    private Quaternion _uprightRotation;
    private Vector3 _axis;
    private bool _isAnimating;
    private bool _isGazed;
    private float _trembleAmount;

    private void Awake()
    {
        _axis = _flipAxis.sqrMagnitude > 0.0001f ? _flipAxis.normalized : Vector3.forward;

        // Guardamos la rotacion "derecha" de referencia; todo lo demas se
        // calcula como un giro alrededor del eje local a partir de ella.
        _uprightRotation = _authoredUpright
            ? transform.localRotation
            : transform.localRotation * Quaternion.AngleAxis(-_flipDegrees, _axis);

        IsStraightened = false;
        ApplyAngle(_flipDegrees);
    }

    private void Update()
    {
        if (IsStraightened || _isAnimating || !_isGazed || _trembleAmount <= 0f)
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

        if (!IsStraightened && !_isAnimating)
        {
            ApplyAngle(_flipDegrees);
        }
    }

    public void OnGazeSelect()
    {
        if (IsStraightened || _isAnimating || !IsPlayerInRange())
        {
            return;
        }

        Straighten();
    }

    /// <summary>
    /// Endereza el cuadro (con animacion). Publica para poder dispararla desde
    /// un UnityEvent sin mirar el cuadro (por ejemplo, para probar).
    /// </summary>
    public void Straighten()
    {
        if (IsStraightened || _isAnimating || !isActiveAndEnabled)
        {
            return;
        }

        StartCoroutine(StraightenRoutine());
    }

    /// <summary>
    /// Lo vuelve a poner al reves, sin animacion, y lo deja listo para
    /// enderezarse otra vez (por ejemplo, si el loop del pasillo "reinicia" la
    /// pintura). No deshace lo que se haya disparado en On Straightened.
    /// </summary>
    public void ResetUpsideDown()
    {
        StopAllCoroutines();
        _isAnimating = false;
        IsStraightened = false;
        _trembleAmount = 0f;
        SetCollidersEnabled(true);
        ApplyAngle(_flipDegrees);
    }

    private IEnumerator StraightenRoutine()
    {
        _isAnimating = true;
        Play(_straightenSound);
        _onStraightenStart?.Invoke();
        Debug.Log($"[UpsideDownPainting] {gameObject.name}: enderezando.");

        float duration = Mathf.Max(0.01f, _straightenDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Arranca lento (como si costara despegarlo), acelera y frena.
            float eased = t * t * (3f - 2f * t);
            ApplyAngle(Mathf.Lerp(_flipDegrees, 0f, eased));
            yield return null;
        }

        ApplyAngle(0f);
        Play(_settledSound);

        if (_settleWobbleDegrees > 0f && _settleWobbleDuration > 0f)
        {
            elapsed = 0f;
            while (elapsed < _settleWobbleDuration)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / _settleWobbleDuration);
                // Dos oscilaciones que se apagan.
                ApplyAngle(_settleWobbleDegrees * (1f - u) * Mathf.Sin(u * Mathf.PI * 4f));
                yield return null;
            }
            ApplyAngle(0f);
        }

        _isAnimating = false;
        IsStraightened = true;

        if (_disableCollidersWhenDone)
        {
            SetCollidersEnabled(false);
        }

        Debug.Log($"[UpsideDownPainting] {gameObject.name}: derecho.");
        _onStraightened?.Invoke();
    }

    private void ApplyAngle(float degrees)
    {
        transform.localRotation = _uprightRotation * Quaternion.AngleAxis(degrees, _axis);
    }

    private bool IsPlayerInRange()
    {
        if (_maxUseDistance <= 0f)
        {
            return true;
        }

        Transform cam = _cameraTransform;
        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.transform;
        }
        if (cam == null)
        {
            return true;
        }

        Vector3 offset = transform.position - cam.position;
        offset.y = 0f;
        return offset.magnitude <= _maxUseDistance;
    }

    private void SetCollidersEnabled(bool on)
    {
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
        {
            col.enabled = on;
        }
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
}
